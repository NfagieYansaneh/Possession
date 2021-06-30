using ExitGames.Client.Photon.StructWrapping;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum attackDirection { NEUTRAL, FORWARD, UP, DOWN };
public enum attackType { LIGHT, HEAVY, BASIC_ABILITY, ULTIMATE_ABILITY };

public class PlayerInputHandler : MonoBehaviour
{
    // Variable stores an instance of the InputMaster, which holds all of our input actions for input processing
    InputMaster input;

    public Color playerColor;

    public float universalFixedMinDeadzone = 0.125f;
    public float universalFixedMaxDeadzone = 0.925f;

    public float groundHorizontalDeadzone = 0.2f;
    public float groundVerticalDeadzone = 0.2f;

    public float aerialHorizontalDeadzone = 0.2f;
    public float aerialVerticalDeadzone = 0.2f;

    public float attackVerticalAngleDeadzone = 90f;
    public float attackHorizontalAngleDeadzone = 90f;
    public float attackNeutralRadius = 1f; // Only for KB&M user

    public int playerAttackDirection = (int)attackDirection.NEUTRAL;
    public int playerAttackType = (int)attackType.LIGHT;

    Vector2 mousePosition = Vector2.zero;
    public Vector2 mouseNormalized = Vector2.zero;

    [HideInInspector] public Vector2 RAWmovementDirection     = Vector2.zero;
    [HideInInspector] public Vector2 universalMovementDirection = Vector2.zero;
    [HideInInspector] public Vector2 groundMovementDirection  = Vector2.zero;
    [HideInInspector] public Vector2 aerialMovementDirection  = Vector2.zero;
    [HideInInspector] public Vector2 dashingMovementDirection = Vector2.zero;

    [HideInInspector] public Vector2 RAWattackDirection       = Vector2.zero;
    [HideInInspector] public Vector2 groundAttackDirection    = Vector2.zero;
    [HideInInspector] public Vector2 aerialAttackDirection    = Vector2.zero;

    [HideInInspector] public bool spaceKeyHeld = false;

    public BaseCharacterController possessedCharacter;

    private void Awake()
    {
        input = new InputMaster();

        // when movement keys are performed, call "OnMovementPerformed" function
        input.Player.Movement.started += OnMovementPerformed;
        input.Player.Movement.performed += OnMovementPerformed;
        input.Player.Movement.canceled += OnMovementPerformed;

        input.Player.Jump.started += ctx => spaceKeyHeld = true;
        input.Player.Jump.performed += OnJumpPerformed; // when the jump keys are performed, call "OnJumpPerformed" function
        input.Player.Jump.canceled += ctx => spaceKeyHeld = false;

        input.Player.Dodge.performed += OnDodgePerformed; // when the dodge keys are performed, call "OnDodgePerformed" function

        input.Player.LightAttack.performed += OnLightAttackPerformed;

        input.Player.HeavyAttack.performed += OnHeavyAttackPerformed;

        input.Player.BasicAbility.performed += OnBasicAbilityPerformed;

        input.Player.UltimateAbility.performed += OnUltimateAbilityPerformed;

        input.Player.CrownThrow.performed += OnCrownThrowPerformed;

        //input.Player.MousePosition.started += OnMousePositionPerformed;
        //input.Player.MousePosition.performed += OnMousePositionPerformed;
        //input.Player.MousePosition.canceled += OnMousePositionPerformed;
        
    }

    public void Update()
    {
        mousePosition = Camera.main.ScreenToWorldPoint(input.Player.MousePosition.ReadValue<Vector2>());
        Vector2 directionRelativeToPlayer = new Vector3(mousePosition.x, mousePosition.y) - possessedCharacter.transform.position;
        mouseNormalized = directionRelativeToPlayer.normalized;
        RefineAttack();
        //Vector2 mousePositon = mainCam.ScreenToWorldPoint(Mouse.current.position);
    }

    public void OnGUI()
    {
        GUI.Label(new Rect(20f, 120f, 150f, 40f), new GUIContent("Current Health "));
        possessedCharacter.healthHandler.currentHealth =
            (int)GUI.HorizontalSlider(new Rect(20f, 140f, 150f, 40f), possessedCharacter.healthHandler.currentHealth, 0f, possessedCharacter.healthHandler.maxHealth);
    }

#if UNITY_EDITOR
    public void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(possessedCharacter.transform.position,
            mousePosition);
    }
#endif

    void RefineMovement()
    {
        if (RAWmovementDirection.magnitude <= universalFixedMinDeadzone)
        {
            groundMovementDirection = aerialMovementDirection = universalMovementDirection = Vector2.zero;
            return;
        }

        universalMovementDirection = RAWmovementDirection;

        // Refining groundMovementDirection that only handles horizontal ground movement
        if (RAWmovementDirection.x > groundHorizontalDeadzone || RAWmovementDirection.x < -groundHorizontalDeadzone)
            groundMovementDirection.x = (RAWmovementDirection.x > 0f) ? 1f : -1f;
        else groundMovementDirection.x = 0f;

        // Aerial
        if (RAWmovementDirection.y > aerialVerticalDeadzone || RAWmovementDirection.y < -aerialVerticalDeadzone)
        {
            aerialMovementDirection.y = (RAWmovementDirection.y > 0f) ? 1f : -1f;
        } else
        {
            aerialMovementDirection.y = 0f;
        }
    }

    void RefineAttack()
    {
        // detect device change at home
        //if (Input.c)
        //{
            RAWattackDirection = mouseNormalized;
        //} 
        /*else if (input.controlSchemes.Equals("Gamepad"))
        {
            RAWattackDirection = RAWmovementDirection;
        }*/

        if(RAWattackDirection.magnitude <= universalFixedMinDeadzone)
        {
            playerAttackDirection = (int)attackDirection.NEUTRAL;
            return;
        }

        //pls program a flip script 

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
            possessedCharacter.PerformJump(context);
        }
    }

    void OnDodgePerformed(InputAction.CallbackContext context)
    {
        if (possessedCharacter != null)
        {
            possessedCharacter.PerformDodge(context);
        }
    }

    void OnLightAttackPerformed(InputAction.CallbackContext context)
    {
        if (possessedCharacter != null)
        {
            possessedCharacter.PerformLightAttack(context);
        }
    }

    void OnHeavyAttackPerformed(InputAction.CallbackContext context)
    {
        if (possessedCharacter != null)
        {
            possessedCharacter.PerformHeavyAttack(context);
        }
    }

    void OnBasicAbilityPerformed(InputAction.CallbackContext context)
    {
        if (possessedCharacter != null)
        {
            possessedCharacter.PerformBasicAbility(context);
        }
    }

    void OnUltimateAbilityPerformed(InputAction.CallbackContext context)
    {
        if (possessedCharacter != null)
        {
            possessedCharacter.PerformUltimateAbility(context);
        }
    }

    void OnCrownThrowPerformed(InputAction.CallbackContext context)
    {
        if (possessedCharacter != null)
        {
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
