using System.Runtime.InteropServices;

namespace GameClient;

internal static class SessionClipboard
{
    private const string Sdl = "SessionClipboardSDL";

    static SessionClipboard()
    {
        // Use the SDL2 binary shipped with MonoGame DesktopGL on each platform.
        NativeLibrary.SetDllImportResolver(typeof(SessionClipboard).Assembly, (name, assembly, path) =>
        {
            if (name != Sdl) return IntPtr.Zero;
            string library = OperatingSystem.IsMacOS() ? "libSDL2-2.0.0.dylib" :
                OperatingSystem.IsWindows() ? "SDL2.dll" : "libSDL2-2.0.so.0";
            return NativeLibrary.Load(library, assembly, path);
        });
    }

    [DllImport(Sdl, CallingConvention = CallingConvention.Cdecl)]
    private static extern int SDL_SetClipboardText([MarshalAs(UnmanagedType.LPUTF8Str)] string text);

    [DllImport(Sdl, CallingConvention = CallingConvention.Cdecl)]
    private static extern IntPtr SDL_GetClipboardText();

    [DllImport(Sdl, CallingConvention = CallingConvention.Cdecl)]
    private static extern void SDL_free(IntPtr pointer);

    public static bool TryCopy(string text) => SDL_SetClipboardText(text) == 0;

    public static string Read()
    {
        var pointer = SDL_GetClipboardText();
        try { return Marshal.PtrToStringUTF8(pointer) ?? string.Empty; }
        finally { SDL_free(pointer); }
    }
}
