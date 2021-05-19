using ExitGames.Client.Photon.StructWrapping;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

public class PlayerInputHandler : MonoBehaviour
{
    // Variable stores an instance of the InputMaster, which holds all of our input actions for input processing
    InputMaster input;

    public BaseCharacterController possessedCharacter;

    private void Awake()
    {
        input = new InputMaster();

        // when movement keys are performed, call "OnMovementPerformed" function
        input.Player.Movement.started += OnMovementPerformed;
        input.Player.Movement.performed += OnMovementPerformed;
        input.Player.Movement.canceled += OnMovementPerformed;

        input.Player.Jump.performed+= OnJumpPerformed; // when the jump keys are performed, call "OnJumpPerformed" function

        input.Player.Dodge.performed += OnDodgePerformed; // when the dodge keys are performed, call "OnDodgePerformed" function

        input.Player.LightAttack.performed += OnLightAttackPerformed;

        input.Player.HeavyAttack.performed += OnHeavyAttackPerformed;

        input.Player.BasicAbility.performed += OnBasicAbilityPerformed;

        input.Player.UltimateAbility.performed += OnUltimateAbilityPerformed;

        input.Player.CrownThrow.performed += OnCrownThrowPerformed;

    }

    void OnMovementPerformed(InputAction.CallbackContext context)
    {
        if(possessedCharacter != null)
        {
            possessedCharacter.PerformMovement(context);
        }
    }

    void OnJumpPerformed(InputAction.CallbackContext context)
    {
        if (possessedCharacter != null)
        {
            possessedCharacter.PerformJump(context);
        }
    }

    void OnDodgePerformed(InputAction.CallbackContext context)
    {
        if (possessedCharacter != null)
        {
            possessedCharacter.PerformDodge(context);
        }
    }

    void OnLightAttackPerformed(InputAction.CallbackContext context)
    {
        if (possessedCharacter != null)
        {
            possessedCharacter.PerformLightAttack(context);
        }
    }

    void OnHeavyAttackPerformed(InputAction.CallbackContext context)
    {
        if (possessedCharacter != null)
        {
            possessedCharacter.PerformHeavyAttack(context);
        }
    }

    void OnBasicAbilityPerformed(InputAction.CallbackContext context)
    {
        if (possessedCharacter != null)
        {
            possessedCharacter.PerformBasicAbility(context);
        }
    }

    void OnUltimateAbilityPerformed(InputAction.CallbackContext context)
    {
        if (possessedCharacter != null)
        {
            possessedCharacter.PerformUltimateAbility(context);
        }
    }

    void OnCrownThrowPerformed(InputAction.CallbackContext context)
    {
        if (possessedCharacter != null)
        {
            possessedCharacter.PerformCrownThrow(context);
        }
    }

    private void OnEnable()
    {
        input.Player.Enable(); // enables the ability to process and read inputs
    }

    private void OnDisable()
    {
        input.Player.Disable(); // disables the ability to process and read inputs
    }
}
