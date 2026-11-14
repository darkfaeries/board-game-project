namespace GameServer;

public class GeneratedMap
{
    public IReadOnlyList<GeneratedCell> Cells { get; }
    public float SeaLevel { get; }

    public GeneratedMap(IEnumerable<GeneratedCell> cells, float seaLevel)
    {
        Cells = Array.AsReadOnly(cells.ToArray());
        SeaLevel = seaLevel;
    }

    public bool IsWater(GeneratedCell cell) => cell.Elevation < SeaLevel;
}
