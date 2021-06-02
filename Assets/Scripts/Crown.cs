using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;
using UnityEditor;

public class Crown : MonoBehaviour
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
    public Collider2D possessedPlayersCollider;

    [HideInInspector]
    public Vector2 direction;

    [Header("Character Seeking & PN")]
    public bool enablePN = true;
    public float characterAffnRange = 1f;
    public float characterAffnExitRange = 2f;
    public float forceLockOnRadius = 0f; // if distance of the crown towards a seekedCharacter is lower than this, than the crown will be forced to seek that character
    // as long as its within the crown 'cone' vision

    public List<BaseCharacterController> seekedCharacters = new List<BaseCharacterController>();
    public bool seeking = false;
    BaseCharacterController seekedCharacter;

    [Range(0f, 180f)]
    public float cone;
    public float maxAngCorr;
    public float dampening = 0.85f;
    Vector3 prevLOS;
    Vector3 LOS;
    Vector3 LOSGain;
    float LOSAng;
    //public LayerMask characterMask;

    public void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        possessedPlayersCollider = playerInputHandler.GetComponent<Collider2D>();
        //gameObject.SetActive(false);
    }

    Vector3 upperBound;
    Vector3 lowerBound;

    public void FixedUpdate()
    {
        upperBound = transform.right * (characterAffnRange / 2);
        lowerBound = transform.right * (characterAffnRange / 2);
        upperBound = Vector3.RotateTowards(rb.velocity.normalized, Vector2.Perpendicular(rb.velocity.normalized), -cone * Mathf.Deg2Rad, 0.0f);
        lowerBound = Vector3.RotateTowards(rb.velocity.normalized, -Vector2.Perpendicular(rb.velocity.normalized), -cone * Mathf.Deg2Rad, 0.0f);
        dotProduct = Vector3.Dot(rb.velocity.normalized, upperBound);

        SeekCharacters();
        if (enablePN) ApplyPorportionalNavigation();
    }

    float dotProduct;
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawLine(transform.position, transform.position + upperBound);
        Gizmos.DrawLine(transform.position, transform.position + lowerBound);
        //Handles.Label(transform.position + Vector3.up * 2f, Convert.ToString(dotProduct));

        for (int i = 0; i < seekedCharacters.Count; i++)
        {
            Vector3 distanceHalf = (seekedCharacters[i].transform.position - transform.position) / 2;

            Vector2 direction = seekedCharacters[i].transform.position - transform.position;
            float dot = Vector2.Dot(direction.normalized, rb.velocity.normalized);

            //Handles.Label(transform.position + distanceHalf, Convert.ToString(dot));

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

    public void ThrowMe(Vector2 desiredDirection, Collider2D newCollider)
    {
        // gameObject.SetActive(true); // A deactivated game object can not activate itself
        if (desiredDirection == Vector2.zero)
        {
            direction = playerInputHandler.possessedCharacter.transform.right;
            if (!playerInputHandler.possessedCharacter.facingRight)
                direction *= -1;
        }

        else direction = desiredDirection;
        possessedPlayersCollider = newCollider;

        rb.velocity = velocity = direction * throwSpeed;
    }

    public void SeekCharacters() // adds characters to the seeking list if they are within the character affinity range
    {
        foreach(BaseCharacterController baseCharacterController in BaseCharacterController.baseCharacterControllers)
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

    public int ApplyPorportionalNavigation() // return 0 if it was able to seek a character, else returns -1
    {
        // Tutorial that taught me how to intergrate proportional navigation into my crown
        // https://www.moddb.com/members/blahdy/blogs/gamedev-introduction-to-proportional-navigation-part-i
        // https://answers.unity.com/questions/585035/lookat-2d-equivalent-.html

        // previousDistance is the distance from the crown to the seekedCharacter
        float previousDistance = 0f;

        // previousDot is the dot product from our crown's velocity and the direction towards the seekedCharacter
        float previousDot = 0f;

        seeking = false;
        foreach (BaseCharacterController baseCharacterController in seekedCharacters)
        {
            Vector2 direction = baseCharacterController.transform.position - transform.position;
            float dot = Vector2.Dot(direction.normalized, rb.velocity.normalized);

            float distance = direction.magnitude;

            // Vector2.dot takes in two vectors and tells us whether both vectors face the same direction, or opposite directions
            // Vector2.dot returns a float that ranges from 1 to -1
            // 1 means both vectors face in the exact same direction
            // -1 means both vectors face in the exact opposite directions

            // first case handles a case where there is only 1 potential seekedCharacter and just makes sure its in the crowns cone of vision
            // second case handles a situation where our 'dot' is greater than the 'previousDot' but is not too far from the originally seekedCharacter
            // third case handles a situation where our 'dot' is or is not greater than the 'previousDot' but is far closer to the originally seekedCharacter

            if (dot >= dotProduct && !seeking ||
                dot >= dotProduct && dot > previousDot && !(Vector2.Distance(transform.position, seekedCharacter.transform.position) <= forceLockOnRadius) ||
                distance < forceLockOnRadius && !seeking ||
                distance < forceLockOnRadius && seeking && distance < previousDistance)
            {
                previousDot = dot;
                seeking = true;
                seekedCharacter = baseCharacterController;
                previousDistance = distance;
            }
            else if (seekedCharacter == baseCharacterController) // can no longer seek that character because it does not pass any of the 3 cases above
            {
                Debug.LogWarning("hmmm");
                seeking = false;
            }
        }

        if (!seeking) return -1;

        // Obtaining Line Of Sight (LOS) rotation rate
        transform.right = seekedCharacter.transform.position - transform.position;

        LOS = seekedCharacter.transform.position - transform.position;
        LOSGain = LOS.normalized - prevLOS.normalized;
        LOSAng = Vector3.Angle(transform.right, LOS);

        Vector2 perpVec = Vector3.Cross(LOS.normalized, LOSGain.normalized); //perpVec is perpindicular to both the LOS and the LOS change
        int N = 3;

        float rotationSpeed = Mathf.Clamp(Mathf.Abs(LOSGain.magnitude * N), 0, maxAngCorr); //rotation_Speed = Omega * N (Clamped to maximum rotation speed)
        if (LOSAng < cone) // used to be 60
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

        return 0;
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if(collider == possessedPlayersCollider) {
            return;
        }

        BaseCharacterController baseCharacterController = collider.gameObject.GetComponent<BaseCharacterController>();

        if (baseCharacterController != null)
        {
            seekedCharacters.Clear();
            baseCharacterController.PossessMe();
            gameObject.SetActive(false);
        }

        else if (collider.gameObject.layer == 3) // else if it hits the ground, deflect off it
        {
            RaycastHit2D hit;
            hit = Physics2D.Raycast(transform.position, rb.velocity.normalized, 1f, layerGroundMaskCollision); // detcting ground 
            direction = Vector2.Reflect(rb.velocity.normalized, hit.normal);


            // Debugging
            Debug.DrawRay(hit.point, rb.velocity.normalized, Color.red, 1f);
            Debug.DrawRay(hit.point, hit.normal, Color.green, 1f);
            //Debug.DrawRay(hit.point, direction, Color.yellow, 1f);

            rb.velocity = direction * rb.velocity.magnitude * dampening;
            if (seeking && enablePN)
            {
                /*bool ready = false;
                float previousDot = 0f;
                float previousDistance = 0f;
                foreach (BaseCharacterController potentialSeek in seekedCharacters)
                {
                    Vector2 direction = potentialSeek.transform.position - transform.position;
                    float dot = Vector2.Dot(direction.normalized, rb.velocity.normalized);

                    if (dot > previousDot || direction.magnitude < forceLockOnRadius && !ready ||
                        direction.magnitude < forceLockOnRadius && direction.magnitude < previousDistance && ready)
                    {
                        ready = true;
                        seekedCharacter = potentialSeek; 
                    }
                }*/

                Vector3 x = seekedCharacter.transform.position - transform.position;
                if(x.magnitude < forceLockOnRadius) rb.velocity = Vector3.RotateTowards(rb.velocity, x.normalized, 80f * Mathf.Deg2Rad, 0.0f);
                else if (x.magnitude - 2f < forceLockOnRadius) rb.velocity = Vector3.RotateTowards(rb.velocity, x.normalized, 60f * Mathf.Deg2Rad, 0.0f);
                else
                rb.velocity = Vector3.RotateTowards(rb.velocity, x.normalized, 40f * Mathf.Deg2Rad, 0.0f);
            }

            velocity = rb.velocity;
            // t_velocityChangeCurveTimestamp = Time.time;

        }
    }
}
