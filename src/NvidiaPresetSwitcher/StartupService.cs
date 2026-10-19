using System;
using System.Security;
using Microsoft.Win32;

namespace NvidiaPresetSwitcher
{
    internal static class StartupService
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string ValueName = "NvidiaAppPresetSwitcher";

        public static bool TrySetEnabled(string executablePath, bool enabled, out string error)
        {
            error = null;
            try
            {
                if (String.IsNullOrWhiteSpace(executablePath)) throw new ArgumentException("程序路径为空。", "executablePath");
                string command = "\"" + executablePath + "\" --startup";
                using (RegistryKey key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true))
                {
                    if (key == null) throw new InvalidOperationException("启动项注册表不可用。");
                    if (enabled)
                    {
                        object current = key.GetValue(ValueName);
                        if (!String.Equals(current as string, command, StringComparison.Ordinal))
                            key.SetValue(ValueName, command, RegistryValueKind.String);
                    }
                    else if (key.GetValue(ValueName) != null) key.DeleteValue(ValueName, false);
                }
                return true;
            }
            catch (UnauthorizedAccessException ex) { error = ex.Message; return false; }
            catch (SecurityException ex) { error = ex.Message; return false; }
            catch (Exception ex) { error = ex.Message; return false; }
        }
    }
}
