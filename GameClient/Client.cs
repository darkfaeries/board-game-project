using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra;
using Myra.Graphics2D.UI;
using Myra.Graphics2D;
using Myra.Graphics2D.TextureAtlases;
using System;
using System.Threading.Tasks;
using System.Collections.Concurrent;
using FontStashSharp;
using System.IO;
using System.Linq;
using Microsoft.Extensions.Logging.Abstractions;

namespace GameClient
{
    public enum AppState
    {
        MainMenu,
        InGame
    }

    public class Client : Game
    {
        private const string JoinPrompt = "Создайте игру или введите код комнаты.";

        private GraphicsDeviceManager _graphics;
        private Desktop _desktop = null!;
        private AppState _appState = AppState.MainMenu;
        private GameClient _networkClient;

        private Button _createButton = null!;
        private Label _createLabelMain = null!;
        private Label _createLabelShadow = null!;
        private Label _statusLabelMain = null!;
        private Label _statusLabelShadow = null!;
        private SessionCodeInput _codeInput = null!;
        private string? _createdRoomCode;
        private Button _connectButton = null!;
        private Label _connectLabelMain = null!;
        private Label _connectLabelShadow = null!;

        private bool _isConnected = false;
        private bool _isApplyingClientSizeChanges;
        private int _windowedWidth = 1920;
        private int _windowedHeight = 1080;
        private readonly string _playerName = $"Player-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

        private MapScreen _mapScreen = null!;

        // Снимок состояния тянем из хаба и применяем на игровом потоке.
        private GameSnapshot? _pendingSnapshot;
        private volatile bool _snapshotDirty;

        // Чей сейчас ход и какая фаза: сервер сообщает это только событиями.
        private sealed record CurrentPlayerState(Guid? Id);
        private volatile CurrentPlayerState _currentPlayer = new(null);

        private sealed record PhaseState(TurnPhase Phase, bool RollUsed, int? LastRoll);
        private volatile PhaseState _phaseState = new(TurnPhase.Roll, false, null);
        private sealed record StatusState(GameStatus Status);
        private volatile StatusState _gameStatus = new(GameStatus.WaitingForPlayers);
        private sealed record PendingDiscardsState(Dictionary<Guid, int> Counts);
        private volatile PendingDiscardsState _pendingDiscards = new(new Dictionary<Guid, int>());
        private sealed record WinnerState(Guid? PlayerId);
        private volatile WinnerState _winner = new(null);
        private sealed record PendingTradeState(PendingTradeSnapshot? Trade);
        private volatile PendingTradeState _pendingTrade = new(null);
        private volatile string? _pendingTradeNotice;
        private readonly ConcurrentQueue<AxialCoords> _pendingTribeCollections = new();

        private Guid? _myPlayerId;
        private bool _gameStarted;
        private int _gameStartEventPending;

        public Client(GameClient? networkClient = null, bool startWindowed = false, int? windowNumber = null)
        {
            _graphics = new GraphicsDeviceManager(this);
            var displayMode = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode;
            // F11/Alt+Enter returns to a usable window instead of opening a
            // 1920x1080 window that may not fit on the current monitor.
            _windowedWidth = Math.Min(1600, displayMode.Width);
            _windowedHeight = Math.Min(900, displayMode.Height);
            _graphics.PreferredBackBufferWidth = startWindowed ? _windowedWidth : displayMode.Width;
            _graphics.PreferredBackBufferHeight = startWindowed ? _windowedHeight : displayMode.Height;
            _graphics.IsFullScreen = !startWindowed;
            _graphics.HardwareModeSwitch = false;
            _graphics.PreferMultiSampling = true;

            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            Window.Title = windowNumber is int number ? $"Каменный век · Окно {number}" : "Каменный век";
            Window.AllowUserResizing = true;
            Window.ClientSizeChanged += OnClientSizeChanged;
            Window.KeyDown += OnKeyDown;

            // Укажите URL вашего SignalR хаба
            _networkClient = networkClient ?? new GameClient("http://localhost:5285", NullLogger<GameClient>.Instance);

            _networkClient.PlayerCountChanged += (playerCount) =>
            {
                _statusLabelMain.Text = _statusLabelShadow.Text = $"Подключено, код: {_networkClient.GameCode}, игроков: {playerCount}";
                _snapshotDirty = true;
            };

            // Любое игровое событие означает, что состояние на карте пора обновить.
            _networkClient.GameStarted += value =>
            {
                _winner = new WinnerState(null);
                _currentPlayer = new CurrentPlayerState(value.CurrentPlayerId);
                _phaseState = new PhaseState(value.CurrentPhase, false, null);
                _gameStatus = new StatusState(GameStatus.Setup);
                _pendingDiscards = new PendingDiscardsState(new Dictionary<Guid, int>());
                _pendingTrade = new PendingTradeState(null);
                // SignalR callbacks run off the game thread. Let Update perform
                // the UI transition for every client that receives the broadcast.
                System.Threading.Interlocked.Exchange(ref _gameStartEventPending, 1);
                _snapshotDirty = true;
            };
            _networkClient.TurnChanged += value =>
            {
                // Сервер шлёт событие после каждого EndTurn, поэтому фазу двигаем так же, как он.
                _currentPlayer = new CurrentPlayerState(value.NextPlayerId);
                _phaseState = new PhaseState(NextPhase(_phaseState.Phase), false, null);
                _gameStatus = new StatusState(GameStatus.Playing);
                _pendingTrade = new PendingTradeState(null);
                _snapshotDirty = true;
            };
            _networkClient.InitialPlacementSubmitted += value =>
            {
                _currentPlayer = new CurrentPlayerState(value.NextPlayerId);
                _phaseState = new PhaseState(value.Phase, false, null);
                _gameStatus = new StatusState(value.Status);
                _snapshotDirty = true;
            };
            _networkClient.RollPhaseCompleted += value =>
            {
                _phaseState = new PhaseState(_phaseState.Phase, true, value.DiceRoll);
                _snapshotDirty = true;
            };
            _networkClient.SevenRolled += value =>
            {
                var pending = value.PendingDecisions
                    .Where(decision => decision.DecisionType == nameof(DiscardResourcesDecision) && decision.RequiredCount > 0)
                    .GroupBy(decision => decision.PlayerId)
                    .ToDictionary(group => group.Key, group => group.Max(decision => decision.RequiredCount));
                _pendingDiscards = new PendingDiscardsState(pending);
                _snapshotDirty = true;
            };
            _networkClient.ExplorerCreated += _ => _snapshotDirty = true;
            _networkClient.ExplorerMoved += _ => _snapshotDirty = true;
            _networkClient.ExplorerConvertedToCamp += value =>
            {
                _pendingTribeCollections.Enqueue(value.NodeCoords);
                _snapshotDirty = true;
            };
            _networkClient.ProgressAdvanced += _ => _snapshotDirty = true;
            _networkClient.PlayerTradeRequested += value =>
            {
                _pendingTrade = new PendingTradeState(new PendingTradeSnapshot(
                    value.TradeId,
                    value.Source,
                    value.Target,
                    new Dictionary<ResourceType, int>(value.Offer),
                    new Dictionary<ResourceType, int>(value.Request)));
                _snapshotDirty = true;
            };
            _networkClient.TradeDecisionSubmitted += value =>
            {
                _pendingTrade = new PendingTradeState(null);
                _pendingTradeNotice = value.IsAccepted ? "Обмен принят!" : "Обмен отклонён.";
                _snapshotDirty = true;
            };
            _networkClient.BankTradeCompleted += _ => _snapshotDirty = true;
            _networkClient.DiscardResourcesSubmitted += value =>
            {
                var pending = new Dictionary<Guid, int>(_pendingDiscards.Counts);
                pending.Remove(value.PlayerId);
                _pendingDiscards = new PendingDiscardsState(pending);
                _snapshotDirty = true;
            };
            _networkClient.WinnerChecked += value =>
            {
                _winner = new WinnerState(value.Id);
                _snapshotDirty = true;
            };
        }

        protected override void LoadContent()
        {
            MyraEnvironment.Game = this;
            if (OperatingSystem.IsMacOS())
            {
                // Myra implements editing shortcuts using Control. Accept either Mac modifier.
                MyraEnvironment.DownKeysGetter = down =>
                {
                    MyraEnvironment.DefaultDownKeysGetter(down);
                    down[(int)Keys.LeftControl] |= down[(int)Keys.LeftWindows];
                    down[(int)Keys.RightControl] |= down[(int)Keys.RightWindows];
                };
                MyraEnvironment.MouseInfoGetter = () =>
                {
                    var mouse = MyraEnvironment.DefaultMouseInfoGetter();
                    mouse.Position = WindowPointer.InViewport(GraphicsDevice);
                    return mouse;
                };
            }
            BuildUI();

            _mapScreen = new MapScreen(GraphicsDevice, Content);
            _mapScreen.Initialize();
            _mapScreen.LoadContent();
            Deactivated += (_, _) => _mapScreen.ResetPointerInput();

            _mapScreen.RollRequested += () => _ = RollAsync();
            _mapScreen.EndTurnRequested += () => _ = EndTurnAsync();
            _mapScreen.InitialPlacementRequested += coords => _ = RunMapActionAsync(
                () => _networkClient.SubmitInitialPlacementAsync(coords), "Не удалось поставить начальный лагерь");
            _mapScreen.CreateExplorerRequested += coords => _ = RunMapActionAsync(
                () => _networkClient.CreateExplorerAsync(coords), "Не удалось создать исследователя");
            _mapScreen.ConvertExplorerToCampRequested += coords => _ = RunMapActionAsync(
                () => _networkClient.ConvertExplorerToCampAsync(coords), "Не удалось построить лагерь", checkWinner: true);
            _mapScreen.MoveExplorerRequested += route => _ = RunMapActionAsync(
                () => _networkClient.MoveExplorerAsync(route), "Не удалось переместить исследователя", checkWinner: true);
            _mapScreen.ProgressRequested += progressType => _ = RunMapActionAsync(
                () => _networkClient.AdvanceProgressAsync(progressType), "Не удалось повысить параметр", checkWinner: true);
            _mapScreen.DiscardResourcesRequested += resources => _ = RunMapActionAsync(
                () => _networkClient.DiscardResourcesDecisionAsync(resources),
                "Не удалось сбросить ресурсы");
            _mapScreen.TradeWithPlayerRequested += (targetId, offer, request) => _ = RunMapActionAsync(
                () => _networkClient.TradeWithPlayerAsync(targetId, offer, request), "Не удалось предложить обмен");
            _mapScreen.TradeDecisionRequested += (sourcePlayerId, tradeId, isAccepted) => _ = RunMapActionAsync(
                () => _networkClient.SubmitTradeDecisionAsync(sourcePlayerId, tradeId, isAccepted), "Не удалось ответить на обмен");
            _mapScreen.TradeWithBankRequested += (offer, resource) => _ = RunMapActionAsync(
                () => _networkClient.TradeWithBankAsync(offer, resource), "Не удалось обменять ресурсы");
            _mapScreen.MenuRequested += () => _appState = AppState.MainMenu;
            _mapScreen.ReturnToMainMenuRequested += () =>
            {
                _appState = AppState.MainMenu;
                _ = _networkClient.DisconnectAsync();
                _isConnected = false;
                _gameStarted = false;
                _gameStartEventPending = 0;
                _myPlayerId = null;
                _pendingSnapshot = null;
                _snapshotDirty = false;
                _winner = new WinnerState(null);
                _gameStatus = new StatusState(GameStatus.WaitingForPlayers);
                _currentPlayer = new CurrentPlayerState(null);
                _phaseState = new PhaseState(TurnPhase.Roll, false, null);
                _pendingDiscards = new PendingDiscardsState(new Dictionary<Guid, int>());
                _pendingTrade = new PendingTradeState(null);
                _pendingTradeNotice = null;
                while (_pendingTribeCollections.TryDequeue(out _)) { }
                _mapScreen.Reset();
                _createLabelMain.Text = _createLabelShadow.Text = "Создать игру";
                _connectLabelMain.Text = _connectLabelShadow.Text = "Подключиться";
                _statusLabelMain.Text = _statusLabelShadow.Text = JoinPrompt;
                _statusLabelMain.TextColor = Color.LightCoral;
            };
        }

        private static TurnPhase NextPhase(TurnPhase phase) => phase switch
        {
            TurnPhase.Roll => TurnPhase.Trade,
            TurnPhase.Trade => TurnPhase.Actions,
            _ => TurnPhase.Roll,
        };

        protected override void Initialize()
        {
            base.Initialize();
            SyncBackBufferToWindow();
        }

        // Окно 1920x1080 на мониторе меньшего размера обрезается, а backbuffer остаётся
        // 1920x1080: UI рисуется за границей окна и клики по нему не попадают.
        // После создания устройства подгоняем backbuffer под реальную клиентскую область.
        private void SyncBackBufferToWindow()
        {
            if (_graphics.IsFullScreen) return;

            var bounds = Window.ClientBounds;
            if (bounds.Width <= 0 || bounds.Height <= 0) return;

            _windowedWidth = bounds.Width;
            _windowedHeight = bounds.Height;

            if (_graphics.PreferredBackBufferWidth == bounds.Width &&
                _graphics.PreferredBackBufferHeight == bounds.Height)
                return;

            _isApplyingClientSizeChanges = true;
            try
            {
                _graphics.PreferredBackBufferWidth = bounds.Width;
                _graphics.PreferredBackBufferHeight = bounds.Height;
                _graphics.ApplyChanges();
            }
            finally
            {
                _isApplyingClientSizeChanges = false;
            }
        }

        private void BuildUI()
        {
            string assetsPath = System.IO.Path.Combine(AppContext.BaseDirectory, "assets");

            string fontPath = System.IO.Path.Combine(assetsPath, "Marmelad-Regular.ttf");
            if (!File.Exists(fontPath))
            {
                string fontsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
                if (!string.IsNullOrEmpty(fontsDirectory))
                {
                    fontPath = System.IO.Path.Combine(fontsDirectory, "arial.ttf");
                }
            }

            byte[] ttfData = File.ReadAllBytes(fontPath);
            FontSystem fontSystem = new FontSystem();
            fontSystem.AddFont(ttfData);

            SpriteFontBase regularFont = fontSystem.GetFont(28);

            Texture2D windowTex = Texture2D.FromFile(GraphicsDevice, System.IO.Path.Combine(assetsPath, "window.png"));
            Texture2D inputTex = Texture2D.FromFile(GraphicsDevice, System.IO.Path.Combine(assetsPath, "input1.png"));
            Texture2D buttonTex = Texture2D.FromFile(GraphicsDevice, System.IO.Path.Combine(assetsPath, "button.png"));
            Texture2D textTex = Texture2D.FromFile(GraphicsDevice, System.IO.Path.Combine(assetsPath, "text.png"));

            Texture2D vignetteTex = CreateVignetteTexture(1100, 500, 1.0f);
            var rootPanel = new Panel();
            string bgPath = System.IO.Path.Combine(assetsPath, "background_main_menu.jpg");
            if (File.Exists(bgPath))
            {
                Texture2D bgTexture = Texture2D.FromFile(GraphicsDevice, bgPath);
                rootPanel.Background = new TextureRegion(bgTexture);
            }

            var mainCardContainer = new Panel
            {
                Width = 1400,
                Height = 900,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var mainCardShadow = new Image
            {
                Renderable = new TextureRegion(windowTex),
                Color = Color.Black * 0.7f,
                Left = 15,
                Top = 15,
                Width = 1400,
                Height = 900
            };

            var mainCard = new Panel
            {
                Width = 1400,
                Height = 900,
                Background = new TextureRegion(windowTex)
            };

            var contentStack = new VerticalStackPanel
            {
                Spacing = 5,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Top,
                Padding = new Thickness(0, 180, 0, 0)
            };

            var titleImage = new Image { Renderable = new TextureRegion(textTex), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Width = 624, Height = 234 };

            var titlePanel = new Panel { Width = 624, Height = 234, HorizontalAlignment = HorizontalAlignment.Center };

            var vignette = new Image
            {
                Renderable = new TextureRegion(vignetteTex),
                Width = 1100,
                Height = 500,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center
            };

            var titleShadow = new Image { Renderable = new TextureRegion(textTex), Color = Color.Black * 0.55f, Left = 6, Top = 6, Width = 624, Height = 234 };

            titlePanel.Widgets.Add(vignette);
            titlePanel.Widgets.Add(titleShadow);
            titlePanel.Widgets.Add(titleImage);

            titlePanel.Margin = new Thickness(0, 15, 0, 0);
            contentStack.Widgets.Add(titlePanel);

            var statusContainer = new Panel { Width = 750, Height = 100, HorizontalAlignment = HorizontalAlignment.Center };
            statusContainer.Margin = new Thickness(0, 0, 0, 0);
            _statusLabelShadow = new Label { Text = JoinPrompt, TextColor = Color.Black * 0.7f, Wrap = true, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Font = regularFont, Left = 2, Top = 2 };
            _statusLabelMain = new Label { Text = JoinPrompt, TextColor = Color.LightCoral, Wrap = true, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Font = regularFont };
            statusContainer.Widgets.Add(_statusLabelShadow);
            statusContainer.Widgets.Add(_statusLabelMain);
            contentStack.Widgets.Add(statusContainer);

            var inputPanel = CreateInputField(inputTex, regularFont, 550, 70);
            inputPanel.Margin = new Thickness(0, -10, 0, 0);
            contentStack.Widgets.Add(inputPanel);

            var buttonsPanel = new HorizontalStackPanel
            {
                Spacing = 30,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            var createTextContainer = new Panel { Margin = new Thickness(0, -10, 0, 0) };
            _createLabelShadow = new Label { Text = "Создать игру", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextColor = Color.Black * 0.7f, Font = regularFont, Left = 2, Top = 2 };
            _createLabelMain = new Label { Text = "Создать игру", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextColor = Color.White, Font = regularFont };
            createTextContainer.Widgets.Add(_createLabelShadow);
            createTextContainer.Widgets.Add(_createLabelMain);

            _createButton = new Button { Content = createTextContainer };
            StyleAssetButton(_createButton, buttonTex, 300, 110);

            _createButton.TouchDown += (s, e) => { createTextContainer.Left = 4; createTextContainer.Top = 4; };
            _createButton.TouchUp += (s, e) => { createTextContainer.Left = 0; createTextContainer.Top = 0; };
            _createButton.TouchLeft += (s, e) => { createTextContainer.Left = 0; createTextContainer.Top = 0; };

            _createButton.Click += (s, a) => OnCreateClicked();
            buttonsPanel.Widgets.Add(WrapWithShadow(_createButton, buttonTex, 300, 110));

            var connectTextContainer = new Panel { Margin = new Thickness(0, -10, 0, 0) };
            _connectLabelShadow = new Label { Text = "Подключиться", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextColor = Color.Black * 0.7f, Font = regularFont, Left = 2, Top = 2 };
            _connectLabelMain = new Label { Text = "Подключиться", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextColor = Color.White, Font = regularFont };
            connectTextContainer.Widgets.Add(_connectLabelShadow);
            connectTextContainer.Widgets.Add(_connectLabelMain);

            _connectButton = new Button { Content = connectTextContainer };
            StyleAssetButton(_connectButton, buttonTex, 300, 110);

            _connectButton.TouchDown += (s, e) => { connectTextContainer.Left = 4; connectTextContainer.Top = 4; };
            _connectButton.TouchUp += (s, e) => { connectTextContainer.Left = 0; connectTextContainer.Top = 0; };
            _connectButton.TouchLeft += (s, e) => { connectTextContainer.Left = 0; connectTextContainer.Top = 0; };

            _connectButton.Click += (s, a) => OnConnectClicked();
            buttonsPanel.Widgets.Add(WrapWithShadow(_connectButton, buttonTex, 300, 110));

            var exitTextContainer = new Panel { Margin = new Thickness(0, -10, 0, 0) };
            var exitLabelShadow = new Label { Text = "Выйти", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextColor = Color.Black * 0.7f, Font = regularFont, Left = 2, Top = 2 };
            var exitLabelMain = new Label { Text = "Выйти", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextColor = Color.White, Font = regularFont };
            exitTextContainer.Widgets.Add(exitLabelShadow);
            exitTextContainer.Widgets.Add(exitLabelMain);

            var exitButton = new Button { Content = exitTextContainer };
            StyleAssetButton(exitButton, buttonTex, 300, 110);

            exitButton.TouchDown += (s, e) => { exitTextContainer.Left = 4; exitTextContainer.Top = 4; };
            exitButton.TouchUp += (s, e) => { exitTextContainer.Left = 0; exitTextContainer.Top = 0; };
            exitButton.TouchLeft += (s, e) => { exitTextContainer.Left = 0; exitTextContainer.Top = 0; };

            exitButton.Click += (s, a) => Exit();
            buttonsPanel.Widgets.Add(WrapWithShadow(exitButton, buttonTex, 300, 110));

            contentStack.Widgets.Add(buttonsPanel);
            mainCard.Widgets.Add(contentStack);

            mainCardContainer.Widgets.Add(mainCardShadow);
            mainCardContainer.Widgets.Add(mainCard);
            rootPanel.Widgets.Add(mainCardContainer);

            _desktop = new Desktop { Root = rootPanel };
            _desktop.FocusedKeyboardWidget = _codeInput;
            _desktop.KeyDown += (_, e) =>
            {
                bool shortcut = _desktop.IsKeyDown(Keys.LeftControl) || _desktop.IsKeyDown(Keys.RightControl);
                bool alt = _desktop.IsKeyDown(Keys.LeftAlt) || _desktop.IsKeyDown(Keys.RightAlt);
                if (alt) return;

                if (e.Data == Keys.C && shortcut && !_codeInput.IsKeyboardFocused)
                {
                    string? code = _isConnected ? _networkClient.GameCode : _codeInput.Text;
                    if (!string.IsNullOrEmpty(code)) SessionClipboard.TryCopy(code);
                }

                if (e.Data == Keys.Enter && !shortcut && _codeInput.IsKeyboardFocused && !_isConnected &&
                    _codeInput.Text.Length == SessionCodeInput.CodeLength && _connectButton.Enabled)
                    OnConnectClicked();
            };
        }

        private Texture2D CreateVignetteTexture(int width, int height, float maxAlpha)
        {
            Texture2D tex = new Texture2D(GraphicsDevice, width, height);
            Color[] data = new Color[width * height];
            float cx = width / 2f;
            float cy = height / 2f;

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    float dx = (x - cx) / cx;
                    float dy = (y - cy) / cy;
                    float dist = (float)Math.Sqrt(dx * dx + dy * dy);

                    float innerRadius = 0.35f;
                    float t;
                    if (dist < innerRadius)
                    {
                        t = 1f;
                    }
                    else
                    {
                        t = 1f - (dist - innerRadius) / (1f - innerRadius);
                        t = MathHelper.Clamp(t, 0f, 1f);
                        t = t * t * (3f - 2f * t);
                    }

                    float alpha = t * maxAlpha;

                    int aByte = (int)(alpha * 255);
                    data[y * width + x] = new Color(0, 0, 0, aByte);
                }
            }

            tex.SetData(data);
            return tex;
        }

        private Panel CreateInputField(Texture2D inputTex, SpriteFontBase font, int width, int height)
        {
            var container = new Panel { Width = width, Height = height, HorizontalAlignment = HorizontalAlignment.Center };

            var shadow = new Image { Renderable = new TextureRegion(inputTex), Color = Color.Black * 0.55f, Left = 6, Top = 6, Width = width, Height = height };
            var background = new Image { Renderable = new TextureRegion(inputTex), Width = width, Height = height };

            _codeInput = new SessionCodeInput
            {
                Text = "",
                Width = width,
                Height = height,
                Font = font,
                TextColor = Color.White,
                Background = null,
                Padding = new Thickness(75, 16)
            };

            container.Widgets.Add(shadow);
            container.Widgets.Add(background);
            container.Widgets.Add(_codeInput);

            return container;
        }

        private Panel WrapWithShadow(Widget mainWidget, Texture2D tex, int width, int height)
        {
            var container = new Panel { Width = width, Height = height, HorizontalAlignment = HorizontalAlignment.Center };
            var shadow = new Image { Renderable = new TextureRegion(tex), Color = Color.Black * 0.55f, Left = 6, Top = 6, Width = width, Height = height };

            container.Widgets.Add(shadow);
            container.Widgets.Add(mainWidget);

            return container;
        }

        private void StyleAssetButton(Button button, Texture2D tex, int width, int height)
        {
            button.Width = width;
            button.Height = height;
            button.Padding = new Thickness(0);
            button.Background = new TextureRegion(tex);
            button.OverBackground = new TextureRegion(tex);
            button.PressedBackground = new TextureRegion(tex);
        }

        private void OnClientSizeChanged(object sender, EventArgs e)
        {
            if (_graphics.IsFullScreen) return;

            if (_isApplyingClientSizeChanges)
            {
                return;
            }

            int width = Window.ClientBounds.Width;
            int height = Window.ClientBounds.Height;
            if (width <= 0 || height <= 0)
            {
                return;
            }

            _windowedWidth = width;
            _windowedHeight = height;

            if (_graphics.PreferredBackBufferWidth == width && _graphics.PreferredBackBufferHeight == height)
                return;

            _isApplyingClientSizeChanges = true;
            try
            {
                _graphics.PreferredBackBufferWidth = width;
                _graphics.PreferredBackBufferHeight = height;
                _graphics.ApplyChanges();
            }
            finally
            {
                _isApplyingClientSizeChanges = false;
            }
        }

        private void OnKeyDown(object sender, InputKeyEventArgs e)
        {
            bool altEnter = (e.Key == Keys.Enter &&
                             (Keyboard.GetState().IsKeyDown(Keys.LeftAlt) ||
                              Keyboard.GetState().IsKeyDown(Keys.RightAlt)));

            if (altEnter || e.Key == Keys.F11)
            {
                ToggleFullscreen();
            }
        }

        private void ToggleFullscreen()
        {
            if (!_graphics.IsFullScreen)
            {
                int width = Window.ClientBounds.Width;
                int height = Window.ClientBounds.Height;
                if (width > 0 && height > 0)
                {
                    _windowedWidth = width;
                    _windowedHeight = height;
                }

                _graphics.IsFullScreen = true;
                _graphics.PreferredBackBufferWidth = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Width;
                _graphics.PreferredBackBufferHeight = GraphicsAdapter.DefaultAdapter.CurrentDisplayMode.Height;
            }
            else
            {
                _graphics.IsFullScreen = false;
                _graphics.PreferredBackBufferWidth = _windowedWidth;
                _graphics.PreferredBackBufferHeight = _windowedHeight;
            }

            _graphics.ApplyChanges();
        }

        private async void OnCreateClicked()
        {
            if (_isConnected)
            {
                if (_gameStarted)
                {
                    _appState = AppState.InGame;
                    return;
                }
                _createButton.Enabled = false;
                var start = await _networkClient.StartGameAsync();
                _createButton.Enabled = true;

                if (!start.Ok)
                {
                    _statusLabelMain.Text = _statusLabelShadow.Text = start.Error ?? "Не удалось начать игру";
                    _statusLabelMain.TextColor = Color.LightCoral;
                }
                return;
            }

            _createLabelMain.Text = _createLabelShadow.Text = "Создаём игру…";
            _createButton.Enabled = false;
            _connectButton.Enabled = false;

            var auth = await _networkClient.AuthenticateGuestAsync(_playerName);
            if (!auth.Ok)
            {
                _statusLabelMain.Text = _statusLabelShadow.Text = auth.Error ?? "Ошибка авторизации";
                _statusLabelMain.TextColor = Color.LightCoral;
                _createLabelMain.Text = _createLabelShadow.Text = "Создать игру";
                _createButton.Enabled = true;
                _connectButton.Enabled = true;
                return;
            }

            var result = await _networkClient.CreateAsync();

            if (result.IsSuccess)
            {
                string code = result.Data ?? string.Empty;
                Interlocked.Exchange(ref _createdRoomCode, code);
                await ConnectToSessionAsync(code);
            }
            else
            {
                _statusLabelMain.Text = _statusLabelShadow.Text = result.Data ?? "Ошибка создания игры";
                _statusLabelMain.TextColor = Color.LightCoral;
            }

            if (!_isConnected)
                _createLabelMain.Text = _createLabelShadow.Text = "Создать игру";

            _createButton.Enabled = true;
            _connectButton.Enabled = true;
        }

        private async void OnConnectClicked()
        {
            if (_isConnected)
            {
                _connectLabelMain.Text = _connectLabelShadow.Text = "Отключаемся…";
                _connectButton.Enabled = false;
                await Disconnect();
                _connectButton.Enabled = true;
                return;
            }

            string inputCode = _codeInput.Text;
            if (string.IsNullOrWhiteSpace(inputCode)) return;

            await ConnectToSessionAsync(inputCode);
        }

        private async Task ConnectToSessionAsync(string gameCode)
        {
            _connectLabelMain.Text = _connectLabelShadow.Text = "Подключаемся…";
            _connectButton.Enabled = false;

            var result = await _networkClient.ConnectAsync(gameCode, _playerName);

            if (result.IsSuccess)
            {
                _isConnected = true;
                _snapshotDirty = true;
                _statusLabelMain.Text = _statusLabelShadow.Text = $"Успешно подключено к игре: {gameCode}!";
                _statusLabelMain.TextColor = Color.LightGreen;
                _createLabelMain.Text = _createLabelShadow.Text = "Начать игру";
                _connectLabelMain.Text = _connectLabelShadow.Text = "Отключиться";
            }
            else
            {
                _statusLabelMain.Text = _statusLabelShadow.Text = result.Error ?? "Не удалось подключиться к игре";
                _statusLabelMain.TextColor = Color.LightCoral;
                _createLabelMain.Text = _createLabelShadow.Text = "Создать игру";
                _connectLabelMain.Text = _connectLabelShadow.Text = "Подключиться";
            }

            _connectButton.Enabled = true;
        }

        private async Task Disconnect()
        {
            await _networkClient.DisconnectAsync();
            _isConnected = false;
            _gameStarted = false;
            _mapScreen.Reset();
            _pendingSnapshot = null;
            _snapshotDirty = false;
            _statusLabelMain.Text = _statusLabelShadow.Text = JoinPrompt;
            _statusLabelMain.TextColor = Color.LightCoral;
            _createLabelMain.Text = _createLabelShadow.Text = "Создать игру";
            _connectLabelMain.Text = _connectLabelShadow.Text = "Подключиться";
        }

        protected override void Update(GameTime gameTime)
        {
            if (Interlocked.Exchange(ref _createdRoomCode, null) is { } code)
                _codeInput.SetRoomCode(code);

            if (System.Threading.Interlocked.Exchange(ref _gameStartEventPending, 0) == 1)
            {
                _gameStarted = true;
                _createLabelMain.Text = _createLabelShadow.Text = "Продолжить";
                _appState = AppState.InGame;
            }

            PumpSnapshot();

            if (_appState == AppState.InGame)
            {
                _mapScreen.Update(gameTime, IsActive);

                if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                {
                    _appState = AppState.MainMenu;
                }
            }
            if (_appState == AppState.MainMenu)
                _mapScreen.ResetPointerInput();
            base.Update(gameTime);
        }

        // Применяет готовый снимок и, если состояние изменилось, запрашивает новый.
        private void PumpSnapshot()
        {
            while (_pendingTribeCollections.TryDequeue(out var nodeCoords))
                _mapScreen.MarkTribeCollected(nodeCoords);

            if (System.Threading.Interlocked.Exchange(ref _pendingTradeNotice, null) is { } tradeNotice)
                _mapScreen.ShowMessage(tradeNotice, isError: false);

            if (_pendingSnapshot is { } snapshot)
            {
                _pendingSnapshot = null;
                _mapScreen.ApplySnapshot(snapshot);
            }

            if (!_snapshotDirty) return;

            _snapshotDirty = false;
            _ = RefreshSnapshotAsync();
        }

        private async Task RefreshSnapshotAsync()
        {
            try
            {
                var snapshot = await _networkClient.GetSnapshotAsync();
                if (snapshot is null) return;

                // Свой id ищем по уникальному имени, которое клиент отправляет при входе.
                snapshot.YouPlayerId = snapshot.Players
                    .FirstOrDefault(player => player.Name == _networkClient.AuthenticatedUsername)?.Id;
                _myPlayerId = snapshot.YouPlayerId;
                snapshot.GameCode = _networkClient.GameCode;

                snapshot.CurrentPlayerId = _currentPlayer.Id;
                snapshot.Status = _gameStatus.Status;
                snapshot.Phase = _phaseState.Phase;
                snapshot.RollUsed = _phaseState.RollUsed;
                snapshot.LastRoll = _phaseState.LastRoll;
                snapshot.WinnerPlayerId = _winner.PlayerId;
                snapshot.PendingTrade = _pendingTrade.Trade;
                foreach (var player in snapshot.Players)
                    player.DiscardResourcesRequired = _pendingDiscards.Counts.TryGetValue(player.Id, out int required)
                        ? required
                        : null;

                _pendingSnapshot = snapshot;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Не удалось получить состояние партии: {ex.Message}");
            }
        }

        // --- Действия игрока в свой ход ---

        private async Task RollAsync()
        {
            if (_myPlayerId is not Guid playerId) return;

            _mapScreen.Busy = true;
            try
            {
                var result = await _networkClient.RollPhaseAsync(playerId);
                if (!result.Ok)
                    _mapScreen.ShowMessage(result.Error ?? "Не удалось бросить кубики");

                _snapshotDirty = true;
            }
            finally
            {
                _mapScreen.Busy = false;
            }
        }

        private async Task EndTurnAsync()
        {
            _mapScreen.Busy = true;
            try
            {
                var result = await _networkClient.EndTurnAsync();
                if (!result.Ok)
                    _mapScreen.ShowMessage(result.Error ?? "Не удалось завершить ход");

                _snapshotDirty = true;
            }
            finally
            {
                _mapScreen.Busy = false;
            }
        }

        private async Task RunMapActionAsync(
            Func<Task<(bool Ok, string? Error)>> action,
            string fallbackError,
            bool checkWinner = false)
        {
            if (_mapScreen.Busy) return;

            _mapScreen.Busy = true;
            try
            {
                var result = await action();
                if (!result.Ok)
                    _mapScreen.ShowMessage(result.Error ?? fallbackError);
                else if (checkWinner)
                    _ = _networkClient.CheckWinnerAsync();

                _snapshotDirty = true;
            }
            finally
            {
                _mapScreen.Busy = false;
            }
        }

        protected override void Draw(GameTime gameTime)
        {
            if (_appState == AppState.MainMenu)
            {
                GraphicsDevice.Clear(new Color(25, 25, 25));
                _desktop.Render();
            }
            else if (_appState == AppState.InGame)
            {
                _mapScreen.Draw(gameTime);
            }

            base.Draw(gameTime);
        }
    }
}
