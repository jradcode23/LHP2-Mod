using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using LHP2_Archi_Mod.Addresses;
using Reloaded.Hooks.Definitions;
using Reloaded.Hooks.Definitions.Enums;
using Reloaded.Memory;
using Reloaded.Memory.Interfaces;
using static LHP2_Archi_Mod.Collection64.AsmX64;

namespace LHP2_Archi_Mod.Collection64;

// Hooks and patches of the Collection: x64 registers mapped to the same callbacks as on 32-bit
internal static unsafe class HooksX64
{
    private static readonly HashSet<string> FirstCallLogged = new();
    private static readonly object FirstCallLock = new();
    private static byte[]? _boxesOriginalBytes;

    // Offline checks: receives the hook definitions instead of installing them
    internal static Action<string, AsmHookBehaviour, string[]>? DefinitionSink { get; set; }

    private static nuint Address(string site) => DefinitionSink != null ? (nuint)0x10001000 : Addr64.Get(site);

    private static void Log(string message)
    {
        if (DefinitionSink != null)
            Console.WriteLine(message);
        else
            Game.PrintToLog(message);
    }

    // ------------------------------------------------------------------ hook setup

    public static void SetupHooks(IReloadedHooks? hooks)
    {
        // No hooked site has a jump into its first 5 bytes (checked on the executable)
        // MapID / LevelID: where the two LevelID writes join, before the MapID write
        Hook(hooks, "Hook_LevelAndMap", AsmHookBehaviour.ExecuteAfter,
            Call((nint)(delegate* unmanaged<int, void>)&OnLevelAndMap, ["mov ecx, dword [rbx+0x38]"]));

        Hook(hooks, "Hook_CompleteLevel", AsmHookBehaviour.ExecuteAfter,
            Call((nint)(delegate* unmanaged<void>)&OnCompleteLevel, []));
        // LevelSIP / TrueWizard: on the call after the state write, to avoid a RIP-relative one
        Hook(hooks, "Hook_LevelSIP", AsmHookBehaviour.ExecuteFirst,
            Call((nint)(delegate* unmanaged<void>)&OnLevelSIP, []));
        Hook(hooks, "Hook_TrueWizard", AsmHookBehaviour.ExecuteFirst,
            Call((nint)(delegate* unmanaged<void>)&OnTrueWizard, []));
        Hook(hooks, "Hook_Crest", AsmHookBehaviour.ExecuteFirst,
            Call((nint)(delegate* unmanaged<int, void>)&OnCrest, ["movzx ecx, word [rbx+0x38]"]));
        Hook(hooks, "Hook_RedBrickPurchase", AsmHookBehaviour.ExecuteAfter,
            Call((nint)(delegate* unmanaged<int, void>)&OnRedBrickPurchase, ["mov ecx, edi"]));
        Hook(hooks, "Hook_GoldBrickPurchase", AsmHookBehaviour.ExecuteFirst,
            Call((nint)(delegate* unmanaged<int, void>)&OnGoldBrickPurchase, ["mov ecx, dword [rbx+0x00]"]));
        Hook(hooks, "Hook_SpellUnlock", AsmHookBehaviour.ExecuteFirst,
            Call((nint)(delegate* unmanaged<long, void>)&OnSpellUnlock, ["mov rcx, qword [rbx+0x38]"]));
        Hook(hooks, "Hook_HubCharacterCollected", AsmHookBehaviour.ExecuteAfter,
            Call((nint)(delegate* unmanaged<nint, int, void>)&OnHubCharacterCollected, ["lea rcx, [r9+r10*4]", "mov edx, r8d"]));
        Hook(hooks, "Hook_LevelCharacterCollected", AsmHookBehaviour.ExecuteFirst,
            Call((nint)(delegate* unmanaged<int, void>)&OnLevelCharacterCollected, ["movzx ecx, di"]));
        Hook(hooks, "Hook_CharacterPurchased", AsmHookBehaviour.ExecuteFirst,
            Call((nint)(delegate* unmanaged<nint, int, void>)&OnCharacterPurchased, ["mov rcx, qword [rbx+0x30]", "mov edx, dword [rbx+0x38]"]));
        Hook(hooks, "Hook_HubSIP", AsmHookBehaviour.ExecuteFirst,
            Call((nint)(delegate* unmanaged<int, void>)&OnHubSIP, ["mov ecx, dword [rbx+0x30]"]));
        Hook(hooks, "Hook_HubGB", AsmHookBehaviour.ExecuteFirst,
            Call((nint)(delegate* unmanaged<int, void>)&OnHubGB, ["mov ecx, dword [rbx+0x30]"]));
        Hook(hooks, "Hook_HubRB", AsmHookBehaviour.ExecuteFirst,
            Call((nint)(delegate* unmanaged<int, void>)&OnHubRB, ["mov ecx, dword [rbx+0x38]"]));

        // Ghost tasks: bts/bt/write/jc replaced by the mod's call, then "return 0" as on 32-bit
        nuint ghostReturn0 = Address("Jmp_GhostPathReturn0");
        if (ghostReturn0 != 0)
        {
            Hook(hooks, "Hook_GhostPath", AsmHookBehaviour.DoNotExecuteOriginal,
                Call((nint)(delegate* unmanaged<nint, int, void>)&OnGhostPath,
                    ["mov edx, 1", "mov ecx, dword [rbx+0x30]", "shl edx, cl", "mov rcx, qword [rbx+0x18]", "add rcx, 0xC"],
                    tail: AbsoluteJump(ghostReturn0)));
        }

        // Shop / level select: the 6 calls to SetShopOpen; the caller replaces the esp & 0xF trick
        foreach (var (site, nibble) in new[] { ("Call_SetShopOpen_Shop", 0x0C), ("Call_SetShopOpen_LevelSelect", 0x08),
                                               ("Call_SetShopOpen_Codes", 0), ("Call_SetShopOpen_A", 0),
                                               ("Call_SetShopOpen_B", 0), ("Call_SetShopOpen_C", 0) })
        {
            Hook(hooks, site, AsmHookBehaviour.ExecuteFirst,
                Call((nint)(delegate* unmanaged<int, int, void>)&OnSetShopOpen, ["movzx ecx, byte [rbx+0x30]", $"mov edx, {nibble}"]));
        }

        Hook(hooks, "Hook_OpenMenu", AsmHookBehaviour.ExecuteFirst,
            Call((nint)(delegate* unmanaged<void>)&OnOpenMenu, []));
        // ReduceMenuCount: after "dec edi" (new count), before the RIP-relative write
        Hook(hooks, "Hook_ReduceMenuCount", AsmHookBehaviour.ExecuteFirst,
            Call((nint)(delegate* unmanaged<int, void>)&OnReduceMenuCount, ["mov ecx, edi"]));
        // CloseMenu: on the function epilogue, once every reset is done
        Hook(hooks, "Hook_CloseMenu", AsmHookBehaviour.ExecuteFirst,
            Call((nint)(delegate* unmanaged<void>)&OnCloseMenu, []));

        // Character purchased: "skip" clears ZF, otherwise the original cmp runs again
        {
            var asm = new List<string> { "use64" };
            asm.AddRange(Prologue);
            asm.Add($"mov rax, 0x{(nint)(delegate* unmanaged<int>)&OnCharacterCmp:X}");
            asm.Add("call rax");
            asm.AddRange(RestoreXmmAndStack);
            asm.AddRange(["pop rbx", "pop r11", "pop r10", "pop r9", "pop r8", "pop rdx", "pop rcx",
                          "test al, al", "pop rax", "lea rsp, [rsp+8]", "jnz skip_cmp",
                          "cmp byte [rdi+rax+0x74], 0", "skip_cmp:"]);
            Hook(hooks, "Hook_CharacterCmp", AsmHookBehaviour.DoNotExecuteOriginal, asm.ToArray());
        }

        // Polyjuice pot: loops over Player1/Player2; RAX is the player when opening
        if (Off.CauldronSelectedItem >= 0)
        {
            Hook(hooks, "Hook_OpenPolyjuicePot", AsmHookBehaviour.ExecuteFirst,
                Call((nint)(delegate* unmanaged<nint, void>)&OnOpenPolyjuicePot, ["mov rcx, " + SavedRax]));
            Hook(hooks, "Hook_ClosePolyjuicePot", AsmHookBehaviour.ExecuteFirst,
                Call((nint)(delegate* unmanaged<void>)&OnClosePolyjuicePot, []));
        }
        else
        {
            Log("[Hook] Polyjuice cauldron: hooks not installed (offset of the selected item unknown on 64-bit).");
        }

        Hook(hooks, "Hook_ChangeCharacters", AsmHookBehaviour.ExecuteFirst,
            Call((nint)(delegate* unmanaged<int, nint, void>)&OnChangeCharacters, ["mov ecx, esi", "mov rdx, rdi"]));
        // ChangeYears: before the strcmp with "N0CUT5", as on 32-bit
        Hook(hooks, "Hook_ChangeYears", AsmHookBehaviour.ExecuteFirst,
            Call((nint)(delegate* unmanaged<void>)&OnChangeYears, []));
        // Interrupted messages: before "hint pointer = 0", and before the timer reset (site 2)
        Hook(hooks, "Hook_InterruptedMessage1", AsmHookBehaviour.ExecuteAfter,
            Call((nint)(delegate* unmanaged<int, void>)&OnInterruptedMessage, ["xor ecx, ecx"]));
        // Site 2 is only safe once Nop_HintColor2 removed the RIP-relative write after it
        nuint interrupted2 = Address("Hook_InterruptedMessage2");
        if (DefinitionSink != null || (interrupted2 != 0 && *(byte*)(interrupted2 + 2) == 0x90 && *(byte*)(interrupted2 + 4) == 0x90))
            Hook(hooks, "Hook_InterruptedMessage2", AsmHookBehaviour.ExecuteFirst,
                Call((nint)(delegate* unmanaged<int, void>)&OnInterruptedMessage, ["mov ecx, 1"]));
        else
            Log("[Hook] Hook_InterruptedMessage2 not installed: the hint color NOP is not in place.");

        // Spell tests: before "mov edx, id ; call TestBit64", RCX gets the minifig's spells
        foreach (string site in new[] { "Hook_SpecsAnim1", "Hook_SpecsAnim2", "Hook_SpecsUsable" })
            Hook(hooks, site, AsmHookBehaviour.ExecuteFirst,
                Call((nint)(delegate* unmanaged<ulong>)&OnCheckSpecsUnlock, [], returnInto: SavedRcx));
        Hook(hooks, "Hook_HermBag", AsmHookBehaviour.ExecuteFirst,
            Call((nint)(delegate* unmanaged<ulong>)&OnCheckHermBagUnlock, [], returnInto: SavedRcx));
        Hook(hooks, "Hook_Polyjuice", AsmHookBehaviour.ExecuteFirst,
            Call((nint)(delegate* unmanaged<ulong, ulong>)&OnCheckPolyjuiceUnlock, ["mov rcx, qword [rbx+0x30]"], returnInto: SavedRcx));

        // Duels: the health is in DL on 64-bit (CL on 32-bit)
        Hook(hooks, "Hook_Duel", AsmHookBehaviour.ExecuteFirst,
            Call((nint)(delegate* unmanaged<ulong, ulong>)&OnSetDuelingHealth, ["mov rcx, qword [rbx+0x28]"], returnInto: SavedRdx));
        Hook(hooks, "Hook_ShopItemSelected", AsmHookBehaviour.ExecuteAfter,
            Call((nint)(delegate* unmanaged<int, void>)&OnShopItemSelected, ["mov ecx, dword [rbx+0x30]"]));
        Hook(hooks, "Hook_CharacterShopItemSelected", AsmHookBehaviour.ExecuteFirst,
            Call((nint)(delegate* unmanaged<int, void>)&OnCharacterShopItemSelected, ["mov ecx, dword [rbx+0x28]"]));
        Hook(hooks, "Hook_StudCollected", AsmHookBehaviour.ExecuteFirst,
            Call((nint)(delegate* unmanaged<ulong, nint, void>)&OnStudCollected, ["mov rcx, qword [rbx+0x00]", "mov rdx, rdi"]));
        // Hub map entry: "or byte [rdx+rcx*4+2], 4", map index in EAX as on 32-bit
        Hook(hooks, "Hook_HubMapEntry", AsmHookBehaviour.ExecuteFirst,
            Call((nint)(delegate* unmanaged<int, void>)&OnHubMapEntry, ["mov ecx, dword [rbx+0x38]"]));
        // Every frame, on the game thread: frame times and the deaths received from Death Link
        Hook(hooks, "Hook_Frame", AsmHookBehaviour.ExecuteFirst,
            Call((nint)(delegate* unmanaged<void>)&OnFrame, []));
        // Death sending: after "mov [r14+0x6C], bl", the player is in R14 (EBX on 32-bit)
        Hook(hooks, "Hook_SendDeath", AsmHookBehaviour.ExecuteAfter,
            Call((nint)(delegate* unmanaged<nint, void>)&OnSendDeath, ["mov rcx, r14"]));
    }

    private static void Hook(IReloadedHooks? hooks, string site, AsmHookBehaviour behaviour, string[] asm)
    {
        if (DefinitionSink != null)
        {
            DefinitionSink(site, behaviour, asm);
            return;
        }
        nuint address = Addr64.Get(site);
        if (address == 0)
        {
            Game.PrintToLog($"[Hook] {site}: address not found, hook NOT installed.");
            return;
        }
        try
        {
            var options = new AsmHookOptions { Behaviour = behaviour, PreferRelativeJump = true, MaxOpcodeSize = 5 };
            Game._asmHooks.Add(hooks!.CreateAsmHook(asm, (long)address, options).Activate());
            Game.PrintToLog($"[Hook] {site} installed at harry2.exe+{address - Mod.BaseAddress:X} ({behaviour}).");
        }
        catch (Exception ex)
        {
            Game.PrintToLog($"[Hook] {site}: installation FAILED ({ex.Message}).");
        }
    }

    // Position-independent absolute jump that keeps rax and the flags
    private static string[] AbsoluteJump(nuint target) =>
        ["push rax", $"mov rax, 0x{target:X}", "xchg rax, [rsp]", "ret"];

    // Logs the first call of each callback; an exception must never reach the game
    private static void First([CallerMemberName] string name = "")
    {
        lock (FirstCallLock)
        {
            if (FirstCallLogged.Add(name))
                Game.PrintToLog($"[Hook] {name}: first call.");
        }
    }

    // A per-frame hook can fail in a loop: log at most one line every 10 s per hook
    private static readonly Dictionary<string, (DateTime Last, int Suppressed)> ErrorLog = new();

    private static void Error(Exception ex, [CallerMemberName] string name = "")
    {
        int suppressed;
        lock (ErrorLog)
        {
            DateTime now = DateTime.UtcNow;
            if (ErrorLog.TryGetValue(name, out var state) && now - state.Last < TimeSpan.FromSeconds(10))
            {
                ErrorLog[name] = (state.Last, state.Suppressed + 1);
                return;
            }
            suppressed = state.Suppressed;
            ErrorLog[name] = (now, 0);
        }
        string more = suppressed > 0 ? $" ({suppressed} more since the last message)" : "";
        Game.PrintToLog($"[Hook] {name}: exception {ex.GetType().Name}: {ex.Message}{more}");
    }

    // ------------------------------------------------------------------ callbacks

    [UnmanagedCallersOnly]
    private static void OnLevelAndMap(int mapId)
    {
        try
        {
            First();
            Game.OnLevelChange(*(int*)Addr.LevelID);
            Game.OnMapUpdate(mapId);
        }
        catch (Exception ex) { Error(ex); }
    }

    [UnmanagedCallersOnly]
    private static void OnCompleteLevel() { try { First(); Game.OnLevelComplete(); } catch (Exception ex) { Error(ex); } }

    [UnmanagedCallersOnly]
    private static void OnLevelSIP() { try { First(); Game.OnLevelSIP(); } catch (Exception ex) { Error(ex); } }

    [UnmanagedCallersOnly]
    private static void OnTrueWizard() { try { First(); Game.OnTrueWizard(); } catch (Exception ex) { Error(ex); } }

    // Crest ids are shifted by +1 in the Collection: map them back to the 32-bit values
    [UnmanagedCallersOnly]
    private static void OnCrest(int ax) { try { First(); Game.OnHouseCrest(ax - 1); } catch (Exception ex) { Error(ex); } }

    [UnmanagedCallersOnly]
    private static void OnRedBrickPurchase(int index) { try { First(); Game.OnRedBrickPurchase(index); } catch (Exception ex) { Error(ex); } }

    // 64-bit: EBX holds the item index (a 1 << index mask on 32-bit)
    [UnmanagedCallersOnly]
    private static void OnGoldBrickPurchase(int index)
    {
        try { First(); Game.OnGoldBrickPurchase(1 << (index & 31)); } catch (Exception ex) { Error(ex); }
    }

    // 64-bit: the mask is in RAX (EDX:EAX on 32-bit)
    [UnmanagedCallersOnly]
    private static void OnSpellUnlock(long mask)
    {
        try { First(); Game.OnSpellUnlock((int)mask, (int)(mask >> 32)); } catch (Exception ex) { Error(ex); }
    }

    [UnmanagedCallersOnly]
    private static void OnHubCharacterCollected(nint address, int mask)
    {
        try { First(); Game.OnHubCharacterCollected(address, mask); } catch (Exception ex) { Error(ex); }
    }

    [UnmanagedCallersOnly]
    private static void OnLevelCharacterCollected(int id) { try { First(); Game.OnLevelCharacterCollected(id); } catch (Exception ex) { Error(ex); } }

    [UnmanagedCallersOnly]
    private static void OnCharacterPurchased(nint baseAddress, int index)
    {
        try { First(); Game.OnCharacterPurchased(baseAddress, index); } catch (Exception ex) { Error(ex); }
    }

    [UnmanagedCallersOnly]
    private static void OnHubSIP(int index3) { try { First(); Game.OnHubSIP(index3); } catch (Exception ex) { Error(ex); } }

    [UnmanagedCallersOnly]
    private static void OnHubGB(int index3) { try { First(); Game.OnHubGB(index3); } catch (Exception ex) { Error(ex); } }

    [UnmanagedCallersOnly]
    private static void OnHubRB(int index3) { try { First(); Game.OnHubRB(index3); } catch (Exception ex) { Error(ex); } }

    [UnmanagedCallersOnly]
    private static void OnGhostPath(nint address, int mask) { try { First(); HubHandler.HandleGhostPaths(address, mask); } catch (Exception ex) { Error(ex); } }

    // nibble replaces the esp & 0xF test: 0xC = shop, 0x8 = level select, 0 = other
    [UnmanagedCallersOnly]
    private static void OnSetShopOpen(int open, int nibble)
    {
        try { First(); Game.OnShopChange(open, nibble); } catch (Exception ex) { Error(ex); }
    }

    // 64-bit: the menu id isn't in EDI, the function copies it into a global
    [UnmanagedCallersOnly]
    private static void OnOpenMenu()
    {
        try { First(); Game.OnOpenMenu(*(int*)Addr.MenuIdGlobal); } catch (Exception ex) { Error(ex); }
    }

    [UnmanagedCallersOnly]
    private static void OnReduceMenuCount(int count) { try { First(); Game.OnReduceMenuCount(count); } catch (Exception ex) { Error(ex); } }

    [UnmanagedCallersOnly]
    private static void OnCloseMenu() { try { First(); Game.OnCloseMenu(); } catch (Exception ex) { Error(ex); } }

    [UnmanagedCallersOnly]
    private static int OnCharacterCmp()
    {
        try { First(); return Game.OnCharacterCmp() ? 1 : 0; } catch (Exception ex) { Error(ex); return 0; }
    }

    [UnmanagedCallersOnly]
    private static void OnOpenPolyjuicePot(nint player) { try { First(); Game.HandleOpenPolyjuicePot((nuint)player); } catch (Exception ex) { Error(ex); } }

    [UnmanagedCallersOnly]
    private static void OnClosePolyjuicePot() { try { First(); Game.OnClosePolyjuicePot(); } catch (Exception ex) { Error(ex); } }

    [UnmanagedCallersOnly]
    private static void OnChangeCharacters(int characterId, nint holder)
    {
        try { First(); Game.OnChangeCharacters(characterId, (int)holder); } catch (Exception ex) { Error(ex); }
    }

    [UnmanagedCallersOnly]
    private static void OnChangeYears() { try { First(); Game.OnChangeYears(); } catch (Exception ex) { Error(ex); } }

    private static bool _frameLogged;

    [UnmanagedCallersOnly]
    private static void OnFrame()
    {
        try
        {
            if (!_frameLogged)
            {
                _frameLogged = true; // not First(): it takes a lock, too costly every frame
                Game.PrintToLog("[Hook] OnFrame: first call.");
            }
            FrameMonitor.OnFrame();
            Mod.GameInstance?.Player1?.RunPendingKill64();
        }
        catch (Exception ex) { Error(ex); }
    }

    [UnmanagedCallersOnly]
    private static void OnHubMapEntry(int mapIndex) { try { First(); Game.OnHubMapEntry(mapIndex); } catch (Exception ex) { Error(ex); } }

    [UnmanagedCallersOnly]
    private static void OnSendDeath(nint player) { try { First(); Game.HandleSendPlayerDeath((nuint)player); } catch (Exception ex) { Error(ex); } }

    [UnmanagedCallersOnly]
    private static void OnInterruptedMessage(int timerAlreadyReset)
    {
        try { First(); HintSystem.HandleInterruptedMessage(timerAlreadyReset != 0); } catch (Exception ex) { Error(ex); }
    }

    [UnmanagedCallersOnly]
    private static ulong OnCheckSpecsUnlock()
    {
        try { First(); return SpellHandler.CheckSpecsUnlock64(); } catch (Exception ex) { Error(ex); return 0; }
    }

    [UnmanagedCallersOnly]
    private static ulong OnCheckHermBagUnlock()
    {
        try { First(); return SpellHandler.CheckHermBagUnlock64(); } catch (Exception ex) { Error(ex); return 0; }
    }

    // Bit 26 (Polyjuice) is in the low dword, as on 32-bit
    [UnmanagedCallersOnly]
    private static ulong OnCheckPolyjuiceUnlock(ulong spells)
    {
        try
        {
            First();
            uint low = (uint)SpellHandler.CheckPolyjuiceUnlock((int)(uint)spells);
            return (spells & 0xFFFFFFFF00000000UL) | low;
        }
        catch (Exception ex) { Error(ex); return spells; }
    }

    [UnmanagedCallersOnly]
    private static ulong OnSetDuelingHealth(ulong rdx)
    {
        try { First(); return Game.OnSetDuelingHealth((nuint)rdx); } catch (Exception ex) { Error(ex); return rdx; }
    }

    [UnmanagedCallersOnly]
    private static void OnShopItemSelected(int index) { try { First(); Game.OnShopItemSelected(index); } catch (Exception ex) { Error(ex); } }

    [UnmanagedCallersOnly]
    private static void OnCharacterShopItemSelected(int index) { try { First(); Game.OnCharacterShopItemSelected(index); } catch (Exception ex) { Error(ex); } }

    [UnmanagedCallersOnly]
    private static void OnStudCollected(ulong value, nint counterAddress)
    {
        try { Game.OnStudCollected((nuint)value, (nuint)counterAddress); } catch (Exception ex) { Error(ex); }
    }

    // ------------------------------------------------------------------ patches

    // (site, bytes to NOP, matching 32-bit patch)
    private static readonly (string Site, int Length, string Comment)[] Nops =
    [
        ("Nop_GoldBrickCorrector1", 7, "Gold Brick Amount Corrector #1"),
        ("Nop_GoldBrickCorrector23", 4, "Gold Brick Amount Correctors #2 and #3 (merged into one write on 64-bit)"),
        ("Nop_FreeplayCheck", 6, "Freeplay check for Save and Exit"),
        ("Nop_UnlockCurrentStory", 5, "Unlock Current Level Story"),
        ("Nop_UnlockCurrentFreeplay", 5, "Unlock Current Level Freeplay"),
        ("Nop_UnlockNextStory", 5, "Unlock Next Story Level"),
        ("Nop_HintSystem", 7, "Hint System"),
        ("Nop_HintSystemDup", 7, "Hint System (copy duplicated by the 64-bit compiler)"),
        ("Nop_HintCall", 5, "Call to Hint System that doesn't clear old value"),
        ("Nop_HintColor2", 7, "Resetting the Hint Color to 2"),
        ("Nop_HintColorChange", 7, "Changing the hint color"),
        ("Nop_HintColor5", 7, "Resetting the Hint Color (5)"),
        ("Nop_SpecsJump1", 2, "Specs lesson - Animation 1"),
        ("Nop_SpecsJump2", 2, "Specs lesson - Animation 2"),
        ("Nop_SpecsJump3", 2, "Specs lesson - If ability active"),
        ("Nop_CheatDuplicate", 2, "Cheat code already entered"),
        ("Nop_DarkTimesReload", 6, "Forces to Dark Times upon save reload (jz rel32 on 64-bit)"),
        ("Nop_HermBagLesson", 2, "Herm Bag Lesson check"),
        ("Nop_MinifigCopy", 7, "mov static into minifig (parts 1 and 2 merged)"),
        ("Nop_MinifigClear", 11, "clears out all spells except green (parts 1 and 2 merged)"),
    ];

    public static void ApplyPatches()
    {
        foreach (var (site, length, comment) in Nops)
        {
            nuint address = Addr64.Get(site);
            if (address == 0)
            {
                Game.PrintToLog($"[Patch] {site} not found: NOP not applied ({comment}).");
                continue;
            }
            Memory.Instance.SafeWrite(address, Enumerable.Repeat((byte)0x90, length).ToArray());
        }

        // Hint timer: the 8-byte write also covers the next float, so only that float is zeroed
        RewriteTimerReset("Patch_HintTimer");
        RewriteTimerReset("Patch_HintTimerDup");

        // AND [rbp+0x80], 0xFFCFFFFF: stop clearing bits 20-21 of the minifig's spells
        nuint green = Addr64.Get("Patch_GreenSpells");
        if (green != 0 && *(byte*)(green + 9) == 0xCF)
            Memory.Instance.SafeWrite(green + 9, [0xFF]);
        else
            Game.PrintToLog("[Patch] Patch_GreenSpells: unexpected bytes, patch not applied.");

        Game.PrintToLog($"[Patch] {Nops.Length} 64-bit NOPs processed.");
    }

    private static void RewriteTimerReset(string site)
    {
        nuint a = Addr64.Get(site);
        if (a == 0)
            return;
        byte* p = (byte*)a;
        if (p[0] != 0x48 || p[1] != 0xC7 || p[2] != 0x05 || *(uint*)(p + 7) != 0)
        {
            Game.PrintToLog($"[Patch] {site}: unexpected bytes, patch not applied.");
            return;
        }
        // 8-byte write at the timer becomes a 4-byte write at timer+4
        int disp = *(int*)(p + 3) + 5;
        byte[] patch = new byte[11];
        patch[0] = 0xC7; patch[1] = 0x05;
        BitConverter.GetBytes(disp).CopyTo(patch, 2);
        patch[10] = 0x90;
        Memory.Instance.SafeWrite(a, patch);
    }

    // LockBoxes: mov rcx, [SpellBase] -> xor ecx, ecx, so the spell 0x31 test fails
    public static void LockBoxes()
    {
        nuint a = Addr64.Get("Patch_Boxes");
        if (a == 0)
            return;
        _boxesOriginalBytes ??= new ReadOnlySpan<byte>((void*)a, 7).ToArray();
        Memory.Instance.SafeWrite(a, [0x31, 0xC9, 0x0F, 0x1F, 0x44, 0x00, 0x00]);
    }

    // UnlockBoxes: restores the original bytes, read before the first patch
    public static void UnlockBoxes()
    {
        nuint a = Addr64.Get("Patch_Boxes");
        if (a == 0 || _boxesOriginalBytes == null)
            return;
        Memory.Instance.SafeWrite(a, _boxesOriginalBytes);
    }
}
