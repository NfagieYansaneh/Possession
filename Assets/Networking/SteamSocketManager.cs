using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;
using Steamworks.Data;
using System;

// code from https://github.com/bthomas2622/facepunch-steamworks-tutorial/blob/main/SteamSocketsStuff.cs

public class SteamSocketManager : SocketManager
{
	public override void OnConnecting(Connection connection, ConnectionInfo data)
	{
		base.OnConnecting(connection, data);//The base class will accept the connection
		Debug.Log("SocketManager OnConnecting");
	}

	public override void OnConnected(Connection connection, ConnectionInfo data)
	{
		base.OnConnected(connection, data);
		Debug.Log("New player connecting");
	}

	public override void OnDisconnected(Connection connection, ConnectionInfo data)
	{
		base.OnDisconnected(connection, data);
		Debug.Log("Player disconnected");
	}

	public override void OnMessage(Connection connection, NetIdentity identity, IntPtr data, int size, long messageNum, long recvTime, int channel)
	{
		// Socket server received message, forward on message to all members of socket server
		SteamManager.RelaySocketMessageReceived(data, size, connection.Id);
		Debug.Log("Socket message received");
	}
}