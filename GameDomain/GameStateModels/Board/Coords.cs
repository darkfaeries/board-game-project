using System;
using System.Collections.Generic;
using System.Linq;


public class AxialCoords
{
    public int q { get; }
    public int r { get; }

    public override int GetHashCode()
    {
        return HashCode.Combine(q, r);
    }

    public override bool Equals(object obj)
    {
        return obj is AxialCoords other && q == other.q && r == other.r;
    }

    public bool IsNode()
    {
        return !this.IsHex();
    }

    public bool IsHex()
    {
        return (q - r) % 3 == 0;
    }

    public AxialCoords(int in_q, int in_r)
    {
        q = in_q;
        r = in_r;
    }

    public AxialCoords(CubeCoords cubeCoords)
    {
        if (cubeCoords.q + cubeCoords.r + cubeCoords.s != 0)
        {
            throw new Exception("Cube coords do not sum to 0!");
        }

        q = cubeCoords.q;
        r = cubeCoords.r;
    }

    public static implicit operator CubeCoords(AxialCoords coords) => new CubeCoords(coords);
    public static explicit operator AxialCoords(CubeCoords coords) => new AxialCoords(coords);

    // returns only node coordinates (6)
    public List<AxialCoords> HexGetNeighbors()
    {
        return ((CubeCoords)this).HexGetNeighbors().Map(coords => (AxialCoords)coords);
    }

    // returns only node coordinates (3)
    public List<AxialCoords> NodeGetNodeNeighbors()
    {
        return ((CubeCoords)this).NodeGetNodeNeighbors().Map(coords => (AxialCoords)coords);
    }

    // returns only hex coordinates (3)
    public List<AxialCoords> NodeGetHexNeighbors()
    {
        return ((CubeCoords)this).NodeGetHexNeighbors().Map(coords => (AxialCoords)coords);
    }
}

public class CubeCoords
{
    public int q { get; }
    public int r { get; }
    public int s { get; }

    public CubeCoords(int in_q, int in_r, int in_s)
    {
        q = in_q;
        r = in_r;
        s = in_s;
    }

    public CubeCoords(AxialCoords axial)
    {
        q = axial.q;
        r = axial.r;
        s = - axial.q - axial.r;
    }

    public static CubeCoords operator +(CubeCoords left, CubeCoords right)
    {
        return new CubeCoords(left.q + right.q, left.r + right.r, left.s + right.s);
    }

    private static List<CubeCoords> neighborsMask = new List<CubeCoords>
    {
        new CubeCoords(1, 0, -1), // node -> node
        new CubeCoords(-1, 0, 1), // node -> hex
        new CubeCoords(-1, 1, 0), // node -> node
        new CubeCoords(1, -1, 0), // node -> hex
        new CubeCoords(0, -1, 1), // node -> node
        new CubeCoords(0, 1, -1)  // node -> hex
    };

    public List<CubeCoords> HexGetNeighbors()
    {
        List<CubeCoords> neighbors = new List<CubeCoords>();
        foreach (var mask in neighborsMask)
        {
            neighbors.Add(this + mask);
        }
        return neighbors;
    }

    public List<CubeCoords> NodeGetNodeNeighbors()
    {
        List<CubeCoords> neighbors = new List<CubeCoords>();
        for (int i = 0; i < neighborsMask.Count; ++i)
        {
            if (i % 2 == 0) neighbors.Add(this + neighborsMask[i]);
        }
        return neighbors;
    }

    public List<CubeCoords> NodeGetHexNeighbors()
    {
        List<CubeCoords> neighbors = new List<CubeCoords>();
        for (int i = 0; i < neighborsMask.Count; ++i)
        {
            if (i % 2 == 1) neighbors.Add(this + neighborsMask[i]);
        }
        return neighbors;
    }
}

// helper for neighbor conversion

public static class CoordConversionHelper
{
    public static List<TOut> Map<TIn, TOut>(this IEnumerable<TIn> src, Func<TIn, TOut> f)
        => src.Select(f).ToList();
}
