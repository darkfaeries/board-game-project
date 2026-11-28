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
    public int TribeCounterNumber { get; set; }
    public List<Tribe> TribesCollected { get; } = new List<Tribe>();
    public List<ExplorationCounter> ExplorationCounters { get; } = new List<ExplorationCounter>();

    public string ConnectionId { get; set; } = string.Empty;

    public Player(string name, PlayerColor color)
    {
        Id = Guid.NewGuid();
        Name = name;
        Color = color;
    }
}
