#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

public class ChooseCampToRemoveDecision : PendingDecision
{
    public List<Guid> EligibleCampIds { get; set; } = new List<Guid>();
}
