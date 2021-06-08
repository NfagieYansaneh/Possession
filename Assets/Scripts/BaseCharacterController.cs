using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class BaseCharacterController : MonoBehaviour
{
    // Character controller : https://github.com/Brackeys/2D-Character-Controller/blob/master/CharacterController2D.cs
    // Input system : https://www.youtube.com/watch?v=IurqiqduMVQ


    [Header("Movement")]
    [Header("-----Base Character Controller Values-----")]
    [Range(0f, 1f)]
    public float movementSmoothing = 0f;        // How much to smooth out movement
    public bool applySmoothing = false;         // Decides whether to apply the ability to smooth out movement
    public float movementSpeed = 0f;            // Determines the speed of the characters movement
    public bool facingRight = true;             // For determining which way the player is currently facing.

    public AnimationCurve startMovementCurve; // how movement rises when started
    public AnimationCurve endMovementCurve; // how movement decays when stopped

    // t_ variables are for timing purposes (and are hidden)
    [HideInInspector] public float t_startMovementCurveTimestamp;
    [HideInInspector] public float t_endMovementCurveTimestamp;

    [Header("Gravity & Jumps")]
    public bool airControl = true;              // Whether you can steer while jumping
    public float jumpHeight = 1;
    public float airborneJumpHeight = 1;

    [Tooltip("successiveJumpHeightReduction")]
    [Range(0f, 1f)]
    public float successiveJumpHeightReduction; // reduces the height of each successive airborne jump
    public int maxJumps = 2;                    // tells us how many jumps can we perform (including a jump from the ground)
    public int jumpIndex = 1;                   // tells us which jump we are at (1 represent our first jump);
    public int airJumpsPerformed = 0;           // tells us how many air jumps we have performed while airborne

    public float gravity = 2f;                  // Determines the base strength of gravity
    public float gravityMultiplier = 1f;        // Used to amplify or weaken the base strength of gravity
    [Range(1f, 3f)]
    public float fallingGravityMultiplier = 1.2f; // Used to amplify gravity when falling for the sake of game feel

    [Header("Dodging")]
    public bool dodging = false;
    [Range(0f, 1f)]
    public float dodgeTime;
    [Range(0f, 1f)]
    public float neutralDodgeTime;
    [Range(0f, 5f)]
    public float dodgeDistance;
    public AnimationCurve dodgeCurve;
    [HideInInspector] public float t_dodgeCurveTimestamp;


    [Header("Ground Checking")]
    public LayerMask whatIsGround;              // A mask determining what is ground to the character
    public Transform groundCheck;               // A position marking where to check if the player is grounded
    public Transform ceilingCheck;              // A position marking where to check for ceilings
    public float groundedRadius = .2f;          // Radius of the overlap circle to determine if grounded
    public bool isGrounded;                     // Whether or not the player is grounded.
    public bool isFalling;                      // Whether or not the player is falling
    public float ceilingRadius = .2f;           // Radius of the overlap circle to determine if the player can stand up
    public Collider2D myCollider;
    public UnityEvent OnLandEvent;              // Event called when landed

    // Other variables (misc and private)
    [HideInInspector] public Rigidbody2D rb;
    private Vector2 velocity = Vector2.zero;
    private Vector2 targetVelocity = Vector2.zero;

    [HideInInspector] public float curVerticalVelocity = 0f;

    // t_ variables are for timing purposes (and are hidden)
    [HideInInspector] public float t_velocityTimestamp = 0f;
    [HideInInspector] public Vector2 t_velocity = Vector2.zero;
    [HideInInspector] public float t_gravityTimestamp = 0f;

    // Variable stores an instance of the InputMaster, which holds all of our input actions for input processing
    InputMaster input;

    [Header("Crown")]
    public GameObject crownObject;
    public Crown crownScript;
    public static List<BaseCharacterController> baseCharacterControllers; // list containing all base character controllers in scene
    //[Min(1f)]
    //public float crownAffinityScalar = 1f;

    [Header("Player Input Handler (For changing possession)")]
    public PlayerInputHandler playerInputHandler;
    public Vector2 movementDirection = Vector2.zero;


    void Awake()
    {
        input = new InputMaster();

        t_velocityTimestamp = 0f;
        t_velocity = Vector2.zero;
        t_gravityTimestamp = 0f;

        // input.Player.Movement.performed += ctx => Debug.Log(ctx.ReadValueAsObject());  THIS IS A LAMBDA FUNCTION

        rb = GetComponent<Rigidbody2D>();
        myCollider = GetComponent<Collider2D>();
    }

    private void Start()
    {
        //StartCoroutine(SnapToGround());
        size = new Vector3(myCollider.bounds.size.x - 0.05f, 0.55f);
    }

    private void OnEnable()
    {
        if (baseCharacterControllers == null) baseCharacterControllers = new List<BaseCharacterController>();

        baseCharacterControllers.Add(this);
    }

    private void OnDisable()
    {
        baseCharacterControllers.Remove(this);
    }

    Vector3 size;
    RaycastHit2D groundHit;

    Vector2 position;
    private void OnDrawGizmosSelected()
    {
        if (groundHit == false)
        {
            Gizmos.color = Color.magenta;
        }
        else if (snapping)
            Gizmos.color = Color.yellow;
        else
            Gizmos.color = Color.red;
        //Gizmos.DrawWireCube(groundCheck.position, size);
        Vector2 tempPosition = new Vector2(position.x, position.y - 0.55f / 2);
        Gizmos.DrawWireCube(tempPosition, size);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, groundHit.point);

        Gizmos.color = Color.magenta;
        if (groundHit == true)
        {
            float diff = groundHit.point.y - groundCheck.position.y;
            Gizmos.DrawLine(groundCheck.position, new Vector3(groundCheck.position.x, groundCheck.position.y - diff));
        }
    }

    void Update()
    {

    }

    // FixedUpdate is called every 'x' seconds
    void FixedUpdate()
    {
        bool wasGrounded = isGrounded;
        isGrounded = false;

        // Player is grounded if the circlecast to groundcheck position hits anything designated as ground
        // Based on whether that object is in the ground layer
        position = groundCheck.position + Vector3.up * size.y/2;
        groundHit = Physics2D.BoxCast(position, size, 0f, Vector2.down, 0.55f / 2, whatIsGround);

        if(groundHit == true)
        {
            if (curVerticalVelocity <= 0f)
            {
                isGrounded = true;
                isFalling = false;

                // reset our jumps
                jumpIndex = 1;
                airJumpsPerformed = 0;
                curVerticalVelocity = 0f;

                distanceToGround = groundCheck.transform.position.y - groundHit.point.y;
                if (distanceToGround != 0f) snapping = true;
                else snapping = false;

                //if(distanceToGround > 0f)
                transform.position -= Vector3.up * (distanceToGround);

                if (!wasGrounded)
                {

                    OnLandEvent.Invoke();
                }
            }
        }

        //Collider2D[] Colliders = Physics2D.OverlapCircleAll(groundCheck.position, groundedRadius, whatIsGround);

        // Loop through all detected 'ground' colliders in order to determine if we are grounded

        /*
        for (int i = 0; i < Colliders.Length; i++)
        {
            if (Colliders[i].gameObject != gameObject) // if the collider does not equal us
            {
                if (curVerticalVelocity < 0f)
                {
                    isGrounded = true;
                    isFalling = false;

                    // reset our jumps
                    jumpIndex = 1;
                    airJumpsPerformed = 0;
                }

                if (!wasGrounded && curVerticalVelocity < 0f) // If I was perivously not grounded, this means I had just landed
                {
                    // Debug.Log("Grounded");
                    // Snap to ground
                    RaycastHit2D hit = Physics2D.BoxCast(new Vector2(transform.position.x, transform.position.y - transform.localScale.y/2), Vector2.one, 0, Vector2.down, 1f, whatIsGround);
                    if (hit == true)
                    {
                        Debug.Log("Hey");
                        float distanceToGround = transform.position.y - hit.point.y;
                        transform.position += Vector3.up * (0.5f - distanceToGround);
                    }

                    OnLandEvent.Invoke();
                }
            }
        }
        */

        // Move();
        HandleMovement();
        RunAtFixedUpdate();
    }

    bool snapping = false;
    float distanceToGround = 0f;

    public void HandleMovement()
    {
        // Handle Dashing

        // Handle General movement
        Vector2 velocity;
        velocity = targetVelocity * startMovementCurve.Evaluate(Time.time - t_startMovementCurveTimestamp);

        // Handles Gravity
        if (!isGrounded)
        {
            if (curVerticalVelocity < 0f)
            {
                // applies fallingGravityMultiplier if we are falling
                curVerticalVelocity -= gravity * gravityMultiplier * fallingGravityMultiplier * Time.deltaTime;
                isFalling = true;
            }
            else
            {
                // else apply typical gravity
                curVerticalVelocity -= gravity * gravityMultiplier * Time.deltaTime;
            }
        } 
        else
        {
            curVerticalVelocity = 0f;
        }

        if (isGrounded && groundHit)
        {
            rb.velocity = new Vector2(velocity.x, 0f);
            Debug.DrawRay(groundHit.point, groundHit.normal, Color.magenta);
            float dot = Vector2.Dot(groundHit.normal, Vector2.right);
            if (dot == 1f || dot == -1f) return;
            rb.velocity = Vector3.ProjectOnPlane(rb.velocity, groundHit.normal);
            return;
        }
        rb.velocity = new Vector2(velocity.x, curVerticalVelocity);
    }

    public void Move()
    {
        // sets velocity w/ smoothing

        if (t_velocityTimestamp >= Time.time)
        {
            // if we are still within a timed velocity's period, than use its timed velocity
            if (dodging)
            {
                float x = (Time.time - t_dodgeCurveTimestamp) / dodgeTime;
                rb.velocity = t_velocity * dodgeCurve.Evaluate(x);
            }
            else
            {
                //Debug.Log("WAKKKAWAKKKA");
                //rb.velocity = (applySmoothing) ? Vector2.SmoothDamp(rb.velocity, t_velocity, ref velocity, movementSmoothing) : t_velocity;
            }
        } else {
            // rb.velocity = (applySmoothing)? Vector2.SmoothDamp(rb.velocity, targetVelocity, ref velocity, movementSmoothing) : targetVelocity;
            rb.velocity = targetVelocity * startMovementCurve.Evaluate(Time.time - t_startMovementCurveTimestamp);
        }

        // Handles gravity
        if (t_gravityTimestamp <= Time.time)
        {
            if (!isGrounded)
            {
                // If not grounded, accelerate downwards
                if(curVerticalVelocity < 0f)
                {
                    // applies fallingGravityMultiplier if we are falling
                    curVerticalVelocity -= gravity * gravityMultiplier * fallingGravityMultiplier * Time.deltaTime;
                    isFalling = true; 
                } else {
                    // else apply typical gravity
                    curVerticalVelocity -= gravity * gravityMultiplier * Time.deltaTime;
                }
            }
            else
            {
                // Else, stay still (-1f helps just sort out the possiblity of hovering over the ground)

                curVerticalVelocity = 0f;
            }

            if (isGrounded)
            {
                RaycastHit2D hit = Physics2D.Raycast(transform.position, Vector2.down, 2f, whatIsGround);
                if (hit != false)
                {
                    //Debug.Log("Hey?");
                    rb.velocity = Vector3.ProjectOnPlane(rb.velocity, hit.normal);
                }
            }
            else
            {

                rb.velocity = new Vector2(rb.velocity.x, curVerticalVelocity);
            }
        } else {
            // do nothing
        }

        // STOP USING rb.velocity AND USE VELOCITY (Performant?)

    }

    public void SetVelocity(Vector2 newVelocity)
    {
        t_startMovementCurveTimestamp = Time.time;
        targetVelocity = newVelocity;
    }

    // set velocity over a duration of time. Decide whether to apply gravity or smoothing when doing so...
    private bool prevSmoothing;
    public void SetVelocityTimed(Vector2 newVelocity, float duration, bool applyGravity)
    {
        t_velocityTimestamp = Time.time + duration;
        t_velocity = newVelocity;

        if(!applyGravity) { 
            t_gravityTimestamp = Time.time + duration;
            curVerticalVelocity = 0f;
        }
    }

    // functions that can be overwritten depending on each characters needs

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

    public virtual void PerformUltimateAbility(InputAction.CallbackContext context)
    {
        // meant to be overwritten
    }

    public virtual void PerformCrownThrow(InputAction.CallbackContext context)
    {
        // not typically meant to be overwritten
        crownObject.SetActive(true);
        crownObject.transform.position = transform.position;
        crownScript.ThrowMe(playerInputHandler.RAWmovementDirection, myCollider);
    }

    public virtual void PossessMe()
    {
        // not typically meant to be overwritten
        playerInputHandler.possessedCharacter.OnPossessionLeave(); // calls the 'leave' function on previous possession
        playerInputHandler.possessedCharacter = this;
    }

    public virtual void OnPossessionLeave()
    {
        // not typically meant to be overwritten
        movementDirection = Vector2.zero;
        targetVelocity = Vector2.zero;
    }

    public virtual void RunAtFixedUpdate()
    {
        // meant to be overwritten
    }
}
