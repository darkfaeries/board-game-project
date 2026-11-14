namespace GameServer;

public class BoardBuilder
{
    public BoardState Build(GeneratedMap map)
    {
        Dictionary<AxialCoords, Hex> hexes = new Dictionary<AxialCoords, Hex>();
        foreach (var cell in map.Cells)
        {
            if (!map.IsWater(cell))
            {
                hexes[cell.Coords] = new Hex(cell.Coords, TerrainType.Plains, -1, Region.Africa);
            }
        }

        return new BoardState(
            hexes,
            new Dictionary<AxialCoords, Node> (),
            new Dictionary<Path, ExplorationPath> (),
            new HashSet<Path> ());
    }
}
