using System;
using System.IO;
using System.Text.Json;
using System.ComponentModel; // 需要引用
using TS6_SpeakerOverlay.Models;

namespace TS6_SpeakerOverlay.Services
{
    public static class ConfigService
    {
        private const string CONFIG_FILE = "config.json";
        private static AppConfig? _currentConfig;

        public static AppConfig Load()
        {
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
                    config = new AppConfig(); // 解析失败用默认
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
                File.WriteAllText(CONFIG_FILE, json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Config] Save failed: {ex.Message}");
            }
        }
    }
}