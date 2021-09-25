using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;
using System; 

/* Purpose of Rin_CharacterController is to override some functions within the BaseCharacterController script to fit
 * the specific needs of our character, Rin
 */


public class Rin_CharacterController : BaseCharacterController
{
    // Vector2 movementDirection = Vector2.zero; // already defined in BaseCharacterController.cs

    // Note, I also have access to movmentSpeed and curVerticalVelocity that is apart of our BaseCharacterController

    float oldAnimSpeed = 0f;
    bool playingJumpAnimation = false;

    // Code that is run at every update frame and we have to do it in this manner because we are not inheriting from MonoBehaviour
    public override void RunAtUpdate()
    {
        base.RunAtUpdate();

        // If are capable of accessing our jump animation
        if (!overrideJumpAnim && !hitStopActive)
        {
            // and we are also falling...
            if (!isGrounded || isGrounded && curVerticalVelocity > 0f)
            {
                // then apply the appropriate jump animation frame based on our current vertical velocity
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
                    // in this case, we just landed so we will play our appropriate squash frame (but I haven't animated the squash frame yet)
                    anim.SetTrigger(Animator.StringToHash("Landed"));
                    playingJumpAnimation = false;
                    anim.speed = 1f;
                }

                playingJumpAnimation = false;
            }
        } 
        else if (!hitStopActive)
        {
            // make sure animation speed is returned to normal if we are currently not in a hitstop
            anim.speed = 1f;
        }
    }

    // Code that is essentially Start() and we have to do it in this manner because we are not inheriting from MonoBehaviour
    public override void RunAtStart()
    {
        base.RunAtStart();

        oldAnimSpeed = anim.speed;
    }

    // Code that is run at every 'x' seconds and we have to do it in this manner because we are not inheriting from MonoBehaviour
    public override void RunAtFixedUpdate()
    {
        base.RunAtFixedUpdate();
    }

    Vector2 oldGroundMovementDirection = Vector2.zero;
    
    // handles movement code and flips the character respectve to the direction it is moving (but can also be flipped do to mouse movements, so this function's
    // request to flip is not absoulte)

    // Also ensures that we play the running animation when performing movement
    public override void PerformMovement(InputAction.CallbackContext context)
    {
        movementDirection = playerInputHandler.groundMovementDirection;
        if (oldGroundMovementDirection == movementDirection && oldGroundMovementDirection != Vector2.zero) return;

        if (movementDirection.magnitude <= playerInputHandler.universalFixedMinDeadzone)
        {
            movementDirection = Vector2.zero;
            anim.SetBool(Animator.StringToHash("Running"), false);
        } else
        {
            anim.SetBool(Animator.StringToHash("Running"), true);
        }

        Vector2 targetVelocity;

        // calculates the target velocity as this is the velocity that this character to attempting to approach
        targetVelocity = new Vector2(movementSpeed * movementDirection.x, curVerticalVelocity);
        SetVelocity(targetVelocity);

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

    // Performs movement but this is called from an Ai's request and takes in the Ai's requested direction to move in
    // the direction that is taken in is typically normalized
    public override void PerformMovementAi(Vector2 direction)
    {
        if (holdMovementAiOverride) direction = Vector2.zero;

        // ensuring that our character is running
        if (direction == Vector2.zero)
        {
            anim.SetBool(Animator.StringToHash("Running"), false);
        }
        else
        {
            anim.SetBool(Animator.StringToHash("Running"), true);
        }

        Ai_movementDirection = direction;
        Vector2 targetVelocity;

        // calculates the target velocity as this is the velocity that this character to attempting to approach
        targetVelocity = new Vector2(movementSpeed * direction.x, curVerticalVelocity);
        SetVelocity(targetVelocity);

        // flips player based on requested direction of the Ai
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

    // Performs a jump based on whether we have pressed the jump key
    public override void PerformJump(InputAction.CallbackContext context)
    {
        // if we have not exceeded the maximum number of jumps
        if (jumpIndex <= maxJumps - 1)
        {
            // we use kinematic equations to determine our targetVerticalVelocity based on this characters specified jumpHeight and gravity
            float targetVerticalVelocity;
            isFalling = false;

            if (isGrounded) { 
                // performing a ground jump
                targetVerticalVelocity = Mathf.Sqrt(2 * gravity * gravityMultiplier * jumpHeight);
            } else
            {
                // performing an airborne jump
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

    // Performs a jump but on the request of the Ai, we will also take into consideration if the Ai requests to hold the jump key down
    // in order to form a higher jump
    public override void PerformJumpAi(bool holdSpaceKey =false, bool mustBeGrounded=false)
    {
        Ai_holdSpaceKey = holdSpaceKey; // will the Ai be holding the jump key down to form a higher jump
        if (mustBeGrounded && !isGrounded)
        {
            Ai_jumpIsQueued = true; // queues a jump if the jump requested stated that we must be grounded
            return;
        }

        // if it is possible to form a jump
        if (jumpIndex <= maxJumps - 1)
        {
            // we use kinematic equations to determine our targetVerticalVelocity based on this characters specified jumpHeight and gravity
            float targetVerticalVelocity;
            isFalling = false;

            if (isGrounded)
            {
                // performing a ground jump
                targetVerticalVelocity = Mathf.Sqrt(2 * gravity * gravityMultiplier * jumpHeight);
            }
            else
            {
                // performing an airborne jump
                float currentHeightReduction = Mathf.Pow(successiveJumpHeightReduction, airJumpsPerformed);
                targetVerticalVelocity = Mathf.Sqrt(2 * gravity * gravityMultiplier * airborneJumpHeight * currentHeightReduction);
                airJumpsPerformed++;
            }
            isGrounded = false;

            curVerticalVelocity = targetVerticalVelocity;
            rb.velocity = new Vector2(rb.velocity.x, curVerticalVelocity);
            jumpIndex++;
        } else { 
            Ai_jumpIsQueued = true; // queue a jump if it was impossible to form a jump during this request
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

        overrideJumpAnim = true; // overriding jump animation
    }

    // Resets the override on jump animations so the player can resume to its jump animation typically after an attack animation has been completed
    public void ResetOverrideJumpAnim()
    {
        // Clears one time IDs that basically represent the IDs of the characters who were hit during the duration of the entire attack animati
        HITBOXES_ClearOnetimeIDs();

        overrideJumpAnim = false;
        if (!isGrounded)
        {
            EnableShadowCrownDebug(); // toggles back on the shadow and crown of a character
        }
    }

    // Quick Debugging tool that allows me to disable the shadow and crown of a character
    public void DisableShadowCrownDebug()
    {
        shadow.SetActive(false);
        crown.SetActive(false);
    }

    // Quick Debugging tool that allows me to enable the shadow and crown of a character
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

        overrideJumpAnim = true; // overriding jump animation
    }

    public override void PerformBasicAbility(InputAction.CallbackContext context)
    {

    }

    public override void PerformUltimateAbility(InputAction.CallbackContext context)
    {

    }

    public override void PerformCrownThrow(InputAction.CallbackContext context)
    {
        base.PerformCrownThrow(context);

    }

    public override void OnPossessionLeave()
    {
        base.OnPossessionLeave();

        oldGroundMovementDirection = Vector2.zero;
    }
}
