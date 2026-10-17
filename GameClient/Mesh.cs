using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

using System;
using System.Collections.Generic;

namespace GameClient;

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
        IncludePoint(p);
        _vertices.Add(new VertexPositionColor(new Vector3(p, 0f), color));
        return _vertices.Count - 1;
    }

    public void IncludePoint(Vector2 point)
    {
        Min = Vector2.Min(Min, point);
        Max = Vector2.Max(Max, point);
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

// A separate textured mesh is used for terrain tiles so paths and markers can
// continue to use the color-only mesh above.
public class TexturedMeshBuilder
{
    private readonly List<VertexPositionTexture> _vertices = new();
    private readonly List<int> _indices = new();

    public void AddHexagon(
        Vector2 center,
        float radius,
        float textureZoom,
        int textureWidth,
        int textureHeight,
        Vector2 cropPosition)
    {
        if (textureZoom <= 0f)
            throw new ArgumentOutOfRangeException(nameof(textureZoom), "Texture zoom must be positive.");
        if (textureWidth <= 0 || textureHeight <= 0)
            throw new ArgumentOutOfRangeException(nameof(textureWidth), "Texture dimensions must be positive.");

        Vector2[] corners = MeshBuilder.RegularPolygon(center, radius, 6, 0f);
        Vector2 min = new(float.MaxValue);
        Vector2 max = new(float.MinValue);
        foreach (Vector2 corner in corners)
        {
            min = Vector2.Min(min, corner);
            max = Vector2.Max(max, corner);
        }

        Vector2 tileSize = max - min;
        float cropWidth = MathF.Min(1f, 1f / textureZoom);
        float cropHeight = cropWidth * textureWidth / textureHeight / (tileSize.X / tileSize.Y);
        if (cropHeight > 1f)
        {
            cropHeight = 1f;
            cropWidth = cropHeight * textureHeight / textureWidth * (tileSize.X / tileSize.Y);
        }

        float startU = (1f - cropWidth) * MathHelper.Clamp(cropPosition.X, 0f, 1f);
        float startV = (1f - cropHeight) * MathHelper.Clamp(cropPosition.Y, 0f, 1f);

        int centerIndex = AddVertex(center, ToUv(center, min, max, cropWidth, cropHeight, startU, startV));
        int firstCornerIndex = _vertices.Count;
        foreach (Vector2 corner in corners)
            AddVertex(corner, ToUv(corner, min, max, cropWidth, cropHeight, startU, startV));

        for (int i = 0; i < corners.Length; i++)
        {
            _indices.Add(centerIndex);
            _indices.Add(firstCornerIndex + i);
            _indices.Add(firstCornerIndex + (i + 1) % corners.Length);
        }
    }

    private int AddVertex(Vector2 position, Vector2 uv)
    {
        _vertices.Add(new VertexPositionTexture(new Vector3(position, 0f), uv));
        return _vertices.Count - 1;
    }

    private static Vector2 ToUv(
        Vector2 point,
        Vector2 min,
        Vector2 max,
        float cropWidth,
        float cropHeight,
        float startU,
        float startV)
    {
        Vector2 size = max - min;
        Vector2 uv = new(
            startU + (point.X - min.X) / size.X * cropWidth,
            startV + (point.Y - min.Y) / size.Y * cropHeight);
        return uv;
    }

    public TexturedMesh Build(GraphicsDevice device)
    {
        if (_vertices.Count > ushort.MaxValue)
            throw new InvalidOperationException("Too many vertices for 16-bit indices");

        var vertexBuffer = new VertexBuffer(
            device, VertexPositionTexture.VertexDeclaration, _vertices.Count, BufferUsage.WriteOnly);
        vertexBuffer.SetData(_vertices.ToArray());

        var indices16 = new short[_indices.Count];
        for (int i = 0; i < _indices.Count; i++)
            indices16[i] = (short)_indices[i];

        var indexBuffer = new IndexBuffer(device, IndexElementSize.SixteenBits, indices16.Length, BufferUsage.WriteOnly);
        indexBuffer.SetData(indices16);

        return new TexturedMesh(vertexBuffer, indexBuffer, _indices.Count / 3);
    }
}

public class TexturedMesh : IDisposable
{
    private readonly VertexBuffer _vertexBuffer;
    private readonly IndexBuffer _indexBuffer;
    private readonly int _primitiveCount;

    public TexturedMesh(VertexBuffer vertexBuffer, IndexBuffer indexBuffer, int primitiveCount)
    {
        _vertexBuffer = vertexBuffer;
        _indexBuffer = indexBuffer;
        _primitiveCount = primitiveCount;
    }

    public void Draw(GraphicsDevice device, BasicEffect effect, Texture2D texture)
    {
        if (_primitiveCount == 0) return;

        effect.Texture = texture;
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
