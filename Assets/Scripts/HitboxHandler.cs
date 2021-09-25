using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/* Purpose of HitboxHandler is to handle our hitboxes when performing attacks and to deal damage towards other
 * characters if the hitbox hits their hurt box
 */

public enum knockbackDirection { 
    UP, UP_FORWARD60, UP_FORWARD45, UP_FORWARD30, 
    FORWARD, DOWN_FORWARD30, DOWN_FORWARD45, DOWN_FORWARD60,
    DOWN , UP_BACK60, UP_BACK45, UP_BACK30, 
    BACK, DOWN_BACK30, DOWN_BACK45, DOWN_BACK60
};

// essentially, when I hitbox hits a character, we store that character into a list of recently hit characters and in some instances
// we will only store that list for one frame of animation and clear it by calling a clear command. Clear degree just states the degree
// at which we clear our recently hit characers list. DONT_CLEAR states that we will not clear the recently hit characters list for this
// hitbox. CLEAR_ME states we will clear the recently hit characters list for this hitbox. CLEAR_ALL states that we will clear the recently
// hit characters list for all hitboxes that is stored for the character
public enum clearDegree
{
    DONT_CLEAR, CLEAR_ME, CLEAR_ALL
}

// stored this knockbackDirectionClass seperate from HitboxHandler since this function is very useful and may be used outside
// of the domain of the HitboxHandler.cs

public static class knockbackDirectionClass {

    // calculating knockback direction to give to other character that has just been struck by this character
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

        // else, no knockback direction could be calculated, so we just return nothing
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
    // background velocities that we have just applied to them in the form of a knockback

    public static List<HitboxHandler> allHitboxHandlers; // all hitboxes in our scene
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

    // Handles trigger box processing in which our hitbox collides with another collider
    void HandleTrigger(Collider2D collision)
    {
        // if its an interact w/ Dmg, we shouldn't put it on a timestamp, and instead wait if a new attack is used
        // however, not objects utilise "Interact w/ Dmg" and may be depricated if there is no use...

        if(collision.tag == "Hurtbox" || collision.tag == "Interact w/ Dmg")
        {

            HurtboxHandler hurtboxHandler = collision.gameObject.GetComponent<HurtboxHandler>();
            BaseCharacterController theirCharacterController = hurtboxHandler.baseCharacterController;

            foreach(BaseCharacterController baseCharacterController in recentlyHitCharacters)
            {
                if (baseCharacterController == theirCharacterController) return;
            }

            theirCharacterController.healthHandler.UpdateHealth((int)(baseCharacterController.damageTiers[damagePointer] 
                * baseCharacterController.damageMultiplier * -1));

            Debug.LogError("RIN DID " + baseCharacterController.damageTiers[damagePointer] + " OF DAMAGE");
            

            // updating recentlyHitCharacters list to all other hitboxes parented to the character if linkHitboxList is true
            if (linkHitboxList)
            {
                foreach (HitboxHandler hitboxHandler in myHitboxHandlers)
                {
                    hitboxHandler.recentlyHitCharacters.Add(theirCharacterController);
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
                bool attackerFacingRight = baseCharacterController.facingRight;

                float distance = baseCharacterController.knockbackDistanceTiers[damagePointer];
                distance *= (theirCharacterController.isGrounded) ? 1 : baseCharacterController.knockbackAirborneDistanceMultiplier;

                theirCharacterController.ApplyKnockback(currentKnockbackDirection, attackerFacingRight, distance,
                    baseCharacterController.knockbackDurationTiers[damagePointer]);

                // Implement knockback dragging that essentially allows to attack to influence the vector of knockback applied to the other character, based on their movement dureing 
                // the attack
                foreach (int id in recentlyHitCharactersID)
                {
                    if (theirCharacterController.backgroundVelocityID.Contains(id))
                    {
                        theirCharacterController.RemoveBackgroundVelocityAt(id);
                    }
                }

                recentlyHitCharactersID.Add(theirCharacterController.SetNewBackgroundVelocityGravityIncorporated_Velcoity(baseCharacterController.previousRbVelocity / 3f,
                            baseCharacterController.knockbackDurationTiers[damagePointer] - Time.deltaTime * 2));
                
            }
        }
    }

    // Clears one time IDs that basically represent the IDs of the characters who were hit during the duration of the entire attack animation
    public void ClearOnetimeIDs()
    {
        recentlyHitCharactersID.Clear();
        ClearIDs(clearAmount);
    }

    // Clears IDs that basically represent the IDs of the characters who were hit this frame. However, its clear degree depends on hitbox.clearAmount
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

#if UNITY_EDITOR // we have to make sure to not compile this code if we are building our code for a release since this can cause some compiler errors
    private void OnDrawGizmos()
    {
        // draws hitboxes in editor if debugging process is enabled
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
}

