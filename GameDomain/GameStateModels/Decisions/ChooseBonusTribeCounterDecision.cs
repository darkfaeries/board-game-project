#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

public class ChooseBonusTribeCounterDecision : PendingDecision
{
    public List<Guid> EligibleTribeCounterIds { get; set; } = new List<Guid>();
}
