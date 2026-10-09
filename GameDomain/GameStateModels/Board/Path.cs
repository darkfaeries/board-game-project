#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

public class Path
{
    public Guid Id { get; }
    public RequirementBox? Requirement { get; }
    public ExplorationCounter? ExplorationCounter { get; set; }
}
