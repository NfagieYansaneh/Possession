using System.Collections;
using System.Collections.Generic;
using Photon.Bolt;
//using Photon.Pun;
using UnityEngine;

// assisted w/ Photon Bolt Documentation && https://www.youtube.com/watch?v=wPKuMnXe-eA&list=PL8OCpfy38RwnwU9iJkV39M42mTXvqj6wt&t=8s

public class PlayerSpawnerBOLT : GlobalEventListener
{
    public GameObject playerPrefab = null;
    public PlayerInputHandler playerInputHandler = null;
    public GameObject crownPrefab = null;

    public override void SceneLoadLocalDone(string scene, IProtocolToken token)
    {
        var spawnPos = new Vector3(Random.Range(-2, 2), Random.Range(5, 10), 0f);
        BoltNetwork.Instantiate(playerPrefab, spawnPos, Quaternion.identity);
    }
}
/*
public class PlayerSpawner : MonoBehaviour
{
    public GameObject playerPrefab = null;
    public PlayerInputHandler playerInputHandler = null;
    public GameObject crownPrefab = null;

    // Start is called before the first frame update
    void Start()
    {
        GameObject newCrown = PhotonNetwork.Instantiate(crownPrefab.name, Vector3.zero, Quaternion.identity);
        GameObject newPlayerPrefab = PhotonNetwork.Instantiate(playerPrefab.name, new Vector3(0, 9, 0), Quaternion.identity);

        BaseCharacterController baseCharacterController = newPlayerPrefab.GetComponent<BaseCharacterController>();
        Crown crownScript = newCrown.GetComponent<Crown>();

        playerInputHandler.possessedCharacter = baseCharacterController;

        baseCharacterController.playerInputHandler = playerInputHandler;
        baseCharacterController.crownObject = newCrown;
        baseCharacterController.crownScript = crownScript;
        baseCharacterController.currentlyPossessed = true;

        crownScript.playerInputHandler = playerInputHandler;
        newCrown.SetActive(false);
    }


}*/