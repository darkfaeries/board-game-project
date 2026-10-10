// Explicit discriminator preserves the concrete type through JSON.
public sealed class PendingDecisionSnapshot
{
    public string Kind { get; set; } = "";
    public Guid PlayerId { get; set; }
    public int? RequiredCount { get; set; }
    public TerrainType? Terrain { get; set; }
    public List<Guid>? EligibleIds { get; set; }

    public static PendingDecisionSnapshot FromDecision(PendingDecision d)
    {
        var result = d switch
        {
            DiscardResourcesDecision x => 
                new PendingDecisionSnapshot { Kind = "discardResources", RequiredCount = x.RequiredCount },
            ChooseEnemyDecision => 
                new PendingDecisionSnapshot { Kind = "chooseEnemy" },
            ChooseEnemyDestinationDecision => 
                new PendingDecisionSnapshot { Kind = "chooseEnemyDestination" },
            ChooseStealTargetDecision x =>
                 new PendingDecisionSnapshot { Kind = "chooseStealTarget", EligibleIds = new(x.EligiblePlayerIds) },
            ChooseCampToRemoveDecision x => 
                new PendingDecisionSnapshot { Kind = "chooseCampToRemove", EligibleIds = new(x.EligibleCampIds) },
            ChooseBonusTribeCounterDecision x => 
                new PendingDecisionSnapshot { Kind = "chooseBonusTribeCounter", EligibleIds = new(x.EligibleTribeCounterIds) },
            ChooseDesertificationHexDecision x => 
                new PendingDecisionSnapshot { Kind = "chooseDesertificationHex", Terrain = x.Terrain },
            _ => throw new InvalidOperationException("Unsupported decision type")
        };

        result.PlayerId = d.PlayerId;
        return result;
    }

    public PendingDecision ToDecision()
    {
        if (PlayerId == Guid.Empty) throw new InvalidOperationException("Invalid decision owner");

        PendingDecision decision = Kind switch
        {
            "discardResources" when RequiredCount.HasValue && RequiredCount.Value >= 0 => 
                new DiscardResourcesDecision { RequiredCount = RequiredCount.Value },
            "chooseEnemy" => new ChooseEnemyDecision(),
            "chooseEnemyDestination" => new ChooseEnemyDestinationDecision(),
            "chooseStealTarget" when EligibleIds != null => 
                new ChooseStealTargetDecision { EligiblePlayerIds = new(EligibleIds) },
            "chooseCampToRemove" when EligibleIds != null => 
                new ChooseCampToRemoveDecision { EligibleCampIds = new(EligibleIds) },
            "chooseBonusTribeCounter" when EligibleIds != null => 
                new ChooseBonusTribeCounterDecision { EligibleTribeCounterIds = new(EligibleIds) },
            "chooseDesertificationHex" when Terrain.HasValue && Enum.IsDefined(Terrain.Value) => 
                new ChooseDesertificationHexDecision { Terrain = Terrain.Value },
            _ => throw new InvalidOperationException("Invalid decision snapshot")
        };
        
        decision.PlayerId = PlayerId;
        return decision;
    }
}
