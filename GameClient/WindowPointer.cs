using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Runtime.InteropServices;

namespace GameClient;

internal static class WindowPointer
{
    // DesktopGL uses SDL2. On macOS its global mouse position and window origin
    // can disagree, especially across fullscreen transitions. Read the event
    // position relative to the focused SDL window before converting to pixels.
    private const string MacSdl = "libSDL2-2.0.0.dylib";

    [DllImport(MacSdl)]
    private static extern IntPtr SDL_GetMouseFocus();

    [DllImport(MacSdl)]
    private static extern uint SDL_GetMouseState(out int x, out int y);

    [DllImport(MacSdl)]
    private static extern void SDL_GetWindowSize(IntPtr window, out int width, out int height);

    public static Point InViewport(GraphicsDevice device)
    {
        var viewport = device.Viewport;
        if (!OperatingSystem.IsMacOS())
            return Mouse.GetState().Position - viewport.Bounds.Location;

        var surface = device.PresentationParameters;
        var window = surface.DeviceWindowHandle;
        if (window == IntPtr.Zero || SDL_GetMouseFocus() != window)
            return new Point(-1, -1);

        SDL_GetWindowSize(window, out int width, out int height);
        if (width <= 0 || height <= 0) return new Point(-1, -1);

        SDL_GetMouseState(out int x, out int y);
        return new Point((int)Math.Floor((double)x * surface.BackBufferWidth / width) - viewport.X,
                         (int)Math.Floor((double)y * surface.BackBufferHeight / height) - viewport.Y);
    }
}
