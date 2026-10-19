# NVIDIA APP预设切换器

Windows 本地工具，用于保存并快速恢复 **NVIDIA App 颜色设置、分辨率、刷新率和屏幕方向**。

## 功能

- 读取与保存每台 NVIDIA 显示器的分辨率、刷新率和方向。
- 读取与保存数字振动、色调、亮度、对比度和伽马。
- 新建、重命名、删除并一键应用预设。
- `Ctrl+Alt+0` 默认显示或隐藏工具面板。
- `Ctrl+Alt+1` 至 `Ctrl+Alt+9` 默认应用前九个预设。
- 设置页面支持静默自启、托盘图标隐藏、自定义快捷键和简体中文/英文。
- 主窗口从任务栏和 `Alt+Tab` 列表隐藏。
- 自动复制旧版 `%APPDATA%\NvidiaPresetSwitcher\presets.json`，旧版文件保持原样。

## 设置文件

- 预设：`%APPDATA%\NvidiaAppPresetSwitcher\presets.json`
- 设置：`%APPDATA%\NvidiaAppPresetSwitcher\settings.json`
- 开机启动：`HKCU\Software\Microsoft\Windows\CurrentVersion\Run\NvidiaAppPresetSwitcher`

## 构建

```powershell
.\build.ps1
```

输出：`outputs\NvidiaAppPresetSwitcher.exe`

## 运行要求

- Windows 10/11 x64
- NVIDIA GPU 与显示驱动
- .NET Framework 4.7.2 或更高版本

## 测试参数

`--no-system-changes` 会跳过开机启动项写入，供隔离测试使用。

