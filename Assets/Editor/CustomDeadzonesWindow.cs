using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/* Purpose of CustomDeadzonesWindow is to be a tool that allows to visualise who player inputs are being
 * processed within the game
 */

public class CustomDeadzonesWindow : EditorWindow
{
    /* List of deadzones are used to tweak the feel of gameplay to determine how sensitive inputs should be respective to player inputs */

    // "universal" is typically used to just understand the direction of the movement keys with basic deadzones applied to it
    float universalFixedMinDeadzone = 0.125f;
    float universalFixedMaxDeadzone = 0.925f;

    // "ground" is typically used to just understand whether our movement keys are heading to the left or right
    float groundHorizontalDeadzone = 0.2f;
    float groundVerticalDeadzone = 0.2f;

    // "aerial" is typically used to just understand whether our movement keys are heading up or down
    float aerialHorizontalDeadzone = 0.2f;
    float aerialVerticalDeadzone = 0.2f;

    // "attack" is typically used to just understand the direction of our attack
    // here we are uses angles to determine the angle our attack direction as it will have to be relative to our player's position to select from different directional attacks
    float attackVerticalAngleDeadzone = 90f;
    float attackHorizontalAngleDeadzone = 90f;

    // Position of where the GUI joystick circle should be placed to represent the boundaries of a joystick (that can also be translated into movements on a keyboard)
    Vector3 groundDeadzonePosition = Vector3.zero;
    Vector3 aerialDeadzonePosition = Vector3.zero;
    Vector3 universalDeadzonePosition = Vector3.zero;
    Vector3 attackDeadzonePosition = Vector3.zero;
    Vector3 RAWMovementDeadzonePosition = Vector3.zero;
    Vector3 RAWAttackDeadzonePosition = Vector3.zero;

    // where the little cyan circle is held represents the GUI joystick stick position relative to the GUI joystick base
    // and the GUI joystick stick can also be translated into movements on a keyboard
    Vector3 groundMovementGUIPosition = Vector3.zero;
    Vector3 aerialMovementGUIPosition = Vector3.zero;
    Vector3 universalDeadzoneGUIPosition = Vector3.zero;
    Vector3 attackDeadzoneGUIPosition = Vector3.zero;
    Vector3 RAWMovementDeadzoneGUIPosition = Vector3.zero;
    Vector3 RAWAttackDeadzoneGUIPosition = Vector3.zero;


    static CustomDeadzonesWindow window;
    static PlayerInputHandler playerInputHandler = null;

    float radius; // radius for GUI joystick circles circles

    bool bootup = true;

    // Creating our CustomDeadzones window when we click on "Tools" then "Deadzones" on our menu items
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

    // Drawing GUI elements
    private void OnGUI()
    {
        // grabs playerInputHandler stored deadzones
        if (bootup)
        {
            LoadDeadzones(); bootup = false;
        }

        Rect rect = EditorGUILayout.BeginVertical();

        /* drawing sliders that allow us to adjust deadzones of our inputs*/
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

        EditorGUILayout.Space();

        float margin = 5f;
        radius = (window.position.width / 8) - 2 * margin;

        // going from top row left to top right, as I am assiging the position of the joystick circles to be then drawn with Handles.DrawSolidDisc
        groundDeadzonePosition = new Vector3(rect.position.x + rect.width / 8 + margin, radius + rect.height);
        aerialDeadzonePosition = new Vector3(rect.position.x + 3 * rect.width / 8 + margin, radius + rect.height);
        universalDeadzonePosition = new Vector3(rect.position.x + rect.width - (rect.width / 8) + margin, radius + rect.height);
        attackDeadzonePosition = new Vector3(rect.position.x + rect.width - (3*rect.width / 8) + margin, radius + rect.height);

        // going from bottom row left to bottom right, as I am assiging the position of the joystick circles to be then drawn with Handles.DrawSolidDisc
        RAWMovementDeadzonePosition = new Vector3(rect.position.x + rect.width / 8 + margin, window.position.height - radius - margin);
        RAWAttackDeadzonePosition = new Vector3(rect.position.x + 3 * rect.width / 8 + margin, window.position.height - radius - margin);
        // newPotentialDeadzonePosition = new Vector3(rect.position.x + rect.width - (rect.width / 8) + margin, window.position.height - radius - margin);
        // newPotentialDeadzonePosition = new Vector3(rect.position.x + rect.width - (3 * rect.width / 8) + margin, window.position.height - radius - margin);

        DrawDeadzones();

    }

    // Draws deadzones (as well as some other joystick GUI elements) that will be shown in our CustomDeadzones window
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

        /* drawing TOP ROW Grounded deadzone visualiser */

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



        /* drawing TOP ROW Aerial deadzones visualiser */

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




        /* drawing TOP ROW Universal deadzone visualiser */

        Handles.color = Color.white;
        Handles.DrawSolidDisc(universalDeadzonePosition, new Vector3(0, 0, 1), radius * universalFixedMaxDeadzone);

        Handles.color = Color.red;
        Handles.DrawSolidDisc(universalDeadzonePosition, new Vector3(0, 0, 1), radius * universalFixedMinDeadzone);

        Handles.Label(universalDeadzonePosition + Vector3.down * radius + Vector3.left * radius, "Universal Deadzone");



        /* drawing TOP ROW Attack deadzone visualiser */

        Handles.color = Color.white;
        Handles.DrawSolidDisc(attackDeadzonePosition, new Vector3(0, 0, 1), radius * universalFixedMaxDeadzone);

        Handles.color = Color.red;
        Handles.DrawSolidDisc(attackDeadzonePosition, new Vector3(0, 0, 1), radius * universalFixedMinDeadzone);

        Vector2 upperBound = Vector2.up;
        upperBound = Vector3.RotateTowards(upperBound, Vector2.right, Mathf.Deg2Rad * (attackVerticalAngleDeadzone / 2f), 0f);
        Debug.Log(upperBound);

        Handles.color = Color.magenta;

        Handles.DrawSolidArc(attackDeadzonePosition, new Vector3(0, 0, 1), new Vector3(upperBound.x, -upperBound.y),
            (attackVerticalAngleDeadzone), radius);

        Handles.DrawSolidArc(attackDeadzonePosition, new Vector3(0, 0, 1), new Vector3(-upperBound.x, upperBound.y),
            (attackVerticalAngleDeadzone), radius);

        Handles.Label(attackDeadzonePosition + Vector3.down * radius + Vector3.left * radius, "Attack Deadzone");




        /* drawing BOTTOM ROW RAW movement visualiser */

        Handles.color = Color.white;
        Handles.DrawSolidDisc(RAWMovementDeadzonePosition, new Vector3(0, 0, 1), radius);

        Handles.Label(RAWMovementDeadzonePosition + Vector3.down * radius + Vector3.left * radius, "RAW Movement");




        /* drawing BOTTOM ROW RAW movement visualiser */

        Handles.color = Color.white;
        Handles.DrawSolidDisc(RAWAttackDeadzonePosition, new Vector3(0, 0, 1), radius);

        Handles.color = Color.magenta;
        Handles.Label(RAWAttackDeadzonePosition + Vector3.down * radius + Vector3.left * radius, "RAW Attack");


        // Updates and checks for new player inputs to assess where the GUI joystick stick should be relative to our GUI joystick base
        UpdateMovementGUIPosition();

        // Drawing the little cyan circle within our GUI joystick base that represents the GUI joystick stick's current position relative to the
        // GUI joystick base (that can also be translated into movements on a keyboard)

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

        attackVerticalAngleDeadzone = playerInputHandler.attackVerticalAngleDeadzone;
        attackHorizontalAngleDeadzone = playerInputHandler.attackHorizontalAngleDeadzone;

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

        playerInputHandler.attackVerticalAngleDeadzone   = attackVerticalAngleDeadzone;
        playerInputHandler.attackHorizontalAngleDeadzone = attackHorizontalAngleDeadzone;

        Debug.Log("Deadzones were saved!");
    }

    Vector2 previousMovementDirection = Vector3.zero;
    Vector2 previousAttackDrection = Vector2.zero;
    public void Update()
    {
        if (playerInputHandler != null && playerInputHandler.RAWmovementDirection == previousMovementDirection &&
            playerInputHandler.RAWattackDirection == previousAttackDrection) return;

        window.Repaint(); // redraws window

        previousMovementDirection = playerInputHandler.RAWmovementDirection;
        previousAttackDrection = playerInputHandler.RAWattackDirection;
    }

    // acquires player's inputs to assess where the GUI joystick stick should be drawn relative to the GUI joystick base for our CustomDeadzones window
    public void UpdateMovementGUIPosition()
    {
        groundMovementGUIPosition = groundDeadzonePosition +
            new Vector3(playerInputHandler.groundMovementDirection.x, -playerInputHandler.groundMovementDirection.y) * radius;

        aerialMovementGUIPosition = aerialDeadzonePosition +
            new Vector3(playerInputHandler.aerialMovementDirection.x, -playerInputHandler.aerialMovementDirection.y) * radius;

        universalDeadzoneGUIPosition = universalDeadzonePosition +
            new Vector3(playerInputHandler.universalMovementDirection.x, -playerInputHandler.universalMovementDirection.y) * radius;

        RAWMovementDeadzoneGUIPosition = RAWMovementDeadzonePosition +
            new Vector3(playerInputHandler.RAWmovementDirection.x, -playerInputHandler.RAWmovementDirection.y) * radius;

        RAWAttackDeadzoneGUIPosition = RAWAttackDeadzonePosition +
            new Vector3(playerInputHandler.RAWattackDirection.x, -playerInputHandler.RAWattackDirection.y) * radius;

        switch (playerInputHandler.playerAttackDirection)
        {
            case (int)attackDirection.NEUTRAL:
                attackDeadzoneGUIPosition = attackDeadzonePosition;
                break;

            case (int)attackDirection.UP:
                attackDeadzoneGUIPosition = attackDeadzonePosition -
                    Vector3.up * radius;
                break;

            case (int)attackDirection.DOWN:
                attackDeadzoneGUIPosition = attackDeadzonePosition -
                    Vector3.down * radius;
                break;

            case (int)attackDirection.FORWARD:
                attackDeadzoneGUIPosition = attackDeadzonePosition +
                    Vector3.right * radius * ((playerInputHandler.possessedCharacter.facingRight) ? 1f : -1f);
                break;
        }
    }

}
