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
    public List<AxialCoords> CampPositions { get; set; } = new List<AxialCoords>();
    public List<AxialCoords> ExplorerPositions { get; set; } = new List<AxialCoords>();
    public int TribeCounterNumber { get; set; }
    public List<Tribe> TribesCollected { get; } = new List<Tribe>();
    public List<ExplorationCounter> ExplorationCounters { get; } = new List<ExplorationCounter>();
}
