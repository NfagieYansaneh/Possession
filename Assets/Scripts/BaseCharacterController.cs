using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using System;

public enum animationCurves { LINEAR, CONSTANT, EXPONENTIAL, EXPONENTIAL_DECAY };
public enum backgroundVelocityType { GRAVITY_INCORPORATED_WITH_FRICTION, LERPING_EFFECT };

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

    [Header("Background Velocities")]
    private bool backgroundVelocitiesCurrentlyActive = false;
    public List<Vector2> backgroundVelocities;
    public List<float> backgroundVelocityTimestamps;
    public List<float> backgroundVelocityDurations;
    public List<AnimationCurve> backgroundVelocityCurves;
    public List<backgroundVelocityType> backgroundVelocityTypes;
    public List<int> backgroundVelocityID;
    private int currentNewID = 0;

    public AnimationCurve exponentialCurve;
    public AnimationCurve exponentialDecayCurve;
    
    [Header("Gravity & Jumps")]
    public bool airControl = true;              // Whether you can steer while jumping
    public float jumpHeight = 1;
    public float airborneJumpHeight = 1;
    [HideInInspector]
    public bool Ai_jumpIsQueued = false;

    [Tooltip("successiveJumpHeightReduction")]
    [Range(0f, 1f)]
    public float successiveJumpHeightReduction; // reduces the height of each successive airborne jump
    public int maxJumps = 1;                    // "maxJumps - 1" tells us how many jumps can we perform (including a jump from the ground)
    public int jumpIndex = 0;                   // tells us which jump we are at (0 represent our first jump);
    public int airJumpsPerformed = 0;           // tells us how many air jumps we have performed while airborne

    public float gravity = 2f;                  // Determines the base strength of gravity
    public float gravityMultiplier = 1f;        // Used to amplify or weaken the base strength of gravity
    [Range(1f, 3f)]
    public float fallingGravityMultiplier = 1.2f; // Used to amplify gravity when falling for the sake of game feel

    [Header("Dodging")]

    public int maxDodges = 1;
    public int dodgeIndex = 0;

    public bool dodging = false;
    [HideInInspector] Vector2 dodgeDirection = Vector2.zero;
    [HideInInspector] public bool dodgeCarryingMomentum = false;
    [Range(0f, 1f)]
    public float dodgeTime;

    [Range(0f, 1f)]
    public float neutralDodgeTime;
    [Range(0f, 15f)]
    public float dodgeDistance;
    public AnimationCurve dodgeCurve;
    public AnimationCurve dodgeMomentumFallofCurve;

    [HideInInspector] public float dodgeTimestamp = 0f;
    public bool gravityEnabled = true;
    [HideInInspector] public Vector2 dodgeVelocity = Vector2.zero;

    [HideInInspector] public float t_dodgeCurveTimestamp;
    [HideInInspector] public float dodgeMomentumFallofCurveTimestamp;
    public Vector2 dodgeBoxSize = Vector2.zero;
    [HideInInspector] public Vector2 dodgeBoxPosition = Vector2.zero;
    public Vector2 dodgeBoxOffset = Vector2.zero;

    bool dodgeBoxClipped = false;

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
    public bool willIgnoreSteepSlope;
    public float ceilingRadius = .2f;           // Radius of the overlap circle to determine if the player can stand up
   
    public float ceilingTimeAmount = 0.1f;
    public bool ceilingTimerActive = false;
    Vector2 ceilingCheckBoxSize = Vector2.zero;
    Vector2 ceilingCheckBoxPosition = Vector2.zero;
    float ceilingTimestamp = 0f;

    bool ceilingCheckBoxClipped = false;

    Vector2 groundCheckBoxSize = Vector2.zero;
    Vector2 groundCheckBoxPosition = Vector2.zero;

    RaycastHit2D[] groundHits = new RaycastHit2D[3]; // can recieve up to 3 hits
    RaycastHit2D groundHit;

    public Collider2D myCollider;
    // Quick fix
    public CircleCollider2D circleCollider2D;

    public UnityEvent OnLandEvent;              // Event called when landed
    bool snapping = true;                       // When snapping the player towards ground

    // Other variables (misc and private)
    [HideInInspector] public Rigidbody2D rb;
   // private Vector2 velocity = Vector2.zero;
    private Vector2 targetVelocity = Vector2.zero;

    [HideInInspector] public float curVerticalVelocity = 0f;

    // t_ variables are for timing purposes (and are hidden)

    // Variable stores an instance of the InputMaster, which holds all of our input actions for input processing
    InputMaster input;

    [Header("Crown")]
    public GameObject crownObject;
    public Crown crownScript;
    public bool currentlyPossessed = false;
    public static List<BaseCharacterController> baseCharacterControllers; // list containing all base character controllers in scene !!!!!
    //[Min(1f)]
    //public float crownAffinityScalar = 1f;

    [Header("Player Input Handler (For changing possession)")]
    public PlayerInputHandler playerInputHandler;
    public Vector2 movementDirection = Vector2.zero;

    [Header("Animation - Override")]
    public Animator anim;
    public bool overrideJumpAnim = false;
    public bool hitStopActive = false;

    [Header("Diegetic Ui")]
    public GameObject basicOutline;
    public GameObject shadow;
    public GameObject crown;

    [Header("Health Handling")]
    public HealthHandler healthHandler;

    [Header("Damage Handling")]
    public int[] damageTiers = { 7, 10, 19, 35 };

    public float[] hitStopTiers = { 0.1f, 0.2f, 0.3f, 0.5f };

    public bool canRecvieceKnockback = true;
    public bool showHitboxes = false;

    public float[] knockbackDistanceTiers = { 1, 2, 4, 5 };
    public float[] knockbackDurationTiers = { 0.1f, 0.2f, 0.3f, 0.5f };
    public float knockbackAirborneDistanceMultiplier = 1.2f;

    public List<HitboxHandler> hitboxes;
    public List<HurtboxHandler> hurtboxes;

    public float damageMultiplier = 1;
    //public int damagePointer = 0;

    [Header("Impulse Handling")]
    public ImpulseHandler impulseHandler;


    [Header("Ai Movement")]
    [HideInInspector]
    public Vector2 Ai_movementDirection = Vector2.zero;
    public bool Ai_holdDownKey = false;
    public bool Ai_holdSpaceKey = false;

    // come on, use the struct for background velocity

    void Awake()
    {
        input = new InputMaster();
        
        // input.Player.Movement.performed += ctx => Debug.Log(ctx.ReadValueAsObject());  THIS IS A LAMBDA FUNCTION

        rb = GetComponent<Rigidbody2D>();
        //myCollider = GetComponent<Collider2D>();
        //circleCollider2D = GetComponent<CircleCollider2D>();
    }

    private void Start()
    {
        // StartCoroutine(SnapToGround());
        groundCheckBoxSize = new Vector3(myCollider.bounds.size.x - 0.05f, 0.55f);
        ceilingCheckBoxSize = new Vector3(myCollider.bounds.size.x - 0.05f, 0.55f);
        jumpIndex = 0;

        RunAtStart();
    }

    private void OnEnable()
    {
        if (baseCharacterControllers == null) baseCharacterControllers = new List<BaseCharacterController>();

        baseCharacterControllers.Add(this);
        foreach(BaseCharacterController baseCharacterController in baseCharacterControllers)
        {
            if (baseCharacterController == this) continue;
            // fix this later, but it works, just a little clunky
            Physics2D.IgnoreCollision(baseCharacterController.myCollider, myCollider);
            Physics2D.IgnoreCollision(baseCharacterController.circleCollider2D, circleCollider2D);
            Physics2D.IgnoreCollision(baseCharacterController.circleCollider2D, myCollider);
            Physics2D.IgnoreCollision(baseCharacterController.myCollider, circleCollider2D);
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
            dodgeBoxPosition = new Vector2(transform.position.x + dodgeBoxOffset.x, transform.position.y + dodgeBoxOffset.y);
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

        // dodge
        if (dodgeBoxClipped) Gizmos.color = Color.red;
        else Gizmos.color = Color.green;
        Gizmos.DrawWireCube(dodgeBoxPosition, dodgeBoxSize);

        Gizmos.color = Color.yellow;
    }

    void Update()
    {
        if (!hitStopActive)
        {
            // Handling timers and shiz
            if (Time.time > dodgeTimestamp && dodging)
            {
                dodgeMomentumFallofCurveTimestamp = Time.time + dodgeMomentumFallofCurve.keys[dodgeMomentumFallofCurve.length - 1].time;
                dodgeCarryingMomentum = true;
                dodging = false;
                gravityEnabled = true;
                // Do some end dodging shiz
            }

            if (holdMovementAiOverride && Time.time > t_holdMovementAiTimeStamp)
            {
                holdMovementAiOverride = false;
            } else if(holdMovementAiOverride) { SetVelocity(Vector2.zero); }

            if (Time.time > dodgeMomentumFallofCurveTimestamp && dodgeCarryingMomentum && isGrounded)
            {
                dodgeCarryingMomentum = false;
            }

            if (Time.time > ceilingTimestamp && ceilingTimerActive)
            {
                ceilingTimerActive = false;
                curVerticalVelocity = -1f;
            }

            ceilingCheckBoxPosition = ceilingCheck.position + Vector3.up * ceilingCheckBoxSize.y / 2;
            groundCheckBoxPosition = groundCheck.position + Vector3.up * groundCheckBoxSize.y / 2;
            if (facingRight)
                dodgeBoxPosition = new Vector2(transform.position.x + dodgeBoxOffset.x, transform.position.y + dodgeBoxOffset.y);
            else
                dodgeBoxPosition = new Vector2(transform.position.x - dodgeBoxOffset.x, transform.position.y + dodgeBoxOffset.y);

            RunAtUpdate();
        }
    }

    // FixedUpdate is called every 'x' seconds
    void FixedUpdate()
    {

        if (!hitStopActive)
        {
            HandleCollisionsAndSnapping();
            HandleMovementRevamped();
            if (Ai_jumpIsQueued && isGrounded)
            {
                Ai_jumpIsQueued = false;
                PerformJumpAi();
            }
        }

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
            Vector2 directionTowardsClosestPoint;
            if (previousClosestPoint.x <= groundCheckBoxPosition.x + groundCheckBoxSize.x / 2 && previousClosestPoint.x >= groundCheckBoxPosition.x - groundCheckBoxSize.x / 2 &&
                previousClosestPoint.y <= groundCheckBoxPosition.y)
            {
                //Debug.Log("Closest Point");
                directionTowardsClosestPoint = previousClosestPoint - new Vector2(transform.position.x, transform.position.y);
                groundHit = Physics2D.Raycast(transform.position, directionTowardsClosestPoint.normalized, directionTowardsClosestPoint.magnitude + 0.5f, whatIsGround);
            }
            else if (previousHit.point.y <= groundCheckBoxPosition.y) // else use groundHit (which may not be the closest point to the players feet)
            {
                //Debug.LogError("Using previous hit");
                groundHit = previousHit;
            }
            else // contact point is not desirable
            {
                groundContactFound = false;
                //Debug.LogWarning("oh no"); // 
            }

            // if we found a desirable ground contact point...
            Debug.DrawRay(groundHit.point, groundHit.normal, Color.magenta);

            // If ground has been detected
            if (groundContactFound && curVerticalVelocity <= 0f && groundHit == true)
            {
                float incidentAngle = Vector2.Angle(Vector2.up, groundHit.normal);
                isGrounded = (85f <= incidentAngle && incidentAngle <= 95f) ? false : true;
                isFalling = false;
                isOnWalkableSlope = (incidentAngle < maxSlopeAngle) ? true : false;
                isOnSteepSlope = (incidentAngle < maxSteepSlopeAngle && maxSlopeAngle < incidentAngle) ? true : false;
                isSliding = (maxSteepSlopeAngle > incidentAngle || 85f <= incidentAngle && incidentAngle <= 95f) ? false : true;
                // reset our jumps
                jumpIndex = 0;
                airJumpsPerformed = 0;
                dodgeIndex = 0;

                if ((!dodging || dodging && dodgeVelocity.y < 0f) && isGrounded && (isOnWalkableSlope || isOnSteepSlope))
                {

                    bool willSnap = true;
                    // apply snapping when
                    if(isOnSteepSlope)
                    {
                        Vector2 directionOfRaycast = (facingRight) ? Vector2.left : Vector2.right;
                        RaycastHit2D hit = Physics2D.Raycast(groundCheckBoxPosition, directionOfRaycast, 1f, whatIsGround);

                        // The reasoning for such a long if statement is the encompass the requirements needed for a player controlling a possessed character
                        // and the requirements for an Ai controlling an unpossessed character
                        if((playerInputHandler.groundMovementDirection.x != 0f 
                            && Vector2.Dot(groundHit.normal.normalized, playerInputHandler.groundMovementDirection.normalized) > 0f
                            && hit.normal != groundHit.normal && currentlyPossessed) || (currentlyPossessed && Ai_movementDirection.x != 0f && 
                            Vector2.Dot(groundHit.normal.normalized, Ai_movementDirection.normalized) > 0f && 
                            hit.normal != groundHit.normal)) // Ai compliant
                        {
                            Debug.Log("TRIGGERED");
                            willIgnoreSteepSlope = true;
                            willSnap = false;
                        } 
                        else
                        {
                            willIgnoreSteepSlope = false;
                            isOnWalkableSlope = true;
                        }
                    }
                    else
                    {
                        willIgnoreSteepSlope = false;
                    }

                    if (willSnap)
                    {
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
            else
            {
                isSliding = false;
            }
        } else
        {
            isSliding = false;
        }

        // ceiling checking
        if (curVerticalVelocity > 0f)
        {
            ceilingCheckBoxClipped = Physics2D.OverlapBox(ceilingCheckBoxPosition, ceilingCheckBoxSize, 0f, whatIsGround);
            if (ceilingCheckBoxClipped && !ceilingTimerActive)
            {
                ceilingTimerActive = true;
                ceilingTimestamp = Time.time + ceilingTimeAmount;
            }
            else if (!ceilingCheckBoxClipped)
            {
                ceilingTimerActive = false;
                ceilingCheckBoxClipped = false;
            }
        }
        else
        {
            ceilingTimerActive = false;
            ceilingCheckBoxClipped = false;
        }

        // dodge
        dodgeBoxClipped = Physics2D.OverlapBox(dodgeBoxPosition, dodgeBoxSize, 0f, whatIsGround);
    }


    bool slowing = false; // rename this variable
    public void HandleMovementRevamped()
    {
        Vector2 velocity;

        // Dodging section, revamp later by using backgroundVelocity
        if (dodging)
        {
            //Debug.Log(Vector2.Dot(playerInputHandler.RAWmovementDirection.normalized, groundHit.normal));

            velocity = dodgeVelocity * dodgeCurve.Evaluate((Time.time - t_dodgeCurveTimestamp) / dodgeTime);
            if (dodgeBoxClipped)
            {
                Vector2 direction = dodgeVelocity.normalized;
                Vector2 position = new Vector2(transform.position.x, transform.position.y);
                //Debug.DrawRay(position, direction, Color.cyan, 120f);
                //RaycastHit2D hit = Physics2D.BoxCast(position, direction, 0f, direction, 0.5f);
                RaycastHit2D hit = Physics2D.Raycast(position, direction, 0.8f, whatIsGround);
                if (hit)
                {
                    //Debug.Log("Im being called");
                    //Debug.DrawRay(hit.point, hit.normal, Color.magenta, 120f);
                    //Debug.LogWarning("called");
                    velocity = Vector2.Perpendicular(hit.normal) * -velocity;

                    if (facingRight) rb.velocity = new Vector2(velocity.x, velocity.y);
                    else rb.velocity = new Vector2(velocity.x, -velocity.y);
                    return;
                }

                // No catch?
            }

            // come back to this 
            //Debug.Log("just work");
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
                   targetVelocity * endMovementCurve.Evaluate(Time.time - t_startMovementCurveTimestamp);
            }

            if (dodgeCarryingMomentum)
            {
                if (!isGrounded)
                {
                    //dodgeMomentumFallofCurveTimestamp += Time.fixedDeltaTime;
                    dodgeMomentumFallofCurveTimestamp = Time.time + dodgeMomentumFallofCurve.keys[dodgeMomentumFallofCurve.length - 1].time;
                    velocity += calculatedDodgeSpeed * dodgeDirection * dodgeMomentumFallofCurve.Evaluate(0f);
                }
                else
                {

                    velocity += calculatedDodgeSpeed * dodgeDirection * dodgeMomentumFallofCurve.Evaluate(Time.time - dodgeMomentumFallofCurveTimestamp);
                }
            }
        }

        Vector2 backgroundVelocity = GetBackgroundVelocities();
        bool applyBackgroundVelocityPerpendicular = (Vector2.Dot(groundHit.normal, backgroundVelocity.normalized) < 0f) ? true : false;
        if (backgroundVelocitiesCurrentlyActive)
        {
            Debug.DrawRay(groundHit.point, backgroundVelocity.normalized, Color.green);
            //Debug.Log(Vector2.Dot(groundHit.normal, backgroundVelocity.normalized));
            //Debug.LogWarning(backgroundVelocity.y);
            //Debug.Break();
        }
        // Handles Gravity to calculate curVerticalVelocity
        if (!isGrounded && gravityEnabled || isSliding && gravityEnabled)
        {

            if (curVerticalVelocity < 0f && !isSliding)
            {
                // Debug.LogWarning("Gravity Activated AMP");
                // applies fallingGravityMultiplier if we are falling
                if ((playerInputHandler.aerialMovementDirection.y < 0f && currentlyPossessed) || (!currentlyPossessed && 
                    Ai_holdDownKey)) // Ai compliant
                {
                    // get rid of magic number
                    curVerticalVelocity -= gravity * gravityMultiplier * fallingGravityMultiplier * 2f * Time.deltaTime;
                }
                else
                {
                    curVerticalVelocity -= gravity * gravityMultiplier * fallingGravityMultiplier * Time.deltaTime;
                }

                // curVerticalVelocity += backgroundVelocity.y;
                isFalling = true;
            }
            else
            {
                // Debug.LogWarning("Gravity Activated");
                // else apply typical gravity
                if ((playerInputHandler.spaceKeyHeld && !isSliding && currentlyPossessed) || (!currentlyPossessed && Ai_holdSpaceKey && !isSliding)) // Ai compliant
                {
                    // get rid of magic number
                    curVerticalVelocity -= gravity * gravityMultiplier * 0.8f * Time.deltaTime;
                }
                else if (
                    (Vector2.Dot(playerInputHandler.groundMovementDirection, groundHit.normal) < 0f && isSliding && currentlyPossessed) || ( !currentlyPossessed &&
                    Vector2.Dot(Ai_movementDirection, groundHit.normal)< 0f ) && isSliding) // Ai compliant
                {
                    // get rid of magic number
                    curVerticalVelocity -= gravity * gravityMultiplier * 0.3f * Time.deltaTime;
                }
                else
                {
                    curVerticalVelocity -= gravity * gravityMultiplier * Time.deltaTime;
                }

                // curVerticalVelocity += backgroundVelocity.y;
                //curVerticalVelocity -= gravity * gravityMultiplier * Time.deltaTime;
            }

            //curVerticalVelocity += backgroundVelocity.y;
        }
        else if (gravityEnabled && isGrounded && curVerticalVelocity < 0f) // && we are grounded && we are not sliding // This is causing the jitter
        {
            //if (backgroundVelocitiesCurrentlyActive) Debug.LogError("my worst nightmare");
            curVerticalVelocity = 0f;
        }


        // Handling sliding
        if (isSliding && isGrounded)
        {
            // Depending on the current user input direction and if the player is trying to move away from the slide
            if ((Vector2.Dot(groundHit.normal.normalized, playerInputHandler.groundMovementDirection) > 0f && currentlyPossessed) ||
                (!currentlyPossessed && Vector2.Dot(groundHit.normal.normalized, Ai_movementDirection) > 0f)) // Ai compliant
            {
                curVerticalVelocity = 0f;
                rb.velocity = new Vector2(velocity.x + backgroundVelocity.x, 0f);

            }
            else
            {
                // else just apply gravity perpendicularly to the slide
                rb.velocity = Vector2.Perpendicular(groundHit.normal) * -curVerticalVelocity * Vector2.Dot(Vector2.up, -Vector2.Perpendicular(groundHit.normal));
            }

            //Debug.Log("Sliding : " + Time.frameCount);
            //ApplyBackgroundVelocity(backgroundVelocity, applyBackgroundVelocityPerpendicular);
            return;
        }

        // Handles ground movement when not sliding & grounded
        if ((playerInputHandler.groundMovementDirection.x != 0f && isGrounded && !willIgnoreSteepSlope && currentlyPossessed) || (!currentlyPossessed 
            && isGrounded && !willIgnoreSteepSlope)) // Ai compliant
        {
            rb.velocity = -Vector2.Perpendicular(groundHit.normal) * (velocity.x);
            rb.velocity = new Vector2(rb.velocity.x, curVerticalVelocity);

            //ApplyBackgroundVelocity(backgroundVelocity, applyBackgroundVelocityPerpendicular);
            return;
        }

        // Handles airborne movement
        if (gravityEnabled)
        {
            //Debug.LogWarning("Im being called & " + backgroundVelocity.y);
            if(backgroundVelocitiesCurrentlyActive)
                rb.velocity = new Vector2(velocity.x + backgroundVelocity.x, curVerticalVelocity);
            else rb.velocity = new Vector2(velocity.x, curVerticalVelocity);
        }
        else
            rb.velocity = velocity;
    }

    public void ApplyBackgroundVelocity(Vector2 backgroundVelocity, bool applyPerpendicularlyToGround)
    {
        if (applyPerpendicularlyToGround)
        {
            rb.velocity += Vector2.Perpendicular(groundHit.normal) * ((backgroundVelocity.x > 0) ? backgroundVelocity.magnitude : -backgroundVelocity.magnitude)
                * Vector2.Dot(Vector2.up, -Vector2.Perpendicular(groundHit.normal));
        }
        else
        {
            rb.velocity += backgroundVelocity;
        }
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

    public Vector2 GetBackgroundVelocities()
    {
        UpdateBackgroundVelocities();

        int index = 0;
        Vector2 temp = Vector2.zero;

        backgroundVelocitiesCurrentlyActive = false;
        foreach (Vector2 velocity in backgroundVelocities)
        {
            backgroundVelocitiesCurrentlyActive = true;

            if (backgroundVelocityTypes[index] == backgroundVelocityType.GRAVITY_INCORPORATED_WITH_FRICTION)
            {
                // in this type of backgroundVelocity, messing with the vertical component is not neccessary

                if (isGrounded)
                {
                    temp.x += velocity.x * backgroundVelocityCurves[index].Evaluate(
                        (Time.time + backgroundVelocityDurations[index]) - backgroundVelocityTimestamps[index] / backgroundVelocityDurations[index]);
                }
                else temp.x += velocity.x;
            }

            index++;
        }


        return temp;

    }

    List<int> indexesMarked = new List<int>(0);
    public void UpdateBackgroundVelocities()
    {
        int numOfItemsRemoved = 0;
        int index = 0;

        for (; index < backgroundVelocityTimestamps.Count; index++)
        {
            if (backgroundVelocityTypes[index] == backgroundVelocityType.GRAVITY_INCORPORATED_WITH_FRICTION)
            {
                if (!isGrounded)
                {
                    backgroundVelocityTimestamps[index] += Time.fixedDeltaTime;
                }
                else
                {

                    if (backgroundVelocityTimestamps[index] < Time.time)
                        indexesMarked.Add(index);
                }
            }

            index++;
        }

        foreach (int i in indexesMarked)
        {
            RemoveBackgroundVelocityAt(i - numOfItemsRemoved);

            numOfItemsRemoved++;
        }

        if (indexesMarked.Count > 0)
            indexesMarked.Clear();
    }
    
    // the reason these return hashes is so we can find the ID's of a background velocity
    // this allows us the ability to prevent stacking of background velocities

    public int SetNewBackgroundVelocityGravityIncorporated(Vector2 distance, float horizontalFrictionDuration, animationCurves animationCurveType = animationCurves.EXPONENTIAL_DECAY)
    {
        Vector2 newBackgroundVelocity = new Vector2((distance.x * 2f) / horizontalFrictionDuration, Mathf.Sqrt(2 * gravity * gravityMultiplier * distance.y));
        backgroundVelocities.Add(newBackgroundVelocity);
        backgroundVelocityTimestamps.Add(Time.time + horizontalFrictionDuration + Time.deltaTime);
        backgroundVelocityDurations.Add(horizontalFrictionDuration);
        backgroundVelocityTypes.Add(backgroundVelocityType.GRAVITY_INCORPORATED_WITH_FRICTION);
        backgroundVelocityID.Add(currentNewID);

        curVerticalVelocity += newBackgroundVelocity.y;
        if (curVerticalVelocity > 0f && isGrounded) isGrounded = false;
        switch (animationCurveType)
        {
            case animationCurves.CONSTANT:
                backgroundVelocityCurves.Add(AnimationCurve.Constant(Time.time + Time.deltaTime, Time.time + Time.deltaTime + horizontalFrictionDuration, 1f));
                break;

            case animationCurves.LINEAR:
                backgroundVelocityCurves.Add(AnimationCurve.Linear(Time.time + Time.deltaTime, 1f, Time.time + Time.deltaTime + horizontalFrictionDuration, 0f));
                break;

            case animationCurves.EXPONENTIAL:
                backgroundVelocityCurves.Add(exponentialCurve);
                break;

            case animationCurves.EXPONENTIAL_DECAY:
                backgroundVelocityCurves.Add(exponentialDecayCurve);
                break;
        }

        return currentNewID++;
    }

    public int SetNewBackgroundVelocityGravityIncorporated_Velcoity(Vector2 velocity, float horizontalFrictionDuration, animationCurves animationCurveType = animationCurves.EXPONENTIAL_DECAY)
    {
        //Debug.Break();

        Vector2 newBackgroundVelocity = velocity;
        backgroundVelocities.Add(newBackgroundVelocity);
        backgroundVelocityTimestamps.Add(Time.time + horizontalFrictionDuration + Time.deltaTime);
        backgroundVelocityDurations.Add(horizontalFrictionDuration);
        backgroundVelocityTypes.Add(backgroundVelocityType.GRAVITY_INCORPORATED_WITH_FRICTION);
        backgroundVelocityID.Add(currentNewID);

        curVerticalVelocity += newBackgroundVelocity.y;
        if (curVerticalVelocity > 0f && isGrounded) isGrounded = false;
        switch (animationCurveType)
        {
            case animationCurves.CONSTANT:
                backgroundVelocityCurves.Add(AnimationCurve.Constant(Time.time + Time.deltaTime, Time.time + Time.deltaTime + horizontalFrictionDuration, 1f));
                break;

            case animationCurves.LINEAR:
                backgroundVelocityCurves.Add(AnimationCurve.Linear(Time.time + Time.deltaTime, 1f, Time.time + Time.deltaTime + horizontalFrictionDuration, 0f));
                break;

            case animationCurves.EXPONENTIAL:
                backgroundVelocityCurves.Add(exponentialCurve);
                break;

            case animationCurves.EXPONENTIAL_DECAY:
                backgroundVelocityCurves.Add(exponentialDecayCurve);
                break;
        }

        return currentNewID++;
    }

    public int SetNewBackgroundVelocityLerpingEffect(Vector2 distance)
    {
        return currentNewID++;
    }

    public bool RemoveBackgroundVelocityAt(int index)
    {
        if (backgroundVelocities.Count >= (index) && backgroundVelocityCurves.Count >= (index)
            && backgroundVelocityDurations.Count >= (index) && backgroundVelocityTimestamps.Count >= (index) && backgroundVelocityTypes.Count >= (index)
            && backgroundVelocityID.Count >= (index))
        {
            backgroundVelocities.RemoveAt(index);
            backgroundVelocityCurves.RemoveAt(index);
            backgroundVelocityDurations.RemoveAt(index);
            backgroundVelocityTimestamps.RemoveAt(index);
            backgroundVelocityTypes.RemoveAt(index);
            backgroundVelocityID.RemoveAt(index);
            return true;
        }
        else
            return false;
        // ... 
    }

    public void ClearAllBackgroundVelocities()
    {
        backgroundVelocities.Clear();
        backgroundVelocityTimestamps.Clear();
        backgroundVelocityDurations.Clear();
        backgroundVelocityCurves.Clear();
        backgroundVelocityTypes.Clear();
        backgroundVelocityID.Clear();
    }


    [HideInInspector]
    public Vector2 previousRbVelocity;

    bool previousDodgeMomentumState;
    bool previousDodgeState;
    public void HitStop(float duration, float force)
    {
        previousDodgeState = dodging;
        previousDodgeMomentumState = dodgeCarryingMomentum;

        previousRbVelocity = rb.velocity;
        //previousCurVerticalVelocity = curVerticalVelocity;

        int index = 0;
        for(;index<backgroundVelocityTimestamps.Count;index++)
        {
            backgroundVelocityTimestamps[index] += duration + Time.deltaTime;
            index++;
        }

        dodging = false;
        dodgeCarryingMomentum = false;
        hitStopActive = true;
        anim.speed = 0f;
        rb.velocity = Vector2.zero;

        if (currentlyPossessed)
        {
            impulseHandler.Shake(force);
        }
        StartCoroutine(HitStopEnumerator(duration)); 
    }

    IEnumerator HitStopEnumerator(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (previousDodgeState)
        {
            dodgeTimestamp += duration + Time.deltaTime;
            dodging = true;
        } else if (previousDodgeMomentumState)
        {
            dodgeMomentumFallofCurveTimestamp += duration + Time.deltaTime;
            dodgeCarryingMomentum = true;
        }

        rb.velocity = previousRbVelocity;
        //curVerticalVelocity = previousCurVerticalVelocity;
        hitStopActive = false;
        anim.speed = 1f;
    }

    public void ApplyKnockback(knockbackDirection direction, bool attackerFacingRight, float distance, float horizontalFrictionDuration,
        animationCurves animationCurveType = animationCurves.EXPONENTIAL_DECAY)
    {
        SetNewBackgroundVelocityGravityIncorporated(knockbackDirectionClass.calculateKnockbackDirection(direction, attackerFacingRight) * distance,
            horizontalFrictionDuration, animationCurveType);
    }
    // functions that can be overwritten depending on each characters needs

    public void HITBOXES_ForceClearAll()
    {
        foreach (HitboxHandler hitbox in hitboxes)
        {
            hitbox.ClearIDs(clearDegree.CLEAR_ALL);
        }
    }

    public void HITBOXES_UpdateClearIDs()
    {
        foreach(HitboxHandler hitbox in hitboxes)
        {
            hitbox.ClearIDs(hitbox.clearAmount);
        }
    }

    public void HITBOXES_ClearOnetimeIDs()
    {
        foreach(HitboxHandler hitbox in hitboxes)
        {
            hitbox.ClearOnetimeIDs();
        }
    }

    public virtual void PerformMovement(InputAction.CallbackContext context)
    {
        // meant to be overwritten
    }

    public virtual void PerformMovementAi(Vector2 direction)
    {
        // meant to be overwritten
    }

    public virtual void PerformJump(InputAction.CallbackContext context)
    {
        // meant to be overwritten
    }

    public virtual void PerformJumpAi(bool holdSpaceKey = false, bool mustBeGrounded = false)
    {
        // meant to be overwritten
    }

    float calculatedDodgeSpeed = 0f; 
    public virtual void PerformDodge(InputAction.CallbackContext context)
    {
        // not meant to be typically overwritten
        if (dodgeIndex > maxDodges - 1) return;
        
        calculatedDodgeSpeed = (dodgeDistance / dodgeTime * (dodgeCurve.keys[2].time - dodgeCurve.keys[1].time));
        if (playerInputHandler.universalMovementDirection == Vector2.zero) // make sure to set proper deadzones!
        {
            dodgeTimestamp = Time.time + neutralDodgeTime;
            dodgeVelocity = Vector2.zero;
        }
        else
        {
            dodgeTimestamp = Time.time + dodgeTime;
            dodgeVelocity = playerInputHandler.universalMovementDirection.normalized * calculatedDodgeSpeed;
        }

        dodgeDirection = playerInputHandler.universalMovementDirection.normalized;
        t_dodgeCurveTimestamp = Time.time;
        gravityEnabled = false;
        dodging = true;
        curVerticalVelocity = 0f;

        // we reset our jumps, however, we remove our ground jump by setting jumpIndex to 1 (which represents our air jump), instead of 0 (which represents our ground jump)
        if (jumpIndex == maxJumps) jumpIndex -= 1;
        else jumpIndex -= (maxJumps-jumpIndex);
        if(!isGrounded)dodgeIndex++;
    }

    public virtual void PerformDodgeAi(Vector2 direction)
    {
        if (holdMovementAiOverride) return;

        // not meant to be typically overwritten
        if (dodgeIndex > maxDodges - 1) return;

        calculatedDodgeSpeed = (dodgeDistance / dodgeTime * (dodgeCurve.keys[2].time - dodgeCurve.keys[1].time));
        if (direction == Vector2.zero) // make sure to set proper deadzones!
        {
            dodgeTimestamp = Time.time + neutralDodgeTime;
            dodgeVelocity = Vector2.zero;
        }
        else
        {
            dodgeTimestamp = Time.time + dodgeTime;
            dodgeVelocity = direction * calculatedDodgeSpeed;
        }

        dodgeDirection = direction;
        t_dodgeCurveTimestamp = Time.time;
        gravityEnabled = false;
        dodging = true;
        curVerticalVelocity = 0f;

        // we reset our jumps, however, we remove our ground jump by setting jumpIndex to 1 (which represents our air jump), instead of 0 (which represents our ground jump)
        if (jumpIndex == maxJumps) jumpIndex -= 1;
        else jumpIndex -= (maxJumps - jumpIndex);
        if (!isGrounded) dodgeIndex++;
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
        crownScript.ThrowMe(playerInputHandler.universalMovementDirection, myCollider);
    }

    public virtual void PossessMe()
    {
        // not typically meant to be overwritten
        Vector2 targetVelocity = new Vector2(movementSpeed * playerInputHandler.groundMovementDirection.x, curVerticalVelocity);
        SetVelocity(targetVelocity);
        currentlyPossessed = true;

        // hacked together
        if (playerInputHandler.groundMovementDirection.x < -0.2 && facingRight == true)
        {
            dodgeCarryingMomentum = false;
            facingRight = false;

            transform.localScale = new Vector3(-1, 1, 1);
        }

        else if (playerInputHandler.groundMovementDirection.x > 0.2 && facingRight == false)
        {
            dodgeCarryingMomentum = false;
            facingRight = true;

            transform.localScale = new Vector3(1, 1, 1);
        }

        playerInputHandler.possessedCharacter.OnPossessionLeave(); // calls the 'leave' function on previous possession
        playerInputHandler.possessedCharacter = this;
        playerInputHandler.possessedCharacter.shadow.SetActive(true);
        playerInputHandler.possessedCharacter.crown.SetActive(true);
        playerInputHandler.possessedCharacter.shadow.GetComponent<SpriteRenderer>().color = playerInputHandler.playerColor;
    }

    public virtual void OnPossessionLeave()
    {
        // not typically meant to be overwritten
        movementDirection = Vector2.zero;
        targetVelocity = Vector2.zero;
        shadow.SetActive(false);
        crown.SetActive(false);
        currentlyPossessed = false;
    }

    // Ai Virtual

    public virtual void JumpWaypointAI(bool holdSpaceKey=false, bool mustBeGrounded=false)
    {
        PerformJumpAi(holdSpaceKey, mustBeGrounded);
    }

    private float t_holdMovementAiTimeStamp = 0f;
    public bool holdMovementAiOverride = false;
    public virtual void HoldMovementAi(Vector2 direction, float time)
    {
        holdMovementAiOverride = true;
        t_holdMovementAiTimeStamp = Time.time + time;
        PerformMovementAi(direction);
    }

    public virtual void RunWaypointAI(Vector2 direction)
    {
        PerformMovementAi(direction);
    }

    public virtual void DodgeWaypointAI(Vector2 direction)
    {
        PerformDodgeAi(direction);
    }

    public virtual void RunAtFixedUpdate()
    {
        // meant to be overwritten
    }

    public virtual void RunAtStart()
    {
        // meant to be overwritten
    }

    public virtual void RunAtUpdate()
    {
        // meant to be overwritten
    }
}
