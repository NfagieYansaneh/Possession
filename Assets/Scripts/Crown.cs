using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Crown : MonoBehaviour
{
    public float speed;
    public float affinity;
    public Rigidbody2D rb;
    public LayerMask layerMaskCollision;

    [HideInInspector]
    public Collider2D sendersCollider; // for ignoring the collider of the character who sent the crown

    [HideInInspector]
    public Vector3 direction;

    public void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        gameObject.SetActive(false);
    }

    public void ThrowMe(Vector3 newDirection, Collider2D newSendersCollider)
    {
        // gameObject.SetActive(true); // A deactivated game object can not activate itself
        direction = newDirection;
        rb.velocity = direction * speed;
        sendersCollider = newSendersCollider;
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
            rb.velocity = direction * speed;
        }
    }
}
