namespace GameServer;

public class NoiseHeightSource : IHeightSource
{
    public int Seed { get; }
    private readonly ILogger _logger;

    public NoiseHeightSource(int seed, ILogger logger)
    {
        Seed = seed;
        _logger = logger;
    }

    public float Sample(float u, float v)
    {
        if (!float.IsFinite(u) || u < 0 || u > 1)
        {
            _logger.LogError("u must be between 0 and 1");
            throw new ArgumentOutOfRangeException(nameof(u), "u must be between 0 and 1");
        }

        if (!float.IsFinite(v) || v < 0 || v > 1)
        {
            _logger.LogError("v must be between 0 and 1");
            throw new ArgumentOutOfRangeException(nameof(v), "v must be between 0 and 1");
        }

        float x = u * 5f + (SmoothNoise(u * 3f + 17f, v * 3f + 31f) - 0.5f) * 1.4f;
        float y = v * 4f + (SmoothNoise(u * 3f + 47f, v * 3f + 13f) - 0.5f) * 1.4f;
        float height = 0.70f * SmoothNoise(x, y)
                     + 0.22f * SmoothNoise(x * 2f + 53f, y * 2f + 71f)
                     + 0.08f * SmoothNoise(x * 4f + 89f, y * 4f + 97f);

        float edgeDistance = MathF.Min(MathF.Min(u, 1f - u), MathF.Min(v, 1f - v));
        float edge = Math.Clamp(edgeDistance / 0.12f, 0f, 1f);
        edge = edge * edge * (3f - 2f * edge);
        return Math.Clamp(height - (1f - edge), 0f, 1f);
    }

    private float SmoothNoise(float x, float y)
    {
        int x0 = (int)MathF.Floor(x);
        int y0 = (int)MathF.Floor(y);
        float tx = x - x0;
        float ty = y - y0;
        tx = tx * tx * (3 - 2 * tx);
        ty = ty * ty * (3 - 2 * ty);

        float top = Lerp(ValueAt(x0, y0), ValueAt(x0 + 1, y0), tx);
        float bottom = Lerp(ValueAt(x0, y0 + 1), ValueAt(x0 + 1, y0 + 1), tx);
        return Lerp(top, bottom, ty);
    }

    private float ValueAt(int x, int y)
    {
        unchecked
        {
            uint hash = (uint)Seed ^ (uint)x * 0x9E3779B9u ^ (uint)y * 0x85EBCA6Bu;
            hash ^= hash >> 16;
            hash *= 0x7FEB352Du;
            hash ^= hash >> 15;
            hash *= 0x846CA68Bu;
            hash ^= hash >> 16;
            return (hash >> 8) / 16777215f;
        }
    }

    private static float Lerp(float a, float b, float t) => a + (b - a) * t;
}
