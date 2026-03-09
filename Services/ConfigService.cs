using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace PresenterShield.Services
{
    public class ConfigService
    {
        private readonly string _configFilePath;

        public ConfigService()
        {
            var appDataPath = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var appFolder = Path.Combine(appDataPath, "PresenterShield");
            Directory.CreateDirectory(appFolder);
            _configFilePath = Path.Combine(appFolder, "config.json");
        }

        public void SavePrivateWindowNames(HashSet<string> windowNames)
        {
            try
            {
                var json = JsonSerializer.Serialize(windowNames);
                File.WriteAllText(_configFilePath, json);
            }
            catch { }
        }

        public HashSet<string> LoadPrivateWindowNames()
        {
            try
            {
                if (File.Exists(_configFilePath))
                {
                    var json = File.ReadAllText(_configFilePath);
                    return JsonSerializer.Deserialize<HashSet<string>>(json) ?? new HashSet<string>();
                }
            }
            catch { }
            
            return new HashSet<string>();
        }
    }
}
