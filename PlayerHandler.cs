namespace LHP2_Archi_Mod;

public enum EffectType : ushort
{
    // Values to give the first state address which seems to be type (+ 0x54C)
    Immobulus = 0xA5,
    SlugulusEructo = 0xA7,
    Incarcerous = 0xF4,

    // Values to write in the second state address which seems to be related animation (+ 0x558)
    GivePlayerEffect = 0x19,
    KillPlayer = 0x31,
    Default = 0xFFFF,
}

public record class ReceivedEffect(string Reason, EffectType PlayerEffect)
{
    public string Reason = Reason;
    public EffectType PlayerEffect = PlayerEffect;
}

public unsafe class Player(byte* BaseAddress, int amnesty)
{
    // Memory offsets for the Player struct
    private const uint PlayerStateOffset1 = 0x54C;
    private const uint PlayerStateOffset2 = 0x558;
    private const uint PlayerEffectTimer = 0x504;
    private const uint PlayerMaxHealthOffset = 0xF0C;
    private const uint PlayerCurrentHealthOffset = 0xF0D;
    private const uint PlayerDespawnedStateOffset = 0x54;
    private const uint PlayerControlFlagsOffset = 0x4D;
    private const uint PlayerDamageInvulnerabilityTimerOffset = 0x11B8;
    private const uint PlayerDeathValueOffset = 0x55;

    // Pointers to the Relevant Addresses in the Player Struct
    private byte* PlayerBaseAddress = BaseAddress;
    private byte* PointerToPlayerStruct => *(byte**)PlayerBaseAddress;
    private ushort* PlayerStateEffect => (ushort*)(PointerToPlayerStruct + PlayerStateOffset1); // This seems to be effect that the player receives
    private ushort* PlayerStateAnimation => (ushort*)(PointerToPlayerStruct + PlayerStateOffset2); // This seems to be how the player is animation of the effect
    private byte* PlayerMaxHealth => PointerToPlayerStruct + PlayerMaxHealthOffset;
    // private byte* PlayerCurrentHealth => PointerToPlayerStruct + PlayerCurrentHealthOffset; // Keeping for Damage Link in the future

    // Lock and Variable to ensure that Deaths aren't sent when we receive death
    private readonly object _receivedDeathLock = new();
    private bool _receivedDeath; 

    // Variables and functions relating to receiving a negative effect
    private int _receiveDeathAmnesty = amnesty;
    private readonly Queue<ReceivedEffect> _inboundEffectQueue = new();
    private readonly object _inboundEffectQueueLock = new();
    private bool _isProcessingInboundQueue;

    public void QueueInboundEffect(string cause, EffectType effectType)
    {
        ReceivedEffect effect = new(cause, effectType);
        lock (_inboundEffectQueueLock)
        {
            _inboundEffectQueue.Enqueue(effect);
            if (_isProcessingInboundQueue)
            {
                return;
            }

            _isProcessingInboundQueue = true;
        }

        StartBackgroundProcessor(ProcessInboundEffectQueue, "InboundEffectProcessor");
    }

    private void ProcessInboundEffectQueue()
    {
        while (true)
        {
            ReceivedEffect? nextEffect;
            lock (_inboundEffectQueueLock)
            {
                if (_inboundEffectQueue.Count == 0)
                {
                    _isProcessingInboundQueue = false;
                    return;
                }

                nextEffect = _inboundEffectQueue.Dequeue();
            }

            if (!CanPlayerReceiveNegativeEffect())
            {
                lock (_inboundEffectQueueLock)
                {
                    _inboundEffectQueue.Enqueue(nextEffect);
                }

                Thread.Sleep(100);
                continue;
            }

            if (nextEffect.PlayerEffect == EffectType.KillPlayer)
            {
                ProcessInboundDeathLink(nextEffect.Reason);
            }
        }
    }

    private void ProcessInboundDeathLink(string reason)
    {
        lock (_receivedDeathLock)
        {
            _receivedDeath = true;
        }
        string deathLinkMessage = reason;

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

    public void ReceiveDeathLink(string reason)
    {
        QueueInboundEffect(reason, EffectType.KillPlayer);
        Game.PrintToLog($"Death Link queued: {reason}.");
    }

    private bool CanPlayerReceiveNegativeEffect()
    {
        if (PointerToPlayerStruct == null)
        {
            Game.PrintToLog("Cannot receive negative effect: PointerToPlayerStruct is null.");
            return false;
        }
        if (*PlayerStateAnimation != (ushort)EffectType.Default) // Player isn't performing any animation
        {
            return false;
        }
        byte* isPlayerDead = PointerToPlayerStruct + PlayerDespawnedStateOffset;
        if (*isPlayerDead == 3) // 3 indicates the player is despawned. Lasts just as long as the respawn timer
        {
            return false;
        }
        byte isPlayerControllable = *(PointerToPlayerStruct + PlayerControlFlagsOffset);
        if ((isPlayerControllable & (1 << 3)) != 0)
        {
            return false;
        }
        if (*PlayerMaxHealth < 8) // Player has less than 8 health, which means they are on a rideable
        {
            return false;
        }
        byte* duelingCameraState = *(byte**)(Mod.BaseAddress + 0xC4ECB0) + 0x1FD1;
        if (*duelingCameraState == 1) // Player is in a duel, which means they can't die
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

            // Mod.Logger!.WriteLine($"Player Death Function Address: 0x{(nuint)deathWrapperAddress:X}");
            Mod.Logger!.WriteLine($"Player Lose Studs Function Address: 0x{(nuint)loseStudsAddress:X}");
            Mod.Logger!.WriteLine($"Player Spawn Studs Function Address: 0x{(nuint)spawnStudsAddress:X}");

            if (PointerToPlayerStruct == null)
            {
                Mod.Logger!.WriteLine("KillPlayer aborted: PointerToPlayerStruct is null.");
                return;
            }

            if (reduceStudTotalFunction == null)
            {
                Mod.Logger!.WriteLine("KillPlayer aborted: LoseStuds wrapper is null.");
                return;
            }

            if (spawnStudFunction == null)
            {
                Mod.Logger!.WriteLine("KillPlayer aborted: StudDropSpawner wrapper is null.");
                return;
            }

            Mod.Logger!.WriteLine($"Player Struct Address: 0x{(nuint)PointerToPlayerStruct:X}");

            uint studsLost = reduceStudTotalFunction((int)PointerToPlayerStruct, 1);
            Mod.Logger!.WriteLine($"Player Studs Lost: {studsLost}");
            // playerDeathFunction((int)PointerToPlayerStruct, 5, 0, 1, 0, 0); // Removed for now, was randomly crashing. It seemed to be corrupting other function call addresses

            WriteToPlayerState((ushort)EffectType.KillPlayer);

            if (studsLost == 0)
            {
                Mod.Logger!.WriteLine("No studs lost, skipping stud spawn.");
                return;
            }

            int worldObj = *(int*)(Mod.BaseAddress + 0xC5E358);
            Mod.Logger!.WriteLine($"World Object: 0x{(nuint)worldObj:X}");

            uint studLow = studsLost;
            uint studHigh = 0; // Current setup has stud loss capped at 2k (I think) so this should never be needed

            IntPtr unknownPlayerPtr0 = (int)PointerToPlayerStruct + 0xFCC;

            if (unknownPlayerPtr0 == IntPtr.Zero)
            {
                Mod.Logger!.WriteLine("KillPlayer aborted: unknownPlayerPtr0 is null.");
                return;
            }

            int unknownPlayerInt = *(PointerToPlayerStruct + 0x55);
            float unknownPlayerFloat = *(float*)(PointerToPlayerStruct + 0x1168);
            Mod.Logger!.WriteLine($"Unknown Player Ptr0: 0x{(nuint)unknownPlayerPtr0:X}");
            Mod.Logger!.WriteLine($"Unknown Player Int: {unknownPlayerInt}");
            Mod.Logger!.WriteLine($"Unknown Player Float: {unknownPlayerFloat}");

            spawnStudFunction(
                worldObj, studLow, studHigh, 0, 0, 0,
                unknownPlayerPtr0, 0, 0, unknownPlayerInt, 1.0f,
                unknownPlayerFloat, 0.0f, 1, 0, 0, 0, 0
            );
        }
        catch (Exception ex)
        {
            Mod.Logger!.WriteLine($"Exception during KillPlayer: {ex.Message}");
            Mod.Logger!.WriteLine($"Stack Trace: {ex.StackTrace}");
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
            Mod.Logger?.WriteLine("WriteToPlayerState aborted: PointerToPlayerStruct is null.");
            return;
        }

        *PlayerStateAnimation = value;
    }

    // Functions and variables relating to sending deaths when the player dies
    private readonly Queue<int> _outboundDeathLinkQueue = new();
    private readonly object _outboundDeathLinkQueueLock = new();
    private int _sendDeathAmnesty = amnesty;
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
    private int _nextOutboundDeathLinkId;
    private bool _isProcessingOutboundDeathLinks;
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
}
