using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Myra;
using Myra.Graphics2D.UI;
using Myra.Graphics2D;
using Myra.Graphics2D.TextureAtlases;
using System;
using System.Threading.Tasks;
using FontStashSharp;
using System.IO;

namespace GameClient
{
    public class Client : Game
    {
        private GraphicsDeviceManager _graphics;
        private Desktop _desktop = null!;
        private Label _statusLabel = null!;
        private TextBox _codeInput = null!;
        private Button _connectButton = null!;
        private Label _connectButtonLabel = null!;

        private bool _isConnected = false;

        public Client()
        {
            _graphics = new GraphicsDeviceManager(this);
            _graphics.PreferredBackBufferWidth = 1280;
            _graphics.PreferredBackBufferHeight = 720;
            _graphics.IsFullScreen = false;
            Content.RootDirectory = "Content";
            IsMouseVisible = true;
        }

        protected override void LoadContent()
        {
            MyraEnvironment.Game = this;
            BuildUI();
        }

        private void BuildUI()
        {
            string fontPath = Path.Combine(AppContext.BaseDirectory, "arial.ttf");
            string fontsDirectory = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
            if (!File.Exists(fontPath) && !string.IsNullOrEmpty(fontsDirectory))
            {
                fontPath = Path.Combine(fontsDirectory, "arial.ttf");
            }

            byte[] ttfData = File.ReadAllBytes(fontPath);
            FontSystem fontSystem = new FontSystem();
            fontSystem.AddFont(ttfData);

            SpriteFontBase largeFont = fontSystem.GetFont(80);
            SpriteFontBase regularFont = fontSystem.GetFont(28);

            var mainCard = new Panel
            {
                Width = 680,
                Height = 550,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Background = CreateRoundedTexture(680, 550, 25, new Color(35, 35, 40))
            };

            var contentStack = new VerticalStackPanel
            {
                Spacing = 30,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Padding = new Thickness(40)
            };

            var welcomeLabel = new Label
            {
                Text = "Каменный век",
                HorizontalAlignment = HorizontalAlignment.Center,
                TextColor = Color.White,
                Font = largeFont
            };
            contentStack.Widgets.Add(welcomeLabel);

            _statusLabel = new Label
            {
                Text = "Вы не подключены к игре. Создайте новую игру.", 
                TextColor = Color.LightCoral,
                Wrap = true,
                Width = 480,
                HorizontalAlignment = HorizontalAlignment.Center,
                Font = regularFont
            };
            contentStack.Widgets.Add(_statusLabel);

            _codeInput = new TextBox
            {
                Text = "",
                Width = 480,
                Height = 50,
                HorizontalAlignment = HorizontalAlignment.Center,
                Font = regularFont,
                TextColor = Color.White,
                Padding = new Thickness(15, 10),
                Background = CreateRoundedTexture(480, 50, 10, new Color(20, 20, 25))
            };
            contentStack.Widgets.Add(_codeInput);

            var buttonsPanel = new HorizontalStackPanel
            {
                Spacing = 20,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            _connectButtonLabel = new Label
            {
                Text = "Connect",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextColor = Color.White,
                Font = regularFont
            };

            _connectButton = new Button { Content = _connectButtonLabel };
            StyleModernButton(_connectButton, 230, 85, new Color(46, 46, 46), new Color(30, 30, 30), new Color(20, 20, 20));
            _connectButton.Click += (s, a) => OnConnectClicked();
            buttonsPanel.Widgets.Add(_connectButton);

            var exitButtonLabel = new Label
            {
                Text = "Exit", 
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                TextColor = Color.White,
                Font = regularFont
            };
            var exitButton = new Button { Content = exitButtonLabel };
            StyleModernButton(exitButton, 230, 85, new Color(100, 100, 100), new Color(80, 80, 80), new Color(70, 70, 70));
            exitButton.Click += (s, a) => Exit();
            buttonsPanel.Widgets.Add(exitButton);

            contentStack.Widgets.Add(buttonsPanel);
            mainCard.Widgets.Add(contentStack);

            _desktop = new Desktop
            {
                Root = mainCard
            };
        }

        private void StyleModernButton(Button button, int width, int height, Color baseColor, Color hoverColor, Color pressedColor)
        {
            button.Width = width;
            button.Height = height;
            button.Padding = new Thickness(0);
            button.Background = CreateRoundedTexture(width, height, 10, baseColor);
            button.OverBackground = CreateRoundedTexture(width, height, 10, hoverColor);
            button.PressedBackground = CreateRoundedTexture(width, height, 10, pressedColor);
        }

        private TextureRegion CreateRoundedTexture(int width, int height, int radius, Color color)
        {
            Texture2D texture = new Texture2D(GraphicsDevice, width, height);
            Color[] data = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    int dx = Math.Max(0, Math.Max(radius - x, x - (width - radius - 1)));
                    int dy = Math.Max(0, Math.Max(radius - y, y - (height - radius - 1)));

                    if (dx * dx + dy * dy <= radius * radius)
                    {
                        data[y * width + x] = color;
                    }
                    else
                    {
                        data[y * width + x] = Color.Transparent;
                    }
                }
            }
            texture.SetData(data);
            return new TextureRegion(texture);
        }

        private void OnConnectClicked()
        {
            if (!_isConnected)
            {
                string inputCode = _codeInput.Text;
                _connectButtonLabel.Text = "Connecting...";
                _connectButton.Enabled = false;
                TryConnectAsync(inputCode);
            }
            else
            {
                Disconnect();
            }
        }

        private async void TryConnectAsync(string inputCode)
        {
            await Task.Delay(1000);

            _isConnected = true;
            int playersCount = 1;

            _statusLabel.Text = $"Вы подключены, код игры: {inputCode}, количество игроков: {playersCount}";
            _statusLabel.TextColor = Color.LightGreen;

            _connectButtonLabel.Text = "Disconnect";
            _connectButton.Enabled = true;
        }

        private void Disconnect()
        {
            _isConnected = false;
            _statusLabel.Text = "Вы не подключены к игре. Создайте новую игру Введите URL сервера.";
            _statusLabel.TextColor = Color.LightCoral;

            _connectButtonLabel.Text = "Connect";
        }

        protected override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
        }

        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(new Color(25, 25, 25));
            _desktop.Render();
            base.Draw(gameTime);
        }
    }
}