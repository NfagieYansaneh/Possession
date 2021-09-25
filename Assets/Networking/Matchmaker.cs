using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Steamworks.ServerList;
using Steamworks.Data;
using Steamworks;
using System;
using System.Text;

public class Matchmaker : MonoBehaviour
{
    Internet Request;
    public Client client;

    public void FindMatch()
    {
        Request = new Internet();
        // Request.AddFilter("gamemode", "1v1");
        // Request.AddFilter("map", "miami_beach");
        Request.OnChanges += OnServersUpdated;
        Request.RunQueryAsync(timeoutSeconds: 30);
    }

    public void HostMatch()
    {
        if(Request != null)
        {
            Request.Dispose();
            Request = null;
        }

        client.P2PLookForSessionRequest();
    }

    void OnServersUpdated()
    {
        if (Request.Responsive.Count == 0)
        {
            Debug.Log("Found no matches");
            return;
        }


        foreach (var s in Request.Responsive)
        {
            ServerResponded(s);
            Debug.Log("Found Match");
        }

        Request.Responsive.Clear();
    }

    void ServerResponded(ServerInfo server)
    {
        Debug.Log($"{server.Name} Responded!");

        string hello = "Hello!";
        // byte[] mydata = ASCIIEncoding.ASCII.GetBytes(hello);
        // var sent = SteamNetworking.SendP2PPacket(server.SteamId, mydata);
    }
}
