using System;
using System.Collections.Generic;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace NvidiaPresetSwitcher
{
    internal sealed class MainForm : Form
    {
        private const int WmHotkey = 0x0312, PanelHotkeyId = 900, WsExToolWindow = 0x80;
        [DllImport("user32.dll", SetLastError = true)] private static extern bool RegisterHotKey(IntPtr window, int id, uint modifiers, uint virtualKey);
        [DllImport("user32.dll")] private static extern bool UnregisterHotKey(IntPtr window, int id);

        private readonly PresetStore _store = new PresetStore();
        private readonly DisplayColorService _colors = new DisplayColorService();
        private readonly SettingsStore _settingsStore;
        private readonly bool _noSystemChanges;
        private AppSettings _settings;
        private Localizer _text;
        private PresetCollection _data;
        private bool _forceClose, _allowVisible;
        private NotifyIcon _tray;
        private Label _title, _subtitle, _currentTitle, _current, _listTitle, _presetDetails, _status, _languageLabel, _panelHotkeyLabel, _presetHotkeysLabel;
        private TabControl _tabs;
        private TabPage _presetTab, _settingsTab;
        private ListBox _list;
        private Button _refresh, _create, _apply, _rename, _delete, _exit, _saveSettings, _resetHotkeys;
        private CheckBox _startup, _hideTray;
        private ComboBox _language;
        private TextBox _panelHotkey;
        private TextBox[] _presetHotkeys;

        public MainForm(bool startHidden, string startupWarning, SettingsStore settingsStore, AppSettings settings, bool noSystemChanges)
        {
            _settingsStore = settingsStore; _settings = settings; _settings.Normalize(); _text = new Localizer(_settings); _data = _store.Load(); _noSystemChanges = noSystemChanges;
            StartPosition = FormStartPosition.CenterScreen; ShowInTaskbar = false; MinimumSize = new Size(820, 620); ClientSize = new Size(940, 660);
            Font = new Font("Microsoft YaHei UI", 9F); BackColor = Color.FromArgb(18, 20, 24);
            BuildInterface(); LoadSettingsControls(); ApplyLanguage(); ConfigureTray();
            Resize += delegate { if (WindowState == FormWindowState.Minimized) Hide(); };
            FormClosing += OnFormClosing;
            Shown += delegate { RefreshList(); RefreshCurrent(); ShowStartupWarning(startupWarning); };
            _allowVisible = !startHidden;
            if (startHidden) { RefreshList(); RefreshCurrent(); ShowStartupWarning(startupWarning); }
            IntPtr hiddenMessageWindow = Handle;
        }

        protected override CreateParams CreateParams { get { CreateParams value = base.CreateParams; value.ExStyle |= WsExToolWindow; return value; } }
        protected override void SetVisibleCore(bool value) { if (value && !_allowVisible) value = false; base.SetVisibleCore(value); }
        protected override void OnHandleCreated(EventArgs e) { base.OnHandleCreated(e); RegisterConfiguredHotkeys(false); }
        protected override void OnHandleDestroyed(EventArgs e) { UnregisterConfiguredHotkeys(); base.OnHandleDestroyed(e); }
        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WmHotkey)
            {
                int id = m.WParam.ToInt32();
                if (id == PanelHotkeyId) ToggleWindow();
                else { int index = id - 1000; if (index >= 0 && index < _data.Presets.Count) ApplyPreset(_data.Presets[index]); }
            }
            base.WndProc(ref m);
        }

        private void BuildInterface()
        {
            Panel header = new Panel { Dock = DockStyle.Top, Height = 92, BackColor = Color.FromArgb(28, 31, 36) };
            header.Controls.Add(new Panel { Dock = DockStyle.Left, Width = 5, BackColor = Color.FromArgb(118, 185, 0) });
            _title = new Label { AutoSize = true, Location = new Point(32, 15), ForeColor = Color.White, Font = new Font(Font.FontFamily, 20F, FontStyle.Bold) };
            _subtitle = new Label { AutoSize = true, Location = new Point(35, 56), ForeColor = Color.FromArgb(158, 165, 175) };
            header.Controls.Add(_title); header.Controls.Add(_subtitle);
            _tabs = new TabControl { Dock = DockStyle.Fill, Padding = new Point(18, 7), Font = new Font(Font.FontFamily, 10F, FontStyle.Bold) };
            _presetTab = new TabPage { BackColor = Color.FromArgb(237, 240, 244), Padding = new Padding(20) };
            _settingsTab = new TabPage { BackColor = Color.FromArgb(237, 240, 244), Padding = new Padding(20) };
            _tabs.TabPages.Add(_presetTab); _tabs.TabPages.Add(_settingsTab); BuildPresetPage(); BuildSettingsPage();
            _status = new Label { Dock = DockStyle.Bottom, Height = 34, Padding = new Padding(20, 0, 0, 0), TextAlign = ContentAlignment.MiddleLeft, BackColor = Color.FromArgb(30, 34, 40), ForeColor = Color.FromArgb(190, 196, 204) };
            Controls.Add(_tabs); Controls.Add(_status); Controls.Add(header);
        }

        private void BuildPresetPage()
        {
            Panel currentCard = new Panel { Dock = DockStyle.Top, Height = 126, BackColor = Color.White };
            _currentTitle = new Label { AutoSize = true, Location = new Point(20, 15), Font = new Font(Font.FontFamily, 11F, FontStyle.Bold), ForeColor = Color.FromArgb(36, 40, 46) };
            _current = new Label { Location = new Point(20, 45), Height = 65, ForeColor = Color.FromArgb(70, 76, 86) };
            _refresh = NewButton(false, 108); _refresh.Location = new Point(770, 44); _refresh.Anchor = AnchorStyles.Top | AnchorStyles.Right; _refresh.Click += delegate { RefreshCurrent(); };
            currentCard.Resize += delegate { _refresh.Left = currentCard.ClientSize.Width - _refresh.Width - 20; _current.Width = _refresh.Left - 36; };
            currentCard.Controls.Add(_currentTitle); currentCard.Controls.Add(_current); currentCard.Controls.Add(_refresh);
            Panel listCard = new Panel { Dock = DockStyle.Fill, BackColor = Color.White };
            _listTitle = new Label { AutoSize = true, Location = new Point(20, 16), Font = new Font(Font.FontFamily, 11F, FontStyle.Bold), ForeColor = Color.FromArgb(36, 40, 46) };
            _list = new ListBox { Location = new Point(20, 48), BorderStyle = BorderStyle.None, IntegralHeight = false, DrawMode = DrawMode.OwnerDrawFixed, ItemHeight = 38, BackColor = Color.FromArgb(246, 248, 250), ForeColor = Color.FromArgb(35, 39, 45), Font = new Font(Font.FontFamily, 10F) };
            _list.DrawItem += DrawPresetItem; _list.DoubleClick += delegate { ApplySelected(); }; _list.SelectedIndexChanged += delegate { UpdatePresetDetails(); };
            _presetDetails = new Label { ForeColor = Color.FromArgb(90, 96, 106), Height = 25 };
            _create = NewButton(true, 168); _apply = NewButton(false, 88); _rename = NewButton(false, 88); _delete = NewButton(false, 88); _exit = NewButton(false, 100);
            _create.Click += delegate { CreatePreset(); }; _apply.Click += delegate { ApplySelected(); }; _rename.Click += delegate { RenameSelected(); }; _delete.Click += delegate { DeleteSelected(); }; _exit.Click += delegate { _forceClose = true; Close(); };
            listCard.Controls.Add(_listTitle); listCard.Controls.Add(_list); listCard.Controls.Add(_presetDetails); listCard.Controls.Add(_create); listCard.Controls.Add(_apply); listCard.Controls.Add(_rename); listCard.Controls.Add(_delete); listCard.Controls.Add(_exit);
            listCard.Resize += delegate { int y = listCard.ClientSize.Height - 52; _list.SetBounds(20, 48, listCard.ClientSize.Width - 40, y - 84); _presetDetails.SetBounds(20, y - 30, listCard.ClientSize.Width - 40, 24); _create.Location = new Point(20, y); _apply.Location = new Point(198, y); _rename.Location = new Point(296, y); _delete.Location = new Point(394, y); _exit.Location = new Point(listCard.ClientSize.Width - 120, y); };
            _presetTab.Controls.Add(listCard); _presetTab.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 14, BackColor = _presetTab.BackColor }); _presetTab.Controls.Add(currentCard);
        }

        private void BuildSettingsPage()
        {
            Panel content = new Panel { Dock = DockStyle.Fill, AutoScroll = true, BackColor = Color.White };
            _startup = NewCheckBox(20); _hideTray = NewCheckBox(58);
            _languageLabel = NewSettingLabel(102); _language = new ComboBox { Left = 250, Top = 96, Width = 220, DropDownStyle = ComboBoxStyle.DropDownList }; _language.Items.AddRange(new object[] { "简体中文", "English" });
            _panelHotkeyLabel = NewSettingLabel(148); _panelHotkey = NewHotkeyBox(250, 142, 220); _presetHotkeysLabel = NewSettingLabel(196);
            _presetHotkeys = new TextBox[9];
            for (int i = 0; i < 9; i++) { Label number = new Label { Text = (i + 1).ToString(), Left = 250 + (i % 3) * 150, Top = 225 + (i / 3) * 44, Width = 20, Height = 28, TextAlign = ContentAlignment.MiddleCenter }; _presetHotkeys[i] = NewHotkeyBox(number.Left + 24, number.Top, 116); content.Controls.Add(number); content.Controls.Add(_presetHotkeys[i]); }
            _saveSettings = NewButton(true, 132); _saveSettings.Location = new Point(250, 378); _saveSettings.Click += delegate { SaveSettings(); };
            _resetHotkeys = NewButton(false, 150); _resetHotkeys.Location = new Point(392, 378); _resetHotkeys.Click += delegate { ResetHotkeyControls(); };
            content.Controls.Add(_startup); content.Controls.Add(_hideTray); content.Controls.Add(_languageLabel); content.Controls.Add(_language); content.Controls.Add(_panelHotkeyLabel); content.Controls.Add(_panelHotkey); content.Controls.Add(_presetHotkeysLabel); content.Controls.Add(_saveSettings); content.Controls.Add(_resetHotkeys); _settingsTab.Controls.Add(content);
        }

        private static Label NewSettingLabel(int top) { return new Label { Left = 24, Top = top, Width = 205, Height = 30, TextAlign = ContentAlignment.MiddleLeft, ForeColor = Color.FromArgb(45, 50, 58), Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold) }; }
        private static TextBox NewHotkeyBox(int left, int top, int width) { return new TextBox { Left = left, Top = top, Width = width, BorderStyle = BorderStyle.FixedSingle, Font = new Font("Segoe UI", 10F) }; }
        private static CheckBox NewCheckBox(int top) { return new CheckBox { Left = 24, Top = top, Width = 450, Height = 30, ForeColor = Color.FromArgb(45, 50, 58), Font = new Font("Microsoft YaHei UI", 9F, FontStyle.Bold) }; }
        private static Button NewButton(bool primary, int width) { Button b = new Button { Width = width, Height = 36, FlatStyle = FlatStyle.Flat, Cursor = Cursors.Hand, BackColor = primary ? Color.FromArgb(118, 185, 0) : Color.FromArgb(235, 238, 242), ForeColor = primary ? Color.White : Color.FromArgb(40, 45, 52) }; b.FlatAppearance.BorderSize = 0; return b; }

        private void ApplyLanguage()
        {
            Text = _title.Text = _text.T("AppTitle"); _subtitle.Text = _text.T("Subtitle"); _presetTab.Text = _text.T("Presets"); _settingsTab.Text = _text.T("Settings"); _currentTitle.Text = _text.T("Current"); _refresh.Text = _text.T("Refresh"); _listTitle.Text = _text.T("MyPresets");
            _create.Text = _text.T("Create"); _apply.Text = _text.T("Apply"); _rename.Text = _text.T("Rename"); _delete.Text = _text.T("Delete"); _exit.Text = _text.T("Exit"); _startup.Text = _text.T("Startup"); _hideTray.Text = _text.T("Tray"); _languageLabel.Text = _text.T("Language"); _panelHotkeyLabel.Text = _text.T("PanelHotkey"); _presetHotkeysLabel.Text = _text.T("PresetHotkeys"); _saveSettings.Text = _text.T("SaveSettings"); _resetHotkeys.Text = _text.T("ResetHotkeys");
        }

        private void LoadSettingsControls() { _startup.Checked = _settings.SilentStartup; _hideTray.Checked = _settings.HideTray; _language.SelectedIndex = _settings.Language == "en-US" ? 1 : 0; _panelHotkey.Text = _settings.PanelHotkey; for (int i = 0; i < 9; i++) _presetHotkeys[i].Text = _settings.PresetHotkeys[i]; }
        private void SaveSettings()
        {
            List<string> requested = new List<string> { _panelHotkey.Text.Trim() }; for (int i = 0; i < 9; i++) requested.Add(_presetHotkeys[i].Text.Trim());
            HashSet<string> seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); List<HotkeyDefinition> parsed = new List<HotkeyDefinition>();
            foreach (string item in requested) { HotkeyDefinition definition; if (!HotkeyDefinition.TryParse(item, out definition)) { MessageBox.Show(this, String.Format(_text.T("HotkeyInvalid"), item)); return; } if (!seen.Add(definition.Normalized)) { MessageBox.Show(this, String.Format(_text.T("HotkeyDuplicate"), definition.Normalized)); return; } parsed.Add(definition); }
            _settings.SilentStartup = _startup.Checked; _settings.HideTray = _hideTray.Checked; _settings.Language = _language.SelectedIndex == 1 ? "en-US" : "zh-CN"; _settings.PanelHotkey = parsed[0].Normalized; _settings.PresetHotkeys.Clear(); for (int i = 1; i < parsed.Count; i++) _settings.PresetHotkeys.Add(parsed[i].Normalized);
            _settingsStore.Save(_settings); _text = new Localizer(_settings); ApplyLanguage(); ConfigureTray();
            string startupError = null; if (!_noSystemChanges) StartupService.TrySetEnabled(Application.ExecutablePath, _settings.SilentStartup, out startupError);
            string hotkeyError = RegisterConfiguredHotkeys(true); _status.Text = _text.T("SettingsSaved"); if (!String.IsNullOrWhiteSpace(startupError)) _status.Text = _text.T("StartupFailed") + startupError; else if (!String.IsNullOrWhiteSpace(hotkeyError)) _status.Text = hotkeyError; RefreshCurrent();
        }

        private void ResetHotkeyControls() { _panelHotkey.Text = "Ctrl+Alt+0"; List<string> defaults = AppSettings.DefaultPresetHotkeys(); for (int i = 0; i < 9; i++) _presetHotkeys[i].Text = defaults[i]; }
        private string RegisterConfiguredHotkeys(bool report)
        {
            if (!IsHandleCreated) return null; UnregisterConfiguredHotkeys(); List<string> conflicts = new List<string>(); RegisterOne(PanelHotkeyId, _settings.PanelHotkey, conflicts); for (int i = 0; i < 9; i++) RegisterOne(1000 + i, _settings.PresetHotkeys[i], conflicts);
            return report && conflicts.Count > 0 ? String.Format(_text.T("HotkeyConflict"), String.Join(", ", conflicts.ToArray())) : null;
        }
        private void RegisterOne(int id, string value, List<string> conflicts) { HotkeyDefinition h; if (!HotkeyDefinition.TryParse(value, out h) || !RegisterHotKey(Handle, id, h.Modifiers, h.VirtualKey)) conflicts.Add(value); }
        private void UnregisterConfiguredHotkeys() { if (!IsHandleCreated) return; UnregisterHotKey(Handle, PanelHotkeyId); for (int i = 0; i < 9; i++) UnregisterHotKey(Handle, 1000 + i); }

        private void ConfigureTray()
        {
            if (_settings.HideTray) { if (_tray != null) { _tray.Visible = false; _tray.Dispose(); _tray = null; } return; }
            if (_tray == null) { _tray = new NotifyIcon { Icon = SystemIcons.Application, Visible = true }; _tray.DoubleClick += delegate { ShowWindow(); }; }
            _tray.Text = _text.T("AppTitle"); ContextMenuStrip menu = new ContextMenuStrip(); ToolStripMenuItem open = new ToolStripMenuItem(_text.T("Presets")); open.Click += delegate { ShowWindow(); }; menu.Items.Add(open);
            for (int i = 0; i < _data.Presets.Count; i++) { ColorPreset preset = _data.Presets[i]; ToolStripMenuItem item = new ToolStripMenuItem(preset.Name); item.Click += delegate { ApplyPreset(preset); }; menu.Items.Add(item); }
            menu.Items.Add(new ToolStripSeparator()); ToolStripMenuItem exit = new ToolStripMenuItem(_text.T("Exit")); exit.Click += delegate { _forceClose = true; Close(); }; menu.Items.Add(exit); _tray.ContextMenuStrip = menu;
        }

        private void ShowStartupWarning(string value) { if (!String.IsNullOrWhiteSpace(value)) _status.Text = _text.T("StartupFailed") + value; }
        private void RefreshCurrent() { try { _current.Text = _colors.DescribeCurrent(_settings.Language); _status.Text = _text.T("ReadOk"); } catch (Exception ex) { _current.Text = _text.T("ReadFailed") + ex.Message; _status.Text = _text.T("DriverHint"); } }
        private void RefreshList()
        {
            string id = _list.SelectedItem == null ? null : ((ColorPreset)_list.SelectedItem).Id; _list.BeginUpdate(); _list.Items.Clear(); foreach (ColorPreset item in _data.Presets) _list.Items.Add(item); _list.EndUpdate();
            for (int i = 0; i < _list.Items.Count; i++) if (((ColorPreset)_list.Items[i]).Id == id) _list.SelectedIndex = i; if (_list.SelectedIndex < 0 && _list.Items.Count > 0) _list.SelectedIndex = 0; ConfigureTray(); RegisterConfiguredHotkeys(false); UpdatePresetDetails();
        }
        private void UpdatePresetDetails() { ColorPreset preset = _list.SelectedItem as ColorPreset; if (preset == null) { _presetDetails.Text = ""; return; } List<string> modes = new List<string>(); foreach (DisplaySnapshot display in preset.Displays) if (display.DisplayMode != null) modes.Add(display.DisplayMode.Describe()); _presetDetails.Text = String.Join("  |  ", modes.ToArray()); }
        private void DrawPresetItem(object sender, DrawItemEventArgs e) { if (e.Index < 0) return; bool selected = (e.State & DrawItemState.Selected) != 0; using (Brush bg = new SolidBrush(selected ? Color.FromArgb(223, 239, 198) : _list.BackColor)) e.Graphics.FillRectangle(bg, e.Bounds); ColorPreset preset = (ColorPreset)_list.Items[e.Index]; using (Brush fg = new SolidBrush(Color.FromArgb(34, 39, 45))) e.Graphics.DrawString((e.Index + 1) + ".  " + preset.Name, e.Font, fg, e.Bounds.Left + 10, e.Bounds.Top + 9); }

        private void CreatePreset()
        {
            using (PromptDialog dialog = new PromptDialog(_text.T("NewPreset"), _text.T("NewPresetPrompt"), "")) { if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Value.Length == 0) return; if (NameExists(dialog.Value, null)) { MessageBox.Show(this, _text.T("Duplicate")); return; } try { ColorPreset preset = _colors.Capture(dialog.Value); _data.Presets.Add(preset); _store.Save(_data); RefreshList(); _list.SelectedItem = preset; _status.Text = _text.T("Saved") + preset.Name; } catch (Exception ex) { ShowError(_text.T("NewPreset"), ex); } }
        }
        private void ApplySelected() { ColorPreset preset = _list.SelectedItem as ColorPreset; if (preset != null) ApplyPreset(preset); }
        private void ApplyPreset(ColorPreset preset) { try { ApplyResult result = _colors.Apply(preset); if (result.Success) { _status.Text = _text.T("Applied") + preset.Name; RefreshCurrent(); } else MessageBox.Show(this, String.Join(Environment.NewLine, result.Messages.ToArray()), _text.T("PartialFailure"), MessageBoxButtons.OK, MessageBoxIcon.Warning); } catch (Exception ex) { ShowError(_text.T("Apply"), ex); } }
        private void RenameSelected() { ColorPreset preset = _list.SelectedItem as ColorPreset; if (preset == null) return; using (PromptDialog dialog = new PromptDialog(_text.T("RenamePreset"), _text.T("RenamePrompt"), preset.Name)) { if (dialog.ShowDialog(this) != DialogResult.OK || dialog.Value.Length == 0 || dialog.Value == preset.Name) return; if (NameExists(dialog.Value, preset.Id)) { MessageBox.Show(this, _text.T("Duplicate")); return; } preset.Name = dialog.Value; _store.Save(_data); RefreshList(); _list.SelectedItem = preset; _status.Text = _text.T("Renamed"); } }
        private void DeleteSelected() { ColorPreset preset = _list.SelectedItem as ColorPreset; if (preset == null) return; if (MessageBox.Show(this, String.Format(_text.T("ConfirmDelete"), preset.Name), _text.T("Delete"), MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return; _data.Presets.Remove(preset); _store.Save(_data); RefreshList(); _status.Text = _text.T("Deleted"); }
        private bool NameExists(string name, string exceptId) { foreach (ColorPreset item in _data.Presets) if (item.Id != exceptId && String.Equals(item.Name, name, StringComparison.CurrentCultureIgnoreCase)) return true; return false; }
        private void ShowWindow() { _allowVisible = true; Show(); WindowState = FormWindowState.Normal; Activate(); }
        private void ToggleWindow() { if (Visible) Hide(); else ShowWindow(); }
        private void OnFormClosing(object sender, FormClosingEventArgs e) { if (!_forceClose && e.CloseReason == CloseReason.UserClosing) { e.Cancel = true; Hide(); return; } if (_tray != null) { _tray.Visible = false; _tray.Dispose(); _tray = null; } }
        private void ShowError(string title, Exception ex) { MessageBox.Show(this, ex.Message, title, MessageBoxButtons.OK, MessageBoxIcon.Error); _status.Text = title; }
    }
}
