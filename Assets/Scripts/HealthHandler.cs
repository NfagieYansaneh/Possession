using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class HealthHandler : MonoBehaviour
{
    private Rect buttonPos;
    public int spriteVersion = 0;
    public SpriteRenderer spriteR;
    public Sprite[] sprites;

    public int currentHealth = 0; // max is 100
    public int maxHealth = 100;

    public int injuredThreshold;
    public int exposedThreshold;

    void Start()
    {
        buttonPos = new Rect(10.0f, 10.0f, 150.0f, 50.0f);
        spriteR = gameObject.GetComponent<SpriteRenderer>();
    }

    void OnGUI()
    {
        if (GUI.Button(buttonPos, "Choose next sprite"))
        {
            spriteVersion += 1;
            if (spriteVersion > 2)
                spriteVersion = 0;
            spriteR.sprite = sprites[spriteVersion];
        }
    }
}
