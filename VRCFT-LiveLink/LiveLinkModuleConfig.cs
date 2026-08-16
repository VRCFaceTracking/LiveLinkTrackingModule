using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace LiveLinkExtTrackingInterface;

public sealed class LiveLinkModuleConfig
{
    public const ushort DefaultPortNum = 11111;

    [JsonInclude]
    public ushort PortNum = DefaultPortNum;

    //[JsonInclude]
    //public LLModuleConfigData LLModuleConfigData = LLModuleConfigData.Default;

    public static class ModuleConfigPath
    {
        public const string Filename = "LiveLinkModuleConfig.json";
        public static string Directory => Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);
        public static string FullPath => Path.Combine(ModuleConfigPath.Directory, ModuleConfigPath.Filename);
    }

    public string ToJsonString()
    {
        var jsonOptions = new JsonSerializerOptions
        {
            IncludeFields = true,
            WriteIndented = true
        };
        return JsonSerializer.Serialize(this, jsonOptions);
    }

    public void WriteJsonFile(string filename) =>
        WriteJsonFile(this, filename);

    public static void WriteJsonFile(LiveLinkModuleConfig config, string filename)
    {
        if (config == null)
            return;
        var jsonStr = config.ToJsonString();
        File.WriteAllText(filename, jsonStr);
    }

    public static LiveLinkModuleConfig ReadJsonFile(string filename)
    {
        var jsonStr = File.ReadAllText(filename);
        var jsonOptions = new JsonSerializerOptions { IncludeFields = true };
        return JsonSerializer.Deserialize<LiveLinkModuleConfig>(jsonStr, jsonOptions);
    }

    public static LiveLinkModuleConfig LoadOrNewConfig(ILogger logger, string filename)
    {
        var moduleConfig = LoadConfig(logger, filename);
        if (moduleConfig == null)
        {
            moduleConfig = new LiveLinkModuleConfig();
            SaveConfig(moduleConfig, logger, filename);
        }

        Debug.Assert(moduleConfig != null);
        return moduleConfig;
    }

    public static LiveLinkModuleConfig? LoadConfig(ILogger logger, string configFile)
    {
        try
        {
            if (logger == null || string.IsNullOrEmpty(configFile))
                return null;
            logger.LogDebug($"Loading module config file: {configFile}");
            if (!File.Exists(configFile))
            {
                logger.LogWarning($"Failed to find {ModuleConfigPath.Filename}, file doest not exist in {ModuleConfigPath.Directory}");
                return null;
            }
            var result = LiveLinkModuleConfig.ReadJsonFile(configFile);
            if (result == null)
                throw new Exception($"Failed read json file {configFile}");
            logger.LogDebug("Successfully loaded LiveLinkModule config.");
            return result;
        }
        catch (Exception ex)
        {
            logger.LogError($"Failed to read LiveLinkModule config. Reason: {ex.Message}");
            return null;
        }
    }

    public static bool SaveConfig(LiveLinkModuleConfig LLModuleConfig, ILogger logger, string configFile)
    {
        try
        {
            logger.LogDebug($"Saving LiveLinkModule config to: {configFile}");

            LLModuleConfig.WriteJsonFile(configFile);

            logger.LogDebug($"LiveLinkModule config saved successfully");
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError($"Failed to save LiveLinkModule config. Reason: {ex.Message}");
            return false;
        }
    }
}