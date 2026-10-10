public sealed class PlayerSnapshot
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public PlayerColor Color { get; set; }
    public Dictionary<ResourceType, int> Resources { get; set; } = new();
    public Dictionary<ProgressType, int> Progress { get; set; } = new();
    public List<CoordSnapshot> CampPositions { get; set; } = new();
    public List<CoordSnapshot> ExplorerPositions { get; set; } = new();
    public int TribeCounterNumber { get; set; }
    public List<Tribe> TribesCollected { get; set; } = new();
    public List<CounterSnapshot> ExplorationCounters { get; set; } = new();

    public static PlayerSnapshot FromPlayer(Player p) => new()
    {
        Id = p.Id, Name = p.Name, Color = p.Color,
        Resources = new(p.Resources.Resources), Progress = new(p.Progress.Progress),
        CampPositions = p.CampPositions.Select(CoordSnapshot.From).ToList(),
        ExplorerPositions = p.ExplorerPositions.Select(CoordSnapshot.From).ToList(),
        TribeCounterNumber = p.TribeCounterNumber, TribesCollected = new(p.TribesCollected),
        ExplorationCounters = p.ExplorationCounters.Select(c => CounterSnapshot.From(c)!).ToList()
    };

    public Player ToPlayer()
    {
        if (Id == Guid.Empty || Name == null || !Enum.IsDefined(Color) || TribeCounterNumber < 0 ||
            Resources == null || Progress == null || CampPositions == null || ExplorerPositions == null ||
            TribesCollected == null || ExplorationCounters == null ||
            Resources.Any(resInfo => !Enum.IsDefined(resInfo.Key) || resInfo.Value < 0) ||
            Progress.Any(progInfo => !Enum.IsDefined(progInfo.Key) || progInfo.Value < 0) || 
            TribesCollected.Any(t => !Enum.IsDefined(t)))
            throw new InvalidOperationException("Invalid player snapshot");
        
        var player = new Player(Id, Name, Color)
        {
            Resources = new ResourceInventory { Resources = new(Resources) },
            Progress = new ProgressState { Progress = new(Progress) },
            CampPositions = CampPositions.Select(c => c.ToCoords()).ToList(),
            ExplorerPositions = ExplorerPositions.Select(c => c.ToCoords()).ToList(),
            TribeCounterNumber = TribeCounterNumber
        };

        player.TribesCollected.AddRange(TribesCollected);
        player.ExplorationCounters.AddRange(ExplorationCounters.Select(c => c.ToCounter()));
        
        return player;
    }
}
