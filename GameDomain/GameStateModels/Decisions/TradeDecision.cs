public class TradeDecision : PendingDecision
{
    public Guid TradeId { get; set; } = Guid.NewGuid();
    public Guid SourcePlayerId { get; set; }
    public Guid TargetPlayerId { get; set; }
    public Dictionary<ResourceType, int> OfferedResources { get; set; } = new();
    public Dictionary<ResourceType, int> RequestedResources { get; set; } = new();
}