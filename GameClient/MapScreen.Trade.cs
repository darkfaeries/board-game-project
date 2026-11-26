using Microsoft.Xna.Framework;

namespace GameClient;

public partial class MapScreen
{
    // The artwork and controls share the 1254px coordinate space of the Penpot panel.
    private static Rectangle TradeRect(int x, int y, int w, int h)
    {
        const float scale = 2f / 3;
        return new Rectangle(322 + (int)MathF.Round(x * scale), 120 + (int)MathF.Round(y * scale),
            (int)MathF.Round(w * scale), (int)MathF.Round(h * scale));
    }

    private static Rectangle ModeTab(TradeMode mode) => TradeRect(mode == TradeMode.Player ? 142 : 629, 157, 486, 101);
    private static Rectangle PartnerCard(int index, int count)
    {
        int width = (1136 - (count - 1) * 12) / count;
        return TradeRect(59 + index * (width + 12), 319, width, 195);
    }

    private static readonly int[] TradeRowCenters = { 660, 761, 865, 971 };

    private static Rectangle AmountButton(int row, bool request, int delta)
    {
        int x = (request ? 920 : 613) + (delta > 0 ? 180 : 0);
        return TradeRect(x, TradeRowCenters[row] - 38, 78, 76);
    }

    private static Rectangle BankChoice(int row) => TradeRect(918, TradeRowCenters[row] - 38, 266, 76);
    private static readonly Rectangle SubmitButton = TradeRect(90, 1045, 1076, 139);

    private IEnumerable<PlayerSnapshot> TradePartners =>
        _snapshot!.Players.Where(p => p.Id != _snapshot.YouPlayerId);

    private void EnsureTradePartner()
    {
        if (_tradeTargetId is Guid id && TradePartners.Any(p => p.Id == id)) return;
        _tradeTargetId = TradePartners.FirstOrDefault()?.Id;
        _tradeRequest.Clear();
    }

    private bool CanSubmitOffer(PlayerSnapshot? target)
    {
        if (!CanTrade || HasPendingTrade || _snapshot?.You is not { } me ||
            _tradeOffer.Count == 0 || _tradeOffer.Any(p => p.Value > me.Resource(p.Key))) return false;
        if (_tradeMode == TradeMode.Bank) return _tradeOffer.Count == 3;
        return target != null && _tradeRequest.Count > 0 &&
            _tradeRequest.All(p => p.Value <= target.Resource(p.Key) && !_tradeOffer.ContainsKey(p.Key));
    }

    private bool CanChangeAmount(ResourceType resource, bool request, int delta, PlayerSnapshot? target)
    {
        if (Busy || HasPendingTrade) return false;
        var amounts = request ? _tradeRequest : _tradeOffer;
        int value = amounts.GetValueOrDefault(resource);
        if (delta < 0) return value > 0;
        if (request) return !_tradeOffer.ContainsKey(resource) && value < (target?.Resource(resource) ?? 0);
        if (_tradeMode == TradeMode.Player && _tradeRequest.ContainsKey(resource)) return false;
        if (_tradeMode == TradeMode.Bank && value == 0 && _tradeOffer.Count == 3) return false;
        return value < (_snapshot?.You?.Resource(resource) ?? 0);
    }

    private bool HandleTradeClick(Point point)
    {
        if (!TradeRect(0, 0, 1254, 1254).Contains(point)) return false;
        if (HasPendingTrade) return true;

        foreach (var mode in Enum.GetValues<TradeMode>())
        {
            if (!ModeTab(mode).Contains(point)) continue;
            if (_tradeMode != mode)
            {
                _tradeMode = mode;
                _tradeOffer.Clear();
                _tradeRequest.Clear();
            }
            return true;
        }

        var partners = TradePartners.ToArray();
        if (_tradeMode == TradeMode.Player)
        {
            for (int i = 0; i < partners.Length; i++)
            {
                if (!PartnerCard(i, partners.Length).Contains(point)) continue;
                if (_tradeTargetId != partners[i].Id) _tradeRequest.Clear();
                _tradeTargetId = partners[i].Id;
                return true;
            }
        }
        var target = partners.FirstOrDefault(p => p.Id == _tradeTargetId);
        for (int row = 0; row < Resources.Length; row++)
        {
            var resource = Resources[row].Type;
            if (_tradeMode == TradeMode.Bank && BankChoice(row).Contains(point))
            {
                _bankResourceToGet = resource;
                return true;
            }
            for (int column = 0; column < (_tradeMode == TradeMode.Bank ? 1 : 2); column++)
            {
                bool request = column == 1;
                for (int delta = -1; delta <= 1; delta += 2)
                {
                    if (!AmountButton(row, request, delta).Contains(point)) continue;
                    if (CanChangeAmount(resource, request, delta, target))
                        AdjustTradeAmount(request ? _tradeRequest : _tradeOffer, resource, delta,
                            (request ? target : _snapshot!.You)?.Resource(resource) ?? 0);
                    return true;
                }
            }
        }
        if (SubmitButton.Contains(point) && CanSubmitOffer(target)) SubmitTrade();
        return true;
    }

    private void DrawTradePanel()
    {
        var game = _snapshot!;
        var partners = TradePartners.ToArray();
        var target = partners.FirstOrDefault(p => p.Id == _tradeTargetId);
        bool bank = _tradeMode == TradeMode.Bank;
        var mouse = HudMouse;
        Art("barter-panel", TradeRect(0, 0, 1254, 1254));
        foreach (var mode in Enum.GetValues<TradeMode>())
        {
            var tab = ModeTab(mode);
            DrawTradeControl(_tradeMode == mode ? "barter-gold" : "barter-stone", tab,
                mode == TradeMode.Player ? "С игроком" : "С банком", 42, !Busy && !HasPendingTrade, mouse);
        }

        Label(bank ? "Обмен с банком" : "С кем обменяться:", TradeRect(348, 261, 565, 55), 30, true);
        if (bank)
        {
            Label("Отдайте три разных вида ресурсов", TradeRect(130, 332, 994, 72), 34, true);
            Label("Выберите один ресурс в колонке «Прошу»", TradeRect(130, 409, 994, 70), 28, true);
        }
        else
        {
            for (int i = 0; i < partners.Length; i++)
                DrawTradePartner(partners[i], PartnerCard(i, partners.Length), mouse);
        }

        for (int row = 0; row < Resources.Length; row++)
        {
            var resource = Resources[row].Type;
            int cy = TradeRowCenters[row];
            Label((game.You?.Resource(resource) ?? 0).ToString(), TradeRect(434, cy - 38, 116, 76), 32, true);
            DrawTradeAmount(row, resource, false, target, mouse);
            if (bank)
            {
                bool selected = resource == _bankResourceToGet;
                DrawTradeControl(selected ? "barter-gold" : "barter-stone", BankChoice(row),
                    selected ? "Получить 1" : "Выбрать", 21, !Busy, mouse);
            }
            else DrawTradeAmount(row, resource, true, target, mouse);
        }

        string action = bank ? "Обменять" : "Предложить обмен";
        if (game.PendingTrade is { } waiting)
            action = $"Ждём ответа от {partners.FirstOrDefault(p => p.Id == waiting.TargetPlayerId)?.Name ?? "игрока"}";
        DrawTradeControl("barter-submit", SubmitButton, action, 49, CanSubmitOffer(target), mouse);
    }

    private void DrawTradeAmount(int row, ResourceType resource, bool request, PlayerSnapshot? target, Point mouse)
    {
        var amounts = request ? _tradeRequest : _tradeOffer;
        var valueBox = TradeRect(request ? 1003 : 695, TradeRowCenters[row] - 38, 94, 76);
        Label(amounts.GetValueOrDefault(resource).ToString(), valueBox, 32, true);
        for (int delta = -1; delta <= 1; delta += 2)
            DrawTradeControl(delta < 0 ? "barter-minus" : "barter-plus", AmountButton(row, request, delta),
                "", 0, CanChangeAmount(resource, request, delta, target), mouse);
    }

    private void DrawTradeControl(string skin, Rectangle bounds, string text, int size, bool enabled, Point mouse)
    {
        Art(skin, bounds, enabled ? Color.White : new Color(157, 151, 144));
        if (enabled && bounds.Contains(mouse))
            _spriteBatch.Draw(_pixel, bounds, Color.Wheat * 0.12f);
        if (text.Length == 0) return;
        bounds.Inflate(-bounds.Width / 12, -bounds.Height / 8);
        Label(text, bounds, size, true, enabled ? Ivory : Color.LightGray);
    }

    private void DrawTradePartner(PlayerSnapshot player, Rectangle bounds, Point mouse)
    {
        bool blue = player.Color == PlayerColor.Blue;
        bool green = player.Color == PlayerColor.White;
        Art(blue ? "barter-blue" : "barter-green", bounds);
        if (!blue && !green)
        {
            var portrait = new Rectangle(bounds.X + 4, bounds.Y + 7, bounds.Width * 43 / 100, bounds.Height - 14);
            Art(Identity(player.Color).Portrait, portrait);
        }
        var name = new Rectangle(bounds.X + bounds.Width * 43 / 100, bounds.Y + bounds.Height / 7,
            bounds.Width * 54 / 100, bounds.Height / 3);
        string caption = player.Name;
        if (caption.Length > 16)
        {
            int middle = caption.Length / 2;
            int space = caption.LastIndexOf(' ', middle);
            int split = space > middle / 2 ? space : middle;
            caption = caption[..split] + "\n" + caption[split..].TrimStart();
            name.Height = bounds.Height * 2 / 3;
        }
        Label(caption, name, 36, true);
        if (player.Id == _tradeTargetId)
        {
            var edge = new Color(244, 181, 85);
            var r = bounds;
            r.Inflate(-4, -5);
            _spriteBatch.Draw(_pixel, new Rectangle(r.X, r.Y, r.Width, 2), edge);
            _spriteBatch.Draw(_pixel, new Rectangle(r.X, r.Bottom - 2, r.Width, 2), edge);
            _spriteBatch.Draw(_pixel, new Rectangle(r.X, r.Y, 2, r.Height), edge);
            _spriteBatch.Draw(_pixel, new Rectangle(r.Right - 2, r.Y, 2, r.Height), edge);
        }
        else if (bounds.Contains(mouse)) _spriteBatch.Draw(_pixel, bounds, Color.Wheat * 0.12f);
    }
}
