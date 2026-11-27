using Microsoft.Xna.Framework;

namespace GameClient;

public partial class MapScreen
{
    private static readonly Rectangle DiscardPanel = new(352, 283, 780, 496);
    private static readonly Rectangle SubmitDiscardButton = new(438, 670, 608, 63);
    private static readonly (int Delta, string Caption)[] DiscardControls =
    {
        (-10, "−10"), (-1, "−"), (1, "+"), (10, "+10")
    };

    private static Rectangle DiscardRow(int index) => new(413, 456 + index * 51, 658, 46);

    private static Rectangle DiscardControl(Rectangle row, int index) =>
        new(row.Right - 234 + 56 * index, row.Y + 3, 50, 40);

    private bool CanSubmitDiscard(int required, PlayerSnapshot player) =>
        !Busy && _discardSelection.Values.Sum() == required &&
        _discardSelection.All(item => item.Value <= player.Resource(item.Key));

    private void HandleDiscardClick(Point point)
    {
        if (_snapshot?.You is not { DiscardResourcesRequired: int required } player) return;

        int remaining = Math.Max(0, required - _discardSelection.Values.Sum());
        for (int row = 0; row < Resources.Length; row++)
        {
            var resource = Resources[row].Type;
            for (int index = 0; index < DiscardControls.Length; index++)
            {
                if (!DiscardControl(DiscardRow(row), index).Contains(point)) continue;

                int amount = _discardSelection.GetValueOrDefault(resource);
                int delta = DiscardControls[index].Delta;
                int available = Math.Min(remaining, Math.Max(0, player.Resource(resource) - amount));
                int next = delta > 0 ? amount + Math.Min(delta, available) : Math.Max(0, amount + delta);
                if (next == 0) _discardSelection.Remove(resource);
                else _discardSelection[resource] = next;
                return;
            }
        }

        if (SubmitDiscardButton.Contains(point) && CanSubmitDiscard(required, player))
        {
            DiscardResourcesRequested?.Invoke(new Dictionary<ResourceType, int>(_discardSelection));
            _discardSelection.Clear();
        }
    }

    private void DrawDiscardPanel()
    {
        if (_snapshot?.You is not { DiscardResourcesRequired: int required } player) return;
        var panel = DiscardPanel;
        var mouse = HudMouse;

        Art("action-dialog-frame", panel);
        CenteredLabel("Сброс ресурсов", new Rectangle(panel.X + 64, 324, 652, 48), 42);
        int selected = _discardSelection.Values.Sum();
        CenteredLabel($"Выбрано {selected} из {required}", new Rectangle(panel.X + 64, 375, 652, 34), 28,
            selected == required ? new Color(185, 217, 165) : new Color(240, 196, 112));

        var firstRow = DiscardRow(0);
        Label("Ресурс", new Rectangle(firstRow.X + 14, 415, 210, 34), 21);
        CenteredLabel("В запасе", new Rectangle(firstRow.X + 222, 415, 94, 34), 21);
        CenteredLabel("К сбросу", new Rectangle(firstRow.X + 320, 415, 94, 34), 21);
        _spriteBatch.Draw(_pixel, new Rectangle(firstRow.X, 449, firstRow.Width, 2), Color.Wheat * .50f);

        for (int i = 0; i < Resources.Length; i++)
        {
            var (resource, icon) = Resources[i];
            var row = DiscardRow(i);
            int amount = _discardSelection.GetValueOrDefault(resource);
            int stock = player.Resource(resource);

            _spriteBatch.Draw(_pixel, row, Color.Black * .28f);
            Art(icon, new Rectangle(row.X + 15, row.Y + 4, 38, 38), stock > 0 ? Color.White : Color.Gray);
            Label(ResourceName(resource), new Rectangle(row.X + 70, row.Y, 145, 46), 27);
            CenteredLabel(stock.ToString(), new Rectangle(row.X + 222, row.Y, 94, 46), 26);
            CenteredLabel(amount.ToString(), new Rectangle(row.X + 320, row.Y, 94, 46), 26,
                amount > 0 ? new Color(240, 196, 112) : null);
            for (int index = 0; index < DiscardControls.Length; index++)
            {
                var (delta, caption) = DiscardControls[index];
                bool enabled = !Busy && (delta < 0 ? amount > 0 : amount < stock && selected < required);
                DrawTradeControl("barter-stone", DiscardControl(row, index), caption, 24, enabled, mouse);
            }
        }

        bool canSubmit = CanSubmitDiscard(required, player);
        DrawTradeControl(canSubmit ? "barter-gold" : "barter-stone",
            SubmitDiscardButton, "Сбросить выбранное", 34, canSubmit, mouse);
    }

    private void DrawPendingWaitPanel()
    {
        if (_snapshot is null) return;

        var pendingPlayers = _snapshot.Players
            .Where(player => player.DiscardResourcesRequired is > 0)
            .ToArray();
        const int width = 736;
        const int rowHeight = 58;
        int height = 196 + pendingPlayers.Length * rowHeight;
        var center = MapArea.Center;
        var panel = new Rectangle(center.X - width / 2, center.Y - height / 2, width, height);
        Art("action-dialog-frame", panel);

        CenteredLabel("Ожидание сброса ресурсов", new Rectangle(panel.X + 56, panel.Y + 35, width - 112, 47), 39);
        _spriteBatch.Draw(_pixel, new Rectangle(center.X - 96, panel.Y + 98, 192, 2), Color.Wheat * .55f);

        int y = panel.Y + 116;
        foreach (var player in pendingPlayers)
        {
            var row = new Rectangle(panel.X + 68, y, width - 136, 48);
            _spriteBatch.Draw(_pixel, row, Color.Black * .28f);
            Art(Identity(player.Color).Portrait, new Rectangle(row.X + 13, row.Y + 5, 38, 38));
            Label(player.DisplayName, new Rectangle(row.X + 65, row.Y + 3, row.Width - 212, 42), 26);
            CenteredLabel($"Сбросить {player.DiscardResourcesRequired}",
                new Rectangle(row.Right - 143, row.Y, 132, 48), 23, new Color(240, 196, 112));
            y += rowHeight;
        }

        CenteredLabel("Игроки с более чем 7 картами сбрасывают половину",
            new Rectangle(panel.X + 56, y + 9, width - 112, 30), 24);
    }

}
