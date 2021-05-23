using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Crown : MonoBehaviour
{
    public float speed;
    public float affinity;
    public Rigidbody2D rb;
    public LayerMask layerMaskCollision;
    public AnimationCurve velocityChangeCurve; // how our speed changes overtime when our velocity changes (typically by deflections)

    // t_ variables are for timing purposes (and are hidden)
    [HideInInspector] public float t_velocityChangeCurveTimestamp = 0f;

    [HideInInspector]
    public Collider2D sendersCollider; // for ignoring the collider of the character who sent the crown

    [HideInInspector]
    public Vector2 direction;
    public Vector2 velocity;

    public void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        gameObject.SetActive(false);
    }

    public void FixedUpdate()
    {
        rb.velocity = velocity * velocityChangeCurve.Evaluate(Time.time - t_velocityChangeCurveTimestamp);
    }

    public void ThrowMe(Vector2 newDirection, Collider2D newSendersCollider)
    {
        // gameObject.SetActive(true); // A deactivated game object can not activate itself
        direction = newDirection;
        velocity = direction * speed;
        sendersCollider = newSendersCollider;
        t_velocityChangeCurveTimestamp = Time.time;
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        if(collider == sendersCollider) {
            return;
        }

        BaseCharacterController baseCharacterController = collider.gameObject.GetComponent<BaseCharacterController>();

        if (baseCharacterController != null)
        {
            baseCharacterController.PossessMe();
            gameObject.SetActive(false);
        }
        else if (collider.gameObject.layer == 3) // else if it hits the ground, deflect off it
        {
            RaycastHit2D hit;
            hit = Physics2D.Raycast(transform.position, direction, 1f, layerMaskCollision); // detcting ground 
            Debug.DrawRay(hit.point, hit.normal, Color.green, 1f);
            Debug.LogError("REFLECTED");
            direction = Vector2.Reflect(direction, hit.normal);
            velocity = direction * speed;
            t_velocityChangeCurveTimestamp = Time.time;
        }
    }
}
