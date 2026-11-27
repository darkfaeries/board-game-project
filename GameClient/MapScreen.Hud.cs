using FontStashSharp;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace GameClient;

public partial class MapScreen
{
    // Coordinates are in the same 1920 × 1080 space as the Penpot board.
    private static readonly Rectangle DesignArea = new(0, 0, 1920, 1080);
    private static readonly Rectangle MapArea = new(111, 148, 1262, 767);
    private static readonly Rectangle Sidebar = new(1470, 120, 426, 936);
    private static readonly Rectangle MenuButton = new(1748, 30, 145, 64);
    private static readonly Rectangle ExploreButton = new(1503, 610, 360, 84);
    private static readonly Rectangle CampButton = new(1503, 708, 360, 84);
    private static readonly Rectangle TradeButton = new(1503, 806, 360, 84);
    private static readonly Rectangle EndTurnButton = new(1503, 940, 360, 84);
    private static readonly Color Ivory = new(245, 228, 191);
    private static readonly (ResourceType Type, string Icon)[] Resources =
    {
        (ResourceType.Meat, "meat"), (ResourceType.Bone, "bone"),
        (ResourceType.Flint, "flint"), (ResourceType.Hides, "hide")
    };
    private static readonly string[] ProgressIcons = { "meat", "spear", "camp", "hide" };
    private static readonly string ArtDirectory = System.IO.Path.Combine(AppContext.BaseDirectory, "assets", "painted");
    private readonly Dictionary<string, Texture2D> _art = new();
    private bool _exploreOpen;
    private bool _tradeOpen;

    private Matrix HudToScreen
    {
        get
        {
            var viewport = _graphicsDevice.Viewport;
            float scale = Math.Min(viewport.Width / 1920f, viewport.Height / 1080f);
            return Matrix.CreateScale(scale, scale, 1) * Matrix.CreateTranslation(
                (viewport.Width - 1920 * scale) / 2, (viewport.Height - 1080 * scale) / 2, 0);
        }
    }

    private Matrix BoardToHud
    {
        get
        {
            var size = _boardMax - _boardMin;
            float scale = Math.Min(MapArea.Width / size.X, MapArea.Height / size.Y);
            var offset = MapArea.Center.ToVector2() - (_boardMax + _boardMin) * (scale / 2);
            return Matrix.CreateScale(scale, scale, 1) * Matrix.CreateTranslation(offset.X, offset.Y, 0);
        }
    }

    private Point ToHud(Point point) => Vector2.Transform(point.ToVector2(), Matrix.Invert(HudToScreen)).ToPoint();
    private Point HudMouse => ToHud(WindowPointer.InViewport(_graphicsDevice));
    private static Rectangle ExplorerOption(int index) => new(1180, 615 + index * 70, 280, 64);

    private void LoadPaintedArt()
    {
        _art.Add("barter-panel", Texture2D.FromFile(_graphicsDevice, System.IO.Path.Combine(ArtDirectory, "barter-panel.jpg")));
        foreach (string path in Directory.EnumerateFiles(ArtDirectory, "*.png"))
        {
            string key = System.IO.Path.GetFileNameWithoutExtension(path);
            if (Enum.TryParse<TerrainType>(key, true, out _)) continue; // Loaded with mipmaps for the board.
            _art.Add(key, Texture2D.FromFile(_graphicsDevice, path, DefaultColorProcessors.PremultiplyAlpha));
        }
    }

    private void Art(string key, Rectangle bounds, Color? tint = null) =>
        _spriteBatch.Draw(_art[key], bounds, tint ?? Color.White);

    private void DrawPanel(Rectangle panel, int border = 10)
    {
        for (int row = 0; row < 3; row++)
        for (int col = 0; col < 3; col++)
        {
            int x = col == 2 ? panel.Right - border : panel.X + col * border;
            int y = row == 2 ? panel.Bottom - border : panel.Y + row * border;
            Art($"panel-{row}-{col}", new Rectangle(x, y,
                col == 1 ? panel.Width - 2 * border : border, row == 1 ? panel.Height - 2 * border : border));
        }
    }

    private void Label(string text, Rectangle box, int fontSize)
    {
        if (string.IsNullOrEmpty(text)) return;
        var font = _numberFontSystem.GetFont(fontSize * 1.18f);
        var size = font.MeasureString(text);
        float scale = Math.Min(1, Math.Min(box.Width / size.X, box.Height / size.Y));
        var pos = new Vector2(box.X,
            box.Y + (box.Height - size.Y * scale) / 2);
        _spriteBatch.DrawString(font, text, pos, Ivory, scale: new Vector2(scale));
    }

    private void CenteredLabel(string text, Rectangle box, int size, Color? color = null, bool wrap = false)
    {
        if (string.IsNullOrWhiteSpace(text)) return;
        var font = _numberFontSystem.GetFont(size * 1.18f);
        var ink = font.TextBounds(text, Vector2.Zero);
        float width = ink.X2 - ink.X;
        string? secondLine = null;
        Bounds secondInk = default;
        if (wrap && width > box.Width)
        {
            // Split near the middle at a word boundary. Keep unbroken names on one line.
            int middle = text.Length / 2;
            int left = text.LastIndexOf(' ', middle);
            int right = text.IndexOf(' ', middle);
            int split = left < 0 || (right >= 0 && right - middle < middle - left) ? right : left;
            if (split > 0 && split < text.Length - 1)
            {
                secondLine = text[(split + 1)..].TrimStart();
                text = text[..split].TrimEnd();
                ink = font.TextBounds(text, Vector2.Zero);
                secondInk = font.TextBounds(secondLine, new Vector2(0, font.LineHeight));
                width = Math.Max(ink.X2 - ink.X, secondInk.X2 - secondInk.X);
            }
        }

        float bottom = secondLine == null ? ink.Y2 : secondInk.Y2;
        float scale = Math.Min(1f, Math.Min(box.Width / Math.Max(1, width), box.Height / Math.Max(1, bottom - ink.Y)));
        var center = box.Location.ToVector2() + box.Size.ToVector2() * .5f;
        float y = center.Y - (ink.Y + bottom) * .5f * scale;
        void DrawLine(string line, Bounds bounds, float top)
        {
            var pos = new Vector2(center.X - (bounds.X + bounds.X2) * .5f * scale, top);
            _spriteBatch.DrawString(font, line, pos, color ?? Ivory, scale: new Vector2(scale));
        }
        DrawLine(text, ink, y);
        if (secondLine != null) DrawLine(secondLine, secondInk, y + font.LineHeight * scale);
    }

    private void DrawPaintedButton(Rectangle button, string title, bool enabled, bool highlight,
        string? icon = null, bool amber = false, int fontSize = 24)
    {
        var tint = enabled ? Color.White : new Color(152, 148, 142);
        if (enabled && highlight && Mouse.GetState().LeftButton == ButtonState.Pressed) tint = Color.LightGray;
        Art(amber ? "button-amber" : "button-slate", button, tint);
        if (enabled && highlight) _spriteBatch.Draw(_pixel, button, new Color(255, 194, 99) * 0.12f);
        var box = button;
        box.Inflate(-Math.Max(3, button.Width / 12), -Math.Max(2, button.Height / 8));
        if (icon != null)
        {
            Art(icon, new Rectangle(button.X + 42, button.Y + 17, 50, 50), tint);
            box.X = button.X + 105;
            box.Width = button.Width - 130;
        }
        CenteredLabel(title, box, fontSize, amber ? new Color(52, 32, 15) : enabled ? Ivory : Color.Silver);
    }

    private static (string Portrait, string Tribe) Identity(PlayerColor color) => color switch
    {
        PlayerColor.Blue => ("portrait-blue", "Синее"),
        PlayerColor.White => ("portrait-green", "Зелёное"),
        PlayerColor.Orange => ("portrait-yellow", "Жёлтое"),
        _ => ("portrait-red", "Красное"),
    };

    private static string Score(int? points) => points switch
    {
        null => "—",
        1 => "1 очко",
        2 or 3 or 4 => $"{points} очка",
        _ => $"{points} очков",
    };

    private void DrawHud()
    {
        if (_snapshot is not { } game) return;
        _spriteBatch.Begin(transformMatrix: HudToScreen, samplerState: SamplerState.LinearClamp);
        var mouse = HudMouse;
        Art("dock", new Rectangle(22, 18, 450, 88));
        string turn = game.IsMyTurn ? "Ваш ход" : game.CurrentPlayer?.DisplayName ?? "Ожидание";
        Label(turn, new Rectangle(76, 31, 216, 52), 40);
        Label(game.TurnNumber is int n ? $"Ход {n}" : PhaseName(game.Phase), new Rectangle(306, 39, 125, 43), 30);
        PlayerSnapshot? hoveredPlayer = null;
        int hoveredCardCenter = 0;
        for (int i = 0; i < game.Players.Count; i++)
        {
            var player = game.Players[i];
            int x = 500 + i * 290;
            var card = new Rectangle(x, 12, 280, 96);
            Art("dock", card);
            var (portrait, tribe) = Identity(player.Color);
            Art(portrait, new Rectangle(x + 10, 19, 80, 80));
            Label(tribe + "\nплемя", new Rectangle(x + 110, 26, 118, 50), 20);
            Label(Score(player.VictoryPoints), new Rectangle(x + 172, 62, 80, 27), 20);
            if (card.Contains(mouse))
            {
                hoveredPlayer = player;
                hoveredCardCenter = card.Center.X;
            }
        }
        DrawPaintedButton(MenuButton, "Меню", true, MenuButton.Contains(mouse));
        DrawPanel(Sidebar, 36);
        CenteredLabel("Ваше племя", new Rectangle(1510, 151, 346, 48), 38);
        if (game.You is { } me) DrawTribe(me, mouse);
        DrawPaintedButton(ExploreButton, CanConfirmMove ? $"Переместить ({_selectedRoute.Count - 1})" : "Исследовать",
            CanBuild, _exploreOpen || ExploreButton.Contains(mouse), "explore", fontSize: 32);
        DrawPaintedButton(CampButton, "Лагерь", CanBuild, _boardActionMode == BoardActionMode.ConvertToCamp || CampButton.Contains(mouse), "camp", fontSize: 32);
        DrawPaintedButton(TradeButton, "Обмен", CanTrade, _tradeOpen || TradeButton.Contains(mouse), "trade", fontSize: 32);
        bool rollPhase = game.Status == GameStatus.Playing && game.Phase == TurnPhase.Roll && !game.RollUsed;
        DrawPaintedButton(EndTurnButton, rollPhase ? "Бросить кубики" : EndTurnLabel,
            AvailableTurnAction is not null, EndTurnButton.Contains(mouse), amber: true, fontSize: 30);

        if (_exploreOpen && CanBuild)
        {
            DrawButton(ExplorerOption(0), "Создать исследователя", true, ExplorerOption(0).Contains(mouse));
            DrawButton(ExplorerOption(1), "Переместить", true, ExplorerOption(1).Contains(mouse));
        }
        if (game.Status == GameStatus.Setup) DrawSetupInstructions();
        else if (_message == null)
        {
            string status = $"Фаза: {PhaseName(game.Phase)}";
            if (game.LastRoll is int roll) status += $" · Выпало {roll}";
            else if (game.RollUsed) status += " · Кубики брошены";
            Label(status, new Rectangle(800, 975, 620, 44), 26);
        }
        if (new Rectangle(22, 18, 450, 88).Contains(mouse))
            CenteredLabel($"{game.You?.DisplayName} · Код игры {game.GameCode}", new Rectangle(28, 102, 450, 38), 23);
        if (hoveredPlayer != null) DrawPlayerDetails(hoveredPlayer, hoveredCardCenter);
        if (_tradeOpen && game.Phase == TurnPhase.Trade && game.IsMyTurn) DrawTradePanel();
        if (HasPendingDiscards)
        {
            if (HasPendingDiscard) DrawDiscardPanel();
            else DrawPendingWaitPanel();
        }
        if (HasIncomingTradeForMe) DrawIncomingTradePanel();
        DrawMessage();
        if (IsGameOver) DrawVictoryOverlay();
        _spriteBatch.End();
    }

    private void DrawTribe(PlayerSnapshot me, Point mouse)
    {
        var (portrait, tribe) = Identity(me.Color);
        Art(portrait, new Rectangle(1528, 219, 94, 94));
        Label(tribe + "\nплемя", new Rectangle(1652, 226, 192, 80), 32);
        _spriteBatch.Draw(_pixel, new Rectangle(1510, 344, 346, 2), new Color(149, 108, 71));
        Label("Развитие племени", new Rectangle(1510, 362, 346, 38), 30);
        for (int i = 0; i < ProgressTypes.Length; i++)
        {
            var row = ProgressRowBounds(i);
            var type = ProgressTypes[i];
            int level = me.ProgressLevel(type);
            bool atMaxLevel = level >= GameConstants.MaxProgressLevel;
            bool canUpgrade = CanBuild && !HasPendingDiscards && !HasPendingTrade && !atMaxLevel &&
                              HasResources(me, ProgressCost(level + 1));
            if (row.Contains(mouse))
            {
                _spriteBatch.Draw(_pixel, row, Color.Wheat * 0.12f);
                DrawTip(ProgressName(type) + " · " + (atMaxLevel ? "Максимальный уровень" :
                    $"Следующий уровень: {ProgressCostLabel(ProgressCost(level + 1))}"), row.Y);
            }
            Art(ProgressIcons[i], new Rectangle(row.X, row.Y + 4, 34, 34));
            Label(ProgressName(type), new Rectangle(row.X + 46, row.Y, 112, 42), 26);
            for (int j = 0; j < GameConstants.MaxProgressLevel; j++)
            {
                var cell = new Rectangle(row.X + 164 + 28 * j, row.Y + 12, 23, 22);
                _spriteBatch.Draw(_pixel, cell, new Color(143, 108, 71));
                cell.Inflate(-1, -1);
                _spriteBatch.Draw(_pixel, cell, j < level ? new Color(233, 163, 67) : new Color(39, 43, 43));
            }
            var upgradeButton = new Rectangle(row.Right - 39, row.Y + 3, 37, 37);
            DrawTradeControl("barter-plus", upgradeButton, "", 0, canUpgrade, mouse);
        }
        Art("dock", new Rectangle(28, 958, 740, 104));
        for (int i = 0; i < Resources.Length; i++)
        {
            var (resource, icon) = Resources[i];
            int x = 104 + i * 160;
            Art(icon, new Rectangle(x, 981, 58, 58));
            Label(me.Resource(resource).ToString(), new Rectangle(x + 76, 988, 68, 45), 36);
            if (new Rectangle(x, 978, 140, 66).Contains(mouse))
                CenteredLabel(ResourceName(resource), new Rectangle(x - 24, 928, 155, 32), 24);
        }
    }

    private void DrawTip(string text, int y)
    {
        var bounds = new Rectangle(985, y, 470, 54);
        DrawPanel(bounds);
        bounds.Inflate(-14, -6);
        CenteredLabel(text, bounds, 22);
    }

    private void DrawPlayerDetails(PlayerSnapshot player, int cardCenter)
    {
        const int width = 576;
        int x = Math.Clamp(cardCenter - width / 2, 20, DesignArea.Right - width - 20);
        var panel = new Rectangle(x, 115, width, 216);
        Art("action-dialog-frame", panel);

        Art(Identity(player.Color).Portrait, new Rectangle(panel.X + 41, panel.Y + 24, 50, 50));
        CenteredLabel(player.DisplayName, new Rectangle(panel.X + 108, panel.Y + 21, 411, 58), 29, wrap: true);
        _spriteBatch.Draw(_pixel, new Rectangle(panel.X + 39, panel.Y + 84, width - 78, 1), Color.Wheat * .50f);

        const int cellWidth = 124;
        for (int i = 0; i < Resources.Length; i++)
        {
            var (resource, icon) = Resources[i];
            int cellX = panel.X + 40 + i * cellWidth;
            var cell = new Rectangle(cellX + 3, panel.Y + 94, cellWidth - 6, 63);
            _spriteBatch.Draw(_pixel, cell, Color.Black * .28f);
            Art(icon, new Rectangle(cellX + 15, panel.Y + 100, 31, 31));
            CenteredLabel(player.Resource(resource).ToString(),
                new Rectangle(cellX + 52, panel.Y + 100, 57, 31), 27);
            CenteredLabel(ResourceName(resource), new Rectangle(cell.X, panel.Y + 130, cell.Width, 25), 19);
        }

        CenteredLabel($"Лагеря {player.Camps.Count} · Исследователи {player.Explorers.Count} · Племена {player.TribesCollected}",
            new Rectangle(panel.X + 36, panel.Y + 165, width - 72, 29), 22);
    }
}
