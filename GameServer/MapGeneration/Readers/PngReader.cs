namespace GameServer;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

public class PngReader
{
    private readonly ILogger<PngReader> _logger;

    private readonly string _pngPath;

    public PngReader(ILogger<PngReader> logger, string pngPath) 
    {
        _logger = logger;
        _pngPath = pngPath;
    }

    public float[,] Read()
    {
        try
        {
            using var image = Image.Load<L8>(_pngPath);

            int width = image.Width;
            int height = image.Height;

            float[,] result = new float[height, width];

            for (int w = 0; w < width; ++w)
            {
                for (int h = 0; h < height; ++h)
                {
                    byte brightness = image[w, h].PackedValue;
                    result[h, w] = brightness / 255f;
                }
            }

            return result;
        }
        catch (Exception e)
        {
            _logger.LogError(e, e.Message + " in " + _pngPath);
            throw;
        }
    }
}
