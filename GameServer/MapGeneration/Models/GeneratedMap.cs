namespace GameServer;

public class GeneratedMap
{
    public IReadOnlyList<GeneratedCell> Cells { get; }
    public float SeaLevel { get; }
    internal int? MaxSeaCrossingSteps { get; }

    public GeneratedMap(IEnumerable<GeneratedCell> cells, float seaLevel) : this(cells, seaLevel, null)
    {
    }

    internal GeneratedMap(IEnumerable<GeneratedCell> cells, float seaLevel, int? maxSeaCrossingSteps)
    {
        Cells = Array.AsReadOnly(cells.ToArray());
        SeaLevel = seaLevel;
        MaxSeaCrossingSteps = maxSeaCrossingSteps;
    }

    public bool IsWater(GeneratedCell cell) => cell.Elevation < SeaLevel;
}
