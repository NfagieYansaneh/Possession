using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;
using Steamworks.Data;
using System;

public class Client : MonoBehaviour
{
	// static MyServer server;
	public void P2PLookForSessionRequest()
	{
		SteamNetworking.OnP2PSessionRequest = (steamid) =>
		{
			// If we want to let this steamid talk to us
			SteamNetworking.AcceptP2PSessionWithUser(steamid);
		};

		// https://www.speedguide.net/port.php?port=21893
		// server = SteamNetworkingSockets.CreateNormalSocket<MyServer>(NetAddress.AnyIp(21893));
	}

	public void FixedUpdate()
    {
		while (SteamNetworking.IsP2PPacketAvailable())
		{
			var packet = SteamNetworking.ReadP2PPacket();
			if (packet.HasValue)
			{
				HandleMessageFrom(packet.Value.SteamId, packet.Value.Data);
			}
		}
	}

	void HandleMessageFrom(SteamId steamid, byte[] data)
	{
		Debug.Log($"{steamid} just sent you a message!");
		foreach(byte b in data)
        {
			Debug.Log(b);
        }
	}
}

/* //
// Kick everyone
// 
foreach ( var connection in Connected )
{
	connection.Close();
} */