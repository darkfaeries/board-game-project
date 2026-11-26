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
    private Texture2D _ring = null!;

    // Действия игрока на карте.
    public event Action? RollRequested;
    public event Action? EndTurnRequested;
    public event Action<AxialCoords>? InitialPlacementRequested;
    public event Action<AxialCoords>? CreateExplorerRequested;
    public event Action<AxialCoords>? ConvertExplorerToCampRequested;
    public event Action<List<AxialCoords>>? MoveExplorerRequested;
    public event Action<ProgressType>? ProgressRequested;
    public event Action<Dictionary<ResourceType, int>>? DiscardResourcesRequested;
    public event Action<Guid, Dictionary<ResourceType, int>, Dictionary<ResourceType, int>>? TradeWithPlayerRequested;
    public event Action<Guid, Guid, bool>? TradeDecisionRequested;
    public event Action<Dictionary<ResourceType, int>, ResourceType>? TradeWithBankRequested;
    public event Action? ReturnToMainMenuRequested;
    public bool Busy { get; set; }

    private MouseState _previousMouse;
    private string? _message;
    private float _messageSeconds;
    private BoardActionMode _boardActionMode;
    private AxialCoords? _selectedExplorer;
    private readonly List<AxialCoords> _selectedRoute = new();
    private TradeMode _tradeMode;
    private Guid? _tradeTargetId;
    private readonly Dictionary<ResourceType, int> _tradeOffer = new();
    private readonly Dictionary<ResourceType, int> _tradeRequest = new();
    private readonly Dictionary<ResourceType, int> _discardSelection = new();
    private readonly HashSet<AxialCoords> _collectedTribeNodes = new();
    private int? _tribeCounterPlayerCount;
    private ResourceType _bankResourceToGet = ResourceType.Meat;

    public GameSnapshot? Snapshot => _snapshot;
    private bool IsGameOver => _snapshot?.WinnerPlayerId is not null;

    public void Reset()
    {
        _snapshot = null;
        _message = null;
        _messageSeconds = 0f;
        _boardActionMode = BoardActionMode.None;
        _selectedExplorer = null;
        _selectedRoute.Clear();
        _tradeMode = TradeMode.Player;
        _tradeTargetId = null;
        _tradeOffer.Clear();
        _tradeRequest.Clear();
        _discardSelection.Clear();
        _collectedTribeNodes.Clear();
        _tribeCounterPlayerCount = null;
        _bankResourceToGet = ResourceType.Meat;
        Busy = false;

        if (_board is not null)
        {
            foreach (var node in _board.Nodes.Values)
            {
                node.HasTribeCounter = false;
            }
        }
    }

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
            _board = ClientBoardLoader.LoadBoard(hexesReader, nodesReader, pathsReader);
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
        _ring = CreateRingTexture(128);

        LoadTerrainTextures();
        _boardMesh = BuildBoardMesh();
    }

    public void UnloadContent()
    {
        _boardMesh?.Dispose();
        _pixel?.Dispose();
        _circle?.Dispose();
        _ring?.Dispose();
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

        if (IsGameOver)
        {
            if (releasedThisFrame && VictoryMainMenuButtonBounds.Contains(mouse.Position))
            {
                ReturnToMainMenuRequested?.Invoke();
            }
            _previousMouse = mouse;
            return;
        }

        if (releasedThisFrame)
        {
            if (HasIncomingTradeForMe && HandleIncomingTradeClick(mouse.Position))
            {
                // Incoming trade decision handled.
            }
            else if (HasPendingDiscards && DiscardPanelBounds.Contains(mouse.Position))
            {
                if (HasPendingDiscard) HandleDiscardClick(mouse.Position);
            }
            else if (CanPlaceInitialCamp && HandleInitialPlacementClick(mouse.Position))
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
            else if (CanBuild && HandleProgressClick(mouse.Position))
            {
                // Progress controls handled.
            }
            else if (CanBuild && _boardActionMode != BoardActionMode.None)
                HandleBoardNodeClick(mouse.Position);
        }

        _previousMouse = mouse;
    }

    private bool CanRoll =>
        !IsGameOver && !Busy && _snapshot is not null && _snapshot.IsMyTurn &&
        _snapshot.Status == GameStatus.Playing &&
        _snapshot.Phase == TurnPhase.Roll && !_snapshot.RollUsed;

    private bool CanEndTurn => !IsGameOver && !Busy && _snapshot?.IsMyTurn == true &&
                               _snapshot.Status == GameStatus.Playing && !HasPendingDiscards && !HasPendingTrade &&
                               (_snapshot.Phase != TurnPhase.Roll || _snapshot.RollUsed);

    private bool CanBuild => !IsGameOver && !Busy && _snapshot?.IsMyTurn == true &&
                             _snapshot.Status == GameStatus.Playing &&
                             _snapshot.Phase == TurnPhase.Actions;
    private bool CanTrade => !IsGameOver && !Busy && _snapshot?.IsMyTurn == true &&
                             _snapshot.Status == GameStatus.Playing &&
                             _snapshot.Phase == TurnPhase.Trade;
    private bool CanPlaceInitialCamp => !IsGameOver && !Busy && _snapshot?.Status == GameStatus.Setup &&
                                        _snapshot.IsMyTurn;
    private bool HasPendingTrade => _snapshot?.PendingTrade is not null;
    private bool HasIncomingTradeForMe => _snapshot?.PendingTrade is { } trade &&
                                          _snapshot.YouPlayerId is Guid myId &&
                                          trade.TargetPlayerId == myId;
    private bool HasPendingDiscard => _snapshot?.You?.DiscardResourcesRequired is > 0;
    private bool HasPendingDiscards => _snapshot?.Players.Any(player => player.DiscardResourcesRequired is > 0) == true;
    private bool CanSubmitDiscard => !IsGameOver && HasPendingDiscard && !Busy &&
        _snapshot?.You?.DiscardResourcesRequired == _discardSelection.Values.Sum();

    private Rectangle HeaderPanelBounds => new(16, 16, 450, 160);

    private Rectangle PlayersPanelBounds(int playerCount)
    {
        const int cardW = 280;
        const int cardH = 72;
        const int gapX = 10;
        const int gapY = 8;
        int maxCols = _graphicsDevice.Viewport.Width >= 1380 ? 3 : 2;
        int cols = Math.Min(maxCols, Math.Max(1, playerCount));
        int rows = Math.Max(1, (playerCount + cols - 1) / cols);
        int width = 16 + cols * cardW + (cols - 1) * gapX;
        int height = 16 + rows * cardH + (rows - 1) * gapY;
        return new Rectangle(_graphicsDevice.Viewport.Width - width - 16, 16, width, height);
    }

    private Rectangle RollButton =>
        new(16, _graphicsDevice.Viewport.Height - 48 - 16, 200, 48);

    private Rectangle EndTurnButton
    {
        get
        {
            var roll = RollButton;
            return new Rectangle(roll.Right + 12, roll.Y, 220, 48);
        }
    }

    private Rectangle BoardActionButtonsBounds => new(16, RollButton.Y - 44 - 10, 510, 44);

    private Rectangle DiscardPanelBounds
    {
        get
        {
            int height = 90 + Enum.GetValues<ResourceType>().Length * 38;
            return new Rectangle(16, RollButton.Y - height - 12, 560, height);
        }
    }

    private Rectangle DiscardRowBounds(int index)
    {
        var panel = DiscardPanelBounds;
        return new Rectangle(panel.X + 12, panel.Y + 48 + index * 38, panel.Width - 24, 32);
    }

    private bool HandleDiscardClick(Point point)
    {
        if (!HasPendingDiscard || _snapshot?.You is not { } player) return true;

        for (int i = 0; i < Enum.GetValues<ResourceType>().Length; i++)
        {
            var resource = Enum.GetValues<ResourceType>()[i];
            var row = DiscardRowBounds(i);
            int controlX = row.Right - 212;
            var minusTen = new Rectangle(controlX, row.Y, 42, row.Height);
            var minusOne = new Rectangle(controlX + 46, row.Y, 38, row.Height);
            var plusOne = new Rectangle(controlX + 88, row.Y, 38, row.Height);
            var plusTen = new Rectangle(controlX + 130, row.Y, 42, row.Height);
            _discardSelection.TryGetValue(resource, out int amount);

            if (minusTen.Contains(point))
                SetDiscardAmount(resource, Math.Max(0, amount - 10));
            else if (minusOne.Contains(point))
                SetDiscardAmount(resource, Math.Max(0, amount - 1));
            else if (plusOne.Contains(point))
                SetDiscardAmount(resource, Math.Min(player.Resource(resource), amount + 1));
            else if (plusTen.Contains(point))
                SetDiscardAmount(resource, Math.Min(player.Resource(resource), amount + 10));
            else
                continue;

            return true;
        }

        var panel = DiscardPanelBounds;
        var submit = new Rectangle(panel.X + 12, panel.Bottom - 42, panel.Width - 24, 32);
        if (submit.Contains(point) && CanSubmitDiscard)
        {
            DiscardResourcesRequested?.Invoke(new Dictionary<ResourceType, int>(
                _discardSelection.Where(item => item.Value > 0)));
            _discardSelection.Clear();
        }
        return true;
    }

    private void SetDiscardAmount(ResourceType resource, int amount)
    {
        if (amount <= 0) _discardSelection.Remove(resource);
        else _discardSelection[resource] = amount;
    }

    private Rectangle TradePanelBounds
    {
        get
        {
            int width = _tradeMode == TradeMode.Player ? 430 : 350;
            int playerCount = _tradeMode == TradeMode.Player
                ? Math.Max(0, (_snapshot?.Players.Count ?? 0) - 1)
                : 0;
            int headerHeight = _tradeMode == TradeMode.Player ? 196 : 178;
            int top = PlayersPanelBounds(_snapshot?.Players.Count ?? 0).Bottom + 12;
            return new Rectangle(_graphicsDevice.Viewport.Width - width - 16, top, width, headerHeight + playerCount * 38 + Enum.GetValues<ResourceType>().Length * 42);
        }
    }

    private Rectangle IncomingTradePanelBounds
    {
        get
        {
            var viewport = _graphicsDevice.Viewport;
            const int width = 420;
            const int height = 220;
            return new Rectangle(
                (viewport.Width - width) / 2,
                110,
                width,
                height);
        }
    }

    private Rectangle ProgressPanelBounds =>
        new(_graphicsDevice.Viewport.Width - 430 - 16, PlayersPanelBounds(_snapshot?.Players.Count ?? 0).Bottom + 12, 430, 56 + Enum.GetValues<ProgressType>().Length * 44);

    private Rectangle ProgressButtonBounds(int index)
    {
        var panel = ProgressPanelBounds;
        return new Rectangle(panel.X + 12, panel.Y + 42 + index * 44, 158, 34);
    }

    private bool HandleProgressClick(Point point)
    {
        for (int i = 0; i < ProgressTypes.Length; i++)
        {
            if (!ProgressButtonBounds(i).Contains(point)) continue;

            var progressType = ProgressTypes[i];
            if (_snapshot?.You is not { } player) return true;

            int level = player.ProgressLevel(progressType);
            if (level >= 5)
            {
                ShowMessage("Этот параметр уже достиг максимального уровня.");
                return true;
            }

            var cost = ProgressCost(level + 1);
            if (!HasResources(player, cost.Select(item => (item.Key, item.Value)).ToArray()))
            {
                ShowMessage("Не хватает ресурсов для повышения параметра.");
                return true;
            }

            ProgressRequested?.Invoke(progressType);
            return true;
        }

        return ProgressPanelBounds.Contains(point);
    }

    private static readonly ProgressType[] ProgressTypes =
    {
        ProgressType.Food,
        ProgressType.Hunting,
        ProgressType.Shelter,
        ProgressType.Clothing,
    };

    private static Dictionary<ResourceType, int> ProgressCost(int targetLevel) => targetLevel switch
    {
        1 => new() { [ResourceType.Flint] = 1 },
        2 => new() { [ResourceType.Bone] = 1 },
        3 or 4 => new() { [ResourceType.Bone] = 1, [ResourceType.Flint] = 1 },
        5 => new() { [ResourceType.Hides] = 1, [ResourceType.Bone] = 1, [ResourceType.Flint] = 1 },
        _ => new(),
    };

    private static string ProgressName(ProgressType progressType) => progressType switch
    {
        ProgressType.Food => "Еда",
        ProgressType.Hunting => "Охота",
        ProgressType.Shelter => "Жилище",
        ProgressType.Clothing => "Одежда",
        _ => progressType.ToString(),
    };

    private static string ProgressCostLabel(Dictionary<ResourceType, int> cost)
    {
        if (cost.Count == 0) return "Макс. уровень";

        return string.Join(", ", cost.Select(item => $"{ResourceName(item.Key)} {item.Value}"));
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

            if (actions[i] == BoardActionMode.MoveExplorer &&
                _boardActionMode == BoardActionMode.MoveExplorer && _selectedRoute.Count > 1)
            {
                MoveExplorerRequested?.Invoke(new List<AxialCoords>(_selectedRoute));
                _selectedRoute.Clear();
                _selectedExplorer = null;
                _boardActionMode = BoardActionMode.None;
                return true;
            }

            _boardActionMode = _boardActionMode == actions[i] ? BoardActionMode.None : actions[i];
            _selectedExplorer = null;
            _selectedRoute.Clear();
            _message = _boardActionMode switch
            {
                BoardActionMode.CreateExplorer => "Нажмите на свободный узел рядом с вашим лагерем.",
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
                    _selectedRoute.Clear();
                    _selectedRoute.Add(coords);
                    ShowMessage("Выберите соседние свободные узлы по маршруту, затем снова нажмите «Переместить».");
                    return;
                }

                if (_selectedRoute.Any(position => position.Equals(coords)))
                {
                    int routeIndex = _selectedRoute.FindIndex(position => position.Equals(coords));
                    if (routeIndex >= 0 && routeIndex < _selectedRoute.Count - 1)
                        _selectedRoute.RemoveRange(routeIndex + 1, _selectedRoute.Count - routeIndex - 1);
                    return;
                }

                if (!CanAppendMoveStep(coords, me, out var moveError))
                    ShowMessage(moveError);
                else
                {
                    _selectedRoute.Add(coords);
                    ShowMessage($"Маршрут: {_selectedRoute.Count - 1} шаг(а). Нажмите «Переместить», чтобы выполнить.");
                }
                break;
        }
    }

    private bool HandleInitialPlacementClick(Point screenPoint)
    {
        var node = GetNodeAtScreenPoint(screenPoint);
        if (node is null) return false;

        if (!node.IsStartSpace)
        {
            ShowMessage("Начальный лагерь можно поставить только на выделенный стартовый узел.");
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
                var position = HexLayout.ToWorld(startNode.Coords);
                bool isHovered = startNode == hovered;
                DrawRing(
                    position,
                    isHovered ? 0.82f : 0.7f,
                    isHovered ? new Color(255, 218, 112) : new Color(184, 221, 153),
                    isHovered ? 0.95f : 0.7f);

                if (isHovered)
                    DrawDisc(position, 0.38f, new Color(255, 218, 112) * 0.18f);
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
                else if (!coords.NodeGetNodeNeighbors().Any(c => me.Camps.Any(camp => camp.Q == c.q && camp.R == c.r)))
                    error = "Исследователя можно поставить рядом со своим лагерем.";
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
                    error = "Для лагеря нужны шкуры, кость и кремень.";
                else if (!node.HasTribeCounter || node.Tribe is null)
                    error = "Лагерь можно построить только на узле с племенем.";
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
                return CanAppendMoveStep(coords, me, out error);

            default:
                error = "Сначала выберите действие.";
                return false;
        }
    }

    private bool CanAppendMoveStep(AxialCoords coords, PlayerSnapshot player, out string error)
    {
        if (_selectedRoute.Count == 0)
        {
            error = "Сначала выберите своего исследователя.";
            return false;
        }

        var from = _selectedRoute[^1];
        var path = new Path(from, coords);
        int steps = _selectedRoute.Count;
        int movementRange = 2 + player.ProgressLevel(ProgressType.Food);
        int meatCost = (steps + movementRange - 1) / movementRange;

        if (!from.NodeGetNodeNeighbors().Contains(coords))
            error = "Следующий узел маршрута должен быть соседним.";
        else if (IsOccupied(coords))
            error = "Узел уже занят.";
        else if (_board.BlockedPaths.Contains(path))
            error = "Этот проход закрыт.";
        else if (!HasResources(player, (ResourceType.Meat, meatCost)))
            error = $"Для маршрута длиной {steps} нужен {meatCost} мяса.";
        else if (_board.ExplorationPaths.TryGetValue(path, out var explorationPath) &&
                 (player.ProgressLevel(ProgressType.Clothing) < explorationPath.ClothingRequirement ||
                  player.ProgressLevel(ProgressType.Shelter) < explorationPath.ShelterRequirement))
            error = "Не хватает уровня одежды или жилища для этого пути.";
        else
        {
            error = string.Empty;
            return true;
        }

        return false;
    }

    private bool IsOccupied(AxialCoords coords) => _snapshot?.Players.Any(player =>
        player.Camps.Any(camp => camp.Q == coords.q && camp.R == coords.r) ||
        player.Explorers.Any(explorer => explorer.Q == coords.q && explorer.R == coords.r)) == true;

    private static bool IsOwnExplorerAt(PlayerSnapshot player, AxialCoords coords) =>
        player.Explorers.Any(explorer => explorer.Q == coords.q && explorer.R == coords.r);

    private static bool HasResources(PlayerSnapshot player, params (ResourceType Resource, int Amount)[] cost) =>
        cost.All(item => player.Resource(item.Resource) >= item.Amount);

    private bool HandleIncomingTradeClick(Point point)
    {
        if (_snapshot?.PendingTrade is not { } trade) return false;

        var panel = IncomingTradePanelBounds;
        if (!panel.Contains(point)) return false;

        int buttonWidth = (panel.Width - 36) / 2;
        var acceptButton = new Rectangle(panel.X + 12, panel.Bottom - 46, buttonWidth, 34);
        var declineButton = new Rectangle(acceptButton.Right + 12, panel.Bottom - 46, buttonWidth, 34);

        if (acceptButton.Contains(point))
        {
            if (_snapshot.You is { } me &&
                !trade.Request.All(item => me.Resource(item.Key) >= item.Value))
            {
                ShowMessage("Недостаточно ресурсов для принятия обмена.");
                return true;
            }
            TradeDecisionRequested?.Invoke(trade.SourcePlayerId, trade.TradeId, true);
        }
        else if (declineButton.Contains(point))
        {
            TradeDecisionRequested?.Invoke(trade.SourcePlayerId, trade.TradeId, false);
        }

        return true;
    }

    private bool HandleTradeClick(Point point)
    {
        var panel = TradePanelBounds;
        if (!panel.Contains(point)) return false;

        if (HasPendingTrade)
        {
            ShowMessage("Дождитесь ответа игрока на текущее предложение обмена.");
            return true;
        }

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
                y += 24; // заголовок "С кем обменяться"
                foreach (var player in _snapshot!.Players.Where(player => player.Id != _snapshot.YouPlayerId))
                {
                    if (new Rectangle(panel.X + 12, y, panel.Width - 24, 34).Contains(point))
                    {
                        if (_tradeTargetId != player.Id)
                        {
                            _tradeTargetId = player.Id;
                            _tradeRequest.Clear();
                        }
                        return true;
                    }
                    y += 38;
                }
                y += 44; // заголовки колонок Отдаю / Прошу

                var targetPlayer = _snapshot.Players.FirstOrDefault(p => p.Id == _tradeTargetId);
                foreach (ResourceType resource in Enum.GetValues<ResourceType>())
                {
                    // Колонка "Отдаю": [-], [+]
                    if (new Rectangle(panel.Right - 226, y, 26, 32).Contains(point))
                    {
                        AdjustTradeAmount(_tradeOffer, resource, -1, _snapshot.You?.Resource(resource) ?? 0);
                        return true;
                    }
                    if (new Rectangle(panel.Right - 166, y, 26, 32).Contains(point))
                    {
                        if (_tradeRequest.TryGetValue(resource, out int req) && req > 0)
                            ShowMessage("Нельзя отдавать ресурс, который вы просите в обмен.");
                        else
                            AdjustTradeAmount(_tradeOffer, resource, +1, _snapshot.You?.Resource(resource) ?? 0);
                        return true;
                    }

                    // Колонка "Прошу": [-], [+]
                    int maxRequest = targetPlayer?.Resource(resource) ?? 99;
                    if (new Rectangle(panel.Right - 104, y, 26, 32).Contains(point))
                    {
                        AdjustTradeAmount(_tradeRequest, resource, -1, maxRequest);
                        return true;
                    }
                    if (new Rectangle(panel.Right - 44, y, 26, 32).Contains(point))
                    {
                        if (_tradeOffer.TryGetValue(resource, out int give) && give > 0)
                            ShowMessage("Нельзя просить ресурс, который вы отдаёте в обмен.");
                        else
                            AdjustTradeAmount(_tradeRequest, resource, +1, maxRequest);
                        return true;
                    }

                    y += 42;
                }
            }
            else
            {
                y += 46; // заголовок и подсказка обмена с банком
                foreach (ResourceType resource in Enum.GetValues<ResourceType>())
                {
                    if (new Rectangle(panel.Right - 92, y, 28, 32).Contains(point))
                    {
                        AdjustTradeAmount(_tradeOffer, resource, +1, _snapshot!.You?.Resource(resource) ?? 0);
                        return true;
                    }
                    if (new Rectangle(panel.Right - 128, y, 28, 32).Contains(point))
                    {
                        AdjustTradeAmount(_tradeOffer, resource, -1, _snapshot!.You?.Resource(resource) ?? 0);
                        return true;
                    }
                    if (new Rectangle(panel.X + 12, y, panel.Width - 160, 32).Contains(point))
                        _bankResourceToGet = resource;
                    y += 42;
                }
            }

            var submit = new Rectangle(panel.X + 12, panel.Bottom - 44, panel.Width - 24, 34);
            if (submit.Contains(point)) SubmitTrade();
        }

        return true;
    }

    private static void AdjustTradeAmount(Dictionary<ResourceType, int> dict, ResourceType resource, int delta, int max)
    {
        dict.TryGetValue(resource, out int current);
        int next = Math.Clamp(current + delta, 0, Math.Max(0, max));
        if (next == 0) dict.Remove(resource);
        else dict[resource] = next;
    }

    private void SubmitTrade()
    {
        var offer = new Dictionary<ResourceType, int>(_tradeOffer);
        var request = new Dictionary<ResourceType, int>(_tradeRequest);

        if (_tradeMode == TradeMode.Player)
        {
            if (_tradeTargetId is not Guid targetId)
            {
                ShowMessage("Выберите игрока для обмена.");
                return;
            }
            if (offer.Count == 0 || request.Count == 0)
            {
                ShowMessage("Выберите, что отдаёте и что просите в обмен.");
                return;
            }
            if (offer.Keys.Any(request.ContainsKey))
            {
                ShowMessage("Нельзя отдавать и просить один и тот же ресурс.");
                return;
            }

            TradeWithPlayerRequested?.Invoke(targetId, offer, request);
        }
        else
        {
            if (offer.Count == 0)
            {
                ShowMessage("Сначала выберите ресурсы для обмена.");
                return;
            }
            if (offer.Count != 3)
            {
                ShowMessage("По правилам сервера для обмена с банком нужны 3 разных вида ресурсов.");
                return;
            }

            TradeWithBankRequested?.Invoke(offer, _bankResourceToGet);
        }

        _tradeOffer.Clear();
        _tradeRequest.Clear();
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
        if (_snapshot?.GameCode != snapshot.GameCode)
        {
            _collectedTribeNodes.Clear();
            _tribeCounterPlayerCount = null;
        }

        _snapshot = snapshot;
        if (snapshot.Status is GameStatus.Setup or GameStatus.Playing &&
            _tribeCounterPlayerCount != snapshot.Players.Count)
        {
            _tribeCounterPlayerCount = snapshot.Players.Count;
            UpdateTribeCountersForPlayerCount(snapshot.Players.Count);
        }

        if (IsGameOver)
        {
            _boardActionMode = BoardActionMode.None;
            _selectedExplorer = null;
            _selectedRoute.Clear();
            _tradeOffer.Clear();
            _discardSelection.Clear();
        }
        if (!HasPendingDiscard)
            _discardSelection.Clear();
    }

    public void MarkTribeCollected(AxialCoords nodeCoords)
    {
        if (!_collectedTribeNodes.Add(nodeCoords))
            return;

        if (_board.Nodes.TryGetValue(nodeCoords, out var node) && node.HasTribeCounter)
        {
            node.HasTribeCounter = false;
            RebuildBoardMesh();
        }
    }

    private void UpdateTribeCountersForPlayerCount(int playerCount)
    {
        bool changed = false;
        foreach (var node in _board.Nodes.Values)
        {
            bool hasCounter = !_collectedTribeNodes.Contains(node.Coords) &&
                              (playerCount == 3 ? node.CanSettle3p : node.CanSettle);
            if (node.HasTribeCounter == hasCounter)
                continue;

            node.HasTribeCounter = hasCounter;
            changed = true;
        }

        if (changed)
            RebuildBoardMesh();
    }

    private void RebuildBoardMesh()
    {
        var oldBoardMesh = _boardMesh;
        _boardMesh = BuildBoardMesh();
        oldBoardMesh?.Dispose();
    }

    public void ShowMessage(string message)
    {
        _message = message;
        _messageSeconds = 8f;
    }

    private void DrawPieces()
    {
        if (_snapshot is null) return;

        for (int i = 1; i < _selectedRoute.Count; i++)
        {
            var from = HexLayout.ToWorld(_selectedRoute[i - 1]);
            var to = HexLayout.ToWorld(_selectedRoute[i]);
            DrawLine(from, to, 0.12f, new Color(255, 244, 100) * 0.9f);
        }

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

    private void DrawRing(Vector2 center, float diameter, Color color, float opacity)
    {
        _spriteBatch.Draw(
            _ring,
            center,
            sourceRectangle: null,
            color: color * opacity,
            rotation: 0f,
            origin: new Vector2(_ring.Width / 2f, _ring.Height / 2f),
            scale: new Vector2(diameter / _ring.Width),
            effects: SpriteEffects.None,
            layerDepth: 0f);
    }

    private void DrawLine(Vector2 from, Vector2 to, float thickness, Color color)
    {
        Vector2 delta = to - from;
        _spriteBatch.Draw(
            _pixel,
            from,
            sourceRectangle: null,
            color: color,
            rotation: MathF.Atan2(delta.Y, delta.X),
            origin: Vector2.Zero,
            scale: new Vector2(delta.Length(), thickness),
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

        DrawOwnResources(headerPanel);

        DrawPlayersPanel();
        if (_snapshot.Status == GameStatus.Setup)
            DrawSetupInstructions();
        else
        {
            DrawActionButtons();
            if (HasPendingDiscards)
            {
                if (HasPendingDiscard) DrawDiscardPanel();
                else DrawPendingWaitPanel();
            }
            if (CanBuild) DrawBoardActionButtons();
            if (CanBuild) DrawProgressPanel();
            if (CanTrade) DrawTradePanel();
            if (HasIncomingTradeForMe) DrawIncomingTradePanel();
        }
        DrawMessage();
        if (IsGameOver)
            DrawVictoryOverlay();

        _spriteBatch.End();
    }

    private Rectangle VictoryMainMenuButtonBounds
    {
        get
        {
            var viewport = _graphicsDevice.Viewport;
            int width = Math.Min(560, Math.Max(280, viewport.Width - 32));
            const int height = 240;
            var panel = new Rectangle(
                (viewport.Width - width) / 2,
                (viewport.Height - height) / 2,
                width,
                height);
            return new Rectangle(panel.X + 24, panel.Bottom - 48, panel.Width - 48, 36);
        }
    }

    private void DrawVictoryOverlay()
    {
        var viewport = _graphicsDevice.Viewport;
        _spriteBatch.Draw(_pixel, new Rectangle(0, 0, viewport.Width, viewport.Height), Color.Black * 0.68f);

        int width = Math.Min(560, Math.Max(280, viewport.Width - 32));
        const int height = 240;
        var panel = new Rectangle(
            (viewport.Width - width) / 2,
            (viewport.Height - height) / 2,
            width,
            height);
        DrawPanel(panel);

        string winnerName = _snapshot?.Players
            .FirstOrDefault(player => player.Id == _snapshot.WinnerPlayerId)?.Name
            ?? _snapshot?.WinnerPlayerId?.ToString("N") ?? "Неизвестный игрок";

        DrawCenteredText("ИГРА ОКОНЧЕНА", _uiTitleFont, panel, panel.Y + 32, new Color(242, 228, 202));
        DrawCenteredText($"Победитель: {winnerName}", _uiFont, panel, panel.Y + 84, new Color(240, 196, 112));
        DrawCenteredText("Партия успешно завершена!", _uiFont, panel, panel.Y + 120, new Color(206, 194, 172));

        var button = VictoryMainMenuButtonBounds;
        DrawButton(button, "В главное меню", true, button.Contains(Mouse.GetState().Position));
    }

    private void DrawCenteredText(string text, SpriteFontBase font, Rectangle panel, int y, Color color)
    {
        Vector2 size = font.MeasureString(text);
        _spriteBatch.DrawString(
            font,
            text,
            new Vector2(panel.X + (panel.Width - size.X) / 2f, y),
            color);
    }

    private void DrawOwnResources(Rectangle headerPanel)
    {
        if (_snapshot?.You is not { } player) return;

        (ResourceType Type, string Label)[] resources =
        {
            (ResourceType.Meat, "Мясо"),
            (ResourceType.Bone, "Кость"),
            (ResourceType.Flint, "Кремень"),
            (ResourceType.Hides, "Шкуры"),
        };

        const int gapX = 8;
        const int gapY = 2;
        const int horizontalPadding = 12;
        int cellWidth = (headerPanel.Width - horizontalPadding * 2 - gapX) / 2;
        int cellHeight = 24;
        int startY = headerPanel.Y + 102;

        for (int i = 0; i < resources.Length; i++)
        {
            int column = i % 2;
            int row = i / 2;
            var bounds = new Rectangle(
                headerPanel.X + horizontalPadding + column * (cellWidth + gapX),
                startY + row * (cellHeight + gapY),
                cellWidth,
                cellHeight);
            _spriteBatch.Draw(_pixel, bounds, new Color(24, 23, 20) * 0.92f);
            _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 1), new Color(142, 119, 82) * 0.8f);
            _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - 1, bounds.Width, 1), new Color(142, 119, 82) * 0.8f);

            string text = $"{resources[i].Label} {player.Resource(resources[i].Type)}";
            _spriteBatch.DrawString(
                _uiFont,
                text,
                new Vector2(bounds.X + 8, bounds.Y),
                new Color(242, 228, 202));
        }
    }

    private void DrawMessage()
    {
        if (_message is null) return;

        const int panelWidth = 380;
        var lines = WrapText(_message, panelWidth - 32f);
        const int lineHeight = 26;
        var playersPanel = PlayersPanelBounds(_snapshot?.Players.Count ?? 0);
        int x = _graphicsDevice.Viewport.Width - panelWidth - 16;
        if (CanTrade)
            x = Math.Min(x, TradePanelBounds.X - panelWidth - 12);
        else if (CanBuild)
            x = Math.Min(x, ProgressPanelBounds.X - panelWidth - 12);

        var panel = new Rectangle(
            Math.Max(16, x),
            playersPanel.Bottom + 10,
            panelWidth,
            14 + lines.Count * lineHeight);
        DrawPanel(panel);

        float y = panel.Y + 7;
        foreach (var line in lines)
        {
            _spriteBatch.DrawString(
                _uiFont,
                line,
                new Vector2(panel.X + 16, y),
                new Color(232, 140, 120));
            y += lineHeight;
        }
    }

    private void DrawSetupInstructions()
    {
        var you = _snapshot?.You;
        var bounds = new Rectangle(16, _graphicsDevice.Viewport.Height - 94 - 16, 430, 94);
        DrawPanel(bounds);

        var text = CanPlaceInitialCamp
            ? $"Расстановка лагерей\nВыберите стартовый узел: {you?.Camps.Count ?? 0}/3 поставлено."
            : $"Расстановка лагерей\nОжидайте ход игрока { _snapshot?.CurrentPlayer?.Name ?? "…" }.";
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
        const int cardW = 280;
        const int cardH = 72;
        const int gapX = 10;
        const int gapY = 8;

        var players = _snapshot!.Players;
        var panel = PlayersPanelBounds(players.Count);
        DrawPanel(panel);

        int maxCols = _graphicsDevice.Viewport.Width >= 1380 ? 3 : 2;
        int cols = Math.Min(maxCols, Math.Max(1, players.Count));

        for (int i = 0; i < players.Count; i++)
        {
            var player = players[i];
            int col = i % cols;
            int row = i / cols;
            int x = panel.X + 8 + col * (cardW + gapX);
            int y = panel.Y + 8 + row * (cardH + gapY);

            var cardBounds = new Rectangle(x, y, cardW, cardH);
            _spriteBatch.Draw(_pixel, cardBounds, new Color(28, 26, 23) * 0.7f);

            Color color = PlayerColorToXna(player.Color);
            string name = player.Id == _snapshot.CurrentPlayerId
                ? $"{player.Name} ★"
                : player.Name;
            _spriteBatch.Draw(_pixel, new Rectangle(x + 8, y + 7, 12, 12), color);
            _spriteBatch.DrawString(
                _uiFont,
                name,
                new Vector2(x + 26, y + 2),
                new Color(238, 228, 208));

            string resourcesLine =
                $"Мясо {player.Resource(ResourceType.Meat)} · " +
                $"Кость {player.Resource(ResourceType.Bone)} · " +
                $"Кремень {player.Resource(ResourceType.Flint)} · " +
                $"Шкуры {player.Resource(ResourceType.Hides)}";
            _spriteBatch.DrawString(
                _uiFont,
                resourcesLine,
                new Vector2(x + 8, y + 26),
                new Color(206, 194, 172),
                scale: new Vector2(0.80f));

            string unitsLine =
                $"Лагеря {player.Camps.Count} · Иссл. {player.Explorers.Count} · Племена {player.TribesCollected}";
            _spriteBatch.DrawString(
                _uiFont,
                unitsLine,
                new Vector2(x + 8, y + 47),
                new Color(176, 166, 148),
                scale: new Vector2(0.82f));
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

    private void DrawDiscardPanel()
    {
        if (_snapshot?.You is not { } player || player.DiscardResourcesRequired is not int required)
            return;

        var panel = DiscardPanelBounds;
        DrawPanel(panel);
        _spriteBatch.DrawString(
            _uiTitleFont,
            $"Сброс ресурсов · осталось выбрать {required}",
            new Vector2(panel.X + 12, panel.Y + 6),
            new Color(242, 228, 202));

        int selected = _discardSelection.Values.Sum();
        string instruction = $"Выбрано {selected}/{required}";
        _spriteBatch.DrawString(_uiFont, instruction, new Vector2(panel.X + 12, panel.Y + 30), new Color(206, 194, 172));

        var mouse = Mouse.GetState();
        ResourceType[] resources = Enum.GetValues<ResourceType>();
        for (int i = 0; i < resources.Length; i++)
        {
            var resource = resources[i];
            var row = DiscardRowBounds(i);
            _discardSelection.TryGetValue(resource, out int amount);
            int controlX = row.Right - 212;
            string label = $"{ResourceName(resource)}: {amount}/{player.Resource(resource)}";
            _spriteBatch.DrawString(_uiFont, label, new Vector2(row.X + 4, row.Y + 4), new Color(238, 228, 208));
            bool canEdit = !Busy;
            DrawButton(new Rectangle(controlX, row.Y, 42, row.Height), "−10", canEdit && amount > 0, new Rectangle(controlX, row.Y, 42, row.Height).Contains(mouse.Position));
            DrawButton(new Rectangle(controlX + 46, row.Y, 38, row.Height), "−", canEdit && amount > 0, new Rectangle(controlX + 46, row.Y, 38, row.Height).Contains(mouse.Position));
            DrawButton(new Rectangle(controlX + 88, row.Y, 38, row.Height), "+", canEdit && amount < player.Resource(resource), new Rectangle(controlX + 88, row.Y, 38, row.Height).Contains(mouse.Position));
            DrawButton(new Rectangle(controlX + 130, row.Y, 42, row.Height), "+10", canEdit && amount < player.Resource(resource), new Rectangle(controlX + 130, row.Y, 42, row.Height).Contains(mouse.Position));
        }

        var submit = new Rectangle(panel.X + 12, panel.Bottom - 42, panel.Width - 24, 32);
        DrawButton(submit, "Сбросить выбранное", CanSubmitDiscard, submit.Contains(mouse.Position));
    }

    private void DrawPendingWaitPanel()
    {
        if (_snapshot is null) return;

        var panel = DiscardPanelBounds;
        DrawPanel(panel);
        _spriteBatch.DrawString(
            _uiTitleFont,
            "Ожидание сброса ресурсов",
            new Vector2(panel.X + 12, panel.Y + 8),
            new Color(242, 228, 202));

        var pendingPlayers = _snapshot.Players
            .Where(player => player.DiscardResourcesRequired is > 0)
            .Select(player => $"{player.Name}: сбросить {player.DiscardResourcesRequired}")
            .ToArray();
        string message = string.Join(" · ", pendingPlayers);
        _spriteBatch.DrawString(_uiFont, message, new Vector2(panel.X + 12, panel.Y + 48), new Color(206, 194, 172));
        _spriteBatch.DrawString(
            _uiFont,
            "Ожидайте, пока игроки с более чем 7 картами сбросят половину ресурсов.",
            new Vector2(panel.X + 12, panel.Y + 82),
            new Color(240, 196, 112));
    }

    private void DrawBoardActionButtons()
    {
        var bounds = BoardActionButtonsBounds;
        var mouse = Mouse.GetState();
        var actions = new[]
        {
            (BoardActionMode.CreateExplorer, "Исследователь"),
            (BoardActionMode.MoveExplorer, "Переместить"),
            (BoardActionMode.ConvertToCamp, "Лагерь"),
        };

        for (int i = 0; i < actions.Length; i++)
        {
            var button = new Rectangle(bounds.X + i * 170, bounds.Y, 160, bounds.Height);
            bool active = _boardActionMode == actions[i].Item1;
            string label = actions[i].Item1 == BoardActionMode.MoveExplorer && active && _selectedRoute.Count > 1
                ? $"Переместить ({_selectedRoute.Count - 1})"
                : actions[i].Item2;
            DrawButton(button, label, true, active || button.Contains(mouse.Position));
        }
    }

    private void DrawIncomingTradePanel()
    {
        if (_snapshot?.PendingTrade is not { } trade) return;

        var panel = IncomingTradePanelBounds;
        DrawPanel(panel);
        var mouse = Mouse.GetState();

        string sourceName = _snapshot.Players
            .FirstOrDefault(p => p.Id == trade.SourcePlayerId)?.Name ?? "Игрок";

        _spriteBatch.DrawString(_uiTitleFont, "Предложение обмена", new Vector2(panel.X + 14, panel.Y + 8), new Color(242, 228, 202));
        _spriteBatch.DrawString(_uiFont, $"От: {sourceName}", new Vector2(panel.X + 14, panel.Y + 42), new Color(240, 196, 112));
        _spriteBatch.DrawString(_uiFont, $"Отдаёт вам: {FormatResourceDict(trade.Offer)}", new Vector2(panel.X + 14, panel.Y + 72), new Color(180, 232, 168));
        _spriteBatch.DrawString(_uiFont, $"Просит у вас: {FormatResourceDict(trade.Request)}", new Vector2(panel.X + 14, panel.Y + 102), new Color(238, 182, 148));

        bool canAfford = _snapshot.You is { } me &&
                         trade.Request.All(item => me.Resource(item.Key) >= item.Value);
        if (!canAfford)
        {
            _spriteBatch.DrawString(_uiFont, "Недостаточно ресурсов для принятия.", new Vector2(panel.X + 14, panel.Y + 132), new Color(232, 140, 120));
        }

        int buttonWidth = (panel.Width - 36) / 2;
        var acceptButton = new Rectangle(panel.X + 12, panel.Bottom - 46, buttonWidth, 34);
        var declineButton = new Rectangle(acceptButton.Right + 12, panel.Bottom - 46, buttonWidth, 34);
        DrawButton(acceptButton, "Принять", canAfford, canAfford && acceptButton.Contains(mouse.Position));
        DrawButton(declineButton, "Отклонить", true, declineButton.Contains(mouse.Position));
    }

    private static string ResourceName(ResourceType resource) => resource switch
    {
        ResourceType.Meat => "Мясо",
        ResourceType.Bone => "Кость",
        ResourceType.Flint => "Кремень",
        ResourceType.Hides => "Шкуры",
        _ => resource.ToString(),
    };

    private static string FormatResourceDict(Dictionary<ResourceType, int> resources)
    {
        var nonZero = resources.Where(kv => kv.Value > 0).Select(kv => $"{ResourceName(kv.Key)}: {kv.Value}").ToArray();
        return nonZero.Length == 0 ? "ничего" : string.Join(", ", nonZero);
    }

    private void DrawTradePanel()
    {
        var panel = TradePanelBounds;
        DrawPanel(panel);
        var mouse = Mouse.GetState();
        _spriteBatch.DrawString(_uiTitleFont, "Торговля", new Vector2(panel.X + 12, panel.Y + 6), new Color(242, 228, 202));

        var playerMode = new Rectangle(panel.X + 12, panel.Y + 34, 150, 32);
        var bankMode = new Rectangle(panel.X + 174, panel.Y + 34, 160, 32);
        DrawButton(playerMode, "С игроком", true, _tradeMode == TradeMode.Player);
        DrawButton(bankMode, "С банком", true, _tradeMode == TradeMode.Bank);

        int y = panel.Y + 78;
        if (_tradeMode == TradeMode.Player)
        {
            _spriteBatch.DrawString(_uiFont, "С кем обменяться:", new Vector2(panel.X + 12, y), new Color(206, 194, 172));
            y += 24;
            foreach (var player in _snapshot!.Players.Where(player => player.Id != _snapshot.YouPlayerId))
            {
                var target = new Rectangle(panel.X + 12, y, panel.Width - 24, 34);
                bool selected = _tradeTargetId == player.Id;
                DrawButton(target, player.Name, true, selected || target.Contains(mouse.Position));
                y += 38;
            }

            if (_snapshot.PendingTrade is { } pending)
            {
                string targetName = _snapshot.Players.FirstOrDefault(p => p.Id == pending.TargetPlayerId)?.Name ?? "игрока";
                _spriteBatch.DrawString(_uiFont, $"Ожидание ответа от {targetName}...", new Vector2(panel.X + 12, y + 8), new Color(240, 196, 112));
                return;
            }

            _spriteBatch.DrawString(_uiFont, "Ресурс", new Vector2(panel.X + 12, y + 6), new Color(176, 166, 148));
            _spriteBatch.DrawString(_uiFont, "Отдаю", new Vector2(panel.Right - 216, y + 6), new Color(238, 182, 148));
            _spriteBatch.DrawString(_uiFont, "Прошу", new Vector2(panel.Right - 94, y + 6), new Color(180, 232, 168));
            y += 44;

            var targetPlayer = _snapshot.Players.FirstOrDefault(p => p.Id == _tradeTargetId);
            foreach (ResourceType resource in Enum.GetValues<ResourceType>())
            {
                int available = _snapshot.You?.Resource(resource) ?? 0;
                int targetAvailable = targetPlayer?.Resource(resource) ?? 0;
                string rowLabel = targetPlayer is not null
                    ? $"{ResourceName(resource)} ({available} | {targetAvailable})"
                    : $"{ResourceName(resource)} ({available})";
                _spriteBatch.DrawString(_uiFont, rowLabel, new Vector2(panel.X + 12, y + 5), new Color(238, 228, 208));

                // Отдаю
                _tradeOffer.TryGetValue(resource, out int giveAmount);
                _tradeRequest.TryGetValue(resource, out int reqAmount);
                bool canGivePlus = giveAmount < available && reqAmount == 0;
                var giveMinus = new Rectangle(panel.Right - 226, y, 26, 32);
                var givePlus = new Rectangle(panel.Right - 166, y, 26, 32);
                DrawButton(giveMinus, "−", giveAmount > 0, giveMinus.Contains(mouse.Position));
                _spriteBatch.DrawString(_uiFont, giveAmount.ToString(), new Vector2(panel.Right - 194, y + 5), new Color(242, 228, 202));
                DrawButton(givePlus, "+", canGivePlus, canGivePlus && givePlus.Contains(mouse.Position));

                // Прошу
                bool canReqPlus = targetPlayer is not null && reqAmount < targetAvailable && giveAmount == 0;
                var reqMinus = new Rectangle(panel.Right - 104, y, 26, 32);
                var reqPlus = new Rectangle(panel.Right - 44, y, 26, 32);
                DrawButton(reqMinus, "−", reqAmount > 0, reqMinus.Contains(mouse.Position));
                _spriteBatch.DrawString(_uiFont, reqAmount.ToString(), new Vector2(panel.Right - 72, y + 5), new Color(242, 228, 202));
                DrawButton(reqPlus, "+", canReqPlus, canReqPlus && reqPlus.Contains(mouse.Position));

                y += 42;
            }
        }
        else
        {
            _spriteBatch.DrawString(_uiFont, $"Получить: {ResourceName(_bankResourceToGet)}", new Vector2(panel.X + 12, y), new Color(206, 194, 172));
            _spriteBatch.DrawString(_uiFont, "Нажмите на ресурс, который хотите получить.", new Vector2(panel.X + 12, y + 22), new Color(176, 166, 148));
            y += 46;

            foreach (ResourceType resource in Enum.GetValues<ResourceType>())
            {
                var minus = new Rectangle(panel.Right - 128, y, 28, 32);
                var plus = new Rectangle(panel.Right - 92, y, 28, 32);
                DrawButton(minus, "−", true, minus.Contains(mouse.Position));
                DrawButton(plus, "+", true, plus.Contains(mouse.Position));

                _tradeOffer.TryGetValue(resource, out int amount);
                int available = _snapshot!.You?.Resource(resource) ?? 0;
                string label = _bankResourceToGet == resource
                    ? $"▶ {ResourceName(resource)}: {amount}/{available}"
                    : $"{ResourceName(resource)}: {amount}/{available}";
                _spriteBatch.DrawString(_uiFont, label, new Vector2(panel.X + 12, y + 5), new Color(238, 228, 208));
                y += 42;
            }
        }

        var submit = new Rectangle(panel.X + 12, panel.Bottom - 44, panel.Width - 24, 34);
        DrawButton(submit, _tradeMode == TradeMode.Bank ? "Обменять" : "Предложить обмен", true, submit.Contains(mouse.Position));
    }

    private void DrawProgressPanel()
    {
        if (_snapshot?.You is not { } player) return;

        var panel = ProgressPanelBounds;
        DrawPanel(panel);
        _spriteBatch.DrawString(
            _uiTitleFont,
            "Развитие",
            new Vector2(panel.X + 12, panel.Y + 6),
            new Color(242, 228, 202));

        for (int i = 0; i < ProgressTypes.Length; i++)
        {
            var progressType = ProgressTypes[i];
            int level = player.ProgressLevel(progressType);
            bool canAdvance = level < 5 && HasResources(
                player,
                ProgressCost(level + 1).Select(item => (item.Key, item.Value)).ToArray());
            var button = ProgressButtonBounds(i);
            string buttonText = level >= 5
                ? $"{ProgressName(progressType)} MAX"
                : $"{ProgressName(progressType)}: {level} -> {level + 1}";
            DrawButton(button, buttonText, canAdvance, button.Contains(Mouse.GetState().Position));

            var cost = ProgressCost(level + 1);
            string costText = level >= 5 ? "Макс. уровень" : $"Цена: {ProgressCostLabel(cost)}";
            _spriteBatch.DrawString(
                _uiFont,
                costText,
                new Vector2(panel.X + 180, button.Y + 8),
                canAdvance ? new Color(206, 194, 172) : new Color(144, 132, 116),
                scale: new Vector2(0.92f));
        }
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
        var shadowBounds = new Rectangle(bounds.X + 2, bounds.Y + 2, bounds.Width, bounds.Height);
        _spriteBatch.Draw(_pixel, shadowBounds, Color.Black * 0.45f);

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
        float maxWidth = Math.Max(20f, bounds.Width - 12f);
        float scale = size.X > maxWidth ? maxWidth / size.X : 1f;
        var textPos = new Vector2(
            bounds.X + (bounds.Width - size.X * scale) / 2f,
            bounds.Y + (bounds.Height - size.Y * scale) / 2f);
        _spriteBatch.DrawString(_uiFont, text, textPos + new Vector2(1.5f, 1.5f), Color.Black * 0.65f, scale: new Vector2(scale));
        _spriteBatch.DrawString(
            _uiFont,
            text,
            textPos,
            enabled ? new Color(246, 236, 214) : new Color(132, 124, 112),
            scale: new Vector2(scale));
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
        var shadowBounds = new Rectangle(bounds.X + 4, bounds.Y + 4, bounds.Width, bounds.Height);
        _spriteBatch.Draw(_pixel, shadowBounds, Color.Black * 0.45f);
        _spriteBatch.Draw(_pixel, bounds, new Color(20, 18, 16) * 0.86f);

        var border = new Color(146, 122, 84) * 0.92f;
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, 2), border);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - 2, bounds.Width, 2), border);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, 2, bounds.Height), border);
        _spriteBatch.Draw(_pixel, new Rectangle(bounds.Right - 2, bounds.Y, 2, bounds.Height), border);
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
                // SpriteBatch's default AlphaBlend expects premultiplied alpha.
                pixels[y * size + x] = new Color(alpha, alpha, alpha, alpha);
            }
        }

        texture.SetData(pixels);
        return texture;
    }

    private Texture2D CreateRingTexture(int size)
    {
        var texture = new Texture2D(_graphicsDevice, size, size);
        var pixels = new Color[size * size];
        float outerRadius = size / 2f - 2f;
        float innerRadius = outerRadius - size * 0.085f;
        var center = new Vector2(size / 2f, size / 2f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float edgeDistance = MathF.Min(outerRadius - distance, distance - innerRadius);
                float alpha = MathHelper.Clamp(edgeDistance + 1f, 0f, 1f);
                pixels[y * size + x] = new Color(alpha, alpha, alpha, alpha);
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

        const float mapScale = 0.80f;
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
