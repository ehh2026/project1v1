using System.Windows.Media.Imaging;
using InteractiveWorldMap.Models;

namespace InteractiveWorldMap.Services;

public interface IZoomedMapResampler
{
    BitmapSource Resize(BitmapSource source, int width, int height, ZoomedMapResamplingMode mode);
}
