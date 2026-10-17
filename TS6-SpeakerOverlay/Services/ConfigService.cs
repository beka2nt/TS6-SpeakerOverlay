using System;
using System.IO;
using System.Text.Json;
using System.ComponentModel; // 需要引用
using TS6_SpeakerOverlay.Models;

namespace TS6_SpeakerOverlay.Services
{
    public static class ConfigService
    {
        private const string ConfigFileName = "config.json";
        private static AppConfig? _currentConfig;

        public static AppConfig Load()
        {
            if (_currentConfig != null) return _currentConfig;

            AppConfig config;
            string configPath;
            try
            {
                configPath = AppDataPaths.GetFilePath(ConfigFileName);
                AppDataPaths.MigrateLegacyFile(ConfigFileName, configPath);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Failed to initialize the configuration directory.", ex);
                configPath = Path.Combine(AppContext.BaseDirectory, ConfigFileName);
            }

            if (!File.Exists(configPath))
            {
                config = new AppConfig();
            }
            else
            {
                try
                {
                    string json = File.ReadAllText(configPath);
                    config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                }
                catch (Exception ex)
                {
                    AppLogger.Error("Failed to load the configuration. Defaults will be used.", ex);
                    config = new AppConfig();
                }
            }

            _currentConfig = config;
            
            // [关键] 监听属性变化，实现自动保存
            config.PropertyChanged += Config_PropertyChanged;
            
            return config;
        }

        private static void Config_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_currentConfig != null)
            {
                Save(_currentConfig);
            }
        }

        public static void Save(AppConfig config)
        {
            try
            {
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(config, options);
                string configPath = AppDataPaths.GetFilePath(ConfigFileName);
                string temporaryPath = configPath + ".tmp";
                File.WriteAllText(temporaryPath, json);
                File.Move(temporaryPath, configPath, overwrite: true);
            }
            catch (Exception ex)
            {
                AppLogger.Error("Failed to save the configuration.", ex);
            }
        }
    }
}
