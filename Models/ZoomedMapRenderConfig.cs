using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace InteractiveWorldMap.Models;

[JsonConverter(typeof(StringEnumConverter))]
public enum ZoomedMapResamplingMode
{
    Fant,
    Lanczos3,
    MitchellNetravali,
    Bicubic,
    BicubicSharpened
}

public sealed class ZoomedMapRenderConfig
{
    public ZoomedMapResamplingMode ResamplingMode { get; set; } = ZoomedMapResamplingMode.Fant;
}
