using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace NvidiaPresetSwitcher
{
    internal sealed class DisplayColorService
    {
        private const string RegistryRoot = @"Software\NVIDIA Corporation\Global\NVTweak\Devices";
        private static readonly string[] RegistryValueNames =
        {
            "3538946", "3538947", "3538948",
            "3538949", "3538950", "3538951",
            "3538952", "3538953", "3538954"
        };

        [DllImport("gdi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr CreateDC(string driver, string device, string output, IntPtr initData);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool DeleteDC(IntPtr dc);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool GetDeviceGammaRamp(IntPtr dc, IntPtr ramp);

        [DllImport("gdi32.dll", SetLastError = true)]
        private static extern bool SetDeviceGammaRamp(IntPtr dc, IntPtr ramp);

        private readonly NvApi _nvApi;

        public DisplayColorService()
        {
            _nvApi = new NvApi();
        }

        public ColorPreset Capture(string name)
        {
            ColorPreset preset = new ColorPreset();
            preset.Id = Guid.NewGuid().ToString("N");
            preset.Name = name;
            preset.CreatedUtc = DateTime.UtcNow;

            List<NvApi.NvDisplay> displays = _nvApi.ReadDisplays();
            foreach (NvApi.NvDisplay display in displays)
            {
                DisplaySnapshot snapshot = new DisplaySnapshot();
                snapshot.DeviceName = display.DeviceName;
                snapshot.DisplayIndex = display.Index;
                snapshot.HasDigitalVibrance = display.DigitalVibrance.HasValue;
                snapshot.DigitalVibrance = display.DigitalVibrance.GetValueOrDefault();
                snapshot.HasHue = display.Hue.HasValue;
                snapshot.Hue = display.Hue.GetValueOrDefault();
                snapshot.DisplayMode = DisplayModeService.Read(display.DeviceName);
                byte[] ramp = ReadGammaRamp(display.DeviceName);
                snapshot.GammaRampBase64 = ramp == null ? null : Convert.ToBase64String(ramp);
                preset.Displays.Add(snapshot);
            }

            preset.RegistryColors = ReadRegistryColors();
            if (preset.RegistryColors.Count == displays.Count)
            {
                for (int i = 0; i < displays.Count; i++) preset.RegistryColors[i].DisplayDeviceName = displays[i].DeviceName;
            }
            return preset;
        }

        public ApplyResult Apply(ColorPreset preset)
        {
            ApplyResult result = new ApplyResult();
            List<string> modeErrors = DisplayModeService.Apply(preset.Displays);
            for (int i = 0; i < modeErrors.Count; i++) result.AddError(modeErrors[i]);
            List<NvApi.NvDisplay> currentDisplays;
            try { currentDisplays = _nvApi.ReadDisplays(); }
            catch (Exception ex) { result.AddError("读取 NVIDIA 显示器失败：" + ex.Message); return result; }

            foreach (DisplaySnapshot saved in preset.Displays)
            {
                NvApi.NvDisplay current = FindDisplay(currentDisplays, saved);
                if (current == null)
                {
                    result.AddError("未找到显示器 " + saved.DeviceName);
                    continue;
                }

                if (saved.HasDigitalVibrance)
                {
                    int status = _nvApi.SetDigitalVibrance(current, saved.DigitalVibrance);
                    if (status != 0) result.AddError(current.DeviceName + " 的数字振动控制应用失败，状态码 " + status);
                }
                if (saved.HasHue)
                {
                    int status = _nvApi.SetHue(current, saved.Hue);
                    if (status != 0) result.AddError(current.DeviceName + " 的色调应用失败，状态码 " + status);
                }
                RegistryColorSnapshot colorSettings = FindRegistrySettings(preset.RegistryColors, current, saved.DisplayIndex);
                if (colorSettings != null)
                {
                    int status = _nvApi.SetGammaRamp(current, colorSettings);
                    if (status != 0) result.AddError(current.DeviceName + " 的亮度/对比度/伽马应用失败，状态码 " + status);
                }
                else if (!String.IsNullOrEmpty(saved.GammaRampBase64))
                {
                    try
                    {
                        byte[] ramp = Convert.FromBase64String(saved.GammaRampBase64);
                        if (!WriteGammaRamp(current.DeviceName, ramp)) result.AddError(current.DeviceName + " 的亮度/对比度/伽马应用失败");
                    }
                    catch (Exception ex) { result.AddError(current.DeviceName + " 的 LUT 数据错误：" + ex.Message); }
                }
            }

            return result;
        }

        public List<RegistryColorSnapshot> ReadRegistryColors()
        {
            List<RegistryColorSnapshot> result = new List<RegistryColorSnapshot>();
            using (RegistryKey root = Registry.CurrentUser.OpenSubKey(RegistryRoot, false))
            {
                if (root == null) return result;
                foreach (string deviceName in root.GetSubKeyNames())
                {
                    using (RegistryKey color = root.OpenSubKey(deviceName + "\\Color", false))
                    {
                        if (color == null) continue;
                        RegistryColorSnapshot snapshot = new RegistryColorSnapshot();
                        snapshot.DeviceKeyName = deviceName;
                        for (int i = 0; i < RegistryValueNames.Length; i++)
                        {
                            object value = color.GetValue(RegistryValueNames[i], 100);
                            snapshot.Values.Add(Convert.ToInt32(value));
                        }
                        result.Add(snapshot);
                    }
                }
            }
            return result;
        }

        public string DescribeCurrent(string language = "zh-CN")
        {
            List<NvApi.NvDisplay> displays = _nvApi.ReadDisplays();
            List<RegistryColorSnapshot> registry = ReadRegistryColors();
            List<string> lines = new List<string>();
            for (int i = 0; i < displays.Count; i++)
            {
                NvApi.NvDisplay display = displays[i];
                string dvc = display.DigitalVibrance.HasValue ? display.DigitalVibrance.Value + "%" : "—";
                string hue = display.Hue.HasValue ? display.Hue.Value + "°" : "—";
                DisplayModeSnapshot mode = DisplayModeService.Read(display.DeviceName);
                string modeText = mode == null ? "—" : mode.Describe();
                if (language == "en-US") lines.Add(display.DeviceName + "   " + modeText + "   Vibrance " + dvc + "   Hue " + hue);
                else lines.Add(display.DeviceName + "   " + modeText + "   数字振动 " + dvc + "   色调 " + hue);
            }
            for (int i = 0; i < registry.Count; i++)
            {
                RegistryColorSnapshot color = registry[i];
                if (language == "en-US") lines.Add("Color   Brightness " + color.Brightness + "%   Contrast " + color.Contrast + "%   Gamma " + color.Gamma.ToString("0.00"));
                else lines.Add("颜色参数   亮度 " + color.Brightness + "%   对比度 " + color.Contrast + "%   伽马 " + color.Gamma.ToString("0.00"));
            }
            return lines.Count == 0 ? (language == "en-US" ? "No NVIDIA display settings detected" : "暂未检测到 NVIDIA 显示设置") : String.Join(Environment.NewLine, lines.ToArray());
        }

        private static NvApi.NvDisplay FindDisplay(List<NvApi.NvDisplay> displays, DisplaySnapshot saved)
        {
            for (int i = 0; i < displays.Count; i++)
                if (String.Equals(displays[i].DeviceName, saved.DeviceName, StringComparison.OrdinalIgnoreCase)) return displays[i];
            for (int i = 0; i < displays.Count; i++)
                if (displays[i].Index == saved.DisplayIndex) return displays[i];
            return null;
        }

        private static RegistryColorSnapshot FindRegistrySettings(List<RegistryColorSnapshot> settings, NvApi.NvDisplay display, int fallbackIndex)
        {
            if (settings == null) return null;
            for (int i = 0; i < settings.Count; i++)
                if (String.Equals(settings[i].DisplayDeviceName, display.DeviceName, StringComparison.OrdinalIgnoreCase)) return settings[i];
            if (fallbackIndex >= 0 && fallbackIndex < settings.Count) return settings[fallbackIndex];
            if (settings.Count == 1) return settings[0];
            return null;
        }

        private static byte[] ReadGammaRamp(string deviceName)
        {
            IntPtr dc = CreateDC("DISPLAY", deviceName, null, IntPtr.Zero);
            if (dc == IntPtr.Zero) return null;
            IntPtr buffer = Marshal.AllocHGlobal(3 * 256 * 2);
            try
            {
                if (!GetDeviceGammaRamp(dc, buffer)) return null;
                byte[] data = new byte[3 * 256 * 2];
                Marshal.Copy(buffer, data, 0, data.Length);
                return data;
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
                DeleteDC(dc);
            }
        }

        private static bool WriteGammaRamp(string deviceName, byte[] data)
        {
            if (data == null || data.Length != 3 * 256 * 2) return false;
            IntPtr dc = CreateDC("DISPLAY", deviceName, null, IntPtr.Zero);
            if (dc == IntPtr.Zero) return false;
            IntPtr buffer = Marshal.AllocHGlobal(data.Length);
            try
            {
                Marshal.Copy(data, 0, buffer, data.Length);
                return SetDeviceGammaRamp(dc, buffer);
            }
            finally
            {
                Marshal.FreeHGlobal(buffer);
                DeleteDC(dc);
            }
        }

        private static void WriteRegistryColors(List<RegistryColorSnapshot> snapshots)
        {
            if (snapshots == null) return;
            foreach (RegistryColorSnapshot snapshot in snapshots)
            {
                if (snapshot == null || snapshot.Values == null || snapshot.Values.Count < RegistryValueNames.Length) continue;
                using (RegistryKey color = Registry.CurrentUser.CreateSubKey(RegistryRoot + "\\" + snapshot.DeviceKeyName + "\\Color"))
                {
                    color.SetValue("NvCplGammaSet", 1, RegistryValueKind.DWord);
                    for (int i = 0; i < RegistryValueNames.Length; i++)
                        color.SetValue(RegistryValueNames[i], snapshot.Values[i], RegistryValueKind.DWord);
                }
            }
        }
    }
}
