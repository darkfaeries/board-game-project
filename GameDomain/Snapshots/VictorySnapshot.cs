public sealed class VictorySnapshot
{
    public Guid? ExplorationCardOwnerId { get; set; }
    public Guid? ExpansionCardOwnerId { get; set; }
    public Dictionary<ProgressType, Guid> ArtCardOwners { get; set; } = new();
    
    public static VictorySnapshot FromVictory(VictoryState v) => new()
    {
        ExplorationCardOwnerId = v.ExplorationCardOwnerId, ExpansionCardOwnerId = v.ExpansionCardOwnerId,
        ArtCardOwners = new(v.ArtCardOwners)
    };
    
    public VictoryState ToVictory()
    {
        if (ArtCardOwners == null || ArtCardOwners.Keys.Any(k => !Enum.IsDefined(k)))
            throw new InvalidOperationException("Invalid victory snapshot");
        
        return new VictoryState
        {
            ExplorationCardOwnerId = ExplorationCardOwnerId, ExpansionCardOwnerId = ExpansionCardOwnerId,
            ArtCardOwners = new(ArtCardOwners)
        };
    }
}
