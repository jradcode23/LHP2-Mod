namespace LHP2_Archi_Mod;

public unsafe class DeathLink(byte* BaseAddress, int amnesty)
{
    private byte* PlayerBaseAddress = BaseAddress;
    private byte* PointerToPlayerStruct => *(byte**)PlayerBaseAddress;
    private int SendDeathAmensty = amnesty;
    private int ReceiveDeathAmnesty = amnesty;
    private readonly Queue<string> _deathLinkQueue = new();
    private readonly object _deathLinkQueueLock = new();
    private readonly object _receivedDeathLock = new();
    private readonly Queue<int> _outboundDeathLinkQueue = new();
    private readonly object _outboundDeathLinkQueueLock = new();
    private bool _isProcessingDeathLinks;
    private bool _isProcessingOutboundDeathLinks;
    private bool ReceivedDeath = false;
    private int NextOutboundDeathLinkId;

    public void SendPlayerDeath()
    {
        bool received;
        lock (_receivedDeathLock)
        {
            received = ReceivedDeath;
        }
        if (received)
        {
            Game.PrintToLog("Death Due to Death Received. Skipping");
            return;
        }
        if (SendDeathAmensty > 0)
        {
            SendDeathAmensty--;
            HintSystem.AddInterruptedMessageToFront($"Sent Death ignored due to amnesty. Remaining amnesty: {SendDeathAmensty}", 0);
            Game.PrintToLog($"Sent Death ignored due to amnesty. Remaining amnesty: {SendDeathAmensty}");
            return;
        }
        QueueOutboundDeathLink();
    }

    private void QueueOutboundDeathLink()
    {
        int id;
        lock (_outboundDeathLinkQueueLock)
        {
            id = NextOutboundDeathLinkId++;
            _outboundDeathLinkQueue.Enqueue(id);
            if (_isProcessingOutboundDeathLinks)
            {
                return;
            }

            _isProcessingOutboundDeathLinks = true;
        }
        HintSystem.AddInterruptedMessageToFront($"Sending Death. You have caused {id + 1} deaths", 0);
        //TODO: add death count to data storage
        ProcessOutboundDeathLinkQueue();
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

    public void QueueDeathLink(string cause)
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

        ProcessDeathLinkQueue();
    }

    private void ProcessDeathLinkQueue()
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

            if (!CanPlayerDie())
            {
                lock (_deathLinkQueueLock)
                {
                    _deathLinkQueue.Enqueue(nextDeath);
                }

                Thread.Sleep(100);
                continue;
            }

            ProcessDeathLink(nextDeath);
        }
    }

    private void ProcessDeathLink(string slot)
    {
        lock (_receivedDeathLock)
        {
            ReceivedDeath = true;
        }
        string deathLinkMessage = $"Death Link received from {slot}";
        if (ReceiveDeathAmnesty > 0)
        {
            ReceiveDeathAmnesty--;
            HintSystem.AddInterruptedMessageToFront($"{deathLinkMessage} Ignored due to amnesty. Remaining amnesty: {ReceiveDeathAmnesty}", 0);
            Game.PrintToLog($" Death Link received but ignored due to amnesty. Remaining amnesty: {ReceiveDeathAmnesty}");
            return;
        }

        HintSystem.AddInterruptedMessageToFront($"{deathLinkMessage}", 0);
        Game.PrintToLog($"{deathLinkMessage}.");
        KillPlayer();
        lock (_receivedDeathLock)
        {
            ReceivedDeath = false;
        }
    }

    public void ReceiveDeathLink(string slot)
    {

        if (slot == Mod.GameInstance!.PlayerName)
        {
            Game.PrintToLog("Player Caused Death");
            return;
        }
        QueueDeathLink(slot);
        Game.PrintToLog($"Death Link received Queued from {slot}.");
    }

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
        if ((isPlayerControllable & (1 << 3)) != 0)
        {
            return false;
        }
        byte playerMaxHealth = *(PointerToPlayerStruct + 0xF0C);
        if (playerMaxHealth < 8) // Player has less than 8 health, which means duel or broom or something
        {
            return false;
        }
        float damageInvulnerabilityTimer = *(float*)(PointerToPlayerStruct + 0x11B8);
        if (damageInvulnerabilityTimer > 2.5) // Set to 2.5 cause changing map is 3 seconds and changing character is 2.5 seconds. Don't want them to be able to cheese it by changing character
        {
            return false;
        }
        bool nothingOnScreen = HintSystem.IsScreenEmpty();
        bool hubCutscene = HintSystem.IsPlayerNotInHubCutscene();
        if (!nothingOnScreen || !hubCutscene)
        {
            return false;
        }
        int deathValue = *(int*)(PointerToPlayerStruct + 0x55);
        if ((deathValue & 0xFFFF) == 0x300)
        {
            Game.PrintToLog("Player can die");
            return true;
        }
        return false;
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