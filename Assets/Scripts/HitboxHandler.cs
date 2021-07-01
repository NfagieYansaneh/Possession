using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum knockbackDirection { 
    UP, UP_FORWARD60, UP_FORWARD45, UP_FORWARD30, 
    FORWARD, DOWN_FORWARD30, DOWN_FORWARD45, DOWN_FORWARD60,
    DOWN , UP_BACK60, UP_BACK45, UP_BACK30, 
    BACK, DOWN_BACK30, DOWN_BACK45, DOWN_BACK60
};

[RequireComponent(typeof(CapsuleCollider2D))]
public class HitboxHandler : MonoBehaviour
{
    public BaseCharacterController baseCharacterController;
    public CapsuleCollider2D hitbox;
    public List<BaseCharacterController> recentlyHitCharacters;
    public List<float> recentlyHitCharacterTimestamps;
    public static float recentlyHitDelay = 0.1f;

    public static List<HitboxHandler> allHitboxHandlers;
    public List<HitboxHandler> myHitboxHandlers;
    public knockbackDirection currentKnockbackDirection;

    private void OnEnable()
    {
        if (allHitboxHandlers == null) allHitboxHandlers = new List<HitboxHandler>();

        allHitboxHandlers.Add(this);
    }

    private void OnDisable()
    {
        allHitboxHandlers.Remove(this);
    }

    private void Start()
    {
        hitbox = GetComponent<CapsuleCollider2D>();
        foreach(HitboxHandler hitBoxHandler in allHitboxHandlers)
        {
            if(hitBoxHandler.baseCharacterController == baseCharacterController)
            {
                myHitboxHandlers.Add(hitBoxHandler);
            }
        }
    }

    List<int> indexesMarked = new List<int>(0);
    public void Update()
    {
        int index = 0;
        foreach (BaseCharacterController hitCharacters in recentlyHitCharacters)
        {
            if(recentlyHitCharacterTimestamps[index] < Time.time)
                indexesMarked.Add(index);

            index++;
        }

        int numOfItemsRemoved = 0;
        foreach (int i in indexesMarked)
        {
            if(recentlyHitCharacters.Count >= (i - numOfItemsRemoved) && recentlyHitCharacterTimestamps.Count >= (i - numOfItemsRemoved))
            recentlyHitCharacters.RemoveAt(i - numOfItemsRemoved);
            recentlyHitCharacterTimestamps.RemoveAt(i - numOfItemsRemoved);

            numOfItemsRemoved++;
        }

        if (indexesMarked.Count > 0)
            indexesMarked.Clear();
    }

    void HandleTrigger(Collider2D collision)
    {
        // if its an interact w/ Dmg, we shouldn't put it on a timestamp, and instead wait if a new attack is used
        if( collision.tag == "Hurtbox" || collision.tag == "Interact w/ Dmg")
        {
            HurtboxHandler hurtboxHandler = collision.gameObject.GetComponent<HurtboxHandler>();
            BaseCharacterController theirCharacterController = hurtboxHandler.baseCharacterController;

            //the "this." part is just to emphasis that I am checking wether their character controller is equal to my character controller
            if (recentlyHitCharacters.Contains(theirCharacterController) || theirCharacterController == this.baseCharacterController) return;

            theirCharacterController.healthHandler.UpdateHealth((int)(baseCharacterController.damageTiers[baseCharacterController.damagePointer] 
                * baseCharacterController.damageMultiplier * -1));

            Debug.LogError("RIN DID " + baseCharacterController.damageTiers[baseCharacterController.damagePointer] + " OF DAMAGE");
            Debug.LogWarning("RIN HIT STOP @ " + baseCharacterController.hitStopTiers[baseCharacterController.damagePointer]);

            // apply this to all other hitboxes parented to the character
            foreach (HitboxHandler hitboxHandler in myHitboxHandlers)
            {
                hitboxHandler.recentlyHitCharacters.Add(theirCharacterController);
                hitboxHandler.recentlyHitCharacterTimestamps.Add(Time.time + baseCharacterController.hitStopTiers[baseCharacterController.damagePointer] + recentlyHitDelay + Time.deltaTime);
            }

            // Applying Hitstop
            theirCharacterController.HitStop(baseCharacterController.hitStopTiers[baseCharacterController.damagePointer] + Time.deltaTime);
            baseCharacterController.HitStop(baseCharacterController.hitStopTiers[baseCharacterController.damagePointer] + Time.deltaTime);

            // Applying Knockback
            if (theirCharacterController.canRecvieceKnockback)
            {
                Vector2 direction = theirCharacterController.gameObject.transform.position - transform.position;
                bool attackerFacingRight = baseCharacterController.facingRight;

                float magnitude = baseCharacterController.knockbackMagnitudeTiers[baseCharacterController.damagePointer];
                magnitude *= (theirCharacterController.isGrounded) ? 1 : baseCharacterController.knockbackAirborneMagnitudeMultiplier;

                theirCharacterController.ApplyKnockback(currentKnockbackDirection, attackerFacingRight, magnitude,
                    baseCharacterController.knockbackDurationTiers[baseCharacterController.damagePointer]);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        HandleTrigger(collision);
    }
    private void OnTriggerStay2D(Collider2D collision)
    {
        HandleTrigger(collision);
    }
    private void OnTriggerExit2D(Collider2D collision)
    {
        HandleTrigger(collision);
    }
}
