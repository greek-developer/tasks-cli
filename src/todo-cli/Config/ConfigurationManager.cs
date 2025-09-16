
using System.CommandLine;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Runtime.CompilerServices;

using todocli.Commands; 

namespace todocli.Config;

public static class ConfigurationManager
{

    private static TodoCliConfig? _config;
    
    public static TodoCliConfig Config { get => _config ??= LoadConfig(); }

    public static string GetConfigPath() =>
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
            ".todo-cli",
            "config.json");

    private static TodoCliConfig LoadConfig()
    {
        var configPath = GetConfigPath();

        if (!File.Exists(configPath))
        {
            var configDir = Path.GetDirectoryName(configPath);
            if (!Directory.Exists(configDir))
            {
                Directory.CreateDirectory(configDir);
            }
            var defaultConfig = new TodoCliConfig();
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
            var config = JsonSerializer.Deserialize<TodoCliConfig>(json);
            return config ?? new TodoCliConfig();
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

