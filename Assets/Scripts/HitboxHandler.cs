using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum knockbackDirection { 
    UP, UP_FORWARD60, UP_FORWARD45, UP_FORWARD30, 
    FORWARD, DOWN_FORWARD30, DOWN_FORWARD45, DOWN_FORWARD60,
    DOWN , UP_BACK60, UP_BACK45, UP_BACK30, 
    BACK, DOWN_BACK30, DOWN_BACK45, DOWN_BACK60
};

public enum clearDegree
{
    DONT_CLEAR, CLEAR_ME, CLEAR_ALL
}

public static class knockbackDirectionClass {
    public static Vector2 calculateKnockbackDirection(knockbackDirection direction, bool attackerFacingRight)
    {
        Vector2 temp;
        Vector2 directionCalculated;

        switch (direction)
        {
            case knockbackDirection.UP:
                directionCalculated = Vector2.up;
                return directionCalculated;
                 

            case knockbackDirection.UP_FORWARD60:
                temp = Vector2.up;
                if (attackerFacingRight)
                    temp = Vector3.RotateTowards(temp, Vector2.right, Mathf.Deg2Rad * 30, 0);
                else
                    temp = Vector3.RotateTowards(temp, Vector2.left, Mathf.Deg2Rad * 30, 0);

                directionCalculated = temp.normalized;
                return directionCalculated;
                

            case knockbackDirection.UP_FORWARD45:
                temp = Vector2.up + (Vector2.right * ((attackerFacingRight) ? 1 : -1));

                directionCalculated = temp.normalized;
                return directionCalculated;
                 

            case knockbackDirection.UP_FORWARD30:
                temp = Vector2.up;
                if (attackerFacingRight)
                    temp = Vector3.RotateTowards(temp, Vector2.right, Mathf.Deg2Rad * 60, 0);
                else
                    temp = Vector3.RotateTowards(temp, Vector2.left, Mathf.Deg2Rad * 60, 0);

                directionCalculated = temp.normalized;
                return directionCalculated;
                 

            case knockbackDirection.FORWARD:
                directionCalculated = (Vector2.right * ((attackerFacingRight) ? 1 : -1));
                return directionCalculated;
                 

            case knockbackDirection.DOWN_FORWARD30:
                temp = Vector2.down;
                if (attackerFacingRight)
                    temp = Vector3.RotateTowards(temp, Vector2.right, Mathf.Deg2Rad * 60, 0);
                else
                    temp = Vector3.RotateTowards(temp, Vector2.left, Mathf.Deg2Rad * 60, 0);

                directionCalculated = temp.normalized;
                return directionCalculated;
                 

            case knockbackDirection.DOWN_FORWARD45:
                temp = Vector2.down + (Vector2.right * ((attackerFacingRight) ? 1 : -1));
                directionCalculated = temp.normalized;
                return directionCalculated;
                 

            case knockbackDirection.DOWN_FORWARD60:
                temp = Vector2.down;
                if (attackerFacingRight)
                    temp = Vector3.RotateTowards(temp, Vector2.right, Mathf.Deg2Rad * 30, 0);
                else
                    temp = Vector3.RotateTowards(temp, Vector2.left, Mathf.Deg2Rad * 30, 0);

                directionCalculated = temp.normalized;
                return directionCalculated;
                 

            case knockbackDirection.DOWN:
                directionCalculated = Vector2.down;
                return directionCalculated;
                 

            case knockbackDirection.UP_BACK60:
                temp = Vector2.up;
                if (attackerFacingRight)
                    temp = Vector3.RotateTowards(temp, Vector2.left, Mathf.Deg2Rad * 30, 0);
                else
                    temp = Vector3.RotateTowards(temp, Vector2.right, Mathf.Deg2Rad * 30, 0);

                directionCalculated = temp.normalized;
                return directionCalculated;
                 

            case knockbackDirection.UP_BACK45:
                temp = Vector2.up + (Vector2.right * ((attackerFacingRight) ? -1 : 1));
                directionCalculated = temp.normalized;
                return directionCalculated;
                 

            case knockbackDirection.UP_BACK30:
                temp = Vector2.up;
                if (attackerFacingRight)
                    temp = Vector3.RotateTowards(temp, Vector2.left, Mathf.Deg2Rad * 60, 0);
                else
                    temp = Vector3.RotateTowards(temp, Vector2.right, Mathf.Deg2Rad * 60, 0);

                directionCalculated = temp.normalized;
                return directionCalculated;
                 

            case knockbackDirection.BACK:
                directionCalculated = (Vector2.right * ((attackerFacingRight) ? -1 : 1));
                return directionCalculated;
                 

            case knockbackDirection.DOWN_BACK30:
                temp = Vector2.down;
                if (attackerFacingRight)
                    temp = Vector3.RotateTowards(temp, Vector2.left, Mathf.Deg2Rad * 60, 0);
                else
                    temp = Vector3.RotateTowards(temp, Vector2.right, Mathf.Deg2Rad * 60, 0);

                directionCalculated = temp.normalized;
                return directionCalculated;
                 

            case knockbackDirection.DOWN_BACK45:
                temp = Vector2.down + (Vector2.right * ((attackerFacingRight) ? -1 : 1));

                directionCalculated = temp.normalized;
                return directionCalculated;
                 

            case knockbackDirection.DOWN_BACK60:
                temp = Vector2.down;
                if (attackerFacingRight)
                    temp = Vector3.RotateTowards(temp, Vector2.left, Mathf.Deg2Rad * 30, 0);
                else
                    temp = Vector3.RotateTowards(temp, Vector2.right, Mathf.Deg2Rad * 30, 0);

                directionCalculated = temp.normalized;
                return directionCalculated;
                 
        }

        return Vector2.zero;
    }
}

[RequireComponent(typeof(CapsuleCollider2D))]
public class HitboxHandler : MonoBehaviour
{
    public BaseCharacterController baseCharacterController;
    public CapsuleCollider2D hitbox;
    public List<BaseCharacterController> recentlyHitCharacters;
    public List<int> recentlyHitCharactersID; // its not the type of id you think, and its works differently, its more so an id to their
    // background velocities

    //public List<float> recentlyHitCharacterTimestamps;
    //public static float recentlyHitDelay = 0.1f;

    public static List<HitboxHandler> allHitboxHandlers;
    public List<HitboxHandler> myHitboxHandlers;
    public knockbackDirection currentKnockbackDirection;

    public int damagePointer = 0;
    public bool linkHitboxList = false;

    public clearDegree clearAmount = clearDegree.DONT_CLEAR;

    bool canDamage = true;
    bool canDrawHitboxes = true;
    //bool canPerformOneTimeAction = true;

    private void OnEnable()
    {
        if (allHitboxHandlers == null) allHitboxHandlers = new List<HitboxHandler>();

        allHitboxHandlers.Add(this);

        if (baseCharacterController.hitboxes == null) baseCharacterController.hitboxes = new List<HitboxHandler>();
        baseCharacterController.hitboxes.Add(this);

        canDrawHitboxes = true;
        canDamage = true;
    }

    private void OnDisable()
    {
        allHitboxHandlers.Remove(this);
        baseCharacterController.hitboxes.Remove(this);
        canDrawHitboxes = false;
        canDamage = false;
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
    /*public void Update()
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
    */

    void HandleTrigger(Collider2D collision)
    {
        // if its an interact w/ Dmg, we shouldn't put it on a timestamp, and instead wait if a new attack is used
        if(collision.tag == "Hurtbox" || collision.tag == "Interact w/ Dmg")
        {

            HurtboxHandler hurtboxHandler = collision.gameObject.GetComponent<HurtboxHandler>();
            BaseCharacterController theirCharacterController = hurtboxHandler.baseCharacterController;

            //the "this." part is just to emphasis that I am checking wether their character controller is equal to my character controller
            foreach(BaseCharacterController baseCharacterController in recentlyHitCharacters)
            {
                //Debug.LogWarning(baseCharacterController + " & " + theirCharacterController);
                if (baseCharacterController == theirCharacterController) return;
            }
            //if (theirCharacterController == this.baseCharacterController) return;

            theirCharacterController.healthHandler.UpdateHealth((int)(baseCharacterController.damageTiers[damagePointer] 
                * baseCharacterController.damageMultiplier * -1));

            Debug.LogError("RIN DID " + baseCharacterController.damageTiers[damagePointer] + " OF DAMAGE");
            //Debug.LogWarning("RIN HIT STOP @ " + baseCharacterController.hitStopTiers[damagePointer]);
            

            // apply this to all other hitboxes parented to the character
            if (linkHitboxList)
            {
                foreach (HitboxHandler hitboxHandler in myHitboxHandlers)
                {
                    hitboxHandler.recentlyHitCharacters.Add(theirCharacterController);
                    //hitboxHandler.recentlyHitCharacterTimestamps.Add(Time.time + baseCharacterController.hitStopTiers[damagePointer] + recentlyHitDelay + Time.deltaTime);
                }
            } 
            else
            {
                recentlyHitCharacters.Add(theirCharacterController);
            }

            // Applying Hitstop
            theirCharacterController.HitStop(baseCharacterController.hitStopTiers[damagePointer] + Time.deltaTime, baseCharacterController.damageTiers[damagePointer]);
            baseCharacterController.HitStop(baseCharacterController.hitStopTiers[damagePointer] + Time.deltaTime, baseCharacterController.damageTiers[damagePointer]);

            // Applying Knockback
            if (theirCharacterController.canRecvieceKnockback)
            {
                //Vector2 direction = theirCharacterController.gameObject.transform.position - transform.position;
                bool attackerFacingRight = baseCharacterController.facingRight;

                float distance = baseCharacterController.knockbackDistanceTiers[damagePointer];
                distance *= (theirCharacterController.isGrounded) ? 1 : baseCharacterController.knockbackAirborneDistanceMultiplier;

                theirCharacterController.ApplyKnockback(currentKnockbackDirection, attackerFacingRight, distance,
                    baseCharacterController.knockbackDurationTiers[damagePointer]);

                // Implement knockback dragging later idek
                foreach (int id in recentlyHitCharactersID)
                {
                    if (theirCharacterController.backgroundVelocityID.Contains(id))
                    {
                        theirCharacterController.RemoveBackgroundVelocityAt(id);
                    }
                }

                recentlyHitCharactersID.Add(theirCharacterController.SetNewBackgroundVelocityGravityIncorporated_Velcoity(baseCharacterController.previousRbVelocity / 3f,
                            baseCharacterController.knockbackDurationTiers[damagePointer] - Time.deltaTime * 2));
                

                // add a cap to the magnitude of background velocity
            }
        }
    }

    public void ClearOnetimeIDs()
    {
        recentlyHitCharactersID.Clear();
        ClearIDs(clearAmount);
    }

    public void ClearIDs(clearDegree clear)
    {
        switch (clear)
        {
            case clearDegree.DONT_CLEAR:
                break;

            case clearDegree.CLEAR_ME:
                recentlyHitCharacters.Clear();
                break;

            case clearDegree.CLEAR_ALL:
                foreach (HitboxHandler hitbox in myHitboxHandlers)
                {
                    hitbox.recentlyHitCharacters.Clear();
                }
                break;
        }
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (baseCharacterController.showHitboxes && canDrawHitboxes && hitbox.enabled)
        {
            Vector2 position = hitbox.bounds.center;
            Vector2 directionCalculated = Vector2.zero;
            Color color = Color.cyan;
            bool attackerFacingRight = baseCharacterController.facingRight;
            // calculate radius and height
            Helper.DrawCapsule.DrawWireCapsule(position, transform.rotation, hitbox.size.x / 2, hitbox.size.y, Color.red);

            Helper.DrawArrow.ForGizmo(position, knockbackDirectionClass.calculateKnockbackDirection(currentKnockbackDirection, attackerFacingRight), color);
            
        }
    }
#endif

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if(canDamage)
            HandleTrigger(collision);
    }
    private void OnTriggerStay2D(Collider2D collision)
    {
        if (canDamage)
            HandleTrigger(collision);
    }
    /*private void OnTriggerExit2D(Collider2D collision)
    {
        if (canDamage)
            HandleTrigger(collision);
    }*/
}

