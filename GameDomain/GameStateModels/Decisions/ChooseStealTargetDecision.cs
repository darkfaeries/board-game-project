#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

public class ChooseStealTargetDecision : ExplorationDecision
{
    public EnemyType EnemyType { get; set; }
    public AxialCoords Destination { get; set; } = null!;
    public List<Guid> EligiblePlayerIds { get; set; } = new List<Guid>();
}
