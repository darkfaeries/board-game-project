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
        const int gap = 12;
        int width = Math.Min(562, (1136 - (count - 1) * gap) / count);
        int start = (1254 - count * width - (count - 1) * gap) / 2;
        return TradeRect(start + index * (width + gap), 319, width, 195);
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
        CenteredLabel("Торговля", TradeRect(436, 50, 382, 79), 64);
        foreach (var mode in Enum.GetValues<TradeMode>())
        {
            var tab = ModeTab(mode);
            DrawTradeControl(_tradeMode == mode ? "barter-gold" : "barter-stone", tab,
                mode == TradeMode.Player ? "С игроком" : "С банком", 42, !Busy && !HasPendingTrade, mouse);
        }

        CenteredLabel(bank ? "Обмен с банком" : "С кем обменяться:", TradeRect(400, 261, 454, 55), 30);
        if (bank)
        {
            CenteredLabel("Отдайте три разных вида ресурсов", TradeRect(130, 341, 994, 72), 34);
            CenteredLabel("Выберите один ресурс в колонке «Прошу»", TradeRect(130, 421, 994, 70), 28);
        }
        else
        {
            for (int i = 0; i < partners.Length; i++)
                DrawTradePartner(partners[i], PartnerCard(i, partners.Length), mouse);
        }

        CenteredLabel("Ресурс", TradeRect(62, 535, 328, 64), 31);
        CenteredLabel("В наличии", TradeRect(400, 535, 184, 64), 27);
        CenteredLabel("Отдаю", TradeRect(610, 535, 264, 64), 31, new Color(239, 170, 129));
        CenteredLabel("Прошу", TradeRect(918, 535, 264, 64), 31, new Color(181, 211, 163));
        for (int row = 0; row < Resources.Length; row++)
        {
            var (resource, icon) = Resources[row];
            int cy = TradeRowCenters[row];
            Art(icon, TradeRect(81, cy - 40, 80, 80));
            CenteredLabel(ResourceName(resource), TradeRect(172, cy - 38, 202, 76), 33);
            CenteredLabel((game.You?.Resource(resource) ?? 0).ToString(), TradeRect(434, cy - 38, 116, 76), 32);
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
        CenteredLabel(amounts.GetValueOrDefault(resource).ToString(), valueBox, 32);
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
        CenteredLabel(text, bounds, size, enabled ? Ivory : Color.LightGray);
    }

    private void DrawTradePartner(PlayerSnapshot player, Rectangle bounds, Point mouse)
    {
        bool selected = player.Id == _tradeTargetId;
        _spriteBatch.Draw(_art["backdrop"], bounds, new Rectangle(260, 0, 1350, 465), new Color(145, 138, 130));
        Art("barter-partner", bounds, selected ? new Color(255, 193, 91) : new Color(152, 150, 145));
        if (bounds.Contains(mouse)) _spriteBatch.Draw(_pixel, bounds, Color.Wheat * .10f);

        int inset = bounds.Height / 9;
        Rectangle portrait, name;
        if (bounds.Width < bounds.Height * 5 / 2)
        {
            int side = bounds.Height * 2 / 5;
            portrait = new Rectangle(bounds.Center.X - side / 2, bounds.Top + inset, side, side);
            name = new Rectangle(bounds.X + inset, portrait.Bottom + 3,
                bounds.Width - 2 * inset, bounds.Bottom - inset - portrait.Bottom - 3);
        }
        else
        {
            int side = bounds.Height - 2 * inset;
            portrait = new Rectangle(bounds.X + inset, bounds.Y + inset, side, side);
            name = new Rectangle(portrait.Right + inset, bounds.Y + inset,
                bounds.Right - portrait.Right - 2 * inset, bounds.Height - 2 * inset);
        }
        Art(Identity(player.Color).Portrait, portrait);
        CenteredLabel(player.Name.Trim(), name, 36, wrap: true);
    }
}
