using System.Collections;
using System.Collections.Generic;
using Photon;
using Photon.Pun;
using UnityEngine;


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

}