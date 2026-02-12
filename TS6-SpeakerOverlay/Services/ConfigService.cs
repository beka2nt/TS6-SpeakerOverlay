using System;
using System.IO;
using System.Text.Json;
using System.ComponentModel;
using TS6_SpeakerOverlay.Models;

namespace TS6_SpeakerOverlay.Services
{
    public static class ConfigService
    {
        // 1. 定义存储文件夹路径 (C:\Users\用户名\AppData\Roaming\TS6-SpeakerOverlay)
        private static readonly string AppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), 
            "TS6-SpeakerOverlay"
        );
        
        private static readonly string CONFIG_FILE = Path.Combine(AppDataFolder, "config.json");
        private static AppConfig? _currentConfig;

        public static AppConfig Load()
        {
            // 确保文件夹存在
            if (!Directory.Exists(AppDataFolder)) Directory.CreateDirectory(AppDataFolder);

            AppConfig config;
            if (!File.Exists(CONFIG_FILE))
            {
                config = new AppConfig();
            }
            else
            {
                try
                {
                    string json = File.ReadAllText(CONFIG_FILE);
                    config = JsonSerializer.Deserialize<AppConfig>(json) ?? new AppConfig();
                }
                catch
                {
                    config = new AppConfig();
                }
            }

            _currentConfig = config;
            config.PropertyChanged += Config_PropertyChanged;
            return config;
        }

        private static void Config_PropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_currentConfig != null) Save(_currentConfig);
        }

        public static void Save(AppConfig config)
        {
            try
            {
                if (!Directory.Exists(AppDataFolder)) Directory.CreateDirectory(AppDataFolder);
                var options = new JsonSerializerOptions { WriteIndented = true };
                string json = JsonSerializer.Serialize(config, options);
                File.WriteAllText(CONFIG_FILE, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Config] Save failed: {ex.Message}");
            }
        }
    }
}