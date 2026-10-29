namespace LHP2_Archi_Mod.Addresses;

public enum GameVersionKind
{
    Unknown,
    // Original 32-bit harry2.exe (Steam / GOG)
    Original32,
    // 64-bit harry2.exe of LEGO Harry Potter Collection, the build analysed for the port
    Collection64,
    // 64-bit harry2.exe of another build: signatures only, no fallback offsets
    Collection64Unknown,
}

// Detects the game version at startup from the pointer size and the PE header
public static class GameVersion
{
    // PE header of the analysed 64-bit build (harry2.exe, SHA-256 4eae0a0e...)
    private const uint Collection64TimeDateStamp = 0x66FA9E3B;
    private const uint Collection64SizeOfImage = 0x1CB3000;

    public static GameVersionKind Current { get; private set; } = GameVersionKind.Unknown;
    public static bool Is64 => IntPtr.Size == 8;
    public static uint TimeDateStamp { get; private set; }
    public static uint SizeOfImage { get; private set; }

    public static unsafe void Detect(nuint moduleBase)
    {
        byte* pe = (byte*)moduleBase;
        int lfanew = *(int*)(pe + 0x3C);
        TimeDateStamp = *(uint*)(pe + lfanew + 8);
        ushort magic = *(ushort*)(pe + lfanew + 24);
        SizeOfImage = *(uint*)(pe + lfanew + 24 + 56);

        if (!Is64)
        {
            // Steam and GOG 32-bit executables were always supported: no build check
            Current = GameVersionKind.Original32;
        }
        else if (magic != 0x20B)
        {
            Current = GameVersionKind.Unknown;
        }
        else if (TimeDateStamp == Collection64TimeDateStamp && SizeOfImage == Collection64SizeOfImage)
        {
            Current = GameVersionKind.Collection64;
        }
        else
        {
            Current = GameVersionKind.Collection64Unknown;
        }
    }

    public static string Describe() =>
        $"{Current} ({IntPtr.Size * 8}-bit pointers, TimeDateStamp 0x{TimeDateStamp:X8}, SizeOfImage 0x{SizeOfImage:X})";
}
