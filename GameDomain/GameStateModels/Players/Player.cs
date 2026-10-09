#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

public class Player
{
    public Guid Id { get; }
    public string Name { get; } = string.Empty;
    public PlayerColor Color { get; }
    public ResourceInventory Resources { get; set; } = new ResourceInventory();
    public ProgressState Progress { get; set; } = new ProgressState();
    public List<int> CampPositions { get; set; } = new List<Camp>();
    public List<int> ExplorerPositions { get; set; } = new List<Explorer>();
    public List<TribeCounter> TribeCounters { get; set; } = new List<TribeCounter>();
    public List<ExplorationCounter> ExplorationCounters { get; set; } = new List<ExplorationCounter>();
}
