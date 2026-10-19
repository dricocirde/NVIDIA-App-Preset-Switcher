using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Windows.Forms;

namespace NvidiaPresetSwitcher
{
    [DataContract]
    internal sealed class AppSettings
    {
        [DataMember(Order = 1)] public int Version = 1;
        [DataMember(Order = 2)] public bool SilentStartup = true;
        [DataMember(Order = 3)] public bool HideTray = true;
        [DataMember(Order = 4)] public string Language = "zh-CN";
        [DataMember(Order = 5)] public string PanelHotkey = "Ctrl+Alt+0";
        [DataMember(Order = 6)] public List<string> PresetHotkeys = DefaultPresetHotkeys();

        public static List<string> DefaultPresetHotkeys()
        {
            List<string> result = new List<string>();
            for (int i = 1; i <= 9; i++) result.Add("Ctrl+Alt+" + i);
            return result;
        }

        public void Normalize()
        {
            if (String.IsNullOrWhiteSpace(Language)) Language = "zh-CN";
            if (String.IsNullOrWhiteSpace(PanelHotkey)) PanelHotkey = "Ctrl+Alt+0";
            if (PresetHotkeys == null) PresetHotkeys = DefaultPresetHotkeys();
            while (PresetHotkeys.Count < 9) PresetHotkeys.Add("Ctrl+Alt+" + (PresetHotkeys.Count + 1));
            if (PresetHotkeys.Count > 9) PresetHotkeys.RemoveRange(9, PresetHotkeys.Count - 9);
        }
    }

    internal sealed class HotkeyDefinition
    {
        public uint Modifiers;
        public uint VirtualKey;
        public string Normalized;

        public static bool TryParse(string text, out HotkeyDefinition definition)
        {
            definition = null;
            if (String.IsNullOrWhiteSpace(text)) return false;
            string[] parts = text.Split(new[] { '+' }, StringSplitOptions.RemoveEmptyEntries);
            uint modifiers = 0;
            Keys key = Keys.None;
            List<string> normalized = new List<string>();
            foreach (string raw in parts)
            {
                string part = raw.Trim();
                if (part.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || part.Equals("Control", StringComparison.OrdinalIgnoreCase)) { modifiers |= 0x0002; if (!normalized.Contains("Ctrl")) normalized.Add("Ctrl"); }
                else if (part.Equals("Alt", StringComparison.OrdinalIgnoreCase)) { modifiers |= 0x0001; if (!normalized.Contains("Alt")) normalized.Add("Alt"); }
                else if (part.Equals("Shift", StringComparison.OrdinalIgnoreCase)) { modifiers |= 0x0004; if (!normalized.Contains("Shift")) normalized.Add("Shift"); }
                else if (part.Equals("Win", StringComparison.OrdinalIgnoreCase)) { modifiers |= 0x0008; if (!normalized.Contains("Win")) normalized.Add("Win"); }
                else
                {
                    if (key != Keys.None) return false;
                    if (part.Length == 1 && Char.IsLetterOrDigit(part[0])) key = (Keys)Char.ToUpperInvariant(part[0]);
                    else
                    {
                        Keys parsed;
                        if (!Enum.TryParse<Keys>(part, true, out parsed)) return false;
                        key = parsed;
                    }
                    normalized.Add(key.ToString().Replace("D0", "0").Replace("D1", "1").Replace("D2", "2").Replace("D3", "3").Replace("D4", "4").Replace("D5", "5").Replace("D6", "6").Replace("D7", "7").Replace("D8", "8").Replace("D9", "9"));
                }
            }
            if (key == Keys.None || modifiers == 0) return false;
            definition = new HotkeyDefinition { Modifiers = modifiers | 0x4000, VirtualKey = (uint)key, Normalized = String.Join("+", normalized.ToArray()) };
            return true;
        }
    }
}
