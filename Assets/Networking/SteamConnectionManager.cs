using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Steamworks;
using Steamworks.Data;
using System;

public class SteamConnectionManager : ConnectionManager
{
    public override void OnConnected(ConnectionInfo info)
    {
        base.OnConnected(info);
        Debug.Log("ConnectionOnConnected");
    }

    public override void OnConnecting(ConnectionInfo info)
    {
        base.OnConnecting(info);
        Debug.Log("ConnectionOnConnecting");
    }

    public override void OnDisconnected(ConnectionInfo info)
    {
        base.OnDisconnected(info);
        Debug.Log("ConnectionOnDisconnected");
    }

    public override void OnMessage(IntPtr data, int size, long messageNum, long recvTime, int channel)
    {
        // Message received from socket server, delegate to method for processing
        SteamManager.ProcessMessageFromSocketServer(data, size);
        Debug.Log("Connection Got A Message");
    }
}
