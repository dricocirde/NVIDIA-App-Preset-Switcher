using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Text;

// The gamma-curve construction in SetGammaRamp is derived from the independently
// published NvColorProfiles gamma_ramp implementation by 0skater0, which credits
// WindowsDisplayAPI by Soroush Falahati. That method is distributed under
// LGPL-3.0-or-later; see COPYING.LESSER and THIRD_PARTY_NOTICES.md.

namespace NvidiaPresetSwitcher
{
    internal sealed class NvApi
    {
        [DllImport("nvapi64.dll", EntryPoint = "nvapi_QueryInterface", CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr QueryInterface(uint id);

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int InitializeDelegate();
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int EnumDisplayDelegate(int index, out IntPtr displayHandle);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)] private delegate int GetDisplayNameDelegate(IntPtr displayHandle, StringBuilder displayName);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetDvcDelegate(IntPtr displayHandle, uint outputId, ref DvcInfo info);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int SetDvcDelegate(IntPtr displayHandle, uint outputId, ref DvcInfo info);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int GetHueDelegate(IntPtr displayHandle, uint outputId, ref HueInfo info);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int SetHueDelegate(IntPtr displayHandle, uint outputId, uint hueAngle);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet = CharSet.Ansi)] private delegate int GetDisplayIdDelegate(string displayName, out uint displayId);
        [UnmanagedFunctionPointer(CallingConvention.Cdecl)] private delegate int SetTargetGammaDelegate(uint displayId, IntPtr data);

        [StructLayout(LayoutKind.Sequential)]
        private struct DvcInfo
        {
            public uint Version;
            public int CurrentLevel;
            public int MinLevel;
            public int MaxLevel;
            public int DefaultLevel;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct HueInfo
        {
            public uint Version;
            public uint CurrentAngle;
            public uint DefaultAngle;
        }

        internal sealed class NvDisplay
        {
            public int Index;
            public IntPtr Handle;
            public string DeviceName;
            public uint DisplayId;
            public int? DigitalVibrance;
            public int? Hue;
        }

        private readonly EnumDisplayDelegate _enumerate;
        private readonly GetDisplayNameDelegate _getName;
        private readonly GetDvcDelegate _getDvc;
        private readonly SetDvcDelegate _setDvc;
        private readonly GetHueDelegate _getHue;
        private readonly SetHueDelegate _setHue;
        private readonly GetDisplayIdDelegate _getDisplayId;
        private readonly SetTargetGammaDelegate _setTargetGamma;

        public NvApi()
        {
            int status = GetDelegate<InitializeDelegate>(0x0150E828)();
            if (status != 0) throw new InvalidOperationException("NVAPI 初始化失败，状态码 " + status);
            _enumerate = GetDelegate<EnumDisplayDelegate>(0x9ABDD40D);
            _getName = GetDelegate<GetDisplayNameDelegate>(0x22A78B05);
            _getDvc = GetDelegate<GetDvcDelegate>(0x0E45002D);
            _setDvc = GetDelegate<SetDvcDelegate>(0x4A82C2B1);
            _getHue = GetDelegate<GetHueDelegate>(0x95B64341);
            _setHue = GetDelegate<SetHueDelegate>(0xF5A0F22C);
            _getDisplayId = GetDelegate<GetDisplayIdDelegate>(0xAE457190);
            _setTargetGamma = GetDelegate<SetTargetGammaDelegate>(0x7082A053);
        }

        public List<NvDisplay> ReadDisplays()
        {
            List<NvDisplay> result = new List<NvDisplay>();
            for (int index = 0; index < 64; index++)
            {
                IntPtr handle;
                if (_enumerate(index, out handle) != 0) break;
                StringBuilder name = new StringBuilder(64);
                _getName(handle, name);

                NvDisplay display = new NvDisplay();
                display.Index = index;
                display.Handle = handle;
                display.DeviceName = name.ToString();
                uint displayId;
                if (_getDisplayId(display.DeviceName, out displayId) == 0) display.DisplayId = displayId;

                DvcInfo dvc = NewDvcInfo();
                if (_getDvc(handle, 0, ref dvc) == 0) display.DigitalVibrance = dvc.CurrentLevel;

                HueInfo hue = NewHueInfo();
                if (_getHue(handle, 0, ref hue) == 0) display.Hue = (int)hue.CurrentAngle;
                result.Add(display);
            }
            return result;
        }

        public int SetDigitalVibrance(NvDisplay display, int value)
        {
            DvcInfo info = NewDvcInfo();
            info.CurrentLevel = value;
            return _setDvc(display.Handle, 0, ref info);
        }

        public int SetHue(NvDisplay display, int value)
        {
            return _setHue(display.Handle, 0, (uint)value);
        }

        public int SetGammaRamp(NvDisplay display, RegistryColorSnapshot settings)
        {
            if (display.DisplayId == 0) return -5;
            if (settings == null || settings.Values == null || settings.Values.Count < 9) return -5;

            const int pointCount = 1024;
            const int floatCount = pointCount * 3;
            float[] ramp = new float[floatCount];
            for (int point = 0; point < pointCount; point++)
            {
                double x = point / (double)(pointCount - 1);
                for (int channel = 0; channel < 3; channel++)
                {
                    double brightnessRaw = settings.Values[channel];
                    double contrastRaw = settings.Values[3 + channel];
                    double gammaRaw = settings.Values[6 + channel];
                    double contrastNorm = (contrastRaw - 100.0) / 100.0;
                    double value;
                    if (contrastNorm <= 0)
                        value = (contrastNorm + 1.0) * (x - 0.5);
                    else
                        value = (x - 0.5) / Math.Max(1.0 - contrastNorm, 0.000001);
                    value += (brightnessRaw - 100.0) / 100.0 + 0.5;
                    value = Math.Max(0.0, Math.Min(1.0, value));
                    value = Math.Pow(value, 1.0 / Math.Max(0.01, gammaRaw / 100.0));
                    ramp[point * 3 + channel] = (float)Math.Max(0.0, Math.Min(1.0, value));
                }
            }

            int size = 4 + floatCount * 4 + 4;
            IntPtr data = Marshal.AllocHGlobal(size);
            try
            {
                for (int i = 0; i < size; i++) Marshal.WriteByte(data, i, 0);
                Marshal.WriteInt32(data, 0, size | 0x10000);
                Marshal.Copy(ramp, 0, IntPtr.Add(data, 4), ramp.Length);
                Marshal.WriteInt32(data, 4 + floatCount * 4, 1);
                return _setTargetGamma(display.DisplayId, data);
            }
            finally { Marshal.FreeHGlobal(data); }
        }

        private static DvcInfo NewDvcInfo()
        {
            DvcInfo info = new DvcInfo();
            info.Version = (uint)(Marshal.SizeOf(typeof(DvcInfo)) | 0x10000);
            return info;
        }

        private static HueInfo NewHueInfo()
        {
            HueInfo info = new HueInfo();
            info.Version = (uint)(Marshal.SizeOf(typeof(HueInfo)) | 0x10000);
            return info;
        }

        private static T GetDelegate<T>(uint id) where T : class
        {
            IntPtr pointer = QueryInterface(id);
            if (pointer == IntPtr.Zero) throw new InvalidOperationException("缺少 NVAPI 接口 0x" + id.ToString("X8"));
            return Marshal.GetDelegateForFunctionPointer(pointer, typeof(T)) as T;
        }
    }
}
