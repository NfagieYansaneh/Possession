using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CapsuleCollider2D))]
public class HitboxHandler : MonoBehaviour
{
    public BaseCharacterController baseCharacterController;
    public CapsuleCollider2D hitbox;
    public List<BaseCharacterController> recentlyHitCharacters;
    public List<float> recentlyHitCharacterTimestamps;
    public static float recentlyHitDelay = 0.1f;

    private void Start()
    {
        hitbox = GetComponent<CapsuleCollider2D>();
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
            recentlyHitCharacters.Add(theirCharacterController);
            recentlyHitCharacterTimestamps.Add(Time.time + baseCharacterController.hitStopTiers[baseCharacterController.damagePointer] + recentlyHitDelay + Time.deltaTime);

            theirCharacterController.HitStop(baseCharacterController.hitStopTiers[baseCharacterController.damagePointer] + Time.deltaTime);
            baseCharacterController.HitStop(baseCharacterController.hitStopTiers[baseCharacterController.damagePointer] + Time.deltaTime);
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
