using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

// deadzones are meant to refine raw data input

public class CustomDeadzonesWindow : EditorWindow
{
    float universalFixedMinDeadzone = 0.125f;
    float universalFixedMaxDeadzone = 0.925f;

    float groundHorizontalDeadzone = 0.2f;
    float groundVerticalDeadzone = 0.2f;

    float aerialHorizontalDeadzone = 0.2f;
    float aerialVerticalDeadzone = 0.2f;

    Vector3 groundDeadzonePosition = Vector3.zero;
    Vector3 aerialDeadzonePosition = Vector3.zero;
    Vector3 universalDeadzonePosition = Vector3.zero;
    Vector3 attackDeadzonePosition = Vector3.zero;
    Vector3 RAWMovementDeadzonePosition = Vector3.zero;
    Vector3 RAWAttackDeadzonePosition = Vector3.zero;

    Vector3 groundMovementGUIPosition = Vector3.zero;
    Vector3 aerialMovementGUIPosition = Vector3.zero;
    Vector3 universalDeadzoneGUIPosition = Vector3.zero;
    Vector3 attackDeadzoneGUIPosition = Vector3.zero;
    Vector3 RAWMovementDeadzoneGUIPosition = Vector3.zero;
    Vector3 RAWAttackDeadzoneGUIPosition = Vector3.zero;

    static CustomDeadzonesWindow window;
    static PlayerInputHandler playerInputHandler = null;

    float radius; // radius for deadzone circles

    bool bootup = true;

    [MenuItem("Tools/Deadzones")]
    public static void ShowWindow()
    {
       window = GetWindow<CustomDeadzonesWindow>("Deadzones");
       
        if (playerInputHandler == null)
        {
           foreach (GameObject obj in Resources.FindObjectsOfTypeAll(typeof(GameObject)))
           {
               playerInputHandler = obj.GetComponent<PlayerInputHandler>();
               if (playerInputHandler != null)
               {
                   break;
               }
           }
        }
    }

    private void OnGUI()
    {
        // grabs playerInputHandler 
        if (bootup)
        {
            LoadDeadzones(); bootup = false;
        }

        Rect rect = EditorGUILayout.BeginVertical();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Universal Fixed Min/Max Deadzone:");
        EditorGUILayout.MinMaxSlider(ref universalFixedMinDeadzone, ref universalFixedMaxDeadzone, 0f, 1f);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Grounded Horizontal Deadzone:");
        groundHorizontalDeadzone = EditorGUILayout.Slider(groundHorizontalDeadzone, 0f, 1f);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Grounded Vertical Deadzone:");
        groundVerticalDeadzone = EditorGUILayout.Slider(groundVerticalDeadzone, 0f, 1f);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Aerial Horizontal Deadzone:");
        aerialHorizontalDeadzone = EditorGUILayout.Slider(aerialHorizontalDeadzone, 0f, 1f);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("Aerial Vertical Deadzone:");
        aerialVerticalDeadzone = EditorGUILayout.Slider(aerialVerticalDeadzone, 0f, 1f);
        EditorGUILayout.EndHorizontal();

        if (GUILayout.Button("Force Find Player Input Handler"))
        {
            foreach (GameObject obj in Resources.FindObjectsOfTypeAll(typeof(GameObject)))
            {
                playerInputHandler = obj.GetComponent<PlayerInputHandler>();
                if (playerInputHandler != null)
                {
                    break;
                }
            }
        }

        if (GUILayout.Button("Reset Deadzones"))
        {
            universalFixedMinDeadzone = 0.125f;
            universalFixedMaxDeadzone = 0.925f;

            groundHorizontalDeadzone = 0.2f;
            groundVerticalDeadzone = 0.2f;

            aerialHorizontalDeadzone = 0.2f;
            aerialVerticalDeadzone = 0.2f;
        }

        if (GUILayout.Button("Load Deadzones"))
        {
            LoadDeadzones();
        }

        if (GUILayout.Button("Save Deadzones"))
        {
            SaveDeadzones();
        }


        // This function is only a dream :(
        // I need to fix it later
        /*if (GUILayout.Button("Fix Drift"))
        {
            universalFixedMinDeadzone = playerInputHandler.possessedCharacter.movementDirection.magnitude + 0.1f;
        }*/

        EditorGUILayout.Space();

        float margin = 5f; // magic value
        radius = (window.position.width / 8) - 2 * margin;

        // Top row left to right
        groundDeadzonePosition = new Vector3(rect.position.x + rect.width / 8 + margin, radius + rect.height);
        aerialDeadzonePosition = new Vector3(rect.position.x + 3 * rect.width / 8 + margin, radius + rect.height);
        universalDeadzonePosition = new Vector3(rect.position.x + rect.width - (rect.width / 8) + margin, radius + rect.height);
        attackDeadzonePosition = new Vector3(rect.position.x + rect.width - (3*rect.width / 8) + margin, radius + rect.height);

        // Bottom row left to right
        RAWMovementDeadzonePosition = new Vector3(rect.position.x + rect.width / 8 + margin, window.position.height - radius - margin);
        RAWAttackDeadzonePosition = new Vector3(rect.position.x + 3 * rect.width / 8 + margin, window.position.height - radius - margin);
        //aerialDeadzonePosition = new Vector3(rect.position.x + rect.width - (rect.width / 8) + margin, window.position.height - radius - margin);
        //aerialDeadzonePosition = new Vector3(rect.position.x + rect.width - (3 * rect.width / 8) + margin, window.position.height - radius - margin);

        DrawDeadzones();

    }

    void DrawDeadzones()
    {
        Vector3[] vertices;

        if (universalFixedMaxDeadzone != 1f)
        {
            Handles.color = Color.red;
            Handles.DrawSolidDisc(groundDeadzonePosition, new Vector3(0, 0, 1), radius);
            Handles.DrawSolidDisc(aerialDeadzonePosition, new Vector3(0, 0, 1), radius);
            Handles.DrawSolidDisc(universalDeadzonePosition, new Vector3(0, 0, 1), radius);
            Handles.DrawSolidDisc(attackDeadzonePosition, new Vector3(0, 0, 1), radius);
        }

        /* TOP ROW Grounded deadzone visualiser */

        Handles.color = Color.white;
        Handles.DrawSolidDisc(groundDeadzonePosition, new Vector3(0, 0, 1), radius * universalFixedMaxDeadzone);

        Handles.color = Color.red;
        Handles.DrawSolidDisc(groundDeadzonePosition, new Vector3(0, 0, 1), radius * universalFixedMinDeadzone);

        // Horizontal deadzone
        if (groundHorizontalDeadzone != 0f)
        {
            vertices = new Vector3[]
            {
            new Vector3 (groundDeadzonePosition.x - (groundHorizontalDeadzone*radius), groundDeadzonePosition.y + radius), // Top Left
            new Vector3 (groundDeadzonePosition.x + (groundHorizontalDeadzone*radius), groundDeadzonePosition.y + radius), // Top Right
            new Vector3 (groundDeadzonePosition.x + (groundHorizontalDeadzone*radius), groundDeadzonePosition.y + -radius), // Bottom Right
            new Vector3 (groundDeadzonePosition.x - (groundHorizontalDeadzone*radius), groundDeadzonePosition.y + -radius), // Bottom Left
            };
            Handles.DrawSolidRectangleWithOutline(vertices, Color.red, Color.red);
        }

        // Vertical deadzone
        if (groundVerticalDeadzone != 0f)
        {
            vertices = new Vector3[]
            {
            new Vector3 (groundDeadzonePosition.x + radius , groundDeadzonePosition.y - (groundVerticalDeadzone*radius)), // Top Left
            new Vector3 (groundDeadzonePosition.x + radius , groundDeadzonePosition.y + (groundVerticalDeadzone*radius)), // Top Right
            new Vector3 (groundDeadzonePosition.x + -radius, groundDeadzonePosition.y + (groundVerticalDeadzone*radius)), // Bottom Right
            new Vector3 (groundDeadzonePosition.x + -radius, groundDeadzonePosition.y - (groundVerticalDeadzone*radius)), // Bottom Left
            };
            Handles.DrawSolidRectangleWithOutline(vertices, Color.red, Color.red);
        }

        Handles.Label(groundDeadzonePosition + Vector3.down * radius + Vector3.left * radius, "Grounded Deadzone");



        /* TOP ROW Aerial deadzones visualiser */

        Handles.color = Color.white;
        Handles.DrawSolidDisc(aerialDeadzonePosition, new Vector3(0, 0, 1), radius * universalFixedMaxDeadzone);

        Handles.color = Color.red;
        Handles.DrawSolidDisc(aerialDeadzonePosition, new Vector3(0, 0, 1), radius * universalFixedMinDeadzone);

        // Horizontal deadzone
        if (aerialHorizontalDeadzone != 0f)
        {
            vertices = new Vector3[]
            {
            new Vector3 (aerialDeadzonePosition.x - (aerialHorizontalDeadzone*radius), aerialDeadzonePosition.y + radius), // Top Left
            new Vector3 (aerialDeadzonePosition.x + (aerialHorizontalDeadzone*radius), aerialDeadzonePosition.y + radius), // Top Right
            new Vector3 (aerialDeadzonePosition.x + (aerialHorizontalDeadzone*radius), aerialDeadzonePosition.y + -radius), // Bottom Right
            new Vector3 (aerialDeadzonePosition.x - (aerialHorizontalDeadzone*radius), aerialDeadzonePosition.y + -radius), // Bottom Left
            };
            Handles.DrawSolidRectangleWithOutline(vertices, Color.red, Color.red);
        }

        // Vertical deadzone
        if (aerialVerticalDeadzone != 0f)
        {
            vertices = new Vector3[]
            {
            new Vector3 (aerialDeadzonePosition.x + radius , aerialDeadzonePosition.y - (aerialVerticalDeadzone*radius)), // Top Left
            new Vector3 (aerialDeadzonePosition.x + radius , aerialDeadzonePosition.y + (aerialVerticalDeadzone*radius)), // Top Right
            new Vector3 (aerialDeadzonePosition.x + -radius, aerialDeadzonePosition.y + (aerialVerticalDeadzone*radius)), // Bottom Right
            new Vector3 (aerialDeadzonePosition.x + -radius, aerialDeadzonePosition.y - (aerialVerticalDeadzone*radius)), // Bottom Left
            };
            Handles.DrawSolidRectangleWithOutline(vertices, Color.red, Color.red);
        }

        Handles.Label(aerialDeadzonePosition + Vector3.down * radius + Vector3.left * radius, "Aerial Deadzone");




        /* TOP ROW Universal deadzone visualiser */

        Handles.color = Color.white;
        Handles.DrawSolidDisc(universalDeadzonePosition, new Vector3(0, 0, 1), radius * universalFixedMaxDeadzone);

        Handles.color = Color.red;
        Handles.DrawSolidDisc(universalDeadzonePosition, new Vector3(0, 0, 1), radius * universalFixedMinDeadzone);

        Handles.Label(universalDeadzonePosition + Vector3.down * radius + Vector3.left * radius, "Universal Deadzone");



        /* TOP ROW Attack deadzone visualiser */

        Handles.color = Color.white;
        Handles.DrawSolidDisc(attackDeadzonePosition, new Vector3(0, 0, 1), radius * universalFixedMaxDeadzone);

        Handles.color = Color.red;
        Handles.DrawSolidDisc(attackDeadzonePosition, new Vector3(0, 0, 1), radius * universalFixedMinDeadzone);

        Handles.Label(attackDeadzonePosition + Vector3.down * radius + Vector3.left * radius, "Attack Deadzone");




        /* BOTTOM ROW RAW movement visualiser */

        Handles.color = Color.white;
        Handles.DrawSolidDisc(RAWMovementDeadzonePosition, new Vector3(0, 0, 1), radius);

        Handles.Label(RAWMovementDeadzonePosition + Vector3.down * radius + Vector3.left * radius, "RAW Movement");




        /* BOTTOM ROW RAW movement visualiser */

        Handles.color = Color.white;
        Handles.DrawSolidDisc(RAWAttackDeadzonePosition, new Vector3(0, 0, 1), radius);

        Handles.color = Color.magenta;
        Handles.Label(RAWAttackDeadzonePosition + Vector3.down * radius + Vector3.left * radius, "RAW Attack");

        UpdateMovementGUIPosition();
        Handles.color = Color.cyan;
        Handles.DrawSolidDisc(groundMovementGUIPosition, new Vector3(0, 0, 1), radius / 10);
        Handles.DrawSolidDisc(aerialMovementGUIPosition, new Vector3(0, 0, 1), radius / 10);
        Handles.DrawSolidDisc(universalDeadzoneGUIPosition, new Vector3(0, 0, 1), radius / 10);
        Handles.DrawSolidDisc(attackDeadzoneGUIPosition, new Vector3(0, 0, 1), radius / 10);
        Handles.DrawSolidDisc(RAWMovementDeadzoneGUIPosition, new Vector3(0, 0, 1), radius / 10);
        Handles.DrawSolidDisc(RAWAttackDeadzoneGUIPosition, new Vector3(0, 0, 1), radius / 10);
    }

    void LoadDeadzones()
    {
        universalFixedMinDeadzone = playerInputHandler.universalFixedMinDeadzone;
        universalFixedMaxDeadzone = playerInputHandler.universalFixedMaxDeadzone;

        groundHorizontalDeadzone = playerInputHandler.groundHorizontalDeadzone;
        groundVerticalDeadzone = playerInputHandler.groundVerticalDeadzone;
        aerialHorizontalDeadzone = playerInputHandler.aerialHorizontalDeadzone;
        aerialVerticalDeadzone = playerInputHandler.aerialVerticalDeadzone;

        Debug.Log("Deadzones were loaded!");
    }

    void SaveDeadzones()
    {
        playerInputHandler.universalFixedMinDeadzone = universalFixedMinDeadzone;
        playerInputHandler.universalFixedMaxDeadzone = universalFixedMaxDeadzone;

        playerInputHandler.groundHorizontalDeadzone = groundHorizontalDeadzone;
        playerInputHandler.groundVerticalDeadzone = groundVerticalDeadzone;
        playerInputHandler.aerialHorizontalDeadzone = aerialHorizontalDeadzone;
        playerInputHandler.aerialVerticalDeadzone = aerialVerticalDeadzone;
        Debug.Log("Deadzones were saved!");
    }

    Vector2 previousMovementDirection = Vector3.zero;
    public void Update()
    {
        if (playerInputHandler != null && playerInputHandler.RAWmovementDirection == previousMovementDirection) return;

        window.Repaint();

        previousMovementDirection = playerInputHandler.RAWmovementDirection;
    }

    public void UpdateMovementGUIPosition()
    {
        groundMovementGUIPosition = groundDeadzonePosition +
            new Vector3(playerInputHandler.groundMovementDirection.x, -playerInputHandler.groundMovementDirection.y) * radius;

        aerialMovementGUIPosition = aerialDeadzonePosition +
            new Vector3(playerInputHandler.aerialMovementDirection.x, -playerInputHandler.aerialMovementDirection.y) * radius;

        universalDeadzoneGUIPosition = universalDeadzonePosition +
            new Vector3(playerInputHandler.universalMovementDirection.x, -playerInputHandler.universalMovementDirection.y) * radius;

        attackDeadzoneGUIPosition = attackDeadzonePosition +
            new Vector3(playerInputHandler.groundAttackDirection.x, -playerInputHandler.aerialAttackDirection.y) * radius;

        RAWMovementDeadzoneGUIPosition = RAWMovementDeadzonePosition +
            new Vector3(playerInputHandler.RAWmovementDirection.x, -playerInputHandler.RAWmovementDirection.y) * radius;

        RAWAttackDeadzoneGUIPosition = RAWAttackDeadzonePosition +
            new Vector3(playerInputHandler.RAWattackDirection.x, -playerInputHandler.RAWattackDirection.y) * radius;
    }

}
