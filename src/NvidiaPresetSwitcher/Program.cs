using System;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace NvidiaPresetSwitcher
{
    internal static class Program
    {
        [STAThread]
        private static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--diagnose")
            {
                try
                {
                    DisplayColorService service = new DisplayColorService();
                    Console.WriteLine(service.DescribeCurrent());
                    ColorPreset snapshot = service.Capture("diagnostic");
                    Console.WriteLine("Displays=" + snapshot.Displays.Count + ", RegistryColorKeys=" + snapshot.RegistryColors.Count);
                    for (int i = 0; i < snapshot.Displays.Count; i++)
                    {
                        DisplaySnapshot d = snapshot.Displays[i];
                        Console.WriteLine("Ramp[{0}]={1} bytes", d.DeviceName, String.IsNullOrEmpty(d.GammaRampBase64) ? 0 : Convert.FromBase64String(d.GammaRampBase64).Length);
                        Console.WriteLine("Mode[{0}]={1}", d.DeviceName, d.DisplayMode == null ? "unavailable" : d.DisplayMode.Describe());
                        if (d.DisplayMode != null) Console.WriteLine("ModeDiag[{0}]={1}", d.DeviceName, DisplayModeService.Diagnose(d.DeviceName, d.DisplayMode));
                    }
                    if (args.Length > 1 && args[1] == "--apply-current")
                    {
                        ApplyResult applied = service.Apply(snapshot);
                        Console.WriteLine("ApplyCurrent=" + (applied.Success ? "OK" : String.Join(" | ", applied.Messages.ToArray())));
                        if (!applied.Success) return 3;
                    }
                    return 0;
                }
                catch (Exception ex) { Console.Error.WriteLine(ex); return 1; }
            }

            if (args.Any(delegate(string arg) { return String.Equals(arg, "--self-test", StringComparison.OrdinalIgnoreCase); }))
            {
                try
                {
                    HotkeyDefinition hotkey;
                    if (!HotkeyDefinition.TryParse("Ctrl+Alt+0", out hotkey) || hotkey.VirtualKey != 0x30) throw new InvalidOperationException("Hotkey parser failed");
                    SettingsStore testStore = new SettingsStore();
                    AppSettings testSettings = new AppSettings();
                    testSettings.Language = "en-US";
                    testSettings.PanelHotkey = "Ctrl+Shift+F10";
                    testStore.Save(testSettings);
                    AppSettings reloaded = testStore.Load();
                    if (reloaded.Language != "en-US" || reloaded.PanelHotkey != "Ctrl+Shift+F10") throw new InvalidOperationException("Settings round-trip failed");
                    ColorPreset testPreset = new DisplayColorService().Capture("self-test");
                    if (testPreset.Displays.Count == 0 || testPreset.Displays[0].DisplayMode == null || testPreset.Displays[0].DisplayMode.Width <= 0) throw new InvalidOperationException("Display mode capture failed");
                    Console.WriteLine("SelfTest=OK; Mode=" + testPreset.Displays[0].DisplayMode.Describe());
                    return 0;
                }
                catch (Exception ex) { Console.Error.WriteLine("SelfTest=FAILED; " + ex); return 2; }
            }

            bool startHidden = args.Any(delegate(string arg) { return String.Equals(arg, "--startup", StringComparison.OrdinalIgnoreCase); });
            bool noSystemChanges = args.Any(delegate(string arg) { return String.Equals(arg, "--no-system-changes", StringComparison.OrdinalIgnoreCase); });
            SettingsStore settingsStore = new SettingsStore();
            AppSettings settings = settingsStore.Load();

            bool created;
            using (Mutex mutex = new Mutex(true, "NvidiaAppPresetSwitcher.SingleInstance", out created))
            {
                if (!created)
                {
                    MessageBox.Show("NVIDIA APP预设切换器已经在后台运行。\n请使用面板快捷键调出工具页面。", "程序已运行", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return 0;
                }
                Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                try
                {
                    string startupError;
                    if (noSystemChanges) startupError = null;
                    else StartupService.TrySetEnabled(Application.ExecutablePath, settings.SilentStartup, out startupError);
                    Application.Run(new MainForm(startHidden, startupError, settingsStore, settings, noSystemChanges));
                }
                catch (Exception ex) { MessageBox.Show(ex.Message, "启动失败", MessageBoxButtons.OK, MessageBoxIcon.Error); return 1; }
                return 0;
            }
        }
    }
}
