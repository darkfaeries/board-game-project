#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

public class ChooseStealTargetDecision : PendingDecision
{
    public List<Guid> EligiblePlayerIds { get; set; } = new List<Guid>();
}
