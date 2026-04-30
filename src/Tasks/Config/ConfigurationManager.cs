
using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Runtime.CompilerServices;

using Tasks.Commands; 

namespace Tasks.Config;

public static class ConfigurationManager
{

    private static TasksConfig? _config;
    
    public static TasksConfig Config { get => _config ??= LoadConfig(); }

    public static string GetConfigPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".tasks",
            "config.json");

    private static TasksConfig LoadConfig()
    {
        var configPath = GetConfigPath();

        if (!File.Exists(configPath))
        {
            var configDir = Path.GetDirectoryName(configPath);

            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir!);
            }

            var defaultConfig = new TasksConfig();
            var jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            var json = JsonSerializer.Serialize(defaultConfig, jsonOptions);
            File.WriteAllText(configPath, json);
            return defaultConfig;
        }
        else
        {
            var json = File.ReadAllText(configPath);
            var config = JsonSerializer.Deserialize<TasksConfig>(json);
            return config ?? new TasksConfig();
        }
    }

    public static void SaveConfig()
    {
        var configPath = GetConfigPath();
        var jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
        var json = JsonSerializer.Serialize(_config, jsonOptions);
        File.WriteAllText(configPath, json);
    }
}

