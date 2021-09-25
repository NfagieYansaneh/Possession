using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Users;

#if UNITY_EDITOR
using UnityEditor;
#endif

/* Purpose of BaseCharacterController is to be the base character controller script that all characters inherit and are
 * capable of overridding to fit their specific needs
 */

/* Purpsoe of PlayerInputHandler is to handle player inputs and call the corresponding functions towards the currently possessed character
 * that is possessed by this player
 */

public enum attackDirection { NEUTRAL, FORWARD, UP, DOWN };
public enum attackType { LIGHT, HEAVY, BASIC_ABILITY, ULTIMATE_ABILITY };

public class PlayerInputHandler : MonoBehaviour
{
    // Variable stores an instance of the InputMaster, which holds all of our input actions for input processing
    InputMaster input;

    // used to assess which input device we are currently using
    PlayerInput controls;

    public enum ControlDevices
    {
        Gamepad,
        KeyboardAndMouse,
        Keyboard
    };

    public ControlDevices currentDevice; // currentInputDevice

    // player color that will be used to represent the shadow of the possessed character (used to help differentiate one character from another
    public Color playerColor;

    /* List of deadzones are used to tweak the feel of gameplay to determine how sensitive inputs should be respective to player inputs */

    // "universal" is typically used to just understand the direction of the movement keys with basic deadzones applied to it
    public float universalFixedMinDeadzone = 0.125f;
    public float universalFixedMaxDeadzone = 0.925f;

    // "ground" is typically used to just understand whether our movement keys are heading to the left or right
    public float groundHorizontalDeadzone = 0.2f;
    public float groundVerticalDeadzone = 0.2f;

    // "aerial" is typically used to just understand whether our movement keys are heading up or down
    public float aerialHorizontalDeadzone = 0.2f;
    public float aerialVerticalDeadzone = 0.2f;

    // "attack" is typically used to just understand the direction of our attack
    // here we are uses angles to determine the angle our attack direction as it will have to be relative to our player's position to select from different directional attacks
    public float attackVerticalAngleDeadzone = 90f;
    public float attackHorizontalAngleDeadzone = 90f;
    
    // However, to form a neutral attack on a gamepad, we would just leave the left joystick (handles movement) into a neutral position
    // Although, on keyboard and mouse, we don't have a keyboard, thus if we click close enough on our currently possessed character (distance defined as attackNeutralRadius)
    // we will be performing a netural attack
    public float attackNeutralRadius = 1f; // Only for KB&M user

    public int playerAttackDirection = (int)attackDirection.NEUTRAL;
    public int playerAttackType = (int)attackType.LIGHT;

    Vector2 mousePosition = Vector2.zero;
    public Vector2 mouseNormalized = Vector2.zero; // presents a normalized vector of our mouse position relative to our currently possessed character

    [HideInInspector] public Vector2 RAWmovementDirection     = Vector2.zero;   // "RAW" movement direction is the raw movement inputs without any deadzones applied on top of it
    [HideInInspector] public Vector2 universalMovementDirection = Vector2.zero;     // "universal" is typically used to just understand the direction of the movement keys with basic deadzones applied to it
    [HideInInspector] public Vector2 groundMovementDirection  = Vector2.zero;    // "ground" is typically used to just understand whether our movement keys are heading to the left or right
    [HideInInspector] public Vector2 aerialMovementDirection  = Vector2.zero;    // "aerial" is typically used to just understand whether our movement keys are heading up or down
    [HideInInspector] public Vector2 dashingMovementDirection = Vector2.zero;    // "dashing" is typically used to just understand the direction our dash will be performed

    [HideInInspector] public Vector2 RAWattackDirection       = Vector2.zero;
    
    // no longer need this since we can handle computations to determine direction of our attack by only using RAWattackDirection
    // [HideInInspector] public Vector2 groundAttackDirection    = Vector2.zero;
    // [HideInInspector] public Vector2 aerialAttackDirection    = Vector2.zero;

    [HideInInspector] public bool spaceKeyHeld = false;

    public BaseCharacterController possessedCharacter; // currently possessed character

    private void Awake()
    {
        input = new InputMaster();
        controls = GetComponent<UnityEngine.InputSystem.PlayerInput>();
        ParseDeviceName(controls.defaultControlScheme, true); // assesses the our default input device and updates currentDevice being used

        /* when movement keys are performed, call the appropriate functions */

        input.Player.Movement.started += OnMovementPerformed;
        input.Player.Movement.performed += OnMovementPerformed;
        input.Player.Movement.canceled += OnMovementPerformed;

        input.Player.Jump.started += ctx => spaceKeyHeld = true;
        input.Player.Jump.performed += OnJumpPerformed;
        input.Player.Jump.canceled += ctx => spaceKeyHeld = false;

        input.Player.Dodge.performed += OnDodgePerformed;

        input.Player.LightAttack.performed += OnLightAttackPerformed;

        input.Player.HeavyAttack.performed += OnHeavyAttackPerformed;

        input.Player.BasicAbility.performed += OnBasicAbilityPerformed;

        input.Player.UltimateAbility.performed += OnUltimateAbilityPerformed;

        input.Player.CrownThrow.performed += OnCrownThrowPerformed;

        controls.onControlsChanged += OnControlsChanged;

        //input.Player.MousePosition.started += OnMousePositionPerformed;
        //input.Player.MousePosition.performed += OnMousePositionPerformed;
        //input.Player.MousePosition.canceled += OnMousePositionPerformed;
    }

    public void Update()
    {
        mousePosition = Camera.main.ScreenToWorldPoint(input.Player.MousePosition.ReadValue<Vector2>());
        Vector2 directionRelativeToPlayer = new Vector3(mousePosition.x, mousePosition.y) - possessedCharacter.transform.position;
        mouseNormalized = directionRelativeToPlayer.normalized;

        // RefineAttack is used to determine the attack of our directional attack
        RefineAttack();
    }

    public void OnGUI()
    {
        // Debugging purpose used to adjust and view the currentHealth of our currently possessed character
        GUI.Label(new Rect(20f, 120f, 150f, 40f), new GUIContent("Current Health "));
        possessedCharacter.healthHandler.currentHealth =
            (int)GUI.HorizontalSlider(new Rect(20f, 140f, 150f, 40f), possessedCharacter.healthHandler.currentHealth, 0f, possessedCharacter.healthHandler.maxHealth);
    }

#if UNITY_EDITOR
    public void OnDrawGizmosSelected()
    {
        // Debugging purposes used to visualisse our mouse position relative to our possessed character
        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(possessedCharacter.transform.position,
            mousePosition);
    }
#endif

    // assesses the our default input device and updates currentDevice being used
    public void ParseDeviceName(string sensitiveString, bool debugLog)
    {
        if (sensitiveString == "Gamepad")
        {
            currentDevice = ControlDevices.Gamepad;
            if (debugLog) Debug.Log("Switched to gamepad");
        } else if(sensitiveString == "Keyboard and mouse") {
            currentDevice = ControlDevices.KeyboardAndMouse;
            if (debugLog) Debug.Log("Switched to keyboard and mouse");
        }
    }

    public void OnControlsChanged(PlayerInput curPlayerInput)
    {
        // updates currentDevice enum so PlayerInputHandler is now aware of current input device
        ParseDeviceName(curPlayerInput.currentControlScheme, true);

        /*
        if (curPlayerInput.currentControlScheme == "Gamepad")
        {
            // Do something more if needed...
        }
        else if (curPlayerInput.currentControlScheme == "Keyboard and mouse")
        {
            // Do something more if needed...
        }
        */
    }

    // Applying deadzones on top of our RAWmovementDirection in order to determine the direction of our movement
    // Essentially, refining our movement as we refine down our RAWmovementDirection into individual components
    void RefineMovement()
    {
        if (RAWmovementDirection.magnitude <= universalFixedMinDeadzone)
        {
            // RAWmovementDirection magnitude is too low, so we just ignore it
            groundMovementDirection = aerialMovementDirection = universalMovementDirection = Vector2.zero;
            return;
        }

        universalMovementDirection = RAWmovementDirection;

        // Refining groundMovementDirection from our RAWmovementDirection that only detects horizontal movement (typically for ground movement applications)
        if (RAWmovementDirection.x > groundHorizontalDeadzone || RAWmovementDirection.x < -groundHorizontalDeadzone)
            groundMovementDirection.x = (RAWmovementDirection.x > 0f) ? 1f : -1f;
        else groundMovementDirection.x = 0f;

        // Refining aerialMovementDirection from our RAWmovementDirection that only detects vertical movement (typically for aerial movement applications)
        if (RAWmovementDirection.y > aerialVerticalDeadzone || RAWmovementDirection.y < -aerialVerticalDeadzone)
        {
            aerialMovementDirection.y = (RAWmovementDirection.y > 0f) ? 1f : -1f;
        } else
        {
            aerialMovementDirection.y = 0f;
        }
    }

    // Refining RAWattackDirection into an enum mean to represent the direction of our attack 
    void RefineAttack()
    {
        // Depending on our currentDevice, RAWattackDirection will be based on either our movement keys, or the position of our mass relative to our possessed character
        if(currentDevice == ControlDevices.Gamepad)
        {
            RAWattackDirection = RAWmovementDirection;
        } else if(currentDevice == ControlDevices.KeyboardAndMouse)
        {
            RAWattackDirection = mouseNormalized;
        }

        if(RAWattackDirection.magnitude <= universalFixedMinDeadzone)
        {
            playerAttackDirection = (int)attackDirection.NEUTRAL;
            return;
        }

        /* depending on the angle of our RAWattackDireciton, we will set the playerAttackDirection to different directions */

        if (Vector2.Angle(Vector2.right, RAWattackDirection.normalized) <= attackHorizontalAngleDeadzone / 2)
        {
            if(!possessedCharacter.facingRight)
            {
                possessedCharacter.facingRight = true;
                possessedCharacter.transform.localScale = new Vector3(1, 1, 1);
            }
            playerAttackDirection = (int)attackDirection.FORWARD;
        }

        if (Vector2.Angle(Vector2.left, RAWattackDirection.normalized) <= attackHorizontalAngleDeadzone / 2)
        {
            if (possessedCharacter.facingRight)
            {
                possessedCharacter.facingRight = false;
                possessedCharacter.transform.localScale = new Vector3(-1, 1, 1);
            }
            playerAttackDirection = (int)attackDirection.FORWARD;
        }

        else if (Vector2.Angle(Vector2.up, RAWattackDirection.normalized) <= attackVerticalAngleDeadzone / 2)
        {
            playerAttackDirection = (int)attackDirection.UP;
        }

        else if (Vector2.Angle(Vector2.down, RAWattackDirection.normalized) <= attackVerticalAngleDeadzone / 2)
        {
            playerAttackDirection = (int)attackDirection.DOWN;
        }
    }

    void OnMovementPerformed(InputAction.CallbackContext context)
    {
        RAWmovementDirection = context.ReadValue<Vector2>();
        RefineMovement();
        //RefineAttack();

        if(possessedCharacter != null)
        {
            possessedCharacter.PerformMovement(context);
        }
    }

    void OnJumpPerformed(InputAction.CallbackContext context)
    {
        if (possessedCharacter != null)
        {
            Debug.LogWarning("Jump performed");
            possessedCharacter.PerformJump(context);
        }
    }

    void OnDodgePerformed(InputAction.CallbackContext context)
    {
        if (possessedCharacter != null)
        {
            Debug.LogWarning("Dodge performed");
            possessedCharacter.PerformDodge(context);
        }
    }

    void OnLightAttackPerformed(InputAction.CallbackContext context)
    {
        if (possessedCharacter != null)
        {
            Debug.LogWarning("Light Attack performed");
            possessedCharacter.PerformLightAttack(context);
        }
    }

    void OnHeavyAttackPerformed(InputAction.CallbackContext context)
    {
        if (possessedCharacter != null)
        {
            Debug.LogWarning("Heavy Attack performed");
            possessedCharacter.PerformHeavyAttack(context);
        }
    }

    void OnBasicAbilityPerformed(InputAction.CallbackContext context)
    {
        if (possessedCharacter != null)
        {
            Debug.LogWarning("Basic Ability performed");
            possessedCharacter.PerformBasicAbility(context);
        }
    }

    void OnUltimateAbilityPerformed(InputAction.CallbackContext context)
    {
        if (possessedCharacter != null)
        {
            Debug.LogWarning("Ultimate Ability performed");
            possessedCharacter.PerformUltimateAbility(context);
        }
    }

    // throwing crown that allows you to possess other characters
    void OnCrownThrowPerformed(InputAction.CallbackContext context)
    {
        if (possessedCharacter != null)
        {
            Debug.LogWarning("Crown throw performed");
            possessedCharacter.PerformCrownThrow(context);
        }
    }

    private void OnEnable()
    {
        input.Player.Enable(); // enables the ability to process and read inputs
    }

    private void OnDisable()
    {
        input.Player.Disable(); // disables the ability to process and read inputs
    }
}
