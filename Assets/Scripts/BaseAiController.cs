using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;
using UnityEngine.Events;

// help from https://arongranberg.com/astar/docs/custom_movement_script.html

public enum typeofWaypoint { RUN, JUMP, DODGE, NEUTRAL_DODGE };

public class BaseAiController : MonoBehaviour
{
    public Transform targetPosition;
    public BaseCharacterController baseCharacterController;
    Seeker seeker;
    bool calculatePathing = false;
    Path path;
    int currentWaypoint = 0;

    bool reachedEndOfPath = false;
    bool pathComplete = false;

    float nextWaypointDistance = 2;
    float pathCompleteDistance = 1.9f;
    float pathLeftDistance = 2.5f;

    bool specialWaypointUpcoming = false;
    typeofWaypoint currentTypeofWaypoint = typeofWaypoint.RUN;

    public List<GraphNode> specialWaypoints = new List<GraphNode>();
    public List<typeofWaypoint> specialWaypointTypes = new List<typeofWaypoint>();

    // Start is called before the first frame update
    void Start()
    {
        seeker = GetComponent<Seeker>();
        baseCharacterController = GetComponent<BaseCharacterController>();

        // baseCharacterController.PerformMovementAi(Vector2.left);
        // InvokeRepeating("StartNewPath", 2f, 3456f);
        // InvokeRepeating("HandleAiLogic", 1f, 0.16f);

        // StartNewPath();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(targetPosition.position, 0.2f);
    }

    private void FixedUpdate()
    {
        // baseCharacterController.PerformMovementAi(Vector2.);
        /* if (calculatePathing)
        {
            // seeker.StartPath(transform.position, targetPosition.position, OnPathComplete);
            seeker.StartPath(transform.position, targetPosition.position, OnPathComplete);
        } */
    }

    public void StartNewPath()
    {
        seeker.StartPath(transform.position, targetPosition.position, OnPathComplete);
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
        //Debug.Log("Yay, we got a path back. Did it have an error? " + p.error);
        if(!p.error)
        {
            //pathComplete = false;
            path = p;

            /*
            if (path.duration < Time.fixedDeltaTime)
            {
                Debug.Log(Time.fixedDeltaTime + " : " + path.duration + " diff : " + (Time.fixedDeltaTime - path.duration));
            } else
            {
                Debug.LogWarning(Time.fixedDeltaTime + " : " + path.duration + " diff : " + (Time.fixedDeltaTime - path.duration));
            }
            */

            currentWaypoint = 0;
        }
    }

    // GET THIS OUT OF UPDATE
    public void Update()
    {
        HandleAiLogic();
    }

    public void HandleAiLogic()
    {
        if (path == null)
        {
            // No path to follow yet
            return;
        }

        // can be optimised
        if (Vector2.Distance(transform.position, targetPosition.position) <= pathCompleteDistance)
        {
            pathComplete = true;
        }
        else if (pathComplete && Vector2.Distance(transform.position, targetPosition.position) >= pathLeftDistance)
        {
            pathComplete = false;
        }

        // for maximum perforance, you can just check the squard distance
        float distanceToWaypoint = Vector2.Distance(baseCharacterController.groundCheck.position + Vector3.up * 0.25f, path.vectorPath[currentWaypoint]);
        reachedEndOfPath = false;

        bool issueOutSpecialCommand = false;
        currentTypeofWaypoint = typeofWaypoint.RUN;

        for (int i=0; i < specialWaypoints.Count; i++)
        {
            if (specialWaypoints[i] == null) continue;

            if(path.path[currentWaypoint] == specialWaypoints[i])
            {
                specialWaypointUpcoming = true;
                if(distanceToWaypoint < 0.1f)
                {
                    specialWaypointUpcoming = false;
                    currentTypeofWaypoint = specialWaypointTypes[i];
                    reachedEndOfPath = true;
                    issueOutSpecialCommand = true;
                    currentWaypoint++;
                }
            }
        }

        while (!specialWaypointUpcoming)
        {
            // for maximum perforance, you can just check the squard distance

            if (distanceToWaypoint < nextWaypointDistance)
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

            distanceToWaypoint = Vector2.Distance(baseCharacterController.groundCheck.position + Vector3.up * 0.25f, path.vectorPath[currentWaypoint]);
        }

        if (!reachedEndOfPath && !pathComplete)
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
        else if (pathComplete && baseCharacterController.Ai_movementDirection != Vector2.zero && currentTypeofWaypoint == typeofWaypoint.RUN)
        {
            baseCharacterController.PerformMovementAi(Vector2.zero);
        }

        if (issueOutSpecialCommand && reachedEndOfPath)
        {
            switch (currentTypeofWaypoint)
            {
                case typeofWaypoint.RUN:
                    break;

                case typeofWaypoint.JUMP:
                    baseCharacterController.PerformJumpAi();
                    break;

                case typeofWaypoint.DODGE:
                    break;

                case typeofWaypoint.NEUTRAL_DODGE:
                    break;
            }
        }
    }
}


