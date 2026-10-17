using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Myra;
using Myra.Graphics2D.UI;
using Myra.Graphics2D;
using Myra.Graphics2D.TextureAtlases;
using System;
using System.Threading.Tasks;
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
        private GraphicsDeviceManager _graphics;
        private Desktop _desktop = null!;
        private AppState _appState = AppState.MainMenu;
        private GameClient _networkClient;

        private Button _createButton = null!;
        private Label _createLabelMain = null!;
        private Label _createLabelShadow = null!;
        private Label _statusLabelMain = null!;
        private Label _statusLabelShadow = null!;
        private TextBox _codeInput = null!;
        private Button _connectButton = null!;
        private Label _connectLabelMain = null!;
        private Label _connectLabelShadow = null!;

        private bool _isConnected = false;
        private bool _isApplyingClientSizeChanges;
        private int _windowedWidth = 1920;
        private int _windowedHeight = 1080;
        private readonly string _playerName = $"Player-{Guid.NewGuid().ToString("N")[..6].ToUpperInvariant()}";

        private MapScreen _mapScreen = null!;

        public Client()
        {
            _graphics = new GraphicsDeviceManager(this);
            _graphics.PreferredBackBufferWidth = 1920;
            _graphics.PreferredBackBufferHeight = 1080;
            _graphics.IsFullScreen = false;
            _graphics.HardwareModeSwitch = false;
            _graphics.PreferMultiSampling = true;

            Content.RootDirectory = "Content";
            IsMouseVisible = true;

            Window.AllowUserResizing = true;
            Window.ClientSizeChanged += OnClientSizeChanged;
            Window.KeyDown += OnKeyDown;

            // Укажите URL вашего SignalR хаба
            _networkClient = new GameClient("http://localhost:5285", NullLogger<GameClient>.Instance);

            _networkClient.PlayerCountChanged += (playerCount) =>
            {
                _statusLabelMain.Text = _statusLabelShadow.Text = $"Подключено, код: {_networkClient.GameCode}, игроков: {playerCount}";
            };
        }

        protected override void LoadContent()
        {
            MyraEnvironment.Game = this;
            BuildUI();

            _mapScreen = new MapScreen(GraphicsDevice, Content);
            _mapScreen.Initialize();
            _mapScreen.LoadContent();
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
            _statusLabelShadow = new Label { Text = "Вы не подключены к игре.\nСоздайте новую игру.", TextColor = Color.Black * 0.7f, Wrap = true, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Font = regularFont, Left = 2, Top = 2 };
            _statusLabelMain = new Label { Text = "Вы не подключены к игре.\nСоздайте новую игру.", TextColor = Color.LightCoral, Wrap = true, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Font = regularFont };
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
            _createLabelShadow = new Label { Text = "Create", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextColor = Color.Black * 0.7f, Font = regularFont, Left = 2, Top = 2 };
            _createLabelMain = new Label { Text = "Create", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextColor = Color.White, Font = regularFont };
            createTextContainer.Widgets.Add(_createLabelShadow);
            createTextContainer.Widgets.Add(_createLabelMain);

            _createButton = new Button { Content = createTextContainer };
            StyleAssetButton(_createButton, buttonTex, 300, 110);

            _createButton.TouchDown += (s, e) => { _createButton.Left = 4; _createButton.Top = 4; };
            _createButton.TouchUp += (s, e) => { _createButton.Left = 0; _createButton.Top = 0; };
            _createButton.TouchLeft += (s, e) => { _createButton.Left = 0; _createButton.Top = 0; };

            _createButton.Click += (s, a) => OnCreateClicked();
            buttonsPanel.Widgets.Add(WrapWithShadow(_createButton, buttonTex, 300, 110));

            var connectTextContainer = new Panel { Margin = new Thickness(0, -10, 0, 0) };
            _connectLabelShadow = new Label { Text = "Connect", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextColor = Color.Black * 0.7f, Font = regularFont, Left = 2, Top = 2 };
            _connectLabelMain = new Label { Text = "Connect", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextColor = Color.White, Font = regularFont };
            connectTextContainer.Widgets.Add(_connectLabelShadow);
            connectTextContainer.Widgets.Add(_connectLabelMain);

            _connectButton = new Button { Content = connectTextContainer };
            StyleAssetButton(_connectButton, buttonTex, 300, 110);

            _connectButton.TouchDown += (s, e) => { _connectButton.Left = 4; _connectButton.Top = 4; };
            _connectButton.TouchUp += (s, e) => { _connectButton.Left = 0; _connectButton.Top = 0; };
            _connectButton.TouchLeft += (s, e) => { _connectButton.Left = 0; _connectButton.Top = 0; };

            _connectButton.Click += (s, a) => OnConnectClicked();
            buttonsPanel.Widgets.Add(WrapWithShadow(_connectButton, buttonTex, 300, 110));

            var exitTextContainer = new Panel { Margin = new Thickness(0, -10, 0, 0) };
            var exitLabelShadow = new Label { Text = "Exit", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextColor = Color.Black * 0.7f, Font = regularFont, Left = 2, Top = 2 };
            var exitLabelMain = new Label { Text = "Exit", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextColor = Color.White, Font = regularFont };
            exitTextContainer.Widgets.Add(exitLabelShadow);
            exitTextContainer.Widgets.Add(exitLabelMain);

            var exitButton = new Button { Content = exitTextContainer };
            StyleAssetButton(exitButton, buttonTex, 300, 110);

            exitButton.TouchDown += (s, e) => { exitButton.Left = 4; exitButton.Top = 4; };
            exitButton.TouchUp += (s, e) => { exitButton.Left = 0; exitButton.Top = 0; };
            exitButton.TouchLeft += (s, e) => { exitButton.Left = 0; exitButton.Top = 0; };

            exitButton.Click += (s, a) => Exit();
            buttonsPanel.Widgets.Add(WrapWithShadow(exitButton, buttonTex, 300, 110));

            contentStack.Widgets.Add(buttonsPanel);
            mainCard.Widgets.Add(contentStack);

            mainCardContainer.Widgets.Add(mainCardShadow);
            mainCardContainer.Widgets.Add(mainCard);
            rootPanel.Widgets.Add(mainCardContainer);

            _desktop = new Desktop { Root = rootPanel };
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

            _codeInput = new TextBox
            {
                Text = "",
                Width = width,
                Height = height,
                Font = font,
                TextColor = Color.White,
                Background = null,
                Padding = new Thickness(75, 16)
            };

            _codeInput.TextChanged += (s, e) =>
            {
                string filteredText = new string(_codeInput.Text
                    .Where(character => char.IsAsciiHexDigit(character))
                    .Select(char.ToUpperInvariant)
                    .ToArray());

                if (filteredText.Length > 6)
                {
                    filteredText = filteredText.Substring(0, 6);
                }

                if (_codeInput.Text != filteredText)
                {
                    _codeInput.Text = filteredText;
                    _codeInput.CursorPosition = filteredText.Length;
                }
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
                _appState = AppState.InGame;
                return;
            }

            _createLabelMain.Text = _createLabelShadow.Text = "Creating...";
            _createButton.Enabled = false;
            _connectButton.Enabled = false;

            var result = await _networkClient.CreateAsync();

            if (result.IsSuccess)
            {
                string code = result.Data ?? string.Empty;
                _codeInput.Text = code;
                await ConnectToSessionAsync(code);
            }
            else
            {
                _statusLabelMain.Text = _statusLabelShadow.Text = result.Data ?? "Ошибка создания игры";
                _statusLabelMain.TextColor = Color.LightCoral;
            }

            if (!_isConnected)
                _createLabelMain.Text = _createLabelShadow.Text = "Create";

            _createButton.Enabled = true;
            _connectButton.Enabled = true;
        }

        private async void OnConnectClicked()
        {
            if (_isConnected)
            {
                _connectLabelMain.Text = _connectLabelShadow.Text = "Disconnecting...";
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
            _connectLabelMain.Text = _connectLabelShadow.Text = "Connecting...";
            _connectButton.Enabled = false;

            var result = await _networkClient.ConnectAsync(gameCode, _playerName);

            if (result.IsSuccess)
            {
                _isConnected = true;
                _statusLabelMain.Text = _statusLabelShadow.Text = $"Успешно подключено к игре: {gameCode}!";
                _statusLabelMain.TextColor = Color.LightGreen;
                _createLabelMain.Text = _createLabelShadow.Text = "Start Game";
                _connectLabelMain.Text = _connectLabelShadow.Text = "Disconnect";
            }
            else
            {
                _statusLabelMain.Text = _statusLabelShadow.Text = result.Error ?? "Ошибка сети";
                _statusLabelMain.TextColor = Color.LightCoral;
                _createLabelMain.Text = _createLabelShadow.Text = "Create";
                _connectLabelMain.Text = _connectLabelShadow.Text = "Connect";
            }

            _connectButton.Enabled = true;
        }

        private async Task Disconnect()
        {
            await _networkClient.DisconnectAsync();
            _isConnected = false;
            _statusLabelMain.Text = _statusLabelShadow.Text = "Вы не подключены к игре. Создайте новую игру.";
            _statusLabelMain.TextColor = Color.LightCoral;
            _createLabelMain.Text = _createLabelShadow.Text = "Create";
            _connectLabelMain.Text = _connectLabelShadow.Text = "Connect";
        }

        protected override void Update(GameTime gameTime)
        {
            if (_appState == AppState.InGame)
            {
                _mapScreen.Update(gameTime);

                if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                {
                    _appState = AppState.MainMenu;
                }
            }
            base.Update(gameTime);
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
