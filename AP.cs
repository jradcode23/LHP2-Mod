using System.Collections.Concurrent;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Helpers;
using Archipelago.MultiClient.Net.MessageLog.Messages;


namespace LHP2_Archi_Mod;

// Thank you Sonic Heros AP Devs for being the implementation example

public class LHP2_Archipelago
{
    private const string GAME_NAME = "Lego Harry Potter 5-7";
    private ArchipelagoSession _session;
    private LoginSuccessful _loginSuccessful;

    private string Server { get; set; }
    private int Port { get; set; }
    private string Slot { get; set; }
    private string? Seed { get; set; }
    private string Password { get; set; }
    private double SlotInstance { get; set; }

    public static bool IsConnected;
    public static bool IsConnecting;

    public LHP2_Archipelago(string server, int port, string slot, string password)
    {
        Server = server;
        Port = port;
        Slot = slot;
        Password = password;
        CreateSession();
    }

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
        Console.WriteLine($"Connection closed ({reason}) Attempting reconnect...");
        IsConnected = false;
    }

    private bool Connect()
    {
        LoginResult result;

        try
        {
            Game.CheckGameLoaded();
            Seed = _session.ConnectAsync()?.Result?.SeedName;
            Console.WriteLine(Seed + Slot);


            result = _session.LoginAsync(
                game: GAME_NAME,
                name: Slot,
                itemsHandlingFlags: ItemsHandlingFlags.AllItems,
                version: new Version(1, 0, 0),
                tags: new string[] { },
                password: Password
            ).Result;

            Game.CheckSaveFileLoaded();
        }
        catch (Exception e)
        {
            result = new LoginFailure(e.GetBaseException().Message);
        }

        if (result.Successful)
        {
            _loginSuccessful = (LoginSuccessful)result;
            //SlotData = new SlotData(_loginSuccessful.SlotData);
            new Thread(RunCheckLocationsFromList).Start();
            //resync here
            return true;
        }
        var failure = (LoginFailure)result;
        var errorMessage = $"Failed to Connect to {Server}:{Port} as {Slot}:";
        errorMessage = failure.Errors.Aggregate(errorMessage, (current, error) => current + $"\n    {error}");
        errorMessage = failure.ErrorCodes.Aggregate(errorMessage, (current, error) => current + $"\n    {error}");
        Console.WriteLine(errorMessage);
        Console.WriteLine($"Attempting reconnect...");
        return false;
    }

    private void ItemReceived(ReceivedItemsHelper helper)
    {
        while (helper.Any())
        {
            var itemIndex = helper.Index;
            var item = helper.DequeueItem();
            // Mod.GameInstance?.ManageItem(itemIndex, item); //TODO: Add back in once non collectible items are a thing
            // TODO: add check to see if in shop when receiving item
        }
    }

    public void Release()
    {
        _session.SetGoalAchieved();
        _session.SetClientState(ArchipelagoClientState.ClientGoal);
    }

    public void CheckLocations(Int64[] ids)
    {
        ids.ToList().ForEach(id => _locationsToCheck.Enqueue(id + 400000));
    }

    public void CheckLocation(Int64 id)
    {
        _locationsToCheck.Enqueue(id + 400000);
    }

    private ConcurrentQueue<Int64> _locationsToCheck = new();

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

    public bool IsLocationChecked(Int64 id)
    {
        return _session.Locations.AllLocationsChecked.Contains(id + 400000);
    }

    public void UpdateItemsReceived()
    {
        foreach (var item in _session.Items.AllItemsReceived)
        {
            var gameId = item.ItemId - 400000;
            Mod.GameInstance!.ManageItem((int)gameId);
        }
    }

    public void UpdateLocationsChecked()
    {
        foreach (var location in _session.Locations.AllLocationsChecked)
        {
            var gameId = location - 400000;
            Mod.GameInstance!.ManageItem((int)gameId);
        }
    }

    public int CountLocationsCheckedInRange(Int64 start, Int64 end)
    {
        var startId = start + 400000;
        var endId = end + 400000;
        return _session.Locations.AllLocationsChecked.Count(loc => loc >= startId && loc < endId);
    }

    static void OnMessageReceived(LogMessage message)
    {
        Console.WriteLine(message.ToString() ?? string.Empty);
    }
}