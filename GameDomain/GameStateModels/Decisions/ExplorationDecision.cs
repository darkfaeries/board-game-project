#nullable enable

using System;

public abstract class ExplorationDecision : PendingDecision
{
    public Guid ExplorationCounterId { get; set; }
}
