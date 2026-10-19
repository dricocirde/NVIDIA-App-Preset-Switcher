using System;
using System.IO;
using System.Runtime.Serialization.Json;

namespace NvidiaPresetSwitcher
{
    internal sealed class SettingsStore
    {
        private readonly string _directory;
        private readonly string _path;

        public SettingsStore()
        {
            string overrideDirectory = Environment.GetEnvironmentVariable("NVIDIA_APP_PRESET_DATA_DIR");
            _directory = String.IsNullOrWhiteSpace(overrideDirectory) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NvidiaAppPresetSwitcher") : overrideDirectory;
            _path = Path.Combine(_directory, "settings.json");
        }

        public AppSettings Load()
        {
            if (!File.Exists(_path)) return new AppSettings();
            try
            {
                using (FileStream stream = File.OpenRead(_path))
                {
                    AppSettings value = new DataContractJsonSerializer(typeof(AppSettings)).ReadObject(stream) as AppSettings;
                    if (value == null) value = new AppSettings();
                    value.Normalize();
                    return value;
                }
            }
            catch { return new AppSettings(); }
        }

        public void Save(AppSettings settings)
        {
            settings.Normalize();
            Directory.CreateDirectory(_directory);
            string temp = _path + ".tmp";
            using (FileStream stream = File.Create(temp)) new DataContractJsonSerializer(typeof(AppSettings)).WriteObject(stream, settings);
            if (File.Exists(_path)) File.Delete(_path);
            File.Move(temp, _path);
        }
    }
}
