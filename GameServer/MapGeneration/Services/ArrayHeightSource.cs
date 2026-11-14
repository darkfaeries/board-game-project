namespace GameServer;

public class ArrayHeightSource : IHeightSource
{
    private readonly ILogger<ArrayHeightSource> _logger;
    private readonly float[,] _heights;

    public int Width => _heights.GetLength(1);
    public int Height => _heights.GetLength(0);

    public ArrayHeightSource(ILogger<ArrayHeightSource> logger, float[,] heights)
    {
        ArgumentNullException.ThrowIfNull(logger);
        _logger = logger;
        if (heights is null)
        {
            const string message = "The height array must not be null.";
            _logger.LogError(message);
            throw new ArgumentNullException(nameof(heights), message);
        }
        if (heights.GetLength(0) == 0 || heights.GetLength(1) == 0)
        {
            const string message = "The height array must contain at least one row and one column.";
            _logger.LogError(message);
            throw new ArgumentException(message, nameof(heights));
        }

        _heights = (float[,])heights.Clone();
        foreach (float height in _heights)
        {
            if (!float.IsFinite(height) || height < 0 || height > 1)
            {
                const string message = "All height values must be finite and between 0 and 1, inclusive.";
                _logger.LogError("{Message} Invalid value: {Height}.", message, height);
                throw new ArgumentException(message, nameof(heights));
            }
        }
    }

    public float Sample(float u, float v)
    {
        if (!float.IsFinite(u) || u < 0 || u > 1)
        {
            const string message = "The horizontal coordinate u must be finite and between 0 and 1, inclusive.";
            _logger.LogError("{Message} Received: {U}.", message, u);
            throw new ArgumentOutOfRangeException(nameof(u), u, message);
        }
        if (!float.IsFinite(v) || v < 0 || v > 1)
        {
            const string message = "The vertical coordinate v must be finite and between 0 and 1, inclusive.";
            _logger.LogError("{Message} Received: {V}.", message, v);
            throw new ArgumentOutOfRangeException(nameof(v), v, message);
        }

        float x = u * (Width - 1);
        float y = v * (Height - 1);
        int x0 = (int)x;
        int y0 = (int)y;
        int x1 = Math.Min(x0 + 1, Width - 1);
        int y1 = Math.Min(y0 + 1, Height - 1);
        float top = Lerp(_heights[y0, x0], _heights[y0, x1], x - x0);
        float bottom = Lerp(_heights[y1, x0], _heights[y1, x1], x - x0);
        return Lerp(top, bottom, y - y0);
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
