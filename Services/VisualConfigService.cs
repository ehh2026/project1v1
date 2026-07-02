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

    public VisualConfig Load(string filePath)
    {
        EnsureConfigExists(filePath);

        try
        {
            var root = JObject.Parse(File.ReadAllText(filePath));
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
}
