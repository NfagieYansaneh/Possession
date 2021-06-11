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
    [Tooltip("Max slope angle will not go up to 85f because that is a situation that will never occurr")]
    [Range(0f, 85f)]
    public float maxSlopeAngle = 45f;
    [Range(0f, 85f)]
    public float maxSteepSlopeAngle = 55f;
    public bool isSliding;                     // Whether or not the player is sliding.
    public bool isFalling;                      // Whether or not the player is falling
    public bool isOnWalkableSlope;
    public bool isOnSteepSlope;
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

        // Checking for ground contacts
        ContactFilter2D groundContactFilter2D = new ContactFilter2D();
        groundContactFilter2D.SetLayerMask(whatIsGround);

        int numberOfContacts = Physics2D.BoxCast(groundCheckBoxPosition, groundCheckBoxSize, 0f, Vector2.down, groundContactFilter2D, groundHits, 0.55f / 2);
        groundHit = new RaycastHit2D();
        bool groundContactFound = false;

        float previousDistanceY = 0f;
        Vector2 previousClosestPoint = Vector2.zero;
        RaycastHit2D previousHit = new RaycastHit2D();

        // Filters through ground contacts to find ground contact closest to player's feet
        for (int index = 0; index < numberOfContacts; index++)
        {
            Vector2 position = groundCheckBoxPosition;
            position.y = groundCheckBoxPosition.y;
            Vector2 closestPoint = Physics2D.ClosestPoint(position, groundHits[index].collider);
            //if (closestPoint.y > position.y) closestPoint.y = position.y;

            Debug.DrawRay(closestPoint, Vector2.up, Color.red);
            float distanceY = groundCheckBoxPosition.y - closestPoint.y;
            if (previousDistanceY > distanceY || groundContactFound == false)
            {
                previousDistanceY = distanceY;
                previousClosestPoint = closestPoint;
                previousHit = groundHits[index];

                groundContactFound = true;
            }
        }

        // if contact point was found, vett it to determine whether it reaches the requirements for a ground contact point
        if (groundContactFound)
        {
            // this seems inefficient?

            // Checks wether the closest point is withing player's groundCheckBox that is meant to represent the player's feet
            Vector2 direction;
            if (previousClosestPoint.x <= groundCheckBoxPosition.x + groundCheckBoxSize.x / 2 && previousClosestPoint.x >= groundCheckBoxPosition.x - groundCheckBoxSize.x / 2 &&
                previousClosestPoint.y <= groundCheckBoxPosition.y)
            {
                Debug.Log("Closest Point");
                direction = previousClosestPoint - new Vector2(transform.position.x, transform.position.y);
                groundHit = Physics2D.Raycast(transform.position, direction.normalized, direction.magnitude + 0.5f, whatIsGround);
            }
            else if (previousHit.point.y <= groundCheckBoxPosition.y) // else use groundHit (which may not be the closest point to the players feet)
            {
                Debug.LogError("Using previous hit");
                groundHit = previousHit;
            }
            else // contact point is not desirable
            {
                groundContactFound = false;
                Debug.LogWarning("oh no"); // 
            }

            // if we found a desirable ground contact point...
            Debug.DrawRay(groundHit.point, groundHit.normal, Color.magenta);

            // If ground has been detected
            if (groundContactFound && curVerticalVelocity <= 0f && groundHit == true)
            {
                isGrounded = (85f <= Vector2.Angle(Vector2.up, groundHit.normal) && Vector2.Angle(Vector2.up, groundHit.normal) <= 95f) ? false : true;
                isFalling = false;
                isOnWalkableSlope = (Vector2.Angle(Vector2.up, groundHit.normal) < maxSlopeAngle) ? true : false;
                isOnSteepSlope = (Vector2.Angle(Vector2.up, groundHit.normal) < maxSteepSlopeAngle) ? true : false;
                isSliding = (isOnSteepSlope || 85f <= Vector2.Angle(Vector2.up, groundHit.normal) && Vector2.Angle(Vector2.up, groundHit.normal) <= 95f) ? false : true;
                // reset our jumps
                jumpIndex = 1;
                airJumpsPerformed = 0;

                if ((!dodging || dodging && dodgeVelocity.y < 0f) && isGrounded && (isOnWalkableSlope || isOnSteepSlope))
                {
                    bool willSnap = true;
                    // apply snapping when
                    if(isOnSteepSlope)
                    {
                        if(playerInputHandler.groundMovementDirection.x != 0f && Vector2.Dot(groundHit.normal.normalized, playerInputHandler.groundMovementDirection.normalized) > 0f)
                        {
                            willSnap = false;
                        } else isOnWalkableSlope = true;

                        if (playerInputHandler.RAWmovementDirection.y < 0f)
                        {
                            isOnWalkableSlope = true;
                            willSnap = true;
                        }
                    }

                    if (willSnap)
                    {
                        Debug.LogWarning("Snapping?");
                        float distanceToGround = groundCheck.transform.position.y - groundHit.point.y;
                        if (distanceToGround != 0f) snapping = true;
                        else snapping = false;

                        transform.position -= Vector3.up * (distanceToGround);
                    }
                }

                if (!wasGrounded)
                {
                    OnLandEvent.Invoke();
                }
            }
        }

        // ceiling checking
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
            rb.velocity = velocity;
            return;

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
        if (!isGrounded && gravityEnabled || isSliding && gravityEnabled)
        {

            if (curVerticalVelocity < 0f && !isSliding)
            {
                // Debug.LogWarning("Gravity Activated AMP");
                // applies fallingGravityMultiplier if we are falling
                curVerticalVelocity -= gravity * gravityMultiplier * fallingGravityMultiplier * Time.deltaTime;
                isFalling = true;
            }
            else
            {
                // Debug.LogWarning("Gravity Activated");
                // else apply typical gravity
                curVerticalVelocity -= gravity * gravityMultiplier * Time.deltaTime;
            }
        } 
        else if (gravityEnabled)
        {
            curVerticalVelocity = 0f;
        }

        // Debug.DrawRay(transform.position, rb.velocity.normalized, Color.blue);
        // Debug.DrawRay(groundHit.point, Vector3.down, Color.cyan);

        // Sliding and grounded
        if(isSliding && isGrounded)
        {
            if (Vector2.Dot(groundHit.normal.normalized, playerInputHandler.groundMovementDirection) > 0f)
            {
                curVerticalVelocity = 0f;
                rb.velocity = new Vector2(velocity.x, 0f);
                return;
            }
            else
            {
                rb.velocity = Vector2.Perpendicular(groundHit.normal) * -curVerticalVelocity * Vector2.Dot(Vector2.up, -Vector2.Perpendicular(groundHit.normal));
                return;
            }
        }

        // Not sliding and grounded
        else if (playerInputHandler.groundMovementDirection.x != 0f && isGrounded && !(isOnSteepSlope && Vector2.Dot(groundHit.normal.normalized, playerInputHandler.groundMovementDirection.normalized) > 0f) && playerInputHandler.RAWmovementDirection.y >= 0f)
        {
            rb.velocity = -Vector2.Perpendicular(groundHit.normal) * velocity.x;
            return;
        }

        // Sliding but not grounded (stopgap for a bug)

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
