using System.Collections;
using System.Collections.Generic;
using Photon;
using Photon.Pun;
using UnityEngine;
using TMPro;
using UnityEngine.UI;
using Photon.Realtime;

// assisted w/ Photon Pun documentation & https://www.youtube.com/watch?v=gxUCMOlISeQ&list=PLS6sInD7ThM0nJcyLfxgP9fnHKoQMJu-v

public class Matchmaker : MonoBehaviourPunCallbacks
{
    public GameObject findMatchPanel = null;
    public GameObject waitingStatusPanel = null;
    public TextMeshProUGUI waitingStatusText = null;

    private bool isConnecting = false;

    private const string GameVersion = "0.3.1-alpha.1";
    private const int MaxPlayersPerRoom = 2;

    private void Awake()
    {
        PhotonNetwork.AutomaticallySyncScene = true;
    }

    public void FindMatch()
    {
        isConnecting = true;

        findMatchPanel.SetActive(false);
        waitingStatusPanel.SetActive(true);

        waitingStatusText.text = "Searching...";

        if (PhotonNetwork.IsConnected)
        {
            PhotonNetwork.JoinRandomRoom(); // Join a game if you can
        } 
        else
        {
            PhotonNetwork.GameVersion = GameVersion;
            PhotonNetwork.ConnectUsingSettings();
        }
    }

    public override void OnConnectedToMaster()
    {
        Debug.Log("Connect To Master");

        if (isConnecting)
        {
            PhotonNetwork.JoinRandomRoom();  
        }
    }

    public override void OnDisconnected(DisconnectCause cause)
    {
        waitingStatusPanel.SetActive(false);
        findMatchPanel.SetActive(true);

        Debug.Log($"Disconnected due to: {cause}");
    }

    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        // No random rooms to join

        Debug.Log("No clients are witing for an opponent, creating a new room");

        // setting room name to null will just let Photon set a name

        PhotonNetwork.CreateRoom(null, new RoomOptions { MaxPlayers = MaxPlayersPerRoom });
    }

    public override void OnJoinedRoom()
    {
        Debug.Log("Client successfully joined a room");

        int playerCount = PhotonNetwork.CurrentRoom.PlayerCount;

        if(playerCount != MaxPlayersPerRoom)
        {
            waitingStatusText.text = "Waiting For Opponent";
            Debug.Log("Client is waiting for an opponent");
        }
        else
        {
            waitingStatusText.text = "Opponent Found";
            Debug.Log("Matching is ready to begin");
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        if(PhotonNetwork.CurrentRoom.PlayerCount == MaxPlayersPerRoom)
        {
            PhotonNetwork.CurrentRoom.IsOpen = false;

            waitingStatusText.text = "Opponent Found";
            Debug.Log("Match is ready to begin");

            PhotonNetwork.LoadLevel("Miami Beach");
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
