using System.Collections.Generic;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Services;

public interface IManualLayoutManager
{
    bool SaveLayout(string key, List<RadialExtension> extensions);
    ManualLayout? LoadLayout(string key);
    bool DeleteLayout(string key);
    bool LayoutExists(string key);
    List<string> GetAllLayoutKeys();
    bool ApplyLayout(ManualLayout layout, List<RadialExtension> extensions);
}
