using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using System;
using System.IO;
using System.Collections.Generic;

namespace Sandbox;

public class Game1 : Game
{
    private GraphicsDeviceManager _graphics;
    private BasicEffect _effect;
    private SpriteBatch _spriteBatch;
    private SpriteFont _numberFont;
    private BoardState _board;

    // Built once in world units (hex corner radius = 1); fitted to the window via the World matrix.
    private Mesh _boardMesh;
    private Vector2 _boardMin;
    private Vector2 _boardMax;

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.AllowUserResizing = true;

        // Antialiasing: smooths the jagged edges of all triangles.
        _graphics.GraphicsProfile = GraphicsProfile.HiDef;
        _graphics.PreferMultiSampling = true;
        _graphics.PreparingDeviceSettings += (_, e) =>
            e.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = 8;
    }

    protected override void Initialize()
    {
        string maps_path = "../GameDomain/GameFiles/Maps/";
        string current_map_dir = "Default_Map/";

        string hexes_path = maps_path + current_map_dir + "hexes.csv";
        string nodes_path = maps_path + current_map_dir + "nodes.csv";
        string paths_path = maps_path + current_map_dir + "paths.csv";

        using (var hexes_rd = new StreamReader(hexes_path))
        using (var nodes_rd = new StreamReader(nodes_path))
        using (var paths_rd = new StreamReader(paths_path))
        {
            _board = new BoardState(hexes_rd, nodes_rd, paths_rd);
        }

        Console.WriteLine($"hexes loaded: {_board.Hexes.Count}");
        Console.WriteLine($"nodes loaded: {_board.Nodes.Count}");
        Console.WriteLine($"explo paths loaded: {_board.ExplorationPaths.Count}");

        base.Initialize();
    }

    protected override void LoadContent()
    {
        _effect = new BasicEffect(GraphicsDevice)
        {
            VertexColorEnabled = true,
        };

        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _numberFont = Content.Load<SpriteFont>("Fonts/NumberToken");

        _boardMesh = BuildBoardMesh();
    }

    protected override void UnloadContent()
    {
        _boardMesh?.Dispose();
        _effect?.Dispose();
        _spriteBatch?.Dispose();
    }

    private Mesh BuildBoardMesh()
    {
        var mb = new MeshBuilder();
        var pathsAdded = new HashSet<Path>();

        foreach (var hex in _board.Hexes.Values)
            AddHexTile(mb, hex);

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
        return mb.Build(GraphicsDevice);
    }

    private static void AddHexTile(MeshBuilder mb, Hex hex)
    {
        Vector2 center = HexLayout.ToWorld(hex.Coords);

        mb.AddHexagon(center, 1f, TerrainColor(hex.Terrain));
        // mb.AddHexagonOutline(center, 1f, 0.08f, new Color(245, 215, 160));
        mb.AddCircle(center, 0.3f, new Color(245, 225, 185));
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
            mb.AddUpTriangle(p, 0.22f, TribeColor(tribe));
            mb.AddUpTriangleOutline(p, 0.22f, 0.03f, Color.Black);
        }
        else
        {
            mb.AddCircle(p, 0.12f, new Color(240, 180, 90));
        }
    }

    protected override void Update(GameTime gameTime)
    {
        if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
            Exit();

        base.Update(gameTime);
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(new Color(30, 110, 190));

        var viewport = GraphicsDevice.Viewport;

        // Pixel coordinates: (0,0) is the top-left corner, Y grows downwards.
        _effect.Projection = Matrix.CreateOrthographicOffCenter(0, viewport.Width, viewport.Height, 0, 0, 1);
        Matrix world = FitToScreen(viewport.Width, viewport.Height, margin: 20f);
        _effect.World = world;

        GraphicsDevice.RasterizerState = RasterizerState.CullNone;
        _boardMesh.Draw(GraphicsDevice, _effect);

        // Same world matrix as the mesh, so text positions and sizes are in world units too.
        _spriteBatch.Begin(transformMatrix: world, samplerState: SamplerState.LinearClamp);
        foreach (var hex in _board.Hexes.Values)
            DrawNumberToken(hex);
        _spriteBatch.End();

        base.Draw(gameTime);
    }

    private void DrawNumberToken(Hex hex)
    {
        if (hex.NumberToken <= 0) return;

        const float textHeight = 0.42f;
        float scale = textHeight / _numberFont.LineSpacing;

        string text = hex.NumberToken.ToString();
        Vector2 origin = _numberFont.MeasureString(text) / 2f;
        Color color = hex.NumberToken is 6 or 8 ? new Color(190, 30, 30) : new Color(70, 40, 20);

        _spriteBatch.DrawString(_numberFont, text, HexLayout.ToWorld(hex.Coords), color, 0f, origin, scale, SpriteEffects.None, 0f);
    }

    // World units -> pixels: scale so the board fits, then center it.
    private Matrix FitToScreen(int width, int height, float margin)
    {
        Vector2 size = _boardMax - _boardMin;
        float scale = MathF.Min((width - 2 * margin) / size.X, (height - 2 * margin) / size.Y);
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
        Tribe.Austronesians => Color.Black,
        Tribe.Americans => Color.HotPink,
        _ => Color.Magenta,
    };
}
