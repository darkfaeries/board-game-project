using Microsoft.Extensions.Logging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

using System;
using System.Collections.Generic;

using GameServer;

namespace Sandbox;

public class Game1 : Game
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<Game1> _logger;
    private readonly GraphicsDeviceManager _graphics;
    private BasicEffect _effect;
    private SpriteBatch _spriteBatch;
    private SpriteFont _numberFont;
    private static readonly MapType SelectedMapType = MapType.Earth;
    private static readonly MapGenerationOptions HeightMapOptions = new(40, 24, 0.45f);
    // Temporary coastline approximation: low land at elevation zero is also hidden.
    private static readonly MapGenerationOptions EarthMapOptions = new(56, 20, 1f / 255f);
    private readonly int _mapSeed;
    private readonly IHeightSource _earthHeights;

    private BoardState _board;
    private GeneratedMap _generatedMap;

    private Mesh _boardMesh;
    private Vector2 _boardMin;
    private Vector2 _boardMax;

    public Game1(ILoggerFactory loggerFactory, int seed, IHeightSource earthHeights)
    {
        ArgumentNullException.ThrowIfNull(loggerFactory);
        _loggerFactory = loggerFactory;
        _logger = loggerFactory.CreateLogger<Game1>();
        _mapSeed = seed;
        _earthHeights = earthHeights ?? throw new ArgumentNullException(nameof(earthHeights));
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.AllowUserResizing = true;

        _graphics.GraphicsProfile = GraphicsProfile.HiDef;
        _graphics.PreferMultiSampling = true;
        _graphics.PreparingDeviceSettings += (_, e) =>
            e.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = 8;
    }

    protected override void Initialize()
    {
        switch (SelectedMapType)
        {
            case MapType.Classic:
            {
                var csvGenerator = new CsvBoardGenerator(
                    _loggerFactory.CreateLogger<CsvBoardGenerator>());

                IBoardFactory factory = new BoardFactory(
                    _loggerFactory.CreateLogger<BoardFactory>(),
                    csvGenerator);

                _board = factory.GetBoardState(SelectedMapType);
                break;
            }
            case MapType.BySeed:
            {
                IHeightSource heights = new NoiseHeightSource(_mapSeed, _loggerFactory.CreateLogger<NoiseHeightSource>());
                _generatedMap = new MapGenerator().Generate(
                    HeightMapOptions, heights, _loggerFactory.CreateLogger<MapGenerator>());
                _logger.LogInformation("Generated {HexCount} hexes with seed {Seed}.", _generatedMap.Cells.Count, _mapSeed);
                break;
            }
            case MapType.Earth:
            {
                // Keep latitudes from 90 degrees north to 60 degrees south.
                IHeightSource croppedHeights = new CroppedHeightSource(
                    _loggerFactory.CreateLogger<CroppedHeightSource>(),
                    _earthHeights,
                    (0f, 0f),
                    (1f, 5f / 6f));
                _generatedMap = new MapGenerator().Generate(
                    EarthMapOptions, croppedHeights, _loggerFactory.CreateLogger<MapGenerator>());
                _logger.LogInformation("Generated {CellCount} cells from Earth heights.", _generatedMap.Cells.Count);
                break;
            }
            default:
                _logger.LogError("Invalid map type: {MapType}.", SelectedMapType);
                throw new ArgumentOutOfRangeException(nameof(SelectedMapType), SelectedMapType, "Invalid map type.");
        }

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

        if (_generatedMap != null)
        {
            int landCount = 0;
            foreach (var cell in _generatedMap.Cells)
            {
                if (_generatedMap.IsWater(cell)) continue;
                Vector2 center = HexLayout.ToWorld(cell.Coords);
                mb.AddHexagon(center, 1f, ElevationColor(cell.Elevation, _generatedMap.SeaLevel));
                mb.AddHexagonOutline(center, 1f, 0.035f, new Color(45, 55, 50));
                landCount++;
            }

            if (landCount == 0)
                throw new InvalidOperationException("The generated map contains no land cells to display.");

            _logger.LogInformation("Displaying {LandCount} land cells.", landCount);
            _boardMin = mb.Min;
            _boardMax = mb.Max;
            return mb.Build(GraphicsDevice);
        }

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

        _effect.Projection = Matrix.CreateOrthographicOffCenter(0, viewport.Width, viewport.Height, 0, 0, 1);
        Matrix world = FitToScreen(viewport.Width, viewport.Height, margin: 20f);
        _effect.World = world;

        GraphicsDevice.RasterizerState = RasterizerState.CullNone;
        _boardMesh.Draw(GraphicsDevice, _effect);

        _spriteBatch.Begin(transformMatrix: world, samplerState: SamplerState.LinearClamp);
        if (_board != null)
        {
            foreach (var hex in _board.Hexes.Values)
                DrawNumberToken(hex);
        }
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

    private Matrix FitToScreen(int width, int height, float margin)
    {
        Vector2 size = _boardMax - _boardMin;
        float scale = MathF.Min(
            MathF.Max(1f, width - 2 * margin) / size.X,
            MathF.Max(1f, height - 2 * margin) / size.Y);
        Vector2 offset = new Vector2(width, height) / 2f - (_boardMin + size / 2f) * scale;

        return Matrix.CreateScale(scale, scale, 1f) * Matrix.CreateTranslation(offset.X, offset.Y, 0f);
    }

    private static Color ElevationColor(float elevation, float seaLevel)
    {
        float landHeight = seaLevel < 1 ? (elevation - seaLevel) / (1 - seaLevel) : 0;
        if (landHeight < 0.15f)
            return Color.Lerp(new Color(215, 205, 140), new Color(75, 140, 65), landHeight / 0.15f);
        if (landHeight < 0.6f)
            return Color.Lerp(new Color(75, 140, 65), new Color(130, 105, 80), (landHeight - 0.15f) / 0.45f);
        return Color.Lerp(new Color(130, 105, 80), new Color(240, 240, 235), (landHeight - 0.6f) / 0.4f);
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
