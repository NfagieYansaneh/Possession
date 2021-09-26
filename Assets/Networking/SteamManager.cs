using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;
using System;
using System.Threading.Tasks;
using Steamworks.Data;

// assist from https://www.youtube.com/watch?v=xJl3yHjhils&t=65s & https://wiki.facepunch.com/steamworks
public class SteamManager : MonoBehaviour
{
    public uint appId = 480;
    public SteamId PlayerSteamId;
    public SteamId OpponentSteamId;
    public string gameVersion;

    public static SteamSocketManager steamSocketManager;

    public static SteamConnectionManager steamConnectionManager;

    [HideInInspector]
    public bool activeSteamSocketServer = false;

    [HideInInspector]
    public bool activeSteamSocketConnect = false;

    [HideInInspector]
    public bool activeSteamSocketConnection = false;

    public bool NOT_HOST = true;
    public Lobby hostedMultiplayerLobby;
    public Lobby currentLobby;
    bool LobbyPartnerDisconnected = false;
    public List<Lobby> lobbyList = new List<Lobby>();

    Steamworks.ServerList.Internet Request;

    public void Awake()
    {
        try
        {
            Steamworks.SteamClient.Init(appId, true);
        }
        catch (System.Exception e)
        {
            Debug.Log($"Couldn't init steam client, {e.Message}");
            // Something went wrong! Steam is closed?
        }

        // Helpful to reduce time to use SteamNetworkingSockets later
        SteamNetworkingUtils.InitRelayNetworkAccess();
        PlayerSteamId = SteamClient.SteamId;
    }

    void Update()
    {
        SteamClient.RunCallbacks();

        try
        {
            if (activeSteamSocketServer)
            {
                steamSocketManager.Receive();
            }
            if (activeSteamSocketConnection)
            {
                steamConnectionManager.Receive();
            }
        }
        catch
        {
            Debug.Log("Error receiving data on socket/connection");
        }
    }

    public async void FindMatch()
    {
        NOT_HOST = true;

        if (await RefreshMultiplayerLobbies())
        {
            if (await JoinLobby())
            {
                JoinSteamSocketServer(currentLobby.Owner.Id);
            }
        }
    }

    public async void HostMatch()
    {
        NOT_HOST = false;

        if(!(await CreateSteamSocketServer())){
            Debug.Log("Couldn't create lobby");
        }
    }

    private async Task<bool> CreateSteamSocketServer()
    {
        steamSocketManager = SteamNetworkingSockets.CreateRelaySocket<SteamSocketManager>(0);
        // Host needs to connect to own socket server with a ConnectionManager to send/receive messages
        // Relay Socket servers are created/connected to through SteamIds rather than "Normal" Socket Servers which take IP addresses
        steamConnectionManager = SteamNetworkingSockets.ConnectRelay<SteamConnectionManager>(PlayerSteamId);

        if(!(await CreateLobby(2)))
        {
            return false;
        }

        activeSteamSocketServer = true;
        activeSteamSocketConnection = true;

        return true;
    }

    public async Task<bool> CreateLobby(int lobbySize, string map= "miami beach", string gamemode= "1v1")
    {
        try
        {
            var createLobbyOutput = await SteamMatchmaking.CreateLobbyAsync(lobbySize);
            if (!createLobbyOutput.HasValue)
            {
                Debug.Log("Lobby created but not correctly instantiated");
                throw new Exception();
            }

            LobbyPartnerDisconnected = false;
            hostedMultiplayerLobby = createLobbyOutput.Value;
            hostedMultiplayerLobby.SetPublic();
            hostedMultiplayerLobby.SetJoinable(true);
        
            hostedMultiplayerLobby.SetData("map", map);
            hostedMultiplayerLobby.SetData("gamemode", gamemode);
            hostedMultiplayerLobby.SetData("gameVersion", gameVersion);

            currentLobby = hostedMultiplayerLobby;

            return true;
        }
        catch (Exception exception)
        {
            Debug.Log("Failed to create multiplayer lobby");
            Debug.Log(exception.ToString());
            return false;
        }
    }

    public async Task<bool> RefreshMultiplayerLobbies(string map = "miami beach", string gamemode = "1v1")
    {
        try
        {
            //activeUnrankedLobbies.Clear();
            lobbyList.Clear();
            Lobby[] lobbies = await SteamMatchmaking.LobbyList.WithMaxResults(20).WithKeyValue("map", map).WithKeyValue("gamemode", gamemode).WithKeyValue("gameVersion", gameVersion).RequestAsync();
            if (lobbies != null)
            {
                foreach (Lobby lobby in lobbies)
                {
                    lobbyList.Add(lobby);
                    //activeUnrankedLobbies.Add(lobby);
                    //could later display this on a server list
                }
            }
            return true;
        }
        catch (Exception e)
        {
            Debug.Log(e.ToString());
            Debug.Log("Error fetching multiplayer lobbies");
            return true;
        }
    }

    public async Task<bool> JoinLobby()
    {
        Debug.Log($"Attempting to join {lobbyList[0].Id} out of {lobbyList.Count} lobbies");

        RoomEnter joinedLobbySuccess = await lobbyList[0].Join();
        if (joinedLobbySuccess != RoomEnter.Success)
        {
            Debug.Log("failed to join lobby");
            return false;
        }

        currentLobby = lobbyList[0];
        
        return true;
    }

    private void JoinSteamSocketServer(SteamId hostSteamId)
    {
        if (NOT_HOST)
        {
            Debug.Log("joining socket server");
            steamConnectionManager = SteamNetworkingSockets.ConnectRelay<SteamConnectionManager>(hostSteamId, 0);
            activeSteamSocketServer = false;
            activeSteamSocketConnection = true;
        }
    }

    private void LeaveSteamSocketServer()
    {
        activeSteamSocketServer = false;
        activeSteamSocketConnection = false;
        try
        {
            // Shutdown connections/sockets. I put this in try block because if player 2 is leaving they don't have a socketManager to close, only connection
            steamConnectionManager.Close();
            steamSocketManager.Close();
        }
        catch
        {
            Debug.Log("Error closing socket server / connection manager");
        }
    }

    private void OnApplicationQuit()
    {
        LeaveSteamSocketServer();
        Steamworks.SteamClient.Shutdown();
    }

    public static void RelaySocketMessageReceived(IntPtr message, int size, uint connectionSendingMessageId)
    {
        try
        {
            // Loop to only send messages to socket server members who are not the one that sent the message
            for (int i = 0; i < steamSocketManager.Connected.Count; i++)
            {
                if (steamSocketManager.Connected[i].Id != connectionSendingMessageId)
                {
                    Result success = steamSocketManager.Connected[i].SendMessage(message, size);
                    if (success != Result.OK)
                    {
                        Result retry = steamSocketManager.Connected[i].SendMessage(message, size);
                    }
                }
            }
        }
        catch
        {
            Debug.Log("Unable to relay socket server message");
        }
    }

    public bool SendMessageToSocketServer(byte[] messageToSend)
    {
        try
        {
            // Convert string/byte[] message into IntPtr data type for efficient message send / garbage management
            int sizeOfMessage = messageToSend.Length;
            IntPtr intPtrMessage = System.Runtime.InteropServices.Marshal.AllocHGlobal(sizeOfMessage);
            System.Runtime.InteropServices.Marshal.Copy(messageToSend, 0, intPtrMessage, sizeOfMessage);
            Result success = steamConnectionManager.Connection.SendMessage(intPtrMessage, sizeOfMessage, SendType.Reliable);
            if (success == Result.OK)
            {
                System.Runtime.InteropServices.Marshal.FreeHGlobal(intPtrMessage); // Free up memory at pointer
                return true;
            }
            else
            {
                // RETRY
                Result retry = steamConnectionManager.Connection.SendMessage(intPtrMessage, sizeOfMessage, SendType.Reliable);
                System.Runtime.InteropServices.Marshal.FreeHGlobal(intPtrMessage); // Free up memory at pointer
                if (retry == Result.OK)
                {
                    return true;
                }
                return false;
            }
        }
        catch (Exception e)
        {
            Debug.Log(e.Message);
            Debug.Log("Unable to send message to socket server");
            return false;
        }
    }

    public static void ProcessMessageFromSocketServer(IntPtr messageIntPtr, int dataBlockSize)
    {
        try
        {
            byte[] message = new byte[dataBlockSize];
            System.Runtime.InteropServices.Marshal.Copy(messageIntPtr, message, 0, dataBlockSize);
            string messageString = System.Text.Encoding.UTF8.GetString(message);

            // Do something with received message

        }
        catch
        {
            Debug.Log("Unable to process message from socket server");
        }
    }
}
