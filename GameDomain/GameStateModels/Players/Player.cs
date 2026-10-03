#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

public class Player
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public PlayerColor Color { get; set; }
    public ResourceInventory Resources { get; set; } = new ResourceInventory();
    public ProgressState Progress { get; set; } = new ProgressState();
    public List<Camp> Camps { get; set; } = new List<Camp>();
    public List<Explorer> Explorers { get; set; } = new List<Explorer>();
    public List<TribeCounter> TribeCounters { get; set; } = new List<TribeCounter>();
    public List<ExplorationCounter> ExplorationCounters { get; set; } = new List<ExplorationCounter>();
}
