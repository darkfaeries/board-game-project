using Microsoft.Xna.Framework.Input;
using Myra.Events;
using Myra.Graphics2D.UI;

namespace GameClient;

internal sealed class SessionCodeInput : TextBox
{
    public const int CodeLength = 6;

    public override void OnChar(char c)
    {
        if (!char.IsAsciiHexDigit(c) || Text.Length - Math.Abs(SelectEnd - SelectStart) >= CodeLength)
            return;
        base.OnChar(char.ToUpperInvariant(c));
    }

    public override void OnKeyDown(Keys key)
    {
        bool shortcut = Desktop.IsKeyDown(Keys.LeftControl) || Desktop.IsKeyDown(Keys.RightControl);
        bool shift = Desktop.IsKeyDown(Keys.LeftShift) || Desktop.IsKeyDown(Keys.RightShift);
        if (shortcut)
        {
            int start = Math.Min(SelectStart, SelectEnd);
            int length = Math.Abs(SelectEnd - SelectStart);
            switch (key)
            {
                case Keys.C:
                    string copy = length == 0 ? Text : Text.Substring(start, length);
                    if (copy.Length > 0) SessionClipboard.TryCopy(copy);
                    return;
                case Keys.X:
                    if (length > 0 && SessionClipboard.TryCopy(Text.Substring(start, length)))
                        base.OnKeyDown(Keys.Delete);
                    return;
                case Keys.V:
                    if (!shift) PasteCode(SessionClipboard.Read());
                    return;
                case Keys.Z:
                case Keys.Y:
                    UndoOrRedo(key == Keys.Y || shift);
                    return;
                case Keys.Left:
                case Keys.Right:
                    base.OnKeyDown(key == Keys.Left ? Keys.Home : Keys.End);
                    return;
                case Keys.D:
                    // Myra's duplicate-line shortcut bypasses the room-code length limit.
                    return;
            }
        }
        base.OnKeyDown(key);
    }

    public void SetRoomCode(string code)
    {
        ReplaceAll(Normalize(code, CodeLength));
        CursorPosition = Text.Length;
        SelectAll();
    }

    private static string Normalize(string text, int limit) =>
        new(text.Where(char.IsAsciiHexDigit).Take(limit).Select(char.ToUpperInvariant).ToArray());

    private void UndoOrRedo(bool redo)
    {
        // Myra restores a replacement in two steps. Keep the caret valid before it resets the selection.
        int caret = CursorPosition;
        void FitCaret(object sender, ValueChangedEventArgs<string> change) =>
            CursorPosition = Math.Min(caret, change.NewValue.Length);

        TextChanged += FitCaret;
        try { base.OnKeyDown(redo ? Keys.Y : Keys.Z); }
        finally { TextChanged -= FitCaret; }
    }

    private void PasteCode(string text)
    {
        int length = Math.Abs(SelectEnd - SelectStart);
        int start = length == 0 ? CursorPosition : Math.Min(SelectStart, SelectEnd);
        int available = CodeLength - Text.Length + length;
        string code = Normalize(text, available);
        if (code.Length == 0) return;

        Replace(start, length, code);
        // Replace records a single undo step. A forward movement also collapses Myra's selection.
        CursorPosition = start + code.Length - 1;
        base.OnKeyDown(Keys.Right);
    }
}
