using Microsoft.Xna.Framework;

namespace GameClient;

public partial class MapScreen
{
    private static readonly Rectangle IncomingTradePanel = new(326, 243, 832, 574);
    private static readonly Rectangle AcceptTradeButton = new(391, 718, 344, 63);
    private static readonly Rectangle DeclineTradeButton = new(749, 718, 344, 63);
    private static readonly Color TradeGold = new(215, 169, 105);

    private bool CanAcceptTrade(PendingTradeSnapshot trade) =>
        _snapshot?.You is { } me && trade.Request.All(item => me.Resource(item.Key) >= item.Value);

    private void HandleIncomingTradeClick(Point point)
    {
        if (Busy || _snapshot?.PendingTrade is not { } trade) return;

        if (AcceptTradeButton.Contains(point) && CanAcceptTrade(trade))
            TradeDecisionRequested?.Invoke(trade.SourcePlayerId, trade.TradeId, true);
        else if (DeclineTradeButton.Contains(point))
            TradeDecisionRequested?.Invoke(trade.SourcePlayerId, trade.TradeId, false);
    }

    private void DrawIncomingTradePanel()
    {
        if (_snapshot?.PendingTrade is not { } trade) return;
        var panel = IncomingTradePanel;
        var mouse = HudMouse;

        _spriteBatch.Draw(_pixel, DesignArea, Color.Black * .42f);
        Art("action-dialog-frame", panel);
        CenteredLabel("Предложение обмена", new Rectangle(panel.X + 64, 279, panel.Width - 128, 51), 46);
        _spriteBatch.Draw(_pixel, new Rectangle(panel.Center.X - 100, 336, 200, 2), TradeGold * .65f);

        var source = _snapshot.Players.FirstOrDefault(p => p.Id == trade.SourcePlayerId);
        var senderCard = new Rectangle(panel.Center.X - 208, 353, 416, 73);
        Art("dock", senderCard);
        var senderLabel = new Rectangle(senderCard.X + 66, senderCard.Y + 12, senderCard.Width - 132, 49);
        if (source is not null)
        {
            Art(Identity(source.Color).Portrait, new Rectangle(senderCard.X + 55, senderCard.Y + 11, 51, 51));
            senderLabel.X += 52;
            senderLabel.Width -= 52;
        }
        CenteredLabel(source?.DisplayName ?? "Игрок", senderLabel, 31, Ivory, wrap: true);

        DrawIncomingTradeResources(new Rectangle(391, 448, 344, 218), "Вы получаете", trade.Offer,
            new Color(185, 217, 165));
        DrawIncomingTradeResources(new Rectangle(749, 448, 344, 218), "Вы отдаёте", trade.Request,
            new Color(241, 182, 146));

        bool canAfford = CanAcceptTrade(trade);
        if (!canAfford)
            CenteredLabel("Не хватает ресурсов для обмена", new Rectangle(391, 672, 702, 37), 23,
                new Color(244, 157, 138));

        DrawTradeControl(canAfford && !Busy ? "barter-gold" : "barter-stone",
            AcceptTradeButton, "Принять", 34, canAfford && !Busy, mouse);
        DrawTradeControl("barter-stone", DeclineTradeButton, "Отклонить", 34, !Busy, mouse);
    }

    private void DrawIncomingTradeResources(Rectangle box, string title,
        Dictionary<ResourceType, int> amounts, Color headingColor)
    {
        _spriteBatch.Draw(_pixel, box, Color.Black * .34f);
        _spriteBatch.Draw(_pixel, new Rectangle(box.X, box.Y, box.Width, 2), TradeGold * .65f);
        _spriteBatch.Draw(_pixel, new Rectangle(box.X, box.Bottom - 1, box.Width, 1), TradeGold * .40f);
        CenteredLabel(title, new Rectangle(box.X, box.Y + 4, box.Width, 36), 28, headingColor);

        var resources = Resources.Where(resource => amounts.GetValueOrDefault(resource.Type) > 0).ToArray();
        if (resources.Length == 0)
        {
            CenteredLabel("Нет ресурсов", new Rectangle(box.X, box.Y + 44, box.Width, box.Height - 44),
                25, Color.LightGray);
            return;
        }

        const int rowHeight = 41;
        int y = box.Y + 44 + (box.Height - 44 - resources.Length * rowHeight) / 2;
        foreach (var (resource, icon) in resources)
        {
            Art(icon, new Rectangle(box.X + 20, y + 2, 37, 37));
            Label(ResourceName(resource), new Rectangle(box.X + 69, y, 166, rowHeight), 25);
            CenteredLabel($"×{amounts[resource]}", new Rectangle(box.Right - 103, y, 85, rowHeight), 26);
            y += rowHeight;
        }
    }
}
