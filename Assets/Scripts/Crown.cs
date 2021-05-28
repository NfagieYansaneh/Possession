using System.Collections;
using System.Collections.Generic;
using UnityEngine;

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

    public void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        possessedPlayersCollider = playerInputHandler.GetComponent<Collider2D>();
        //gameObject.SetActive(false);
    }

    public void FixedUpdate()
    {
        // rb.velocity = velocity; // * velocityChangeCurve.Evaluate(Time.time - t_velocityChangeCurveTimestamp);
        SeekCharacters();
        GravitateToSeeked();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, characterAffnRange);
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, characterAffnExitRange);

        if (Application.isPlaying)
        {
            foreach (BaseCharacterController baseCharacterController in BaseCharacterController.baseCharacterControllers)
            {
                if (seekedCharacters.Contains(baseCharacterController))
                {
                    Gizmos.color = Color.green;
                }
                else
                {
                    Gizmos.color = Color.red;
                }

                Gizmos.DrawLine(transform.position, baseCharacterController.transform.position);
            }
        }
    }

    public void ThrowMe(Vector2 desiredDirection, Collider2D newCollider)
    {
        // gameObject.SetActive(true); // A deactivated game object can not activate itself
        if (desiredDirection == Vector2.zero) direction = playerInputHandler.possessedCharacter.transform.right;
        else direction = desiredDirection;
        possessedPlayersCollider = newCollider;

        rb.velocity = direction * speed;

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

    public void GravitateToSeeked()
    {
        foreach(BaseCharacterController baseCharacterController in seekedCharacters)
        {
            Vector2 direction = baseCharacterController.transform.position - gameObject.transform.position;
            float distance = direction.magnitude;

            if (distance == 0f) continue;

            // check for override!

            // applying gravity formula whilst assuming the crown and seeked character have a mass of 1. Furthermore, our Big G is
            // our characterAffinity value unless it is overriden

            float forceMagnitude = (characterAffinity) / (distance * distance);
            Vector2 force = direction.normalized * forceMagnitude;
            rb.AddForce(force);
        }
    }

    // depreicated 
    /* public void GravitateTowardsCharacters()
    {
        // loops through every base character controller in game scene
        foreach(BaseCharacterController baseCharacterController in BaseCharacterController.baseCharacterControllers)
        {
            if (baseCharacterController == playerInputHandler.possessedCharacter)
            {
                Debug.LogWarning("THIS");
                continue;
            }

            Debug.LogWarning("NOT THIS");
            Vector2 direction = baseCharacterController.transform.position - gameObject.transform.position;
            float distance = direction.magnitude;

            if (distance == 0f) return;

            float forceMagnitude = (baseCharacterController.crownAffinityScalar * globalCrownCharacterAffinity) / (distance * distance);
            Vector2 force = direction.normalized * forceMagnitude;
            rb.AddForce(force);
        }
    } */

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
            Debug.DrawRay(hit.point, direction, Color.yellow, 1f);

            rb.velocity = direction * rb.velocity.magnitude * dampening;
            // t_velocityChangeCurveTimestamp = Time.time;

        }
    }
}
