using System;
using System.Collections.Generic;

namespace NvidiaPresetSwitcher
{
    internal sealed class Localizer
    {
        private readonly AppSettings _settings;
        private static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
            {"AppTitle", "NVIDIA APP Preset Switcher"}, {"Subtitle", "Save and restore display mode and NVIDIA color settings"},
            {"Presets", "Presets"}, {"Settings", "Settings"}, {"Current", "Current display state"}, {"Refresh", "Refresh"},
            {"MyPresets", "My presets"}, {"Create", "Save current as preset"}, {"Apply", "Apply"}, {"Rename", "Rename"},
            {"Delete", "Delete"}, {"Exit", "Exit"}, {"Startup", "Silent startup"}, {"Tray", "Hide system tray icon"},
            {"Language", "Language"}, {"PanelHotkey", "Show/hide panel"}, {"PresetHotkeys", "Preset shortcuts 1–9"},
            {"SaveSettings", "Save settings"}, {"ResetHotkeys", "Restore defaults"}, {"Chinese", "简体中文"}, {"English", "English"},
            {"Reading", "Reading…"}, {"ReadOk", "Current settings loaded"}, {"ReadFailed", "Read failed: "},
            {"NewPreset", "New preset"}, {"NewPresetPrompt", "Name the current display and color settings:"}, {"Duplicate", "This preset name already exists."},
            {"Saved", "Preset saved: "}, {"Applied", "Preset applied: "}, {"Renamed", "Preset renamed"}, {"Deleted", "Preset deleted"},
            {"ConfirmDelete", "Delete preset \"{0}\"?"}, {"RenamePreset", "Rename preset"}, {"RenamePrompt", "New preset name:"},
            {"SettingsSaved", "Settings saved"}, {"HotkeyInvalid", "Invalid shortcut: {0}"}, {"HotkeyDuplicate", "Duplicate shortcut: {0}"},
            {"HotkeyConflict", "Shortcut is already used by another program: {0}"}, {"PartialFailure", "Some settings failed"},
            {"StartupFailed", "Startup configuration failed: "}, {"DriverHint", "Confirm that the display is connected to an NVIDIA GPU"},
            {"NoDisplays", "No NVIDIA display settings detected"}, {"Brightness", "Brightness"}, {"Contrast", "Contrast"},
            {"Gamma", "Gamma"}, {"Vibrance", "Vibrance"}, {"Hue", "Hue"}, {"Resolution", "Resolution"}, {"RefreshRate", "Refresh rate"}
        };

        public Localizer(AppSettings settings) { _settings = settings; }

        public string T(string key)
        {
            if (_settings.Language == "en-US")
            {
                string value;
                if (En.TryGetValue(key, out value)) return value;
            }
            return Zh(key);
        }

        private static string Zh(string key)
        {
            Dictionary<string, string> zh = new Dictionary<string, string>
            {
                {"AppTitle", "NVIDIA APP预设切换器"}, {"Subtitle", "保存并恢复显示模式与 NVIDIA 颜色设置"},
                {"Presets", "预设"}, {"Settings", "设置"}, {"Current", "当前显示状态"}, {"Refresh", "刷新读取"},
                {"MyPresets", "我的预设"}, {"Create", "保存当前为新预设"}, {"Apply", "应用"}, {"Rename", "重命名"},
                {"Delete", "删除"}, {"Exit", "退出程序"}, {"Startup", "开启静默自启"}, {"Tray", "隐藏系统托盘图标"},
                {"Language", "语言设置"}, {"PanelHotkey", "显示/隐藏工具面板"}, {"PresetHotkeys", "预设 1～9 快捷键"},
                {"SaveSettings", "保存设置"}, {"ResetHotkeys", "恢复默认快捷键"}, {"Chinese", "简体中文"}, {"English", "English"},
                {"Reading", "正在读取…"}, {"ReadOk", "已读取当前设置"}, {"ReadFailed", "读取失败："},
                {"NewPreset", "新建预设"}, {"NewPresetPrompt", "为当前显示与颜色设置命名："}, {"Duplicate", "预设名称已存在。"},
                {"Saved", "已保存预设："}, {"Applied", "已应用预设："}, {"Renamed", "预设已重命名"}, {"Deleted", "预设已删除"},
                {"ConfirmDelete", "删除预设“{0}”？"}, {"RenamePreset", "重命名预设"}, {"RenamePrompt", "新的预设名称："},
                {"SettingsSaved", "设置已保存"}, {"HotkeyInvalid", "快捷键格式错误：{0}"}, {"HotkeyDuplicate", "快捷键重复：{0}"},
                {"HotkeyConflict", "快捷键已被其他程序占用：{0}"}, {"PartialFailure", "部分设置应用失败"},
                {"StartupFailed", "开机自启配置失败："}, {"DriverHint", "请确认显示器由 NVIDIA GPU 驱动"},
                {"NoDisplays", "暂未检测到 NVIDIA 显示设置"}, {"Brightness", "亮度"}, {"Contrast", "对比度"},
                {"Gamma", "伽马"}, {"Vibrance", "数字振动"}, {"Hue", "色调"}, {"Resolution", "分辨率"}, {"RefreshRate", "刷新率"}
            };
            string value;
            return zh.TryGetValue(key, out value) ? value : key;
        }
    }
}
