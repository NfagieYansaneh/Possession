using System.Collections;
using System.Collections.Generic;
using UnityEngine;

// assist from https://www.youtube.com/watch?v=xJl3yHjhils&t=65s
public class SteamManager : MonoBehaviour
{
    public uint appId = 480;

    private void Awake()
    {
        DontDestroyOnLoad(this);

        try
        {
            Steamworks.SteamClient.Init(appId, true);
            Debug.Log("Steam is up and running");
        }
        catch (System.Exception e)
        {
            Debug.Log(e.Message);
        }
    }

    private void OnApplicationQuit()
    {
        try
        {
            Steamworks.SteamClient.Shutdown();
        }
        catch
        {

        }
    }
    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
