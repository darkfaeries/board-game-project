#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

public class Player
{
    public Guid Id { get; }
    public string Name { get; private set; } = string.Empty;
    public PlayerColor Color { get; }
    public ResourceInventory Resources { get; set; } = new ResourceInventory();
    public ProgressState Progress { get; set; } = new ProgressState();
    public List<Guid> CampPositions { get; set; } = new();
    public List<Guid> ExplorerPositions { get; set; } = new();
    public List<TribeCounter> TribeCounters { get; set; } = new List<TribeCounter>();
    public List<ExplorationCounter> ExplorationCounters { get; set; } = new List<ExplorationCounter>();

    public string ConnectionId { get; set; } = string.Empty;

    public Player(string name, PlayerColor color)
    {
        Id = Guid.NewGuid();
        Name = name;
        Color = color;
    }
}
