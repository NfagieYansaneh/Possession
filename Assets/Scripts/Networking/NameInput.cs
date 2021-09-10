using System.Collections;
using System.Collections.Generic;

using Photon.Pun;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

// assisted w/ Photon Pun documentation & https://www.youtube.com/watch?v=gxUCMOlISeQ&list=PLS6sInD7ThM0nJcyLfxgP9fnHKoQMJu-v

public class NameInput : MonoBehaviour
{
    public TMP_InputField nameInputField = null;
    public Button continueButton = null;

    private const string PlayerPrefsNameKey = "PlayerName"; // saving nickname to preferences

    // Start is called before the first frame update
    void Start()
    {
        SetUpInputField();
        continueButton.interactable = false;
    }

    private void SetUpInputField()
    {
        if(!PlayerPrefs.HasKey(PlayerPrefsNameKey)) { return; }

        string defaultName = PlayerPrefs.GetString(PlayerPrefsNameKey);
        SetPlayerName(defaultName);
    }

    public void SetPlayerName(string name)
    {
        if (string.IsNullOrEmpty(name) || name.Length > 32)
        {
            continueButton.interactable = false;
        }
        else continueButton.interactable = true;
    }

    public void SavePlayerName()
    {
        string playerName = nameInputField.text;
        PhotonNetwork.NickName = playerName;
        PlayerPrefs.SetString(PlayerPrefsNameKey, playerName);
    }

}
