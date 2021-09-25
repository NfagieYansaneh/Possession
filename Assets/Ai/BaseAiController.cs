using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;
using UnityEngine.Events;
using System;

// assistance from https://arongranberg.com/astar/docs/custom_movement_script.html

/* Purpose of BaseAiController is handle our the Ai scans for new paths towards its destination
 * and detects waypoints, such as jump waypoints in which the Ai will cause the character to jump once the Ai
 * reaches within a certain defined distance towards that jump waypoint
 */

public enum typeofWaypoint { RUN, WAIT, JUMP, AIRBORNE_JUMP, DODGE, NEUTRAL_DODGE };

public class BaseAiController : MonoBehaviour
{
    public Transform targetPlayerPosition;
    Vector3 detourWaypointPosition; // detour waypoint positions are defined when it is impossible to form a path towards the targetPlayerPosition

    public BaseCharacterController baseCharacterController;
    public Seeker seeker;
    bool calculatePathing = false; // unused as of now
    Path path;
    public int currentWaypoint = 0; // currentWaypoint refers to the nodes apart of the path towards the targetPlayerPosition that the Ai is trying to head to next. We have to refer to this
    // as an int as it repsents an index among an array

    public bool reachedEndOfPath = false; // reached the end of this current path
    public bool pathComplete = false; // reached targetPlayerPosition


    float nextWaypointDistance = 2; // distance to 'reach' the nextWaypoint (not related to special waypoints where jumps or dashes are performed), and look for the next one that is apart
    // of the points that makeup our path towards the targetPlayerPosition

    float pathCompleteDistance = 1.9f; // distance for Ai to complete our path by being within 1.9f of the targetPlayerPosition
    float pathLeftDistance = 2.5f; // distance for Ai to no longer have its path completed due to being further than 2.5f distance away from targetPlayerPosition

    public bool takingDetour = false;
    public bool specialWaypointUpcoming = false;
    typeofWaypoint currentTypeofWaypoint = typeofWaypoint.RUN;
    public bool startPathing = false;
    public bool currentlyBeingPossessed = false;

    // specialWaypoint represents our structs for assigning commands at specific nodes, thus, allowing us to issue a jump command once we reach within the activation range of the special waypoint
    public struct specialWaypoint 
    {
        public bool active; // special waypoint active

        public GraphNode node;
        public readonly Vector3 nodePosition { get { return (Vector3)node.position; } }
        public bool facingRight; // is this special waypoint will be facing to the right or not (facing the left)? For ex. a jump facing right means we will jump towards the right

        public typeofWaypoint waypointType;
        public UnityEvent events;
        public float activationRange;

        public bool ifBelowActivate; // if below this special waypoint, activate
        public bool ifLeftActivate; // if to the left of this special waypoint, activate
        public bool ifRightActivate; // if to the right of this special waypoint, activate
        public bool ifAboveActivate; // if above this special waypoint, activate
        public bool ifFallingActivate; // if falling, activate this special waypoint

        public bool ignoreY; // used to ignore the Y component of vecters when determining if we are in the activation range of this special waypoint

        // used mainly for just debugging as this gives context on what jumpNode and jumpEndNode is in between this special waypoint that even created this special waypoint in the first place
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

    private void OnDrawGizmos()
    {
        // debugging purposes
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(targetPlayerPosition.position, 0.2f);
    }

    public void StartNewPath(Vector3 position, bool takingDetourEnabled = false)
    {
        if (!startPathing) return;
        specialWaypoints.Clear();
        seeker.StartPath(transform.position, position, OnPathComplete);

        if (takingDetourEnabled)
        {
            takingDetour = true;
            detourWaypointPosition = position;
        }
    }

    private void OnGUI()
    {
        if (GUI.Button(new Rect(500, 20, 135, 50), new GUIContent("Calculate Pathing")))
        {
            specialWaypoints.Clear();
            seeker.StartPath(transform.position, targetPlayerPosition.position, OnPathComplete);
            InvokeRepeating("PeriodicUpdatePathRepeating", 1f, 0.25f);
            startPathing = true;
        }
    }

    public void OnPathComplete(Path p)
    {
        if(!p.error)
        {
            path = p;

            // resetting booleans and currentWaypoint index to get ready to assess new path...
            reachedEndOfPath = false;
            pathComplete = false;
            takingDetour = false;
            specialWaypointUpcoming = false;

            currentWaypoint = 0;
        }
    }

    public void Update()
    {
        if (currentlyBeingPossessed) return;

        // I could shift this out of Update(); but there is no demand for it, it may be more performant by doing so...
        HandleAiLogic();
    }

    // bool queueNewPath = false;
    void PeriodicUpdatePathRepeating()
    {
        if (!currentlyBeingPossessed)
        {
            if (baseCharacterController.isGrounded && takingDetour == false && startedEnumerator == false && baseCharacterController.hitStopActive == false)
            {
                StartNewPath(targetPlayerPosition.position);
                // queueNewPath = false;
            }
            else
            {
                // queueNewPath = true;
            }
        }
    }

    public bool startedEnumerator = false;
    IEnumerator CalculateNewPathEnumerator(float wait)
    {
        yield return new WaitForSeconds(wait);
        pathComplete = true;
        takingDetour = false;
        startedEnumerator = false;
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
            // determining if we have completed our path by getting close enough to our destination
            if (Vector2.Distance(transform.position, targetPlayerPosition.position) <= pathCompleteDistance)
            {
                pathComplete = true;
            }
            else if (pathComplete && Vector2.Distance(transform.position, targetPlayerPosition.position) >= pathLeftDistance)
            {
                // in this case, we have gotten to far from our destination so I set pathComplete to false
                pathComplete = false;
            }
        } else
        {
            // in this instance we are taking a detour and getting ready to recalculate a new path once we complete the detour
            if (Vector2.Distance(transform.position, detourWaypointPosition) <= 1f && !startedEnumerator)
            {
                Debug.Log("Recalculating pathing after completing the detour");
                startedEnumerator = true;
                StartCoroutine(CalculateNewPathEnumerator(0.2f));
            }
        }

        float distanceToWaypoint;
        distanceToWaypoint = Mathf.Abs(baseCharacterController.groundCheck.position.x - path.vectorPath[currentWaypoint].x);
        reachedEndOfPath = false;

        currentTypeofWaypoint = typeofWaypoint.RUN;

        bool ignoreY = false; // ignoreY is used to ignore the Y component in a vectors when checking how close we are to an upcoming special waypoint

        while (true)
        {
            bool willLoop = false;

            for (int i = 0; i < specialWaypoints.Count; i++)
            {
                if (specialWaypoints[i].active == false) continue;

                if (path.path[currentWaypoint] == specialWaypoints[i].node)
                {
                    ignoreY = specialWaypoints[i].ignoreY;

                    // determining distance to our special waypoint
                    distanceToWaypoint = (specialWaypoints[i].ignoreY)? Mathf.Abs(baseCharacterController.groundCheck.position.x - path.vectorPath[currentWaypoint].x) :
                    Vector3.Distance(baseCharacterController.groundCheck.position + Vector3.up * 0.25f, path.vectorPath[currentWaypoint]);

                    specialWaypointUpcoming = true;
                    if (distanceToWaypoint <= specialWaypoints[i].activationRange || 
                        (specialWaypoints[i].ifBelowActivate && baseCharacterController.groundCheck.position.y < specialWaypoints[i].nodePosition.y) ||
                        (specialWaypoints[i].ifLeftActivate && baseCharacterController.groundCheck.position.x < specialWaypoints[i].nodePosition.x) ||
                        (specialWaypoints[i].ifRightActivate && baseCharacterController.groundCheck.position.x > specialWaypoints[i].nodePosition.x))
                    {
                        // in this case, we are actually close enough to our special waypoint or have passed the necessary criteria to execute the commands that 
                        // this special waypoint holds
  
                        specialWaypointUpcoming = false;
                        currentTypeofWaypoint = specialWaypoints[i].waypointType;
                        specialWaypoints[i].events.Invoke();
                        reachedEndOfPath = true;
                        currentWaypoint++;

                        specialWaypoints[i].isActive(false);
                        willLoop = true; // we are going to loop once more to see if we are close enough to any other special waypoint in order to invoke their commands as well
                        break;
                    }
                }
            }

            if (willLoop) continue; // in this case, we requested a loop so we will loop once more and search through the special waypoints
            else break; // else break the loop
        }

        // while loop formed to set the new currentWaypoint that the Ai is travelling to that is along the Ai's path
        while (!specialWaypointUpcoming)
        {

            bool willBreak = false;
            foreach(specialWaypoint sWaypoint in specialWaypoints)
            {
                if (sWaypoint.node == path.path[currentWaypoint]) willBreak = true;
            }

            if (willBreak) break; // in the case currentWaypoint we are travelling too is actually a special waypoint
            // and we want to break the loop here so we don't accidentally skip over this special waypoint

            if (distanceToWaypoint < nextWaypointDistance)
            {
                // in this case, we have reached the waypoint so lets head to the next waypoint by adding 1 to our currentWaypoint

                if (currentWaypoint + 1 < path.vectorPath.Count)
                {
                    currentWaypoint++;
                }
                else
                {
                    // in this instance, we have actually reached the end of our path because we reach the final waypoint in our path
                    // so we set reachedEndOfPath to true and break the loop
                    reachedEndOfPath = true;
                    break;
                }
            }
            else
            {
                // in thhis case, we are close enough to the currentWaypoint in order to head the subsequent waypoint, so we just break the loop for now
                break;
            }

            // Uneccessary code that serves no purpose?

            /*
            if (specialWaypointUpcoming && !ignoreY)
            {
                distanceToWaypoint = Vector3.Distance(baseCharacterController.groundCheck.position + Vector3.up * 0.25f, path.vectorPath[currentWaypoint]);
            } else
            distanceToWaypoint = Mathf.Abs(baseCharacterController.groundCheck.position.x - path.vectorPath[currentWaypoint].x); */
        }

        // in the case that we have not reached the end of the path, nor did we reach the targetPlayerPosition, nor are we currently experiencing a holdMovementAiOverride
        // we will assess the direction of the current waypoint we are heading towards, and set the appropriate direction via PerformMovementAi
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
            // in this case, our path is complete, so we just set PerformMovementAi to Vector2.zero
            baseCharacterController.PerformMovementAi(Vector2.zero);
        }
    }
}


