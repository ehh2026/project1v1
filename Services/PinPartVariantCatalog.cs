using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace InteractiveWorldMap.Services;

public class PinPartVariantCatalog
{
    private readonly ILogger _logger;

    public PinPartVariantCatalog(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public IReadOnlyList<string> ListVariants(
        string contentFolderPath,
        string partsFolderPath,
        string subfolderName,
        string? ensureIncluded = null)
    {
        var list = new List<string>();
        var path = Path.Combine(contentFolderPath, partsFolderPath, subfolderName);

        if (Directory.Exists(path))
        {
            try
            {
                list.AddRange(
                    Directory.GetDirectories(path)
                        .Select(Path.GetFileName)
                        .Where(name => !string.IsNullOrEmpty(name))!);
            }
            catch (Exception ex)
            {
                _logger.LogWarning($"Failed to read variants from {path}: {ex.Message}");
            }
        }
        else
        {
            _logger.LogWarning($"Variants directory does not exist: {path}");
        }

        if (!string.IsNullOrWhiteSpace(ensureIncluded) &&
            !list.Contains(ensureIncluded, StringComparer.OrdinalIgnoreCase))
        {
            list.Add(ensureIncluded);
        }

        list.Sort(StringComparer.OrdinalIgnoreCase);
        return list;
    }
}
