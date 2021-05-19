using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using System; // rid of this later

public class Rin_CharacterController : BaseCharacterController
{
    Vector2 movementDirection = Vector2.zero;
    bool dodging = false;

    /* The variables & functions you have access too on baseCharacterController is...
     * 
     * movementSpeed
     * curVerticalVelocity
     * 
     * 
     * 
     * 
     * 
     */

    public override void RunAtFixedUpdate()
    { 
        if(t_velocityTimestamp <= Time.time && dodging == true)
        {
            // Run end dodging script
            dodging = false;
        } 
    }

    public override void PerformMovement(InputAction.CallbackContext context)
    {
        movementDirection = context.ReadValue<Vector2>();

        // calculates our velocity, but leaves the character current vertical velocity alone
        Vector2 targetVelocity = new Vector2(movementSpeed * movementDirection.x, curVerticalVelocity);
        SetVelocity(targetVelocity);

        // flips player based on movement keys
        if (movementDirection.x < -0.2 && facingRight == true)
        {
            facingRight = false;
            transform.localScale = new Vector3(-1, 1, 1);
        }
        else if (movementDirection.x > 0.2 && facingRight == false)
        {
            facingRight = true;
            transform.localScale = new Vector3(1, 1, 1);
        }
    }

    public override void PerformJump(InputAction.CallbackContext context)
    {
        if (isGrounded)
        {
            isGrounded = false;

            // using a kinematic formula to compute the intial vertical velocity I need to reach a given height
            float targetVerticalVelocity = Mathf.Sqrt(2 * gravity * gravityMultiplier * jumpHeight);
            curVerticalVelocity = targetVerticalVelocity;
        }
    }

    public override void PerformDodge(InputAction.CallbackContext context)
    {
        SetVelocityTimed(movementDirection * 5, 1, false, false);
        dodging = true;
    }

    public override void PerformLightAttack(InputAction.CallbackContext context)
    {
        Debug.LogWarning("Light Attack performed");
    }

    public override void PerformHeavyAttack(InputAction.CallbackContext context)
    {
        Debug.LogWarning("Heavy Attack performed");
    }

    public override void PerformBasicAbility(InputAction.CallbackContext context)
    {
        Debug.LogWarning("Basic Ability performed");
    }
}
