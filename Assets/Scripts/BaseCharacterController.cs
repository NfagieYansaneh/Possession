using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using System;

/* Purpose of BaseCharacterController is to be the base character controller script that all characters inherit and are
 * capable of overridding to fit their specific needs
 */

public enum animationCurves { LINEAR, CONSTANT, EXPONENTIAL, EXPONENTIAL_DECAY }; // for defining how a background velocity should decay overtime
public enum backgroundVelocityType { GRAVITY_INCORPORATED_WITH_FRICTION, LERPING_EFFECT }; // defining key characteristic of background velocity
// either that we have friction and gravity incorportated, or we just lerp towards it in the instance were we have gravity disabled and we are essentially just floating

public class BaseCharacterController : MonoBehaviour
{
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
    [HideInInspector] public float t_startMovementCurveTimestamp; // timestamp to figure out when we started moving when we pressed a movement key

    // background velocities are essentially just a way for the BaseCharacterController to handle momentum
    [Header("Background Velocities")]
    private bool backgroundVelocitiesCurrentlyActive = false;

    // may change backgroundVelocity into a struct, but there is no demand as of now...

    public List<Vector2> backgroundVelocities;
    public List<float> backgroundVelocityTimestamps; // timestamp of when a backgroundVelocity will end
    public List<float> backgroundVelocityDurations; // how long a background velocity will last
    public List<AnimationCurve> backgroundVelocityCurves; // time of curve the background velocity represents in order to determine how it will decay over time
    public List<backgroundVelocityType> backgroundVelocityTypes; // key characteristic of background velocty, backgroundVelocityType defined above in its enum
    public List<int> backgroundVelocityID;
    private int currentNewID = 0;

    public AnimationCurve exponentialCurve; // reference as to what an exponential animation curve is
    public AnimationCurve exponentialDecayCurve; // reference as to what an exponentially decaying animation curve is
    
    [Header("Gravity & Jumps")]
    public bool airControl = true;              // Whether you can steer while jumping
    public float jumpHeight = 1;
    public float airborneJumpHeight = 1;
    [HideInInspector]
    public bool Ai_jumpIsQueued = false;        // if the Ai has a jump that is queued and will be issued as soon as it lands

    [Tooltip("successiveJumpHeightReduction")]
    [Range(0f, 1f)]
    public float successiveJumpHeightReduction; // reduces the height of each successive airborne jump
    public int maxJumps = 1;                    // maxJumps tells us how many jumps can we perform (including a jump from the ground)
    public int jumpIndex = 0;                   // tells us which jump we are at (0 represent our first jump);
    public int airJumpsPerformed = 0;           // tells us how many consequctive air jumps we have performed while airborne

    public float gravity = 2f;                  // Determines the base strength of gravity
    public float gravityMultiplier = 1f;        // Used to amplify or weaken the base strength of gravity
    [Range(1f, 3f)]
    public float fallingGravityMultiplier = 1.2f; // Used to amplify gravity when falling for the sake of game feel

    [Header("Dodging")]

    public int maxDodges = 1;
    public int dodgeIndex = 0;                  // current dodge index, dodge index of '0' states that we are at our first dodge

    public bool dodging = false;                // states if we are currently dodging
    [HideInInspector] Vector2 dodgeDirection = Vector2.zero;
    [HideInInspector] public bool dodgeCarryingMomentum = false;
    [Range(0f, 1f)]
    public float dodgeTime;                     // how long a dodge should last

    [Range(0f, 1f)]
    public float neutralDodgeTime;              // how long a neutral dodge should last
    [Range(0f, 15f)]
    public float dodgeDistance;                 // dodge distance
    public AnimationCurve dodgeCurve;           // animation curve that establishes how a dodge velocity will change over the duration of the dodge
    public AnimationCurve dodgeMomentumFallofCurve; // animation curve that establishes how a dodge velocity will fall off after the dodge is done

    [HideInInspector] public float dodgeTimestamp = 0f; // timestamp stores the time in which dodge will expire
    [HideInInspector] public Vector2 dodgeVelocity = Vector2.zero;

    [HideInInspector] public float t_dodgeCurveTimestamp; // timestamp here is essentially the same as dodgeTimestamp, but I use this excusively to determine 
    // how my dodgeVelocity will change overtime
    [HideInInspector] public float dodgeMomentumFallofCurveTimestamp; // timestamp in which dodge

    // dodgeBox is used to assess the area just in front of the character in order to assess how a dodge can be performed...
    public Vector2 dodgeBoxSize = Vector2.zero;
    [HideInInspector] public Vector2 dodgeBoxPosition = Vector2.zero;
    public Vector2 dodgeBoxOffset = Vector2.zero;

    bool dodgeBoxClipped = false; // states whether the dodgeBox has clipped with the ground or an obstacle

    [Header("Ground Checking")]
    public LayerMask whatIsGround;              // A mask determining what is ground to the character
    public Transform groundCheck;               // A position marking where to check if the player is grounded
    public Transform ceilingCheck;              // A position marking where to check for ceilings
    public bool isGrounded;                     // Whether or not the player is grounded.
    public bool gravityEnabled = true;

    [Tooltip("Max slope angle will not go up to 85f because that is a situation that will never occurr")]
    [Range(0f, 85f)]
    public float maxSlopeAngle = 45f; // max slope angle represents the maximum angle in which we will always be snapping towards the ground
    [Range(0f, 85f)]
    public float maxSteepSlopeAngle = 55f; // max steep slope angle represents the maximum angle we will not be always snapping towards the ground, but also represents the angle before we start sliding on slopes
    // steeper than the maxSteepSlopeAngle

    public bool isSliding;                     // Whether or not the player is sliding.
    public bool isFalling;                      // Whether or not the player is falling
    public bool isOnWalkableSlope;
    public bool isOnSteepSlope;
    public bool willIgnoreSteepSlope;
   
    public float ceilingTimeAmount = 0.1f;      // how long we have to collide with ceiling before we count it as the player colliding with the ceiling
    public bool ceilingTimerActive = false;

    // ceilingCheckBox is just a box to represent our ceiling detection hitbox
    Vector2 ceilingCheckBoxSize = Vector2.zero;
    Vector2 ceilingCheckBoxPosition = Vector2.zero;
    float ceilingTimestamp = 0f; // ceilingTimestamp is defined at the instant we hit a ceiling and is formed by Time.time + a duration. If we are still hiting the ceiling after
    // ceilingTimestamp, that we state that we have hit a ceiling and that we should execute the appropriate code when hitting a ceiling.

    bool ceilingCheckBoxClipped = false; // states whether the ceilingCheckBox has collided with an obstacle

    // groundCheckBox is just a box detect ground detection
    Vector2 groundCheckBoxSize = Vector2.zero;
    Vector2 groundCheckBoxPosition = Vector2.zero;

    RaycastHit2D[] groundHits = new RaycastHit2D[3]; // can recieve hold up to 3 ground hits that basically represent three points where we detect the ground
    RaycastHit2D groundHit; // used to select the groundHit we will be refering to aside after vetting ground hits from the groundHits array

    public Collider2D myCollider;
    public CircleCollider2D circleCollider2D;

    public UnityEvent OnLandEvent;              // Event called when landed
    bool snapping = true;                       // States if player is snapping towards ground when grounded

    [HideInInspector] public Rigidbody2D rb;
    private Vector2 targetVelocity = Vector2.zero; // velocity the player is targetting for

    [HideInInspector] public float curVerticalVelocity = 0f;

    [Header("Crown")]
    public GameObject crownObject;
    public Crown crownScript;
    public bool currentlyPossessed = false;
    public static List<BaseCharacterController> baseCharacterControllers; // list containing all base character controllers in scene !!!!!

    [Header("Player Input Handler (For changing possession)")]
    public PlayerInputHandler playerInputHandler;
    public Vector2 movementDirection = Vector2.zero;

    [Header("Animation - Override")]
    public Animator anim;
    public bool overrideJumpAnim = false;
    public bool hitStopActive = false; // hitstop is basically when our animation freezes to emphasize a character being struck

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

    public List<HitboxHandler> hitboxes; // hit boxes when we are doing attacks
    public List<HurtboxHandler> hurtboxes; // our hurt boxes

    public float damageMultiplier = 1;

    [Header("Impulse Handling")]
    public ImpulseHandler impulseHandler; // handling screen shake


    [Header("Ai Movement")]
    [HideInInspector]
    public Vector2 Ai_movementDirection = Vector2.zero; // direction the Ai is moving in
    public bool Ai_holdDownKey = false; // will the Ai being holding down key while falling to fall faster?
    public bool Ai_holdSpaceKey = false; // will the Ai being holding the jump key to perform higher jumps?
    public BaseAiController baseAiController;

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Start()
    {
        groundCheckBoxSize = new Vector3(myCollider.bounds.size.x - 0.05f, 0.55f);
        ceilingCheckBoxSize = new Vector3(myCollider.bounds.size.x - 0.05f, 0.55f);
        jumpIndex = 0;

        baseAiController.currentlyBeingPossessed = currentlyPossessed;

        RunAtStart();
    }

    private void OnEnable()
    {
        if (baseCharacterControllers == null) baseCharacterControllers = new List<BaseCharacterController>();

        baseCharacterControllers.Add(this);

        // making sure we ignore colliders of other baseCharacterControllers so we can pass through other characters
        foreach(BaseCharacterController baseCharacterController in baseCharacterControllers)
        {
            if (baseCharacterController == this) continue;
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

    // For debugging sake, we will be drawing out the CheckBoxes so we can teak them in our Editor
    private void OnDrawGizmosSelected()
    {
        if (!Application.isPlaying)
        {
            groundCheckBoxSize = new Vector3(myCollider.bounds.size.x - 0.05f, 0.55f);
            ceilingCheckBoxSize = new Vector3(myCollider.bounds.size.x - 0.05f, 0.55f);

            ceilingCheckBoxPosition = ceilingCheck.position + Vector3.up * ceilingCheckBoxSize.y / 2;
            groundCheckBoxPosition = groundCheck.position + Vector3.up * groundCheckBoxSize.y / 2;
            dodgeBoxPosition = new Vector2(transform.position.x + dodgeBoxOffset.x, transform.position.y + dodgeBoxOffset.y);
        }

        // draws groundCheckBox with different colors based on its status
        if (!groundHit || !Application.isPlaying)
        {
            Gizmos.color = Color.green;
        }
        else if (snapping)  // snapping as in trying to snap the player towards the ground while being grounded
            Gizmos.color = Color.yellow;
        else
            Gizmos.color = Color.red;

        Vector2 tempPosition = new Vector2(groundCheckBoxPosition.x, groundCheckBoxPosition.y - 0.55f / 2);
        Gizmos.DrawWireCube(tempPosition, groundCheckBoxSize);

        Gizmos.color = Color.cyan;
        Gizmos.DrawLine(transform.position, groundHit.point);

        if (!ceilingCheckBoxClipped || !Application.isPlaying)
        {
            Gizmos.color = Color.green;
        } 
        else
        {
            Gizmos.color = Color.red;
        }

        Gizmos.DrawWireCube(ceilingCheckBoxPosition, ceilingCheckBoxSize);

        // drawing dodgeBox wit different colors based on its status
        if (dodgeBoxClipped) Gizmos.color = Color.red;
        else Gizmos.color = Color.green;
        Gizmos.DrawWireCube(dodgeBoxPosition, dodgeBoxSize);
    }

    void Update()
    {
        // Every frame, we will check if we are currently in a hitstop (defined above)
        if (!hitStopActive)
        {
            // if not, we will handle our timers and update our timestamp status

            if (Time.time > dodgeTimestamp && dodging)
            {
                // a dodge a just ended as we have reached the end of its duration that was timestamped in the form of dodgeTimestamp...

                dodgeMomentumFallofCurveTimestamp = Time.time + dodgeMomentumFallofCurve.keys[dodgeMomentumFallofCurve.length - 1].time;
                dodgeCarryingMomentum = true;
                dodging = false;
                gravityEnabled = true;
            }

            if (holdMovementAiOverride && Time.time > t_holdMovementAiTimeStamp)
            {
                // the Ai's movement override command has reached the end of its duration that was timestamped in the form of t_holdMovementAiTimeStamp...

                holdMovementAiOverride = false;
            } else if(holdMovementAiOverride) { 
                // Else enforce Ai's movement override command
                SetVelocity(Vector2.zero); 
            }

            if (Time.time > dodgeMomentumFallofCurveTimestamp && dodgeCarryingMomentum && isGrounded)
            {
                // the dodge's momentum has reached the end of its duration, so we are disabling it
                dodgeCarryingMomentum = false;
            }

            if (Time.time > ceilingTimestamp && ceilingTimerActive)
            {
                // we have collided with the ceiling for a sufficient amount of time, so we will recognise this as a ceiling collision
                ceilingTimerActive = false;
                curVerticalVelocity = -1f;
            }

            // update our ceilingCheckBoxPosition and groundCheckBoxPosition in the case in which we have updated them from our editor as 
            // we try to tweak the character controller live time
            ceilingCheckBoxPosition = ceilingCheck.position + Vector3.up * ceilingCheckBoxSize.y / 2;
            groundCheckBoxPosition = groundCheck.position + Vector3.up * groundCheckBoxSize.y / 2;

            // offsets the dodgeBoxPosition approriately if we are or aren't facingRight
            if (facingRight)
                dodgeBoxPosition = new Vector2(transform.position.x + dodgeBoxOffset.x, transform.position.y + dodgeBoxOffset.y);
            else
                dodgeBoxPosition = new Vector2(transform.position.x - dodgeBoxOffset.x, transform.position.y + dodgeBoxOffset.y);

            // virtual code, that can be overrided by character controllers that inherit this BaseCharacterController.cs, to be run every update (every frame)
            RunAtUpdate();

            HandleCollisionsAndSnapping(); // Handles ground collisions and appropriate snapping towards the ground
        }
    }

    // FixedUpdate is called every 'x' seconds
    void FixedUpdate()
    {

        if (!hitStopActive)
        {
            // Handles player movement when hitstop is not active
            HandleMovementRevamped();

            // if the Ai queued a jump, then performing that jump as soon as are grounded
            if (Ai_jumpIsQueued && isGrounded)
            {
                Ai_jumpIsQueued = false;
                PerformJumpAi();
            }
        }

        // virtual code, that can be overrided by character controllers that inherit this BaseCharacterController.cs, to be run every fixed update
        RunAtFixedUpdate();
    }

    // Handles ground collisions and appropriate snapping towards the ground
    public void HandleCollisionsAndSnapping()
    {
        bool wasGrounded = isGrounded;
        isGrounded = false;

        // Checking for ground contacts
        ContactFilter2D groundContactFilter2D = new ContactFilter2D();
        groundContactFilter2D.SetLayerMask(whatIsGround);

        int numberOfContacts = Physics2D.BoxCast(groundCheckBoxPosition, groundCheckBoxSize, 0f, Vector2.down, groundContactFilter2D, groundHits, 0.55f / 2);
        groundHit = new RaycastHit2D();
        bool groundContactFound = false;

        // Filters through ground contacts to find ground contact closest to player's feet
        
        float previousDistanceY = 0f;
        Vector2 previousClosestPoint = Vector2.zero;
        RaycastHit2D previousHit = new RaycastHit2D();

        for (int index = 0; index < numberOfContacts; index++)
        {
            Vector2 position = groundCheckBoxPosition;
            position.y = groundCheckBoxPosition.y;
            Vector2 closestPoint = Physics2D.ClosestPoint(position, groundHits[index].collider);

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
            // Checks wether the closest point is within player's groundCheckBox that is meant to represent the player's feet
            Vector2 directionTowardsClosestPoint;
            if (previousClosestPoint.x <= groundCheckBoxPosition.x + groundCheckBoxSize.x / 2 && previousClosestPoint.x >= groundCheckBoxPosition.x - groundCheckBoxSize.x / 2 &&
                previousClosestPoint.y <= groundCheckBoxPosition.y)
            {
                directionTowardsClosestPoint = previousClosestPoint - new Vector2(transform.position.x, transform.position.y);
                groundHit = Physics2D.Raycast(transform.position, directionTowardsClosestPoint.normalized, directionTowardsClosestPoint.magnitude + 0.5f, whatIsGround);
            }
            else if (previousHit.point.y <= groundCheckBoxPosition.y) // else use groundHit (which may not be the closest point to the players feet)
            {
                groundHit = previousHit;
            }
            else // contact point is not desirable
            {
                groundContactFound = false;
            }

            // if we found a desirable ground contact point...
            Debug.DrawRay(groundHit.point, groundHit.normal, Color.magenta);

            // then we will assess the slope of the ground contact point and identify whether we will snap towards it or wont snap towards it.
            // Furthermore, we have to assess if we are truly grounded in the first place (maybe we are moving away from the ground dude to curVerticalVelocity being > 0f?)
            if (groundContactFound && curVerticalVelocity <= 0f && groundHit == true)
            {
                float incidentAngle = Vector2.Angle(Vector2.up, groundHit.normal);
                isGrounded = (85f <= incidentAngle && incidentAngle <= 95f) ? false : true;
                isFalling = false;
                isOnWalkableSlope = (incidentAngle < maxSlopeAngle) ? true : false;
                isOnSteepSlope = (incidentAngle < maxSteepSlopeAngle && maxSlopeAngle < incidentAngle) ? true : false;
                isSliding = (maxSteepSlopeAngle > incidentAngle || 85f <= incidentAngle && incidentAngle <= 95f) ? false : true;

                // reset our jumps and dodges
                jumpIndex = 0;
                airJumpsPerformed = 0;
                dodgeIndex = 0;

                if ((!dodging || dodging && dodgeVelocity.y < 0f) && isGrounded && (isOnWalkableSlope || isOnSteepSlope))
                {

                    // will represent when we should apply snapping...
                    bool willSnap = true;

                    if(isOnSteepSlope)
                    {
                        // if we are on a steep slope, then we need to assess how we are approaching this steep slope to determine whether we will snap towards it or not such
                        // as whether we are moving onto or away the steep slope, or if we are currently on the steep slope or maybe we just transitioned onto it.

                        Vector2 directionOfRaycast = (facingRight) ? Vector2.left : Vector2.right;
                        RaycastHit2D hit = Physics2D.Raycast(groundCheckBoxPosition, directionOfRaycast, 1f, whatIsGround);

                        // The reasoning for such a long if statement is to encompass the requirements needed for a player controlling a possessed character
                        // and the requirements for an Ai controlling an unpossessed character to make sure our BaseCharacterController is Ai compliant
                        if((playerInputHandler.groundMovementDirection.x != 0f 
                            && Vector2.Dot(groundHit.normal.normalized, playerInputHandler.groundMovementDirection.normalized) > 0f
                            && hit.normal != groundHit.normal && currentlyPossessed) || (currentlyPossessed && Ai_movementDirection.x != 0f && 
                            Vector2.Dot(groundHit.normal.normalized, Ai_movementDirection.normalized) > 0f && 
                            hit.normal != groundHit.normal))
                        {
                            willIgnoreSteepSlope = true; // meaning that we will ignore the request to snap onto this steep slope due to requirements not being met
                            willSnap = false;
                        } 
                        else
                        {
                            willIgnoreSteepSlope = false; // we will snap on the steep slope and determine it as walkable
                            isOnWalkableSlope = true;
                        }
                    }
                    else
                    {
                        willIgnoreSteepSlope = false; // we will ignore the request to snap onto this steep slope
                    }

                    if (willSnap)
                    {
                        // if we will snap, apply snapping logic...

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
                // reset isSliding status if we are no longer grounded
                isSliding = false;
            }
        } else
        {
            // reset isSliding status if we are no longer grounded
            isSliding = false;
        }

        // ceiling checking for when we are moving up towards the ceiling
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
                // resets timer if we are not hitting the ceiling anymore
                ceilingTimerActive = false;
            }
        }
        else
        {
            // resets timer and clip status if we are not hitting the ceiling anymore
            ceilingTimerActive = false;
            ceilingCheckBoxClipped = false;
        }

        // checking if dodge box is being clipped by obstacles
        dodgeBoxClipped = Physics2D.OverlapBox(dodgeBoxPosition, dodgeBoxSize, 0f, whatIsGround);
    }


    bool slowing = false; // states wheter we are slowing to a halt as a result of letting go of movement keys

    // Handles movement for this character, named Revamped after I refactorted the code here to be more efficient
    public void HandleMovementRevamped()
    {
        Vector2 velocity;

        // Dodging section, maybe reworked later with backgroundVelocity, but there is no current demand to rework it...
        if (dodging)
        {
            // in the case that we are dodging, we can just ignore the rest of this movement code and evaluate what are
            // dodging velocity should be given this period in time over the dodge's duration
            velocity = dodgeVelocity * dodgeCurve.Evaluate((Time.time - t_dodgeCurveTimestamp) / dodgeTime);

            if (dodgeBoxClipped)
            {
                // in the case that our dodgeBoxHasClipped, we will try to find a RaycastHit2D..
                Vector2 direction = dodgeVelocity.normalized;
                Vector2 position = new Vector2(transform.position.x, transform.position.y);
                RaycastHit2D hit = Physics2D.Raycast(position, direction, 0.8f, whatIsGround);

                if (hit)
                {
                    // ... and if we do, we will change our velocity direction so we can sort of work our way around this obstacle
                    velocity = Vector2.Perpendicular(hit.normal) * -velocity;

                    if (facingRight) rb.velocity = new Vector2(velocity.x, velocity.y);
                    else rb.velocity = new Vector2(velocity.x, -velocity.y);
                    return;
                }

            }

            rb.velocity = velocity;

            return;

        }
        else
        {
            // In this instance, we are not dodging, and we are just assigning our velocity normaly based the target velocity that is defined from
            // which movement keys pressed. Target velocity is essentially the velocity that the player currently wants to achieve based on player inputs.

            if (!slowing)
            {
                // if we are not slowing, then just set velocity to target velocity whilst evaluating how the velocity should change overtime when we first
                // pressed the movement key. For example, this could achieve the ability for the character to ramp up in speed
                velocity = targetVelocity * startMovementCurve.Evaluate(Time.time - t_startMovementCurveTimestamp);
            }
            else
            {
                // else, we are slowing velocity will decrease over time by following the curve defined with endMovementCurve
                velocity = (facingRight) ? targetVelocity * endMovementCurve.Evaluate(Time.time - t_startMovementCurveTimestamp) :
                   targetVelocity * endMovementCurve.Evaluate(Time.time - t_startMovementCurveTimestamp);
            }

            if (dodgeCarryingMomentum)
            {
                // if dodge is carrying momentum... 
                if (!isGrounded)
                {
                    // ... and we are not grounded, we will extend the duration that we will be carrying momentum
                    dodgeMomentumFallofCurveTimestamp = Time.time + dodgeMomentumFallofCurve.keys[dodgeMomentumFallofCurve.length - 1].time;
                    velocity += calculatedDodgeSpeed * dodgeDirection * dodgeMomentumFallofCurve.Evaluate(0f);
                }
                else
                {
                    // ... else we will not be extending the duration, and instead just add the current dodge momentum velocity onto our velocity
                    velocity += calculatedDodgeSpeed * dodgeDirection * dodgeMomentumFallofCurve.Evaluate(Time.time - dodgeMomentumFallofCurveTimestamp);
                }
            }
        }

        Vector2 backgroundVelocity = GetBackgroundVelocities();

        // commented out as this code directly below is not needed anymore...
        // bool applyBackgroundVelocityPerpendicular = (Vector2.Dot(groundHit.normal, backgroundVelocity.normalized) < 0f) ? true : false;


        // Handles Gravity to calculate curVerticalVelocity
        if (!isGrounded && gravityEnabled || isSliding && gravityEnabled)
        {

            if (curVerticalVelocity < 0f && !isSliding)
            {
                // applies fallingGravityMultiplier if we are falling
                if ((playerInputHandler.aerialMovementDirection.y < 0f && currentlyPossessed) || (!currentlyPossessed && 
                    Ai_holdDownKey)) // Ai compliant check
                {
                    // doubles gravity if we hold downwards as we fall
                    curVerticalVelocity -= gravity * gravityMultiplier * fallingGravityMultiplier * 2f * Time.deltaTime;
                }
                else
                {
                    curVerticalVelocity -= gravity * gravityMultiplier * fallingGravityMultiplier * Time.deltaTime;
                }

                isFalling = true;
            }
            else
            {
                // not falling

                if ((playerInputHandler.spaceKeyHeld && !isSliding && currentlyPossessed) || (!currentlyPossessed && Ai_holdSpaceKey && !isSliding)) // Ai compliant
                {
                    // multiplies the gravity by 0.8f if we hold the jump key while performing a jump to order to form a higher jump
                    curVerticalVelocity -= gravity * gravityMultiplier * 0.8f * Time.deltaTime;
                }
                else if (
                    (Vector2.Dot(playerInputHandler.groundMovementDirection, groundHit.normal) < 0f && isSliding && currentlyPossessed) || ( !currentlyPossessed &&
                    Vector2.Dot(Ai_movementDirection, groundHit.normal)< 0f ) && isSliding) // Ai compliant
                {
                    // applies gravity if we are on a slope. However, will multiply gravity by 0.3f if we are moving in the direction of the slope
                    curVerticalVelocity -= gravity * gravityMultiplier * 0.3f * Time.deltaTime;
                }
                else
                {
                    // applies gravity if we are on a slope
                    curVerticalVelocity -= gravity * gravityMultiplier * Time.deltaTime;
                }
            }

        }
        else if (gravityEnabled && isGrounded && curVerticalVelocity < 0f)
        {
            curVerticalVelocity = 0f;
        }


        // Handling sliding
        if (isSliding && isGrounded)
        {
            // Depending on the current user input direction and if the player is trying to move away from the slope causing sliding
            if ((Vector2.Dot(groundHit.normal.normalized, playerInputHandler.groundMovementDirection) > 0f && currentlyPossessed) ||
                (!currentlyPossessed && Vector2.Dot(groundHit.normal.normalized, Ai_movementDirection) > 0f)) // Ai compliant
            {
                // in the case that we are moving away from the slope causing sliding, set curVerticalVelocity to 0f

                curVerticalVelocity = 0f;
                rb.velocity = new Vector2(velocity.x + backgroundVelocity.x, 0f);

            }
            else
            {
                // else just apply gravity perpendicularly to the slope causing sliding
                rb.velocity = Vector2.Perpendicular(groundHit.normal) * -curVerticalVelocity * Vector2.Dot(Vector2.up, -Vector2.Perpendicular(groundHit.normal));
            }

            return;
        }

        // Handles ground movement when not sliding & grounded
        if ((playerInputHandler.groundMovementDirection.x != 0f && isGrounded && !willIgnoreSteepSlope && currentlyPossessed) || (!currentlyPossessed 
            && isGrounded && !willIgnoreSteepSlope)) // Ai compliant
        {
            rb.velocity = -Vector2.Perpendicular(groundHit.normal) * (velocity.x);
            rb.velocity = new Vector2(rb.velocity.x, curVerticalVelocity);

            return;
        }

        // Handles airborne movement
        if (gravityEnabled)
        {
            if(backgroundVelocitiesCurrentlyActive)
                rb.velocity = new Vector2(velocity.x + backgroundVelocity.x, curVerticalVelocity);
            else rb.velocity = new Vector2(velocity.x, curVerticalVelocity);
        }
        else
            rb.velocity = velocity;
    }

    // Apply background velocities applies a background velocity towards our rigidbody velocity
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

    // SetVelocity() is called when setting a new targetVelocity for this player
    public void SetVelocity(Vector2 newVelocity)
    {
        if (newVelocity == Vector2.zero)
        {
            t_startMovementCurveTimestamp = Time.time;
            slowing = true; // will be slowing to a halt
            return;
        }

        slowing = false;
        t_startMovementCurveTimestamp = Time.time;
        targetVelocity = newVelocity;
    }

    // GetBackgroundVelocities return a vector sum of all background velocities
    public Vector2 GetBackgroundVelocities()
    {
        UpdateBackgroundVelocities(); // updates background velocities by ensuring that expired background velocities are removed from our list of background velocities

        int index = 0;
        Vector2 temp = Vector2.zero; // temp is the vector sum of our background velocities

        backgroundVelocitiesCurrentlyActive = false;

        // processing of summing up background velocities
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

            // since no process every actually brings a backgroundVelocityType.LERPING_EFFECT as of now, we won't need to create logic for it when trying 
            // to form the sum of our background velocities

            index++;
        }


        return temp;

    }

    // updates background velocities by ensuring that expired background velocities are removed from our list of background velocities
    // indexesMarked are marked background velocities that will be removed from our list
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

            // since no process every actually brings a backgroundVelocityType.LERPING_EFFECT as of now, we won't need to create logic for it when trying 
            // to deal with removing expired background velocities
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

    // Setting a new background velocity with gravity incorporated into that background velocity but we set a distance we wish to cover over a distance defined as horizontalFricitionDuration
    public int SetNewBackgroundVelocityGravityIncorporated(Vector2 distance, float horizontalFrictionDuration, animationCurves animationCurveType = animationCurves.EXPONENTIAL_DECAY)
    {
        Vector2 newBackgroundVelocity = new Vector2((distance.x * 2f) / horizontalFrictionDuration, Mathf.Sqrt(Mathf.Abs(2 * gravity * gravityMultiplier * distance.y)));

        backgroundVelocities.Add(newBackgroundVelocity);
        backgroundVelocityTimestamps.Add(Time.time + horizontalFrictionDuration + Time.deltaTime); // point in time at which background should expire
        backgroundVelocityDurations.Add(horizontalFrictionDuration);
        backgroundVelocityTypes.Add(backgroundVelocityType.GRAVITY_INCORPORATED_WITH_FRICTION);
        backgroundVelocityID.Add(currentNewID);

        curVerticalVelocity += newBackgroundVelocity.y;
        if (curVerticalVelocity > 0f && isGrounded) isGrounded = false;

        // adds appropriate animation curve to this backgroundVelocity that will represent how this background velocity changes over the course of time before it expires
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

    // Setting a new background velocity with gravity incorporated into that background velocity but we set a velocity we wish to start off and the background velocity will expeire after
    // a duration of "horizontalFrictionDuration"
    public int SetNewBackgroundVelocityGravityIncorporated_Velcoity(Vector2 velocity, float horizontalFrictionDuration, animationCurves animationCurveType = animationCurves.EXPONENTIAL_DECAY)
    {
        Vector2 newBackgroundVelocity = velocity;
        backgroundVelocities.Add(newBackgroundVelocity);
        backgroundVelocityTimestamps.Add(Time.time + horizontalFrictionDuration + Time.deltaTime); // point in time at which background should expire
        backgroundVelocityDurations.Add(horizontalFrictionDuration);
        backgroundVelocityTypes.Add(backgroundVelocityType.GRAVITY_INCORPORATED_WITH_FRICTION);
        backgroundVelocityID.Add(currentNewID);

        curVerticalVelocity += newBackgroundVelocity.y;
        if (curVerticalVelocity > 0f && isGrounded) isGrounded = false;

        // adds appropriate animation curve to this backgroundVelocity that will represent how this background velocity changes over the course of time before it expires
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

    // setting a new background velocity with a lerping effect, this is no  demand for this functionality, so I left out the logic...
    public int SetNewBackgroundVelocityLerpingEffect(Vector2 distance)
    {
        // ...
        return currentNewID++;
    }

    // removes a background velocity at a specific index
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


    // "previous" variables are used to store previous states of data before a hitstop had occurred to the player can return normal after the hitstop has been performed
    [HideInInspector]
    public Vector2 previousRbVelocity;

    bool previousDodgeMomentumState;
    bool previousDodgeState;

    // forms a hitstip for a duration of time (hitstop defined above) and the force describes the amount of screen shake that should occur
    public void HitStop(float duration, float force)
    {
        previousDodgeState = dodging;
        previousDodgeMomentumState = dodgeCarryingMomentum;

        previousRbVelocity = rb.velocity;

        int index = 0;
        for(;index<backgroundVelocityTimestamps.Count;index++)
        {
            // offsetting timestamps of backgroundVelocityTimestamps so they will resume naturally after the hitstop is finished
            backgroundVelocityTimestamps[index] += duration + Time.deltaTime;
            index++;
        }

        dodging = false;
        dodgeCarryingMomentum = false;
        hitStopActive = true;
        anim.speed = 0f; // freezing animation
        rb.velocity = Vector2.zero;

        if (currentlyPossessed)
        {
            impulseHandler.Shake(force); // inducing screen shake
        }

        StartCoroutine(HitStopEnumerator(duration));  // at the end of this coroutine, our character will return back to normal
    }

    // IEnumerator that times our hitstop so we can return back to a normal state after "duration"
    IEnumerator HitStopEnumerator(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (previousDodgeState)
        {
            // ensuring we offset our dodgeTimestamp so we can resume back to our normal dodge since hitstop has now finished
            dodgeTimestamp += duration + Time.deltaTime;
            dodging = true;
        } else if (previousDodgeMomentumState)
        {
            // ensuring we offset our dodgeMomentumFallofTimestamp so we can resume back to our falling off dodge momentun since hitstop has now finished
            dodgeMomentumFallofCurveTimestamp += duration + Time.deltaTime;
            dodgeCarryingMomentum = true;
        }

        // since are hitstop is finished, we are resetting variables back to their original state
        rb.velocity = previousRbVelocity;
        hitStopActive = false;
        anim.speed = 1f;
    }
    
    // ApplyKnockback provides a quick and simple way to add a backgroundVelocity onto this character, based on the direction this character has been hit from...
    public void ApplyKnockback(knockbackDirection direction, bool attackerFacingRight, float distance, float horizontalFrictionDuration,
        animationCurves animationCurveType = animationCurves.EXPONENTIAL_DECAY)
    {
        SetNewBackgroundVelocityGravityIncorporated(knockbackDirectionClass.calculateKnockbackDirection(direction, attackerFacingRight) * distance,
            horizontalFrictionDuration, animationCurveType);
    }

    // Clears IDs from ALL hitboxes that basically represent the IDs of the characters who were hit this frame
    public void HITBOXES_ForceClearAll()
    {
        foreach (HitboxHandler hitbox in hitboxes)
        {
            hitbox.ClearIDs(clearDegree.CLEAR_ALL);
        }
    }

    // Clears IDs that basically represent the IDs of the characters who were hit this frame. However, its clear degree depends on hitbox.clearAmount
    public void HITBOXES_UpdateClearIDs()
    {
        foreach(HitboxHandler hitbox in hitboxes)
        {
            hitbox.ClearIDs(hitbox.clearAmount);
        }
    }

    // Clears one time IDs that basically represent the IDs of the characters who were hit during the duration of the entire attack animation
    public void HITBOXES_ClearOnetimeIDs()
    {
        foreach(HitboxHandler hitbox in hitboxes)
        {
            hitbox.ClearOnetimeIDs();
        }
    }

    public virtual void PerformMovement(InputAction.CallbackContext context)
    {
        // meant to be overwritten by specialised character controller that inherits this character controller
    }

    public virtual void PerformMovementAi(Vector2 direction)
    {
        // meant to be overwritten by specialised character controller that inherits this character controller
    }

    public virtual void PerformJump(InputAction.CallbackContext context)
    {
        // meant to be overwritten by specialised character controller that inherits this character controller
    }

    public virtual void PerformJumpAi(bool holdSpaceKey = false, bool mustBeGrounded = false)
    {
        // meant to be overwritten by specialised character controller that inherits this character controller
    }

    float calculatedDodgeSpeed = 0f; 
    public virtual void PerformDodge(InputAction.CallbackContext context)
    {
        // not meant to be typically overwritten

        if (dodgeIndex > maxDodges - 1) return;
        
        // calculating dodgeSpeed and them subsequently determing the dodgeVelocity and dodgeTimeStamp (which represents the time our dodge will expire)
        calculatedDodgeSpeed = (dodgeDistance / dodgeTime * (dodgeCurve.keys[2].time - dodgeCurve.keys[1].time));
        if (playerInputHandler.universalMovementDirection == Vector2.zero)
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

        // we reset our jumps, however, we remove our ground jump by reducing jumpIndex by 1 (which will now an air jump), instead of setting it to 0 (which represents our ground jump)
        if (jumpIndex == maxJumps && jumpIndex != 0) jumpIndex -= 1;
        else jumpIndex -= (maxJumps-jumpIndex);
        if(!isGrounded)dodgeIndex++;
    }

    // PerformDodgeAi is just like PerformDodge, but instead we take in a Vector2 direction as supposed to an InputAction.CallbackContext context
    public virtual void PerformDodgeAi(Vector2 direction)
    {
        // not meant to be typically overwritten

        // Ai have overrided all movement
        if (holdMovementAiOverride) return;
        if (dodgeIndex > maxDodges - 1) return;

        // calculating dodgeSpeed and them subsequently determing the dodgeVelocity and dodgeTimeStamp (which represents the time our dodge will expire)
        calculatedDodgeSpeed = (dodgeDistance / dodgeTime * (dodgeCurve.keys[2].time - dodgeCurve.keys[1].time));
        if (direction == Vector2.zero)
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

        // we reset our jumps, however, we remove our ground jump by reducing jumpIndex by 1 (which will now an air jump), instead of setting it to 0 (which represents our ground jump)
        if (jumpIndex == maxJumps) jumpIndex -= 1;
        else jumpIndex -= (maxJumps - jumpIndex);
        if (!isGrounded) dodgeIndex++;
    }

    public virtual void PerformLightAttack(InputAction.CallbackContext context)
    {
        // meant to be overwritten by specialised character controller that inherits this character controller
    }

    public virtual void PerformHeavyAttack(InputAction.CallbackContext context)
    {
        // meant to be overwritten by specialised character controller that inherits this character controller
    }

    public virtual void PerformBasicAbility(InputAction.CallbackContext context)
    {
        // meant to be overwritten by specialised character controller that inherits this character controller
    }

    public virtual void PerformUltimateAbility(InputAction.CallbackContext context)
    {
        // meant to be overwritten by specialised character controller that inherits this character controller
    }

    public virtual void PerformCrownThrow(InputAction.CallbackContext context, Vector2 direction)
    {
        // not typically meant to be overwritten

        // on crown throw, we activate the crown object and run its crown throwing script
        crownObject.SetActive(true);
        crownObject.transform.position = transform.position;
        crownScript.ThrowMe(direction, myCollider);
    }

    // code that is called when intially possessed by a character
    public virtual void PossessMe()
    {
        // not typically meant to be overwritten even though it is a virtual void


        anim.SetBool(Animator.StringToHash("Running"), playerInputHandler.possessedCharacter.anim.GetBool(Animator.StringToHash("Running")));
        Ai_movementDirection = Vector2.zero;

        Vector2 targetVelocity = new Vector2(movementSpeed * playerInputHandler.groundMovementDirection.x, curVerticalVelocity);
        SetVelocity(targetVelocity);
        currentlyPossessed = true;

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

        baseAiController.currentlyBeingPossessed = true;
        holdMovementAiOverride = false;
    }

    IEnumerator CalculateNewPathEnumerator(float wait)
    {
        yield return new WaitForSeconds(wait);
        baseAiController.currentlyBeingPossessed = false;
        baseAiController.reachedEndOfPath = false;
        baseAiController.pathComplete = false;
        baseAiController.takingDetour = false;
        baseAiController.specialWaypointUpcoming = false;
        baseAiController.currentWaypoint = 0;

        baseAiController.StartNewPath(baseAiController.targetPlayerPosition.position, false);
    }

    // Code that is called when this character is no longer being possessed
    public virtual void OnPossessionLeave()
    {
        // not typically meant to be overwritten

        movementDirection = Vector2.zero;
        targetVelocity = Vector2.zero;
        shadow.SetActive(false);
        crown.SetActive(false);
        currentlyPossessed = false;
        anim.SetBool(Animator.StringToHash("Running"), false);

        if(!hitStopActive)
        overrideJumpAnim = false;

        StartCoroutine(CalculateNewPathEnumerator(0.2f));
    }

    // used to perform a jump at a jumpWaypoint in the instance where this character is not being possessed so the 
    // Ai has taken over
    public virtual void JumpWaypointAI(bool holdSpaceKey=false, bool mustBeGrounded=false)
    {
        PerformJumpAi(holdSpaceKey, mustBeGrounded);
    }

    // used to perform an Ai holdmovemnt command that holds our Ai movement at a "Vector2 direction" for a duration defined from "time"
    private float t_holdMovementAiTimeStamp = 0f;
    public bool holdMovementAiOverride = false;
    public virtual void HoldMovementAi(Vector2 direction, float time)
    {
        holdMovementAiOverride = true;
        t_holdMovementAiTimeStamp = Time.time + time;
        PerformMovementAi(direction);
    }
    
    // used to perform an Ai run at a runWaypoint in the instance where this character is not being possessed so the 
    // Ai has taken over
    public virtual void RunWaypointAI(Vector2 direction)
    {
        PerformMovementAi(direction);
    }

    // used to perform an Ai dodge or dash (terms are analogus) in the instance where this character is not being possessed so the
    // Ai has taken over
    public virtual void DodgeWaypointAI(Vector2 direction)
    {
        PerformDodgeAi(direction);
    }

    public virtual void RunAtFixedUpdate()
    {
        // meant to be overwritten by specialised character controller that inherits this character controller
    }

    public virtual void RunAtStart()
    {
        // meant to be overwritten by specialised character controller that inherits this character controller
    }

    public virtual void RunAtUpdate()
    {
        // meant to be overwritten by specialised character controller that inherits this character controller
    }
}
