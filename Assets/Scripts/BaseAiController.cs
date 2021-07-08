using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;

// help from https://arongranberg.com/astar/docs/custom_movement_script.html

public class BaseAiController : MonoBehaviour
{
    public Transform targetPosition;
    public BaseCharacterController baseCharacterController;
    Seeker seeker;
    bool calculatePathing = false;
    Path path;
    int currentWaypoint = 0;
    bool reachedEndOfPath = false;
    float nextWaypointDistance = 6;

    // Start is called before the first frame update
    void Start()
    {
        seeker = GetComponent<Seeker>();
        baseCharacterController = GetComponent<BaseCharacterController>();

        // baseCharacterController.PerformMovementAi(Vector2.left);
        InvokeRepeating("StartNewPath", 0f, 0.5f);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(targetPosition.position, 0.2f);
    }

    private void FixedUpdate()
    { 
        /* if (calculatePathing)
        {
            // seeker.StartPath(transform.position, targetPosition.position, OnPathComplete);
            seeker.StartPath(transform.position, targetPosition.position, OnPathComplete);
        } */
    }

    public void StartNewPath()
    {
        seeker.StartPath(transform.position, targetPosition.position);
    }

    // Update is called once per frame
    private void OnGUI()
    {
        /* calculatePathing = GUI.Toggle(new Rect(500, 20, 135, 25), calculatePathing, new GUIContent("Calculate Pathing")); */

        if (GUI.Button(new Rect(500, 20, 135, 50), new GUIContent("Calculate Pathing")))
        {
            seeker.StartPath(transform.position, targetPosition.position, OnPathComplete);
        }
    }

    public void OnPathComplete(Path p)
    {
        Debug.Log("Yay, we got a path back. Did it have an error? " + p.error);
        if(!p.error)
        {
            path = p;
            currentWaypoint = 0;
        }
    }

    public void Update()
    {
        if (path == null)
        {
            // No path to follow yet
            return;
        }

        reachedEndOfPath = false;
        float distanceToWaypoint;
        while (true)
        {
            // for maximum perforance, you can just check the squard distance
            distanceToWaypoint = Vector2.Distance(transform.position, path.vectorPath[currentWaypoint]);

            if(distanceToWaypoint < nextWaypointDistance)
            {
                // we have reached the way point
                if (currentWaypoint + 1 < path.vectorPath.Count)
                {
                    currentWaypoint++;
                } 
                else
                {
                    reachedEndOfPath = true;
                    break;
                }
            } 
            else
            {
                break;
            }
        }

        if (!reachedEndOfPath)
        {
            if ((baseCharacterController.Ai_movementDirection == Vector2.right || baseCharacterController.Ai_movementDirection == Vector2.zero)
                && (path.vectorPath[currentWaypoint] - transform.position).x < 0f)
            {
                baseCharacterController.PerformMovementAi(Vector2.left);
            }
            else if ((baseCharacterController.Ai_movementDirection == Vector2.left || baseCharacterController.Ai_movementDirection == Vector2.zero)
              && (path.vectorPath[currentWaypoint] - transform.position).x > 0f)
            {
                baseCharacterController.PerformMovementAi(Vector2.right);
            }
        }

        /* if (!reachedEndOfPath)
        {
            Vector2 dir = (path.vectorPath[currentWaypoint] - transform.position).normalized;
            Debug.DrawRay(new Vector3(0, 9), dir, Color.magenta);

            // Debug.Log(dir);
            baseCharacterController.PerformMovementAi(((path.vectorPath[currentWaypoint] - transform.position).x > 0f)? Vector2.right : Vector2.left);
            //baseCharacterController.PerformMovementAi(Vector2.left);
            Debug.Log(baseCharacterController.rb.velocity);
        } else
        {
            //baseCharacterController.PerformMovementAi(Vector2.zero);
        } */
    }

}


