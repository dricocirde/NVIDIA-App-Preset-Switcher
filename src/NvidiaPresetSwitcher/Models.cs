using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace NvidiaPresetSwitcher
{
    [DataContract]
    internal sealed class PresetCollection
    {
        [DataMember(Order = 1)] public int Version = 2;
        [DataMember(Order = 2)] public List<ColorPreset> Presets = new List<ColorPreset>();
    }

    [DataContract]
    internal sealed class ColorPreset
    {
        [DataMember(Order = 1)] public string Id;
        [DataMember(Order = 2)] public string Name;
        [DataMember(Order = 3)] public DateTime CreatedUtc;
        [DataMember(Order = 4)] public List<DisplaySnapshot> Displays = new List<DisplaySnapshot>();
        [DataMember(Order = 5)] public List<RegistryColorSnapshot> RegistryColors = new List<RegistryColorSnapshot>();

        public override string ToString()
        {
            return Name;
        }
    }

    [DataContract]
    internal sealed class DisplaySnapshot
    {
        [DataMember(Order = 1)] public string DeviceName;
        [DataMember(Order = 2)] public int DisplayIndex;
        [DataMember(Order = 3)] public bool HasDigitalVibrance;
        [DataMember(Order = 4)] public int DigitalVibrance;
        [DataMember(Order = 5)] public bool HasHue;
        [DataMember(Order = 6)] public int Hue;
        [DataMember(Order = 7)] public string GammaRampBase64;
        [DataMember(Order = 8, EmitDefaultValue = false)] public DisplayModeSnapshot DisplayMode;
    }

    [DataContract]
    internal sealed class DisplayModeSnapshot
    {
        [DataMember(Order = 1)] public int Width;
        [DataMember(Order = 2)] public int Height;
        [DataMember(Order = 3)] public int RefreshRate;
        [DataMember(Order = 4)] public int Orientation;
        [DataMember(Order = 5)] public int BitsPerPixel;

        public string Describe()
        {
            return Width + " × " + Height + "  @ " + RefreshRate + " Hz";
        }
    }

    [DataContract]
    internal sealed class RegistryColorSnapshot
    {
        [DataMember(Order = 1)] public string DeviceKeyName;
        [DataMember(Order = 2)] public string DisplayDeviceName;
        [DataMember(Order = 3)] public List<int> Values = new List<int>();

        public int Brightness
        {
            get { return Average(0, 3); }
        }

        public int Contrast
        {
            get { return Average(3, 3); }
        }

        public double Gamma
        {
            get { return AverageRaw(6, 3) / 100.0; }
        }

        private int Average(int start, int count)
        {
            int raw = AverageRaw(start, count);
            return Math.Max(0, Math.Min(100, (int)Math.Round((raw - 80) * 2.5)));
        }

        private int AverageRaw(int start, int count)
        {
            if (Values == null || Values.Count < start + count) return 100;
            int total = 0;
            for (int i = start; i < start + count; i++) total += Values[i];
            return (int)Math.Round(total / (double)count);
        }
    }

    internal sealed class ApplyResult
    {
        public readonly List<string> Messages = new List<string>();
        public bool Success = true;

        public void AddError(string message)
        {
            Success = false;
            Messages.Add(message);
        }
    }
}
