#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

public class Path
{
    public Guid Id { get; set; }
    public Guid FromIntersectionId { get; set; }
    public Guid ToIntersectionId { get; set; }
    public RequirementBox? Requirement { get; set; }
    public ExplorationCounter? ExplorationCounter { get; set; }
}
