using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using System;

[CustomEditor(typeof(HealthHandler))]
[CanEditMultipleObjects]
public class CustomHealthHandlerEditor : Editor
{
    SerializedProperty currentHealth;
    SerializedProperty maxHealth;
    SerializedProperty injuredThreshold;
    SerializedProperty exposedThreshold;

    float minSlider = 0;
    float maxSlider = 1;

    private void OnEnable()
    {
        currentHealth       = serializedObject.FindProperty("currentHealth");
        maxHealth           = serializedObject.FindProperty("maxHealth");
        injuredThreshold    = serializedObject.FindProperty("injuredThreshold");
        exposedThreshold    = serializedObject.FindProperty("exposedThreshold");
    }

    public override void OnInspectorGUI()
    {
        // base.OnInspectorGUI();
        serializedObject.Update();

        if (!maxHealth.hasMultipleDifferentValues)
        EditorGUILayout.PropertyField(maxHealth, new GUIContent("Max Health: "));

        if (!currentHealth.hasMultipleDifferentValues)
        EditorGUILayout.IntSlider(currentHealth, 0, maxHealth.intValue, new GUIContent("Current Health: "));

        if (!injuredThreshold.hasMultipleDifferentValues && !exposedThreshold.hasMultipleDifferentValues)
        {
            EditorGUILayout.MinMaxSlider("Health Thresholds: ", ref minSlider, ref maxSlider, 0f, 100f);
            injuredThreshold.intValue = (int)maxSlider;
            exposedThreshold.intValue = (int)minSlider;
        }

        EditorGUILayout.PropertyField(injuredThreshold, new GUIContent("Injured Threshold: "));
        EditorGUILayout.PropertyField(exposedThreshold, new GUIContent("Exposed Threshold: "));
        maxSlider = injuredThreshold.intValue;
        minSlider = exposedThreshold.intValue;

        ProgressBar(((float)currentHealth.intValue / (float)maxHealth.intValue), "Current Health / Max Health");

        if (currentHealth.intValue > injuredThreshold.intValue && !currentHealth.hasMultipleDifferentValues) {
            ProgressBar(1f - ((float)(currentHealth.intValue - injuredThreshold.intValue) / (float)(maxHealth.intValue - injuredThreshold.intValue)),
                "(i) Normal Status -> Injured Status");
        } 
        else if (currentHealth.intValue > exposedThreshold.intValue && !currentHealth.hasMultipleDifferentValues)
        {
            ProgressBar(1f - ((float)(currentHealth.intValue - exposedThreshold.intValue) / (float)(injuredThreshold.intValue - exposedThreshold.intValue)),
                "(ii) Injured Status -> Exposed Status");
        } 
        else if (!currentHealth.hasMultipleDifferentValues)
        {
            ProgressBar(1f - ((float)currentHealth.intValue / 
                (float)(exposedThreshold.intValue)), "(iii) Exposed Status");
        }

        //EditorGUILayout.MinMaxSlider();
        // ProgressBar();
        serializedObject.ApplyModifiedProperties();
    }

    void ProgressBar(float value, string label)
    {
        // Get a rect for the progress bar using the same margins as a textfield:
        Rect rect = GUILayoutUtility.GetRect(18, 18, "TextField");
        EditorGUI.ProgressBar(rect, value, label);
        EditorGUILayout.Space();
    }

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
