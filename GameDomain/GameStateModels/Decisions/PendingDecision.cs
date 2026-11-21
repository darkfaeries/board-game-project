#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

public abstract class PendingDecision
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid PlayerId { get; set; }
}
