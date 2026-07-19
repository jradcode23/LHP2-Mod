namespace LHP2_Archi_Mod;

public unsafe class DeathLink(byte* BaseAddress, int sendDeath, int receiveDeath)
{
    private byte* PlayerBaseAddress = BaseAddress;
    private byte* PointerToPlayerStruct => *(byte**)PlayerBaseAddress;
    private int SendDeathAmensty = sendDeath;
    private int ReceiveDeathAmnesty = receiveDeath;

    private bool CanPlayerDie()
    {
        if (PointerToPlayerStruct == null)
        {
            return false;
        }
        ushort playerState = *(PointerToPlayerStruct + 0x558);
        if (playerState == 1) // Player is in shop - works faster than player controllable below
        {
            return false;
        }
        byte isPlayerDead = *(PointerToPlayerStruct + 0x54);
        if (isPlayerDead == 3) // 3 indicates the player is despawned. Lasts just as long as the respawn timer
        {
            return false;
        }
        byte isPlayerControllable = *(PointerToPlayerStruct + 0x4D);
        if ((isPlayerControllable & (1 << 3)) == 0)
        {
            return false;
        }
        return true;
    }

    public void KillPlayer()
    {
        // // Keeping for future Damage link
        // IntPtr damagePlayerAddress;
        // var damagePlayer = Mod._hooks!.CreateWrapper<DamagePlayer>((long)(Mod.BaseAddress + 0x416A20), out damagePlayerAddress);
        // damagePlayer(playerAddress, 8);

        var playerDeathFunction = Mod._hooks!.CreateWrapper<Game.KillPLayer>(
            (long)(Mod.BaseAddress + 0x3F8320),
            out nint deathWrapperAddress
        );

        var reduceStudTotalFunction = Mod._hooks!.CreateWrapper<Game.LoseStuds>(
            (long)(Mod.BaseAddress + 0x312DC0),
            out nint loseStudsAddress
        );

        var spawnStudFunction = Mod._hooks!.CreateWrapper<Game.StudDropSpawner>(
            (long)(Mod.BaseAddress + 0x318420),
            out nint spawnStudsAddress
        );

        uint studsLost = reduceStudTotalFunction((int)PointerToPlayerStruct, 1);
        playerDeathFunction((int)PointerToPlayerStruct, 5, 0, 1, 0, 0);

        int worldObj = *(int*)(Mod.BaseAddress + 0xC5E358);
        Game.PrintToLog($"World Object: 0x{(nuint)worldObj:X}");

        uint studLow = studsLost;
        uint studHigh = 0; // Current setup has stud loss capped at 2k (I think) so this should never be needed

        IntPtr unknownPlayerPtr0 = (int)PointerToPlayerStruct + 0xFCC;
        int unknownPlayerInt = *(PointerToPlayerStruct + 0x55);
        float unknownPlayerFloat = *(float*)(PointerToPlayerStruct + 0x1168);

        spawnStudFunction(
            worldObj, studLow, studHigh, 0, 0, 0,
            unknownPlayerPtr0, 0, 0, unknownPlayerInt, 1.0f,
            unknownPlayerFloat, 0.0f, 1, 0, 0, 0, 0
        );
    }
}