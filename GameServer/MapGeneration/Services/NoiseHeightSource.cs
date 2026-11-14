namespace GameServer;

public class NoiseHeightSource : IHeightSource
{
    public int Seed { get; }
    private ILogger _logger;

    public NoiseHeightSource(int seed,  ILogger logger)
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

        float sum = 0;
        float weights = 0;
        float amplitude = 1;
        float frequency = 4;
        for (int octave = 0; octave < 5; octave++)
        {
            sum += SmoothNoise(u * frequency, v * frequency) * amplitude;
            weights += amplitude;
            frequency *= 2;
            amplitude *= 0.5f;
        }
        return Math.Clamp(sum / weights, 0, 1);
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
