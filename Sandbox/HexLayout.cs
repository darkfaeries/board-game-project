using Microsoft.Xna.Framework;

using System;

namespace Sandbox;

// Converts board coordinates to world coordinates.
// Hex centers and nodes share one triangular lattice with step 1, so a hex has corner radius 1
// and its corners are exactly the 6 neighbouring nodes. Hexes are flat-top, Y points down.
public static class HexLayout
{
    public static readonly float Sqrt3 = MathF.Sqrt(3f);

    // Corner i is at angle 60 * i degrees: E, SE, SW, W, NW, NE.
    private static readonly (int dq, int dr)[] CornerOffsets =
    {
        (1, 0), (0, 1), (-1, 1), (-1, 0), (0, -1), (1, -1),
    };

    public static Vector2 ToWorld(AxialCoords c)
    {
        return new Vector2(c.q + c.r / 2f, c.r * Sqrt3 / 2f);
    }

    public static AxialCoords HexCornerCoords(AxialCoords hex, int corner)
    {
        var (dq, dr) = CornerOffsets[corner % 6];
        return new AxialCoords(hex.q + dq, hex.r + dr);
    }

    public static Vector2 HexCorner(AxialCoords hex, int corner)
    {
        return ToWorld(HexCornerCoords(hex, corner));
    }

    public static Vector2[] HexCorners(AxialCoords hex)
    {
        var corners = new Vector2[6];
        for (int i = 0; i < 6; i++)
            corners[i] = HexCorner(hex, i);
        return corners;
    }
}
