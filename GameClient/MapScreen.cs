using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using FontStashSharp;
using System;
using System.IO;
using System.Collections.Generic;

namespace GameClient;

public class MapScreen
{
    private const float TerrainTextureZoom = 1.5f;

    private GraphicsDevice _graphicsDevice;
    private Microsoft.Xna.Framework.Content.ContentManager _content;

    private BasicEffect _effect = null!;
    private BasicEffect _textureEffect = null!;
    private SpriteBatch _spriteBatch = null!;
    private Texture2D? _backgroundTexture;
    private FontSystem _numberFontSystem = null!;
    private SpriteFontBase _numberFont = null!;
    private BoardState _board = null!;

    private Mesh _boardMesh = null!;
    private Dictionary<TerrainType, Texture2D> _terrainTextures = new();
    private Dictionary<TerrainType, TexturedMesh> _terrainMeshes = new();
    private Vector2 _boardMin;
    private Vector2 _boardMax;

    public MapScreen(GraphicsDevice graphicsDevice, Microsoft.Xna.Framework.Content.ContentManager content)
    {
        _graphicsDevice = graphicsDevice;
        _content = content;
    }

    public void Initialize()
    {
        string mapDirectory = System.IO.Path.Combine(
            AppContext.BaseDirectory,
            "GameDomain",
            "GameFiles",
            "Maps",
            "Default_Map");

        string hexesPath = System.IO.Path.Combine(mapDirectory, "hexes.csv");
        string nodesPath = System.IO.Path.Combine(mapDirectory, "nodes.csv");
        string pathsPath = System.IO.Path.Combine(mapDirectory, "paths.csv");

        using (var hexesReader = new StreamReader(hexesPath))
        using (var nodesReader = new StreamReader(nodesPath))
        using (var pathsReader = new StreamReader(pathsPath))
        {
            _board = new BoardState(hexesReader, nodesReader, pathsReader);
        }

        Console.WriteLine($"hexes loaded: {_board.Hexes.Count}");
        Console.WriteLine($"nodes loaded: {_board.Nodes.Count}");
        Console.WriteLine($"explo paths loaded: {_board.ExplorationPaths.Count}");
    }

    public void LoadContent()
    {
        _effect = new BasicEffect(_graphicsDevice)
        {
            VertexColorEnabled = true,
        };
        _textureEffect = new BasicEffect(_graphicsDevice)
        {
            TextureEnabled = true,
            VertexColorEnabled = false,
        };

        _spriteBatch = new SpriteBatch(_graphicsDevice);
        string backgroundPath = System.IO.Path.Combine(
            AppContext.BaseDirectory,
            "assets",
            "tiles",
            "map_background.jpg");  
        if (File.Exists(backgroundPath))
            _backgroundTexture = Texture2D.FromFile(_graphicsDevice, backgroundPath);

        string fontPath = System.IO.Path.Combine(AppContext.BaseDirectory, "assets", "Marmelad-Regular.ttf");
        _numberFontSystem = new FontSystem();
        _numberFontSystem.AddFont(File.ReadAllBytes(fontPath));
        _numberFont = _numberFontSystem.GetFont(64);

        LoadTerrainTextures();
        _boardMesh = BuildBoardMesh();
    }

    public void UnloadContent()
    {
        _boardMesh?.Dispose();
        foreach (var mesh in _terrainMeshes.Values)
            mesh.Dispose();
        foreach (var texture in _terrainTextures.Values)
            texture.Dispose();
        _backgroundTexture?.Dispose();
        _effect?.Dispose();
        _textureEffect?.Dispose();
        _spriteBatch?.Dispose();
        _numberFontSystem?.Dispose();
    }

    private Mesh BuildBoardMesh()
    {
        var mb = new MeshBuilder();
        var tileBuilders = _board.Hexes.Values
            .Select(hex => hex.Terrain)
            .Where(_terrainTextures.ContainsKey)
            .Distinct()
            .ToDictionary(
            terrain => terrain,
            _ => new TexturedMeshBuilder());
        var pathsAdded = new HashSet<Path>();

        foreach (var hex in _board.Hexes.Values)
        {
            Vector2 center = HexLayout.ToWorld(hex.Coords);
            foreach (Vector2 corner in HexLayout.HexCorners(hex.Coords))
                mb.IncludePoint(corner);

            if (tileBuilders.TryGetValue(hex.Terrain, out var tileBuilder))
            {
                Texture2D texture = _terrainTextures[hex.Terrain];
                var random = new Random(HashCode.Combine(hex.Coords.q, hex.Coords.r, (int)hex.Terrain));
                var cropPosition = new Vector2(random.NextSingle(), random.NextSingle());
                tileBuilder.AddHexagon(
                    center,
                    1f,
                    TerrainTextureZoom,
                    texture.Width,
                    texture.Height,
                    cropPosition);
            }
            else
                mb.AddHexagon(center, 1f, TerrainColor(hex.Terrain));

            mb.AddCircle(center, 0.3f, new Color(245, 225, 185));
        }

        foreach (var path in _board.ExplorationPaths.Keys)
        {
            AddExplorationPath(mb, path);
            pathsAdded.Add(path);
        }

        foreach (var node in _board.Nodes.Values)
        {
            foreach (var other in node.Coords.HexGetNeighbors())
            {
                if (other.IsHex()) continue;
                if (!_board.Nodes.ContainsKey(other)) continue;

                Path path = new Path(node.Coords, other);
                if (!pathsAdded.Contains(path) && !_board.BlockedPaths.Contains(path))
                {
                    AddRegularPath(mb, path);
                    pathsAdded.Add(path);
                }
            }
            AddNode(mb, node);
        }

        _boardMin = mb.Min;
        _boardMax = mb.Max;
        _terrainMeshes = tileBuilders.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Build(_graphicsDevice));
        return mb.Build(_graphicsDevice);
    }

    private void LoadTerrainTextures()
    {
        string tilesDirectory = System.IO.Path.Combine(AppContext.BaseDirectory, "assets", "tiles");

        foreach (TerrainType terrain in Enum.GetValues<TerrainType>())
        {
            string texturePath = System.IO.Path.Combine(tilesDirectory, $"{terrain}.jpg");
            if (File.Exists(texturePath))
                _terrainTextures[terrain] = LoadTextureWithMipmaps(texturePath);
        }
    }

    private Texture2D LoadTextureWithMipmaps(string path)
    {
        using var source = Texture2D.FromFile(_graphicsDevice, path);
        int width = source.Width;
        int height = source.Height;
        var currentPixels = new Color[width * height];
        source.GetData(currentPixels);

        var texture = new Texture2D(_graphicsDevice, width, height, true, SurfaceFormat.Color);
        texture.SetData(0, null, currentPixels, 0, currentPixels.Length);

        int level = 1;
        while (width > 1 || height > 1)
        {
            int nextWidth = Math.Max(1, width / 2);
            int nextHeight = Math.Max(1, height / 2);
            var nextPixels = new Color[nextWidth * nextHeight];

            for (int y = 0; y < nextHeight; y++)
            {
                for (int x = 0; x < nextWidth; x++)
                {
                    int x0 = Math.Min(x * 2, width - 1);
                    int x1 = Math.Min(x0 + 1, width - 1);
                    int y0 = Math.Min(y * 2, height - 1);
                    int y1 = Math.Min(y0 + 1, height - 1);

                    Color a = currentPixels[y0 * width + x0];
                    Color b = currentPixels[y0 * width + x1];
                    Color c = currentPixels[y1 * width + x0];
                    Color d = currentPixels[y1 * width + x1];
                    nextPixels[y * nextWidth + x] = new Color(
                        (byte)((a.R + b.R + c.R + d.R + 2) / 4),
                        (byte)((a.G + b.G + c.G + d.G + 2) / 4),
                        (byte)((a.B + b.B + c.B + d.B + 2) / 4),
                        (byte)((a.A + b.A + c.A + d.A + 2) / 4));
                }
            }

            texture.SetData(level++, null, nextPixels, 0, nextPixels.Length);
            width = nextWidth;
            height = nextHeight;
            currentPixels = nextPixels;
        }

        return texture;
    }

    private static void AddExplorationPath(MeshBuilder mb, Path path)
    {
        mb.AddLine(HexLayout.ToWorld(path.from), HexLayout.ToWorld(path.to), 0.12f, new Color(220, 100, 100));
    }

    private static void AddRegularPath(MeshBuilder mb, Path path)
    {
        mb.AddLine(HexLayout.ToWorld(path.from), HexLayout.ToWorld(path.to), 0.12f, new Color(220, 170, 100));
    }

    private static void AddNode(MeshBuilder mb, Node node)
    {
        Vector2 p = HexLayout.ToWorld(node.Coords);

        if (node.Tribe is Tribe tribe)
        {
            mb.AddCircle(p, 0.22f, TribeColor(tribe));
            if (node.CanSettle3p)
            {
                mb.AddUpTriangle(p, 0.22f, TribeColor(tribe));
                mb.AddUpTriangleOutline(p, 0.22f, 0.03f, Color.Black);
            }
        }
        else if (node.IsStartSpace)
        {
            mb.AddCircle(p, 0.22f, Color.Black);
        }
        else
        {
            mb.AddCircle(p, 0.12f, new Color(240, 180, 90));
        }
    }

    public void Update(GameTime gameTime)
    {
        // Проверка нажатия Escape убрана отсюда, так как она теперь обрабатывается в главном Client.cs
    }

    public void Draw(GameTime gameTime)
    {
        _graphicsDevice.Clear(new Color(30, 110, 190));

        var viewport = _graphicsDevice.Viewport;

        if (_backgroundTexture is not null)
        {
            float backgroundScale = MathF.Max(
                viewport.Width / (float)_backgroundTexture.Width,
                viewport.Height / (float)_backgroundTexture.Height);
            Vector2 backgroundSize = new Vector2(_backgroundTexture.Width, _backgroundTexture.Height) * backgroundScale;
            Vector2 backgroundPosition = (new Vector2(viewport.Width, viewport.Height) - backgroundSize) / 2f;

            _spriteBatch.Begin(samplerState: SamplerState.LinearClamp);
            _spriteBatch.Draw(
                _backgroundTexture,
                backgroundPosition,
                sourceRectangle: null,
                color: new Color(185, 185, 185),
                rotation: 0f,
                origin: Vector2.Zero,
                scale: new Vector2(backgroundScale),
                effects: SpriteEffects.None,
                layerDepth: 0f);
            _spriteBatch.End();
        }

        _effect.Projection = Matrix.CreateOrthographicOffCenter(0, viewport.Width, viewport.Height, 0, 0, 1);
        Matrix world = FitToScreen(viewport.Width, viewport.Height, margin: 20f);
        _effect.World = world;
        _textureEffect.Projection = _effect.Projection;
        _textureEffect.World = world;

        _graphicsDevice.RasterizerState = RasterizerState.CullNone;
        _graphicsDevice.BlendState = BlendState.Opaque;
        _graphicsDevice.SamplerStates[0] = SamplerState.LinearClamp;
        foreach (var (terrain, mesh) in _terrainMeshes)
            mesh.Draw(_graphicsDevice, _textureEffect, _terrainTextures[terrain]);

        _boardMesh.Draw(_graphicsDevice, _effect);

        _spriteBatch.Begin(transformMatrix: world, samplerState: SamplerState.LinearClamp);
        foreach (var hex in _board.Hexes.Values)
            DrawNumberToken(hex);
        _spriteBatch.End();
    }

    private void DrawNumberToken(Hex hex)
    {
        if (hex.NumberToken <= 0) return;

        const float textHeight = 0.42f;
        string text = hex.NumberToken.ToString();
        Vector2 textSize = _numberFont.MeasureString(text);
        float scale = textHeight / _numberFont.MeasureString("0").Y;
        Vector2 origin = textSize / 2f;
        Color color = hex.NumberToken is 6 or 8 ? new Color(190, 30, 30) : new Color(70, 40, 20);

        _spriteBatch.DrawString(_numberFont, text, HexLayout.ToWorld(hex.Coords), color, 0f, origin, new Vector2(scale));
    }

    private Matrix FitToScreen(int width, int height, float margin)
    {
        Vector2 size = _boardMax - _boardMin;

        const float mapScale = 0.85f;
        float scale = MathF.Min((width - 2 * margin) / size.X, (height - 2 * margin) / size.Y) * mapScale;
        Vector2 offset = new Vector2(width, height) / 2f - (_boardMin + size / 2f) * scale;

        return Matrix.CreateScale(scale, scale, 1f) * Matrix.CreateTranslation(offset.X, offset.Y, 0f);
    }

    private static Color TerrainColor(TerrainType terrain) => terrain switch
    {
        TerrainType.Hills => new Color(215, 130, 50),
        TerrainType.Plains => new Color(175, 185, 70),
        TerrainType.Mountains => new Color(160, 165, 170),
        TerrainType.Forest => new Color(40, 120, 50),
        _ => Color.Magenta,
    };

    private static Color TribeColor(Tribe tribe) => tribe switch
    {
        Tribe.Indo_Europeans => Color.White,
        Tribe.Asians => Color.Gold,
        Tribe.Austronesians => Color.Brown,
        Tribe.Americans => Color.HotPink,
        _ => Color.Magenta,
    };

    private static Color TribeSecondaryColor(Tribe tribe) => tribe switch
    {
        Tribe.Indo_Europeans => Color.White,
        Tribe.Asians => Color.Gold,
        Tribe.Austronesians => Color.Black,
        Tribe.Americans => Color.HotPink,
        _ => Color.Magenta,
    };
}
