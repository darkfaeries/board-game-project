#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

public class VictoryState
{
    public Guid? ExplorationCardOwnerId { get; set; }
    public Guid? ExpansionCardOwnerId { get; set; }
    public Dictionary<ProgressType, Guid> ArtCardOwners { get; set; } = new Dictionary<ProgressType, Guid>();
}
