using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DebugGUI : MonoBehaviour
{
    public bool globalShowHitboxes = false;
    public bool globalShowHealth = false;

    private void Start()
    {

        foreach (BaseCharacterController baseCharacterController in BaseCharacterController.baseCharacterControllers)
        {
            if (globalShowHitboxes)
                baseCharacterController.showHitboxes = true;
            if (globalShowHealth)
                baseCharacterController.healthHandler.forceDisplayHealth = true;
        }
        
    }

    private void OnGUI()
    {
        if (GUI.Button(new Rect(20.0f, 20.0f, 150.0f, 40.0f), "Next Health Stage"))
        {
            foreach (BaseCharacterController baseCharacterController in BaseCharacterController.baseCharacterControllers)
            {
                if (baseCharacterController.healthHandler != null)
                {
                    if (baseCharacterController.healthHandler.healthStatus == (int)healthStates.HEALTH_EXPOSED)
                        baseCharacterController.healthHandler.healthStatus = (int)healthStates.HEALTH_NORMAL;
                    else baseCharacterController.healthHandler.healthStatus += 1;

                    switch (baseCharacterController.healthHandler.healthStatus)
                    {
                        case (int)healthStates.HEALTH_NORMAL:
                            baseCharacterController.healthHandler.currentHealth = baseCharacterController.healthHandler.maxHealth;
                            break;

                        case (int)healthStates.HEALTH_INJURED:
                            baseCharacterController.healthHandler.currentHealth = baseCharacterController.healthHandler.injuredThreshold;
                            break;

                        case (int)healthStates.HEALTH_EXPOSED:
                            baseCharacterController.healthHandler.currentHealth = baseCharacterController.healthHandler.exposedThreshold;
                            break;
                    }

                    baseCharacterController.healthHandler.UpdateHealth(0);
                }
            }
        }

        bool toggleRecv = GUI.Toggle(new Rect(180f, 20f, 150f, 18f), globalShowHealth, new GUIContent("Force Display Health"));
        if (globalShowHealth != toggleRecv)
        {
            globalShowHealth = toggleRecv;
            foreach (BaseCharacterController baseCharacterController in BaseCharacterController.baseCharacterControllers)
            {
                if (baseCharacterController.healthHandler != null)
                {
                    baseCharacterController.healthHandler.forceDisplayHealth = toggleRecv;
                    baseCharacterController.healthHandler.UpdateGUI();
                }
            }
        }

        if (GUI.Button(new Rect(20f, 70f, 150f, 40f), new GUIContent("Replenish Health")))
        {
            foreach (BaseCharacterController baseCharacterController in BaseCharacterController.baseCharacterControllers)
            {
                if (baseCharacterController.healthHandler != null)
                {
                    baseCharacterController.healthHandler.ReplenishHealth();
                }
            }
        }

        bool toggleARecv = GUI.Toggle(new Rect(180f, 40f, 150f, 15f), globalShowHitboxes, new GUIContent("Show Hitboxes"));
        if (globalShowHitboxes != toggleARecv)
        {
            globalShowHitboxes = toggleARecv;

            foreach (BaseCharacterController baseCharacterController in BaseCharacterController.baseCharacterControllers)
            {
                if (globalShowHitboxes == true)
                    baseCharacterController.showHitboxes = true;
                else
                    baseCharacterController.showHitboxes = false;
            }
        }
    }
}
