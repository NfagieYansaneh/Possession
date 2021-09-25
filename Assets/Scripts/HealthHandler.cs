using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/* Purpose of HealthHandler.cs is to handle and manage a character's health
 * and health status
 */

public enum healthStates { HEALTH_NORMAL, HEALTH_INJURED, HEALTH_EXPOSED }; // health_exposed means that the character is critically low

// hardcoded pattern that will be used to cause the skull icon (that represents current player health)
// to blink in a defined pattern
public enum blinkerStates { BLINKER_ON, BLINKER1_OFF, BLINKER_BURST_ON, BLINKER2_OFF } 

public class HealthHandler : MonoBehaviour
{
    // private Rect buttonPos;
    public SpriteRenderer spriteRenderer;
    public Sprite[] skullSprites;

    public int currentHealth = 100; // max is 100
    public int maxHealth = 100;
    public int healthStatus = (int)healthStates.HEALTH_NORMAL;

    public int injuredThreshold = 40;
    public int exposedThreshold = 15;

    public bool checkHealthEveryFrame = false;
    public bool forceDisplayHealth = false;

    /* blinker variables used let the skull (the health icon) flash in a specific manner to bring player's attention towards it */

    public static float universalBlinkerOnTiming = 0.2f;
    public static float universalBlinkerOffTiming = 0.1f;
    static IEnumerator blinkerCoroutine;
    static int currentBlinkerState = (int)blinkerStates.BLINKER_ON;
    static bool currentBlinkerRenderEnable = true;
    static bool coroutineRunning = false;

    void Start()
    {
        if (!coroutineRunning)
        {
            // starts up a coroutine that will flash the health icons of all skulls (health icons) in sync when they are at the injured status
            blinkerCoroutine = injuredStatusBlinker(0.5f, 0.1f, 0.1f);
            StartCoroutine(blinkerCoroutine);
        }
    }

    private void Update()
    {
        if (checkHealthEveryFrame)
            UpdateHealth(0);
    }

    public void ReplenishHealth()
    {
        currentHealth = maxHealth;
        healthStatus = (int)healthStates.HEALTH_NORMAL;
    }

    int previousHealthStatus = (int)healthStates.HEALTH_EXPOSED;
    public void UpdateHealth(int delta)
    {
        if (delta != 0)
        {
            currentHealth += delta;
            currentHealth = Mathf.Clamp(currentHealth, 0, maxHealth);
        }

        if (currentHealth > injuredThreshold) healthStatus = (int)healthStates.HEALTH_NORMAL;
        else if (currentHealth > exposedThreshold) healthStatus = (int)healthStates.HEALTH_INJURED;
        else healthStatus = (int)healthStates.HEALTH_EXPOSED;

        if (previousHealthStatus != healthStatus)
        {
            UpdateGUI();
            previousHealthStatus = healthStatus;
        }
    }

    public void UpdateGUI()
    {
        switch (healthStatus)
        {
            case (int)healthStates.HEALTH_NORMAL:
                spriteRenderer.sprite = skullSprites[0];
                if (forceDisplayHealth) spriteRenderer.enabled = true;
                else spriteRenderer.enabled = false;
                break;

            case (int)healthStates.HEALTH_INJURED:
                spriteRenderer.sprite = skullSprites[1];
                spriteRenderer.enabled = true;
                break;

            case (int)healthStates.HEALTH_EXPOSED:
                spriteRenderer.sprite = skullSprites[2];
                spriteRenderer.enabled = true;
                break;
        }
    }

    static IEnumerator injuredStatusBlinker(float blinkerOnTiming, float blinkerOnBurstTiming, float blinkerOffTiming)
    {
        // [][][]||[]||
        // squares represent when the skull icon is visible []
        // and double lines represent when the skull icon is not visible ||
        // just quickly showing how the skull will flash

        coroutineRunning = true;

        // performing blinking pattern when our player is at the injured status
        while (true)
        {
            switch (currentBlinkerState)
            {
                case (int)blinkerStates.BLINKER_ON:
                    yield return new WaitForSeconds(blinkerOffTiming);
                    currentBlinkerRenderEnable = true;
                    currentBlinkerState++;
                    break;

                case (int)blinkerStates.BLINKER1_OFF:
                    yield return new WaitForSeconds(blinkerOnTiming);
                    currentBlinkerRenderEnable = false;
                    currentBlinkerState++;
                    break;

                case (int)blinkerStates.BLINKER_BURST_ON:
                    yield return new WaitForSeconds(blinkerOffTiming);
                    currentBlinkerRenderEnable = true;
                    currentBlinkerState++;
                    break;

                case (int)blinkerStates.BLINKER2_OFF:
                    yield return new WaitForSeconds(blinkerOnBurstTiming);
                    currentBlinkerRenderEnable = false;
                    currentBlinkerState = 0;
                    break;
            }

            // makes sure that all blinking with occur in sync with other characters if they are also at the injured status
            foreach (BaseCharacterController baseCharacterController in BaseCharacterController.baseCharacterControllers)
            {
                if(baseCharacterController.healthHandler != null && baseCharacterController.healthHandler.healthStatus == (int)healthStates.HEALTH_INJURED)
                {
                    baseCharacterController.healthHandler.spriteRenderer.enabled = currentBlinkerRenderEnable;
                }
            }
        }
    }
}
