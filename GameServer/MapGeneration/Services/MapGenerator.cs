namespace GameServer;

public class MapGenerator
{
    public GeneratedMap Generate(MapGenerationOptions options, IHeightSource heights, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(heights);

        if (options.Width <= 0 || options.Height <= 0)
        {
            logger.LogError("Width and height must be greater than or equal to 0.");
            throw new ArgumentOutOfRangeException(nameof(options), "Width and height must be greater than or equal to 0.");
        }

        if (!float.IsFinite(options.SeaLevel) || options.SeaLevel < 0 || options.SeaLevel > 1)
        {
            logger.LogError("Sea level must be between 0 and 1.");
            throw new ArgumentOutOfRangeException(nameof(options), "Sea level must be between 0 and 1.");
        }

        var cells = new List<GeneratedCell>(checked(options.Width * options.Height));

        
        double worldWidth = 2 + 1.5 * (options.Width - 1);
        double worldHeight = options.Height + (options.Width > 1 ? 0.5 : 0);

        for (int row = 0; row < options.Height; row++)
        {
            for (int column = 0; column < options.Width; column++)
            {
                int offset = column % 2;
                var coords = new AxialCoords(
                    checked(3 * (column / 2) + offset - row),
                    checked(2 * row + offset));

                
                float u = (float)((1 + 1.5 * column) / worldWidth);
                float v = (float)((row + 0.5 * offset + 0.5) / worldHeight);
                float elevation = heights.Sample(u, v);
                
                if (!float.IsFinite(elevation) || elevation < 0 || elevation > 1)
                {
                    logger.LogError("Elevation must be between 0 and 1.");
                    throw new InvalidDataException("Elevation must be between 0 and 1.");
                }

                cells.Add(new GeneratedCell(coords, elevation));
            }
        }

        return new GeneratedMap(cells, options.SeaLevel);
    }
}
