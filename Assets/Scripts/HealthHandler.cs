using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum healthStates { HEALTH_NORMAL, HEALTH_INJURED, HEALTH_EXPOSED };
public enum blinkerStates { BLINKER_ON, BLINKER1_OFF, BLINKER_BURST_ON, BLINKER2_OFF }

public class HealthHandler : MonoBehaviour
{
    private Rect buttonPos;
    public SpriteRenderer spriteRenderer;
    public Sprite[] skullSprites;

    public int currentHealth = 100; // max is 100
    public int maxHealth = 100;
    public int healthStatus = (int)healthStates.HEALTH_NORMAL;

    public int injuredThreshold = 40;
    public int exposedThreshold = 15;

    public bool checkHealthEveryFrame = false;
    public bool forceDisplayHealth = false;

    public static float universalBlinkerOnTiming = 0.2f;
    public static float universalBlinkerOffTiming = 0.1f;
    static IEnumerator blinkerCoroutine;
    static int currentBlinkerState = (int)blinkerStates.BLINKER_ON;
    static bool currentBlinkerRenderEnable = true;
    static bool coroutineRunning = false;

    void Start()
    {
        buttonPos = new Rect(20.0f, 20.0f, 150.0f, 40.0f);

        // get rid of magic value
        if (!coroutineRunning)
        {
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
        coroutineRunning = true;
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
