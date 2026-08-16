namespace LHP2_Archi_Mod;

public unsafe class Player(byte* BaseAddress, int amnesty)
{
    private const int PlayerState1Offset = 0x54C;
    private const int PlayerState2Offset = 0x558;
    private const int PlayerMaxHealthOffset = 0xF0C;
    private const int PlayerCurrentHealthOffset = 0xF0D;
    private const int PlayerDespawnedStateOffset = 0x54;
    private const int PlayerControlFlagsOffset = 0x4D;
    private const int PlayerDamageInvulnerabilityTimerOffset = 0x11B8;
    private const int PlayerDeathValueOffset = 0x55;
    private const ushort DefaultAnimation = 0xFFFF;
    private const byte PlayerDespawnedStateValue = 3;
    private const ushort DeathAnimationConstant = 0x31;

    private byte* PlayerBaseAddress = BaseAddress;
    private byte* PointerToPlayerStruct => *(byte**)PlayerBaseAddress;
    private ushort* PlayerState1 => (ushort*)(PointerToPlayerStruct + PlayerState1Offset); // This seems to be effect that the player receives
    private ushort* PlayerState2 => (ushort*)(PointerToPlayerStruct + PlayerState2Offset); // This seems to be how the player is animation of the effect
    private byte* PlayerMaxHealth => PointerToPlayerStruct + PlayerMaxHealthOffset;
    private byte* PlayerCurrentHealth => PointerToPlayerStruct + PlayerCurrentHealthOffset;

    private int _sendDeathAmnesty = amnesty;
    private int _receiveDeathAmnesty = amnesty;
    private readonly Queue<string> _deathLinkQueue = new();
    private readonly object _deathLinkQueueLock = new();
    private readonly object _receivedDeathLock = new();
    private readonly Queue<int> _outboundDeathLinkQueue = new();
    private readonly object _outboundDeathLinkQueueLock = new();
    private bool _isProcessingDeathLinks;
    private bool _isProcessingOutboundDeathLinks;
    private bool _receivedDeath;
    private int _nextOutboundDeathLinkId;

    public void SendPlayerDeath()
    {
        lock (_receivedDeathLock)
        {
            if (_receivedDeath)
            {
                Game.PrintToLog("Death Due to Death Received. Skipping");
                _receivedDeath = false;
                return;
            }
        }
        if (_sendDeathAmnesty > 0)
        {
            _sendDeathAmnesty--;
            HintSystem.AddInterruptedMessageToFront($"Sent Death ignored due to amnesty. Remaining amnesty: {_sendDeathAmnesty}", 0);
            Game.PrintToLog($"Sent Death ignored due to amnesty. Remaining amnesty: {_sendDeathAmnesty}");
            return;
        }
        QueueOutboundDeathLink();
    }

    private void QueueOutboundDeathLink()
    {
        int id;
        lock (_outboundDeathLinkQueueLock)
        {
            id = _nextOutboundDeathLinkId++;
            _outboundDeathLinkQueue.Enqueue(id);
            if (_isProcessingOutboundDeathLinks)
            {
                return;
            }

            _isProcessingOutboundDeathLinks = true;
        }
        HintSystem.AddInterruptedMessageToFront($"Sending Death. You have caused {id + 1} deaths", 0);
        //TODO: add death count to data storage
        StartBackgroundProcessor(ProcessOutboundDeathLinkQueue, "OutboundDeathLinkProcessor");
    }

    private void ProcessOutboundDeathLinkQueue()
    {
        while (true)
        {
            int? nextDeath;
            lock (_outboundDeathLinkQueueLock)
            {
                if (_outboundDeathLinkQueue.Count == 0)
                {
                    _isProcessingOutboundDeathLinks = false;
                    return;
                }

                nextDeath = _outboundDeathLinkQueue.Dequeue();
            }

            if (!Mod.LHP2_Archipelago!.SendDeath())
            {
                lock (_outboundDeathLinkQueueLock)
                {
                    _outboundDeathLinkQueue.Enqueue(nextDeath.Value);
                }

                Thread.Sleep(1000);
                continue;
            }
        }
    }

    public void QueueInboundDeath(string cause)
    {
        lock (_deathLinkQueueLock)
        {
            _deathLinkQueue.Enqueue(cause);
            if (_isProcessingDeathLinks)
            {
                return;
            }

            _isProcessingDeathLinks = true;
        }

        StartBackgroundProcessor(ProcessInboundDeathLinkQueue, "InboundDeathLinkProcessor");
    }

    private void ProcessInboundDeathLinkQueue()
    {
        while (true)
        {
            string? nextDeath;
            lock (_deathLinkQueueLock)
            {
                if (_deathLinkQueue.Count == 0)
                {
                    _isProcessingDeathLinks = false;
                    return;
                }

                nextDeath = _deathLinkQueue.Dequeue();
            }

            if (!CanPlayerReceiveNegativeEffect())
            {
                lock (_deathLinkQueueLock)
                {
                    _deathLinkQueue.Enqueue(nextDeath);
                }

                Thread.Sleep(100);
                continue;
            }

            ProcessInboundDeathLink(nextDeath);
        }
    }

    private void ProcessInboundDeathLink(string slot)
    {
        lock (_receivedDeathLock)
        {
            _receivedDeath = true;
        }
        string deathLinkMessage = $"Death Link received from {slot}";

        if (_receiveDeathAmnesty > 0)
        {
            _receiveDeathAmnesty--;
            HintSystem.AddInterruptedMessageToFront($"{deathLinkMessage} Ignored due to amnesty. Remaining amnesty: {_receiveDeathAmnesty}", 0);
            Game.PrintToLog($" Death Link received but ignored due to amnesty. Remaining amnesty: {_receiveDeathAmnesty}");
            lock (_receivedDeathLock)
            {
                _receivedDeath = false;
            }
            return;
        }

        HintSystem.AddInterruptedMessageToFront($"{deathLinkMessage}", 0);
        Game.PrintToLog($"{deathLinkMessage}.");
        KillPlayer();
    }

    public void ReceiveDeathLink(string slot)
    {
        QueueInboundDeath(slot);
        Game.PrintToLog($"Death Link received Queued from {slot}.");
    }

    private bool CanPlayerReceiveNegativeEffect()
    {
        if (PointerToPlayerStruct == null)
        {
            Game.PrintToLog("[LHP2.archipelago.mod] Cannot receive negative effect: PointerToPlayerStruct is null.");
            return false;
        }
        if (*PlayerState2 != DefaultAnimation) // Player isn't performing any animation
        {
            return false;
        }
        byte* isPlayerDead = PointerToPlayerStruct + PlayerDespawnedStateOffset;
        if (*isPlayerDead == PlayerDespawnedStateValue) // 3 indicates the player is despawned. Lasts just as long as the respawn timer
        {
            return false;
        }
        byte isPlayerControllable = *(PointerToPlayerStruct + PlayerControlFlagsOffset);
        if ((isPlayerControllable & (1 << 3)) != 0)
        {
            return false;
        }
        if (*PlayerMaxHealth < 8) // Player has less than 8 health, which means duel or broom or something
        {
            return false;
        }
        float* damageInvulnerabilityTimer = (float*)(PointerToPlayerStruct + PlayerDamageInvulnerabilityTimerOffset);
        if (*damageInvulnerabilityTimer > 2) // Set to 2 cause changing map is 3 seconds and changing character is 2.5 seconds
        {
            return false;
        }
        (bool nothingOnScreen, bool hubCutscene) = HintSystem.GetScreenAndCutsceneState();
        if (!nothingOnScreen || !hubCutscene)
        {
            return false;
        }
        int deathValue = *(int*)(PointerToPlayerStruct + PlayerDeathValueOffset);
        if ((deathValue & 0xFFFF) == 0x300)
        {
            Game.PrintToLog("Player can die");
            return true;
        }
        return false;
    }

    public void KillPlayer()
    {
        try
        {
            // // Keeping for future Damage link
            // IntPtr damagePlayerAddress;
            // var damagePlayer = Mod._hooks!.CreateWrapper<DamagePlayer>((long)(Mod.BaseAddress + 0x416A20), out damagePlayerAddress);
            // damagePlayer(playerAddress, 8);

            // var playerDeathFunction = Mod._hooks!.CreateWrapper<Game.KillPLayer>(
            //     (long)(Mod.BaseAddress + 0x3F8320),
            //     out nint deathWrapperAddress
            // );

            var reduceStudTotalFunction = Mod._hooks!.CreateWrapper<Game.LoseStuds>(
                (long)(Mod.BaseAddress + 0x312DC0),
                out nint loseStudsAddress
            );

            var spawnStudFunction = Mod._hooks!.CreateWrapper<Game.StudDropSpawner>(
                (long)(Mod.BaseAddress + 0x318420),
                out nint spawnStudsAddress
            );

            // Mod.Logger!.WriteLine($"[LHP2.archipelago.mod] Player Death Function Address: 0x{(nuint)deathWrapperAddress:X}");
            Mod.Logger!.WriteLine($"[LHP2.archipelago.mod] Player Lose Studs Function Address: 0x{(nuint)loseStudsAddress:X}");
            Mod.Logger!.WriteLine($"[LHP2.archipelago.mod] Player Spawn Studs Function Address: 0x{(nuint)spawnStudsAddress:X}");

            if (PointerToPlayerStruct == null)
            {
                Mod.Logger!.WriteLine("[LHP2.archipelago.mod] KillPlayer aborted: PointerToPlayerStruct is null.");
                return;
            }

            if (reduceStudTotalFunction == null)
            {
                Mod.Logger!.WriteLine("[LHP2.archipelago.mod] KillPlayer aborted: LoseStuds wrapper is null.");
                return;
            }

            if (spawnStudFunction == null)
            {
                Mod.Logger!.WriteLine("[LHP2.archipelago.mod] KillPlayer aborted: StudDropSpawner wrapper is null.");
                return;
            }

            Mod.Logger!.WriteLine($"[LHP2.archipelago.mod] Player Struct Address: 0x{(nuint)PointerToPlayerStruct:X}");

            uint studsLost = reduceStudTotalFunction((int)PointerToPlayerStruct, 1);
            Mod.Logger!.WriteLine($"[LHP2.archipelago.mod] Player Studs Lost: {studsLost}");
            // playerDeathFunction((int)PointerToPlayerStruct, 5, 0, 1, 0, 0); // Removed for now, was randomly crashing. It seemed to be corrupting other function call addresses

            WriteToPlayerState(DeathAnimationConstant); // This value plays the death animation

            if (studsLost == 0)
            {
                Mod.Logger!.WriteLine("No studs lost, skipping stud spawn.");
                return;
            }

            int worldObj = *(int*)(Mod.BaseAddress + 0xC5E358);
            Mod.Logger!.WriteLine($"[LHP2.archipelago.mod] World Object: 0x{(nuint)worldObj:X}");

            uint studLow = studsLost;
            uint studHigh = 0; // Current setup has stud loss capped at 2k (I think) so this should never be needed

            IntPtr unknownPlayerPtr0 = (int)PointerToPlayerStruct + 0xFCC;

            if (unknownPlayerPtr0 == IntPtr.Zero)
            {
                Mod.Logger!.WriteLine("[LHP2.archipelago.mod] KillPlayer aborted: unknownPlayerPtr0 is null.");
                return;
            }

            int unknownPlayerInt = *(PointerToPlayerStruct + 0x55);
            float unknownPlayerFloat = *(float*)(PointerToPlayerStruct + 0x1168);
            Mod.Logger!.WriteLine($"[LHP2.archipelago.mod] Unknown Player Ptr0: 0x{(nuint)unknownPlayerPtr0:X}");
            Mod.Logger!.WriteLine($"[LHP2.archipelago.mod] Unknown Player Int: {unknownPlayerInt}");
            Mod.Logger!.WriteLine($"[LHP2.archipelago.mod] Unknown Player Float: {unknownPlayerFloat}");

            spawnStudFunction(
                worldObj, studLow, studHigh, 0, 0, 0,
                unknownPlayerPtr0, 0, 0, unknownPlayerInt, 1.0f,
                unknownPlayerFloat, 0.0f, 1, 0, 0, 0, 0
            );
        }
        catch (Exception ex)
        {
            Mod.Logger!.WriteLine($"[LHP2.archipelago.mod] Exception during KillPlayer: {ex.Message}");
            Mod.Logger!.WriteLine($"[LHP2.archipelago.mod] Stack Trace: {ex.StackTrace}");
        }
    }

    private static void StartBackgroundProcessor(Action action, string name)
    {
        new Thread(() => action())
        {
            IsBackground = true,
            Name = name
        }.Start();
    }

    private void WriteToPlayerState(ushort value)
    {
        if (PointerToPlayerStruct == null)
        {
            Mod.Logger?.WriteLine("[LHP2.archipelago.mod] WriteToPlayerState aborted: PointerToPlayerStruct is null.");
            return;
        }

        *PlayerState2 = value;
    }
}
