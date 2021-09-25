using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

/* Depreicated and not used anymore because it is unnecessary. However, I may update this in the future if I wish... */

public class CustomPossesserWindow : EditorWindow
{
    Texture2D imagePreviousPossession;
    Texture2D imageCurrentPossession;

    PlayerInputHandler playerInputHandler;

    [MenuItem("Tools/Possesser")]
    public static void ShowWindow() 
    {
        GetWindow<CustomPossesserWindow>("Possesser");
    }

    void OnGUI()
    {
        if (playerInputHandler == null)
        {
            GrabPlayerInputHandler();
        }

        GUILayout.Label("Select a character to possess!");

        if (GUILayout.Button("Possess"))
        {
            Possess();
        }

        EditorGUI.LabelField(new Rect(5, 45, 130, 10), "Previous Possession:");
        EditorGUI.LabelField(new Rect(150, 45, 120, 10), "Current Possession:");

        if (imagePreviousPossession)
        {
            EditorGUI.DrawTextureTransparent(new Rect(10, 60, 100, 100), imagePreviousPossession);
        } else
        {
            imagePreviousPossession = EditorGUIUtility.whiteTexture;
            EditorGUI.DrawTextureTransparent(new Rect(10, 60, 100, 100), imagePreviousPossession);
        }

        if (imageCurrentPossession)
        {
            EditorGUI.DrawTextureTransparent(new Rect(160, 60, 100, 100), imageCurrentPossession);
        } else
        {
            imageCurrentPossession = EditorGUIUtility.whiteTexture;
            EditorGUI.DrawTextureTransparent(new Rect(160, 60, 100, 100), imageCurrentPossession);
        }
    }

    // Method to set Color
    void Possess()
    {
        foreach (GameObject obj in Selection.gameObjects)
        {
            BaseCharacterController baseCharacterController = obj.GetComponent<BaseCharacterController>();

            if (baseCharacterController != null)
            {
                //baseCharacterController.possess();
                imagePreviousPossession = imageCurrentPossession;
                imageCurrentPossession = obj.GetComponent<SpriteRenderer>().sprite.texture;
                playerInputHandler.possessedCharacter = baseCharacterController;
                Debug.Log(obj.name + " was possessed");
            }
        }
    }

    void GrabPlayerInputHandler()
    {
        foreach (GameObject obj in Resources.FindObjectsOfTypeAll(typeof(GameObject)))
        {
            playerInputHandler = obj.GetComponent<PlayerInputHandler>();
            if (playerInputHandler != null)
            {
                imageCurrentPossession = playerInputHandler.possessedCharacter.gameObject.GetComponent<SpriteRenderer>().sprite.texture;
                break;
            }
        }
    }
}
