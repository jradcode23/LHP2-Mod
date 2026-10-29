namespace LHP2_Archi_Mod.Collection64;

// x64 hook stubs (FASM) that save the registers and call [UnmanagedCallersOnly] C# methods
internal static class AsmX64
{
    // Saved registers relative to RBX; xmm0-5 are saved below it
    public const string SavedRbx = "qword [rbx+0x00]";
    public const string SavedR11 = "qword [rbx+0x08]";
    public const string SavedR10 = "qword [rbx+0x10]";
    public const string SavedR9 = "qword [rbx+0x18]";
    public const string SavedR8 = "qword [rbx+0x20]";
    public const string SavedRdx = "qword [rbx+0x28]";
    public const string SavedRcx = "qword [rbx+0x30]";
    public const string SavedRax = "qword [rbx+0x38]";
    public const string ReturnAddress = "qword [rbx+0x48]";

    public static readonly string[] Prologue =
    [
        "pushfq",
        "push rax", "push rcx", "push rdx", "push r8", "push r9", "push r10", "push r11",
        "push rbx",
        "mov rbx, rsp",
        "sub rsp, 0x60",
        "movdqu [rsp+0x00], xmm0", "movdqu [rsp+0x10], xmm1", "movdqu [rsp+0x20], xmm2",
        "movdqu [rsp+0x30], xmm3", "movdqu [rsp+0x40], xmm4", "movdqu [rsp+0x50], xmm5",
        "and rsp, -16",
        "sub rsp, 0x20",
    ];

    // Restores all but the general registers: follow it with PopRegisters
    public static readonly string[] RestoreXmmAndStack =
    [
        "lea rsp, [rbx-0x60]",
        "movdqu xmm0, [rsp+0x00]", "movdqu xmm1, [rsp+0x10]", "movdqu xmm2, [rsp+0x20]",
        "movdqu xmm3, [rsp+0x30]", "movdqu xmm4, [rsp+0x40]", "movdqu xmm5, [rsp+0x50]",
        "mov rsp, rbx",
    ];

    public static readonly string[] PopRegisters =
    [
        "pop rbx", "pop r11", "pop r10", "pop r9", "pop r8", "pop rdx", "pop rcx", "pop rax",
        "popfq",
    ];

    // Standard stub; arguments are FASM lines loading rcx/rdx/r8/r9, RAX can go to a saved register
    public static string[] Call(nint function, string[] arguments, string? returnInto = null, string[]? tail = null)
    {
        var asm = new List<string> { "use64" };
        asm.AddRange(Prologue);
        asm.AddRange(arguments);
        asm.Add($"mov rax, 0x{function:X}");
        asm.Add("call rax");
        if (returnInto != null)
            asm.Add($"mov {returnInto}, rax");
        asm.AddRange(RestoreXmmAndStack);
        asm.AddRange(PopRegisters);
        if (tail != null)
            asm.AddRange(tail);
        return asm.ToArray();
    }
}
