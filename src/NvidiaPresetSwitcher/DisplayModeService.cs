using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NvidiaPresetSwitcher
{
    internal static class DisplayModeService
    {
        private const int EnumCurrentSettings = -1;
        private const int DispChangeSuccessful = 0;
        private const int CdsUpdateRegistry = 0x00000001;
        private const int CdsReset = 0x40000000;
        private const int DmBitsPerPel = 0x00040000;
        private const int DmPelsWidth = 0x00080000;
        private const int DmPelsHeight = 0x00100000;
        private const int DmDisplayFrequency = 0x00400000;
        private const int DmDisplayOrientation = 0x00000080;

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct DevMode
        {
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmDeviceName;
            public short dmSpecVersion;
            public short dmDriverVersion;
            public short dmSize;
            public short dmDriverExtra;
            public int dmFields;
            public int dmPositionX;
            public int dmPositionY;
            public int dmDisplayOrientation;
            public int dmDisplayFixedOutput;
            public short dmColor;
            public short dmDuplex;
            public short dmYResolution;
            public short dmTTOption;
            public short dmCollate;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 32)] public string dmFormName;
            public short dmLogPixels;
            public int dmBitsPerPel;
            public int dmPelsWidth;
            public int dmPelsHeight;
            public int dmDisplayFlags;
            public int dmDisplayFrequency;
            public int dmICMMethod;
            public int dmICMIntent;
            public int dmMediaType;
            public int dmDitherType;
            public int dmReserved1;
            public int dmReserved2;
            public int dmPanningWidth;
            public int dmPanningHeight;
        }

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern bool EnumDisplaySettings(string deviceName, int modeNum, ref DevMode devMode);

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern int ChangeDisplaySettingsEx(string deviceName, ref DevMode devMode, IntPtr hwnd, int flags, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern int ChangeDisplaySettingsEx(string deviceName, IntPtr devMode, IntPtr hwnd, int flags, IntPtr lParam);

        [DllImport("user32.dll", CharSet = CharSet.Ansi)]
        private static extern int ChangeDisplaySettings(ref DevMode devMode, int flags);

        public static DisplayModeSnapshot Read(string deviceName)
        {
            DevMode mode = NewMode();
            if (!EnumDisplaySettings(deviceName, EnumCurrentSettings, ref mode)) return null;
            return new DisplayModeSnapshot
            {
                Width = mode.dmPelsWidth,
                Height = mode.dmPelsHeight,
                RefreshRate = mode.dmDisplayFrequency,
                Orientation = mode.dmDisplayOrientation,
                BitsPerPixel = mode.dmBitsPerPel
            };
        }

        public static List<string> Apply(IEnumerable<DisplaySnapshot> displays)
        {
            List<string> errors = new List<string>();
            foreach (DisplaySnapshot display in displays)
            {
                if (display == null || display.DisplayMode == null) continue;
                DisplayModeSnapshot saved = display.DisplayMode;
                DisplayModeSnapshot before = Read(display.DeviceName);
                if (ModesEqual(before, saved)) continue;
                DevMode mode = NewMode();
                if (!EnumDisplaySettings(display.DeviceName, EnumCurrentSettings, ref mode))
                {
                    errors.Add("读取显示模式失败：" + display.DeviceName);
                    continue;
                }
                DevMode candidate;
                bool foundCandidate = TryFindBestMode(display.DeviceName, saved, out candidate);
                if (foundCandidate) mode = candidate;
                else
                {
                    mode.dmPelsWidth = saved.Width;
                    mode.dmPelsHeight = saved.Height;
                    mode.dmDisplayFrequency = saved.RefreshRate;
                    mode.dmFields = DmPelsWidth | DmPelsHeight | DmDisplayFrequency;
                }
                if (!foundCandidate && saved.Orientation != mode.dmDisplayOrientation)
                {
                    mode.dmDisplayOrientation = saved.Orientation;
                    mode.dmFields |= DmDisplayOrientation;
                }
                if (!foundCandidate && saved.BitsPerPixel > 0 && saved.BitsPerPixel != mode.dmBitsPerPel)
                {
                    mode.dmBitsPerPel = saved.BitsPerPixel;
                    mode.dmFields |= DmBitsPerPel;
                }
                int status = ChangeDisplaySettingsEx(display.DeviceName, ref mode, IntPtr.Zero, CdsReset, IntPtr.Zero);
                if (status != DispChangeSuccessful && Screen.PrimaryScreen != null && String.Equals(Screen.PrimaryScreen.DeviceName, display.DeviceName, StringComparison.OrdinalIgnoreCase))
                    status = ChangeDisplaySettings(ref mode, CdsReset);
                if (status != DispChangeSuccessful)
                {
                    mode.dmFields = DmPelsWidth | DmPelsHeight;
                    status = ChangeDisplaySettingsEx(display.DeviceName, ref mode, IntPtr.Zero, CdsReset, IntPtr.Zero);
                    if (status != DispChangeSuccessful && Screen.PrimaryScreen != null && String.Equals(Screen.PrimaryScreen.DeviceName, display.DeviceName, StringComparison.OrdinalIgnoreCase))
                        status = ChangeDisplaySettings(ref mode, CdsReset);
                }
                if (status != DispChangeSuccessful) errors.Add(display.DeviceName + " 显示模式应用失败，状态码 " + status);
                else
                {
                    DisplayModeSnapshot actual = Read(display.DeviceName);
                    if (actual == null || actual.Width != saved.Width || actual.Height != saved.Height || actual.Orientation != saved.Orientation || Math.Abs(actual.RefreshRate - saved.RefreshRate) > 1)
                        errors.Add(display.DeviceName + " 显示模式应用后与预设不一致");
                }
            }
            return errors;
        }

        private static bool ModesEqual(DisplayModeSnapshot left, DisplayModeSnapshot right)
        {
            return left != null && right != null && left.Width == right.Width && left.Height == right.Height && left.Orientation == right.Orientation && Math.Abs(left.RefreshRate - right.RefreshRate) <= 1;
        }

        public static string Diagnose(string deviceName, DisplayModeSnapshot saved)
        {
            DevMode current = NewMode();
            bool currentOk = EnumDisplaySettings(deviceName, EnumCurrentSettings, ref current);
            DevMode candidate;
            bool candidateOk = TryFindBestMode(deviceName, saved, out candidate);
            return "DevModeSize=" + Marshal.SizeOf(typeof(DevMode)) + ", Current=" + currentOk + "/" + current.dmPelsWidth + "x" + current.dmPelsHeight + "@" + current.dmDisplayFrequency + "/fields=0x" + current.dmFields.ToString("X") + ", Candidate=" + candidateOk + "/" + candidate.dmPelsWidth + "x" + candidate.dmPelsHeight + "@" + candidate.dmDisplayFrequency + "/fields=0x" + candidate.dmFields.ToString("X");
        }

        private static bool TryFindBestMode(string deviceName, DisplayModeSnapshot saved, out DevMode best)
        {
            best = NewMode();
            int bestDistance = Int32.MaxValue;
            bool found = false;
            for (int index = 0; ; index++)
            {
                DevMode candidate = NewMode();
                if (!EnumDisplaySettings(deviceName, index, ref candidate)) break;
                if (candidate.dmPelsWidth != saved.Width || candidate.dmPelsHeight != saved.Height || candidate.dmDisplayOrientation != saved.Orientation) continue;
                if (saved.BitsPerPixel > 0 && candidate.dmBitsPerPel != saved.BitsPerPixel) continue;
                int distance = Math.Abs(candidate.dmDisplayFrequency - saved.RefreshRate);
                if (distance < bestDistance) { best = candidate; bestDistance = distance; found = true; }
                if (distance == 0) break;
            }
            return found;
        }

        private static DevMode NewMode()
        {
            DevMode mode = new DevMode();
            mode.dmDeviceName = new string('\0', 32);
            mode.dmFormName = new string('\0', 32);
            mode.dmSize = (short)Marshal.SizeOf(typeof(DevMode));
            return mode;
        }
    }
}
