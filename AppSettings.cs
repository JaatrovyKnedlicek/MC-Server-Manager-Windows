using System;
using System.IO;
using System.Text.Json;

namespace MC_Server_Manager_3
{
    /// <summary>
    /// Manages application settings, including the "never show stop warning again" preference
    /// </summary>
    public static class AppSettings
    {
        private static readonly string SettingsDirectory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MC Server Manager 3");

        private static readonly string SettingsFile = Path.Combine(SettingsDirectory, "settings.json");

        private static SettingsData? _cache;

        private class SettingsData
        {
            public bool NeverShowStopWarningAgain { get; set; } = false;
            public bool NeverShowBackupWarningAgain { get; set; } = false;
        }

        /// <summary>
        /// Gets whether the stop warning should be shown when closing with running servers
        /// </summary>
        public static bool NeverShowStopWarningAgain
        {
            get => GetSetting().NeverShowStopWarningAgain;
            set
            {
                var data = GetSetting();
                data.NeverShowStopWarningAgain = value;
                SaveSetting(data);
            }
        }

        /// <summary>
        /// Gets whether the backup warning should be shown before backing up
        /// </summary>
        public static bool NeverShowBackupWarningAgain
        {
            get => GetSetting().NeverShowBackupWarningAgain;
            set
            {
                var data = GetSetting();
                data.NeverShowBackupWarningAgain = value;
                SaveSetting(data);
            }
        }

        private static SettingsData GetSetting()
        {
            if (_cache != null)
                return _cache;

            try
            {
                if (File.Exists(SettingsFile))
                {
                    var json = File.ReadAllText(SettingsFile);
                    _cache = JsonSerializer.Deserialize<SettingsData>(json) ?? new SettingsData();
                    return _cache;
                }
            }
            catch
            {
                // If reading fails, use default
            }

            _cache = new SettingsData();
            return _cache;
        }

        private static void SaveSetting(SettingsData data)
        {
            try
            {
                Directory.CreateDirectory(SettingsDirectory);
                var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                File.WriteAllText(SettingsFile, json);
                _cache = data;
            }
            catch
            {
                // If saving fails, at least keep it in memory for this session
            }
        }
    }
}
