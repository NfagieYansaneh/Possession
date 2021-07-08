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

    float oldAnimSpeed = 0f;
    bool playingJumpAnimation = false;
    public override void RunAtUpdate()
    {
        base.RunAtUpdate();

        if (!overrideJumpAnim && !hitStopActive)
        {
            if (!isGrounded || isGrounded && curVerticalVelocity > 0f)
            {
                playingJumpAnimation = true;
                int airIndex;
                float peakRisingVelocity = Mathf.Sqrt(2 * gravity * gravityMultiplier * jumpHeight);
                float peakFallingVelocity = Mathf.Sqrt(2 * gravity * gravityMultiplier * jumpHeight);
                airIndex = (int)Mathf.Clamp(Helper.Remap(curVerticalVelocity, peakRisingVelocity, -1 / 2f * peakFallingVelocity, 0f, 7f), 0, 7);
                if (oldAnimSpeed != anim.speed)
                {
                    oldAnimSpeed = anim.speed;
                }
                anim.speed = 0f;
                anim.Play("Rin_Jump", 0, (1f / 8) * airIndex);
            }
            else
            {
                if (playingJumpAnimation)
                {
                    Debug.Log("Landed");
                    anim.SetTrigger(Animator.StringToHash("Landed"));
                    playingJumpAnimation = false;
                    anim.speed = 1f;
                }

                playingJumpAnimation = false;
            }
        } 
        else if (!hitStopActive)
        {
            anim.speed = 1f;
        }
    }

    public override void RunAtStart()
    {
        base.RunAtStart();

        oldAnimSpeed = anim.speed;
    }

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
            anim.SetBool(Animator.StringToHash("Running"), false);
        } else
        {
            anim.SetBool(Animator.StringToHash("Running"), true);
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

    public override void PerformMovementAi(Vector2 direction)
    {
        if (direction == Vector2.zero)
        {
            // Debug.LogWarning("IN DEADZONE");
            // movementDirection = Vector2.zero;
            anim.SetBool(Animator.StringToHash("Running"), false);
        }
        else
        {
            anim.SetBool(Animator.StringToHash("Running"), true);
            // Debug.Log(movementDirection.magnitude + " : " + playerInputHandler.universalFixedMinDeadzone);
        }

        Ai_movementDirection = direction;
        Vector2 targetVelocity;

        // calculates our velocity, but leaves the character current vertical velocity alone
        targetVelocity = new Vector2(movementSpeed * direction.x, curVerticalVelocity);
        SetVelocity(targetVelocity); // u can speed boost if you spam S or W while moving. Fix this bug (applied a quick fix)

        // flips player based on movement keys
        if (direction.x < -0.2 && facingRight == true)
        {
            dodgeCarryingMomentum = false;
            facingRight = false;

            transform.localScale = new Vector3(-1, 1, 1);
        }
        else if (direction.x > 0.2 && facingRight == false)
        {
            dodgeCarryingMomentum = false;
            facingRight = true;

            transform.localScale = new Vector3(1, 1, 1);
        }

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
        switch (playerInputHandler.playerAttackDirection)
        {
            case (int)attackDirection.FORWARD:
                anim.SetTrigger(Animator.StringToHash("Forward Light"));
                break;

            case (int)attackDirection.DOWN:
                anim.SetTrigger(Animator.StringToHash("Down Light"));
                break;

            case (int)attackDirection.NEUTRAL:
                break;

            case (int)attackDirection.UP:
                anim.SetTrigger(Animator.StringToHash("Up Light"));
                break;
        }
        //anim.SetTrigger(Animator.StringToHash("Forward Light"));
        overrideJumpAnim = true;
        Debug.LogWarning("Light Attack performed");
    }

    // please shift these to base character controller script when creating more characters
    public void ResetOverrideJumpAnim()
    {
        HITBOXES_ClearOnetimeIDs();

        overrideJumpAnim = false;
        if (!isGrounded)
        {
            EnableShadowCrownDebug();
        }
    }

    // Quick Debugging tools
    public void DisableShadowCrownDebug()
    {
        shadow.SetActive(false);
        crown.SetActive(false);
        //basicOutline.SetActive(false);
    }
    public void EnableShadowCrownDebug()
    {
        shadow.SetActive(true);
        crown.SetActive(true);
        //basicOutline.SetActive(true);
    }

    public override void PerformHeavyAttack(InputAction.CallbackContext context)
    {
        switch (playerInputHandler.playerAttackDirection)
        {
            case (int)attackDirection.FORWARD:
                anim.SetTrigger(Animator.StringToHash("Forward Heavy"));
                break;

            case (int)attackDirection.DOWN:
                anim.SetTrigger(Animator.StringToHash("Down Heavy"));
                break;

            case (int)attackDirection.NEUTRAL:
                break;

            case (int)attackDirection.UP:
                anim.SetTrigger(Animator.StringToHash("Up Heavy"));
                break;
        }

        overrideJumpAnim = true;
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
