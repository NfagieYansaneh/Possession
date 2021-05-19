using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class BaseCharacterController : MonoBehaviour
{
    // Character controller : https://github.com/Brackeys/2D-Character-Controller/blob/master/CharacterController2D.cs
    // Input system : https://www.youtube.com/watch?v=IurqiqduMVQ

    [Header("Base Character Controller Values")]
    public float jumpHeight = 1;
    [Range(0f, 1f)]
    public float movementSmoothing = 0f;        // How much to smooth out movement
    public bool applySmoothing = false;         // Decides whether to apply the ability to smooth out movement

    public float movementSpeed = 0f;            // Determines the speed of the characters movement
    public float gravity = 2f;                  // Determines the base strength of gravity
    public float gravityMultiplier = 1f;        // Used to amplify or weaken the base strength of gravity
    public bool airControl = true;              // Whether you can steer while jumping

    public LayerMask whatIsGround;              // A mask determining what is ground to the character
    public Transform groundCheck;               // A position marking where to check if the player is grounded
    public Transform ceilingCheck;              // A position marking where to check for ceilings

    public float groundedRadius = .2f;          // Radius of the overlap circle to determine if grounded
    [HideInInspector] public bool isGrounded;                    // Whether or not the player is grounded.
    public float ceilingRadius = .2f;           // Radius of the overlap circle to determine if the player can stand up

    private Rigidbody2D rb;
    public bool facingRight = true;            // For determining which way the player is currently facing.
    private Vector2 velocity = Vector2.zero;
    Vector2 targetVelocity = Vector2.zero;
    [HideInInspector] public float curVerticalVelocity = 0f;

    [HideInInspector] public float t_velocityTimestamp = 0f;
    [HideInInspector] public Vector2 t_velocity = Vector2.zero;

    [HideInInspector] public float t_gravityTimestamp = 0f;

    // Variable stores an instance of the InputMaster, which holds all of our input actions for input processing
    InputMaster input;

    public UnityEvent OnLandEvent;              // Event called when landed

    void Awake()
    {
        input = new InputMaster();

        // input.Player.Movement.performed += ctx => Debug.Log(ctx.ReadValueAsObject());  THIS IS A LAMBDA FUNCTION

        rb = GetComponent<Rigidbody2D>();
    }

    // FixedUpdate is called every 'x' seconds
    void FixedUpdate()
    {
        bool wasGrounded = isGrounded;
        isGrounded = false;

        // Player is grounded if the circlecast to groundcheck position hits anything designated as ground
        // Based on whether that object is in the ground layer
        Collider2D[] Colliders = Physics2D.OverlapCircleAll(groundCheck.position, groundedRadius, whatIsGround);

        // Loop through all detected 'ground' colliders in order to determine if we are grounded
        for (int i = 0; i < Colliders.Length; i++)
        {
            if (Colliders[i].gameObject != gameObject) // if the collider does not equal us
            {
                isGrounded = true;
                if (!wasGrounded) // If I was perivously not grounded, this means I had just landed
                {
                    // Debug.Log("Grounded");
                    OnLandEvent.Invoke();
                }
            }
        }

        Move();
        RunAtFixedUpdate();
    }

    public void Move()
    {
        // sets velocity w/ smoothing

        if (t_velocityTimestamp >= Time.time)
        {
            rb.velocity = (applySmoothing)? Vector2.SmoothDamp(rb.velocity, t_velocity, ref velocity, movementSmoothing) : t_velocity;
        } else {
            rb.velocity = (applySmoothing)? Vector2.SmoothDamp(rb.velocity, targetVelocity, ref velocity, movementSmoothing) : targetVelocity;
        }

        // Handles gravity
        if (t_gravityTimestamp <= Time.time)
        {
            if (!isGrounded)
            {
                // If not grounded, accelerate downwards
                curVerticalVelocity -= gravity * gravityMultiplier * Time.deltaTime;
            }
            else if (curVerticalVelocity < 0f)
            {
                // Else, stay still (-1f helps just sort out the possiblity of hovering over the ground)
                curVerticalVelocity = -1f;
            }

            rb.velocity = new Vector2(rb.velocity.x, curVerticalVelocity);
        } else {
            // do nothing
        }
    }

    // make the timed scripts
    public void SetVelocity(Vector2 newVelocity)
    {
        targetVelocity = newVelocity;
    }

    // set velocity over a duration of time. Decide whether to apply gravity or smoothing when doing so...
    private bool prevSmoothing;
    public void SetVelocityTimed(Vector2 newVelocity, float duration, bool smoothing, bool applyGravity)
    {
        t_velocityTimestamp = Time.time + duration;
        t_velocity = newVelocity;

        applySmoothing = smoothing;
        prevSmoothing = applySmoothing;

        if(!applyGravity) { 
            t_gravityTimestamp = Time.time + duration;
            curVerticalVelocity = 0f;
        }
    }

    public virtual void PerformMovement(InputAction.CallbackContext context)
    {
        // meant to be overwritten
    }

    public virtual void PerformJump(InputAction.CallbackContext context)
    {
        // meant to be overwritten
    }

    public virtual void PerformDodge(InputAction.CallbackContext context)
    {
        // meant to be overwritten
    }

    public virtual void PerformLightAttack(InputAction.CallbackContext context)
    {
        // meant to be overwritten
    }

    public virtual void PerformHeavyAttack(InputAction.CallbackContext context)
    {
        // meant to be overwritten
    }

    public virtual void PerformBasicAbility(InputAction.CallbackContext context)
    {
        // meant to be overwritten
    }

    public virtual void RunAtFixedUpdate()
    {
        // meant to be overwritten
    }
}
