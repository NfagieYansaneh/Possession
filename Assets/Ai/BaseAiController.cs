using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;
using UnityEngine.Events;
using System;

// help from https://arongranberg.com/astar/docs/custom_movement_script.html

public enum typeofWaypoint { RUN, WAIT, JUMP, AIRBORNE_JUMP, DODGE, NEUTRAL_DODGE };

public class BaseAiController : MonoBehaviour
{
    public Transform targetPlayerPosition;
    Vector3 detourWaypointPosition;

    public BaseCharacterController baseCharacterController;
    public Seeker seeker;
    Path path;
    int currentWaypoint = 0;

    bool reachedEndOfPath = false;
    bool pathComplete = false;


    float nextWaypointDistance = 2;
    float pathCompleteDistance = 1.9f;
    float pathLeftDistance = 2.5f;

    public bool takingDetour = false;
    bool specialWaypointUpcoming = false;
    typeofWaypoint currentTypeofWaypoint = typeofWaypoint.RUN;

    public struct specialWaypoint 
    {
        public bool active;

        public GraphNode node;
        public readonly Vector3 nodePosition { get { return (Vector3)node.position; } }
        public bool facingRight;

        public typeofWaypoint waypointType;
        public UnityEvent events;
        public float activationRange;

        public bool ifBelowActivate;
        public bool ifLeftActivate;
        public bool ifRightActivate;
        public bool ifAboveActivate;
        public bool ifFallingActivate;

        public bool ignoreY;

        public GraphNode contextJumpNode;
        public GraphNode contextJumpEndNode;

        public specialWaypoint(typeofWaypoint type, GraphNode targetNode, UnityAction action, bool waypointFacingRight = false, float activateRange = 0.25f,
            GraphNode newContextJumpNode = null, GraphNode newContextJumpEndNode = null, bool belowActivate=false, bool leftActivate=false, bool rightActivate=false,
            bool aboveActivate=false, bool fallingActivate=false, bool setIgnoreY = false)
        {
            active = true;
            node = targetNode;
            waypointType = type;

            facingRight = waypointFacingRight;
            activationRange = activateRange;

            contextJumpNode = newContextJumpNode;
            contextJumpEndNode = newContextJumpEndNode;

            events = new UnityEvent();
            events.AddListener(action);

            ifBelowActivate = belowActivate;
            ifLeftActivate = leftActivate;
            ifRightActivate = rightActivate;
            ifAboveActivate = aboveActivate;
            ifFallingActivate = fallingActivate;

            ignoreY = setIgnoreY;
        }

        static public specialWaypoint operator +(specialWaypoint myStruct, UnityAction action)
        {
            myStruct.events.AddListener(action);
            return myStruct;
        }

        static public specialWaypoint operator -(specialWaypoint myStruct, UnityAction action)
        {
            myStruct.events.RemoveListener(action);
            return myStruct;
        }

        public void isActive (bool state)
        {
            active = state;
        }

        public string nodePositionToString() { return $"{nodePosition.x} : {nodePosition.y}"; }
        public string waypointTypeToString() {
            string str;
            switch (waypointType)
            {
                case typeofWaypoint.RUN:
                    str = "RUN";
                    break;

                case typeofWaypoint.JUMP:
                    str = "JUMP";
                    break;

                case typeofWaypoint.DODGE:
                    str = "DODGE";
                    break;

                case typeofWaypoint.AIRBORNE_JUMP:
                    str = "AIRBORNE_JUMP";
                    break;

                case typeofWaypoint.WAIT:
                    str = "WAIT";
                    break;

                default:
                    // typeofWaypoint.NEUTRAL_DODGE
                    str = "NEUTRAL_DODGE";
                    break;
            }

            return $"waypointType : {str} & dir : {((facingRight==true)? "facingRight" : "facingLeft")}";
        }
    }

    public List<specialWaypoint> specialWaypoints = new List<specialWaypoint>();

    public bool showGUI = true;
    public bool calculatePathingStartup = false;
    // public List<GraphNode> specialWaypoints = new List<GraphNode>();
    // public List<typeofWaypoint> specialWaypointTypes = new List<typeofWaypoint>();

    // Start is called before the first frame update
    void Start()
    {
        // seeker = GetComponent<Seeker>();
        // baseCharacterController = GetComponent<BaseCharacterController>();

        // baseCharacterController.PerformMovementAi(Vector2.left);
        // InvokeRepeating("StartNewPath", 2f, 3456f);
        // InvokeRepeating("HandleAiLogic", 1f, 0.16f);

        // StartNewPath();

        if (calculatePathingStartup)
        {
            seeker.StartPath(transform.position, targetPlayerPosition.position, OnPathComplete);
            InvokeRepeating("PeriodicUpdatePathRepeating", 1f, 0.5f);
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(targetPlayerPosition.position, 0.2f);
    }

    private void FixedUpdate()
    {
        // baseCharacterController.PerformMovementAi(Vector2.);
        /* if (calculatePathing)
        {
            // seeker.StartPath(transform.position, targetPlayerPosition.position, OnPathComplete);
            seeker.StartPath(transform.position, targetPlayerPosition.position, OnPathComplete);
        } */
    }

    public void StartNewPath(Vector3 position, bool takingDetourEnabled = false)
    {
        specialWaypoints.Clear();
        seeker.StartPath(transform.position, position, OnPathComplete);

        if (takingDetourEnabled)
        {
            takingDetour = true;
            detourWaypointPosition = position;
        }
    }

    // Update is called once per frame
    private void OnGUI()
    {
        /* calculatePathing = GUI.Toggle(new Rect(500, 20, 135, 25), calculatePathing, new GUIContent("Calculate Pathing")); */
        if (showGUI)
        {
            if (GUI.Button(new Rect(500, 20, 135, 50), new GUIContent("Calculate Pathing")))
            {
                specialWaypoints.Clear();
                seeker.StartPath(transform.position, targetPlayerPosition.position, OnPathComplete);
                InvokeRepeating("PeriodicUpdatePathRepeating", 1f, 0.5f);
            }
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

            reachedEndOfPath = false;
            pathComplete = false;
            takingDetour = false;
            specialWaypointUpcoming = false;

            currentWaypoint = 0;
        }
    }

    // GET THIS OUT OF UPDATE
    public void Update()
    {
        HandleAiLogic();
    }

    void PeriodicUpdatePathRepeating()
    {
        if (baseCharacterController.isGrounded && takingDetour==false && startedEnumerator==false && baseCharacterController.hitStopActive==false)
        {
            StartNewPath(targetPlayerPosition.position);
            // also only update if player leaves nodegroup?
        }
    }

    bool startedEnumerator = false;
    IEnumerator CalculateNewPathEnumerator(float wait)
    {
        yield return new WaitForSeconds(wait);
        pathComplete = true;
        takingDetour = false;
        startedEnumerator = false;
        // specialWaypoints.Clear();
        StartNewPath(targetPlayerPosition.position);
    }

    public void HandleAiLogic()
    {
        if (path == null)
        {
            // No path to follow yet
            return;
        }

        // can be optimised
        if (!takingDetour)
        {
            if (Vector2.Distance(transform.position, targetPlayerPosition.position) <= pathCompleteDistance)
            {
                pathComplete = true;
            }
            else if (pathComplete && Vector2.Distance(transform.position, targetPlayerPosition.position) >= pathLeftDistance)
            {
                pathComplete = false;
            }
        } else
        {
            if (Vector2.Distance(transform.position, detourWaypointPosition) <= 1f && !startedEnumerator)
            {
                Debug.Log("Recalculating pathing after completing the detour");
                startedEnumerator = true;
                StartCoroutine(CalculateNewPathEnumerator(0.2f));
            }
        }

        // for maximum perforance, you can just check the squard distance
        float distanceToWaypoint; // = Vector2.Distance(baseCharacterController.groundCheck.position + Vector3.up * 0.25f, path.vectorPath[currentWaypoint]);
        distanceToWaypoint = Mathf.Abs(baseCharacterController.groundCheck.position.x - path.vectorPath[currentWaypoint].x);
        reachedEndOfPath = false;

        currentTypeofWaypoint = typeofWaypoint.RUN;

        bool ignoreY = false;

        while (true)
        {
            bool willLoop = false;

            for (int i = 0; i < specialWaypoints.Count; i++)
            {
                if (specialWaypoints[i].active == false) continue;

                if (path.path[currentWaypoint] == specialWaypoints[i].node)
                {
                    ignoreY = specialWaypoints[i].ignoreY;

                    distanceToWaypoint = (specialWaypoints[i].ignoreY)? Mathf.Abs(baseCharacterController.groundCheck.position.x - path.vectorPath[currentWaypoint].x) :
                    Vector3.Distance(baseCharacterController.groundCheck.position + Vector3.up * 0.25f, path.vectorPath[currentWaypoint]);

                    specialWaypointUpcoming = true;
                    // Magic value!
                    if (distanceToWaypoint <= specialWaypoints[i].activationRange || 
                        (specialWaypoints[i].ifBelowActivate && baseCharacterController.groundCheck.position.y < specialWaypoints[i].nodePosition.y) ||
                        (specialWaypoints[i].ifLeftActivate && baseCharacterController.groundCheck.position.x < specialWaypoints[i].nodePosition.x) ||
                        (specialWaypoints[i].ifRightActivate && baseCharacterController.groundCheck.position.x > specialWaypoints[i].nodePosition.x))
                    {
                        // Debug.Log("x" + i);
                        specialWaypointUpcoming = false;
                        currentTypeofWaypoint = specialWaypoints[i].waypointType;
                        specialWaypoints[i].events.Invoke();
                        reachedEndOfPath = true;
                        currentWaypoint++;

                        // Debug.Log(specialWaypoints[i].waypointTypeToString());

                        specialWaypoints[i].isActive(false);
                        willLoop = true;
                        break;
                    }
                }
            }

            if (willLoop) continue;
            else break;
        }


        while (!specialWaypointUpcoming)
        {
            // for maximum perforance, you can just check the squard distance

            bool willBreak = false;
            foreach(specialWaypoint sWaypoint in specialWaypoints)
            {
                if (sWaypoint.node == path.path[currentWaypoint]) willBreak = true;
            }

            if (willBreak) break;

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

            if (specialWaypointUpcoming && !ignoreY)
            {
                distanceToWaypoint = Vector3.Distance(baseCharacterController.groundCheck.position + Vector3.up * 0.25f, path.vectorPath[currentWaypoint]);
            } else
            distanceToWaypoint = Mathf.Abs(baseCharacterController.groundCheck.position.x - path.vectorPath[currentWaypoint].x);
        }

        if (!reachedEndOfPath && !pathComplete && !baseCharacterController.holdMovementAiOverride)
        {
            if ((baseCharacterController.Ai_movementDirection == Vector2.right || baseCharacterController.Ai_movementDirection == Vector2.zero)
                && (path.vectorPath[currentWaypoint] - transform.position).x < 0f)
            {
                if(Mathf.Abs(baseCharacterController.groundCheck.position.x - path.vectorPath[currentWaypoint].x) > 0.2f)
                    baseCharacterController.PerformMovementAi(Vector2.left);
                else
                    baseCharacterController.PerformMovementAi(Vector2.zero);
            }
            else if ((baseCharacterController.Ai_movementDirection == Vector2.left || baseCharacterController.Ai_movementDirection == Vector2.zero)
              && (path.vectorPath[currentWaypoint] - transform.position).x > 0f)
            {
                if (Mathf.Abs(baseCharacterController.groundCheck.position.x - path.vectorPath[currentWaypoint].x) > 0.2f)
                    baseCharacterController.PerformMovementAi(Vector2.right);
                else
                    baseCharacterController.PerformMovementAi(Vector2.zero);
            }
        }
        else if (pathComplete && baseCharacterController.Ai_movementDirection != Vector2.zero && currentTypeofWaypoint == typeofWaypoint.RUN)
        {
            baseCharacterController.PerformMovementAi(Vector2.zero);
        }
    }
}


