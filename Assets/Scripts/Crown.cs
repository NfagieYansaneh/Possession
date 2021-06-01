using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

public class Crown : MonoBehaviour
{
    public float speed;
    public Rigidbody2D rb;
    public LayerMask layerMaskCollision;
    // public AnimationCurve velocityChangeCurve; // how our speed changes overtime when our velocity changes (typically by deflections)

    // t_ variables are for timing purposes (and are hidden)
    // [HideInInspector] public float t_velocityChangeCurveTimestamp = 0f;

    public PlayerInputHandler playerInputHandler; // player input handler that controls this crown
    [HideInInspector]
    public Collider2D possessedPlayersCollider;

    [HideInInspector]
    public Vector2 direction;

    public float characterAffinity = 1f; // TODO - add an editor button that allows individual character to override default values
    public float characterAffnRange = 1f;
    public float characterAffnExitRange = 2f;
    public List<BaseCharacterController> seekedCharacters = new List<BaseCharacterController>();
    public LayerMask characterMask;

    public float dampening = 0.85f;
    public Vector2 velocity;

    public void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        possessedPlayersCollider = playerInputHandler.GetComponent<Collider2D>();
        //gameObject.SetActive(false);
    }

    public void Update()
    {
        //ApplyPorportionalNavigation();
    }

    public void FixedUpdate()
    {
        // rb.velocity = velocity; // * velocityChangeCurve.Evaluate(Time.time - t_velocityChangeCurveTimestamp);
        SeekCharacters();
        ApplyPorportionalNavigation();
        //ApplyPorportionalNavigation();
        //GravitateToSeeked();
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

        rb.velocity = velocity = direction * speed;
        // t_velocityChangeCurveTimestamp = Time.time;
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
                    hit = Physics2D.Raycast(transform.position, direction.normalized, characterAffnRange + 0.1f, layerMaskCollision); // detcting ground 
                    if (hit == false)
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

    bool seeking = false;
    BaseCharacterController closestCharacter;
    Vector3 prevLOS;
    Vector3 LOS;
    Vector3 LOSGain;
    float LOSAng;
    public float maxAngCorr;

    private void OnDrawGizmosSelected()
    {
        for(int i=0; i<seekedCharacters.Count; i++)
        {
            Vector3 distanceHalf = (seekedCharacters[i].transform.position - transform.position) / 2;

            Vector2 direction = seekedCharacters[i].transform.position - transform.position;
            float dot = Vector2.Dot(direction.normalized, rb.velocity.normalized);

            UnityEditor.Handles.Label(transform.position + distanceHalf, Convert.ToString(dot));

            if (seekedCharacters[i] == closestCharacter)
            {
                Gizmos.color = Color.cyan;
                Gizmos.DrawLine(transform.position, seekedCharacters[i].transform.position);
                continue;
            }

            Gizmos.color = Color.white;
            Gizmos.DrawLine(transform.position, seekedCharacters[i].transform.position);
        }
    }

    public void ApplyPorportionalNavigation()
    {
        // Tutorial that taught me how to intergrate proportional navigation into my crown
        // https://www.moddb.com/members/blahdy/blogs/gamedev-introduction-to-proportional-navigation-part-i
        // https://answers.unity.com/questions/585035/lookat-2d-equivalent-.html

        float previousDistance = 0f;
        float previousDot = 0f;
        foreach (BaseCharacterController baseCharacterController in seekedCharacters)
        {
            Vector2 direction = baseCharacterController.transform.position - transform.position;
            float dot = Vector2.Dot(direction.normalized, rb.velocity.normalized);
            //Debug.DrawRay(transform.position, rb.velocity.normalized, Color.yellow);
            //Debug.DrawRay(transform.position, direction.normalized, Color.red);

            float distance = direction.magnitude;
            if ((previousDot < dot && previousDistance > 1.3f) || 0.4f > Mathf.Abs(dot - previousDot) && distance < previousDistance || previousDistance == 0f)
            {
                previousDot = dot;
                seeking = true;
                closestCharacter = baseCharacterController;
                previousDistance = distance;
            }
        }

        if (previousDistance == 0f)
        {
            seeking = false;
            return;
        }

        // Obtaining Line Of Sight (LOS) rotation rate
        transform.right = closestCharacter.transform.position - transform.position;

        LOS = closestCharacter.transform.position - transform.position;
        LOSGain = LOS.normalized - prevLOS.normalized;
        LOSAng = Vector3.Angle(transform.right, LOS);

        Vector2 perpVec = Vector3.Cross(LOS.normalized, LOSGain.normalized); //perpVec is perpindicular to both the LOS and the LOS change
        int N = 2;

        float rotationSpeed = Mathf.Clamp(Mathf.Abs(LOSGain.magnitude * N), 0, maxAngCorr); //rotation_Speed = Omega * N (Clamped to maximum rotation speed)
        if (LOSAng < 60)
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
            hit = Physics2D.Raycast(transform.position, rb.velocity.normalized, 1f, layerMaskCollision); // detcting ground 
            direction = Vector2.Reflect(rb.velocity.normalized, hit.normal);


            // Debugging
            Debug.DrawRay(hit.point, rb.velocity.normalized, Color.red, 1f);
            Debug.DrawRay(hit.point, hit.normal, Color.green, 1f);
            //Debug.DrawRay(hit.point, direction, Color.yellow, 1f);

            rb.velocity = direction * rb.velocity.magnitude * dampening;
            if (seeking)
            {
                Vector3 x = closestCharacter.transform.position - transform.position;
                if(x.magnitude < 1.5f) rb.velocity = Vector3.RotateTowards(rb.velocity, x.normalized, 78f * Mathf.Deg2Rad, 0.0f);
                else
                rb.velocity = Vector3.RotateTowards(rb.velocity, x.normalized, 22f * Mathf.Deg2Rad, 0.0f);
            }

            velocity = rb.velocity;
            // t_velocityChangeCurveTimestamp = Time.time;

        }
    }
}
