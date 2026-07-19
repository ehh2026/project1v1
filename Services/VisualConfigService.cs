using System;
using System.IO;
using InteractiveWorldMap.Models;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace InteractiveWorldMap.Services;

public class VisualConfigService
{
    private readonly Action<string>? _warningSink;

    public VisualConfigService(Action<string>? warningSink = null)
    {
        _warningSink = warningSink;
    }

    /// <summary>
    /// Loads a single configuration file, creating it from built-in defaults if absent.
    /// </summary>
    public VisualConfig Load(string filePath)
    {
        EnsureConfigExists(filePath);

        try
        {
            var root = JObject.Parse(File.ReadAllText(filePath));
            NormalizeResamplingMode(root);
            return root.ToObject<VisualConfig>() ?? new VisualConfig();
        }
        catch (Exception ex)
        {
            _warningSink?.Invoke($"Failed to load visual configuration; using defaults: {ex.Message}");
            return new VisualConfig();
        }
    }

    /// <summary>
    /// Loads the user configuration overlaid on the shipped defaults. The default file provides
    /// the baseline (and any keys added in an update); values present in the user file win.
    /// The user file is seeded from the default file on first run so local tuning survives pulls.
    /// </summary>
    public VisualConfig Load(string userPath, string defaultPath)
    {
        EnsureConfigExists(userPath, defaultPath);

        try
        {
            JObject root;
            if (File.Exists(defaultPath))
            {
                root = JObject.Parse(File.ReadAllText(defaultPath));
                if (File.Exists(userPath))
                {
                    var userRoot = JObject.Parse(File.ReadAllText(userPath));
                    root.Merge(userRoot, new JsonMergeSettings
                    {
                        MergeArrayHandling = MergeArrayHandling.Replace,
                        MergeNullValueHandling = MergeNullValueHandling.Ignore
                    });
                }
            }
            else
            {
                root = JObject.Parse(File.ReadAllText(userPath));
            }

            NormalizeResamplingMode(root);
            return root.ToObject<VisualConfig>() ?? new VisualConfig();
        }
        catch (Exception ex)
        {
            _warningSink?.Invoke($"Failed to load visual configuration; using defaults: {ex.Message}");
            return new VisualConfig();
        }
    }

    public void Save(VisualConfig config, string filePath)
    {
        if (config == null) throw new ArgumentNullException(nameof(config));

        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = JsonConvert.SerializeObject(config, Formatting.Indented);
        File.WriteAllText(filePath, json);
    }

    public void EnsureConfigExists(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Save(new VisualConfig(), filePath);
        }
    }

    /// <summary>
    /// Seeds the user configuration from the shipped default file when it is missing, so a fresh
    /// checkout or install starts from the authored defaults rather than bare code defaults.
    /// </summary>
    public void EnsureConfigExists(string userPath, string defaultPath)
    {
        if (File.Exists(userPath))
        {
            return;
        }

        if (File.Exists(defaultPath))
        {
            var directory = Path.GetDirectoryName(userPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.Copy(defaultPath, userPath);
        }
        else
        {
            Save(new VisualConfig(), userPath);
        }
    }

    private void NormalizeResamplingMode(JObject root)
    {
        var modeToken = root["ZoomedMapRendering"]?["ResamplingMode"];
        if (modeToken?.Type == JTokenType.String)
        {
            var text = modeToken.Value<string>() ?? string.Empty;
            if (!Enum.TryParse<ZoomedMapResamplingMode>(text, true, out var parsed) ||
                !Enum.IsDefined(typeof(ZoomedMapResamplingMode), parsed))
            {
                _warningSink?.Invoke($"Unknown zoomed-map resampling mode '{text}'; using Fant.");
                modeToken.Replace(ZoomedMapResamplingMode.Fant.ToString());
            }
        }
    }
}
