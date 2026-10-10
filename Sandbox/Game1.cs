using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.IO;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace Sandbox;

public class Game1 : Game
{
    private enum Screen { MainMenu, Connect, Board }
    private Screen _screen = Screen.MainMenu;
    private readonly GraphicsDeviceManager _graphics;
    private BasicEffect _effect;
    private SpriteBatch _spriteBatch;
    private SpriteFont _numberFont;
    private Texture2D _pixel;
    private BoardState _board;
    private Mesh _boardMesh;
    private Vector2 _boardMin, _boardMax;
    private readonly ServerConnection _server = new();
    private Task _request = Task.CompletedTask;
    private MouseState _previousMouse;
    private KeyboardState _previousKeyboard;
    private string _joinCode = "", _gameCode = "", _status = "";
    private int _players;
    private const int ToolbarHeight = 64;
    private bool Busy => !_request.IsCompleted;
    private Rectangle MenuItem(int row) => new(GraphicsDevice.Viewport.Width / 2 - 150, 180 + row * 64, 300, 48);
    private Rectangle MenuButton => new(12, 12, 150, 36);

    public Game1()
    {
        _graphics = new GraphicsDeviceManager(this);
        Content.RootDirectory = "Content";
        IsMouseVisible = true;
        Window.AllowUserResizing = true;
        _graphics.PreferredBackBufferWidth = 1100;
        _graphics.PreferredBackBufferHeight = 760;
        _graphics.GraphicsProfile = GraphicsProfile.HiDef;
        _graphics.PreferMultiSampling = true;
        _graphics.PreparingDeviceSettings += (_, e) =>
            e.GraphicsDeviceInformation.PresentationParameters.MultiSampleCount = 8;
        Window.TextInput += (_, e) =>
        {
            if (_screen != Screen.Connect || Busy) return;
            if (e.Character == '\b')
            { if (_joinCode.Length > 0) _joinCode = _joinCode[..^1]; }
            else if (char.IsAsciiLetterOrDigit(e.Character) && _joinCode.Length < 6)
                _joinCode += char.ToUpperInvariant(e.Character);
        };
    }

    protected override void LoadContent()
    {
        _effect = new BasicEffect(GraphicsDevice) { VertexColorEnabled = true };
        _spriteBatch = new SpriteBatch(GraphicsDevice);
        _numberFont = Content.Load<SpriteFont>("Fonts/NumberToken");
        _pixel = new Texture2D(GraphicsDevice, 1, 1);
        _pixel.SetData(new[] { Color.White });
        // No map is loaded until Create game is clicked.
    }

    protected override void UnloadContent()
    {
        _server.Dispose();
        _boardMesh?.Dispose();
        _pixel?.Dispose();
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
        while (_server.Messages.TryDequeue(out var message))
        {
            if (!message.StartsWith("{"))
            {
                if (message == "Disconnected")
                {
                    _players = 0;
                    if (_screen == Screen.Board)
                    {
                        _boardMesh?.Dispose(); _boardMesh = null; _board = null;
                        _screen = Screen.MainMenu;
                    }
                }
                _status = message;
                continue;
            }
            try
            {
                Console.WriteLine(message);
                using var doc = JsonDocument.Parse(message);
                var root = doc.RootElement;
                switch (root.GetProperty("Type").GetString())
                {
                    case "gameCode": _gameCode = root.GetProperty("Code").GetString(); break;
                    case "playerCount": _players = root.GetProperty("Count").GetInt32(); break;
                    case "gameState":
                        if (!_server.IsConnected) throw new IOException("Connection closed before the board arrived");
                        var snapshot = root.GetProperty("State").Deserialize<GameStateSnapshot>();
                        var state = snapshot.ToGameState();
                        var board = state.Board;
                        _boardMesh?.Dispose();
                        _board = board;
                        _boardMesh = BuildBoardMesh();
                        _gameCode = root.GetProperty("Code").GetString();
                        _screen = Screen.Board;
                        _status = "State received";
                        break;
                }
            }
            catch (Exception ex) { _status = "State error: " + ex.Message; }
        }
        var keyboard = Keyboard.GetState();
        if (keyboard.IsKeyDown(Keys.Escape) && !_previousKeyboard.IsKeyDown(Keys.Escape) && !Busy)
        {
            if (_screen == Screen.MainMenu) Exit();
            else ReturnToMenu();
        }
        if (_screen == Screen.Connect && keyboard.IsKeyDown(Keys.Enter) && !_previousKeyboard.IsKeyDown(Keys.Enter) && !Busy)
            Join();
        _previousKeyboard = keyboard;
        var mouse = Mouse.GetState();
        if (IsActive && mouse.LeftButton == ButtonState.Pressed && _previousMouse.LeftButton == ButtonState.Released && !Busy)
        {
            var point = new Point(mouse.X, mouse.Y);
            if (_screen == Screen.MainMenu)
            {
                if (MenuItem(0).Contains(point))
                {
                    try
                    {
                        StartRequest(_server.CreateGameAsync);
                    }
                    catch (Exception ex) { _status = "Map load failed: " + ex.Message; }
                }
                else if (MenuItem(1).Contains(point)) { _screen = Screen.Connect; _status = "Enter the game code"; }
                else if (MenuItem(2).Contains(point)) Exit();
            }
            else if (_screen == Screen.Connect)
            {
                if (MenuItem(1).Contains(point)) Join();
                else if (MenuItem(2).Contains(point)) ReturnToMenu();
            }
            else if (MenuButton.Contains(point)) ReturnToMenu();
        }
        _previousMouse = mouse;
        base.Update(gameTime);
    }

    private void Join()
    {
        if (_joinCode.Length != 6) { _status = "Enter a 6-character code"; return; }
        string code = _joinCode;
        StartRequest(() => _server.JoinGameAsync(code));
    }

    private void ReturnToMenu()
    {
        _screen = Screen.MainMenu;
        _boardMesh?.Dispose(); _boardMesh = null; _board = null;
        _gameCode = ""; _players = 0;
        StartRequest(() => _server.DisconnectAsync());
    }

    private void StartRequest(Func<Task> operation)
    {
        _status = "Please wait...";
        _request = ExecuteRequest(operation);
    }
    private async Task ExecuteRequest(Func<Task> operation)
    {
        try { await operation(); }
        catch (Exception ex) { _server.Messages.Enqueue("Request failed: " + ex.Message); }
    }

    protected override void Draw(GameTime gameTime)
    {
        GraphicsDevice.Clear(_screen == Screen.Board ? new Color(30, 110, 190) : new Color(20, 25, 35));
        if (_screen == Screen.Board && _boardMesh != null)
        {
            var viewport = GraphicsDevice.Viewport;
            _effect.Projection = Matrix.CreateOrthographicOffCenter(0, viewport.Width, viewport.Height, 0, 0, 1);
            var world = FitToScreen(viewport.Width, viewport.Height, 20f);
            _effect.World = world;
            GraphicsDevice.RasterizerState = RasterizerState.CullNone;
            _boardMesh.Draw(GraphicsDevice, _effect);
            _spriteBatch.Begin(transformMatrix: world, samplerState: SamplerState.LinearClamp);
            foreach (var hex in _board.Hexes.Values) DrawNumberToken(hex);
            _spriteBatch.End();
        }
        _spriteBatch.Begin();
        if (_screen == Screen.MainMenu)
        {
            UiText("BOARD GAME", new Vector2(GraphicsDevice.Viewport.Width / 2 - 90, 100));
            Button(MenuItem(0), "Create game");
            Button(MenuItem(1), "Connect");
            Button(MenuItem(2), "Exit");
            UiText(_status, new Vector2(24, 410));
        }
        else if (_screen == Screen.Connect)
        {
            UiText("CONNECT TO GAME", new Vector2(GraphicsDevice.Viewport.Width / 2 - 100, 100));
            Button(MenuItem(0), _joinCode.Length == 0 ? "Type game code" : _joinCode);
            Button(MenuItem(1), "Connect");
            Button(MenuItem(2), "Back");
            UiText(_status, new Vector2(24, 410));
        }
        else
        {
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, GraphicsDevice.Viewport.Width, ToolbarHeight), new Color(20, 25, 35));
            Button(MenuButton, "Main menu");
            UiText($"Code: {_gameCode}   Players: {_players}   {_status}", new Vector2(185, 20));
        }
        _spriteBatch.End();
        base.Draw(gameTime);
    }
    private void Button(Rectangle bounds, string text)
    {
        _spriteBatch.Draw(_pixel, bounds, Busy ? new Color(45, 45, 50) : new Color(45, 70, 100));
        UiText(text, new Vector2(bounds.X + 15, bounds.Y + 10));
    }
    private void UiText(string text, Vector2 position)
    {
        var chars = text.ToCharArray();
        for (int i = 0; i < chars.Length; i++) if (chars[i] < 32 || chars[i] > 126) chars[i] = '?';
        string visible = new(chars);
        int maxChars = Math.Max(8, GraphicsDevice.Viewport.Width / 10);
        if (visible.Length > maxChars) visible = visible[..maxChars];
        _spriteBatch.DrawString(_numberFont, visible, position, Color.White, 0f, Vector2.Zero,
            20f / _numberFont.LineSpacing, SpriteEffects.None, 0f);
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
        if (_board == null) return Matrix.Identity;
        Vector2 size = _boardMax - _boardMin;
        float usableHeight = Math.Max(1, height - ToolbarHeight);
        float scale = MathF.Min(Math.Max(1, width - 2 * margin) / size.X, Math.Max(1, usableHeight - 2 * margin) / size.Y);
        Vector2 offset = new Vector2(width / 2f, ToolbarHeight + usableHeight / 2f) - (_boardMin + size / 2f) * scale;

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
