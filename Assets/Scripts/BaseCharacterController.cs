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
    [Range(0f, 15f)]
    public float dodgeDistance;
    public AnimationCurve dodgeCurve;

    [HideInInspector] public float dodgeTimestamp = 0f;
    [HideInInspector] public bool gravityEnabled = false;
    [HideInInspector] public Vector2 dodgeVelocity = Vector2.zero;

    [HideInInspector] public float t_dodgeCurveTimestamp;
    public Vector2 dashingBoxSize = Vector2.zero;
    [HideInInspector] public Vector2 dashingBoxPosition = Vector2.zero;
    public Vector2 dashingBoxOffset = Vector2.zero;

    RaycastHit2D dashingHit;
    bool dashingBoxClipped = false;

    [Header("Ground Checking")]
    public LayerMask whatIsGround;              // A mask determining what is ground to the character
    public Transform groundCheck;               // A position marking where to check if the player is grounded
    public Transform ceilingCheck;              // A position marking where to check for ceilings
    public float groundedRadius = .2f;          // Radius of the overlap circle to determine if grounded
    public bool isGrounded;                     // Whether or not the player is grounded.
    public bool isFalling;                      // Whether or not the player is falling
    public float ceilingRadius = .2f;           // Radius of the overlap circle to determine if the player can stand up

    Vector2 ceilingCheckBoxSize = Vector2.zero;
    Vector2 ceilingCheckBoxPosition = Vector2.zero;

    RaycastHit2D ceilingHit;

    bool ceilingCheckBoxClipped = false;

    Vector2 groundCheckBoxSize = Vector2.zero;
    Vector2 groundCheckBoxPosition = Vector2.zero;

    RaycastHit2D[] groundHits = new RaycastHit2D[3]; // can recieve up to 3 hits
    RaycastHit2D groundHit;

    public Collider2D myCollider;
    public UnityEvent OnLandEvent;              // Event called when landed
    bool snapping = true;                       // When snapping the player towards ground

    // Other variables (misc and private)
    [HideInInspector] public Rigidbody2D rb;
    private Vector2 velocity = Vector2.zero;
    private Vector2 targetVelocity = Vector2.zero;

    [HideInInspector] public float curVerticalVelocity = 0f;

    // t_ variables are for timing purposes (and are hidden)

    // Variable stores an instance of the InputMaster, which holds all of our input actions for input processing
    InputMaster input;

    [Header("Crown")]
    public GameObject crownObject;
    public Crown crownScript;
    public static List<BaseCharacterController> baseCharacterControllers; // list containing all base character controllers in scene !!!!!
    //[Min(1f)]
    //public float crownAffinityScalar = 1f;

    [Header("Player Input Handler (For changing possession)")]
    public PlayerInputHandler playerInputHandler;
    public Vector2 movementDirection = Vector2.zero;


    void Awake()
    {
        input = new InputMaster();

        // input.Player.Movement.performed += ctx => Debug.Log(ctx.ReadValueAsObject());  THIS IS A LAMBDA FUNCTION

        rb = GetComponent<Rigidbody2D>();
        myCollider = GetComponent<Collider2D>();
    }

    private void Start()
    {
        // StartCoroutine(SnapToGround());
        groundCheckBoxSize = new Vector3(myCollider.bounds.size.x - 0.05f, 0.55f);
        ceilingCheckBoxSize = new Vector3(myCollider.bounds.size.x - 0.05f, 0.55f);
    }

    private void OnEnable()
    {
        if (baseCharacterControllers == null) baseCharacterControllers = new List<BaseCharacterController>();

        baseCharacterControllers.Add(this);
        foreach(BaseCharacterController baseCharacterController in baseCharacterControllers)
        {
            if (baseCharacterController == this) continue;
            Physics2D.IgnoreCollision(baseCharacterController.myCollider, myCollider);
        }
    }

    private void OnDisable()
    {
        baseCharacterControllers.Remove(this);
    }

    private void OnDrawGizmosSelected()
    {
        // Debugging sake
        if (!Application.isPlaying)
        {
            groundCheckBoxSize = new Vector3(myCollider.bounds.size.x - 0.05f, 0.55f);
            ceilingCheckBoxSize = new Vector3(myCollider.bounds.size.x - 0.05f, 0.55f);

            ceilingCheckBoxPosition = ceilingCheck.position + Vector3.up * ceilingCheckBoxSize.y / 2;
            groundCheckBoxPosition = groundCheck.position + Vector3.up * groundCheckBoxSize.y / 2;
            dashingBoxPosition = new Vector2(transform.position.x + dashingBoxOffset.x, transform.position.y + dashingBoxOffset.y);
        }

        // ground
        if (!groundHit || !Application.isPlaying)
        {
            Gizmos.color = Color.green;
        }
        else if (snapping)
            Gizmos.color = Color.yellow;
        else
            Gizmos.color = Color.red;
        //Gizmos.DrawWireCube(groundCheck.position, groundCheckBoxSize);
        Vector2 tempPosition = new Vector2(groundCheckBoxPosition.x, groundCheckBoxPosition.y - 0.55f / 2);
        Gizmos.DrawWireCube(tempPosition, groundCheckBoxSize);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, groundHit.point);

        // ceiling
        if (!ceilingCheckBoxClipped || !Application.isPlaying)
        {
            Gizmos.color = Color.green;
        } 
        else
        {
            Gizmos.color = Color.red;
        }

        Gizmos.DrawWireCube(ceilingCheckBoxPosition, ceilingCheckBoxSize);

        // dashing
        if (dashingBoxClipped) Gizmos.color = Color.red;
        else Gizmos.color = Color.green;
        Gizmos.DrawWireCube(dashingBoxPosition, dashingBoxSize);
        /* Gizmos.color = Color.magenta;
        if (groundHit == true)
        {
            float diff = groundHit.point.y - groundCheck.position.y;
            Gizmos.DrawLine(groundCheck.position, new Vector3(groundCheck.position.x, groundCheck.position.y - diff));
        } */

        Gizmos.color = Color.yellow;/*
        Gizmos.DrawLine(transform.position, groundHits[0].point);
        Gizmos.DrawLine(transform.position, groundHits[1].point);
        Gizmos.DrawLine(transform.position, groundHits[2].point);
        Gizmos.DrawLine(transform.position, groundHits[3].point);
        Gizmos.DrawLine(transform.position, groundHits[4].point);*/
    }

    void Update()
    {
        if(Time.time > dodgeTimestamp)
        {
            dodging = false;
            gravityEnabled = true;
            // Do some end dodging shiz
        }

        ceilingCheckBoxPosition = ceilingCheck.position + Vector3.up * ceilingCheckBoxSize.y / 2;
        groundCheckBoxPosition = groundCheck.position + Vector3.up * groundCheckBoxSize.y / 2;
        if(facingRight)
            dashingBoxPosition = new Vector2(transform.position.x + dashingBoxOffset.x, transform.position.y + dashingBoxOffset.y);
        else
            dashingBoxPosition = new Vector2(transform.position.x - dashingBoxOffset.x, transform.position.y + dashingBoxOffset.y);
    }

    // FixedUpdate is called every 'x' seconds
    void FixedUpdate()
    {

        HandleCollisionsAndSnapping();
        HandleMovement();
        RunAtFixedUpdate();
    }

    public void HandleCollisionsAndSnapping()
    {
        bool wasGrounded = isGrounded;
        isGrounded = false;

        // Player is grounded if the circlecast to groundcheck position hits anything designated as ground
        // Based on whether that object is in the ground layer
        //position = groundCheck.position + Vector3.up * groundCheckBoxSize.y/2;
  
        ContactFilter2D contactFilter2D = new ContactFilter2D();
        contactFilter2D.SetLayerMask(whatIsGround);

        int numberOfContacts = Physics2D.BoxCast(groundCheckBoxPosition, groundCheckBoxSize, 0f, Vector2.down, contactFilter2D, groundHits, 0.55f / 2);
        Debug.Log(numberOfContacts);
        float previousDistanceY = 0f;
        groundHit = new RaycastHit2D();
        Vector2 previousClosestPoint = Vector2.zero;
        RaycastHit2D previousHit = new RaycastHit2D();

        for (int index = 0; index < numberOfContacts; index++)
        {
            Vector2 position = groundCheckBoxPosition;
            position.y = groundCheckBoxPosition.y + groundCheckBoxSize.y / 2;
            Vector2 closestPoint = Physics2D.ClosestPoint(position, groundHits[index].collider);
            //if (closestPoint.y > position.y) closestPoint.y = position.y;

            Debug.DrawRay(closestPoint, Vector2.up, Color.red);
            float distanceY = groundCheckBoxPosition.y - closestPoint.y;
            if (previousDistanceY > distanceY || previousDistanceY == 0f)
            {
                previousDistanceY = distanceY;
                previousClosestPoint = closestPoint;
                previousHit = groundHits[index];
            }
        }

        if (previousClosestPoint != Vector2.zero)
        {
            // this seems inefficient?
            Vector2 direction;
            if (previousClosestPoint.x < groundCheckBoxPosition.x + groundCheckBoxSize.x / 2 && previousClosestPoint.x > groundCheckBoxPosition.x - groundCheckBoxSize.x / 2 &&
                previousClosestPoint.y < groundCheckBoxPosition.y + groundCheckBoxSize.y / 2 && previousClosestPoint.y > groundCheckBoxPosition.y - groundCheckBoxSize.y / 2)
            {
                direction = previousClosestPoint - new Vector2(transform.position.x, transform.position.y);
            } else
            {
                direction = previousHit.point - new Vector2(transform.position.x, transform.position.y);
            }

            groundHit = Physics2D.Raycast(transform.position, direction.normalized, direction.magnitude + 0.5f, whatIsGround);
            float dot = Vector2.Dot(groundHit.normal, Vector2.right);
            if (dot == 1f || dot == -1f) groundHit = new RaycastHit2D();
        }

        // ground
        if (groundHit == true)
        {
            Debug.DrawRay(groundHit.point, groundHit.normal, Color.magenta);

            // If ground has been detected
            if (curVerticalVelocity <= 0f)
            {
                isGrounded = true;
                isFalling = false;

                // reset our jumps
                jumpIndex = 1;
                airJumpsPerformed = 0;
                curVerticalVelocity = 0f;

                if (!dodging || dodging && dodgeVelocity.y < 0f && isGrounded)
                {
                    float distanceToGround = groundCheck.transform.position.y - groundHit.point.y;
                    if (distanceToGround != 0f) snapping = true;
                    else snapping = false;

                    //if(distanceToGround > 0f)
                    transform.position -= Vector3.up * (distanceToGround);
                }

                if (!wasGrounded)
                {

                    OnLandEvent.Invoke();
                }
            }
        }

        // ceiling
        if (curVerticalVelocity > 0f)
        {
            ceilingCheckBoxClipped = Physics2D.OverlapBox(ceilingCheckBoxPosition, ceilingCheckBoxSize, 0f, whatIsGround);
        }
        else ceilingCheckBoxClipped = false;

        // dashing
        dashingBoxClipped = Physics2D.OverlapBox(dashingBoxPosition, dashingBoxSize, 0f, whatIsGround);
    }

    bool slowing = false;
    public void HandleMovement()
    {
        // Handle Dashing

        // Handle General movement
        Vector2 velocity;
        if (dodging)
        {
            velocity = dodgeVelocity * dodgeCurve.Evaluate((Time.time - t_dodgeCurveTimestamp)/dodgeTime);
        }
        else
        {
            if (!slowing)
            {
                velocity = targetVelocity * startMovementCurve.Evaluate(Time.time - t_startMovementCurveTimestamp);
            }
            else
            {
                velocity = (facingRight) ? targetVelocity * endMovementCurve.Evaluate(Time.time - t_startMovementCurveTimestamp) :
                   targetVelocity * endMovementCurve.Evaluate(Time.time - t_startMovementCurveTimestamp) ;
            }
        }

        // Handles Gravity to calculate curVerticalVelocity
        if (!isGrounded && gravityEnabled)
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
        else if (gravityEnabled)
        {
            curVerticalVelocity = 0f;
        }
        Debug.DrawRay(groundHit.point, Vector3.down, Color.cyan);
        if (isGrounded && playerInputHandler.groundMovementDirection != Vector2.zero)
        {
            float dot = Vector2.Dot(groundHit.normal, Vector2.right);
            if (dot == 1f || dot == -1f)
            {
                Debug.LogWarning("?");
                return;
            }
            //Debug.DrawRay(groundHit.point, Vector3.down, Color.cyan);
            rb.velocity = -Vector2.Perpendicular(groundHit.normal) * velocity.x;
            Debug.DrawRay(transform.position, rb.velocity.normalized, Color.blue);
            Debug.Log("v : " + rb.velocity + " vm : " + rb.velocity.magnitude);
            return;
        }

        rb.velocity = new Vector2(velocity.x, curVerticalVelocity);
    }

    public void SetVelocity(Vector2 newVelocity)
    {
        if (newVelocity == Vector2.zero)
        {
            t_startMovementCurveTimestamp = Time.time;
            slowing = true;
            return;
        }

        slowing = false;
        t_startMovementCurveTimestamp = Time.time;
        targetVelocity = newVelocity;
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
        // not meant to be typically overwritten

        if (playerInputHandler.RAWmovementDirection == Vector2.zero) // make sure to set proper deadzones!
        {
            dodgeTimestamp = Time.time + neutralDodgeTime;
            dodgeVelocity = Vector2.zero;
        }
        else
        {
            dodgeTimestamp = Time.time + dodgeTime;
            dodgeVelocity = playerInputHandler.RAWmovementDirection.normalized * (dodgeDistance / dodgeTime * (dodgeCurve.keys[2].time - dodgeCurve.keys[1].time));
        }

        t_dodgeCurveTimestamp = Time.time;
        gravityEnabled = false;
        dodging = true;
        curVerticalVelocity = 0f;

        // please, shift jumpIndex down by 1 oml
        // we reset our jumps, however, we remove our ground jump by setting jumpIndex to 2 (which represents our air jump), instead of 1 (which represents our ground jump)
        jumpIndex = 2;
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
