using System;
using System.IO;
using System.Runtime.Serialization.Json;

namespace NvidiaPresetSwitcher
{
    internal sealed class PresetStore
    {
        private readonly string _directory;
        private readonly string _path;

        public PresetStore()
        {
            string overrideDirectory = Environment.GetEnvironmentVariable("NVIDIA_APP_PRESET_DATA_DIR");
            _directory = String.IsNullOrWhiteSpace(overrideDirectory) ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NvidiaAppPresetSwitcher") : overrideDirectory;
            _path = Path.Combine(_directory, "presets.json");
            MigrateLegacyData();
        }

        public string FilePath { get { return _path; } }

        public PresetCollection Load()
        {
            if (!File.Exists(_path)) return new PresetCollection();
            try
            {
                using (FileStream stream = File.OpenRead(_path))
                {
                    DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(PresetCollection));
                    PresetCollection data = serializer.ReadObject(stream) as PresetCollection;
                    return data ?? new PresetCollection();
                }
            }
            catch
            {
                string backup = _path + ".broken-" + DateTime.Now.ToString("yyyyMMdd-HHmmss");
                File.Copy(_path, backup, true);
                return new PresetCollection();
            }
        }

        public void Save(PresetCollection data)
        {
            Directory.CreateDirectory(_directory);
            string temp = _path + ".tmp";
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(PresetCollection));
            using (FileStream stream = File.Create(temp)) serializer.WriteObject(stream, data);

            if (File.Exists(_path))
            {
                string backup = _path + ".bak";
                File.Copy(_path, backup, true);
                File.Delete(_path);
            }
            File.Move(temp, _path);
        }

        private void MigrateLegacyData()
        {
            try
            {
                if (File.Exists(_path) || !String.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("NVIDIA_APP_PRESET_DATA_DIR"))) return;
                string legacy = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "NvidiaPresetSwitcher", "presets.json");
                if (!File.Exists(legacy)) return;
                Directory.CreateDirectory(_directory);
                File.Copy(legacy, _path, false);
            }
            catch { }
        }
    }
}
