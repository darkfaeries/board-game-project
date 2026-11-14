using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using FontStashSharp;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace GameClient;

public class MapScreen
{
    private const float TerrainTextureZoom = 1.5f;

    private enum BoardActionMode { None, CreateExplorer, MoveExplorer, ConvertToCamp }
    private enum TradeMode { Player, Bank }

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

    // Живое состояние партии (кампы и исследователи игроков) и ресурсы для его отрисовки.
    private GameSnapshot? _snapshot;
    private SpriteFontBase _uiTitleFont = null!;
    private SpriteFontBase _uiFont = null!;
    private Texture2D _pixel = null!;
    private Texture2D _circle = null!;

    // Действия игрока на карте.
    public event Action? RollRequested;
    public event Action? EndTurnRequested;
    public event Action<AxialCoords>? InitialPlacementRequested;
    public event Action<AxialCoords>? CreateExplorerRequested;
    public event Action<AxialCoords>? ConvertExplorerToCampRequested;
    public event Action<List<AxialCoords>>? MoveExplorerRequested;
    public event Action<Guid, Dictionary<ResourceType, int>>? TradeWithPlayerRequested;
    public event Action<Dictionary<ResourceType, int>, ResourceType>? TradeWithBankRequested;
    public bool Busy { get; set; }

    private MouseState _previousMouse;
    private string? _message;
    private float _messageSeconds;
    private BoardActionMode _boardActionMode;
    private AxialCoords? _selectedExplorer;
    private TradeMode _tradeMode;
    private Guid? _tradeTargetId;
    private readonly Dictionary<ResourceType, int> _tradeOffer = new();
    private ResourceType _bankResourceToGet = ResourceType.Meat;

    public GameSnapshot? Snapshot => _snapshot;

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
        _uiTitleFont = _numberFontSystem.GetFont(30);
        _uiFont = _numberFontSystem.GetFont(21);

        _pixel = new Texture2D(_graphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        _circle = CreateCircleTexture(128);

        LoadTerrainTextures();
        _boardMesh = BuildBoardMesh();
    }

    public void UnloadContent()
    {
        _boardMesh?.Dispose();
        _pixel?.Dispose();
        _circle?.Dispose();
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
        foreach (var mesh in _terrainMeshes.Values)
            mesh.Dispose();
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

        if (node.Tribe is Tribe tribe && node.HasTribeCounter)
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
        if (_message is not null)
        {
            _messageSeconds -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_messageSeconds <= 0f)
                _message = null;
        }

        var mouse = Mouse.GetState();
        bool releasedThisFrame = _previousMouse.LeftButton == ButtonState.Pressed &&
                                 mouse.LeftButton == ButtonState.Released;

        if (releasedThisFrame)
        {
            if (CanPlaceInitialCamp && HandleInitialPlacementClick(mouse.Position))
            {
                // Initial placement submitted.
            }
            else if (_snapshot?.Status == GameStatus.Setup)
            {
                // During setup, other controls and board actions are disabled.
            }
            else if (RollButton.Contains(mouse.Position) && CanRoll)
                RollRequested?.Invoke();
            else if (EndTurnButton.Contains(mouse.Position) && CanEndTurn)
                EndTurnRequested?.Invoke();
            else if (CanBuild && HandleBoardActionButtonClick(mouse.Position))
            {
                // Mode selection handled.
            }
            else if (CanTrade && HandleTradeClick(mouse.Position))
            {
                // Trade controls handled.
            }
            else if (CanBuild && _boardActionMode != BoardActionMode.None)
                HandleBoardNodeClick(mouse.Position);
        }

        _previousMouse = mouse;
    }

    private bool CanRoll =>
        !Busy && _snapshot is not null && _snapshot.IsMyTurn &&
        _snapshot.Status == GameStatus.Playing &&
        _snapshot.Phase == TurnPhase.Roll && !_snapshot.RollUsed;

    private bool CanEndTurn => !Busy && _snapshot?.IsMyTurn == true &&
                               _snapshot.Status == GameStatus.Playing;

    private bool CanBuild => !Busy && _snapshot?.IsMyTurn == true &&
                             _snapshot.Status == GameStatus.Playing &&
                             _snapshot.Phase == TurnPhase.Actions;
    private bool CanTrade => !Busy && _snapshot?.IsMyTurn == true &&
                             _snapshot.Status == GameStatus.Playing &&
                             _snapshot.Phase == TurnPhase.Trade;
    private bool CanPlaceInitialCamp => !Busy && _snapshot?.Status == GameStatus.Setup &&
                                        _snapshot.IsMyTurn;

    private Rectangle HeaderPanelBounds => new(16, 16, 460, 104);

    private Rectangle PlayersPanelBounds(int playerCount) =>
        new(16, HeaderPanelBounds.Bottom + 10, 430, 12 + playerCount * 46);

    private Rectangle RollButton =>
        new(16, PlayersPanelBounds(_snapshot?.Players.Count ?? 0).Bottom + 12, 200, 48);

    private Rectangle EndTurnButton
    {
        get
        {
            var roll = RollButton;
            return new Rectangle(roll.Right + 12, roll.Y, 220, 48);
        }
    }

    private Rectangle BoardActionButtonsBounds => new(16, EndTurnButton.Bottom + 10, 510, 44);

    private Rectangle TradePanelBounds
    {
        get
        {
            int playerCount = Math.Max(0, (_snapshot?.Players.Count ?? 0) - 1);
            return new Rectangle(_graphicsDevice.Viewport.Width - 370, 16, 350, 178 + playerCount * 38 + Enum.GetValues<ResourceType>().Length * 42);
        }
    }

    private bool HandleBoardActionButtonClick(Point point)
    {
        var bounds = BoardActionButtonsBounds;
        var actions = new[]
        {
            BoardActionMode.CreateExplorer,
            BoardActionMode.MoveExplorer,
            BoardActionMode.ConvertToCamp,
        };

        for (int i = 0; i < actions.Length; i++)
        {
            var button = new Rectangle(bounds.X + i * 170, bounds.Y, 160, bounds.Height);
            if (!button.Contains(point)) continue;

            _boardActionMode = _boardActionMode == actions[i] ? BoardActionMode.None : actions[i];
            _selectedExplorer = null;
            _message = _boardActionMode switch
            {
                BoardActionMode.CreateExplorer => "Нажмите на свободный узел рядом с вашим кемпом.",
                BoardActionMode.MoveExplorer => "Выберите своего исследователя, затем соседний узел.",
                BoardActionMode.ConvertToCamp => "Нажмите на своего исследователя у племени.",
                _ => null,
            };
            _messageSeconds = 8f;
            return true;
        }

        return false;
    }

    private void HandleBoardNodeClick(Point screenPoint)
    {
        if (_snapshot?.You is not { } me) return;

        var clickedNode = GetNodeAtScreenPoint(screenPoint);
        if (clickedNode is null) return;
        var coords = clickedNode.Coords;

        switch (_boardActionMode)
        {
            case BoardActionMode.CreateExplorer:
                if (!CanApplyActionAtNode(clickedNode, me, out var createError))
                {
                    ShowMessage(createError);
                    return;
                }
                CreateExplorerRequested?.Invoke(coords);
                _boardActionMode = BoardActionMode.None;
                break;

            case BoardActionMode.ConvertToCamp:
                if (!CanApplyActionAtNode(clickedNode, me, out var convertError))
                {
                    ShowMessage(convertError);
                    return;
                }
                ConvertExplorerToCampRequested?.Invoke(coords);
                _boardActionMode = BoardActionMode.None;
                break;

            case BoardActionMode.MoveExplorer:
                if (_selectedExplorer is null)
                {
                    if (!CanApplyActionAtNode(clickedNode, me, out var selectError))
                    {
                        ShowMessage(selectError);
                        return;
                    }

                    _selectedExplorer = coords;
                    ShowMessage("Теперь выберите соседний свободный узел.");
                    return;
                }

                if (!CanApplyActionAtNode(clickedNode, me, out var moveError))
                {
                    ShowMessage(moveError);
                    return;
                }

                var source = _selectedExplorer!;
                MoveExplorerRequested?.Invoke(new List<AxialCoords> { source, coords });
                _selectedExplorer = null;
                _boardActionMode = BoardActionMode.None;
                break;
        }
    }

    private bool HandleInitialPlacementClick(Point screenPoint)
    {
        var node = GetNodeAtScreenPoint(screenPoint);
        if (node is null) return false;

        if (!node.IsStartSpace)
        {
            ShowMessage("Начальный кемп можно поставить только на выделенный стартовый узел.");
            return true;
        }

        if (IsOccupied(node.Coords))
        {
            ShowMessage("Этот узел уже занят. Выберите другой стартовый узел.");
            return true;
        }

        InitialPlacementRequested?.Invoke(node.Coords);
        return true;
    }

    private Node? GetNodeAtScreenPoint(Point screenPoint)
    {
        Matrix inverse = Matrix.Invert(FitToScreen(
            _graphicsDevice.Viewport.Width,
            _graphicsDevice.Viewport.Height,
            margin: 20f));
        Vector2 worldPoint = Vector2.Transform(new Vector2(screenPoint.X, screenPoint.Y), inverse);

        Node? closestNode = null;
        float bestDistance = 0.42f;
        foreach (var node in _board.Nodes.Values)
        {
            float distance = Vector2.Distance(worldPoint, HexLayout.ToWorld(node.Coords));
            if (distance >= bestDistance) continue;

            bestDistance = distance;
            closestNode = node;
        }

        return closestNode;
    }

    private void DrawHoveredActionTarget()
    {
        if (_snapshot?.Status == GameStatus.Setup)
        {
            if (!CanPlaceInitialCamp) return;

            var hovered = GetNodeAtScreenPoint(Mouse.GetState().Position);
            foreach (var startNode in _board.Nodes.Values.Where(candidate => candidate.IsStartSpace && !IsOccupied(candidate.Coords)))
            {
                var color = startNode == hovered
                    ? new Color(255, 235, 90) * 0.72f
                    : new Color(130, 245, 120) * 0.48f;
                DrawDisc(HexLayout.ToWorld(startNode.Coords), startNode == hovered ? 1.05f : 0.88f, color);
            }
            return;
        }

        if (!CanBuild || _boardActionMode == BoardActionMode.None || _snapshot?.You is not { } me)
            return;

        var node = GetNodeAtScreenPoint(Mouse.GetState().Position);
        if (node is not null && CanApplyActionAtNode(node, me, out _))
            DrawDisc(HexLayout.ToWorld(node.Coords), 0.92f, new Color(135, 245, 120) * 0.45f);
    }

    private bool CanApplyActionAtNode(Node node, PlayerSnapshot me, out string error)
    {
        var coords = node.Coords;
        switch (_boardActionMode)
        {
            case BoardActionMode.CreateExplorer:
                if (!HasResources(me, (ResourceType.Meat, 1), (ResourceType.Hides, 1)))
                    error = "Для исследователя нужны мясо и шкуры.";
                else if (me.Explorers.Count >= 2)
                    error = "У вас уже два исследователя.";
                else if (IsOccupied(coords))
                    error = "Узел уже занят.";
                else if (!me.Camps.Any(camp => camp.ToCoords().NodeGetNodeNeighbors().Contains(coords)))
                    error = "Исследователя можно поставить рядом со своим кемпом.";
                else
                {
                    error = string.Empty;
                    return true;
                }
                return false;

            case BoardActionMode.ConvertToCamp:
                if (!IsOwnExplorerAt(me, coords))
                    error = "Выберите своего исследователя.";
                else if (!HasResources(me,
                             (ResourceType.Hides, 1), (ResourceType.Bone, 1), (ResourceType.Flint, 1)))
                    error = "Для кемпа нужны шкуры, кость и кремень.";
                else if (!node.HasTribeCounter || node.Tribe is null)
                    error = "Кемп можно построить только на узле с племенем.";
                else
                {
                    error = string.Empty;
                    return true;
                }
                return false;

            case BoardActionMode.MoveExplorer:
                if (_selectedExplorer is null)
                {
                    if (!IsOwnExplorerAt(me, coords))
                        error = "Выберите своего исследователя.";
                    else if (!HasResources(me, (ResourceType.Meat, 1)))
                        error = "Для перемещения нужно мясо.";
                    else
                    {
                        error = string.Empty;
                        return true;
                    }
                    return false;
                }

                var source = _selectedExplorer!;
                var path = new Path(source, coords);
                if (source.Equals(coords) || !source.NodeGetNodeNeighbors().Contains(coords))
                    error = "Выберите соседний узел.";
                else if (!HasResources(me, (ResourceType.Meat, 1)))
                    error = "Для перемещения нужно мясо.";
                else if (IsOccupied(coords))
                    error = "Узел уже занят.";
                else if (_board.BlockedPaths.Contains(path))
                    error = "Этот проход закрыт.";
                else if (_board.ExplorationPaths.TryGetValue(path, out var explorationPath) &&
                         (me.ProgressLevel(ProgressType.Clothing) < explorationPath.ClothingRequirement ||
                          me.ProgressLevel(ProgressType.Shelter) < explorationPath.ShelterRequirement))
                    error = "Не хватает уровня одежды или жилища для этого пути.";
                else
                {
                    error = string.Empty;
                    return true;
                }
                return false;

            default:
                error = "Сначала выберите действие.";
                return false;
        }
    }

    private bool IsOccupied(AxialCoords coords) => _snapshot?.Players.Any(player =>
        player.Camps.Any(camp => camp.Q == coords.q && camp.R == coords.r) ||
        player.Explorers.Any(explorer => explorer.Q == coords.q && explorer.R == coords.r)) == true;

    private static bool IsOwnExplorerAt(PlayerSnapshot player, AxialCoords coords) =>
        player.Explorers.Any(explorer => explorer.Q == coords.q && explorer.R == coords.r);

    private static bool HasResources(PlayerSnapshot player, params (ResourceType Resource, int Amount)[] cost) =>
        cost.All(item => player.Resource(item.Resource) >= item.Amount);

    private bool HandleTradeClick(Point point)
    {
        var panel = TradePanelBounds;
        if (!panel.Contains(point)) return false;

        var modeButtonsY = panel.Y + 34;
        if (new Rectangle(panel.X + 12, modeButtonsY, 150, 32).Contains(point))
            _tradeMode = TradeMode.Player;
        else if (new Rectangle(panel.X + 174, modeButtonsY, 160, 32).Contains(point))
            _tradeMode = TradeMode.Bank;
        else
        {
            int y = panel.Y + 78;
            if (_tradeMode == TradeMode.Player)
            {
                y += 24; // заголовок "Кому передать"
                foreach (var player in _snapshot!.Players.Where(player => player.Id != _snapshot.YouPlayerId))
                {
                    if (new Rectangle(panel.X + 12, y, panel.Width - 24, 34).Contains(point))
                    {
                        _tradeTargetId = player.Id;
                        break;
                    }
                    y += 38;
                }
                y += 26; // подпись режима передачи
            }
            else
            {
                y += 46; // заголовок и подсказка обмена с банком
            }
            foreach (ResourceType resource in Enum.GetValues<ResourceType>())
            {
                if (new Rectangle(panel.Right - 92, y, 28, 32).Contains(point))
                {
                    _tradeOffer.TryGetValue(resource, out int current);
                    int available = _snapshot!.You?.Resource(resource) ?? 0;
                    if (current < available) _tradeOffer[resource] = current + 1;
                    return true;
                }
                if (new Rectangle(panel.Right - 128, y, 28, 32).Contains(point))
                {
                    if (_tradeOffer.TryGetValue(resource, out int current) && current > 0)
                    {
                        if (current == 1) _tradeOffer.Remove(resource);
                        else _tradeOffer[resource] = current - 1;
                    }
                    return true;
                }
                if (_tradeMode == TradeMode.Bank && new Rectangle(panel.X + 12, y, panel.Width - 160, 32).Contains(point))
                    _bankResourceToGet = resource;
                y += 42;
            }

            var submit = new Rectangle(panel.X + 12, panel.Bottom - 44, panel.Width - 24, 34);
            if (submit.Contains(point)) SubmitTrade();
        }

        return true;
    }

    private void SubmitTrade()
    {
        if (_tradeOffer.Count == 0)
        {
            ShowMessage("Сначала выберите ресурсы для передачи.");
            return;
        }

        var offer = new Dictionary<ResourceType, int>(_tradeOffer);
        if (_tradeMode == TradeMode.Player)
        {
            if (_tradeTargetId is not Guid targetId)
            {
                ShowMessage("Выберите игрока, которому передать ресурсы.");
                return;
            }

            TradeWithPlayerRequested?.Invoke(targetId, offer);
        }
        else
        {
            if (offer.Count != 3)
            {
                ShowMessage("По правилам сервера для обмена с банком нужны 3 разных вида ресурсов.");
                return;
            }

            TradeWithBankRequested?.Invoke(offer, _bankResourceToGet);
        }

        _tradeOffer.Clear();
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
        DrawHoveredActionTarget();
        DrawPieces();
        _spriteBatch.End();

        DrawHud();
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

    /// <summary>
    /// Применяет полученное состояние: запоминает, где стоят кампы и исследователи.
    /// </summary>
    public void ApplySnapshot(GameSnapshot snapshot)
    {
        _snapshot = snapshot;
    }

    public void ShowMessage(string message)
    {
        _message = message;
        _messageSeconds = 8f;
    }

    private void DrawPieces()
    {
        if (_snapshot is null) return;

        foreach (var player in _snapshot.Players)
        {
            Color color = PlayerColorToXna(player.Color);
            bool isCurrent = player.Id == _snapshot.CurrentPlayerId;

            foreach (var camp in player.Camps)
            {
                Vector2 position = WorldPosition(camp);
                DrawDisc(position, 0.46f, PieceOutline);
                DrawDisc(position, 0.36f, color);
                if (isCurrent)
                    DrawDisc(position, 0.14f, PieceOutline);
            }

            foreach (var explorer in player.Explorers)
            {
                Vector2 position = WorldPosition(explorer);
                DrawDiamond(position, 0.5f, PieceOutline);
                DrawDiamond(position, 0.38f, color);
            }
        }

        if (_selectedExplorer is not null)
            DrawDisc(HexLayout.ToWorld(_selectedExplorer), 0.88f, Color.Yellow * 0.8f);
    }

    private static readonly Color PieceOutline = new(22, 19, 16);

    private void DrawDisc(Vector2 center, float diameter, Color color)
    {
        _spriteBatch.Draw(
            _circle,
            center,
            sourceRectangle: null,
            color: color,
            rotation: 0f,
            origin: new Vector2(_circle.Width / 2f, _circle.Height / 2f),
            scale: new Vector2(diameter / _circle.Width),
            effects: SpriteEffects.None,
            layerDepth: 0f);
    }

    private void DrawDiamond(Vector2 center, float size, Color color)
    {
        _spriteBatch.Draw(
            _pixel,
            center,
            sourceRectangle: null,
            color: color,
            rotation: MathHelper.PiOver4,
            origin: new Vector2(0.5f, 0.5f),
            scale: new Vector2(size),
            effects: SpriteEffects.None,
            layerDepth: 0f);
    }

    private void DrawHud()
    {
        if (_snapshot is null) return;

        _spriteBatch.Begin(samplerState: SamplerState.LinearClamp);

        var headerPanel = HeaderPanelBounds;
        DrawPanel(headerPanel);

        string header = _snapshot.CurrentPlayer is { } currentPlayer
            ? $"Ходит: {currentPlayer.Name}{(_snapshot.IsMyTurn ? " (вы)" : string.Empty)}"
            : "Ход неизвестен";
        _spriteBatch.DrawString(
            _uiTitleFont,
            header,
            new Vector2(headerPanel.X + 16, headerPanel.Y + 8),
            new Color(242, 228, 202));

        string phaseLine = $"Фаза: {PhaseName(_snapshot.Phase)}";
        if (_snapshot.LastRoll is int roll)
            phaseLine += $" · выпало {roll}";
        else if (_snapshot.RollUsed)
            phaseLine += " · кубик брошен";
        _spriteBatch.DrawString(
            _uiFont,
            phaseLine,
            new Vector2(headerPanel.X + 16, headerPanel.Y + 48),
            new Color(206, 194, 172));

        string whoLine = _snapshot.You is { } you
            ? $"Вы: {you.Name}"
            : "Вы: не в сессии";
        if (_snapshot.GameCode is { Length: > 0 } code)
            whoLine += $" · код {code}";
        _spriteBatch.DrawString(
            _uiFont,
            whoLine,
            new Vector2(headerPanel.X + 16, headerPanel.Y + 74),
            new Color(176, 166, 148));

        DrawPlayersPanel();
        if (_snapshot.Status == GameStatus.Setup)
            DrawSetupInstructions();
        else
        {
            DrawActionButtons();
            if (CanBuild) DrawBoardActionButtons();
            if (CanTrade) DrawTradePanel();
        }
        DrawMessage();

        _spriteBatch.End();
    }

    private void DrawMessage()
    {
        if (_message is null) return;

        var lines = WrapText(_message, 620f);
        const int lineHeight = 26;
        int messageTop = _snapshot?.Status == GameStatus.Setup
            ? PlayersPanelBounds(_snapshot.Players.Count).Bottom + 118
            : CanBuild ? BoardActionButtonsBounds.Bottom + 12 : EndTurnButton.Bottom + 12;
        var panel = new Rectangle(
            16,
            messageTop,
            660,
            12 + lines.Count * lineHeight);
        DrawPanel(panel);

        float y = panel.Y + 6;
        foreach (var line in lines)
        {
            _spriteBatch.DrawString(
                _uiFont,
                line,
                new Vector2(panel.X + 12, y),
                new Color(232, 140, 120));
            y += lineHeight;
        }
    }

    private void DrawSetupInstructions()
    {
        var you = _snapshot?.You;
        var bounds = new Rectangle(16, PlayersPanelBounds(_snapshot?.Players.Count ?? 0).Bottom + 12, 430, 94);
        DrawPanel(bounds);

        var text = CanPlaceInitialCamp
            ? $"Расстановка кемпов\nВыберите стартовый узел: {you?.Camps.Count ?? 0}/3 поставлено."
            : $"Расстановка кемпов\nОжидайте ход игрока { _snapshot?.CurrentPlayer?.Name ?? "…" }.";
        var lines = text.Split('\n');
        _spriteBatch.DrawString(_uiTitleFont, lines[0], new Vector2(bounds.X + 12, bounds.Y + 8), new Color(242, 228, 202));
        _spriteBatch.DrawString(_uiFont, lines[1], new Vector2(bounds.X + 12, bounds.Y + 48), new Color(206, 194, 172));
    }

    private List<string> WrapText(string text, float maxWidth)
    {
        var lines = new List<string>();
        var current = string.Empty;

        foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            string candidate = current.Length == 0 ? word : current + " " + word;
            if (current.Length > 0 && _uiFont.MeasureString(candidate).X > maxWidth)
            {
                lines.Add(current);
                current = word;
            }
            else
            {
                current = candidate;
            }
        }

        if (current.Length > 0)
            lines.Add(current);

        return lines;
    }

    private void DrawPlayersPanel()
    {
        const int rowHeight = 46;

        var players = _snapshot!.Players;
        var panel = PlayersPanelBounds(players.Count);
        DrawPanel(panel);

        int y = panel.Y + 6;
        foreach (var player in players)
        {
            Color color = PlayerColorToXna(player.Color);
            string name = player.Id == _snapshot.CurrentPlayerId
                ? $"{player.Name}  ★"
                : player.Name;
            _spriteBatch.Draw(_pixel, new Rectangle(panel.X + 12, y + 6, 14, 14), color);
            _spriteBatch.DrawString(
                _uiFont,
                name,
                new Vector2(panel.X + 34, y),
                new Color(238, 228, 208));

            string stats =
                $"М{player.Resource(ResourceType.Meat)} " +
                $"К{player.Resource(ResourceType.Bone)} " +
                $"Кр{player.Resource(ResourceType.Flint)} " +
                $"Ш{player.Resource(ResourceType.Hides)}   " +
                $"кампы {player.Camps.Count} · иссл {player.Explorers.Count} · племён {player.TribesCollected}";
            _spriteBatch.DrawString(
                _uiFont,
                stats,
                new Vector2(panel.X + 34, y + 22),
                new Color(176, 166, 148));

            y += rowHeight;
        }
    }

    private void DrawActionButtons()
    {
        var mouse = Mouse.GetState();

        var roll = RollButton;
        DrawButton(roll, "Бросок", CanRoll, roll.Contains(mouse.Position));

        var endTurn = EndTurnButton;
        DrawButton(endTurn, EndTurnLabel, CanEndTurn, endTurn.Contains(mouse.Position));
    }

    private void DrawBoardActionButtons()
    {
        var bounds = BoardActionButtonsBounds;
        var mouse = Mouse.GetState();
        var actions = new[]
        {
            (BoardActionMode.CreateExplorer, "Исследователь"),
            (BoardActionMode.MoveExplorer, "Переместить"),
            (BoardActionMode.ConvertToCamp, "Кемп"),
        };

        for (int i = 0; i < actions.Length; i++)
        {
            var button = new Rectangle(bounds.X + i * 170, bounds.Y, 160, bounds.Height);
            bool active = _boardActionMode == actions[i].Item1;
            DrawButton(button, actions[i].Item2, true, active || button.Contains(mouse.Position));
        }
    }

    private void DrawTradePanel()
    {
        var panel = TradePanelBounds;
        DrawPanel(panel);
        _spriteBatch.DrawString(_uiTitleFont, "Торговля", new Vector2(panel.X + 12, panel.Y + 6), new Color(242, 228, 202));

        var playerMode = new Rectangle(panel.X + 12, panel.Y + 34, 150, 32);
        var bankMode = new Rectangle(panel.X + 174, panel.Y + 34, 160, 32);
        DrawButton(playerMode, "Игроку", true, _tradeMode == TradeMode.Player);
        DrawButton(bankMode, "С банком", true, _tradeMode == TradeMode.Bank);

        int y = panel.Y + 78;
        if (_tradeMode == TradeMode.Player)
        {
            _spriteBatch.DrawString(_uiFont, "Кому передать:", new Vector2(panel.X + 12, y), new Color(206, 194, 172));
            y += 24;
            foreach (var player in _snapshot!.Players.Where(player => player.Id != _snapshot.YouPlayerId))
            {
                var target = new Rectangle(panel.X + 12, y, panel.Width - 24, 34);
                bool selected = _tradeTargetId == player.Id;
                DrawButton(target, player.Name, true, selected || target.Contains(Mouse.GetState().Position));
                y += 38;
            }
            _spriteBatch.DrawString(_uiFont, "Передать игроку (подарок)", new Vector2(panel.X + 12, y + 2), new Color(206, 194, 172));
            y += 26;
        }
        else
        {
            _spriteBatch.DrawString(_uiFont, $"Получить: {_bankResourceToGet}", new Vector2(panel.X + 12, y), new Color(206, 194, 172));
            _spriteBatch.DrawString(_uiFont, "Нажмите на ресурс, который хотите получить.", new Vector2(panel.X + 12, y + 22), new Color(176, 166, 148));
            y += 46;
        }

        foreach (ResourceType resource in Enum.GetValues<ResourceType>())
        {
            var minus = new Rectangle(panel.Right - 128, y, 28, 32);
            var plus = new Rectangle(panel.Right - 92, y, 28, 32);
            DrawButton(minus, "−", true, minus.Contains(Mouse.GetState().Position));
            DrawButton(plus, "+", true, plus.Contains(Mouse.GetState().Position));

            _tradeOffer.TryGetValue(resource, out int amount);
            int available = _snapshot!.You?.Resource(resource) ?? 0;
            string label = _tradeMode == TradeMode.Bank && _bankResourceToGet == resource
                ? $"▶ {resource}: {amount}/{available}"
                : $"{resource}: {amount}/{available}";
            _spriteBatch.DrawString(_uiFont, label, new Vector2(panel.X + 12, y + 5), new Color(238, 228, 208));
            y += 42;
        }

        var submit = new Rectangle(panel.X + 12, panel.Bottom - 44, panel.Width - 24, 34);
        DrawButton(submit, _tradeMode == TradeMode.Bank ? "Обменять" : "Передать", true, submit.Contains(Mouse.GetState().Position));
    }

    // В фазах броска и торговли та же кнопка двигает фазу, а не передаёт ход.
    private string EndTurnLabel => _snapshot?.Phase switch
    {
        TurnPhase.Roll => "Дальше · торговля",
        TurnPhase.Trade => "Дальше · действия",
        _ => "Завершить ход",
    };

    private void DrawButton(Rectangle bounds, string text, bool enabled, bool hovered)
    {
        Color fill = !enabled
            ? new Color(38, 36, 32)
            : hovered ? new Color(104, 84, 56) : new Color(70, 56, 40);
        _spriteBatch.Draw(_pixel, bounds, fill);

        Color border = enabled ? new Color(198, 170, 120) : new Color(88, 82, 72);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 2), border);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - 2, bounds.Width, 2), border);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, 2, bounds.Height), border);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.Right - 2, bounds.Y, 2, bounds.Height), border);

        Vector2 size = _uiFont.MeasureString(text);
        _spriteBatch.DrawString(
            _uiFont,
            text,
            new Vector2(bounds.X + (bounds.Width - size.X) / 2f, bounds.Y + (bounds.Height - size.Y) / 2f),
            enabled ? new Color(246, 236, 214) : new Color(132, 124, 112));
    }

    private static string PhaseName(TurnPhase phase) => phase switch
    {
        TurnPhase.Roll => "бросок",
        TurnPhase.Trade => "торговля",
        TurnPhase.Actions => "действия",
        _ => phase.ToString(),
    };

    private void DrawPanel(Rectangle bounds)
    {
        _spriteBatch.Draw(_pixel, bounds, new Color(18, 17, 15) * 0.82f);

        var border = new Color(128, 110, 80) * 0.9f;
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), border);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), border);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, 1, bounds.Height), border);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.Right - 1, bounds.Y, 1, bounds.Height), border);
    }

    private Texture2D CreateCircleTexture(int size)
    {
        var texture = new Texture2D(_graphicsDevice, size, size);
        var pixels = new Color[size * size];
        float radius = size / 2f - 1f;
        var center = new Vector2(size / 2f, size / 2f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float alpha = MathHelper.Clamp(radius - distance + 1f, 0f, 1f);
                pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetData(pixels);
        return texture;
    }

    private static Vector2 WorldPosition(CoordDto coord) =>
        HexLayout.ToWorld(new AxialCoords(coord.Q, coord.R));

    private static Color PlayerColorToXna(PlayerColor color) => color switch
    {
        PlayerColor.Red => new Color(214, 69, 65),
        PlayerColor.Blue => new Color(66, 122, 214),
        PlayerColor.White => new Color(238, 236, 228),
        PlayerColor.Orange => new Color(226, 138, 54),
        _ => Color.Magenta,
    };

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
