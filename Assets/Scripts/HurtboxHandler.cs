using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CapsuleCollider2D))]
public class HurtboxHandler : MonoBehaviour
{
    public BaseCharacterController baseCharacterController;
    private HealthHandler healthHandler;
    public CapsuleCollider2D hurtbox;

    private void Start()
    {
        hurtbox = GetComponent<CapsuleCollider2D>();
    }

    void HandleTrigger(Collider2D collision)
    {
        // ...
    }

    // 4-directional knockback
    /*
    void HandleKnockback(attackDirection directionalAttack, bool attackedFromTheRight)
    {
        switch (directionalAttack)
        {
            case attackDirection.UP:
                break;

            case attackDirection.DOWN:
                break;

            case attackDirection.FORWARD:
                if (attackedFromTheRight)
                {
                    baseCharacterController.ApplyKnockback(Vector2.rig)
                }
                break;
        }
    }
    */
    
    private void OnTriggerEnter2D(Collider2D collision)
    {
        HandleTrigger(collision);
    }
    private void OnTriggerStay2D(Collider2D collision)
    {
        HandleTrigger(collision);
    }
    private void OnTriggerExit2D(Collider2D collision)
    {
        HandleTrigger(collision);
    }
}
