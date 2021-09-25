using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;
using UnityEngine.Events;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

/* Purpose of BaseAiPathModifier is to modify the A* path produced by seeker.StartPath in BaseAiController.cs
 * in order to add necessary jumps, double jumps, dropdowns, etc. to allow the Ai controlled character to navigate
 * the scene towards its final destination
 */

/* Furthermore, the Ai sees the world as a 2D grid of squares refered to as nodes. This is how the Ai can see the world and
 * determine what to do
 */

public enum AdjNodeSearchDirection { LEFT, RIGHT, BOTH }

public class BaseAiPathModifier : MonoModifier
{
    // original path formed from seeker.StartPath
    public List<GraphNode> originalNodes;
    public List<Vector3> originalVectorPath;

    // new path formed that now incorporates jumps, double jumps, dropdowns, etc. to allow the AI controlled character to navigate
    // the scene towards its final destination
    public List<GraphNode> newNodes = new List<GraphNode>();
    public List<Vector3> newVectorPath = new List<Vector3>();

    // jumpNodes refers to all places the Ai should be jumping
    public List<GraphNode> jumpNodes = new List<GraphNode>();

    // jumpEndNodes refers to all places that the Ai should be landing after performing a jump at a jumpNode
    public List<GraphNode> jumpEndNodes = new List<GraphNode>();

    // This contains the list of calculated and processed jumpNodes since not all jumpNodes will be processed due to how some jumpNodes
    // may be skipped to form a more efficent path
    public List<GraphNode> jumpNodesFinal = new List<GraphNode>(); 

    // if a jumpEndNode is formed overhead of the Ai character's jumpNode. In certain circumstances, we may calculate the adj nodes towards
    // this overhead jumpEndNode
    public List<GraphNode> adjNodesFromOverhead = new List<GraphNode>();

    public List<GraphNode> debugNodes = new List<GraphNode>(); // just to visualise some data quickly

    // ignoreJumpNodes are jumpNodes that have been skipped due to forming a more efficent path by doing so
    public List<GraphNode> ignoreJumpNodes = new List<GraphNode>();

    // Latest node that has just used in the _____ function
    public static GraphNode latestInsertNode = null;

    // PaddingNodeStruct encompasses the padding nodes that are used to space out jumps between each other to make them appear more
    // natural
    public struct PaddingNodeStruct
    {
        // padding node
        public GraphNode node;
        public readonly Vector3 position { get { return (Vector3)node.position; } }

        // can this padding node still be used as a node to jump off? if there are are not other available free nodes to jump from?
        public bool isAbsolute;

        public bool isOverlapping(GraphNode otherNode)
        {
            Vector3 otherNodePosition = (Vector3)otherNode.position;
            if (position == otherNodePosition) return true;
            else return false;
        }

        public PaddingNodeStruct(GraphNode newNode, bool absolute)
        {
            node = newNode;
            isAbsolute = absolute;
        }
    }

    // WaypointInsertStruct simplifies a way to insert a waypoint command (used to issue a dash, jump, or airborneJump) into a path at an index
    public struct WaypointInsertStruct
    {
        public readonly Vector3 position { get { return (Vector3)node.position; } }
        public GraphNode node;
        public GraphNode indexNode;

        public WaypointInsertStruct(GraphNode newNode, GraphNode insertAtNode)
        {
            node = newNode;
            indexNode = insertAtNode;
            latestInsertNode = newNode;

            // Debug.Log("Called from " + (Vector3)insertAtNode.position + " : to " + (Vector3)newNode.position);
        }
    }

    // TrimRequestStruct is used to form a trim request that trims all nodes and vector positions within a path between a "from" and "to"
    public struct TrimRequestStruct
    {
        public GraphNode from;
        public GraphNode to;
        public Vector3 fromPosition { get { return (Vector3)from.position; } }
        public Vector3 toPosition { get { return (Vector3)to.position; } }

        public TrimRequestStruct (GraphNode fromNode, GraphNode toNode)
        {
            from = fromNode;
            to = toNode;
        }
    }

    // stores all padding structs
    public List<PaddingNodeStruct> allPaddingStructs = new List<PaddingNodeStruct>();

    // stores all waypoint inserts
    public List<WaypointInsertStruct> waypointInserts = new List<WaypointInsertStruct>();

    // stores all trim requests
    public List<TrimRequestStruct> trimRequests = new List<TrimRequestStruct>();

    // stores all late trim requests that are issued out after the original path has been overwritten by the new path
    public List<TrimRequestStruct> lateTrimRequests = new List<TrimRequestStruct>();

    // newDestination is used for forming a detour node when the original destination node can not be reached
    public GraphNode newDestination = null;

    // character controller that parents this BaseAiPathModifier script
    public BaseCharacterController baseCharacterController;

    // Ai controller that will be calling upon the commands established from the new path fromed from BaseAiPathModifier
    public BaseAiController baseAiController;

    // specialWaypoints is a list of all waypoints that will be used to identify when the Ai should jump, dash, airborne jump, etc.
    public List<BaseAiController.specialWaypoint> specialWaypoints = new List<BaseAiController.specialWaypoint>();

    // For curves
    // public int resolution = 6;

    // List of commonly used variables for handling kinematic equations

    float t_rise;
    float t_maxRise;
    float t_airborneRise;
    float t_maxAirborneRise;

    float gravityRise;
    float gravityFall;
    float Vx;

    float jumpHeight;
    float maxJumpHeight;

    float airborneJumpHeight;
    float maxAirborneJumpHeight;

    float Vyi;

    public void Start()
    {
        // Computing commonly used variables for handling kinematic equations and storing them for later
        Vx = baseCharacterController.movementSpeed;

        jumpHeight = baseCharacterController.jumpHeight;

        airborneJumpHeight = baseCharacterController.airborneJumpHeight;

        gravityRise = baseCharacterController.gravity * baseCharacterController.gravityMultiplier;
        gravityFall = baseCharacterController.gravity * baseCharacterController.gravityMultiplier * baseCharacterController.fallingGravityMultiplier;

        Vyi = Mathf.Sqrt(2 * gravityRise * jumpHeight);
        maxJumpHeight = (Vyi * Vyi) / (2 * gravityRise * 0.8f);
        t_maxRise = 2 * maxJumpHeight / Vyi;

        float temp = GetAirborneVyi(0);
        maxAirborneJumpHeight = (temp * temp) / (2 * gravityRise * 0.8f);
        t_maxAirborneRise = 2 * maxAirborneJumpHeight / temp;

        t_rise = (2 * jumpHeight / Vyi);
        t_airborneRise = 2 * airborneJumpHeight / temp;
    }

    public float GetAirborneVyi (int jumps = 0)
    {
        return Mathf.Sqrt(
            2 * baseCharacterController.gravity * baseCharacterController.gravityMultiplier * GetAirborneJumpHeight(jumps) *
            Mathf.Pow(baseCharacterController.successiveJumpHeightReduction, jumps));
    }

    public float GetAirborneJumpHeight(int jumps = 0)
    {
        return airborneJumpHeight * Mathf.Pow(baseCharacterController.successiveJumpHeightReduction, jumps);
    }

    // States the priorty and order in which this PathModifer will run. Thus, if another path modifer had an Order of 61, that modifier will run after
    // this path modifier runs
    public override int Order { get { return 60; } }

    public override void Apply(Path path)
    {

        if (path.error || path.vectorPath == null || 
            path.vectorPath.Count <= 3) { return; }

        ClearAllLists();
        originalNodes = path.path;
        originalVectorPath = path.vectorPath;

        newNodes = originalNodes;
        newVectorPath = path.vectorPath;


        bool findNextLowPenalty = false;

        GraphNode returnedNode = null;

        // SearchInDirection actually only searchs in the down direction. I could establish the choice to change which direction it searchs, but I have no current
        // need to search in other directions

        // GridGraphGenerate.lowPenalty is the penalty number set to a node if that node is seen as ground, else it is set as GridGraphGenerate.highPenalty as that is the
        // penalty number set to a node if it is seen as air
        if (!Helper.SearchInDirection(GridGraphGenerate.gg, newNodes[0], 0, ref returnedNode) && newNodes[0].Penalty != GridGraphGenerate.lowPenalty)
        {
            List<Vector2> tempVectorPath = new List<Vector2>();
            for (int i = 0; i < 4; i++) // adds first 4 nodes
            {
                tempVectorPath.Add((Vector3)newNodes[i].position);
            }

            if (Helper.CheckDirectionOfPathInSequence(tempVectorPath, Vector2.up, 2))
            {
                // if two adjacent nodes are heading in the direction of 'Vector2.up'

                Helper.SearchInDirection(GridGraphGenerate.gg, newNodes[0], 2, ref returnedNode);
                jumpNodes.Add(returnedNode);
                findNextLowPenalty = true; // ensures that the program will find the next ground node in order to find where the player should land
            }
        }


        // Finds all jumpNodes and jumpEndNodes in the original path
        for (int i=0; i<originalNodes.Count-2; i++)
        {
            if(findNextLowPenalty == true && originalNodes[i].Penalty == GridGraphGenerate.lowPenalty)
            {
                jumpEndNodes.Add(originalNodes[i]);
                findNextLowPenalty = false;
            }

            if(originalNodes[i].Penalty == GridGraphGenerate.lowPenalty && originalNodes[i + 1].Penalty == GridGraphGenerate.highPenalty)
            {
                if (originalNodes[i + 2].Penalty == GridGraphGenerate.highPenalty)
                {
                    jumpNodes.Add(originalNodes[i]);
                    findNextLowPenalty = true;
                }
            }
        }

        if (findNextLowPenalty == true && originalNodes[originalNodes.Count-1].Penalty == GridGraphGenerate.lowPenalty)
        {
            jumpEndNodes.Add(originalNodes[originalNodes.Count-1]);
        }


        // Goes through each jumpNode and determines what type of action should take place
        for (int i = 0; i < jumpEndNodes.Count; i++)
        {
            // I have to seperate the CalculateDropdown & CalculateSingleDropdown because of priority queuing issues
            if (i < jumpNodes.Count)
            {
                if (ignoreJumpNodes.Contains(jumpNodes[i])) continue;
            }

            if (CalculateDropdown(jumpNodes[i], jumpEndNodes[i]))
            {
                // Debug.Log("Dropdown Pathing Chosen...");
                continue;
            }
            else if (CalculateSingleJump(jumpNodes[i], jumpEndNodes[i], jumpNodes[((i < (jumpEndNodes.Count - 2)) ? i + 1 : i)],
                jumpEndNodes[((i < (jumpEndNodes.Count - 2)) ? i + 1 : i)], (i < (jumpEndNodes.Count - 2) ? true : false)))
            {
                // Debug.Log("Single Jump Pathing Chosen...");
                continue;
            }
            else if (CalculateSingleDropdown(jumpNodes[i], jumpEndNodes[i]))
            {
                // Debug.Log("Dropdown + Single jump");
                continue;
            }
            else if (CalculateSingleJumpDashing(jumpNodes[i], jumpEndNodes[i]))
            {
                // Debug.Log("Single Jump + Dashing Pathing Chosen...");
                continue;
            }
            else if (CalculateDoubleJump(jumpNodes[i], jumpEndNodes[i]))
            {
                // Debug.Log("Double Jump Pathing Chosen...");
                continue;
            }
            else
            {
                // no action is possible to be formed on this jumpNode, so we will try to find a detour path

                // Debug.LogError("pathing failed, assessing new path...");

                AssessAndEstablishNewDetour();

                // Debug.Break();
                return;
            }
        }


        // AssessAndEstablishNewDetour() in a naunced instance where no jumpEndNodes are formed even though one jumpNode exists
        if (jumpEndNodes.Count == 0 && jumpNodes.Count == 1 && Vector3.Distance(newVectorPath[0], newVectorPath[newVectorPath.Count-1])>2f
            && !Helper.SearchInDirection(GridGraphGenerate.gg, newNodes[0], 0, ref returnedNode))
        {
            AssessAndEstablishNewDetour();
        }


        // trimming parts of the new path
        foreach(TrimRequestStruct trimRequest in trimRequests)
        {
            TrimInBetween(trimRequest.from, trimRequest.to);
        }

        // inserting waypoints into the new path
        foreach (WaypointInsertStruct insert in waypointInserts)
        {
            int index = newNodes.FindIndex(d => d == insert.indexNode) + 1;

            newNodes.Insert(index, insert.node);
            newVectorPath.Insert(index, insert.position);

            // InsertWaypointAtPosition(insert.position, insert.node, true);
        }

        // late trimming path after waypoints have been inserted
        foreach (TrimRequestStruct trimRequest in lateTrimRequests)
        {
            TrimInBetween(trimRequest.from, trimRequest.to);
        }

        path.path = newNodes;
        path.vectorPath = newVectorPath;
        // throw new System.NotImplementedException();
    }

    public Vector2 GetDirectionOfNode(GraphNode from, GraphNode to)
    {
        Vector3 fromPositon = (Vector3)from.position;
        Vector3 toPosition = (Vector3)to.position;

        Vector3 diff = toPosition - fromPositon;

        return diff.normalized;
    }

    // used to trimp every node in between the "from" node and "to" node that is apart of our new path
    public void TrimInBetween(GraphNode from, GraphNode to)
    {

        int fromIndex = newNodes.FindIndex(d => d == from);
        int toIndex = newNodes.FindIndex(d => d == to);
        // Debug.LogWarning("TRIMMING FROM " + fromIndex + " TO " + toIndex);

        if (fromIndex + 1 == toIndex || fromIndex == toIndex) return;
        //Debug.Log("no returning");
        // Debug.Log($"------------");
        // Debug.Log($"fromIndex : {fromIndex}");
        // Debug.Log($"toIndex : {toIndex}");

        int offset = 0;
        for(int i=fromIndex+1; i<toIndex; i++)
        {
            // Debug.Log($"i : {i}");
            // Debug.Log($"offset : {offset}");
            // Debug.Log("i - offset : " + (i - offset));

            /* foreach (BaseAiController.specialWaypoint specialWaypoint in specialWaypoints)
            {
                if (originalNodes[i] == specialWaypoint.node) continue;
            } */

            if (newNodes[i - offset] != null)
            {
                //Debug.LogError("iglooghost just removed a lei line");
                newNodes.RemoveAt(i - offset);
                newVectorPath.RemoveAt(i - offset);
            }

            else break;

            offset++;
        }
    }

    // called if the original pathing is impossible to perform
    public void AssessAndEstablishNewDetour()
    {
        // NodeGroups are groupings of adjacent groundNodes in order to quickly assess how many 'platforms' we have in our scene. This allows us to assess
        // a new path in a faster and understandable manner

        List<GridGraphGenerate.NodeGroupStruct> potentialGroups = new List<GridGraphGenerate.NodeGroupStruct>();
        GridGraphGenerate.NodeGroupStruct currentNodeGroup = GridGraphGenerate.FindThisNodesNodeGroup(newNodes[1]);

        // Assessing potential node groups that can be used to form a detour path
        for (int index = 0; index < GridGraphGenerate.nodeGroups.Count; index++)
        {
            GridGraphGenerate.NodeGroupStruct nodeGroup = GridGraphGenerate.nodeGroups[index];
            if (currentNodeGroup.middleNodePosition == nodeGroup.middleNodePosition) continue;

            if (nodeGroup.lowestNodePosition.y > newVectorPath[0].y && (nodeGroup.lowestNodePosition.y - newVectorPath[0].y) <= maxAirborneJumpHeight + maxJumpHeight
                && nodeGroup.lowestNodePosition.y < newVectorPath[newVectorPath.Count - 1].y)
            {
                potentialGroups.Add(nodeGroup);
                // usingLowestNode[index] = true;

            }
            else if (nodeGroup.lowestNodePosition.y <= newVectorPath[0].y && nodeGroup.highestNodePosition.y > newVectorPath[0].y && nodeGroup.highestNodePosition.y < newVectorPath[newVectorPath.Count - 1].y)
            {
                potentialGroups.Add(nodeGroup);
                // usingLowestNode[index] = true;
            }
        }

        // Debugging used to help visualise all nodeGroups in the scene as a sort of plus sign.
        foreach (GridGraphGenerate.NodeGroupStruct nodeGroup in potentialGroups)
        {
            Helper.DrawArrow.ForDebugTimed(nodeGroup.middleNodePosition + Vector3.up * 0.6f, Vector3.down, Color.magenta, 10000f, 0.25f, 90);
            Helper.DrawArrow.ForDebugTimed(nodeGroup.middleNodePosition + Vector3.up * 0.6f, Vector3.right, Color.magenta, 10000f, 0.25f, 90);
            Helper.DrawArrow.ForDebugTimed(nodeGroup.middleNodePosition + Vector3.up * 0.6f, Vector3.left, Color.magenta, 10000f, 0.25f, 90);
            Helper.DrawArrow.ForDebugTimed(nodeGroup.middleNodePosition + Vector3.up * 0.6f, Vector3.up, Color.magenta, 10000f, 0.25f, 90);
        }

        Debug.Log("Count " + potentialGroups.Count);
        GridGraphGenerate.NodeGroupStruct selectedGroup = new GridGraphGenerate.NodeGroupStruct(); // pointless but just for the compiler sake

        float smallestDistance = 0f;

        // iterating through all potentialGroups to decide which best canidate will be chosen as the new detour location
        for (int index = 0; index < potentialGroups.Count; index++)
        {
            Debug.Log("looping through potential groups for new destination...");

            GridGraphGenerate.NodeGroupStruct nodeGroup = potentialGroups[index];
            Debug.Log(nodeGroup.lowestNodePosition);
            Debug.Log(nodeGroup.middleNodePosition);
            Debug.Log(nodeGroup.highestNodePosition);

            // I might have to watch out if lowest node is surronded by high node parts
            // even though this entire process here works, I may revamp this later to handle naunced cases.
            float distanceToLowestNode = Vector3.Distance(nodeGroup.lowestNodePosition, newVectorPath[newVectorPath.Count - 1]);
            float distanceToHighestNode = Vector3.Distance(nodeGroup.highestNodePosition, newVectorPath[newVectorPath.Count - 1]);
            float distanceToMiddleNode = Vector3.Distance(nodeGroup.middleNodePosition, newVectorPath[newVectorPath.Count - 1]);

            if (index == 0)
            {

                if (distanceToLowestNode < distanceToHighestNode && distanceToLowestNode < distanceToMiddleNode)
                {
                    smallestDistance = distanceToLowestNode;
                    newDestination = nodeGroup.lowestNode;
                    selectedGroup = nodeGroup;
                }
                else if (distanceToMiddleNode < distanceToLowestNode && distanceToMiddleNode < distanceToHighestNode)
                {
                    smallestDistance = distanceToMiddleNode;
                    newDestination = nodeGroup.middleNode;
                    selectedGroup = nodeGroup;
                }
                else if (distanceToHighestNode <= distanceToMiddleNode && distanceToHighestNode <= distanceToLowestNode)
                {
                    smallestDistance = distanceToHighestNode;
                    newDestination = nodeGroup.highestNode;
                    selectedGroup = nodeGroup;
                }

                continue;
            }

            if (distanceToLowestNode < distanceToHighestNode && distanceToLowestNode < distanceToMiddleNode)
            {
                if (smallestDistance > distanceToLowestNode)
                {
                    smallestDistance = distanceToLowestNode;
                    newDestination = nodeGroup.lowestNode;
                    selectedGroup = nodeGroup;
                    Debug.LogError("Found New Destination...");
                }
            }
            else if (distanceToMiddleNode < distanceToLowestNode && distanceToMiddleNode < distanceToHighestNode)
            {
                if (smallestDistance > distanceToMiddleNode)
                {
                    smallestDistance = distanceToMiddleNode;
                    newDestination = nodeGroup.middleNode;
                    selectedGroup = nodeGroup;
                    Debug.LogError("Found New Destination...");
                }
            }
            else if (distanceToHighestNode <= distanceToMiddleNode && distanceToHighestNode <= distanceToLowestNode)
            {
                if (smallestDistance > distanceToHighestNode)
                {
                    smallestDistance = distanceToHighestNode;
                    newDestination = nodeGroup.highestNode;
                    selectedGroup = nodeGroup;
                    Debug.LogError("Found New Destination...");
                }
            }


        }

        // assiginging newDestination from the nodes apart of the selectedGroup as this will be our new detour
        if (newDestination == selectedGroup.leftistNode)
        {
            newDestination = selectedGroup.leftistNode_3IN;
        }
        else if (newDestination == selectedGroup.rightistNode)
        {
            newDestination = selectedGroup.rightistNode_3IN;
        }

        Debug.LogWarning((Vector3)newDestination.position);

        // starts a new path towards the detour node
        baseAiController.StartNewPath((Vector3)newDestination.position, true);
    }

    // used when checking if a node will be trimmed
    public bool CheckTrims(int index)
    {
        // this function was created to check if an assigned waypoint node could be trimmed off when cycling through trimRequests
        // Because of that, I never had a need to check if a node / waypoint node would be apart of my lateTrimRequests so I excluded lateTrimRequests
        // from CheckTrims()

        foreach (TrimRequestStruct trimRequest in trimRequests)
        {
            int fromIndex = newNodes.FindIndex(d => d == trimRequest.from);
            int toIndex = newNodes.FindIndex(d => d == trimRequest.to);

            if (index > fromIndex && index < toIndex) return true;
        }

        return false;
    }

    // quickly turns a selection of nodes into padding nodes (padding nodes defined above)
    public void PadTheseNodes(List<GraphNode> nodes, bool absolute)
    {
        foreach(GraphNode node in nodes)
        {
            PaddingNodeStruct newPaddingNodeStruct = new PaddingNodeStruct(node, absolute);
            allPaddingStructs.Add(newPaddingNodeStruct);
        }
    }

    // checks if a node is overlapping with a padding node
    public bool IsOverlappingWithPadding(GraphNode otherNode)
    {
        foreach(PaddingNodeStruct padding in allPaddingStructs)
        {
            if(padding.isOverlapping(otherNode) && !padding.isAbsolute)
            {
                return true;
            }
        }

        return false;
    }

    public void ClearAllLists()
    {
        jumpNodes.Clear();
        jumpEndNodes.Clear();
        jumpNodesFinal.Clear();
        ignoreJumpNodes.Clear();
        specialWaypoints.Clear();
        waypointInserts.Clear();
        adjNodesFromOverhead.Clear();
        allPaddingStructs.Clear();
        trimRequests.Clear();
        debugNodes.Clear();
        ignoreJumpNodes.Clear();
    }

    // returns the closest node from a selection of nodes towards a defined position
    private GraphNode FindClosestNode(Vector3 position, List<GraphNode> list)
    {
        if (list.Count == 0) return null;

        //GraphNode closestPreviousNode; // (don't worry, this assigned value to bound to be reassigned later)
        float previousDistanceSquared = 0f;
        GraphNode closestPreviousNode = list[0];

        foreach (GraphNode node in list)
        {
            Vector3 nodePosition = (Vector3)node.position;

            float currentDistanceSquared = ((nodePosition.x - position.x) * (nodePosition.x - position.x)) + ((nodePosition.y - position.y) * (nodePosition.y - position.y));

            if (previousDistanceSquared == 0f)
            {
                closestPreviousNode = node;
                previousDistanceSquared = currentDistanceSquared;
            }
            else if (previousDistanceSquared > currentDistanceSquared)
            {
                closestPreviousNode = node;
                previousDistanceSquared = currentDistanceSquared;
            }

            Vector3 pos = (Vector3)node.position;
            Helper.DrawArrow.ForDebugTimed(pos + Vector3.down * 1f, Vector3.up, Color.grey, 3f);
        }

        return closestPreviousNode;
    }

    // Calculates if is possible to form a dropdown from jumpNode towards jumpEndNode
    private bool CalculateDropdown(GraphNode jumpNode, GraphNode jumpEndNode)
    {
        Vector3 jumpNodePosition = (Vector3)jumpNode.position;
        Vector3 jumpEndNodePosition = (Vector3)jumpEndNode.position;

        if (jumpNodePosition.y <= jumpEndNodePosition.y) return false;

        bool foundAdjNodes = false;
        List<GraphNode> adjNodes = Helper.FindAdjacentNodes(jumpEndNode, ref foundAdjNodes, AdjNodeSearchDirection.BOTH);
        adjNodes.Insert(0, jumpEndNode);


        // determines if dropdown action will be facing towards the right, or towards the left
        bool waypointFacingRight = false;

        GraphNode nextNode = newNodes[(newNodes.FindIndex(d => d == jumpNode) + 1)];
        Vector3 nextNodePosition = (Vector3)nextNode.position;

        if (nextNodePosition.x > jumpNodePosition.x)
        {
            waypointFacingRight = true;
        }

        GraphNode closestNode = FindClosestNode(jumpNodePosition, adjNodes);
        Vector3 closestNodePosition = (Vector3)closestNode.position;

        float Sy = jumpEndNodePosition.y - jumpNodePosition.y; // This is bound to be a negative number
        float t_fall = Mathf.Sqrt(2 * Sy / -gravityFall);
        float Sx = Vx * t_fall;

        // Used to check whether it is possible to dropdown and reach the jumpEndNode target by dropping down from jumpNode position
        if ((jumpNodePosition.x > -Sx - 0.5f + closestNodePosition.x) && waypointFacingRight ||
            (jumpNodePosition.x < Sx + 0.5f + closestNodePosition.x) && !waypointFacingRight)
        {
            // if it is possible, we will begin to assign a waypoint at this jumpNode to ensure that we perform a dropdown at this jumpNode

            GraphNode dropdownAtThisNode;

            if (jumpNodePosition.x != jumpEndNodePosition.x) {
                dropdownAtThisNode = GridGraphGenerate.gg.GetNearest(new Vector3(jumpNodePosition.x + ((waypointFacingRight) ? 1f : -1f),
                    jumpNodePosition.y - 0.5f)).node;
            }
            else
            {
                bool directionFR = false;
                FindVaccantOverheadForOvershoot(jumpNode, ref directionFR);

                dropdownAtThisNode = GridGraphGenerate.gg.GetNearest(new Vector3(jumpNodePosition.x + ((directionFR) ? 1f : -1f),
                    jumpNodePosition.y - 0.5f)).node;
            }

            Vector3 dropdownAtThisNodePosition = (Vector3)dropdownAtThisNode.position;

            Vector2 direction = (waypointFacingRight) ? Vector2.right : Vector2.left;

            // creating a new special waypoint to state where a dropdown should be perform
            // check BaseAiController in order to better understand specialWaypoint initializer
            BaseAiController.specialWaypoint newSpecialWaypoint = new BaseAiController.specialWaypoint(
                typeofWaypoint.RUN, dropdownAtThisNode, () => { baseCharacterController.RunWaypointAI(direction); }, waypointFacingRight, 0.25f, null, null, true, false, false,
                false, false, true);

            if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
            {
                baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                specialWaypoints.Add(newSpecialWaypoint);
                // specialNodeCorrespFunction.Add(jumpEndNode);

                // ensuring that we will insert our dropdown waypoint into our new path at jumpNode's index
                WaypointInsertStruct newInsert = new WaypointInsertStruct(dropdownAtThisNode, jumpNode);
                waypointInserts.Add(newInsert);
            }

            // padding logic & trimming

            // determining an approximation as to where we land 
            List<GraphNode> potentialNodes = new List<GraphNode>();

            foreach (GraphNode node in adjNodes)
            {
                // cycling through adjNodes in jumpEndNode to find approximately landing node (these adjNodes also include jumpEndNode since I explicity inserted jumpEndNode as the first adjNode)
                Vector3 nodePosition = (Vector3)node.position;
                if ((jumpNodePosition.x + Sx > nodePosition.x && jumpNodePosition.x < nodePosition.x && waypointFacingRight) ||
                    (jumpNodePosition.x - Sx < nodePosition.x && jumpNodePosition.x > nodePosition.x && !waypointFacingRight))
                {

                    // checks if our new path list of nodes currently contains this specific node
                    if (!newNodes.Contains(node)) continue;

                    List<Vector2> buffer = new List<Vector2>();
                    buffer.Add(nodePosition);
                    buffer.Add(dropdownAtThisNodePosition);
                    GraphNode nodeOfCollision = null;
                    GraphNode nearNode = null;
                    bool collided = false;

                    // checks if the player would collide with other object if it was too perform a dropdown and travelled from dropdownAtThisNodePosition towards this approximated landing node
                    collided = BresenhamCollisionDetection(buffer, 0, ref nodeOfCollision, ref nearNode);
                    if (collided) continue;

                    // if no collision woud occur, add this approximated and potential landing node to a list
                    potentialNodes.Add(node);
                }
            }

            // selected the furthest potional landing node from the list, else if the list is empty, just select jumpEndNode
            GraphNode selectedNode = (potentialNodes.Count > 0) ? potentialNodes[potentialNodes.Count - 1] : jumpEndNode;
            Vector3 selectedNodePosition = (Vector3)selectedNode.position;


            // trims every path node from droopdownAtThisNode towards the selected landing node in order to clean up our path
            TrimRequestStruct newTrimRequest = new TrimRequestStruct((newNodes.Contains(dropdownAtThisNode))? dropdownAtThisNode : jumpNode, selectedNode);
            trimRequests.Add(newTrimRequest);

            // if our landing node is not apart of the nodes in our new path, we will have to insert our landing node into the new path
            if (potentialNodes.Count > 0 && !newNodes.Contains(potentialNodes[potentialNodes.Count - 1])) {
                int index = newNodes.FindIndex(d => d == jumpEndNode);
                newNodes.Insert(index - 1, potentialNodes[potentialNodes.Count - 1]);
                newVectorPath.Insert(index - 1, (Vector3)potentialNodes[potentialNodes.Count - 1].position);

                newNodes.RemoveAt(index);
                newVectorPath.RemoveAt(index);
            }

            float margin = 0f;

            // finds nodeGroup that we are standing on (nodeGroups defined above)
            GridGraphGenerate.NodeGroupStruct selectedNodeGroupStruct = GridGraphGenerate.FindThisNodesNodeGroup(selectedNode);

            if (selectedNode == selectedNodeGroupStruct.rightistNode) margin = +1f;
            else if (selectedNode == selectedNodeGroupStruct.leftistNode) margin = -1f;

            // halfway node is the node halfway between dropdownAtThisNode and the landing node. This is to better help guide the Ai during the process of dropping down
            // further reinforced with the existance of 'margin' as this helps with instances where the selected node is right at the edge of a platform. If margin didn't
            // exist, we will most likely just clip the edge of the platform and fall short of the dropdown
            GraphNode halfwayNode = GridGraphGenerate.gg.GetNearest(new Vector3(dropdownAtThisNodePosition.x + 
                ((Mathf.Abs(selectedNodePosition.x - dropdownAtThisNodePosition.x)) / 2) * ((waypointFacingRight)? 1 : -1)
                + margin,
                dropdownAtThisNodePosition.y - (Mathf.Abs(selectedNodePosition.y - dropdownAtThisNodePosition.y)) / 2)).node;

            // inserts this halfwayNode as a new special waypoint
            // check BaseAiController in order to better understand specialWaypoint initializer
            newSpecialWaypoint = new BaseAiController.specialWaypoint(
            typeofWaypoint.RUN, halfwayNode, () => { baseCharacterController.RunWaypointAI(direction); }, waypointFacingRight, 0.3f, null, null, true, false, false,
            false, false, true);

            if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
            {
                baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                specialWaypoints.Add(newSpecialWaypoint);

                // ensuring that we add this waypoint into our new path
                WaypointInsertStruct newInsert = new WaypointInsertStruct(halfwayNode, dropdownAtThisNode);
                waypointInserts.Add(newInsert);
            }

            // pads nodes around the landing area to in order to evenly space our Ai jumps, double jumps, dashes, etc. to make them feel more naturally performed
            List<GraphNode> paddingNodes = Helper.FindAdjacentNodes(jumpEndNode, ref foundAdjNodes,
                (waypointFacingRight) ? AdjNodeSearchDirection.RIGHT : AdjNodeSearchDirection.LEFT, 2 + potentialNodes.Count);
            paddingNodes.Add(jumpEndNode);

            PadTheseNodes(paddingNodes, false);

            return true;
        }
        else
        {
            // ...
        }

        // could not perform a dropdown
        return false;
    }

    // Calculates if is possible to form a dropdown into a single jump from jumpNode towards jumpEndNode
    private bool CalculateSingleDropdown(GraphNode jumpNode, GraphNode jumpEndNode)
    {
        Vector3 jumpNodePosition = (Vector3)jumpNode.position;
        Vector3 jumpEndNodePosition = (Vector3)jumpEndNode.position;

        GraphNode closestNode = jumpEndNode; // (jumpNodePosition, adjNodes);
        Vector3 closestNodePosition = (Vector3)closestNode.position;

        bool waypointFacingRight = false;
        if (jumpEndNodePosition.x > jumpNodePosition.x)
        {
            waypointFacingRight = true;
        }

        // determines whether the Ai will need to hold jump to form a higher jump in order to reach jumpEndNode
        bool Ai_holdSpaceKey = false;
        if ((airborneJumpHeight + jumpNodePosition.y < jumpEndNodePosition.y && maxAirborneJumpHeight + jumpNodePosition.y >= jumpEndNodePosition.y) ||
            (Mathf.Abs(jumpEndNodePosition.x - jumpNodePosition.x) > 8.5f))
        {
            Ai_holdSpaceKey = true;
        }

        waypointFacingRight = false;

        if (jumpEndNodePosition.x > jumpNodePosition.x)
        {
            waypointFacingRight = true;
        }


        // entire section uses kinematic equations to determine if it is possible to dropdown and perform a single jump to reach jumpEndNode
        float Sy;
        float t_fall;
        float Sx;

        Sy = jumpEndNodePosition.y - jumpNodePosition.y;

        float airborneVyi = GetAirborneVyi();


        float Sz = 3f;
        if (jumpNodePosition.y <= jumpEndNodePosition.y) Sz = 0f;

        float Sb = ((Ai_holdSpaceKey) ? maxAirborneJumpHeight - 0.5f : airborneJumpHeight) - Sy - Sz;
        float t_dropdown = Mathf.Sqrt(Mathf.Abs(2 * Sb / gravityFall));
        float t_rise = 2 * ((Ai_holdSpaceKey) ? maxAirborneJumpHeight : airborneJumpHeight) / airborneVyi;
        t_fall = Mathf.Sqrt(2 * Sz / gravityFall);
        float t_total = t_dropdown + t_rise;

        Sx = t_total * Vx;
        if (Sx > 6f) return false; // typically, in this case, Sx is just to far to each reach if we were to perform a dropdown into a single jump
        else
        {
            // Debug.Log(Sx);
        }

        // Determines if is possible to reach jumpEndNode by performing a dropdown into a single jump whilst allowing for a bit of overshoot
        if ((jumpNodePosition.x < -Sx + closestNodePosition.x + 3f) && waypointFacingRight ||
            (jumpNodePosition.x > Sx + closestNodePosition.x - 3f) && !waypointFacingRight)
        {
            // if it is possible to reach jumpEndNode by performing a dropdown into a single jump,
            // we go through the respective process of assigning the corresponding waypoints into our new path

            // Adding dropdown waypoint at jumpNode
            GraphNode dropdownAtThisNode = jumpNode;
            Vector3 dropdownAtThisNodePosition = (Vector3)dropdownAtThisNode.position;
            
            // dropdownAtThisNodeGG stands for dropdownAtThisNode "Grid Graph" and Helper.TurnPositionIntoPointOnGridGraph is defined well in Helper.cs
            Vector2 dropdownAtThisNodeGG = Helper.TurnPositionIntoPointOnGridGraph(GridGraphGenerate.gg,
                GridGraphGenerate.gg.GetNearest(new Vector3(jumpNodePosition.x + (Vx * t_dropdown), -Sb + jumpNodePosition.y)).node);

            // states that this jumpNode has been processed (we don't have to do this in every instance)
            if (!jumpNodesFinal.Contains(dropdownAtThisNode))
            {
                jumpNodesFinal.Add(dropdownAtThisNode);
            }

            // determing the specific node that we will be performing our airborne jump
            GraphNode jumpAtThisNode = GridGraphGenerate.gg.GetNearest(new Vector3(jumpNodePosition.x + (Vx * t_dropdown * ((waypointFacingRight) ? 1 : -1)) +
                ((waypointFacingRight) ? 1f : -1f), ((jumpNodePosition.y <= jumpEndNodePosition.y)? -Sb : -Sb) + jumpNodePosition.y)).node; // what is with this pointless tenary operator again?

            Vector2 direction = (waypointFacingRight) ? Vector2.right : Vector2.left;
            Vector3 jumpAtThisNodePosition = (Vector3)jumpAtThisNode.position;

            GraphNode nodeOfCollision = null;
            GraphNode nearNode = null;

            List<Vector2> points = new List<Vector2>();
            points.Add(jumpAtThisNodePosition);
            points.Add(jumpEndNodePosition);

            // checks if we will collide with any object between the airborne jump towards the jumpEnd
            bool collided = BresenhamCollisionDetection(points, 2, ref nodeOfCollision, ref nearNode);
            float padding = 0f;
            
            // if we collided, then we will have to form a new airborne jump node...
            if (collided && waypointFacingRight)
            {
                padding = -1.5f;
                GraphNode newJumpAtThisNode = GridGraphGenerate.gg.GetNearest(new Vector3(jumpNodePosition.x + padding + (Vx * t_dropdown * ((waypointFacingRight) ? 1 : -1)) +
                ((waypointFacingRight) ? 1f : -1f), ((jumpNodePosition.y <= jumpEndNodePosition.y) ? -Sb : -Sb) + jumpNodePosition.y)).node; // what is with this pointless tenary operator again?

                if (newJumpAtThisNode.Walkable)
                {
                    jumpAtThisNode = newJumpAtThisNode;
                }
            }
            else if (collided && !waypointFacingRight)
            {
                padding = 1.5f;
                GraphNode newJumpAtThisNode = GridGraphGenerate.gg.GetNearest(new Vector3(jumpNodePosition.x + padding + (Vx * t_dropdown * ((waypointFacingRight) ? 1 : -1)) +
                ((waypointFacingRight) ? 1f : -1f), ((jumpNodePosition.y <= jumpEndNodePosition.y) ? -Sb : -Sb) + jumpNodePosition.y)).node; // what is with this pointless tenary operator again?

                if (newJumpAtThisNode.Walkable)
                {
                    jumpAtThisNode = newJumpAtThisNode;
                }
            }

            // Adding RUN special waypoint as this will be the node that we are dropping down from
            BaseAiController.specialWaypoint newSpecialWaypoint = new BaseAiController.specialWaypoint(
                typeofWaypoint.RUN, jumpNode, () => { baseCharacterController.RunWaypointAI(direction); }, waypointFacingRight);

            if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
            {
                baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                specialWaypoints.Add(newSpecialWaypoint);
            }

            // Adding JUMP special waypoint as this will be where we will be performing an airborne jump
            newSpecialWaypoint = new BaseAiController.specialWaypoint(
                typeofWaypoint.AIRBORNE_JUMP, jumpAtThisNode, () => { baseCharacterController.JumpWaypointAI(Ai_holdSpaceKey); }, waypointFacingRight, 0.7f,
                jumpNode, jumpEndNode);

            if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
            {
                baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                specialWaypoints.Add(newSpecialWaypoint);

                // ensuring that we are inserting our airborne jump waypoint into our new path
                WaypointInsertStruct newInsert = new WaypointInsertStruct(newSpecialWaypoint.node, jumpNode);
                waypointInserts.Add(newInsert);
            }

            // adding padding & trimming to clean up and let other subsequent jumps be evenly space so they appear more natural
            TrimRequestStruct newTrimRequest = new TrimRequestStruct(jumpNode, closestNode);
            trimRequests.Add(newTrimRequest);

            bool foundAdjNodes = false;
            List<GraphNode> paddingNodes = Helper.FindAdjacentNodes(closestNode, ref foundAdjNodes, (waypointFacingRight) ? AdjNodeSearchDirection.RIGHT : AdjNodeSearchDirection.LEFT, 2);
            PadTheseNodes(paddingNodes, false);
            return true;
        }
        else
        {
            // ...
        }

        return false;
    }

    // Calculates if is possible to form a double jump from jumpNode towards jumpEndNode

    /* IMPORTANT!!!!!!!!!*/
    // Head to CalculateOveshootWaypoints_S() to further understand the importance of the OveshootWaypoints_S jump
    private bool CalculateDoubleJump(GraphNode jumpNode, GraphNode jumpEndNode)
    {
        Vector3 jumpNodePosition = (Vector3)jumpNode.position;
        Vector3 jumpEndNodePosition = (Vector3)jumpEndNode.position;


        bool waypointFacingRight = false;

        // used to determine if it is possible to form a "OveshootWaypoints_S jump" maneueavr if it is impossible to form a typical double jump 
        bool isCapableOfOverhead = false;

        // should the Ai hold the jump key to form higher jumps?
        bool Ai_holdSpaceKey = false;

        if (jumpEndNodePosition.x > jumpNodePosition.x)
        {
            waypointFacingRight = true;
        }

        bool foundAdjNodes = false;

        GraphNode closestNode = FindClosestNode(jumpEndNodePosition, Helper.FindAdjacentNodes(jumpEndNode, ref foundAdjNodes, AdjNodeSearchDirection.BOTH));
        Vector3 closestNodePosition = (Vector3)closestNode.position;

        List<GraphNode> adjNodesAtJump = Helper.FindAdjacentNodes(jumpNode, ref foundAdjNodes, AdjNodeSearchDirection.BOTH);
        adjNodesAtJump.Add(jumpNode);

        // defining some variables used later to form kinematic equations to check whether a double jump is possible from jumpNode to jumpEndNode
        float airborneVyi = GetAirborneVyi();

        float t_rise1 = t_rise;
        float t_rise2 = 2 * GetAirborneJumpHeight() / airborneVyi;
        float Sy;
        float Sz;
        float t_fall1;
        float t_fall2;
        float t_total;

        bool foundAnyPotentialNodes = false;

        // stores potential nodes were a double jump could be performed from to reach jumpEndNode
        List<GraphNode> potentialNodes = new List<GraphNode>();

        // cycling through each adjNodesAtJump to assess if its possible to from a double jump to reach jumpEndNode
        foreach (GraphNode node in adjNodesAtJump)
        {
            Vector3 nodePosition = (Vector3)node.position;

            Sy = jumpEndNodePosition.y - nodePosition.y;

            // 'Sz' represents how high the apex of the second jump should be from the jumpEndNode
            Sz = 3f;

            if ((jumpHeight + airborneJumpHeight < Sy || (Mathf.Abs(nodePosition.x - jumpEndNodePosition.x) > 11.5f) && Sy > 0f) && maxJumpHeight + maxAirborneJumpHeight >= Sy)
            {
                // Ai will hold jump key in order to form higher jumps
                Ai_holdSpaceKey = true;
            }

            if (maxJumpHeight + maxAirborneJumpHeight < Sy)
            {
                // cannot perform doubleJump with this current node
                continue;
            }
            else while (true)
                {
                    // if necessary, reduce Sz if current Sz is impossible to work with...

                    if (Ai_holdSpaceKey)
                    {
                        if (maxJumpHeight + maxAirborneJumpHeight - Sz < Sy)
                        {
                            Sz -= 0.5f;
                        }
                        else break;
                    }
                    else
                    {
                        if (jumpHeight + airborneJumpHeight - Sz < Sy)
                        {
                            Sz -= 0.5f;
                        }
                        else break;
                    }
                }

            t_fall1 = Mathf.Sqrt(2 * (((Ai_holdSpaceKey)? maxJumpHeight + maxAirborneJumpHeight : airborneJumpHeight + jumpHeight) - Sy - Sz) / gravityFall);

            t_fall2 = Mathf.Sqrt(2 * Sz / gravityFall);
            t_total = ((Ai_holdSpaceKey) ? t_maxRise : t_rise) + ((Ai_holdSpaceKey) ? t_maxAirborneRise : t_airborneRise) + t_fall2;

            float Sx = t_total * Vx;

            // used to determine if it is possible to form a "waypointS" maneueavr
            if (maxJumpHeight + maxAirborneJumpHeight >= Sy && 
                (((nodePosition.x > -Sx + jumpEndNodePosition.x) && waypointFacingRight) ||
                ((nodePosition.x > Sx + jumpEndNodePosition.x) && !waypointFacingRight))){

                isCapableOfOverhead = true;
            }

            // checking if this node can be used to form a double jump towards jumpEndNode
            if ((nodePosition.x < -Sx + jumpEndNodePosition.x + 0.25f) && waypointFacingRight ||
                (nodePosition.x > Sx + jumpEndNodePosition.x - 0.25f) && !waypointFacingRight)
            {
                // checks if or list of new nodes that will form our new path currently contains this node that we are anticipating to form a double jump from
                if (!newNodes.Contains(node)) continue;

                if (maxJumpHeight + maxAirborneJumpHeight >= Sy)
                {
                    float Sx_Dropdown = Vx * (((Ai_holdSpaceKey) ? t_maxRise : t_rise) + t_fall1) * ((waypointFacingRight) ? 1 : -1);
                    float Sy_Dropdown = ((Ai_holdSpaceKey) ? maxJumpHeight : jumpHeight) - ((gravityFall * t_fall1 * t_fall1) / 2);

                    Vector2 secondJumpAtThisNodePosition = new Vector2(Sx_Dropdown + nodePosition.x, Sy_Dropdown + nodePosition.y);

                    GraphNode nodeOfCollision = null;
                    GraphNode nearNode = null;

                    // simulating airborne jumps to check whether we will collide with objects during our double jump
                    bool collided = BresenhamCollisionDetection(SimulateSingleJump(nodePosition, secondJumpAtThisNodePosition, waypointFacingRight, 4, false, 0, Ai_holdSpaceKey), 3, ref nodeOfCollision, ref nearNode, false);
                    if (collided) continue;

                    collided = BresenhamCollisionDetection(SimulateSingleJump(secondJumpAtThisNodePosition, jumpEndNodePosition, waypointFacingRight, 4, true, 0, Ai_holdSpaceKey), 3, ref nodeOfCollision, ref nearNode, false);
                    if (collided) continue;

                    // node has passed the critrea to form a double jump, so we will add it to the potentialNodes we can select from
                    foundAnyPotentialNodes = true;
                    potentialNodes.Add(node);
                    continue;
                }
            }
            else
            {
                if(maxJumpHeight + maxAirborneJumpHeight >= Sy)
                {
                    // used to determine if it is possible to form a "OveshootWaypoints_S jump" maneueavr
                    isCapableOfOverhead = true;
                }
            }
        }

        if (!foundAnyPotentialNodes)
        {
            // if we determined that it was impossible to form a double jump, try to get ready to perform a OveshootWaypoints_S jump in which we 
            // overshoot and return back towards our jumpEndNode in order to curve around obstacles

            if (isCapableOfOverhead)
            {
                // directionOfVaccantNodeFR is essentially used to determine which direction we will be overshooting by trying to find
                // a vaccant air node next to the jumpEndNode (that is a grounded node). Determining the direction from the jumpEndNode towards
                // the vaccant air node is how we assess which direction we will be overshooting...
                bool directionOfVaccantNodeFR = false;
                FindVaccantOverheadForOvershoot(jumpEndNode, ref directionOfVaccantNodeFR);

                // temp flag that turn true if we will flip the direction that we overshoot
                bool flag = false;

                GraphNode temp = null;
                Vector2 direction = Vector2.zero;

                int index = newNodes.FindIndex(d => d == jumpNode);
                if (index == -1 || index - 1 < 0) flag = true;
                else
                {
                    // Debug.Log(index);
                    temp = newNodes[index - 1];
                    direction = GetDirectionOfNode(jumpNode, temp);
                }

                // towardsJumpNodeFR states whether the jumpNode is facing right relative to the node (apart of the Ai's path) leading up to the jumpNode
                bool towardsJumpNodeFR = false;

                if (direction == Vector2.right)
                {
                    towardsJumpNodeFR = true;
                }

                // Performs an OvershootWaypoints_S jump instead since it could not perform a double jump. OvershootWaypoints_S returns true if it could be performed, else
                // returns false
                return CalculateOvershootWaypoints_S(jumpEndNode, jumpNode, 1.5f, (flag || directionOfVaccantNodeFR != towardsJumpNodeFR) ? 0:2, TypeofJump.DOUBLE_JUMP, 
                    (flag || directionOfVaccantNodeFR != towardsJumpNodeFR)? true : false);

            }

            // returns false since it could perform a double jump, nor had the chance to form a OvershootWaypoints_S jump
            return false;
        }

        if (potentialNodes.Count >= 1)
        {
            GraphNode jumpAtThisNode = FindClosestNode(jumpNodePosition, potentialNodes);
            Vector3 jumpAtThisNodePosition = (Vector3)jumpAtThisNode.position;

            Sy = jumpEndNodePosition.y - jumpAtThisNodePosition.y;

            // 'Sz' represents how high the apex of the second jump should be from the jumpEndNode
            Sz = 2f;

            if (((Ai_holdSpaceKey) ? maxJumpHeight + maxAirborneJumpHeight : airborneJumpHeight + jumpHeight) - Sz < Sy)
            {
                Sz = Sy - (((Ai_holdSpaceKey) ? maxJumpHeight + maxAirborneJumpHeight : airborneJumpHeight + jumpHeight) - Sz);
            }

            if (jumpHeight + airborneJumpHeight < Sy && maxJumpHeight + maxAirborneJumpHeight >= Sy)
            {
                Ai_holdSpaceKey = true;
            }

            t_fall1 = Mathf.Sqrt(2 * (((Ai_holdSpaceKey) ? maxJumpHeight + maxAirborneJumpHeight : airborneJumpHeight + jumpHeight) - Sy - Sz) / gravityFall);

            t_fall2 = Mathf.Sqrt(2 * Sz / gravityFall);
            t_total = ((Ai_holdSpaceKey) ? t_maxRise : t_rise) + ((Ai_holdSpaceKey) ? t_maxAirborneRise : t_airborneRise) + t_fall2;

            // Finding position of where we need to form the airborne jump
            float Sx_Dropdown = Vx * (((Ai_holdSpaceKey) ? t_maxRise : t_rise) + t_fall1) * ((waypointFacingRight) ? 1 : -1);
            float Sy_Dropdown = ((Ai_holdSpaceKey) ? maxJumpHeight : jumpHeight) - ((gravityFall * t_fall1 * t_fall1) / 2);
            GraphNode secondJumpAtThisNode = GridGraphGenerate.gg.GetNearest(new Vector3(Sx_Dropdown + jumpAtThisNodePosition.x, Sy_Dropdown + jumpAtThisNodePosition.y)).node;

            // states that this jumpNode has been processed (we don't have to do this in every instance)
            if (!jumpNodesFinal.Contains(jumpAtThisNode))
            {
                jumpNodesFinal.Add(jumpAtThisNode);
                
                // adds our first jump as a waypoint that will be apart of our of our new path
                BaseAiController.specialWaypoint newSpecialWaypoint = new BaseAiController.specialWaypoint(
                typeofWaypoint.JUMP, jumpAtThisNode, () => { baseCharacterController.JumpWaypointAI(Ai_holdSpaceKey); }, waypointFacingRight, 0.25f, 
                jumpNode, jumpEndNode);

                if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
                {
                    baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                    specialWaypoints.Add(newSpecialWaypoint);
                }
            }

            // states that this jumpNode has been processed (we don't have to do this in every instance)
            if (!jumpNodesFinal.Contains(secondJumpAtThisNode))
            {
                // adds our airborne jump as a waypoint that will be apart of our new path
                BaseAiController.specialWaypoint newSpecialWaypoint = new BaseAiController.specialWaypoint(
                typeofWaypoint.AIRBORNE_JUMP, secondJumpAtThisNode, () => { baseCharacterController.JumpWaypointAI(Ai_holdSpaceKey); }, waypointFacingRight, 0.6f,
                jumpNode, jumpEndNode);

                WaypointInsertStruct newInsert = new WaypointInsertStruct(newSpecialWaypoint.node, jumpAtThisNode);
                waypointInserts.Add(newInsert);

                if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
                {
                    baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                    specialWaypoints.Add(newSpecialWaypoint);
                }
            }

            // trimming to clean up our double jump
            TrimRequestStruct newTrimRequest = new TrimRequestStruct(jumpAtThisNode, jumpEndNode);
            trimRequests.Add(newTrimRequest);

            // adds some padding towards our jumpEndNode so the next subsequent jump can be evenly spaced from this double jump in order to make the Ai's pathing feel more natural
            List<GraphNode> paddingNodes = Helper.FindAdjacentNodes(jumpEndNode, ref foundAdjNodes, (waypointFacingRight) ? AdjNodeSearchDirection.RIGHT : AdjNodeSearchDirection.LEFT, 4);
            PadTheseNodes(paddingNodes, false);

            return true;
        }

        // could not perform a double jump, nor even a OvershootWaypoints_S jump (stated above)
        return false;
    }

    // Calculates if is possible to form a single jump from jumpNode towards jumpEndNode
    private bool CalculateSingleJump(GraphNode jumpNode, GraphNode jumpEndNode, GraphNode nextJumpNode, GraphNode nextJumpEndNode, bool masterCall = true)
    {

        // Sees if we can calculate a single jump towards the next jumpEndNode instead
        if (masterCall == true)
        {
            if (CalculateSingleJump(jumpNode, nextJumpEndNode, null, null, false))
            {
                // was able to form a single jump towards the next jumpEndNode instead
                // so we have to make sure that we ignore the nextJumpNode since we have overridden their request to calculate a jump
                ignoreJumpNodes.Add(nextJumpNode);
                return true;
            }
        }
        

        Vector3 jumpEndNodePosition = (Vector3)jumpEndNode.position;
        Vector3 jumpNodePosition = (Vector3)jumpNode.position;

        // used to determine if we need to hold the jump key to form higher jumps
        bool Ai_holdSpaceKey = false;

        if ((jumpEndNodePosition.y - jumpNodePosition.y > jumpHeight || Mathf.Abs(jumpNodePosition.x - jumpEndNodePosition.x) > 9.5f) && maxJumpHeight >= jumpEndNodePosition.y - jumpNodePosition.y)
        {
            Ai_holdSpaceKey = true;
        }
        else if (maxJumpHeight < jumpEndNodePosition.y - jumpNodePosition.y)
        {
            // impossible to form a single jump towards the jumpEndNode
            return false;
        }

        bool waypointFacingRight = false;
        if (jumpEndNodePosition.x > jumpNodePosition.x)
        {
            waypointFacingRight = true;
        }

        GraphNode jumpAtThisNode = null;

        GraphNode currentTargetNode = jumpEndNode;
        Vector3 currentTargetNodePosition = jumpEndNodePosition;

        List<GraphNode> adjNodes = new List<GraphNode>();
        List<GraphNode> adjTargetNodes = new List<GraphNode>();

        bool foundAdjNodes = false;
        if (waypointFacingRight)
        {
            adjNodes = Helper.FindAdjacentNodes(jumpNode, ref foundAdjNodes, AdjNodeSearchDirection.LEFT);
            adjTargetNodes = Helper.FindAdjacentNodes(jumpEndNode, ref foundAdjNodes, AdjNodeSearchDirection.RIGHT);
        }
        else
        {
            adjNodes = Helper.FindAdjacentNodes(jumpNode, ref foundAdjNodes, AdjNodeSearchDirection.RIGHT);
            adjTargetNodes = Helper.FindAdjacentNodes(jumpEndNode, ref foundAdjNodes, AdjNodeSearchDirection.LEFT);
        }

        adjNodes.Insert(0, jumpNode); // just for convenicene when using foreach loop
        adjNodes.Reverse();

        bool willSingleJump = false;

        // for debugging purposes...
        int index = 0;
        Color[] colors = { Color.red, Color.green, Color.blue, Color.magenta, Color.cyan };

        // bool that states whether all nodes have been assessed as impossible to form a single jump towards jumpEndNode
        // thus we are only left to assess the padding nodes (padding nodes and the concept of padding is defined above)

        bool onlyPaddingLeft = false;

        // stores list of all encountered padding nodes (that may be cycled through once onlyPaddingLeft turns true)
        List<GraphNode> wasPaddingNode = new List<GraphNode>();

        GraphNode landingNode = null;

        while (!willSingleJump)
        {
            if (onlyPaddingLeft)
            {

                // assessing if a node, that was specifically apart of the padding nodes, can be used to form a single jump towards the jumpEndNode
                foreach (GraphNode node in wasPaddingNode)
                {
                    Vector3 nodePosition = (Vector3)node.position;

                    float Sy_fall = ((Ai_holdSpaceKey)? maxJumpHeight : jumpHeight) - (currentTargetNodePosition.y - nodePosition.y);
                    float t_fall = Mathf.Sqrt(Mathf.Abs(2 * Sy_fall / gravityFall));
                    float Sx = Vx * (((Ai_holdSpaceKey) ? t_maxRise : t_rise) + Mathf.Sqrt(Mathf.Abs(2 * Sy_fall / gravityFall))) * ((waypointFacingRight == false) ? 1 : -1);

                    if ((Sx + currentTargetNodePosition.x - 0.5f > nodePosition.x && Sx + currentTargetNodePosition.x - 1f < nodePosition.x && !waypointFacingRight) || // ?
                            (Sx + currentTargetNodePosition.x + 0.5f < nodePosition.x && Sx + currentTargetNodePosition.x + 1f > nodePosition.x  && waypointFacingRight))
                    {
                        // possible to form a single jump towards jumpEndNode from this node

                        jumpAtThisNode = node;

                        // preparing special waypoint that will be added to our new path as a node to jump from
                        BaseAiController.specialWaypoint newSpecialWaypoint = new BaseAiController.specialWaypoint(
                            typeofWaypoint.JUMP, jumpAtThisNode, () => { baseCharacterController.JumpWaypointAI(Ai_holdSpaceKey); }, waypointFacingRight, 0.5f,
                            jumpNode, currentTargetNode);

                        // simulating a single jump to determine if the single jump towards the jumpEndNode will collide with any obstacle along the way
                        GraphNode nodeOfCollision = null;
                        GraphNode nearNode = null;
                        bool collided = BresenhamCollisionDetection(SimulateSingleJump(nodePosition, currentTargetNodePosition, waypointFacingRight, 4, false, 0, Ai_holdSpaceKey), 3, ref nodeOfCollision, ref nearNode);

                        if (!collided)
                        {
                            // states that this jumpNode has been processed (we don't have to do this in every instance)
                            if (!jumpNodesFinal.Contains(jumpAtThisNode))
                            {
                                jumpNodesFinal.Add(jumpAtThisNode);
                            }

                            if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
                            {
                                baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                                specialWaypoints.Add(newSpecialWaypoint);
                            }

                            landingNode = currentTargetNode;
                            willSingleJump = true;
                            break;
                        }
                    }
                }
            }
            else
            {
                // assessing if a node that was adjacent to our jumpNode can be used to form a single jump towards the jumpEndNode
                foreach (GraphNode node in adjNodes)
                {
                    // can optimize this by not scanning padding over and over again when I would only need to scan them once to identify them.
                    // thus, ignoring them in the next scan. However, this presents virtually no harm to the performance of the game...

                    if (IsOverlappingWithPadding(node))
                    {
                        if (!wasPaddingNode.Contains(node))
                            wasPaddingNode.Add(node);

                        // ensures that we will not jump from a padding node if we don't have to resort to jumping from a padding node
                        continue;
                    }

                    Vector3 nodePosition = (Vector3)node.position;

                    float Sy_fall = ((Ai_holdSpaceKey) ? maxJumpHeight : jumpHeight) - (currentTargetNodePosition.y - nodePosition.y);
                    float t_fall = Mathf.Sqrt(Mathf.Abs(2 * Sy_fall / gravityFall));
                    float Sx = Vx * (((Ai_holdSpaceKey) ? t_maxRise : t_rise) + Mathf.Sqrt(Mathf.Abs(2 * Sy_fall / gravityFall))) * ((waypointFacingRight == false) ? 1 : -1);

                    if ((Sx + currentTargetNodePosition.x - 0.5f > nodePosition.x && Sx + currentTargetNodePosition.x - 1f < nodePosition.x && !waypointFacingRight) ||
                            (Sx + currentTargetNodePosition.x + 0.5f < nodePosition.x && Sx + currentTargetNodePosition.x + 1f > nodePosition.x && waypointFacingRight))
                    {
                        // possible to form a single jump towards jumpEndNode from this node

                        jumpAtThisNode = node;
                        Vector3 jumpAtThisNodePosition = (Vector3)jumpAtThisNode.position;

                        // naucned case in which the jumping from this node will be problematic, so we decide to skip over it
                        if (jumpAtThisNodePosition.x < baseCharacterController.transform.position.x && waypointFacingRight) continue;
                        else if (jumpAtThisNodePosition.x > baseCharacterController.transform.position.x && !waypointFacingRight) continue;

                        // preparing special waypoint that will be added to our new path as a node to jump from
                        BaseAiController.specialWaypoint newSpecialWaypoint = new BaseAiController.specialWaypoint(
                            typeofWaypoint.JUMP, jumpAtThisNode, () => { baseCharacterController.JumpWaypointAI(Ai_holdSpaceKey); }, waypointFacingRight, 0.5f,
                            jumpNode, currentTargetNode);

                        // simulating a single jump to determine if the single jump towards the jumpEndNode will collide with any obstacle along the way
                        GraphNode nodeOfCollision = null;
                        GraphNode nearNode = null;
                        bool collided = BresenhamCollisionDetection(SimulateSingleJump(nodePosition, currentTargetNodePosition, waypointFacingRight, 4, false, 0, Ai_holdSpaceKey), 3, ref nodeOfCollision, ref nearNode);

                        if (!collided)
                        {
                            // states that this jumpNode has been processed (we don't have to do this in every instance)
                            if (!jumpNodesFinal.Contains(jumpAtThisNode))
                            {
                                jumpNodesFinal.Add(jumpAtThisNode);
                            }

                            if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
                            {
                                baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                                specialWaypoints.Add(newSpecialWaypoint);
                            }

                            landingNode = currentTargetNode;
                            willSingleJump = true;
                            break;
                        }
                    }
                }
            }

            // Used to assess whether we may be forced to jump from padding nodes (padding nodes and the concept of padding to defined above)
            if (index == adjTargetNodes.Count || adjTargetNodes.Count == 0)
            {
                if (!onlyPaddingLeft)
                {
                    onlyPaddingLeft = true;
                    currentTargetNode = jumpEndNode;
                    currentTargetNodePosition = jumpEndNodePosition;
                    index = 0;
                }
                else
                {
                    // break from this loop as it is impossible to form a single jump
                    break;
                }
            }
            else
            {
                // debugging by drawing arrows above nodes that we have cycled through to assess were we can perform a single jump in order to reach the jumpEndNode
                currentTargetNode = adjTargetNodes[index];
                currentTargetNodePosition = (Vector3)adjTargetNodes[index].position;
                int colorIndex = index;
                if (colorIndex > colors.Length - 1) colorIndex -= colors.Length * (Mathf.FloorToInt((float)colorIndex / (float)colors.Length));

                Helper.DrawArrow.ForDebugTimed(currentTargetNodePosition + Vector3.up * 1f, Vector3.down, colors[colorIndex], 1000f);
                index++;
            }

        }

        // can and will perform a singleJump
        if (willSingleJump)
        {
            // trimming & padding to clean up new path and provide padding that will evenly space out subsequent jumps so they appear more natural
            TrimRequestStruct newTrimRequest = new TrimRequestStruct(jumpAtThisNode, ((newNodes.Contains(landingNode))? landingNode : jumpEndNode));
            trimRequests.Add(newTrimRequest);

            List<GraphNode> paddingNodes = Helper.FindAdjacentNodes(jumpEndNode, ref foundAdjNodes, (waypointFacingRight) ? AdjNodeSearchDirection.RIGHT : AdjNodeSearchDirection.LEFT, 2, landingNode);
            PadTheseNodes(paddingNodes, false);

            return true;
        }
        else
        {
            // impossible to form a single jump from jumpNode towards jumpEndNode
            return false;
        }
    }

    // Works but could be optimized to get rid of some loops...
    private bool CalculateSingleJumpDashing(GraphNode jumpNode, GraphNode jumpEndNode)
    {
        Vector3 jumpNodePosition = (Vector3)jumpNode.position;
        Vector3 jumpEndNodePosition = (Vector3)jumpEndNode.position;

        // states whether we need to hold the jump key to form a higher jump
        bool Ai_holdSpaceKey = false;
        if (jumpEndNodePosition.y - jumpNodePosition.y > jumpHeight && maxJumpHeight >= jumpEndNodePosition.y - jumpNodePosition.y)
        {
            Ai_holdSpaceKey = true;
        }
        else if (maxJumpHeight < jumpEndNodePosition.y - jumpNodePosition.y)
        {
            // impossible to form a single jump into a lateral dash
            return false;
        }

        bool waypointFacingRight = false;
        
        if (jumpEndNodePosition.x > jumpNodePosition.x)
        {
            waypointFacingRight = true;
        }
        
        bool foundAdjNodes = false;
        GraphNode closestNode = FindClosestNode(jumpNodePosition, Helper.FindAdjacentNodes(jumpEndNode, ref foundAdjNodes, AdjNodeSearchDirection.BOTH));
        Vector3 closestNodePosition = (Vector3)closestNode.position;
        
        List<GraphNode> adjNodesAtJump = Helper.FindAdjacentNodes(jumpNode, ref foundAdjNodes, (waypointFacingRight)? AdjNodeSearchDirection.LEFT : AdjNodeSearchDirection.RIGHT);
        adjNodesAtJump.Insert(0, jumpNode);
        
        // defining variables that will be used to assess if we can perform a single jump into a lateral dash to reach our jumpEndNode
        float SxDash = baseCharacterController.dodgeDistance;
        float SxRise = ((Ai_holdSpaceKey) ? t_maxRise : t_rise) * Vx;
        
        List<GraphNode> potentialNodes = new List<GraphNode>();
        bool foundAnyPotentialNode = false;
        
        float Sz;
        float Sy;
        float Sb;
        
        float t_fall;
        float t_total;
        
        foreach (GraphNode node in adjNodesAtJump)
        {
            Vector3 nodePosition = (Vector3)node.position;

            if ((jumpEndNodePosition.x - nodePosition.x) > SxRise + SxDash)
            {
                // impossible to perform so we skip this node
                continue;
            }
            else
            {
                // ...
            }

            // 'Sz' represents how high the apex of the second jump should be from the jumpEndNode
            Sz = 1.5f;
            Sy = jumpEndNodePosition.y - nodePosition.y;

            // if necessary, reduce Sz if current Sz is impossible to work with...
            if (Ai_holdSpaceKey)
            {
                if (maxJumpHeight - Sz < Sy && maxJumpHeight >= Sy)
                {
                    Sz = Sy - ((maxJumpHeight) - Sz);
                }
            }
            else
            {
                if (jumpHeight - Sz < Sy && jumpHeight >= Sy)
                {
                    Sz = Sy - ((jumpHeight) - Sz);
                }
            }

            Sb = ((Ai_holdSpaceKey) ? maxJumpHeight : jumpHeight) - Sz - Sy;
        
            t_fall = Mathf.Sqrt(Mathf.Abs(2 * Sb / gravityFall));
            t_total = ((Ai_holdSpaceKey) ? t_maxRise : t_rise) + t_fall;
        
            float Sx = t_total * Vx + SxDash;
        
            if (((nodePosition.x > -Sx + jumpEndNodePosition.x) && waypointFacingRight) ||
                ((nodePosition.x < Sx + jumpEndNodePosition.x) && !waypointFacingRight))
            {
                // it is possible to form a single jump into a lateral dash from this node towards the jumpEndNode
                foundAnyPotentialNode = true;
        
                // if we want to be more performant. We can just stop our search once we find one node potential node since
                // Helper.FindAdjacentNodes can form directional seraches...
        
                potentialNodes.Add(node);
                continue;
            }
            else
            {
                // ...
            }
        
        }

        if (!foundAnyPotentialNode)
        {
            // impossible to form a single jump into a lateral dash from this node towards the jumpEndNode
            return false;
        }

        // At this point and onwards, it is possible to perform a single jump + dash

        foreach (GraphNode node in potentialNodes)
        {
            GraphNode jumpAtThisNode = node;
            Vector3 jumpAtThisNodePosition = (Vector3)jumpAtThisNode.position;

            // getting ready to add a special waypoint in the form of a jump at jumpAtThisNode
            BaseAiController.specialWaypoint newJumpSpecialWaypoint = new BaseAiController.specialWaypoint(
                typeofWaypoint.JUMP, jumpAtThisNode, () => { baseCharacterController.JumpWaypointAI(Ai_holdSpaceKey); }, waypointFacingRight, 0.25f, jumpNode, jumpEndNode);

            GraphNode nodeOfCollision = null;
            GraphNode nearNode = null;

            // 'Sz' represents how high the apex of the second jump should be from the jumpEndNode
            Sz = 1.5f; 

            Sy = jumpEndNodePosition.y - jumpAtThisNodePosition.y;

            // if necessary, reduce Sz if current Sz is impossible to work with...
            while (true)
            {
                if (Ai_holdSpaceKey)
                {
                    if (maxJumpHeight - Sz < Sy)
                    {
                        Sz -= 0.5f;
                    }
                    else break;
                }
                else
                {
                    if (jumpHeight - Sz < Sy)
                    {
                        Sz -= 0.5f;
                    }
                    else break;
                }
            }

            Sb = ((Ai_holdSpaceKey) ? maxJumpHeight : jumpHeight) - Sz - Sy;

            t_fall = Mathf.Sqrt(Mathf.Abs(2 * Sb / gravityFall));
            t_total = ((Ai_holdSpaceKey) ? t_maxRise : t_rise) + baseCharacterController.dodgeTime + t_fall;

            // Adding single jump waypoint
            // Finding the position where we need to jump;
            float Sx_Dropdown = Vx * (((Ai_holdSpaceKey) ? t_maxRise : t_rise) + t_fall);
            Sx_Dropdown *= (waypointFacingRight) ? 1 : -1;

            // damn, I need to calculate gravityFall & rise somewhere
            float Sy_Dropdown = (((Ai_holdSpaceKey) ? maxJumpHeight : jumpHeight) - ((gravityFall * (t_fall) * (t_fall)) / 2));

            // at this node, we will perform a dodge that is analogus to a dash that will be a lateral dash towards the jumpEndNode
            GraphNode dodgeAtThisNode = GridGraphGenerate.gg.GetNearest(new Vector3(jumpAtThisNodePosition.x + Sx_Dropdown, Sy_Dropdown + jumpAtThisNodePosition.y)).node;

            Vector2 direction = (waypointFacingRight) ? Vector2.right : Vector2.left;

            // new dodge special waypoint that will be inserted into our new path
            BaseAiController.specialWaypoint newDodgeSpecialWaypoint = new BaseAiController.specialWaypoint(
            typeofWaypoint.DODGE, dodgeAtThisNode, () => { baseCharacterController.DodgeWaypointAI(direction); }, waypointFacingRight, 0.6f);

            Vector3 dodgeAtThisNodePosition = (Vector3)dodgeAtThisNode.position;

            // deciding to skip this specific potential node due to exceeding the bounds of the player scene as we will be hitting the ceiling...
            if (dodgeAtThisNodePosition.y < GridGraphGenerate.gg.size.y / 2) continue;

            // simulates a single jump from jumpAtThisNodePosition to check whether we will collide with other objects along the way
            bool collided = BresenhamCollisionDetection(SimulateSingleJump(jumpAtThisNodePosition, dodgeAtThisNodePosition, waypointFacingRight, 4, false, 0, Ai_holdSpaceKey), 3, ref nodeOfCollision, ref nearNode);

            if (collided)
            {
                // Since we collided, we will skip this specific potential node
                continue;
            }

            // preparing to simulate a later dash to determine if we will collide with other objects along the way
            List<Vector2> points = new List<Vector2>();
            points.Add(new Vector2(dodgeAtThisNodePosition.x, dodgeAtThisNodePosition.y));
            points.Add(new Vector2(dodgeAtThisNodePosition.x + ((waypointFacingRight) ? SxDash : -SxDash), dodgeAtThisNodePosition.y));

            GraphNode temp = null;
            GraphNode nearNodeTemp = null;
            collided = BresenhamCollisionDetection(points, 3, ref temp, ref nearNodeTemp);

            if (collided)
            {
                // Since we collided, we will skip this specific potential node
                continue;
            }

            // states that this jumpNode has been processed (we don't have to do this in every instance)
            if (!jumpNodesFinal.Contains(jumpAtThisNode))
            {
                jumpNodesFinal.Add(jumpAtThisNode);

                if (!baseAiController.specialWaypoints.Contains(newJumpSpecialWaypoint))
                {
                    baseAiController.specialWaypoints.Add(newJumpSpecialWaypoint);
                    specialWaypoints.Add(newJumpSpecialWaypoint);
                }
            }

            // states that this jumpNode has been processed (we don't have to do this in every instance)
            if (!jumpNodesFinal.Contains(dodgeAtThisNode))
            {
                //jumpNodesFinal.Add(dodgeAtThisNode);

                if (!baseAiController.specialWaypoints.Contains(newDodgeSpecialWaypoint))
                {
                    baseAiController.specialWaypoints.Add(newDodgeSpecialWaypoint);
                    specialWaypoints.Add(newDodgeSpecialWaypoint);

                    // ensuring that we insert out dodge waypoint into our new path
                    WaypointInsertStruct newInsert = new WaypointInsertStruct(newDodgeSpecialWaypoint.node, jumpAtThisNode);
                    waypointInserts.Add(newInsert);
                }
            }

            // trimming to clean up our single jump into a lateral dash
            TrimRequestStruct newTrimRequest = new TrimRequestStruct(jumpAtThisNode, jumpEndNode);
            trimRequests.Add(newTrimRequest);

            return true;
        }

        return false;
    }
    
    private enum TypeofJump { SINGLE, DROPDOWN_SINGLE, SINGLE_DODGE, DOUBLE_JUMP };
    
    // Just a warning when using CalculateOvershootWaypoints_S
    // Make sure that distanceFromJumpNode is not too great to the point that distanceFromJumpNode/Vx is more than air time, else overshoot is impossible

    // CalculateOvershootWaypoints_S is a process in which we overshoot a jumpEndNode and curve back into it so we can avoid obstacles along the way
    private bool CalculateOvershootWaypoints_S
        (GraphNode target, GraphNode jumpNode, float Sxa=1.5f, int distanceFromJumpNode=0, 
        TypeofJump typeofJump=TypeofJump.DOUBLE_JUMP, bool flip_s=false)
    {
        // Sxa represents distance from jumpNode towards nodeA
        // nodeA and nodeB are nodes placed within the air to help guide the BaseAiController to navigate through the air to maneauvr around obstacles while performing the overshoot

        bool directionOfVaccantNodeFR = false; // FR means facing right and the direction is defined as relative from jumpEndNode towards adjacent air node

        // used later to assess if we meet the necessary air space required to form an OvershootWaypoints_S jump
        List<GraphNode> vaccantNodes = FindVaccantOverheadForOvershoot(target, ref directionOfVaccantNodeFR);

        // flip_s states whether the direction of our overshoot should be flipped
        if (flip_s) directionOfVaccantNodeFR = !directionOfVaccantNodeFR;

        if (vaccantNodes.Count < 1) return false;

        Vector3 targetPosition = (Vector3)target.position;
        Vector3 jumpNodePosition = (Vector3)jumpNode.position;

        GraphNode newJumpNode;
        Vector3 newJumpNodePosition;

        // used to state whether the Ai should hold the jump key to form higher jumps
        bool Ai_holdSpaceKey = false;

        // entire process here is trying to form a newJumpNode at a "distanceFromJumpNode" away from the original jumpNode
        if (distanceFromJumpNode != 0)
        {
            bool foundAdjNodes = false;
            List<GraphNode> adjNodes = Helper.FindAdjacentNodes(jumpNode, ref foundAdjNodes,
                (directionOfVaccantNodeFR) ? AdjNodeSearchDirection.RIGHT : AdjNodeSearchDirection.LEFT, 0, null, 0);
            if(adjNodes == null)
            {
                // ...
            }

            if (adjNodes.Count - 1 >= distanceFromJumpNode && foundAdjNodes)
            {

                newJumpNode = adjNodes[distanceFromJumpNode];
                newJumpNodePosition = (Vector3)newJumpNode.position;
            } else
            {
                if (!flip_s)
                {
                    flip_s = true;
                    directionOfVaccantNodeFR = !directionOfVaccantNodeFR;
                    distanceFromJumpNode = 0;
                }

                newJumpNode = jumpNode;
                newJumpNodePosition = (Vector3)newJumpNode.position;
            }
        }
        else
        {
            newJumpNode = jumpNode;
            newJumpNodePosition = (Vector3)newJumpNode.position;
        }

        // CalculateWaypoints_S will work different based on the different types of jumps that will be requesting overshooting capabilities. However, I found the need
        // for only using the CalculateWaypoints_S overshooting for double jumps so the other three options are empty as of now...
        switch (typeofJump)
        {
            case TypeofJump.SINGLE:
                break;

            case TypeofJump.DROPDOWN_SINGLE:
                break;

            case TypeofJump.SINGLE_DODGE:
                break;

            case TypeofJump.DOUBLE_JUMP:
                float Sy = targetPosition.y - newJumpNodePosition.y;

                // impossible to form a CalculateWaypoints_S jump
                if (Sy > maxJumpHeight + maxAirborneJumpHeight) return false;

                // is possible to form a CalculateWaypoints_S, but will have to clamp some values to handle naunced bugs...
                if (Sy >= jumpHeight + airborneJumpHeight - ((flip_s)?1.5f:0f) && maxJumpHeight + maxAirborneJumpHeight >= Sy)
                {
                    Sy = Mathf.Clamp(Sy, 0, 8f);

                    // Ai will be holding the jump key to form higher jumps
                    Ai_holdSpaceKey = true;
                }
                else
                {
                    Sy = Mathf.Clamp(Sy, 0, 6f);
                }

                if (Sy <= 4.5f) Sy = 5f;

                // time total for our overshooting double jump when dealing with a height difference of Sy and whether we are holding the jump key or not...
                float time_total = GetRemainingTimeAtDoubleJumpTime(Sy, 0f, Ai_holdSpaceKey);

                // this 'magic number' was actually deduced from debugging while running the BaseAiPathModifier in
                // order to conclude at which node distance away the targetNode has to be away from the jumpNode until 
                // its impossible to form a OvershootWaypoints_S jump towards it. I converted that distance into time by using
                // kinematic equations and is the way I deduced this 'magic number'.
                time_total = Mathf.Clamp(time_total, 0, 1.563938f); 


                float Sx = Mathf.Abs(targetPosition.x - newJumpNodePosition.x);
                float time_sx = Sx / Vx;

                float time_sxa = Sxa / Vx;
                float time_sxb = time_total - time_sxa - time_sxa - time_sx;

                float Sxb = (time_sxb / 2) * Vx;

                // jumpEndNode is too far from jumpNode to perform a OvershootWaypoints_S jump. I may changed from hard coded values
                // if other characters were present on the screen with different movement speeds
                if (Sx > 4.5f) return false;

                // time in which we will perform an airborne jump after performing our grounded jump during our overshooted double jump
                float airborneJumpTime = GetTimeOfDoubleJumpAirborneJumpTime(Sy, Ai_holdSpaceKey);

                float Sya = GetDoubleJumpHeightAtTime(Sy, time_sxa, Ai_holdSpaceKey);
                float Syb = GetDoubleJumpHeightAtTime(Sy, time_sxa * 2 + time_sxb / 2, Ai_holdSpaceKey);

                float SxAirborne = 0f;

                // will airborne jump take priorty over nodeA? 
                // nodeA and nodeB are used to help guide our Ai controller through the air to properly overshooting around obstacles
                // however, we need to queue each command in a certain order so the process occurs smoothly. Thus, In some instances, the airborne
                // jump takes priorty over nodeA for this very reason...

                // this goes into time_sxa & time_sxb since time_sxa states the amount of time it will take to reach nodeA after jumping from the jumpNode
                // and time_sxb states the amount of time to reach the nodeB after jumping and commencing the overshooting double jump from jumpNode
                bool queueAirborneJumpBeforeA = false;

                // thus, depending on different timings, the distance from our jumpNode towards our airborne jump node will be defined with different methods
                // SxAirborne represents that horizontal distance from our jumpNode towards our airborne jump node...
                if (airborneJumpTime < time_sxa)
                {
                    SxAirborne = Vx * airborneJumpTime * ((directionOfVaccantNodeFR) ? -1 : 1);
                    queueAirborneJumpBeforeA = true;
                }
                else if (airborneJumpTime < (time_sxa * 2))
                {
                    SxAirborne = Vx * (airborneJumpTime - time_sxa) * ((directionOfVaccantNodeFR) ? -1 : 1);
                }
                else if (airborneJumpTime < (time_sxa * 2 + time_sxb / 2))
                {
                    SxAirborne = Vx * (airborneJumpTime - time_sxa * 2) * ((directionOfVaccantNodeFR) ? 1 : -1);
                }
                else if (airborneJumpTime < (time_sxa * 2 + time_sxb))
                {
                    SxAirborne = Vx * (airborneJumpTime - time_sxa * 2 - time_sxb / 2) * ((directionOfVaccantNodeFR) ? 1 : -1);
                }

                // These points are representing specific points in space where nodeA and nodeB and our airborneJumpNode will be placed
                // nodeA and nodeB are used to help guide our Ai controller through the air to properly overshooting around obstacles
                Vector2 Sxa_point = new Vector2(((directionOfVaccantNodeFR) ? -Sxa : Sxa) + newJumpNodePosition.x, GetDoubleJumpHeightAtTime(Sy, time_sxa, Ai_holdSpaceKey) + newJumpNodePosition.y);
                Vector2 Sxb_point = new Vector2(((directionOfVaccantNodeFR) ? Sxb : -Sxb) + newJumpNodePosition.x, GetDoubleJumpHeightAtTime(Sy, time_sxa + time_sxa + time_sxb / 2, Ai_holdSpaceKey) + newJumpNodePosition.y);
                Vector2 airJumpPoint = new Vector2(newJumpNodePosition.x + SxAirborne, newJumpNodePosition.y + GetDoubleJumpHeightAtTime(Sy, airborneJumpTime, Ai_holdSpaceKey));

                // Collision detection to assess whether we need to change Sxa that represents the horizontal distance from our jumpNode to nodeA
                // nodeA and nodeB are used to help guide our Ai controller through the air to properly overshooting around obstacles
                GraphNode collisionNode = null;

                // nearNode represents the closest free space air node that is closest towards the collisionNode (the node were a predicted collision will take place)
                GraphNode nearNode = null;
                List<Vector2> points = new List<Vector2>();
                points.Add(Sxa_point);
                points.Add(newJumpNodePosition);

                // does our path towards nodeA anticipate a collision?
                bool Sxa_pathCollided = BresenhamCollisionDetection(points, 3, ref collisionNode, ref nearNode, false);

                // Sxa_collides is for apply circumstantial padding towards our nodeA in the case an anticipated collision will occurr from jumpNode towards nodeA
                // nodeA and nodeB are used to help guide our Ai controller through the air to properly overshooting around obstacles
                float Sxa_collides;
                bool Sxa_willWait = false; // will we stop moving through the air as we approach nodeA if its necessary to avoid a collision?

                bool Sxa_airborneJumpWithinWait = false; // will we stop moving through the air for a long enough period of time that we encroach onto the time that an
                // airborne jump is supposed to be performed?

                float time_sxaCollision = 0f; // used to deduce how long a collision would have occurred if we were to continuing moving in towards the colliding obstacle

                GraphNode calculatedNode_A_WAIT = null; // nodeA_Wait is used to guide the Ai controller through the air to properly overshoot around obstacles in the case
                // that we are forced to stop moving through the air

                if (Sxa_pathCollided)
                {
                    Vector3 collisionNodePosition;

                    if (collisionNode != null)
                        collisionNodePosition = (Vector3)collisionNode.position;
                    else collisionNodePosition = newJumpNodePosition;

                    // distance from newJumpNode towards node of collision
                    Sxa_collides = Mathf.Abs(newJumpNodePosition.x - collisionNodePosition.x);

                    if (Sxa_collides < Sxa)
                    {
                        // we have collided and that obstacle intrudes on our path towards nodeA, so we must adjust
                        // Sxa and Sxa_point as we will be placing nodeA at a new horizontal distance away from newJumpNode to ensure that
                        // we don't collide with obstacles along our way during our overshooting double jump

                        float diff = (Sxa - Sxa_collides);
                        time_sxaCollision = diff / Vx;

                        if (time_sxaCollision > airborneJumpTime) Sxa_airborneJumpWithinWait = true;

                        Sxa = Sxa_collides + ((directionOfVaccantNodeFR) ? 0.5f : -0.5f);
                        time_sxa = Sxa / Vx;
                        Sxa_point = new Vector2(((directionOfVaccantNodeFR) ? -Sxa + 0.5f : Sxa - 0.5f) + newJumpNodePosition.x, GetDoubleJumpHeightAtTime(Sy, time_sxa, Ai_holdSpaceKey) + newJumpNodePosition.y);

                    }
                }

                points.Clear();
                points.Add(Sxa_point);
                points.Add(Sxb_point);

                // does our path towards nodeB anticipate a collision?
                bool Sxb_pathCollided = BresenhamCollisionDetection(points, 8, ref collisionNode, ref nearNode, false);
                if (!Sxb_pathCollided) Sxb_pathCollided = !GridGraphGenerate.gg.GetNearest(Sxb_point).node.Walkable;

                float Sxb_collides;

                bool Sxb_willWait = false; // will we stop moving through the air as we approach nodeB if its necessary to avoid a collision?
                bool Sxb_airborneJumpWithinWait = false; // will we stop moving through the air for a long enough period of time that we encroach onto the time that an
                // airborne jump is supposed to be performed?

                float time_sxbCollision = 0f; // used to deduce how long a collision would have occurred if we were to continuing moving in towards the colliding obstacle
                GraphNode calculatedNode_B_WAIT = null; // nodeB_Wait is used to guide the Ai controller through the air to properly overshoot around obstacles in the case
                // that we are forced to stop moving through the air

                // temp represents just a temporary float variable that may be defined in the instance Sxb_pathCollided is true. In this case, we must store a temporary
                // variable concering its padding as wwe will be using that value else where as well.
                float temp = 0f;

                if (Sxb_pathCollided)
                {
                    // nearNode represents the closest free space air node that is closest towards the collisionNode (the node were a predicted collision will take place)

                    Vector2 nearNodePosition;
                    if (nearNode != null)
                        nearNodePosition = (Vector3)nearNode.position;
                    else nearNodePosition = Sxa_point;

                    // distance from nearJumpNodePosition towards node of collision
                    Sxb_collides = Mathf.Abs(newJumpNodePosition.x - nearNodePosition.x);

                    if (Sxb_collides < Sxb)
                    {

                        // we have collided and that obstacle intrudes on our path towards nodeB, so we must adjust
                        // Sxb and Sxb_point as we will be placing nodeB at a new horizontal distance away from newJumpNode to ensure that
                        // we don't collide with obstacles along our way during our overshooting double jump

                        // however, we will also be allowing our the path to form a "wait" in which our character will be staying stil in the air at certain parts
                        // along the overshooting double jump to ensure that we can avoid some predicted object collisions

                        float diff = (Sxb - Sxb_collides);
                        time_sxbCollision = diff / Vx; // used to deduce how long a collision would have occurred if we were to continuing moving in towards the colliding obstacle

                        // ensuring that we state that we are going to waiting through the air at certain parts along the overshooting double jump
                        Sxb_willWait = true;

                        // Sxb represents the horizontal distance from newJump to reach nodeB
                        Sxb = Sxb_collides;
                        float padding = 1f;

                        // forming the Sxb_point as this will be the position of our nodeB
                        Sxb_point = new Vector2(nearNodePosition.x + ((directionOfVaccantNodeFR) ? -padding : padding),
                            (Mathf.Abs(Sxa_point.y - targetPosition.y) <= 0.5f && Mathf.Abs(Sxa_point.x - targetPosition.x) <= 0.5f) ?
                            (((Mathf.Abs(Sxb_point.y - airJumpPoint.y) > 1f && Mathf.Abs(Sxb_point.x - airJumpPoint.x) > 0.5f)) ? Sxa_point.y : airJumpPoint.y)
                            : GetDoubleJumpHeightAtTime(Sy, time_sxa + time_sxb, Ai_holdSpaceKey) + newJumpNodePosition.y - 2f);

                        // temp is storing a temporary value concering how padding has been processing for our nodeB that will be used else where in this code...
                        temp = nearNodePosition.x + ((directionOfVaccantNodeFR) ? -padding : padding);

                        // calculated node that represents where the Ai will wait and stand still in the air to avoid an object collision...
                        calculatedNode_B_WAIT = GridGraphGenerate.gg.GetNearest(Sxb_point).node;

                        // this wait node will be represented as a special waypoint that to better help guide the BaseAiController through the air while performing the overshoot
                        BaseAiController.specialWaypoint waitWaypoint = new BaseAiController.specialWaypoint(typeofWaypoint.WAIT, calculatedNode_B_WAIT,
                           () => { baseCharacterController.HoldMovementAi(Vector2.zero,
                               ((Mathf.Abs(Sxa_point.y - targetPosition.y) > 0.5f && Mathf.Abs(Sxa_point.x - targetPosition.x) > 0.5f) &&
                               (Mathf.Abs(Sxb_point.y - airJumpPoint.y) >= 1f && Mathf.Abs(Sxb_point.x - airJumpPoint.x) > 0.5f)) ? time_sxbCollision / 3 : 0f); }, false, 1f, null, null);

                        if (!baseAiController.specialWaypoints.Contains(waitWaypoint))
                        {
                            baseAiController.specialWaypoints.Add(waitWaypoint);
                            debugNodes.Add(waitWaypoint.node);
                            specialWaypoints.Add(waitWaypoint);
                        }
                    }
                }

                // checking if we will need to perform an airborne jump while standing still in the air
                if (Sxa_willWait && time_sxbCollision + time_sxa + time_sxaCollision > airborneJumpTime)
                {
                    Sxb_airborneJumpWithinWait = true;
                }
                else if (!Sxa_willWait && time_sxbCollision + time_sxa > airborneJumpTime)
                {
                    Sxb_airborneJumpWithinWait = true;
                }

                // calculating nodeA
                GraphNode calculatedNode_A = null;

                if (!Sxa_willWait)
                    calculatedNode_A = GridGraphGenerate.gg.GetNearest(new Vector3(
                        newJumpNodePosition.x + ((directionOfVaccantNodeFR) ? -1 : 1) * Sxa, newJumpNodePosition.y + Sya)).node;

                // calculateing nodeB
                GraphNode calculatedNode_B = null;

                if (!Sxb_willWait)
                    calculatedNode_B = GridGraphGenerate.gg.GetNearest(new Vector3(
                        newJumpNodePosition.x + ((directionOfVaccantNodeFR) ? 1 : -1) * Sxb, newJumpNodePosition.y + Syb)).node;

                // nodeA and nodeB are used to help guide our Ai controller through the air to properly overshooting around obstacles

                // calculating airborne jump node...
                GraphNode calculatedNode_AirborneJump = null;

                // based on specific cases...
                if (Sxa_airborneJumpWithinWait) {
                    // in the case where we will be performing an airborne jump while waiting due to a predicted collision with the original nodeA
                    calculatedNode_AirborneJump = GridGraphGenerate.gg.GetNearest(new Vector3(
                    newJumpNodePosition.x + ((directionOfVaccantNodeFR) ? -1 : 1) * Sxa, newJumpNodePosition.y + GetDoubleJumpHeightAtTime(Sy, airborneJumpTime, Ai_holdSpaceKey))).node;
                }
                else if (Sxb_airborneJumpWithinWait)
                {
                    // in the case where we will be performing an airborne jump while waiting due to a predicted collision with the original nodeB
                    calculatedNode_AirborneJump = GridGraphGenerate.gg.GetNearest(new Vector3(
                    temp, newJumpNodePosition.y + GetDoubleJumpHeightAtTime(Sy, airborneJumpTime, Ai_holdSpaceKey))).node;
                }
                else
                {
                    if (Sxb_willWait)
                    {
                        // in the case where we will be performing an airborne jump just after waiting due to a predicted collision with the original nodeB
                        calculatedNode_AirborneJump = GridGraphGenerate.gg.GetNearest(new Vector3(
                        temp, newJumpNodePosition.y + GetDoubleJumpHeightAtTime(Sy, airborneJumpTime, Ai_holdSpaceKey))).node;
                    }
                    else
                    {
                        // in the case where we will be performing an airborne jump without any waiting taking place
                        calculatedNode_AirborneJump = GridGraphGenerate.gg.GetNearest(new Vector3(
                        newJumpNodePosition.x + SxAirborne, newJumpNodePosition.y + GetDoubleJumpHeightAtTime(Sy, airborneJumpTime, Ai_holdSpaceKey))).node;
                    }
                }

                // calculating special waypoint to represent our first jump that takes place from newJumpNode
                // check BaseAiController to better understand its specialWaypoint initializer
                BaseAiController.specialWaypoint newSpecialWaypoint = new BaseAiController.specialWaypoint(typeofWaypoint.JUMP, newJumpNode,
                    () => { baseCharacterController.JumpWaypointAI(Ai_holdSpaceKey, true); }, (directionOfVaccantNodeFR) ? true : false, 0.4f, newJumpNode, target); // ? im calculating wheather waypointFacingRight wrongly

                // checking if newJumpNode would have been trimmed off, and if that is the case, we must insert the node back into our new path
                if (CheckTrims(newNodes.FindIndex(d => d == newJumpNode)))
                {
                    WaypointInsertStruct newInsert = new WaypointInsertStruct(newJumpNode, jumpNode);
                    waypointInserts.Add(newInsert);
                }

                if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
                {
                    baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                    debugNodes.Add(newSpecialWaypoint.node); // just to visualise the process for debugging sake
                    specialWaypoints.Add(newSpecialWaypoint);
                }

                // calculating the airborne jump waypoint
                // check BaseAiController to better understand its specialWaypoint initializer
                newSpecialWaypoint = new BaseAiController.specialWaypoint(typeofWaypoint.AIRBORNE_JUMP, calculatedNode_AirborneJump,
                    () => { baseCharacterController.JumpWaypointAI(Ai_holdSpaceKey); }, (directionOfVaccantNodeFR) ? true : false, 0.9f, null,
                    null, false, (!directionOfVaccantNodeFR), (directionOfVaccantNodeFR), false, false); // ? im calculating wheather waypointFacingRight wrongly

                if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
                {
                    baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                    debugNodes.Add(newSpecialWaypoint.node);
                    specialWaypoints.Add(newSpecialWaypoint);
                }

                if (!Sxa_willWait)
                {
                    // if we are not going to wait and stand still in the air due to a predicted collision towards the original nodeA, we will just assign are nodeA waypoint as normal
                    // nodeA and nodeB are used to help guide our Ai controller through the air to properly overshooting around obstacles

                    newSpecialWaypoint = new BaseAiController.specialWaypoint(typeofWaypoint.RUN, calculatedNode_A,
                        () => { baseCharacterController.RunWaypointAI((directionOfVaccantNodeFR) ? Vector2.right : Vector2.left); }, (directionOfVaccantNodeFR) ? true : false, 0.6f);

                    if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
                    {
                        Vector3 calculatedJumpNodePos = (Vector3)calculatedNode_AirborneJump.position;
                        Vector3 towardsOvershootNodePos = (Vector3)calculatedNode_A.position;
                        debugNodes.Add(newSpecialWaypoint.node);

                        baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                        specialWaypoints.Add(newSpecialWaypoint);
                    }
                }

                // trimming to clean up the OvershootWaypoints_S and checks for special cases in which some nodes would had been trimmed anyway
                if (originalNodes.Contains(newJumpNode))
                {
                    if (!CheckTrims(newNodes.FindIndex(d => d == newJumpNode)))
                    {
                        TrimRequestStruct newTrimRequest = new TrimRequestStruct(newJumpNode, target);
                        trimRequests.Add(newTrimRequest);
                    } else
                    {
                        TrimRequestStruct newTrimRequest = new TrimRequestStruct(jumpNode, target);
                        trimRequests.Add(newTrimRequest);
                    }
                }
                else
                {
                    if (!CheckTrims(newNodes.FindIndex(d => d == newJumpNode)))
                    {
                        TrimRequestStruct newTrimRequest = new TrimRequestStruct(jumpNode, target);
                        trimRequests.Add(newTrimRequest);

                        WaypointInsertStruct newInsert = new WaypointInsertStruct(newJumpNode, jumpNode);
                        waypointInserts.Add(newInsert);
                    } else
                    {
                        TrimRequestStruct newTrimRequest = new TrimRequestStruct(jumpNode, target);
                        trimRequests.Add(newTrimRequest);

                        WaypointInsertStruct newInsert = new WaypointInsertStruct(newJumpNode, jumpNode);
                        waypointInserts.Add(newInsert);
                    }
                }

                // queueing logic that is siginifcant process since we need to queue each command in a certain order so the process occurs smoothly.
                // thus we go through a list of checks to determine which insert should be taken priorty based on whether we are waiting in the air or not
                if (queueAirborneJumpBeforeA)
                {
                    WaypointInsertStruct newInsert;

                    if (Sxa_willWait && Sxa_airborneJumpWithinWait)
                    {
                        // newJumpNode -> Node_A_WAIT
                        newInsert = new WaypointInsertStruct(calculatedNode_A_WAIT, newJumpNode);
                        waypointInserts.Add(newInsert);

                        // Node_A_WAIT -> AirborneJump
                        newInsert = new WaypointInsertStruct(calculatedNode_AirborneJump, calculatedNode_A_WAIT);
                        waypointInserts.Add(newInsert);

                    }
                    else if (Sxa_willWait)
                    {
                        // newJumpNode -> AirborneJump
                        newInsert = new WaypointInsertStruct(calculatedNode_AirborneJump, newJumpNode);
                        waypointInserts.Add(newInsert);

                        // AirborneJump -> Node_A_WAIT
                        newInsert = new WaypointInsertStruct(calculatedNode_A_WAIT, calculatedNode_AirborneJump);
                        waypointInserts.Add(newInsert);
                    }
                    else
                    {
                        // newJumpNode -> AirborneJump
                        newInsert = new WaypointInsertStruct(calculatedNode_AirborneJump, newJumpNode);
                        waypointInserts.Add(newInsert);

                        // AirborneJump -> Node_A
                        newInsert = new WaypointInsertStruct(calculatedNode_A, calculatedNode_AirborneJump);
                        waypointInserts.Add(newInsert);
                    }
                }
                else
                {
                    WaypointInsertStruct newInsert;
                    if (Sxa_willWait)
                    {
                        // newJumpNode -> Node_A_WAIT
                        newInsert = new WaypointInsertStruct(calculatedNode_A_WAIT, newJumpNode);
                        waypointInserts.Add(newInsert);

                        // Node_A_WAIT -> AirborneJump
                        newInsert = new WaypointInsertStruct(calculatedNode_AirborneJump, calculatedNode_A_WAIT);
                        waypointInserts.Add(newInsert);
                    }
                    else
                    {
                        // newJumpNode -> Node_A
                        newInsert = new WaypointInsertStruct(calculatedNode_A, newJumpNode);
                        waypointInserts.Add(newInsert);

                        // Node_A -> AirborneJump
                        newInsert = new WaypointInsertStruct(calculatedNode_AirborneJump, calculatedNode_A);
                        waypointInserts.Add(newInsert);
                    }
                }
                

                // Node B
                if (!Sxb_willWait)
                {
                    // if we are not going to wait and stand still in the air due to a predicted collision towards the original nodeB, we will just assign are nodeB waypoint as normal
                    // nodeA and nodeB are used to help guide our Ai controller through the air to properly overshooting around obstacles

                    newSpecialWaypoint = new BaseAiController.specialWaypoint(typeofWaypoint.RUN, calculatedNode_B,
                        () => { baseCharacterController.RunWaypointAI((directionOfVaccantNodeFR) ? Vector2.left : Vector2.right); }, (directionOfVaccantNodeFR) ? false : true, 1f);

                    if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
                    {
                        debugNodes.Add(newSpecialWaypoint.node);

                        baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                        specialWaypoints.Add(newSpecialWaypoint);
                    }
                }

                // Further queuing logic, but now mainly dealing nodeB...
                // thus we again go through a list of checks to determine which insert should be taken priorty based on whether we are waiting in the air or not

                if (Sxb_willWait && Sxb_airborneJumpWithinWait)
                {
                    WaypointInsertStruct newInsert = new WaypointInsertStruct(calculatedNode_B_WAIT, latestInsertNode);
                    waypointInserts.Add(newInsert);
                }
                else if (Sxb_willWait)
                {

                    WaypointInsertStruct newInsert = new WaypointInsertStruct(calculatedNode_B_WAIT, latestInsertNode);
                    waypointInserts.Add(newInsert);

                    newInsert = new WaypointInsertStruct(target, latestInsertNode);
                    waypointInserts.Add(newInsert);
                }
                else
                {
                    WaypointInsertStruct newInsert = new WaypointInsertStruct(calculatedNode_B, latestInsertNode);
                    waypointInserts.Add(newInsert);
                }


                // states that this jumpNode has been processed (we don't have to do this in every instance). However, we will be placing newJumpNode here instead...
                jumpNodesFinal.Add(newJumpNode);

                // adding some padding to our target node (essentially the jumpEndNode) so other subsequent jumps can be performed while be spaced evenly so it appears more natural
                bool foundAdjNodes = false;
                List<GraphNode> paddingNodes = Helper.FindAdjacentNodes(target, ref foundAdjNodes, AdjNodeSearchDirection.BOTH, 2);
                paddingNodes.Add(target);
                PadTheseNodes(paddingNodes, false);

                // was able to perform an OvershootWaypoints_S
                return true;
                break;
        }

        // was not able to perform an OvershootWaypoints_S
        return false;
    }

    private void CalculateForesight(GraphNode dynamicNode)
    {
        // handling nodes that move overtime, but I have no need for this function for now since all objects in the scenes are static... 
    }

    // purpose of this function is to find the vaccant adjacent air node from a ground node that is typically at the edge of a nodeGroup (nodeGroup defined above)
    // function is especially used in tandem with OvershootWaypoints_S

    // if we found no vaccant adjacent air nodes, then we will will be just returning an empty List
    private List<GraphNode> FindVaccantOverheadForOvershoot(GraphNode scanNodePoint, ref bool retDirection)
    {
        // retDirection stands for returned direction
        // false - left
        // true - right
        // Left takes priority

        GraphNode currentNodeBeingVetted;
        List<GraphNode> vaccantNodes = new List<GraphNode>(); // Ranges from 0 to 2

        // Checking nodes to the right
        bool notVaccant = false;

        // Helper.TurnPositionIntoPointOnGridGraph defined well in Helper.cs
        // but essentially, we our turning our scanNodePoint into an (x,y) point along our gridGraph rather then an index number since our GridGraph actually
        // stores a list of all of its nodes apart of just one large array
        Vector3 scanNodePointPosition = Helper.TurnPositionIntoPointOnGridGraph(GridGraphGenerate.gg, scanNodePoint);

        // vetting nodes to the right of scanNodePoint from the bottom right adjacent node, then the middle right adjacent node, then upper right adjacent node
        for (int z = 0; z < 3; z++)
        {
            currentNodeBeingVetted = GridGraphGenerate.gg.nodes[(z - 1 + (int)scanNodePointPosition.y) * GridGraphGenerate.gg.width + ((int)scanNodePointPosition.x + 1)];
            if (currentNodeBeingVetted.Penalty == GridGraphGenerate.lowPenalty) notVaccant = true;
        }

        if (!notVaccant) { 
            // if !notVaccant, then we found a Vaccant air node adjacent to our scanNodePoint
            vaccantNodes.Add(GridGraphGenerate.gg.nodes[((int)scanNodePointPosition.y) * GridGraphGenerate.gg.width + ((int)scanNodePointPosition.x + 1)]);
            retDirection = true;
        }

        // Checking nodes to the left
        notVaccant = false;
        scanNodePointPosition = Helper.TurnPositionIntoPointOnGridGraph(GridGraphGenerate.gg, scanNodePoint);

        // vetting nodes to the left of scanNodePoint from the bottom left adjacent node, then the middle left adjacent node, then upper left adjacent node
        for (int z = 0; z < 3; z++)
        {
            currentNodeBeingVetted = GridGraphGenerate.gg.nodes[(z - 1 + (int)scanNodePointPosition.y) * GridGraphGenerate.gg.width + ((int)scanNodePointPosition.x - 1)];
            if (currentNodeBeingVetted.Penalty == GridGraphGenerate.lowPenalty) notVaccant = true;
        }

        if (!notVaccant)
        {
            // if !notVaccant, then we found a Vaccant air node adjacent to our scanNodePoint
            vaccantNodes.Add(GridGraphGenerate.gg.nodes[((int)scanNodePointPosition.y) * GridGraphGenerate.gg.width + ((int)scanNodePointPosition.x - 1)]);
            retDirection = false;
        }
        
        // found no vaccantNodes if vaccantNodes.Count == 0
        if (vaccantNodes.Count == 0) retDirection = false;

        return vaccantNodes;
    }

    // checks for collisions from a point and checks directly with a height defined as "hightInNodes" to find a non-walkable node
    // if we encounter a non-walkable, then CheckForCollisions will return true and infer that we have "collided", else it will
    // return false and infer that we have not collided

    // countOutOfBounds is used to check whether we should consider the ceiling, and the bounds of the scene when determing if we are colliding with obstacles
    private bool CheckForCollisions(GraphNode point, int heightInNodes, bool countOutOfBounds)
    {
        if (!point.Walkable) return true;
        else
        {
            GraphNode currentNodeBeingVetted;
            Vector3 pointPosition = Helper.TurnPositionIntoPointOnGridGraph(GridGraphGenerate.gg, point);

            // loops through nodes from GraphNode point and upwards and checks each node if it walkable. If not, then we have "collided" and our function will return true, else false.
            for (int z = 0; z < heightInNodes; z++)
            {
                if (GridGraphGenerate.gg.nodes.Length < (z + 1 + (int)pointPosition.y) * GridGraphGenerate.gg.width + ((int)pointPosition.x))
                {
                    // index is outside of the GridGraph array of nodes

                    if (countOutOfBounds)
                        return true;
                    else continue;
                }

                currentNodeBeingVetted = GridGraphGenerate.gg.nodes[(z + 1 + (int)pointPosition.y) * GridGraphGenerate.gg.width + ((int)pointPosition.x)];
                if (!currentNodeBeingVetted.Walkable) return true;

                if (countOutOfBounds)
                {
                    // Left boundary
                    if ((int)pointPosition.x == 0) return true;
                    // right boundary
                    if ((int)pointPosition.x == GridGraphGenerate.gg.width) return true;
                    // upper boundary
                    if ((int)pointPosition.y == 0 || (int)pointPosition.y == GridGraphGenerate.gg.depth) return true;
                }
            }
        }

        return false;
    }

    // Help from http://members.chello.at/~easyfilter/bresenham.html
    // true if we collidied

    // Using BresenhamLine therom to gather all nodes in between one point to another point, then subsequently perform CheckForCollisions on each node to determine if we will collide
    // with any obstacle along the way
    private bool BresenhamCollisionDetection(List<Vector2> points, int collisionHeightInNodes, ref GraphNode nodeOfCollision, ref GraphNode nearNode, bool countOutOfBounds=true)
    {
        // nearNode represents the closest free space air node that is closest towards the collisionNode (the node were a predicted collision will take place)

        foreach (Vector2 point in points)
        {
            // our points are out of bounds of our GridGraph, and if countOutOfBounds is true, then BresenhamCollisionDetection will return true to signify that we have collided
            if ((point.x < -GridGraphGenerate.gg.size.x / 2 || point.x > GridGraphGenerate.gg.size.x / 2) && countOutOfBounds)
            {
                return true;
            }
            if ((point.y < (-GridGraphGenerate.gg.size.y / 2) + GridGraphGenerate.gg.center.y || point.y > (GridGraphGenerate.gg.size.y / 2) + GridGraphGenerate.gg.center.y) && countOutOfBounds)
            {
                return true;
            }
        }

        // using BresenhamLineLoopThrough from Helper.cs to determine all nodes in between our points
        List<GraphNode> nodes = Helper.BresenhamLineLoopThrough(GridGraphGenerate.gg, points);
        nodeOfCollision = nodes[0]; // node in which the collision has occurred

        // looping through each node in nodes and applying CheckForCollisions to see if we could predict a collision
        foreach (GraphNode node in nodes)
        {
            debugNodes.Add(node); // debugging purposes to visualise process
            Vector3 nodePosition = (Vector3)node.position;
            if (node.Walkable & node != nodes[nodes.Count-1]) nearNode = node; // nearNode is the node just before the nodeOfCollision

            if (CheckForCollisions(node, collisionHeightInNodes, countOutOfBounds))
            {
                nodeOfCollision = node;

                // returns true because we have collided
                return true;
            }


        }

        // returns false becase we have not collided
        return false;
    }


    // simulates a single jump from position towards the endPosition and returns a list of points along that simulated jump stated by the simulationResoultion
    // this single jump can also handle airborne jumps...
    private List<Vector2> SimulateSingleJump(Vector2 position, Vector2 endPosition, bool facingRight, int simulationResoultion, bool airborne = false, int numOfAirborneJmps=0, bool Ai_holdSpaceKey=false)
    {
        List<Vector2> points = new List<Vector2>();

        float store_maxJumpHeight = maxJumpHeight;
        float store_jumpHeight = jumpHeight;
        float store_t_maxRise = t_maxRise;
        float store_t_rise = t_rise;
        
        if(airborne)
        {
            // we make sure to return to values to normal after this process, this is just to simplify kinematic equations and make more manageable

            maxJumpHeight = maxAirborneJumpHeight;
            jumpHeight = airborneJumpHeight;
            t_maxRise = t_maxAirborneRise;
            t_rise = t_airborneRise;
        }

        Vector2 oldPosition = Vector2.zero;

        Vector2 jumpNodePosition = position;
        Vector2 jumpEndNodePosition = (Vector3)endPosition;
        points.Add(jumpNodePosition);

        // Debug.DrawLine(jumpNodePosition, Vector2.zero, Color.magenta, 5f); // debugging purposes...

        float Sy_fall = ((Ai_holdSpaceKey)? maxJumpHeight : jumpHeight) - (jumpEndNodePosition.y - jumpNodePosition.y);
        float x = Vx * (((Ai_holdSpaceKey) ? t_maxRise : t_rise) + Mathf.Sqrt(Mathf.Abs(2 * Sy_fall / gravityFall))) * ((facingRight == false) ? 1 : -1);

        for (int k = 0; k < simulationResoultion; k++)
        {
            float curSx = (x / simulationResoultion) * k;
            float curSy = 0f;
            float elaspedTime = ((facingRight) ? -curSx : curSx) / Vx;
            if (elaspedTime < t_rise)
            {
                curSy = (((airborne)? GetAirborneVyi(numOfAirborneJmps) : Vyi) * elaspedTime) + ((-gravityRise * elaspedTime * elaspedTime * ((Ai_holdSpaceKey) ? 0.8f : 1f)) / 2);
                // 0.8f is important here because 0.8f is the amount gravity is multipled by if we were to hold the jump key, so we must factor this into our kinematic equations
            }
            else
            {
                curSy = ((Ai_holdSpaceKey) ? maxJumpHeight : jumpHeight) + (-gravityFall * (elaspedTime - ((Ai_holdSpaceKey) ? t_maxRise : t_rise))
                    * (elaspedTime - ((Ai_holdSpaceKey) ? t_maxRise : t_rise)) * 0.5f);
            }

            Vector3 jumpPos = position;
            Vector2 newPosition = new Vector2((jumpPos.x - curSx), (jumpPos.y + curSy));

        if (oldPosition != Vector2.zero)
            {
                points.Add(newPosition);
            }

            oldPosition = newPosition;

            if (k == simulationResoultion - 1)
            {
                curSx = x;
                elaspedTime = ((facingRight) ? -curSx : curSx) / Vx;
                curSy = ((Ai_holdSpaceKey) ? maxJumpHeight : jumpHeight) + (-gravityFall * (elaspedTime - ((Ai_holdSpaceKey) ? t_maxRise : t_rise)) 
                    * (elaspedTime - ((Ai_holdSpaceKey) ? t_maxRise : t_rise)) * 0.5f);
                newPosition = new Vector2((jumpPos.x - curSx), (jumpPos.y + curSy));

                points.Add(newPosition);
            }
        }

        if (airborne)
        {
            maxJumpHeight = store_maxJumpHeight;
            jumpHeight = store_jumpHeight;
            t_maxRise = store_t_maxRise;
            t_rise = store_t_rise;
        }

        return points;
    }

    // simulates a single jump from position towards the endPosition and returns a list of points along that simulated jump stated by the simulationResoultion
    // this single jump can also handle airborne jumps...

    // used to simulate a dash or dodge (terms are analogus) and returns a list of points along that simulation. However, we only simulate horizontal
    // dashes since the Ai can and will only perform horizontal dashes
    private List<Vector2> SimulateDash(Vector2 position, bool facingRight, int simulationResoultion)
    {
        List<Vector2> points = new List<Vector2>();

        Vector2 oldPosition = Vector2.zero;

        Vector3 jumpNodePosition = position;
        Vector3 jumpEndNodePosition = new Vector3(position.x + baseCharacterController.dodgeDistance *
            ((facingRight) ? 1 : -1), position.y);


        points.Add(jumpNodePosition);
        float x = (facingRight) ? jumpEndNodePosition.x - jumpNodePosition.x : jumpNodePosition.x - jumpEndNodePosition.x;
        for (int k = 0; k < simulationResoultion; k++)
        {
            float curSx = (x / simulationResoultion) * k;

            Vector3 jumpPos = position;
            Vector2 newPosition = new Vector2((jumpPos.x + curSx), (jumpPos.y));

            if (oldPosition != Vector2.zero)
            {
                points.Add(newPosition);
            }

            oldPosition = newPosition;

            if (k == simulationResoultion - 1)
            {
                curSx = x;
                newPosition = new Vector2((jumpPos.x + curSx), (jumpPos.y));
            }
        }

        return points;
    }

    // Gets the delta height that we will experience at a certain time within our single jump, considering if we are holding the jump key as well
    private float GetSingleJumpHeightAtTime (float time, bool Ai_holdSpaceKey)
    {
        float deltaHeight = jumpHeight + ((Vyi * time) + ((-gravityFall * time * time) / 2));
        return deltaHeight;
    }

    // Gets the delta height that we will experience at a certain time within our double jump, considering if we are holding the jump key as well
    // and also considering the Sy, determine usually as the height from jumpNode towards jumpEndNode
    private float GetDoubleJumpHeightAtTime(float Sy, float time, bool Ai_holdSpaceKey)
    {
        float airborneVyi = GetAirborneVyi();

        float t_rise1 = (Ai_holdSpaceKey)? t_maxRise : t_rise;
        float t_rise2 = (Ai_holdSpaceKey) ? t_maxAirborneRise : t_airborneRise;

        float t_fall1;
        float t_fall2;
        float t_total;

        float Sz = 3f; // magic value

        if (Ai_holdSpaceKey)
        {
            if (maxJumpHeight + maxAirborneJumpHeight < Sy) return 0f;
            else if (maxJumpHeight + maxAirborneJumpHeight - Sz < Sy)
            {
                Sz = Sy - ((maxJumpHeight + maxAirborneJumpHeight) - Sz);
            }
        }
        else
        {
            if (jumpHeight + airborneJumpHeight < Sy) return 0f;
            else if (jumpHeight + airborneJumpHeight - Sz < Sy)
            {
                Sz = Sy - ((jumpHeight + airborneJumpHeight) - Sz);
            }
        }

        float Sb = ((Ai_holdSpaceKey)? maxJumpHeight + maxAirborneJumpHeight : airborneJumpHeight + jumpHeight) - Sy - Sz;
        t_fall1 = Mathf.Sqrt(Mathf.Abs(2 * (-Sb) / gravityFall));
        t_fall2 = Mathf.Sqrt(Mathf.Abs(2 * Sz / gravityFall));
        t_total = t_rise1 + t_fall1 + t_rise2 + t_fall2;

        float deltaHeight;

        // depending on the time we selected, we will have to calculate the deltaHeight in different manners
        if (time <= t_rise1)
        {
            // case in which time selected is before or at the point we reach the apex of our first jump after performing the first jump
            deltaHeight = (Vyi * time) + ((-gravityRise * time * time * ((Ai_holdSpaceKey) ? 0.8f : 1f)) / 2);
            return deltaHeight;
        }
        else if (time + 0.0000001 < t_rise1 + t_fall1) // the 0.0000001 is to fix a bug when at max height
        {
            // case in which time selected is before we fall down towards airborne jump location
            float new_time = time - (t_rise1);
            deltaHeight = ((Ai_holdSpaceKey) ? maxJumpHeight : jumpHeight) + ((-gravityFall * new_time * new_time) / 2);
            return deltaHeight;
        }
        else if (time < t_rise1 + t_fall1 + t_rise2)
        {
            // case in which time selected is after we perform the airborne jump
            float new_time = Mathf.Abs(time - (t_rise1 + t_fall1));
            deltaHeight = (((Ai_holdSpaceKey)? maxJumpHeight : jumpHeight) - Sb) 
                + ((GetAirborneVyi(0) * new_time) + ((-gravityRise * new_time * new_time * ((Ai_holdSpaceKey) ? 0.8f : 1f)) / 2));

            return deltaHeight;
        }
        else 
        {

            // case in which time selected is after we reach or at the apex of the airborne jump
            float new_time = time - (t_rise1 + t_fall1 + t_rise2);
            deltaHeight = ((Ai_holdSpaceKey) ? maxJumpHeight + maxAirborneJumpHeight: jumpHeight + airborneJumpHeight) + Sb
                + ((-gravityFall * new_time * new_time) / 2);

            return deltaHeight;
        }
    }

    // returns how much time is remaining in a double jump when we select a time of our own within the double jump
    private float GetRemainingTimeAtDoubleJumpTime(float Sy, float time, bool Ai_holdSpaceKey)
    {
        float t_rise1 = (Ai_holdSpaceKey) ? t_maxRise : t_rise;
        float t_rise2 = (Ai_holdSpaceKey) ? t_maxAirborneRise : t_airborneRise;

        float t_fall1 = 0f;
        float t_fall2 = 0f;
        float t_total;

        float Sz = 3f; // 'Sz' represents how high the apex of the second jump should be from the destination

        // if necessary, reduce Sz if current Sz is impossible to work with...
        if (Ai_holdSpaceKey)
        {
            if (maxJumpHeight + maxAirborneJumpHeight < Sy) return 0f;
            else if (maxJumpHeight + maxAirborneJumpHeight - Sz < Sy)
            {
                Sz = Sy - ((maxJumpHeight + maxAirborneJumpHeight) - Sz);
            }
        }
        else
        {
            if (jumpHeight + airborneJumpHeight < Sy) return 0f;
            else if (jumpHeight + airborneJumpHeight - Sz < Sy)
            {
                Sz = Sy - ((jumpHeight + airborneJumpHeight) - Sz);
            }
        }

        float Sb = ((Ai_holdSpaceKey) ? maxJumpHeight + maxAirborneJumpHeight : jumpHeight + airborneJumpHeight) - Sy - Sz;

        t_fall1 = Mathf.Sqrt(Mathf.Abs(2 * -Sb / gravityFall));
        t_fall2 = Mathf.Sqrt(2 * Sz / gravityFall);
        t_total = t_rise1 + t_fall1 + t_rise2 + t_fall2;

        return t_total - time; // returning time remaining
    }

    // returns time airborne jump will occurr during the duration of te double jump
    private float GetTimeOfDoubleJumpAirborneJumpTime(float Sy, bool Ai_holdSpaceKey)
    {

        float t_rise1 = (Ai_holdSpaceKey) ? t_maxRise : t_rise;

        float t_fall1;

        float Sz = 3f; // 'Sz' represents how high the apex of the second jump should be from the destination

        // if necessary, reduce Sz if current Sz is impossible to work with...
        if (Ai_holdSpaceKey)
        {
            if (maxJumpHeight + maxAirborneJumpHeight < Sy) return 0f;
            else if (maxJumpHeight + maxAirborneJumpHeight - Sz < Sy)
            {
                Sz = Sy - ((maxJumpHeight + maxAirborneJumpHeight) - Sz);
            }
        }
        else
        {
            if (jumpHeight + airborneJumpHeight < Sy) return 0f;
            else if (jumpHeight + airborneJumpHeight - Sz < Sy)
            {
                Sz = Sy - ((jumpHeight + airborneJumpHeight) - Sz);
            }
        }

        float Sb = ((Ai_holdSpaceKey) ? maxJumpHeight + maxAirborneJumpHeight : jumpHeight + airborneJumpHeight) - Sy - Sz;

        t_fall1 = Mathf.Sqrt(Mathf.Abs(2 * -Sb / gravityFall));
        return t_rise1 + t_fall1; // returns time in which airborne jump will occurr in a double jump
    }

    // Gets the vertical velocity at any point during the duration of a double jump when given the time
    private float GetVyOfDoubleJumpTime(float Sy, float time, float Vyi, bool Ai_holdSpaceKey)
    {
        float timeOfDoubleJump = GetTimeOfDoubleJumpAirborneJumpTime(Sy, Ai_holdSpaceKey);
        float Vy = 0f;

        // based on which stage we are in the doubleJump, we have to calculate our vertical velocity differently...
        if (time < timeOfDoubleJump && time < t_rise)
        {
            // before airborne jump and before we reached the apex of our first jump
            Vy = Mathf.Sqrt(Vyi * Vyi + 2 * ((Ai_holdSpaceKey) ? 0.8f : 1f) * gravityRise * GetDoubleJumpHeightAtTime(Sy, time, Ai_holdSpaceKey));
        } if (time < timeOfDoubleJump)
        {
            // before airborne jump
            Vy = Mathf.Sqrt(Vyi * Vyi + 2 * gravityFall * GetDoubleJumpHeightAtTime(Sy, time, Ai_holdSpaceKey));
        } else if(time > timeOfDoubleJump && time < t_airborneRise + timeOfDoubleJump)
        {
            // after airborne jump, but before we reached the apex of the airborne jump
            Vy = Mathf.Sqrt(Vyi * Vyi + 2 * ((Ai_holdSpaceKey) ? 0.8f : 1f) * gravityRise * GetDoubleJumpHeightAtTime(Sy, time, Ai_holdSpaceKey));
        } else
        {
            // after we reached or at the apex of the airborne jump
            Vy = Mathf.Sqrt(Vyi * Vyi + 2 * gravityFall * GetDoubleJumpHeightAtTime(Sy, time, Ai_holdSpaceKey));
        }

        return Vy;
    }

    // In the future of a, @v1.0 release, I may comment my functions & variables like this in order to make
    // it more scalable (https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/recommended-tags)

#if UNITY_EDITOR

    // Entirely used for debugging purposes in which I can draw cubes of different colors to represent different processess
    private void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        foreach (GraphNode node in jumpNodes)
        {
            Gizmos.DrawCube((Vector3)node.position, new Vector3(0.5f, 0.5f));
        }

        Gizmos.color = Color.gray;
        foreach (GraphNode node in jumpEndNodes)
        {
            Gizmos.DrawCube((Vector3)node.position, new Vector3(0.5f, 0.5f));
        }

        Gizmos.color = Color.white;
        foreach (GraphNode node in adjNodesFromOverhead)
        {
            Gizmos.DrawCube((Vector3)node.position, new Vector3(0.5f, 0.5f));
        }

        Gizmos.color = Color.cyan;
        foreach (GraphNode node in debugNodes)
        {
            Gizmos.DrawCube((Vector3)node.position, new Vector3(0.5f, 0.5f));
        }

        Gizmos.color = Color.black;
        foreach (PaddingNodeStruct padding in allPaddingStructs)
        {
            Gizmos.DrawCube(padding.position, new Vector3(0.5f, 0.5f));
        }

        // Drawing unique special waypoint gizmos such as modelling out how a single jump will appear
        foreach (BaseAiController.specialWaypoint specialWaypoint in specialWaypoints)
        {
            Helper.DrawArrow.ForGizmo(specialWaypoint.nodePosition + Vector3.down * 1f, Vector3.up, Color.green);

            switch (specialWaypoint.waypointType)
            {
                case typeofWaypoint.RUN:
                    break;

                case typeofWaypoint.JUMP:
                    // modelling out how a single jump will appear by essentially simulating points...

                    Vector2 oldPosition = Vector2.zero;
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawCube(specialWaypoint.nodePosition, new Vector3(0.5f, 0.5f));

                    Vector3 jumpNodePosition = (Vector3)specialWaypoint.contextJumpNode.position;
                    Vector3 jumpEndNodePosition = (Vector3)specialWaypoint.contextJumpEndNode.position;

                    float Sy_fall = jumpHeight - (jumpEndNodePosition.y - jumpNodePosition.y);
                    float x = Vx * ((2 * jumpHeight / Vyi) + Mathf.Sqrt(2 * Sy_fall / gravityFall)) * ((specialWaypoint.facingRight == false) ? 1 : -1);

                    // the "6" here represents a resoultion of 6
                    for (int k = 0; k < 6; k++)
                    {
                        float curSx = (x / 6) * k;
                        float curSy = 0f;
                        float elaspedTime = ((specialWaypoint.facingRight)? -curSx : curSx) / Vx;
                        if (elaspedTime < t_rise)
                        {
                            curSy = (Vyi * elaspedTime) + ((-gravityRise * elaspedTime * elaspedTime) / 2);
                        }
                        else
                        {
                            curSy = jumpHeight + (-gravityFall * (elaspedTime - t_rise) * (elaspedTime - t_rise) * 0.5f);
                        }

                        Vector3 jumpPos = specialWaypoint.nodePosition;
                        Vector2 newPosition = new Vector2((jumpPos.x - curSx), (jumpPos.y + curSy));

                        if (oldPosition != Vector2.zero)
                        {
                            Gizmos.color = Color.cyan;

                            // draws a line in between each simulated point in order to illustrate how a jump will appear when debugging
                            Gizmos.DrawLine(newPosition, oldPosition);
                        }

                        oldPosition = newPosition;

                        if (k == 5)
                        {
                            curSx = x;
                            elaspedTime = ((specialWaypoint.facingRight) ? -curSx : curSx) / Vx;
                            curSy = jumpHeight + (-gravityFall * (elaspedTime - t_rise) * (elaspedTime - t_rise) * 0.5f);
                            newPosition = new Vector2((jumpPos.x - curSx), (jumpPos.y + curSy));

                            Gizmos.color = Color.cyan;
                            Gizmos.DrawLine(oldPosition, newPosition);
                        }
                    }

                    break;

                case typeofWaypoint.AIRBORNE_JUMP:
                    // ...
                    break;

                case typeofWaypoint.DODGE:
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawCube(specialWaypoint.nodePosition, new Vector3(0.5f, 0.5f));
                    break;

                default:
                    break;
            }
        }

        // drawing a continous line from newNodes in order to illustrate how the Ai will carry out the path, and in what order
        for(int i=0; i<newNodes.Count; i++)
        {
            if (newNodes[i] != null)
            {
                Vector3 position = (Vector3)newNodes[i].position;
                Helper.DrawArrow.ForGizmo(position + Vector3.up * 1f, Vector3.down, Color.cyan);


                if (i > 0)
                {
                    Vector3 oldPosition = (Vector3)newNodes[i - 1].position;

                    Gizmos.color = Color.cyan;
                    Gizmos.DrawLine(position, oldPosition);

                }
                
            }
        
        }
    }
#endif
}
