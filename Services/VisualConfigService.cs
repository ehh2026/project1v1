using System;
using System.IO;
using InteractiveWorldMap.Models;
using Newtonsoft.Json;

namespace InteractiveWorldMap.Services;

public class VisualConfigService
{
    public VisualConfig Load(string filePath)
    {
        EnsureConfigExists(filePath);

        try
        {
            var json = File.ReadAllText(filePath);
            return JsonConvert.DeserializeObject<VisualConfig>(json) ?? new VisualConfig();
        }
        catch (Exception)
        {
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
