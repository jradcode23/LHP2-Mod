using System.Collections.Concurrent;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using System.Diagnostics.CodeAnalysis;
using Archipelago.MultiClient.Net.Models;

namespace LHP2_Archi_Mod;

// Thank you Sonic Heros AP Devs for being the implementation example

public class ArchipelagoHandler
{
    private const string GAME_NAME = "Lego Harry Potter 5-7";
    private ArchipelagoSession _session;
    private LoginSuccessful _loginSuccessful;
    public SlotData SlotDataInstance;
    private static unsafe byte* NewGameTextPTR => *(byte**)(Mod.BaseAddress + 0xC4EB9C) + 0x32E;

    private string Server { get; set; }
    private int Port { get; set; }
    private string Slot { get; set; }
    private string? Seed { get; set; }
    private string Password { get; set; }

    public static bool IsConnected;
    public static bool IsConnecting;
    public static int gameOffset = 400000;

    public ArchipelagoHandler(string server, int port, string slot, string password)
    {
        Server = server;
        Port = port;
        Slot = slot;
        Password = password;
        CreateSession();
    }

    [MemberNotNull(nameof(_session))]
    private void CreateSession()
    {
        _session = ArchipelagoSessionFactory.CreateSession(Server, Port);
        _session.MessageLog.OnMessageReceived += OnMessageReceived;
        _session.Socket.SocketClosed += OnSocketClosed;
        // _session.Socket.PacketReceived += OnPacketReceived;
        _session.Items.ItemReceived += ItemReceived;
    }

    public void InitConnect()
    {
        IsConnecting = true;
        CreateSession();
        IsConnected = Connect();
        IsConnecting = false;
    }

    // Tells the server what we want done when the connection is closed
    private void OnSocketClosed(string reason)
    {
        Game.PrintToLog($"Connection closed ({reason}) Attempting reconnect...");
        IsConnected = false;
    }

    // Attempts to connect to the server
    private unsafe bool Connect()
    {
        LoginResult result;



        try
        {
            // Check to see if Game/Menu is loaded before trying to connect, we do this to mitigate impact of null values and the game changing things later.
            Game.IsGameLoaded();
            HintSystem.SetMessageText("Connecting", (uint)NewGameTextPTR);
            Seed = _session.ConnectAsync()?.Result?.SeedName;
            Game.PrintToLog(Seed + Slot);

            result = _session.LoginAsync(
                game: GAME_NAME,
                name: Slot,
                itemsHandlingFlags: ItemsHandlingFlags.AllItems,
                version: new Version(1, 0, 0),
                tags: [],
                password: Password
            ).Result;
        }
        catch (Exception e)
        {
            result = new LoginFailure(e.GetBaseException().Message);
            HintSystem.SetMessageText("Failed To Connect", (uint)NewGameTextPTR);
        }

        if (result.Successful)
        {
            _loginSuccessful = (LoginSuccessful)result;
            // Sets up slot Data
            SlotDataInstance = new(_loginSuccessful.SlotData);
            SlotDataInstance.PrintData();
            HintSystem.SetMessageText("Hooking, Please Wait", (uint)NewGameTextPTR);
            // Modify the game now that we are connected
            bool isHooked = Mod.InitOnMenu();
            // Store our slot name in game to access later
            Mod.GameInstance!.PlayerName = Slot;
            // Tell DataStorage we are on the menu
            _session.DataStorage[Scope.Slot, "map"] = 402;
            if (isHooked)
            {
                HintSystem.SetMessageText("Ready to Play, New Game", (uint)NewGameTextPTR);
            }
            else
            {
                HintSystem.SetMessageText("Failed To Hook", (uint)NewGameTextPTR);
            }
            // Start our threads
            new Thread(RunCheckLocationsFromList).Start();
            new Thread(HintSystem.HandleMessages).Start();
            new Thread(HandleQueuedItems).Start();
            //resync here
            return true;
        }
        var failure = (LoginFailure)result;
        var errorMessage = $"Failed to Connect to {Server}:{Port} as {Slot}";
        errorMessage = failure.Errors.Aggregate(errorMessage, (current, error) => current + $"\n    {error}");
        errorMessage = failure.ErrorCodes.Aggregate(errorMessage, (current, error) => current + $"\n    {error}");
        Game.PrintToLog(errorMessage);
        Game.PrintToLog($"Attempting reconnect...");
        return false;
    }

    /* Tells archi want we want to do when an item is received. 
    Majority of items besides the groupings in this function are given to the player when they sync up with the item/location container. 
    However, there are a few we want the player to have instantaneously so we handle those instances here.
    */
    private void ItemReceived(ReceivedItemsHelper helper)
    {
        while (helper.Any())
        {
            var item = helper.DequeueItem();
            int gameID = (int)item.ItemId - gameOffset;
            bool prevInShop = false, prevInLevelSelect = false;

            // Verify if the player is in a shop or in the level selector since we give them certain items instantly if they are
            if (Mod.GameInstance != null)
            {
                lock (Mod.GameInstance.StateLock)
                {
                    prevInShop = Mod.GameInstance.PrevInShop;
                    prevInLevelSelect = Mod.GameInstance.PrevInLevelSelect;
                }
            }

            if (Mod.GameInstance != null && prevInShop && gameID != 699)
            {
                // Token or red brick purchasable
                if ((gameID >= 900 && gameID <= 935) || (gameID >= 213 && gameID <= 425))
                {
                    Game.ManageItem(gameID);
                }
                return;
            }
            // If in level select, we give them any item but purple stud
            if (Mod.GameInstance != null && prevInLevelSelect && gameID != 699)
            {
                Game.ManageItem(gameID);
                return;
            }
            // If the player is controllable, we give them purple studs, any spells/abilities, and Horcruxes
            if (Mod.GameInstance != null && Game.IsPlayerControllable())
            {
                // Handle Spells/Abilities
                if (gameID >= 998)
                {
                    Game.ManageItem(gameID);
                    return;
                }
                // Handle Purple Studs
                if (gameID == 699)
                {
                    HubHandler.HandlePurpleStud();
                    return;
                }
                //Handle Horcruxes
                if (gameID >= 440 && gameID <= 446)
                {
                    HubHandler.UpdateHorcruxCount();
                    return;
                }
            }
            // Handle Purple Stud if player isn't controllable (could be in the menu)
            if (Mod.GameInstance != null && !Game.IsPlayerControllable() && gameID == 699)
            {
                _queuedItems.Enqueue(gameID);
                return;
            }
        }
    }

    // Once Goal is completed, this function is called
    public void Release()
    {
        _session.SetGoalAchieved();
        _session.SetClientState(ArchipelagoClientState.ClientGoal);
    }

    // This is the function we use to send completed locations. Pass the ID to this function and it will be marked complete.
    public void CheckLocation(Int64 id)
    {
        _locationsToCheck.Enqueue(id + gameOffset);
    }

    private ConcurrentQueue<Int64> _locationsToCheck = new();
    private ConcurrentQueue<Int64> _queuedItems = new();
    private readonly object _locationsLock = new();
    private readonly object _itemsLock = new();

    // This functions is a separate thread that will tell the server that our player has complete their location checks. Done in a separate thread to keep the game running and not relying on the server.
    public void RunCheckLocationsFromList()
    {
        while (true)
        {
            if (_locationsToCheck.TryDequeue(out var locationId))
                _session.Locations.CompleteLocationChecks(locationId);
            else
            {
                Thread.Sleep(100);
            }
        }
    }

    // If there are instantenous items (like purple studs and in the future traps) that we queue up because the player wasn't controllable, this is a separate thread that waits until the player is controllable to pass them the item. 
    public void HandleQueuedItems()
    {
        while (true)
        {
            if (_queuedItems.TryPeek(out var itemId))
            {
                if (Mod.GameInstance != null && Game.IsPlayerControllable())
                {
                    if (_queuedItems.TryDequeue(out itemId))
                    {
                        // Console.WriteLine($"Handling queued item with game ID {itemId}");
                        if (itemId == 699)
                        {
                            HubHandler.HandlePurpleStud();
                        }
                    }
                }
                else
                {
                    Thread.Sleep(100);
                }
            }
            else
            {
                Thread.Sleep(100);
            }
        }
    }

    // Helper function to verify if a location has already been completed (includes if it was collected by server)
    public bool IsLocationChecked(Int64 id)
    {
        lock (_locationsLock)
        {
            return _session.Locations.AllLocationsChecked.Contains(id + gameOffset);
        }
    }

    /*
    This archi is designed to sync the game state to either the items received state, the locations completed state, or a mixture of both.
    These following functions take the range of archi IDs as parameters and will update the game state to match whether the item has been received or the location checked.
    */

    // Updates Game state to Items
    public void UpdateBasedOnItems(Int64 minItemId, Int64 maxItemId)
    {
        lock (_itemsLock)
        {
            foreach (var item in _session.Items.AllItemsReceived)
            {
                // Only process items whose AP item ID falls within the desired range
                if (item.ItemId - gameOffset < minItemId || item.ItemId - gameOffset > maxItemId)
                    continue;

                var gameId = item.ItemId - gameOffset;
                Game.ManageItem((int)gameId);
            }
        }
    }

    // Updates Game state to locations
    public void UpdateBasedOnLocations(Int64 minLocationId, Int64 maxLocationId)
    {
        lock (_locationsLock)
        {
            foreach (var location in _session.Locations.AllLocationsChecked)
            {
                // Only handle locations within the desired ID range
                if (location - gameOffset < minLocationId || location - gameOffset > maxLocationId)
                    continue;

                var gameId = location - gameOffset;
                Game.ManageItem((int)gameId);
            }
        }
    }

    // Helper Function to count how many items inbetween a certain archi ID range have been received
    public int CountItemsCheckedInRange(Int64 start, Int64 end)
    {
        lock (_itemsLock)
        {
            var startId = start + gameOffset;
            var endId = end + gameOffset;
            return _session.Items.AllItemsReceived.Count(item => item.ItemId >= startId && item.ItemId <= endId);
        }
    }

    // Helper Function to count how many items with a specific archi ID (i.e. gold brick or purple stud) have been received
    public int CountItemsReceivedWithId(Int64 gameId)
    {
        lock (_itemsLock)
        {
            var targetId = gameId + gameOffset;
            return _session.Items.AllItemsReceived.Count(item => item.ItemId == targetId);
        }
    }

    // Helper Function to handle Messages received from the server
    private static void OnMessageReceived(LogMessage message)
    {
        byte itemFlag;
        switch (message)
        {
            // Implementation for when hint messages are received from the server
            case HintItemSendLogMessage hintMessage:
                // Currently printing all hint messages in case it breaks something
                Game.PrintToLog($"Hint Message Received: {hintMessage.ToString() ?? string.Empty}");
                itemFlag = GetItemFlag(hintMessage.Item);
                string hntmsg = hintMessage.ToString() ?? string.Empty;

                if (!hintMessage.IsRelatedToActivePlayer)
                {
                    Game.PrintToLog("Hint not related to active player, skipping");
                    return;
                }

                HintSystem.EnqueueMessage(hntmsg, itemFlag);
                break;
            case ItemSendLogMessage itemMessage:
                // Currently printing all Item messages in case it breaks something
                Game.PrintToLog($"Item Message Received: {itemMessage.ToString() ?? string.Empty}");
                itemFlag = GetItemFlag(itemMessage.Item);
                string itmmsg = itemMessage.ToString() ?? string.Empty;

                if (!itemMessage.IsRelatedToActivePlayer)
                {
                    Game.PrintToLog($"Item not related to active player, skipping");
                    return;
                }

                HintSystem.EnqueueMessage(itmmsg, itemFlag);
                break;
            default:
                /* 
                Printing all messages in case it breaks something, but don't think that will be the case.
                Prints the message type too in case there is something we want to print in the future
                */
                Game.PrintToLog($"{message.GetType().Name} Received: {message.ToString() ?? string.Empty}");
                bool isCheatMessage = message.ToString() != null && message.ToString().Contains("Cheat console");
                if (isCheatMessage)
                {
                    HintSystem.EnqueueMessage(message.ToString(), 3); // If we get a cheat console message, we want to print it
                }
                break;
        }
    }

    // Helper function to help determine what type of item (progression/Trap/Filler) was sent/received. Used to determine the in game text color.
    private static byte GetItemFlag(ItemInfo item)
    {
        Game.PrintToLog($"Item flags are: {item.Flags}");
        if ((item.Flags & ItemFlags.Trap) == ItemFlags.Trap)
        {
            return 0; // 0 is Red
        }
        if ((item.Flags & ItemFlags.Advancement) == ItemFlags.Advancement)
        {
            return 3; // 3 is Purple
        }
        if ((item.Flags & ItemFlags.NeverExclude) == ItemFlags.NeverExclude)
        {
            return 6; // 6 is Dark Blue
        }
        if ((item.Flags & ItemFlags.None) == ItemFlags.None)
        {
            return 2; // 2 is Light Blue
        }
        return 5; // Default flag which is gray
    }

    // We pass the Map ID to data storage for auto tracking purposes
    public void SendMapID(int MapID)
    {
        _session.DataStorage[Scope.Slot, "map"] = MapID;
    }

}