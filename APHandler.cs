using System.Collections.Concurrent;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.MessageLog.Messages;
using System.Diagnostics.CodeAnalysis;

namespace LHP2_Archi_Mod;

// Thank you Sonic Heros AP Devs for being the implementation example

public class ArchipelagoHandler
{
    private const string GAME_NAME = "Lego Harry Potter 5-7";
    private ArchipelagoSession _session;
    private LoginSuccessful _loginSuccessful;
    public SlotData SlotDataInstance;

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
        //_session.Socket.PacketReceived += PacketReceived;
        _session.Items.ItemReceived += ItemReceived;
    }

    public void InitConnect()
    {
        IsConnecting = true;
        CreateSession();
        IsConnected = Connect();
        IsConnecting = false;
    }

    private void OnSocketClosed(string reason)
    {
        Mod.Logger!.WriteLineAsync($"Connection closed ({reason}) Attempting reconnect...");
        IsConnected = false;
    }

    private bool Connect()
    {
        LoginResult result;

        try
        {
            Game.CheckGameLoaded();
            Seed = _session.ConnectAsync()?.Result?.SeedName;
            Mod.Logger!.WriteLineAsync(Seed + Slot);

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
        }

        if (result.Successful)
        {
            _loginSuccessful = (LoginSuccessful)result;
            SlotDataInstance = new(_loginSuccessful.SlotData);
            SlotDataInstance.PrintData();
            Mod.InitOnMenu();
            _session.DataStorage[Scope.Slot, "map"] = 402;
            new Thread(RunCheckLocationsFromList).Start();
            new Thread(HandleQueuedItems).Start();
            //resync here
            return true;
        }
        var failure = (LoginFailure)result;
        var errorMessage = $"Failed to Connect to {Server}:{Port} as {Slot}";
        errorMessage = failure.Errors.Aggregate(errorMessage, (current, error) => current + $"\n    {error}");
        errorMessage = failure.ErrorCodes.Aggregate(errorMessage, (current, error) => current + $"\n    {error}");
        Mod.Logger!.WriteLineAsync(errorMessage);
        Mod.Logger!.WriteLineAsync($"Attempting reconnect...");
        return false;
    }

    private void ItemReceived(ReceivedItemsHelper helper)
    {
        while (helper.Any())
        {
            var item = helper.DequeueItem();
            int gameID = (int)item.ItemId - gameOffset;
            if(Mod.GameInstance != null && Mod.GameInstance.PrevInShop && gameID != 699)
            {
                // Token or red brick purchasable
                if((gameID >= 900 && gameID <= 935) || (gameID >= 213 && gameID <= 425))
                {
                    Game.ManageItem(gameID);
                }
                return;
            }
            if(Mod.GameInstance != null && Mod.GameInstance.PrevInLevelSelect && gameID != 699)
            {
                Game.ManageItem(gameID);
                return;
            }
            if(Mod.GameInstance != null && Game.PlayerControllable())
            {
                if(gameID >= 998)
                {
                    Game.ManageItem(gameID);
                    return;
                }
                if(gameID == 699)
                {
                    HubHandler.HandlePurpleStud();
                    return;
                }
            }
            if(Mod.GameInstance != null && !Game.PlayerControllable() && gameID == 699)
            {
                // Console.WriteLine($"Queuing Purple Stud for later handling with game ID {gameID}");
                _queuedItems.Enqueue(gameID);
                return;
            }
        }
    }

    public void Release()
    {
        _session.SetGoalAchieved();
        _session.SetClientState(ArchipelagoClientState.ClientGoal);
    }

    public void CheckLocation(Int64 id)
    {
        _locationsToCheck.Enqueue(id + gameOffset);
    }

    private ConcurrentQueue<Int64> _locationsToCheck = new();
    private ConcurrentQueue<Int64> _queuedItems = new();
    private readonly object _locationsLock = new();
    private readonly object _itemsLock = new();

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

    public void HandleQueuedItems()
    {
        while (true)
        {
            if (_queuedItems.TryPeek(out var itemId))
            {
                if (Mod.GameInstance != null && Game.PlayerControllable())
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

    public bool IsLocationChecked(Int64 id)
    {
        lock (_locationsLock)
        {
            return _session.Locations.AllLocationsChecked.Contains(id + gameOffset);
        }
    }

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

    public int CountItemsCheckedInRange(Int64 start, Int64 end)
    {
        lock (_itemsLock)
        {
            var startId = start + gameOffset;
            var endId = end + gameOffset;
            return _session.Items.AllItemsReceived.Count(item => item.ItemId >= startId && item.ItemId <= endId);
        }
    }

    public int CountItemsReceivedWithId(Int64 gameId)
    {
        lock (_itemsLock)
        {
            var targetId = gameId + gameOffset;
            return _session.Items.AllItemsReceived.Count(item => item.ItemId == targetId);
        }
    }

    static void OnMessageReceived(LogMessage message)
    {
        Mod.Logger!.WriteLineAsync(message.ToString() ?? string.Empty);
    }

    public void SendMapID(int MapID)
    {
        _session.DataStorage[Scope.Slot,"map"] = MapID;
    }

}