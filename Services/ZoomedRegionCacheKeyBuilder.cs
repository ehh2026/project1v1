using System;
using System.Globalization;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Services;

public sealed record ZoomedRegionSourceFingerprint(
    string Role, string NormalizedPath, long Length, long LastWriteTimeUtcTicks);

public sealed class ZoomedRegionCacheKeyBuilder
{
    public const int CacheSchemaVersion = 8;

    public ZoomedRegionSourceFingerprint Fingerprint(string role, string path)
    {
        var info = new FileInfo(Path.GetFullPath(path));
        return new(role, info.FullName, info.Exists ? info.Length : -1,
            info.Exists ? info.LastWriteTimeUtc.Ticks : -1);
    }

    public string Build(ZoomedRegionRenderRequest request,
        ZoomedRegionSourceFingerprint source, ZoomedMapResamplingMode? actualMode = null)
    {
        string R(double value) => value.ToString("R", CultureInfo.InvariantCulture);
        var text = string.Join("|",
            $"schema={CacheSchemaVersion}", $"policy={ZoomedMapResampler.PolicyVersion}",
            $"center={R(request.CenterX)},{R(request.CenterY)}", $"zoom={R(request.ZoomLevel)}",
            $"pixels={request.PixelWidth}x{request.PixelHeight}",
            $"dpi={R(request.DpiScaleX)},{R(request.DpiScaleY)}",
            $"mode={actualMode ?? request.ResamplingMode}", $"role={source.Role}",
            $"path={source.NormalizedPath}", $"length={source.Length}",
            $"write={source.LastWriteTimeUtcTicks}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text)))[..24];
    }
}
