using LHP2_Archi_Mod.Addresses;

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
    // Memory offsets for the Player struct (they differ on the Collection, see Off)
    private static int PlayerStateOffset1 => Off.PlayerStateEffect;
    private static int PlayerStateOffset2 => Off.PlayerStateAnimation;
    private static int PlayerEffectTimer => Off.PlayerEffectTimer;
    private static int PlayerMaxHealthOffset => Off.PlayerMaxHealth;
    private static int PlayerCurrentHealthOffset => Off.PlayerCurrentHealth;
    private static int PlayerDespawnedStateOffset => Off.PlayerDespawnedState;
    private static int PlayerControlFlagsOffset => Off.PlayerControlFlags;
    private static int PlayerDamageInvulnerabilityTimerOffset => Off.PlayerInvulnerabilityTimer;
    private static int PlayerDeathValueOffset => Off.PlayerDeathValue;

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

    // checkScreen false: the death message itself is on screen when the deferred death runs
    private bool CanPlayerReceiveNegativeEffect(bool checkScreen = true)
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
        byte* duelingCameraState = *(byte**)Addr.DuelCameraPtr + Off.DuelCameraState;
        if (*duelingCameraState == 1) // Player is in a duel, which means they can't die
        {
            return false;
        }
        float* damageInvulnerabilityTimer = (float*)(PointerToPlayerStruct + PlayerDamageInvulnerabilityTimerOffset);
        if (*damageInvulnerabilityTimer > 2) // Set to 2 cause changing map is 3 seconds and changing character is 2.5 seconds
        {
            return false;
        }
        if (checkScreen)
        {
            (bool nothingOnScreen, bool hubCutscene) = HintSystem.GetScreenAndCutsceneState();
            if (!nothingOnScreen || !hubCutscene)
            {
                return false;
            }
        }
        int deathValue = *(int*)(PointerToPlayerStruct + PlayerDeathValueOffset);
        if ((deathValue & 0xFFFF) == 0x300)
        {
            if (checkScreen)
                Game.PrintToLog("Player can die");
            return true;
        }
        return false;
    }

    public void KillPlayer()
    {
        if (GameVersion.Is64)
        {
            KillPlayer64();
            return;
        }

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
                (long)Addr.LoseStudsFunction,
                out nint loseStudsAddress
            );

            var spawnStudFunction = Mod._hooks!.CreateWrapper<Game.StudDropSpawner>(
                (long)Addr.StudDropSpawnerFunction,
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

            int worldObj = *(int*)Addr.WorldObjectPtr;
            Mod.Logger!.WriteLine($"World Object: 0x{(nuint)worldObj:X}");

            uint studLow = studsLost;
            uint studHigh = 0; // Current setup has stud loss capped at 2k (I think) so this should never be needed

            IntPtr unknownPlayerPtr0 = (int)PointerToPlayerStruct + Off.PlayerStudDropPosition;

            if (unknownPlayerPtr0 == IntPtr.Zero)
            {
                Mod.Logger!.WriteLine("KillPlayer aborted: unknownPlayerPtr0 is null.");
                return;
            }

            int unknownPlayerInt = *(PointerToPlayerStruct + Off.PlayerDeathValue);
            float unknownPlayerFloat = *(float*)(PointerToPlayerStruct + Off.PlayerStudDropFloat);
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

    // 64-bit: the game's stud functions crash off its thread, so the death runs on the next frame
    private volatile bool _pendingKill64;

    private void KillPlayer64()
    {
        _pendingKill64 = true;
        // Wait for the frame to apply it: a death queued meanwhile would merge into this one
        while (_pendingKill64)
            Thread.Sleep(10);
    }

    // Called by the per-frame hook, on the game thread
    public void RunPendingKill64()
    {
        if (!_pendingKill64)
            return;
        if (PointerToPlayerStruct == null)
        {
            _pendingKill64 = false;
            Mod.Logger!.WriteLine("KillPlayer aborted: PointerToPlayerStruct is null.");
            return;
        }
        // Deferred past a menu or a load: the player may no longer be able to die, retry next frame
        if (!CanPlayerReceiveNegativeEffect(checkScreen: false))
            return;
        _pendingKill64 = false;
        try
        {

            if (Addr.LoseStudsFunction == 0 || Addr.StudDropSpawnerFunction == 0 || Addr.WorldObjectPtr == 0)
            {
                Mod.Logger!.WriteLine("LoseStuds, StudDropSpawner or the world object not found: death only.");
                WriteToPlayerState((ushort)EffectType.KillPlayer);
                return;
            }

            var loseStuds = (delegate* unmanaged<nint, int, ulong>)Addr.LoseStudsFunction;
            ulong studsLost = loseStuds((nint)PointerToPlayerStruct, 1);
            Mod.Logger!.WriteLine($"Player Studs Lost: {studsLost}");

            WriteToPlayerState((ushort)EffectType.KillPlayer);

            nint worldObj = *(nint*)Addr.WorldObjectPtr;
            if (studsLost == 0 || worldObj == 0)
                return;

            // The spawner takes the stud count as one argument: 17 arguments instead of 18
            nint studDropPosition = (nint)(PointerToPlayerStruct + Off.PlayerStudDropPosition);
            int deathValue = *(PointerToPlayerStruct + Off.PlayerDeathValue);
            float playerFloat = *(float*)(PointerToPlayerStruct + Off.PlayerStudDropFloat);
            var spawner = (delegate* unmanaged<nint, ulong, int, long, uint, long, long, long, int, float, float, long, int, int, byte, byte, int, void>)Addr.StudDropSpawnerFunction;
            spawner(worldObj, studsLost, 0, 0, 0, studDropPosition, 0, 0, deathValue, 1.0f, playerFloat, 0, 1, 0, 0, 0, 0);
        }
        catch (Exception ex)
        {
            Mod.Logger!.WriteLine($"Exception during KillPlayer: {ex.Message}");
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
