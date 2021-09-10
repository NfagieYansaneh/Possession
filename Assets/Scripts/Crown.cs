using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using Photon;
using Photon.Pun;
using UnityEditor;

public class Crown : MonoBehaviourPun
{
    [Header("Basic variables")]
    public float throwSpeed;
    [HideInInspector]
    public Vector2 velocity;
    // public AnimationCurve velocityChangeCurve; // how our speed changes overtime when our velocity changes (typically by deflections)

    // t_ variables are for timing purposes (and are hidden)
    // [HideInInspector] public float t_velocityChangeCurveTimestamp = 0f;

    public PlayerInputHandler playerInputHandler; // player input handler that controls this crown

    [Header("Collision detection")]
    [HideInInspector] public Rigidbody2D rb;
    public LayerMask layerGroundMaskCollision;
    public LayerMask layerCharacterMaskCollision;
    [HideInInspector]
    //public Collider2D possessedPlayersCollider;

    [Header("Character Seeking & PN")]
    public bool enablePN = true;
    public float characterAffnRange = 1f;
    public float characterAffnExitRange = 2f;
    public float forceLockOnRadius = 0f; // if distance of the crown towards a seekedCharacter is lower than this, than the crown will be forced to seek that character
    // as long as its within the crown 'cone' vision

    public List<BaseCharacterController> seekedCharacters = new List<BaseCharacterController>();
    public bool seeking = false;
    BaseCharacterController seekedCharacter;

    // seekedCharacterDirection is a Vector2 value that represents the displacement from the crown towards the seekedCharacter
    private Vector2 seekedCharacterDirection = Vector2.zero;

    // seekedCharacterDistance is the distance from the crown to the seekedCharacter
    private float seekedCharacterDistance = 0f;

    // seekedCharacterDot is the dot product from our crown's velocity and the direction towards the seekedCharacter
    private float seekedCharacterDot = 0f;

    [Range(0f, 180f)]
    public float crownFOVAngle;
    [Range(0f, 180f)]
    public float ignoreCollisionFOVAngle; // used to ignore collision that would result in a deflection if the collision is neglible
    public float maxAngCorr;
    public float dampening = 0.85f;
    private Vector3 prevLOS;
    private Vector3 LOS;
    private Vector3 LOSGain;
    private float LOSAng;

    float crownFOVDot;
    float ignoreCollisionFOVDot;

    Vector3 upperFOVBound;
    Vector3 lowerFOVBound;
    Vector3 upperIgnoreFOVBound;
    Vector3 lowerIgnoreFOVBound;

    public bool requirePhotonView = true;

    public void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        //possessedPlayersCollider = playerInputHandler.GetComponent<Collider2D>();
        //gameObject.SetActive(false);
    }

    public void FixedUpdate()
    {
        // For drawing and setting up the values for the crowns FOV for seeking characters when outside of a characters forceLockOnRadius
        // This will be shifted outside of FixedUpdate() and into Start() later for performance reasons as we don't need to continously compute this number over and over
        // again. This is only in FixedUpdate() for convenice.
        if (requirePhotonView)
        {
            if (!photonView.IsMine) return;
        }

        upperFOVBound = transform.right * (characterAffnRange / 2);
        lowerFOVBound = transform.right * (characterAffnRange / 2);
        upperFOVBound = Vector3.RotateTowards(rb.velocity.normalized, Vector2.Perpendicular(rb.velocity.normalized), -crownFOVAngle * Mathf.Deg2Rad, 0.0f);
        lowerFOVBound = Vector3.RotateTowards(rb.velocity.normalized, -Vector2.Perpendicular(rb.velocity.normalized), -crownFOVAngle * Mathf.Deg2Rad, 0.0f);

        upperIgnoreFOVBound = transform.right * (characterAffnRange / 2);
        lowerIgnoreFOVBound = transform.right * (characterAffnRange / 2);
        upperIgnoreFOVBound = Vector3.RotateTowards(rb.velocity.normalized, Vector2.Perpendicular(rb.velocity.normalized), -ignoreCollisionFOVAngle * Mathf.Deg2Rad, 0.0f);
        lowerIgnoreFOVBound = Vector3.RotateTowards(rb.velocity.normalized, -Vector2.Perpendicular(rb.velocity.normalized), -ignoreCollisionFOVAngle * Mathf.Deg2Rad, 0.0f);

        // if dot product towards a potential possessiable character is greater than or equal to crownFOVDot. Then we are in the crown's FOV
        crownFOVDot = Vector3.Dot(rb.velocity.normalized, upperFOVBound);
        ignoreCollisionFOVDot = Vector3.Dot(rb.velocity.normalized, upperIgnoreFOVBound);

        if (enablePN) ApplyPorportionalNavigation();
    }


    // purely for debugging in order for me to visualise the process of seeking and tracking down characters so possession takes place
    #if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (requirePhotonView)
        {
            if (!photonView.IsMine) return;
        }

        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + upperFOVBound);
        Gizmos.DrawLine(transform.position, transform.position + lowerFOVBound);

        Handles.Label(transform.position + Vector3.up * 2f, Convert.ToString(crownFOVDot));

        Gizmos.color = Color.magenta;
        Gizmos.DrawLine(transform.position, transform.position + upperIgnoreFOVBound);
        Gizmos.DrawLine(transform.position, transform.position + lowerIgnoreFOVBound);

        for (int i = 0; i < seekedCharacters.Count; i++)
        {
            Vector3 distanceHalf = (seekedCharacters[i].transform.position - transform.position) / 2;

            Vector2 direction = seekedCharacters[i].transform.position - transform.position;
            float dot = Vector2.Dot(direction.normalized, rb.velocity.normalized);

            Handles.Label(transform.position + distanceHalf, Convert.ToString(dot));

            Gizmos.color = Color.yellow;
            Gizmos.DrawWireSphere(seekedCharacters[i].transform.position, forceLockOnRadius);

            if (seekedCharacters[i] == seekedCharacter && seeking)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, seekedCharacters[i].transform.position);
                continue;
            }

            Gizmos.color = Color.white;
            Gizmos.DrawLine(transform.position, seekedCharacters[i].transform.position);
        }
    }
    #endif

    // throws crown
    public void ThrowMe(Vector2 desiredDirection, Collider2D newCollider)
    {
        // gameObject.SetActive(true); // A deactivated game object can not activate itself
        Vector2 direction = Vector2.zero;

        if (desiredDirection == Vector2.zero)
        {
            direction = playerInputHandler.possessedCharacter.transform.right;
            if (!playerInputHandler.possessedCharacter.facingRight)
                direction *= -1;
        }

        else direction = desiredDirection;
        //possessedPlayersCollider = newCollider;

        rb.velocity = velocity = direction * throwSpeed;
    }

    // adds characters to the seeking list if they are within the character affinity range
    public void SeekCharacters()
    {
        foreach (BaseCharacterController baseCharacterController in BaseCharacterController.baseCharacterControllers)
        {
            if (baseCharacterController == playerInputHandler.possessedCharacter) continue; // ignore currently possessed character

            // Adding seeked characters

            float distance = Vector2.Distance(baseCharacterController.transform.position, transform.position);

            if (distance <= characterAffnRange)
            {
                Vector2 direction = baseCharacterController.transform.position - transform.position;

                if (!seekedCharacters.Contains(baseCharacterController))
                {
                    RaycastHit2D hit;
                    hit = Physics2D.Raycast(transform.position, direction.normalized, Mathf.Clamp(direction.magnitude, 0f, characterAffnRange), layerGroundMaskCollision); // detcting player
                    if (hit == false && direction.magnitude < characterAffnRange)
                    {
                        seekedCharacters.Add(baseCharacterController);
                    }
                }

                // check for override settings!
            }

            // removing seeked characters
            else if (distance > characterAffnExitRange)
            {
                if (seekedCharacters.Contains(baseCharacterController))
                    seekedCharacters.Remove(baseCharacterController);
            }
        }
    }

    // iterates through all seekedCharacters list and vetts them inorder to find the best character to seek. Thus, assigning this to seekedCharacter
    // returns true if able to find a character to seek that meets all the requirements. Else, returns false.
    // overwriteValues & overwriteRbVelocity is just used to overwrite values for niche cases like vetting characters based on the direction of our deflection
    // velocity when we collider with the ground
    public bool VettSeekedCharacters(bool overwriteValues, Vector2 overwriteRbVelocity)
    {
        seeking = false;
        foreach (BaseCharacterController baseCharacterController in seekedCharacters)
        {
            Vector2 direction = baseCharacterController.transform.position - transform.position;
            float dot;

            if (overwriteValues == false)
            {
                dot = Vector2.Dot(direction.normalized, rb.velocity.normalized);
            } else
            {
                dot = Vector2.Dot(direction.normalized, overwriteRbVelocity);
            }

            float distance = direction.magnitude;

            // Vector2.dot takes in two vectors and tells us whether both vectors face the same direction, or opposite directions
            // Vector2.dot returns a float that ranges from 1 to -1
            // 1 means both vectors face in the exact same direction
            // -1 means both vectors face in the exact opposite directions

            // first case handles a case where there is only 1 potential seekedCharacter and just makes sure its in the crowns cone of vision
            // second case handles a situation where our 'dot' is greater than the 'previousDot' but is not too far from the originally seekedCharacter
            // third case handles a situation where our 'dot' is or is not greater than the 'previousDot' but is far closer to the originally seekedCharacter

            if (dot >= crownFOVDot && !seeking ||
                dot >= crownFOVDot && dot > seekedCharacterDot && !(Vector2.Distance(transform.position, seekedCharacter.transform.position) <= forceLockOnRadius) ||
                distance < forceLockOnRadius && !seeking ||
                distance < forceLockOnRadius && seeking && distance < seekedCharacterDistance)
            {
                seekedCharacterDot = dot;
                seeking = true;
                seekedCharacter = baseCharacterController;
                seekedCharacterDistance = distance;
                seekedCharacterDirection = direction;
            }
            else if (seekedCharacter == baseCharacterController) // can no longer seek that character because it does not pass any of the 3 cases above
            {
                Debug.LogWarning(seekedCharacter + " No longer reaches the requirements to be seeked");
                seeking = false;
            }
        }

        if (!seeking) return false;
        else return true;
    }

    // return true if it was able to apply porportional navigation, else returns false
    public bool ApplyPorportionalNavigation()
    {
        // Tutorial that taught me how to intergrate proportional navigation into my crown
        // https://www.moddb.com/members/blahdy/blogs/gamedev-introduction-to-proportional-navigation-part-i
        // https://answers.unity.com/questions/585035/lookat-2d-equivalent-.html

        SeekCharacters();
        if (VettSeekedCharacters(false, Vector2.zero) == false) return false;

        // Obtaining Line Of Sight (LOS) rotation rate
        transform.right = seekedCharacter.transform.position - transform.position;

        LOS = seekedCharacter.transform.position - transform.position;
        LOSGain = LOS.normalized - prevLOS.normalized;
        LOSAng = Vector3.Angle(transform.right, LOS);

        Vector2 perpVec = Vector3.Cross(LOS.normalized, LOSGain.normalized); //perpVec is perpindicular to both the LOS and the LOS change
        int N = 3;

        float rotationSpeed = Mathf.Clamp(Mathf.Abs(LOSGain.magnitude * N), 0, maxAngCorr); //rotation_Speed = Omega * N (Clamped to maximum rotation speed)
        if (LOSAng < crownFOVAngle) // used to be 60
        {
            Quaternion rot = Quaternion.AngleAxis(rotationSpeed, perpVec.normalized); // rotate with rotationSpeed around perpVec axis.
            //rb.velocity = Vector3.RotateTowards(rb.velocity, )
            transform.rotation *= rot;
        }
        else
        {
            Vector3 newDirection = Vector3.RotateTowards(transform.right, LOS, maxAngCorr * Mathf.Deg2Rad, 0.0f); //Turn towards target at maximum rotation speed
            /*float angle = Mathf.Atan2(newDirection.y, newDirection.x) * Mathf.Rad2Deg;
            transform.rotation = Quaternion.AngleAxis(angle, Vector3.forward);*/
            rb.velocity = Vector3.RotateTowards(rb.velocity, transform.right, maxAngCorr * Mathf.Deg2Rad, 0.0f);
            transform.rotation =
            Quaternion.LookRotation(newDirection);
        }

        prevLOS = LOS;
        rb.velocity = Vector3.RotateTowards(rb.velocity, transform.right, maxAngCorr * Mathf.Deg2Rad, 0.0f);
        //Debug.Log(transform.right);
        //rb.rotation = transform.localRotation.z;

        return true;
    }

    public void HandleDeflectionResponse(Collider2D collider)
    {
        if (requirePhotonView)
        {
            if (!photonView.IsMine) return;
        }

        // I should be using Physics2D.IgnoreCollision so I will implement that later
        if (collider.gameObject == playerInputHandler.possessedCharacter.gameObject)
        {
            return;
        }

        BaseCharacterController baseCharacterController = collider.gameObject.GetComponent<BaseCharacterController>();

        // if we collided with a character. Possess that character...
        if (baseCharacterController != null)
        {
            seekedCharacters.Clear();
            baseCharacterController.PossessMe();
            gameObject.SetActive(false);
        }
        // layermask == (layermask | (1 << layer))
        // change order of checking????
        else if (collider.gameObject.layer == 3 || collider.gameObject.layer == 7) // else if it hits the ground, deflect off it
        {
            if (seeking && seekedCharacterDot >= ignoreCollisionFOVDot)
            {
                return;

                // in this case, we ignore collision because we are still seeking a character along the crown's path and the ground's collider
                // is not too much in the way of the crown's vision since 'seeking' is still set true. Seeking would have been set false as soon as line of
                // to that seekedCharacter was broken.
            }

            RaycastHit2D hit;
            // substraction is to make sure raycast is formed whilst not being submerged in another collider as this results in fault reflected vector calculations
            hit = Physics2D.Raycast(new Vector2(transform.position.x, transform.position.y) - (0.5f*rb.velocity.normalized), rb.velocity.normalized, 1.5f, layerGroundMaskCollision); // detcting ground 
            Vector2 reflectDirection = Vector2.Reflect(rb.velocity.normalized, hit.normal);


            // Debugging
            Debug.DrawRay(hit.point, rb.velocity.normalized, Color.red, 1f);
            Debug.DrawRay(hit.point, hit.normal, Color.green, 1f);
            //Debug.DrawRay(hit.point, direction, Color.yellow, 1f);

            velocity = reflectDirection * rb.velocity.magnitude * dampening;
            if (enablePN)
            {

                // search for a possible character to possess upon deflection and recorrect our deflection vector towards that character
                if (VettSeekedCharacters(true, reflectDirection.normalized))
                {
                    // So many magic numbers
                    if (seekedCharacterDistance < forceLockOnRadius)
                    {
                        velocity = Vector3.RotateTowards(velocity, seekedCharacterDirection.normalized, 80f * Mathf.Deg2Rad, 0.0f);
                    }
                    else if (seekedCharacterDistance - 2f < forceLockOnRadius)
                    {
                        velocity = Vector3.RotateTowards(velocity, seekedCharacterDirection.normalized, 45f * Mathf.Deg2Rad, 0.0f);
                    }
                    else
                    {
                        velocity = Vector3.RotateTowards(velocity, seekedCharacterDirection.normalized, 35f * Mathf.Deg2Rad, 0.0f);
                    }
                }
            }

            rb.velocity = velocity;
            // t_velocityChangeCurveTimestamp = Time.time;

        }
    }

    // handles deflection & assisted redirection upon deflection
    private void OnTriggerEnter2D(Collider2D collider)
    {
        HandleDeflectionResponse(collider);
    }

    private void OnTriggerStay2D(Collider2D collider)
    {
        HandleDeflectionResponse(collider);
    }
}
