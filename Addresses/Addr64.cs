using System.Diagnostics;
using Reloaded.Memory.Sigscan;

namespace LHP2_Archi_Mod.Addresses;

internal enum SigKind
{
    // Address of a code site (hook, NOP, patch, jump target)
    Code,
    // Address of data referenced RIP-relative by the matched instruction
    Rip,
}

// One 64-bit AOB signature, generated from the 64-bit executable (Sigs64.g.cs)
internal readonly record struct SigEntry(SigKind Kind, string Name, uint Rva64, string Pattern, int SiteOffset,
                                         int DispOffset, int InsnSize, int Extra, int Occurrence);

// Resolves the 64-bit addresses by signature; known offsets are a fallback for Collection64 only
internal static class Addr64
{
    private static readonly Dictionary<string, nuint> Resolved = new();
    public static int Found { get; private set; }
    public static int Fallback { get; private set; }
    public static int Missing { get; private set; }

    public static unsafe void Resolve(nuint moduleBase)
    {
        using var process = Process.GetCurrentProcess();
        using var scanner = new Scanner(process, process.MainModule!);
        bool knownBuild = GameVersion.Current == GameVersionKind.Collection64;

        foreach (var e in Sigs64.Entries)
        {
            long match = FindOccurrence(scanner, e.Pattern, e.Occurrence);
            nuint address = 0;
            if (match >= 0)
            {
                nuint site = moduleBase + (nuint)(match + e.SiteOffset);
                address = e.Kind == SigKind.Code
                    ? site
                    : (nuint)((long)site + e.InsnSize + *(int*)(site + (nuint)e.DispOffset) + e.Extra);
                Found++;
                ulong rva = address - moduleBase;
                if (rva != e.Rva64)
                    Game.PrintToLog($"[Sig] {e.Name}: found at +{rva:X} (expected +{e.Rva64:X}) - different build?");
            }
            else if (knownBuild)
            {
                address = moduleBase + e.Rva64;
                Fallback++;
                Game.PrintToLog($"[Sig] {e.Name}: signature not found, using fallback offset +{e.Rva64:X}");
            }
            else
            {
                Missing++;
                Game.PrintToLog($"[Sig] {e.Name}: NOT FOUND");
            }
            Resolved[e.Name] = address;
        }
        Game.PrintToLog($"[Sig] 64-bit resolution: {Found} found, {Fallback} fallback offsets, {Missing} not found " +
                        $"(out of {Sigs64.Entries.Length}).");
    }

    // Offset from the module base of the n-th match, -1 if absent
    private static long FindOccurrence(Scanner scanner, string pattern, int occurrence)
    {
        int start = 0;
        long result = -1;
        for (int i = 0; i <= occurrence; i++)
        {
            var r = scanner.FindPattern(pattern, start);
            if (!r.Found)
                return -1;
            // FindPattern returns an offset in the region: guard against a relative one
            long offset = r.Offset >= start ? r.Offset : start + r.Offset;
            result = offset;
            start = (int)offset + 1;
        }
        return result;
    }

    public static nuint Get(string name) => Resolved.TryGetValue(name, out var a) ? a : 0;

    public static bool Has(string name) => Get(name) != 0;
}
