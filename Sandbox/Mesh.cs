using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using System;
using System.Collections.Generic;

namespace Sandbox;

// Collects shapes into a vertex array + index array.
// Every 3 indices form one triangle; shapes added later are drawn on top.
public class MeshBuilder
{
    private readonly List<VertexPositionColor> _vertices = new();
    private readonly List<int> _indices = new();

    public Vector2 Min { get; private set; } = new(float.MaxValue);
    public Vector2 Max { get; private set; } = new(float.MinValue);

    public int AddVertex(Vector2 p, Color color)
    {
        Min = Vector2.Min(Min, p);
        Max = Vector2.Max(Max, p);
        _vertices.Add(new VertexPositionColor(new Vector3(p, 0f), color));
        return _vertices.Count - 1;
    }

    public void AddTriangle(int a, int b, int c)
    {
        _indices.Add(a);
        _indices.Add(b);
        _indices.Add(c);
    }

    // Convex polygon as a fan from the first corner: (0,1,2), (0,2,3), ...
    public void AddConvexPolygon(IReadOnlyList<Vector2> corners, Color color)
    {
        int first = _vertices.Count;
        foreach (var p in corners)
            AddVertex(p, color);

        for (int i = 1; i < corners.Count - 1; i++)
            AddTriangle(first, first + i, first + i + 1);
    }

    public void AddRegularPolygon(Vector2 center, float radius, int sides, float startAngle, Color color)
    {
        AddConvexPolygon(RegularPolygon(center, radius, sides, startAngle), color);
    }

    public void AddRegularPolygonOutline(Vector2 center, float radius, int sides, float startAngle, float thickness, Color color)
    {
        AddOutline(RegularPolygon(center, radius, sides, startAngle), center, thickness, color);
    }

    // Flat-top hexagon: first corner points east. For pointy-top use startAngle = 30 degrees.
    public void AddHexagon(Vector2 center, float radius, Color color)
    {
        AddRegularPolygon(center, radius, 6, 0f, color);
    }

    public void AddHexagonOutline(Vector2 center, float radius, float thickness, Color color)
    {
        AddRegularPolygonOutline(center, radius, 6, 0f, thickness, color);
    }

    // Triangle pointing up (Y points down, so the top corner is at -90 degrees).
    public void AddUpTriangle(Vector2 center, float radius, Color color)
    {
        AddRegularPolygon(center, radius, 3, -MathHelper.PiOver2, color);
    }

    public void AddUpTriangleOutline(Vector2 center, float radius, float thickness, Color color)
    {
        AddRegularPolygonOutline(center, radius, 3, -MathHelper.PiOver2, thickness, color);
    }

    public void AddCircle(Vector2 center, float radius, Color color)
    {
        AddRegularPolygon(center, radius, 32, 0f, color);
    }

    public void AddCircleOutline(Vector2 center, float radius, float thickness, Color color)
    {
        AddRegularPolygonOutline(center, radius, 32, 0f, thickness, color);
    }

    // Outline of a convex polygon with the given thickness, drawn inwards.
    // Inner corners are moved towards the center, which gives clean mitred joints.
    public void AddOutline(IReadOnlyList<Vector2> corners, Vector2 center, float thickness, Color color)
    {
        int n = corners.Count;
        int first = _vertices.Count;

        // Distance from center to an edge (apothem) of a regular polygon = R * cos(pi / n).
        float apothem = Vector2.Distance(center, corners[0]) * MathF.Cos(MathF.PI / n);
        float innerScale = 1f - thickness / apothem;

        for (int i = 0; i < n; i++)
        {
            AddVertex(corners[i], color);
            AddVertex(center + (corners[i] - center) * innerScale, color);
        }

        for (int i = 0; i < n; i++)
        {
            int outer = first + 2 * i;
            int inner = outer + 1;
            int nextOuter = first + 2 * ((i + 1) % n);
            int nextInner = nextOuter + 1;

            AddTriangle(outer, nextOuter, nextInner);
            AddTriangle(outer, nextInner, inner);
        }
    }

    // Thick line = rectangle = 4 vertices, 2 triangles.
    public void AddLine(Vector2 from, Vector2 to, float thickness, Color color)
    {
        Vector2 dir = to - from;
        if (dir == Vector2.Zero) return;
        dir.Normalize();
        Vector2 n = new Vector2(-dir.Y, dir.X) * (thickness / 2f);

        int a = AddVertex(from + n, color);
        int b = AddVertex(to + n, color);
        int c = AddVertex(to - n, color);
        int d = AddVertex(from - n, color);

        AddTriangle(a, b, c);
        AddTriangle(a, c, d);
    }

    public static Vector2[] RegularPolygon(Vector2 center, float radius, int sides, float startAngle)
    {
        var corners = new Vector2[sides];
        for (int i = 0; i < sides; i++)
        {
            float angle = startAngle + MathHelper.TwoPi * i / sides;
            corners[i] = center + radius * new Vector2(MathF.Cos(angle), MathF.Sin(angle));
        }
        return corners;
    }

    public Mesh Build(GraphicsDevice device)
    {
        if (_vertices.Count > ushort.MaxValue)
            throw new InvalidOperationException("Too many vertices for 16-bit indices");

        var vertexBuffer = new VertexBuffer(device, VertexPositionColor.VertexDeclaration, _vertices.Count, BufferUsage.WriteOnly);
        vertexBuffer.SetData(_vertices.ToArray());

        var indices16 = new short[_indices.Count];
        for (int i = 0; i < _indices.Count; i++)
            indices16[i] = (short)_indices[i];

        var indexBuffer = new IndexBuffer(device, IndexElementSize.SixteenBits, indices16.Length, BufferUsage.WriteOnly);
        indexBuffer.SetData(indices16);

        return new Mesh(vertexBuffer, indexBuffer, _indices.Count / 3);
    }
}

// Geometry uploaded to the GPU once and drawn every frame without rebuilding.
public class Mesh : IDisposable
{
    private readonly VertexBuffer _vertexBuffer;
    private readonly IndexBuffer _indexBuffer;
    private readonly int _primitiveCount;

    public Mesh(VertexBuffer vertexBuffer, IndexBuffer indexBuffer, int primitiveCount)
    {
        _vertexBuffer = vertexBuffer;
        _indexBuffer = indexBuffer;
        _primitiveCount = primitiveCount;
    }

    public void Draw(GraphicsDevice device, Effect effect)
    {
        if (_primitiveCount == 0) return;

        device.SetVertexBuffer(_vertexBuffer);
        device.Indices = _indexBuffer;

        foreach (var pass in effect.CurrentTechnique.Passes)
        {
            pass.Apply();
            device.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0, _primitiveCount);
        }
    }

    public void Dispose()
    {
        _vertexBuffer.Dispose();
        _indexBuffer.Dispose();
    }
}
