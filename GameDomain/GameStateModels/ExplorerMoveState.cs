#nullable enable

public class ExplorerMoveState
{
    public Guid PlayerId { get; set; }
    public List<AxialCoords> Route { get; set; } = new();
    public int CurPosInRoute { get; set; }
    public AxialCoords Position => Route[CurPosInRoute];
    public ExplorationCounter? ActiveCounter { get; set; }
}
