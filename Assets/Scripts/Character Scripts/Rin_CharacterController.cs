using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using System; // rid of this later

public class Rin_CharacterController : BaseCharacterController
{
    // Vector2 movementDirection = Vector2.zero; // already defined in BaseCharacterController.cs

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
        base.RunAtFixedUpdate();
    }

    Vector2 oldGroundMovementDirection = Vector2.zero; // quick fix
    public override void PerformMovement(InputAction.CallbackContext context)
    {
        //movementDirection = context.ReadValue<Vector2>();
        movementDirection = playerInputHandler.groundMovementDirection;
        if (oldGroundMovementDirection == movementDirection) return;

        if (movementDirection.magnitude <= playerInputHandler.universalFixedMinDeadzone)
        {
            // Debug.LogWarning("IN DEADZONE");
            movementDirection = Vector2.zero;
        } else
        {
            // Debug.Log(movementDirection.magnitude + " : " + playerInputHandler.universalFixedMinDeadzone);
        }

        Vector2 targetVelocity;

        // calculates our velocity, but leaves the character current vertical velocity alone
        targetVelocity = new Vector2(movementSpeed * movementDirection.x, curVerticalVelocity);
        SetVelocity(targetVelocity); // u can speed boost if you spam S or W while moving. Fix this bug (applied a quick fix)

        // flips player based on movement keys
        if (movementDirection.x < -0.2 && facingRight == true)
        {
            dodgeCarryingMomentum = false;
            facingRight = false;

            transform.localScale = new Vector3(-1, 1, 1);
        }
        else if (movementDirection.x > 0.2 && facingRight == false)
        {
            dodgeCarryingMomentum = false;
            facingRight = true;

            transform.localScale = new Vector3(1, 1, 1);
        }

        oldGroundMovementDirection = movementDirection;
    }

    public override void PerformJump(InputAction.CallbackContext context)
    {
        if (jumpIndex <= maxJumps - 1)
        {
            float targetVerticalVelocity;
            isFalling = false;

            if (isGrounded) { 
                // using a kinematic formula to compute the intial vertical velocity I need to reach a given height
                targetVerticalVelocity = Mathf.Sqrt(2 * gravity * gravityMultiplier * jumpHeight);
            } else
            {
                float currentHeightReduction = Mathf.Pow(successiveJumpHeightReduction, airJumpsPerformed);
                targetVerticalVelocity = Mathf.Sqrt(2 * gravity * gravityMultiplier * airborneJumpHeight * currentHeightReduction);
                airJumpsPerformed++;
            }
            isGrounded = false;

            curVerticalVelocity = targetVerticalVelocity;
            rb.velocity = new Vector2(rb.velocity.x, curVerticalVelocity);
            jumpIndex++;
        }
    }

    public override void PerformDodge(InputAction.CallbackContext context)
    {
        base.PerformDodge(context);
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

    public override void PerformUltimateAbility(InputAction.CallbackContext context)
    {
        Debug.LogWarning("Ultimate Ability performed");
    }

    public override void PerformCrownThrow(InputAction.CallbackContext context)
    {
        base.PerformCrownThrow(context);
        Debug.LogWarning("Crown Throw performed");
    }
}
