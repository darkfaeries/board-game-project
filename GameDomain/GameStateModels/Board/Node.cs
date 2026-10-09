#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;

public class Node
{
    public AxialCoords Coords { get; }
    public Tribe Tribe { get; }
    public bool IsStartSpace { get; }
    public bool CanSettle { get; }
    public bool CanSettle3p { get; }
    public bool HasTribeCounter { get; set; }

    public Node(AxialCoords coords, Tribe tribe, bool start, bool settle, bool settle_3player)
    {
        Coords = coords;
        Tribe = tribe;
        IsStartSpace = start;
        CanSettle = settle;
        CanSettle3p = settle_3player;
        HasTribeCounter = settle;
        // TODO if 3 players, HasTribeCounter = settle_3player!
    }
}
