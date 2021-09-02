using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;
using UnityEngine.Events;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

public enum AdjNodeSearchDirection { LEFT, RIGHT, BOTH }

public class BaseAiPathModifier : MonoModifier
{
    public List<GraphNode> originalNodes;
    public List<Vector3> originalVectorPath;

    public List<GraphNode> newNodes = new List<GraphNode>();
    public List<Vector3> newVectorPath = new List<Vector3>();

    // Jumping (Single jumps) & gizmos
    public List<GraphNode> jumpNodes = new List<GraphNode>();
    public List<GraphNode> jumpEndNodes = new List<GraphNode>();
    public List<GraphNode> jumpNodesFinal = new List<GraphNode>(); // This contains the list of calculated and processed jump node positions

    public List<GraphNode> ignoreNodes = new List<GraphNode>(); // might be depricated
    public List<GraphNode> adjNodesFromOverhead = new List<GraphNode>();

    public List<GraphNode> debugNodes = new List<GraphNode>(); // just to visualise some data quickly
    public List<GraphNode> ignoreJumpNodes = new List<GraphNode>();

    // Queuing waypoint positions to insert
    public static GraphNode latestInsertNode = null;

    public struct PaddingNodeStruct
    {
        public GraphNode node;
        public readonly Vector3 position { get { return (Vector3)node.position; } }
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

            Debug.Log("Called from " + (Vector3)insertAtNode.position + " : to " + (Vector3)newNode.position);
        }
    }
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
    public struct NodeGroupStruct
    {
        List<GraphNode> allNodes;
        List<Vector3> allNodePositions;

        GraphNode highestNode;
        Vector3 highestNodePosition { get { return (Vector3)highestNode.position; } }

        GraphNode lowestNode;
        Vector3 lowestNodePosition { get { return (Vector3)lowestNode.position; } }

        GraphNode leftistNode;
        Vector3 leftistNodePosition { get { return (Vector3)leftistNode.position; } }

        GraphNode rightistNode;
        Vector3 rightistNodePosition { get { return (Vector3)rightistNode.position; } }

        GraphNode middleNode;
        Vector3 middleNodePosition { get { return (Vector3)middleNode.position; } }

        int Area;

        public NodeGroupStruct(List<GraphNode> nodes, bool leftToRight=true)
        {
            allNodes = nodes;

            allNodes.Sort(delegate (GraphNode param1, GraphNode param2)
            {
                Vector3 param1Position = (Vector3)param1.position;
                Vector3 param2Position = (Vector3)param2.position;

                if (leftToRight && param1Position.x < param2Position.x) return -1;
                else if (!leftToRight && param2Position.x < param1Position.x) return -1;
                else return 1;
            });

            allNodePositions = new List<Vector3>();

            foreach(GraphNode node in allNodes)
            {
                allNodePositions.Add((Vector3)node.position);
            }

            float highest = 0f;
            int highestIndex = 0;

            float lowest = 0f;
            int lowestIndex = 0;

            leftistNode = (leftToRight)? allNodes[0] : allNodes[allNodes.Count - 1];
            rightistNode = (!leftToRight) ? allNodes[0] : allNodes[allNodes.Count - 1];

            Area = allNodes.Count;

            middleNode = allNodes[(int)allNodes.Count / 2];

            for(int i=0; i<allNodePositions.Count; i++)
            {
                if (i==0) {
                    highest = lowest = allNodePositions[i].y;
                }

                if (highest < allNodePositions[i].y)
                {
                    highest = allNodePositions[i].y;
                    highestIndex = i;
                }

                if (lowest > allNodePositions[i].y)
                {
                    lowest = allNodePositions[i].y;
                    lowestIndex = i;
                }
            }

            highestNode = allNodes[highestIndex];
            lowestNode = allNodes[lowestIndex];
        }
    }

    public List<PaddingNodeStruct> allPaddingStructs = new List<PaddingNodeStruct>();
    public List<WaypointInsertStruct> waypointInserts = new List<WaypointInsertStruct>();
    public List<TrimRequestStruct> trimRequests = new List<TrimRequestStruct>();
    public List<TrimRequestStruct> lateTrimRequests = new List<TrimRequestStruct>();
    public List<NodeGroupStruct> nodeGroups = new List<NodeGroupStruct>();

    public BaseCharacterController baseCharacterController;
    public BaseAiController baseAiController;

    public List<BaseAiController.specialWaypoint> specialWaypoints = new List<BaseAiController.specialWaypoint>();

    // For curves
    public int resolution = 6;

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
        baseCharacterController = GetComponent<BaseCharacterController>();
        baseAiController = GetComponent<BaseAiController>();

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

        List<Vector2> tempVectorPath = new List<Vector2>();
        tempVectorPath.Add((Vector3)newNodes[1].position);
        tempVectorPath.Add((Vector3)newNodes[2].position);
        tempVectorPath.Add((Vector3)newNodes[3].position);

        GraphNode returnedNode = null;
        Debug.Log(Helper.CheckDirectionOfPathInSequence(tempVectorPath, Vector2.up));
        Debug.Log(Helper.SearchInDirection(GridGraphGenerate.gg, newNodes[0], 1, ref returnedNode));

        if (Helper.CheckDirectionOfPathInSequence(tempVectorPath, Vector2.up) && !Helper.SearchInDirection(GridGraphGenerate.gg, newNodes[0], 0, ref returnedNode))
        {
            Helper.SearchInDirection(GridGraphGenerate.gg, newNodes[0], 2, ref returnedNode);
            jumpNodes.Add(returnedNode);
            findNextLowPenalty = true;
        }

        // Jump node-ing
        for(int i=0; i<originalNodes.Count-2; i++)
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

        // Debug.Log(jumpNodes.Count);
        // Debug.Log(jumpEndNodes.Count);
        for (int i = 0; i < jumpEndNodes.Count; i++)
        {
            // I have to seperate the CalculateDropdown & CalculateSingleDropdown because of priority queuing issues
            if (i < jumpNodes.Count)
            {
                if (ignoreJumpNodes.Contains(jumpNodes[i])) continue;
            }

            if (CalculateDropdown(jumpNodes[i], jumpEndNodes[i]))
            {
                Debug.Log("Dropdown Pathing Chosen...");
                continue;
            }
            else if (CalculateSingleJump(jumpNodes[i], jumpEndNodes[i], jumpNodes[((i < (jumpEndNodes.Count - 2)) ? i + 1 : i)],
                jumpEndNodes[((i < (jumpEndNodes.Count - 2))? i + 1 : i)], (i < (jumpEndNodes.Count - 2)? true : false))) {
                Debug.Log("Single Jump Pathing Chosen...");
                continue;
            }
            else if (CalculateSingleDropdown(jumpNodes[i], jumpEndNodes[i]))
            {
                Debug.Log("Dropdown + Single jump");
                continue;
            }
            else if (CalculateSingleJumpDashing(jumpNodes[i], jumpEndNodes[i]))
            {
                Debug.Log("Single Jump + Dashing Pathing Chosen...");
                continue;
            }
            else if (CalculateDoubleJump(jumpNodes[i], jumpEndNodes[i]))
            {
                Debug.Log("Double Jump Pathing Chosen...");
                continue;
            } else
            {
                bool overheadFacingRight = false;
                adjNodesFromOverhead = FindVaccantOverheadForOvershoot(jumpEndNodes[i], ref overheadFacingRight);
                continue;
            }
            
        }

        // Node trimming
        Vector2 oldDirection = Vector2.zero;
        // GraphNode lastFromNode;
        
        foreach(TrimRequestStruct trimRequest in trimRequests)
        {
            TrimInBetween(trimRequest.from, trimRequest.to);
        }

        foreach (WaypointInsertStruct insert in waypointInserts)
        {
            int index = newNodes.FindIndex(d => d == insert.indexNode) + 1;

            newNodes.Insert(index, insert.node);
            newVectorPath.Insert(index, insert.position);

            // InsertWaypointAtPosition(insert.position, insert.node, true);
        }

        foreach (TrimRequestStruct trimRequest in lateTrimRequests)
        {
            TrimInBetween(trimRequest.from, trimRequest.to);
        }

        /*
        // Quick test
        // debugNodes = Helper.BresenhamLine(GridGraphGenerate.gg, new Vector2(-1, 11.4f), new Vector2(8, 7));
        //         Debug.DrawLine(new Vector2(-1, 11.4f), new Vector2(6, 11.4f), Color.red, 10000f);
        List<Vector2> points = new List<Vector2>();
        points.Add(new Vector2(-4, 2f));
        points.Add(new Vector2(2, 13.4f));
        points.Add(new Vector2(-5, 2f));
        points.Add(new Vector2(9, 5f));
        points.Add(new Vector2(-7, 4f));
        debugNodes = Helper.BresenhamLineLoopThrough(GridGraphGenerate.gg, points);
        */

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

    public void TrimInBetween(GraphNode from, GraphNode to)
    {

        int fromIndex = newNodes.FindIndex(d => d == from);
        int toIndex = newNodes.FindIndex(d => d == to);
        // Debug.LogWarning("TRIMMING FROM " + fromIndex + " TO " + toIndex);

        if (fromIndex + 1 == toIndex || fromIndex == toIndex) return;
        //Debug.Log("no returning");
        Debug.Log($"------------");
        Debug.Log($"fromIndex : {fromIndex}");
        Debug.Log($"toIndex : {toIndex}");

        int offset = 0;
        for(int i=fromIndex+1; i<toIndex; i++)
        {
            Debug.Log($"i : {i}");
            Debug.Log($"offset : {offset}");
            Debug.Log("i - offset : " + (i - offset));

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

    public bool CheckTrims(int index)
    {
        foreach (TrimRequestStruct trimRequest in trimRequests)
        {
            int fromIndex = newNodes.FindIndex(d => d == trimRequest.from);
            int toIndex = newNodes.FindIndex(d => d == trimRequest.to);

            if (index > fromIndex && index < toIndex) return true;
        }

        return false;
    }

    public void PadTheseNodes(List<GraphNode> nodes, bool absolute)
    {
        foreach(GraphNode node in nodes)
        {
            PaddingNodeStruct newPaddingNodeStruct = new PaddingNodeStruct(node, absolute);
            allPaddingStructs.Add(newPaddingNodeStruct);
        }
    }
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
        ignoreNodes.Clear();
        ignoreJumpNodes.Clear();
        specialWaypoints.Clear();
        waypointInserts.Clear();
        adjNodesFromOverhead.Clear();
        allPaddingStructs.Clear();
        trimRequests.Clear();
        debugNodes.Clear();
        ignoreJumpNodes.Clear();
    }

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

    // you dont need to check for adjNodes like, every time :/

    // Or dropdown jump, or dropdown double jump
    private bool CalculateDropdown(GraphNode jumpNode, GraphNode jumpEndNode)
    {
        Vector3 jumpNodePosition = (Vector3)jumpNode.position;
        Vector3 jumpEndNodePosition = (Vector3)jumpEndNode.position;

        if (jumpNodePosition.y <= jumpEndNodePosition.y) return false;


        // Am i not grabbing the wrong adj nodes?
        bool foundAdjNodes = false;
        List<GraphNode> adjNodes = Helper.FindAdjacentNodes(jumpEndNode, ref foundAdjNodes, AdjNodeSearchDirection.BOTH);
        if (foundAdjNodes == false) return false;

        adjNodes.Insert(0, jumpEndNode);

        // dropdown

        bool waypointFacingRight = false;

        if (jumpEndNodePosition.x > jumpNodePosition.x)
        {
            waypointFacingRight = true;
        }

        GraphNode closestNode = FindClosestNode(jumpNodePosition, adjNodes);
         Vector3 closestNodePosition = (Vector3)closestNode.position;

        float Sy = jumpEndNodePosition.y - jumpNodePosition.y; // This is bound to be a negative number
        float t_fall = Mathf.Sqrt(2 * Sy / -gravityFall);

        float Sx = Vx * t_fall;

        // Actual drop down logic
        if ((jumpNodePosition.x > -Sx - 0.5f + closestNodePosition.x) && waypointFacingRight ||
            (jumpNodePosition.x < Sx + 0.5f + closestNodePosition.x) && !waypointFacingRight)
        {
            GraphNode dropdownAtThisNode = jumpNode;


            Vector2 direction = (waypointFacingRight) ? Vector2.right : Vector2.left;

            BaseAiController.specialWaypoint newSpecialWaypoint = new BaseAiController.specialWaypoint(
                typeofWaypoint.RUN, jumpNode, () => { baseCharacterController.RunWaypointAI(direction); }, waypointFacingRight);

            if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
            {
                baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                specialWaypoints.Add(newSpecialWaypoint);
                // specialNodeCorrespFunction.Add(jumpEndNode);
            }

            // Debug.Log("?");

            // padding logic & trimming

            List<GraphNode> potentialNodes = new List<GraphNode>();

            foreach (GraphNode node in adjNodes)
            {
                Vector3 nodePosition = (Vector3)node.position;
                if ((jumpNodePosition.x > Sx + nodePosition.x) && waypointFacingRight ||
                    (jumpNodePosition.x < Sx + nodePosition.x) && !waypointFacingRight)
                {
                    potentialNodes.Add(node);
                }
            }

            // The bug is here
            Debug.Log((potentialNodes.Count > 0) ? "added a potential" : "added end node");
            TrimRequestStruct newTrimRequest = new TrimRequestStruct(jumpNode, (potentialNodes.Count>0)? potentialNodes[potentialNodes.Count - 1] : jumpEndNode);
            trimRequests.Add(newTrimRequest);

            if (potentialNodes.Count > 0 && !newNodes.Contains(potentialNodes[potentialNodes.Count - 1])) {
                int index = newNodes.FindIndex(d => d == jumpEndNode);
                newNodes.Insert(index - 1, potentialNodes[potentialNodes.Count - 1]);
                newVectorPath.Insert(index - 1, (Vector3)potentialNodes[potentialNodes.Count - 1].position);

                newNodes.RemoveAt(index);
                newVectorPath.RemoveAt(index);
            }

            List<GraphNode> paddingNodes = Helper.FindAdjacentNodes(jumpEndNode, ref foundAdjNodes,
                (waypointFacingRight) ? AdjNodeSearchDirection.RIGHT : AdjNodeSearchDirection.LEFT, 2 + potentialNodes.Count);
            paddingNodes.Add(jumpEndNode);
            PadTheseNodes(paddingNodes, false);
            // the bug is above this

            return true;
        }
        else
        {
            Debug.LogError("Got a potential node");
            // ...
        }

        return false;
        // dropdown + double jump ?
    }
    private bool CalculateSingleDropdown(GraphNode jumpNode, GraphNode jumpEndNode)
    {
        Vector3 jumpNodePosition = (Vector3)jumpNode.position;
        Vector3 jumpEndNodePosition = (Vector3)jumpEndNode.position;

        // Should I really be returning false here?
        bool foundAdjNodes = false;
        List<GraphNode> adjNodes = Helper.FindAdjacentNodes(jumpEndNode, ref foundAdjNodes, AdjNodeSearchDirection.BOTH);
        if (foundAdjNodes == false) return false;

        GraphNode closestNode = jumpEndNode; // (jumpNodePosition, adjNodes);
        Vector3 closestNodePosition = (Vector3)closestNode.position;

        //if (jumpNodePosition.y + 1.5f <= jumpEndNodePosition.y) return false;

        bool waypointFacingRight = false;
        if (jumpEndNodePosition.x > jumpNodePosition.x)
        {
            waypointFacingRight = true;
        }

        bool Ai_holdSpaceKey = false;
        if ((airborneJumpHeight + jumpNodePosition.y < jumpEndNodePosition.y && maxAirborneJumpHeight + jumpNodePosition.y >= jumpEndNodePosition.y) ||
            ((jumpNodePosition.x - 10f > jumpEndNodePosition.x && !waypointFacingRight) || (jumpNodePosition.x + 10f < jumpEndNodePosition.x && waypointFacingRight)))
        {
            Ai_holdSpaceKey = true;
        }

        // dropdown + single jump (! Rework this !) (Change positioning because of priority)
        Debug.Log("dropdown + single jump");
        waypointFacingRight = false;

        if (jumpEndNodePosition.x > jumpNodePosition.x)
        {
            waypointFacingRight = true;
        }


        float Sy;
        float t_fall;
        float Sx;

        Sy = jumpEndNodePosition.y - jumpNodePosition.y; // This is bound to be a negative number

        float airborneVyi = GetAirborneVyi();


        float Sz = 3f; // magic value
        if (jumpNodePosition.y <= jumpEndNodePosition.y) Sz = 0f;

        float Sb = ((Ai_holdSpaceKey) ? maxAirborneJumpHeight - 0.5f : airborneJumpHeight) - Sy - Sz;
        float t_dropdown = Mathf.Sqrt(Mathf.Abs(2 * Sb / gravityFall));
        float t_rise = 2 * ((Ai_holdSpaceKey) ? maxAirborneJumpHeight : airborneJumpHeight) / airborneVyi;
        t_fall = Mathf.Sqrt(2 * Sz / gravityFall);
        float t_total = t_dropdown + t_rise;

        Sx = t_total * Vx;

        // Allowing for a bit of overshoot
        if ((jumpNodePosition.x < -Sx + closestNodePosition.x + 3f) && waypointFacingRight ||
            (jumpNodePosition.x > Sx + closestNodePosition.x - 3f) && !waypointFacingRight)
        {
            // Adding dropdown waypoint
            GraphNode dropdownAtThisNode = jumpNode;
            Vector3 dropdownAtThisNodePosition = (Vector3)dropdownAtThisNode.position;
            Vector2 dropdownAtThisNodeGG = Helper.TurnPositionIntoPointOnGridGraph(GridGraphGenerate.gg,
                GridGraphGenerate.gg.GetNearest(new Vector3(jumpNodePosition.x + (Vx * t_dropdown), -Sb + jumpNodePosition.y)).node);

            if (!jumpNodesFinal.Contains(dropdownAtThisNode))
            {
                jumpNodesFinal.Add(dropdownAtThisNode);
            }

            GraphNode jumpAtThisNode = GridGraphGenerate.gg.GetNearest(new Vector3(jumpNodePosition.x + (Vx * t_dropdown * ((waypointFacingRight) ? 1 : -1)) +
                ((waypointFacingRight) ? 1f : -1f), ((jumpNodePosition.y <= jumpEndNodePosition.y)? -Sb : -Sb) + jumpNodePosition.y)).node; // what is with this pointless tenary operator again?

            Vector2 direction = (waypointFacingRight) ? Vector2.right : Vector2.left;

            // Adding RUN special waypoint
            BaseAiController.specialWaypoint newSpecialWaypoint = new BaseAiController.specialWaypoint(
                typeofWaypoint.RUN, jumpNode, () => { baseCharacterController.RunWaypointAI(direction); }, waypointFacingRight);

            if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
            {
                baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                specialWaypoints.Add(newSpecialWaypoint);
                // specialNodeCorrespFunction.Add(jumpEndNode);
            }

            // Adding JUMP special waypoint
            newSpecialWaypoint = new BaseAiController.specialWaypoint(
                typeofWaypoint.AIRBORNE_JUMP, jumpAtThisNode, () => { baseCharacterController.JumpWaypointAI(Ai_holdSpaceKey); }, waypointFacingRight, 0.7f,
                jumpNode, jumpEndNode);

            Debug.LogError("IFWAIUJFOWAKJIO LOOOOOK O.oOOSEF " + Ai_holdSpaceKey);

            if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
            {
                baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                specialWaypoints.Add(newSpecialWaypoint);

                WaypointInsertStruct newInsert = new WaypointInsertStruct(newSpecialWaypoint.node, jumpNode);
                waypointInserts.Add(newInsert);

                // Debug.Log("Added a special waypoint");
                // specialNodeCorrespFunction.Add(jumpEndNode);
            }

            // adding padding & trimming
            TrimRequestStruct newTrimRequest = new TrimRequestStruct(jumpNode, closestNode);
            trimRequests.Add(newTrimRequest);

            List<GraphNode> paddingNodes = Helper.FindAdjacentNodes(closestNode, ref foundAdjNodes, (waypointFacingRight) ? AdjNodeSearchDirection.RIGHT : AdjNodeSearchDirection.LEFT, 2);
            PadTheseNodes(paddingNodes, false);
            return true;
        }
        else
        {
            // ...
        }

        return false;
        // dropdown + double jump ?
    }

    // this could be optimized like crazy 
    private bool CalculateDoubleJump(GraphNode jumpNode, GraphNode jumpEndNode)
    {
        // Debug.Log("Double Jump Chosen");
        Vector3 jumpNodePosition = (Vector3)jumpNode.position;
        Vector3 jumpEndNodePosition = (Vector3)jumpEndNode.position;

        bool waypointFacingRight = false;
        bool isCapableOfOverhead = false;
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

        float airborneVyi = GetAirborneVyi();

        float t_rise1 = t_rise;
        float t_rise2 = 2 * GetAirborneJumpHeight() / airborneVyi;
        float Sy;
        float Sz;
        float t_fall1;
        float t_fall2;
        float t_total;

        bool foundAnyPotentialNodes = false;
        List<GraphNode> potentialNodes = new List<GraphNode>();

        // Debug.Log("ofoiewfierbtin");
        foreach (GraphNode node in adjNodesAtJump)
        {
            Vector3 nodePosition = (Vector3)node.position;

            Sy = jumpEndNodePosition.y - nodePosition.y;
            Sz = 3f; // magic value
                     // Debug.Log("ofoiewfierbtin");
                     // Debug.Log("maxJumpHeight : " + maxJumpHeight);
                     // Debug.Log("maxAirborneJumpHeight : " + maxAirborneJumpHeight);
                     // Debug.Log("Sy : " + Sy);

            // Sz logic
            if (maxJumpHeight + maxAirborneJumpHeight < Sy)
            {
                // ..  DON'T PERFORM DOUBLE JUMP
                // Debug.Log("Im being called??????");
                continue;
            }
            else while (true)
                {
                    if (Ai_holdSpaceKey)
                    {
                        if (maxJumpHeight + maxAirborneJumpHeight - Sz < Sy)
                        {
                            Sz -= 0.5f;
                            // Debug.LogError("SZ IS NOW " + Sz);
                        }
                        else break;
                    }
                    else
                    {
                        if (jumpHeight + airborneJumpHeight - Sz < Sy)
                        {
                            Sz -= 0.5f;
                            // Debug.LogError("SZ IS NOWNOWNOWNOW " + Sz);
                        }
                        else break;
                    }
                }

            if (jumpHeight + airborneJumpHeight < Sy && maxJumpHeight + maxAirborneJumpHeight >= Sy)
            {
                Ai_holdSpaceKey = true;
                Debug.Log("holding");
            }

            // Debug.Log("ofoiewfierbtin");

            t_fall1 = Mathf.Sqrt(2 * (((Ai_holdSpaceKey)? maxJumpHeight + maxAirborneJumpHeight : airborneJumpHeight + jumpHeight) - Sy - Sz) / gravityFall);
            Debug.LogWarning(2 * (((Ai_holdSpaceKey) ? maxJumpHeight + maxAirborneJumpHeight : airborneJumpHeight + jumpHeight) - Sy - Sz) / gravityFall);

            t_fall2 = Mathf.Sqrt(2 * Sz / gravityFall);
            t_total = ((Ai_holdSpaceKey) ? t_maxRise : t_rise) + ((Ai_holdSpaceKey) ? t_maxAirborneRise : t_airborneRise) + t_fall2;

            float Sx = t_total * Vx;

            // Debug.Log("ofoiewfierbtin");

            if (maxJumpHeight + maxAirborneJumpHeight >= Sy && 
                (((nodePosition.x > -Sx + jumpEndNodePosition.x) && waypointFacingRight) ||
                ((nodePosition.x > Sx + jumpEndNodePosition.x) && !waypointFacingRight))){
                isCapableOfOverhead = true;
                // Debug.Log("CapabaleOfOverhead : 0");
            }

            if ((nodePosition.x < -Sx + jumpEndNodePosition.x + 0.25f) && waypointFacingRight ||
                (nodePosition.x > Sx + jumpEndNodePosition.x - 0.25f) && !waypointFacingRight)
            {
                if (maxJumpHeight + maxAirborneJumpHeight >= Sy)
                {
                    float Sx_Dropdown = Vx * (((Ai_holdSpaceKey) ? t_maxRise : t_rise) + t_fall1) * ((waypointFacingRight) ? 1 : -1);
                    Debug.Log(Vx);
                    Debug.Log(t_maxRise);
                    Debug.Log(t_rise);
                    Debug.Log(t_fall1);
                    Debug.Log(Sx_Dropdown);

                    float Sy_Dropdown = ((Ai_holdSpaceKey) ? maxJumpHeight : jumpHeight) - ((gravityFall * t_fall1 * t_fall1) / 2);
                    Vector2 secondJumpAtThisNodePosition = new Vector2(Sx_Dropdown + nodePosition.x, Sy_Dropdown + nodePosition.y);

                    GraphNode nodeOfCollision = null;
                    GraphNode nearNode = null;

                    bool collided = BresenhamCollisionDetection(SimulateSingleJump(nodePosition, secondJumpAtThisNodePosition, waypointFacingRight, 4, false, 0, Ai_holdSpaceKey), 3, ref nodeOfCollision, ref nearNode);
                    if (collided) continue;

                    collided = BresenhamCollisionDetection(SimulateSingleJump(secondJumpAtThisNodePosition, jumpEndNodePosition, waypointFacingRight, 4, true, 0, Ai_holdSpaceKey), 3, ref nodeOfCollision, ref nearNode);
                    if (collided) continue;

                    // Debug.Log("Found a potential node");
                    foundAnyPotentialNodes = true;
                    potentialNodes.Add(node);
                    continue;
                }
            }
            else
            {
                if(maxJumpHeight + maxAirborneJumpHeight >= Sy)
                {
                    isCapableOfOverhead = true;
                    // Debug.Log("CapabaleOfOverhead : 0");
                }
                // ...
            }

            // Debug.Log("ofoiewfierbtin");
        }

        if (!foundAnyPotentialNodes)
        {
            if (isCapableOfOverhead)
            {
                // Debug.Log("CapableOfOverhead : 1");
                bool directionOfVaccantNodeFR = false; // FL means facing right and facing is relative to jumpEndNode towards the vaccant node
                FindVaccantOverheadForOvershoot(jumpEndNode, ref directionOfVaccantNodeFR); // pointless function

                CalculateOvershootWaypoints_S(jumpEndNode, jumpNode, 1.5f, 2, TypeofJump.DOUBLE_JUMP, (waypointFacingRight != directionOfVaccantNodeFR && Mathf.Abs(jumpEndNodePosition.x - jumpNodePosition.x) <= 1.5f)
                    && !(Mathf.Abs(jumpEndNodePosition.x - jumpNodePosition.x) <= 1f));
            }

            return false;
        }
        // if(potentialNodes empty, cycle to adjNodesAtEndJump while using the closest node to jumpEndNode

        if (potentialNodes.Count >= 1)
        {
            // Possible to perform double jump
            // Debug.Log("HOW MANY NODES? " + potentialNodes.Count);
            GraphNode jumpAtThisNode = FindClosestNode(jumpNodePosition, potentialNodes);
            Vector3 jumpAtThisNodePosition = (Vector3)jumpAtThisNode.position;

            Sy = jumpEndNodePosition.y - jumpAtThisNodePosition.y;
            Sz = 0.5f; // magic value

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

            // Adding single jump waypoint
            // Finding the position where we need to jump;
            float Sx_Dropdown = Vx * (((Ai_holdSpaceKey) ? t_maxRise : t_rise) + t_fall1) * ((waypointFacingRight) ? 1 : -1);
            // Debug.Log(Sx_Dropdown);

            float Sy_Dropdown = ((Ai_holdSpaceKey) ? maxJumpHeight : jumpHeight) - ((gravityFall * t_fall1 * t_fall1) / 2);

            // rounding to nearest 0.5f
            // float Sx_DropdownRounded = (int)(Sx_Dropdown * 2) / 2f;
            // float Sy_DropdownRounded = (int)(Sy_Dropdown * 2) / 2f;

            // gg.nodes[(z - 1) * gg.width + x];
            GraphNode secondJumpAtThisNode = GridGraphGenerate.gg.GetNearest(new Vector3(Sx_Dropdown + jumpAtThisNodePosition.x, Sy_Dropdown + jumpAtThisNodePosition.y)).node;

            // first jump
            if (!jumpNodesFinal.Contains(jumpAtThisNode))
            {
                jumpNodesFinal.Add(jumpAtThisNode);
                BaseAiController.specialWaypoint newSpecialWaypoint = new BaseAiController.specialWaypoint(
                typeofWaypoint.JUMP, jumpAtThisNode, () => { baseCharacterController.JumpWaypointAI(Ai_holdSpaceKey); }, waypointFacingRight, 0.25f, 
                jumpNode, jumpEndNode);

                if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
                {
                    baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                    specialWaypoints.Add(newSpecialWaypoint);
                    // specialNodeCorrespFunction.Add(jumpEndNode);
                }
            }

            // second jump
            if (!jumpNodesFinal.Contains(secondJumpAtThisNode))
            {
                //jumpNodesFinal.Add(secondJumpAtThisNode);
                BaseAiController.specialWaypoint newSpecialWaypoint = new BaseAiController.specialWaypoint(
                typeofWaypoint.AIRBORNE_JUMP, secondJumpAtThisNode, () => { baseCharacterController.JumpWaypointAI(Ai_holdSpaceKey); }, waypointFacingRight, 0.6f,
                jumpNode, jumpEndNode);

                WaypointInsertStruct newInsert = new WaypointInsertStruct(newSpecialWaypoint.node, jumpAtThisNode);
                waypointInserts.Add(newInsert);

                if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
                {
                    baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                    specialWaypoints.Add(newSpecialWaypoint);
                    // specialNodeCorrespFunction.Add(jumpEndNode);
                }
            }

            // trimming
            TrimRequestStruct newTrimRequest = new TrimRequestStruct(jumpAtThisNode, jumpEndNode);
            trimRequests.Add(newTrimRequest);

            return true;
        }

        // Do overhead double jump calculation
        if(isCapableOfOverhead)
        {
            Debug.Log("CapableOfOverhead");
        }

        return false;
    }

    private bool CalculateSingleJump(GraphNode jumpNode, GraphNode jumpEndNode, GraphNode nextJumpNode, GraphNode nextJumpEndNode, bool masterCall = true)
    {
        // float Vx = 8.5f;
        // float jumpHeight = baseCharacterController.jumpHeight;

        // Sees if we can calculate a single jump to the next jumpEndNode instead

        Debug.Log("trying to calculate single jump");
        if (masterCall == true)
        {
            Debug.Log("IS MASTER");
            if (CalculateSingleJump(jumpNode, nextJumpEndNode, null, null, false))
            {
                ignoreJumpNodes.Add(nextJumpNode);
                return true;
            }
        }
        

        Vector3 jumpEndNodePosition = (Vector3)jumpEndNode.position;
        Vector3 jumpNodePosition = (Vector3)jumpNode.position;

        bool Ai_holdSpaceKey = false;

        if (jumpEndNodePosition.y - jumpNodePosition.y > jumpHeight && maxJumpHeight >= jumpEndNodePosition.y - jumpNodePosition.y)
        {
            Debug.Log("Single Jump will be holding");
            Ai_holdSpaceKey = true;
        }
        else if (maxJumpHeight < jumpEndNodePosition.y - jumpNodePosition.y)
        {
            return false;
        }

        bool waypointFacingRight = false;
        Debug.Log(jumpEndNodePosition);
        Debug.Log(jumpNodePosition);
        if (jumpEndNodePosition.x > jumpNodePosition.x)
        {
            Debug.LogWarning("Hello?????????????????????/");
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
        // adjTargetNodes.Reverse();

        // GraphNode currentTargetNode = adjTargetNodes[adjTargetNodes.Count - 1];
        // Vector3 currentTargetNodePosition = (Vector3)currentTargetNode.position;

        bool willSingleJump = false;
        int index = 0;
        Color[] colors = { Color.red, Color.green, Color.blue, Color.magenta, Color.cyan };

        bool onlyPaddingLeft = false;
        List<GraphNode> wasPaddingNode = new List<GraphNode>();
        GraphNode landingNode = null;

        while (!willSingleJump)
        {
            if (onlyPaddingLeft)
            {
                // can optimize this by not scanning over padding over and over again when i would only need to scan them once to identify them.
                // thus, ignoringing them in the next scan
                foreach (GraphNode node in wasPaddingNode)
                {
                    if (IsOverlappingWithPadding(node))
                    {
                        if (!wasPaddingNode.Contains(node))
                            wasPaddingNode.Add(node);
                        continue;
                    }

                    Vector3 nodePosition = (Vector3)node.position;

                    float Sy_fall = ((Ai_holdSpaceKey)? maxJumpHeight : jumpHeight) - (currentTargetNodePosition.y - nodePosition.y);
                    float t_fall = Mathf.Sqrt(Mathf.Abs(2 * Sy_fall / gravityFall));
                    float Sx = Vx * (((Ai_holdSpaceKey) ? t_maxRise : t_rise) + Mathf.Sqrt(Mathf.Abs(2 * Sy_fall / gravityFall))) * ((waypointFacingRight == false) ? 1 : -1);

                    if ((Sx + currentTargetNodePosition.x > nodePosition.x && -Sx + currentTargetNodePosition.x - 0.5f < nodePosition.x && !waypointFacingRight) || // ?
                            (Sx + currentTargetNodePosition.x < nodePosition.x && Sx + currentTargetNodePosition.x + 0.5f > nodePosition.x  && waypointFacingRight))
                    {
                        jumpAtThisNode = node;

                        BaseAiController.specialWaypoint newSpecialWaypoint = new BaseAiController.specialWaypoint(
                            typeofWaypoint.JUMP, jumpAtThisNode, () => { baseCharacterController.JumpWaypointAI(Ai_holdSpaceKey); }, waypointFacingRight, 0.25f,
                            jumpNode, currentTargetNode);

                        GraphNode nodeOfCollision = null;
                        GraphNode nearNode = null;
                        bool collided = BresenhamCollisionDetection(SimulateSingleJump(nodePosition, currentTargetNodePosition, waypointFacingRight, 4, false, 0, Ai_holdSpaceKey), 3, ref nodeOfCollision, ref nearNode);

                        if (!collided)
                        {
                            if (!jumpNodesFinal.Contains(jumpAtThisNode))
                            {
                                jumpNodesFinal.Add(jumpAtThisNode);
                            }

                            if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
                            {
                                baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                                specialWaypoints.Add(newSpecialWaypoint);

                                // specialNodeCorrespFunction.Add(currentTargetNode);
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
                foreach (GraphNode node in adjNodes)
                {
                    if (IsOverlappingWithPadding(node))
                    {
                        if (!wasPaddingNode.Contains(node))
                            wasPaddingNode.Add(node);
                        continue;
                    }

                    Vector3 nodePosition = (Vector3)node.position;

                    float Sy_fall = ((Ai_holdSpaceKey) ? maxJumpHeight : jumpHeight) - (currentTargetNodePosition.y - nodePosition.y);
                    float t_fall = Mathf.Sqrt(Mathf.Abs(2 * Sy_fall / gravityFall));
                    float Sx = Vx * (((Ai_holdSpaceKey) ? t_maxRise : t_rise) + Mathf.Sqrt(Mathf.Abs(2 * Sy_fall / gravityFall))) * ((waypointFacingRight == false) ? 1 : -1);


                    if ((Sx + currentTargetNodePosition.x > nodePosition.x && !waypointFacingRight) ||
                            (Sx + currentTargetNodePosition.x < nodePosition.x && Sx + currentTargetNodePosition.x + 0.5f > nodePosition.x && waypointFacingRight))
                    {
                        jumpAtThisNode = node;
                        Vector3 jumpAtThisNodePosition = (Vector3)jumpAtThisNode.position;
                        if (jumpAtThisNodePosition.x < baseCharacterController.transform.position.x && waypointFacingRight) continue;
                        else if (jumpAtThisNodePosition.x > baseCharacterController.transform.position.x && !waypointFacingRight) continue;

                        BaseAiController.specialWaypoint newSpecialWaypoint = new BaseAiController.specialWaypoint(
                            typeofWaypoint.JUMP, jumpAtThisNode, () => { baseCharacterController.JumpWaypointAI(Ai_holdSpaceKey); }, waypointFacingRight, 0.25f,
                            jumpNode, currentTargetNode);

                        GraphNode nodeOfCollision = null;
                        GraphNode nearNode = null;
                        Debug.LogWarning("Is waypoint facing right? " + waypointFacingRight);
                        bool collided = BresenhamCollisionDetection(SimulateSingleJump(nodePosition, currentTargetNodePosition, waypointFacingRight, 4, false, 0, Ai_holdSpaceKey), 3, ref nodeOfCollision, ref nearNode);

                        if (!collided)
                        {
                            if (!jumpNodesFinal.Contains(jumpAtThisNode))
                            {
                                jumpNodesFinal.Add(jumpAtThisNode);
                            }

                            if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
                            {
                                baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                                specialWaypoints.Add(newSpecialWaypoint);

                                // specialNodeCorrespFunction.Add(currentTargetNode);
                            }

                            landingNode = currentTargetNode;
                            willSingleJump = true;
                            break;
                        }
                    }
                }
            }
            // Debug.Log("index: " + index);
            // Debug.Log("adjTargetNodes.Count: " + adjTargetNodes.Count);
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
                    break;
                }
            }
            else
            {
                currentTargetNode = adjTargetNodes[index];
                currentTargetNodePosition = (Vector3)adjTargetNodes[index].position;
                int colorIndex = index;
                if (colorIndex > colors.Length - 1) colorIndex -= colors.Length * (Mathf.FloorToInt((float)colorIndex / (float)colors.Length));

                Helper.DrawArrow.ForDebugTimed(currentTargetNodePosition + Vector3.up * 1f, Vector3.down, colors[colorIndex], 1000f);
                index++;
            }

            // Debug.Log("Looping through options");
        }

        if (willSingleJump)
        {
            // trimming & padding
            TrimRequestStruct newTrimRequest = new TrimRequestStruct(jumpAtThisNode, landingNode);
            trimRequests.Add(newTrimRequest);

            List<GraphNode> paddingNodes = Helper.FindAdjacentNodes(jumpEndNode, ref foundAdjNodes, (waypointFacingRight) ? AdjNodeSearchDirection.RIGHT : AdjNodeSearchDirection.LEFT, 2, landingNode);
            PadTheseNodes(paddingNodes, false);

            // Debug.Log("Finished search after " + (index + 1) + " cycle(s)");
            return true;
        }
        else
        {
            Debug.Log("Single jump failed");
            return false;
        }
    }

    // Works but could be optimized to get rid of some loops. However, this does not breach below 180fps which is 3x the 60fps target.
    private bool CalculateSingleJumpDashing(GraphNode jumpNode, GraphNode jumpEndNode)
    {
        Vector3 jumpNodePosition = (Vector3)jumpNode.position;
        Vector3 jumpEndNodePosition = (Vector3)jumpEndNode.position;

        bool Ai_holdSpaceKey = false;
        if (jumpEndNodePosition.y - jumpNodePosition.y > jumpHeight && maxJumpHeight >= jumpEndNodePosition.y - jumpNodePosition.y)
        {
            Debug.Log("Single Jump will be holding");
            Ai_holdSpaceKey = true;
        }
        else if (maxJumpHeight < jumpEndNodePosition.y - jumpNodePosition.y)
        {
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
                // impossible to perform
                Debug.Log("Skippping");
                continue;
            }
            else Debug.LogWarning("Not Skipping");
        
            Sz = 1.5f; // magic number
            Sy = jumpEndNodePosition.y - nodePosition.y;

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
                foundAnyPotentialNode = true;
        
                // if we want to be more performant. We can just stop our search once we find one node potential node since
                // Helper.FindAdjacentNodes is directional
        
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
            Debug.Log("no potential nodes");
            return false;
        }
        Debug.Log("hello?????");

        // At this point and onwards, it is possible to perform a single jump + dash

        foreach (GraphNode node in potentialNodes)
        {
            GraphNode jumpAtThisNode = node;
            Vector3 jumpAtThisNodePosition = (Vector3)jumpAtThisNode.position;

            BaseAiController.specialWaypoint newJumpSpecialWaypoint = new BaseAiController.specialWaypoint(
                typeofWaypoint.JUMP, jumpAtThisNode, () => { baseCharacterController.JumpWaypointAI(Ai_holdSpaceKey); }, waypointFacingRight, 0.25f, jumpNode, jumpEndNode);

            GraphNode nodeOfCollision = null;
            GraphNode nearNode = null;

            Sz = 1.5f; // magic number

            Sy = jumpEndNodePosition.y - jumpAtThisNodePosition.y;

            while (true)
            {
                if (Ai_holdSpaceKey)
                {
                    if (maxJumpHeight - Sz < Sy)
                    {
                        Sz -= 0.5f;
                        // Debug.LogError("SZ IS NOW " + Sz);
                    }
                    else break;
                }
                else
                {
                    if (jumpHeight - Sz < Sy)
                    {
                        Sz -= 0.5f;
                        // Debug.LogError("SZ IS NOWNOWNOWNOW " + Sz);
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

            // gg.nodes[(z - 1) * gg.width + x];
            GraphNode dodgeAtThisNode = GridGraphGenerate.gg.GetNearest(new Vector3(jumpAtThisNodePosition.x + Sx_Dropdown, Sy_Dropdown + jumpAtThisNodePosition.y)).node;

            Vector2 direction = (waypointFacingRight) ? Vector2.right : Vector2.left;
            BaseAiController.specialWaypoint newDodgeSpecialWaypoint = new BaseAiController.specialWaypoint(
            typeofWaypoint.DODGE, dodgeAtThisNode, () => { baseCharacterController.DodgeWaypointAI(direction); }, waypointFacingRight, 0.6f);

            Vector3 dodgeAtThisNodePosition = (Vector3)dodgeAtThisNode.position;

            if (dodgeAtThisNodePosition.y < GridGraphGenerate.gg.size.y / 2) continue;
            Debug.Log("dropdown+single jump committed");
            bool collided = BresenhamCollisionDetection(SimulateSingleJump(jumpAtThisNodePosition, dodgeAtThisNodePosition, waypointFacingRight, 4, false, 0, Ai_holdSpaceKey), 3, ref nodeOfCollision, ref nearNode);
            Debug.Log("dropdown+single jump committed");

            if (collided)
            {
                // Debug.Log("Jump section collided");
                continue;
            }

            List<Vector2> points = new List<Vector2>();
            points.Add(new Vector2(dodgeAtThisNodePosition.x, dodgeAtThisNodePosition.y));
            points.Add(new Vector2(dodgeAtThisNodePosition.x + ((waypointFacingRight) ? SxDash : -SxDash), dodgeAtThisNodePosition.y));

            GraphNode temp = null;
            GraphNode nearNodeTemp = null;
            collided = BresenhamCollisionDetection(points, 3, ref temp, ref nearNodeTemp);

            if (collided)
            {
                // Debug.Log("dodge section collided");
                continue;
            }

            if (!jumpNodesFinal.Contains(jumpAtThisNode))
            {
                jumpNodesFinal.Add(jumpAtThisNode);

                if (!baseAiController.specialWaypoints.Contains(newJumpSpecialWaypoint))
                {
                    baseAiController.specialWaypoints.Add(newJumpSpecialWaypoint);
                    specialWaypoints.Add(newJumpSpecialWaypoint);
                    // specialNodeCorrespFunction.Add(jumpEndNode);
                }
            }

            if (!jumpNodesFinal.Contains(dodgeAtThisNode))
            {
                //jumpNodesFinal.Add(dodgeAtThisNode);

                if (!baseAiController.specialWaypoints.Contains(newDodgeSpecialWaypoint))
                {
                    baseAiController.specialWaypoints.Add(newDodgeSpecialWaypoint);
                    specialWaypoints.Add(newDodgeSpecialWaypoint);

                    WaypointInsertStruct newInsert = new WaypointInsertStruct(newDodgeSpecialWaypoint.node, jumpAtThisNode);
                    waypointInserts.Add(newInsert);
                    // specialNodeCorrespFunction.Add(jumpEndNode);
                }
            }

            // trimming
            TrimRequestStruct newTrimRequest = new TrimRequestStruct(jumpAtThisNode, jumpEndNode);
            trimRequests.Add(newTrimRequest);

            Debug.Log("dropdown+single jump committed");
            return true;
        }

        Debug.Log("dropdown+single jump not committed");
        return false;
    }

    // for a given time, y distance, etc.
    private enum directionEnum { LEFT, MIDDLE, RIGHT };
    // we need an enum on the type of action we are doing, for now, I will assume this is a double jump
    // change the name of 'target' to jumpEndNode

    private enum TypeofJump { SINGLE, DROPDOWN_SINGLE, SINGLE_DODGE, DOUBLE_JUMP };
    
    // Make sure that distanceFromJumpNode is not too great to the point that distanceFromJumpNode/Vx is more than air time, else overshoot is impossible
    // * link helper & simulating
    // * link collision to overshoot
    // * make overshoot for every type of jump
    // * trimming+

    // Check if adj node is going down and not select it
    private void CalculateOvershootWaypoints_S
        (GraphNode target, GraphNode jumpNode, float Sxa=1.5f, int distanceFromJumpNode=0, 
        TypeofJump typeofJump=TypeofJump.DOUBLE_JUMP, bool flip_s=false)
    {
        Debug.Log("Attempting to do an overshoot waypoint_s");

        bool directionOfVaccantNodeFR = false; // FL means facing right and facing is relative to jumpEndNode towards the vaccant node
        List<GraphNode> vaccantNodes = FindVaccantOverheadForOvershoot(target, ref directionOfVaccantNodeFR); // pointless function
        if (flip_s) directionOfVaccantNodeFR = !directionOfVaccantNodeFR;

        // POINTLESS FUNCTION :L
        if (vaccantNodes.Count < 1) return;

        Vector3 targetPosition = (Vector3)target.position;
        Vector3 jumpNodePosition = (Vector3)jumpNode.position;

        GraphNode newJumpNode;
        Vector3 newJumpNodePosition;

        bool Ai_holdSpaceKey = false;

        if (distanceFromJumpNode != 0)
        {
            bool foundAdjNodes = false;
            List<GraphNode> adjNodes = Helper.FindAdjacentNodes(jumpNode, ref foundAdjNodes,
                (directionOfVaccantNodeFR) ? AdjNodeSearchDirection.RIGHT : AdjNodeSearchDirection.LEFT, 0, null, 0);
            if(adjNodes == null) ;
            // do some skipping by using the return value of foundAdjNodes

            if (adjNodes.Count - 1 >= distanceFromJumpNode && foundAdjNodes)
            {

                newJumpNode = adjNodes[distanceFromJumpNode]; // bug here
                newJumpNodePosition = (Vector3)newJumpNode.position;
            } else
            {
                Debug.Log("Could not place newJumpNode @ '" + distanceFromJumpNode + "' away from jumpNode");
                Debug.Log("newJumpNode was placed @ '" + adjNodes.Count + "' away from jumpNode");

                newJumpNode = adjNodes[adjNodes.Count - 1];
                newJumpNodePosition = (Vector3)newJumpNode.position;
            }
        }
        else
        {
            newJumpNode = jumpNode;
            newJumpNodePosition = (Vector3)newJumpNode.position;
        }

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
                Debug.Log(Sy);
                // Sy = Mathf.Clamp(Sy, 0, 8f); // clamping to prevent bug beyond 8f and seems to be a working solution

                if (Sy >= jumpHeight + airborneJumpHeight && maxJumpHeight + maxAirborneJumpHeight >= Sy)
                {
                    Sy = Mathf.Clamp(Sy, 0, 8f);
                    Ai_holdSpaceKey = true;
                    Debug.Log("Holding");
                }
                else
                {
                    Sy = Mathf.Clamp(Sy, 0, 6f);
                    Debug.Log("not holding");
                }

                if (Sy <= 4.5f) Sy = 5f;

                float time_total = GetRemainingTimeAtDoubleJumpTime(Sy, 0f, Ai_holdSpaceKey);
                time_total = Mathf.Clamp(time_total, 0, 1.563938f);

                // Debug.Log("time_total= " + time_total);

                float Sx = Mathf.Abs(targetPosition.x - newJumpNodePosition.x);
                float time_sx = Sx / Vx;

                float time_sxa = Sxa / Vx;

                float time_sxb = time_total - time_sxa - time_sxa - time_sx;
                // Debug.Log("time_sxb= " + time_sxb);
                float Sxb = (time_sxb / 2) * Vx;
                // Sxa_intrude
                // Sxb_intrude

                float airborneJumpTime = GetTimeOfDoubleJumpAirborneJumpTime(Sy, Ai_holdSpaceKey);
                // Debug.Log("airborneJumpTime: " + airborneJumpTime);
                // Debug.Log("airborneJumpHeight: " + GetDoubleJumpHeightAtTime(Sy, airborneJumpTime, Ai_holdSpaceKey));

                float Sya = GetDoubleJumpHeightAtTime(Sy, time_sxa, Ai_holdSpaceKey);
                float Syb = GetDoubleJumpHeightAtTime(Sy, time_sxa * 2 + time_sxb / 2, Ai_holdSpaceKey);

                float SxAirborne = 0f;

                bool queueAirborneJumpBeforeA = false;
                // bool queueAirborneJumpBeforeB = false;

                if (airborneJumpTime < time_sxa)
                {
                    SxAirborne = Vx * airborneJumpTime * ((directionOfVaccantNodeFR) ? -1 : 1);
                    queueAirborneJumpBeforeA = true;
                    // queueAirborneJumpBeforeB = true;
                }
                else if (airborneJumpTime < (time_sxa * 2))
                {
                    SxAirborne = Vx * (airborneJumpTime - time_sxa) * ((directionOfVaccantNodeFR) ? -1 : 1);
                    // queueAirborneJumpBeforeB = true;
                }
                else if (airborneJumpTime < (time_sxa * 2 + time_sxb / 2))
                {
                    SxAirborne = Vx * (airborneJumpTime - time_sxa * 2) * ((directionOfVaccantNodeFR) ? 1 : -1);
                    // queueAirborneJumpBeforeB = true;
                }
                else if (airborneJumpTime < (time_sxa * 2 + time_sxb))
                {
                    SxAirborne = Vx * (airborneJumpTime - time_sxa * 2 - time_sxb / 2) * ((directionOfVaccantNodeFR) ? 1 : -1);
                } // probably don't need to do any more if statements...

                // facing right!
                Vector2 Sxa_point = new Vector2(((directionOfVaccantNodeFR) ? -Sxa : Sxa) + newJumpNodePosition.x, GetDoubleJumpHeightAtTime(Sy, time_sxa, Ai_holdSpaceKey) + newJumpNodePosition.y);
                Vector2 Sxb_point = new Vector2(((directionOfVaccantNodeFR) ? Sxb : -Sxb) + newJumpNodePosition.x, GetDoubleJumpHeightAtTime(Sy, time_sxa + time_sxa + time_sxb / 2, Ai_holdSpaceKey) + newJumpNodePosition.y);
                Vector2 airJumpPoint = new Vector2(newJumpNodePosition.x + SxAirborne, newJumpNodePosition.y + GetDoubleJumpHeightAtTime(Sy, airborneJumpTime, Ai_holdSpaceKey));

                // For Sxa
                GraphNode collisionNode = null;
                GraphNode nearNode = null;
                List<Vector2> points = new List<Vector2>();
                points.Add(Sxa_point);
                points.Add(newJumpNodePosition);

                bool Sxa_pathCollided = BresenhamCollisionDetection(points, 8, ref collisionNode, ref nearNode);
                float Sxa_collides;
                bool Sxa_willWait = false; // used for trimming
                bool Sxa_airborneJumpWithinWait = false; // used for trimming
                float time_sxaCollision = 0f;
                GraphNode calculatedNode_A_WAIT = null;

                if (Sxa_pathCollided)
                {
                    Vector3 collisionNodePosition = (Vector3)collisionNode.position;
                    Sxa_collides = Mathf.Abs(jumpNodePosition.x - collisionNodePosition.x);

                    if (Sxa_collides < Sxa)
                    {
                        // check if its possible to just jump backwards and restart the calculation
                        float diff = (Sxa - Sxa_collides);
                        time_sxaCollision = diff / Vx;

                        if (time_sxaCollision > airborneJumpTime) Sxa_airborneJumpWithinWait = true;

                        // create waiting node
                        Sxa = Sxa_collides + ((directionOfVaccantNodeFR) ? 0.5f : -0.5f);
                        time_sxa = Sxa / Vx;
                        Sxa_point = new Vector2(((directionOfVaccantNodeFR) ? -Sxa + 0.5f : Sxa - 0.5f) + newJumpNodePosition.x, GetDoubleJumpHeightAtTime(Sy, time_sxa, Ai_holdSpaceKey) + newJumpNodePosition.y);

                    }
                }

                // Sxa -> Sxb
                points.Clear();
                points.Add(Sxa_point);
                points.Add(Sxb_point);

                bool Sxb_pathCollided = BresenhamCollisionDetection(points, 8, ref collisionNode, ref nearNode);
                if (!Sxb_pathCollided) Sxb_pathCollided = !GridGraphGenerate.gg.GetNearest(Sxb_point).node.Walkable;

                float Sxb_collides;
                bool Sxb_willWait = false; // used for trimming
                bool Sxb_airborneJumpWithinWait = false; // used for trimming
                float time_sxbCollision = 0f;
                GraphNode calculatedNode_B_WAIT = null;
                float temp = 0f;

                if (Sxb_pathCollided)
                {
                    // Debug.LogWarning("COLLIDED");
                    Vector2 nearNodePosition;
                    if (nearNode != null)
                        nearNodePosition = (Vector3)nearNode.position;
                    else nearNodePosition = Sxa_point;

                    Sxb_collides = Mathf.Abs(newJumpNodePosition.x - nearNodePosition.x);

                    if (Sxb_collides < Sxb)
                    {

                        // check if its possible to just jump backwards and restart the calculation
                        float diff = (Sxb - Sxb_collides);
                        time_sxbCollision = diff / Vx;

                        Sxb_willWait = true;

                        Sxb = Sxb_collides;
                        //time_sxb = Sxb_collides / Vx;
                        // ((directionOfVaccantNodeFR) ? Sxb : -Sxb) + newJumpNodePosition.x
                        // Debug.Log("Height: " + GetDoubleJumpHeightAtTime(Sy, time_sxa + time_sxa + time_sxb, Ai_holdSpaceKey));
                        // Debug.Log("time_sxa : " + time_sxa);
                        // Debug.Log("time_sxb : " + time_sxb);
                        float padding = 1f;

                        Sxb_point = new Vector2(nearNodePosition.x + ((directionOfVaccantNodeFR) ? -padding : padding),
                            (Mathf.Abs(Sxa_point.y - targetPosition.y) > 0.5f && Mathf.Abs(Sxa_point.x - targetPosition.x) > 0.5f) ?
                            GetDoubleJumpHeightAtTime(Sy, time_sxa + time_sxb, Ai_holdSpaceKey) + newJumpNodePosition.y - 0.5f :
                            ((Mathf.Abs(Sxb_point.y - airJumpPoint.y) >= 1f && Mathf.Abs(Sxb_point.x - airJumpPoint.x) > 0.5f)) ? Sxa_point.y : airJumpPoint.y);

                        temp = nearNodePosition.x + ((directionOfVaccantNodeFR) ? -padding : padding);

                        calculatedNode_B_WAIT = GridGraphGenerate.gg.GetNearest(Sxb_point).node;
                        BaseAiController.specialWaypoint waitWaypoint = new BaseAiController.specialWaypoint(typeofWaypoint.WAIT, calculatedNode_B_WAIT,
                           () => { baseCharacterController.HoldMovementAi(Vector2.zero,
                               ((Mathf.Abs(Sxa_point.y - targetPosition.y) > 0.5f && Mathf.Abs(Sxa_point.x - targetPosition.x) > 0.5f) &&
                               (Mathf.Abs(Sxb_point.y - airJumpPoint.y) >= 1f && Mathf.Abs(Sxb_point.x - airJumpPoint.x) > 0.5f)) ? time_sxbCollision / 3 : 0f); }, false, 1f, null, null);

                        if (!baseAiController.specialWaypoints.Contains(waitWaypoint))
                        {
                            Debug.Log("added a wait");
                            baseAiController.specialWaypoints.Add(waitWaypoint);
                            debugNodes.Add(waitWaypoint.node);
                            specialWaypoints.Add(waitWaypoint);
                        }
                        // set a waiting node
                    }
                }

                if (Sxa_willWait && time_sxbCollision + time_sxa + time_sxaCollision > airborneJumpTime) Sxb_airborneJumpWithinWait = true;
                else if (!Sxa_willWait && time_sxbCollision + time_sxa > airborneJumpTime) Sxb_airborneJumpWithinWait = true;

                // Sxb -> End (carry out height reduction but this will mean we will have to put this in the front of the queue)


                // Debug.LogError("Sya : " + Sya + " Syb : " + Syb);


                GraphNode calculatedNode_A = null;

                if (!Sxa_willWait)
                    calculatedNode_A = GridGraphGenerate.gg.GetNearest(new Vector3(
                        newJumpNodePosition.x + ((directionOfVaccantNodeFR) ? -1 : 1) * Sxa, newJumpNodePosition.y + Sya)).node;

                GraphNode calculatedNode_B = null;

                if (!Sxb_willWait)
                    calculatedNode_B = GridGraphGenerate.gg.GetNearest(new Vector3(
                        newJumpNodePosition.x + ((directionOfVaccantNodeFR) ? 1 : -1) * Sxb, newJumpNodePosition.y + Syb)).node;

                // Debug.Log("Double jump time: " +GetDoubleJumpHeightAtTime(Sy, airborneJumpTime, Ai_holdSpaceKey));
                GraphNode calculatedNode_AirborneJump = null;

                if (Sxa_airborneJumpWithinWait) {
                    // Debug.LogWarning("1st case");
                    calculatedNode_AirborneJump = GridGraphGenerate.gg.GetNearest(new Vector3(
                    newJumpNodePosition.x + ((directionOfVaccantNodeFR) ? -1 : 1) * Sxa, newJumpNodePosition.y + GetDoubleJumpHeightAtTime(Sy, airborneJumpTime, Ai_holdSpaceKey))).node;
                }
                else if (Sxb_airborneJumpWithinWait)
                {
                    // Debug.LogWarning("2nd case");
                    calculatedNode_AirborneJump = GridGraphGenerate.gg.GetNearest(new Vector3(
                    newJumpNodePosition.x + ((directionOfVaccantNodeFR) ? 1 : -1) * Sxb, newJumpNodePosition.y + GetDoubleJumpHeightAtTime(Sy, airborneJumpTime, Ai_holdSpaceKey))).node;
                }
                else
                {
                    if (Sxb_willWait)
                    {
                        // Debug.LogWarning("3rd case");
                        calculatedNode_AirborneJump = GridGraphGenerate.gg.GetNearest(new Vector3(
                        temp, newJumpNodePosition.y + GetDoubleJumpHeightAtTime(Sy, airborneJumpTime, Ai_holdSpaceKey))).node;
                    }
                    else
                    {
                        // Debug.LogWarning("4th case");
                        calculatedNode_AirborneJump = GridGraphGenerate.gg.GetNearest(new Vector3(
                        newJumpNodePosition.x + SxAirborne, newJumpNodePosition.y + GetDoubleJumpHeightAtTime(Sy, airborneJumpTime, Ai_holdSpaceKey))).node;
                    }
                }

                // first jump waypoint
                BaseAiController.specialWaypoint newSpecialWaypoint = new BaseAiController.specialWaypoint(typeofWaypoint.JUMP, newJumpNode,
                    () => { baseCharacterController.JumpWaypointAI(Ai_holdSpaceKey, true); }, (directionOfVaccantNodeFR) ? true : false, 0.4f, newJumpNode, target); // ? im calculating wheather waypointFacingRight wrongly

                // we trim then insert, we have to tell our program not to trim or either re insert
                if (CheckTrims(newNodes.FindIndex(d => d == newJumpNode)))
                {
                    Debug.LogError("CHECKED TRIMS");
                    WaypointInsertStruct newInsert = new WaypointInsertStruct(newJumpNode, jumpNode);
                    waypointInserts.Add(newInsert);
                }

                if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
                {
                    baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                    debugNodes.Add(newSpecialWaypoint.node);
                    specialWaypoints.Add(newSpecialWaypoint);

                    // specialNodeCorrespFunction.Add(jumpEndNode);
                }

                // airborne jump waypoint
                newSpecialWaypoint = new BaseAiController.specialWaypoint(typeofWaypoint.AIRBORNE_JUMP, calculatedNode_AirborneJump,
                    () => { baseCharacterController.JumpWaypointAI(Ai_holdSpaceKey); }, (directionOfVaccantNodeFR) ? true : false, 0.9f); // ? im calculating wheather waypointFacingRight wrongly

                if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
                {
                    Debug.LogWarning("Is this even added?");
                    baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                    debugNodes.Add(newSpecialWaypoint.node);
                    specialWaypoints.Add(newSpecialWaypoint);
                    // specialNodeCorrespFunction.Add(jumpEndNode);
                }


                // Node A
                if (!Sxa_willWait)
                {
                    newSpecialWaypoint = new BaseAiController.specialWaypoint(typeofWaypoint.RUN, calculatedNode_A,
                        () => { baseCharacterController.RunWaypointAI((directionOfVaccantNodeFR) ? Vector2.right : Vector2.left); }, (directionOfVaccantNodeFR) ? true : false, 0.6f);

                    if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
                    {
                        Vector3 calculatedJumpNodePos = (Vector3)calculatedNode_AirborneJump.position;
                        Vector3 towardsOvershootNodePos = (Vector3)calculatedNode_A.position;
                        debugNodes.Add(newSpecialWaypoint.node);

                        baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                        specialWaypoints.Add(newSpecialWaypoint);
                        // specialNodeCorrespFunction.Add(jumpEndNode);
                    }
                }

                // trimming
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

                // Corresponding queueing logic

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
                    newSpecialWaypoint = new BaseAiController.specialWaypoint(typeofWaypoint.RUN, calculatedNode_B,
                        () => { baseCharacterController.RunWaypointAI((directionOfVaccantNodeFR) ? Vector2.left : Vector2.right); }, (directionOfVaccantNodeFR) ? false : true, 1f);

                    if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
                    {
                        debugNodes.Add(newSpecialWaypoint.node);

                        baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                        specialWaypoints.Add(newSpecialWaypoint);
                        // specialNodeCorrespFunction.Add(jumpEndNode);
                    }
                }

                // Corresponding queuing logic
                
                if (Sxb_willWait && Sxb_airborneJumpWithinWait)
                {
                    WaypointInsertStruct newInsert = new WaypointInsertStruct(calculatedNode_B_WAIT, latestInsertNode);
                    waypointInserts.Add(newInsert);

                    // newInsert = new waypointInsertStruct(calculatedNode_AirborneJump, calculatedNode_B_WAIT);
                    // waypointInserts.Add(newInsert);
                }
                else if (Sxb_willWait)
                {

                    WaypointInsertStruct newInsert = new WaypointInsertStruct(calculatedNode_B_WAIT, latestInsertNode);
                    waypointInserts.Add(newInsert);

                    newInsert = new WaypointInsertStruct(target, latestInsertNode);
                    waypointInserts.Add(newInsert);

                    // newInsert = new waypointInsertStruct(newSpecialWaypoint.node, calculatedNode_B_WAIT);
                    // waypointInserts.Add(newInsert);
                }
                else
                {
                    WaypointInsertStruct newInsert = new WaypointInsertStruct(calculatedNode_B, latestInsertNode);
                    waypointInserts.Add(newInsert);
                }
                
                jumpNodesFinal.Add(newJumpNode);

                break;
        }
    }

    // private void CalculateOvershootWaypoints_C(...){...;}

    // Apart of interception?
    private void CalculateWaitingWaypoint()
    {

    }

    // GridGraph[] futureGraphsF
    private void CalculateForesight(GraphNode dynamicNode)
    {

    }

    // Add direction control
    // false - left
    // true - right
    // Left is priority
    private List<GraphNode> FindVaccantOverheadForOvershoot(GraphNode scanNodePoint, ref bool retDirection)
    {
        GraphNode currentNodeBeingVetted;
        List<GraphNode> vaccantNodes = new List<GraphNode>(); // Ranges from 0 to 2

        // Checking nodes to the right
        bool notVaccant = false;
        Vector3 scanNodePointPosition = Helper.TurnPositionIntoPointOnGridGraph(GridGraphGenerate.gg, scanNodePoint);

        for (int z = 0; z < 3; z++)
        {
            currentNodeBeingVetted = GridGraphGenerate.gg.nodes[(z - 1 + (int)scanNodePointPosition.y) * GridGraphGenerate.gg.width + ((int)scanNodePointPosition.x + 1)];
            if (currentNodeBeingVetted.Penalty == GridGraphGenerate.lowPenalty) notVaccant = true;
        }

        if (!notVaccant) { 
            vaccantNodes.Add(GridGraphGenerate.gg.nodes[((int)scanNodePointPosition.y) * GridGraphGenerate.gg.width + ((int)scanNodePointPosition.x + 1)]);
            retDirection = true;
        }

        // Checking nodes to the left
        notVaccant = false;
        scanNodePointPosition = Helper.TurnPositionIntoPointOnGridGraph(GridGraphGenerate.gg, scanNodePoint);

        for (int z = 0; z < 3; z++)
        {
            currentNodeBeingVetted = GridGraphGenerate.gg.nodes[(z - 1 + (int)scanNodePointPosition.y) * GridGraphGenerate.gg.width + ((int)scanNodePointPosition.x - 1)];
            if (currentNodeBeingVetted.Penalty == GridGraphGenerate.lowPenalty) notVaccant = true;
        }

        if (!notVaccant)
        {
            vaccantNodes.Add(GridGraphGenerate.gg.nodes[((int)scanNodePointPosition.y) * GridGraphGenerate.gg.width + ((int)scanNodePointPosition.x - 1)]);
            retDirection = false;
        }
        if (vaccantNodes.Count == 0) retDirection = false;
        return vaccantNodes;
    }

    /// <summary> 
    /// Used to detect collisions when simulating our pathing
    /// </summary>
    /// 
    /// <returns>
    /// Returns true if point node will be in range of a nonwalkable node
    /// else, it will return false.
    /// </returns>
    private bool CheckForCollisions(GraphNode point, int heightInNodes)
    {
        if (!point.Walkable) return true;
        else
        {
            GraphNode currentNodeBeingVetted;

            /* if (originalVectorPoint != null)
            {
                if (originalVectorPoint.x*0.5f > GridGraphGenerate.gg.width || originalVectorPoint.x*0.5f < -GridGraphGenerate.gg.width) return true; // should this be returning true?
            } */

            Vector3 pointPosition = Helper.TurnPositionIntoPointOnGridGraph(GridGraphGenerate.gg, point);

            for (int z = 0; z < heightInNodes; z++)
            {
                if (GridGraphGenerate.gg.nodes.Length < (z + 1 + (int)pointPosition.y) * GridGraphGenerate.gg.width + ((int)pointPosition.x)) return true;

                currentNodeBeingVetted = GridGraphGenerate.gg.nodes[(z + 1 + (int)pointPosition.y) * GridGraphGenerate.gg.width + ((int)pointPosition.x)];
                if (!currentNodeBeingVetted.Walkable) return true;

                // Left boundary
                if ((int)pointPosition.x == 0) return true;
                // right boundary
                if ((int)pointPosition.x == GridGraphGenerate.gg.width) return true;
                // upper boundary
                if ((int)pointPosition.y == 0) return true;
            }
        }

        return false;
    }

    /// <summary> 
    /// Used to detect collisions when simulating our pathing
    /// </summary>
    /// 
    /// <returns>
    /// Returns true if point node will be in range of a nonwalkable node
    /// else, it will return false.
    /// </returns>
    private bool CheckForCollisions(Vector3 point, int heightInNodes)
    {
        Vector3 pointPosition = Helper.TurnPositionIntoPointOnGridGraph(GridGraphGenerate.gg, point);
        GraphNode node = GridGraphGenerate.gg.nodes[((int)pointPosition.y) * GridGraphGenerate.gg.width + ((int)pointPosition.x)];

        if (!node.Walkable) return true;
        else
        {
            GraphNode currentNodeBeingVetted;

            for (int z = 0; z < heightInNodes-1; z++)
            {
                currentNodeBeingVetted = GridGraphGenerate.gg.nodes[(z + 1 + (int)pointPosition.y) * GridGraphGenerate.gg.width + ((int)pointPosition.x)];
                if (!node.Walkable) return true;
            }
        }

        return false;
    }

    // Help from http://members.chello.at/~easyfilter/bresenham.html
    // true if we collidied
    /// <summary>
    /// 
    /// </summary>
    /// <param name="points"></param>
    /// <param name="collisionHeightInNodes"></param>
    /// <param name="nodeOfCollision"></param>
    /// <param name="nearNode"></param>
    /// <returns>returns true if I have collided, else returns false</returns>
    private bool BresenhamCollisionDetection(List<Vector2> points, int collisionHeightInNodes, ref GraphNode nodeOfCollision, ref GraphNode nearNode)
    {
        List<GraphNode> nodes = Helper.BresenhamLineLoopThrough(GridGraphGenerate.gg, points);

        foreach (GraphNode node in nodes)
        {
            debugNodes.Add(node);
            Vector3 nodePosition = (Vector3)node.position;
            if (node.Walkable & node != nodes[nodes.Count-1]) nearNode = node;

            if (CheckForCollisions(node, collisionHeightInNodes))
            {
                nodeOfCollision = node;
                return true;
            }


        }

        return false;
    }

    // Simulating jumps and movement

    private List<Vector2> SimulateSingleJump(Vector2 position, Vector2 endPosition, bool facingRight, int simulationResoultion, bool airborne = false, int numOfAirborneJmps=0, bool Ai_holdSpaceKey=false)
    {
        List<Vector2> points = new List<Vector2>();

        float store_maxJumpHeight = maxJumpHeight;
        float store_jumpHeight = jumpHeight;
        float store_t_maxRise = t_maxRise;
        float store_t_rise = t_rise;
        
        if(airborne)
        {
            maxJumpHeight = maxAirborneJumpHeight;
            jumpHeight = airborneJumpHeight;
            t_maxRise = t_maxAirborneRise;
            t_rise = t_airborneRise;
        }

        Vector2 oldPosition = Vector2.zero;

        Vector2 jumpNodePosition = position;
        Vector2 jumpEndNodePosition = (Vector3)endPosition;
        points.Add(jumpNodePosition);

        Debug.DrawLine(jumpNodePosition, Vector2.zero, Color.magenta, 5f);
        // Simulating jump points
        float Sy_fall = ((Ai_holdSpaceKey)? maxJumpHeight : jumpHeight) - (jumpEndNodePosition.y - jumpNodePosition.y);
        float x = Vx * (((Ai_holdSpaceKey) ? t_maxRise : t_rise) + Mathf.Sqrt(2 * Sy_fall / gravityFall)) * ((facingRight == false) ? 1 : -1);

        // BaseAiController.specialWaypoint specialWaypoint = specialWaypoints.FindIndex
        for (int k = 0; k < simulationResoultion; k++)
        {
            float curSx = (x / simulationResoultion) * k;
            float curSy = 0f;
            float elaspedTime = ((facingRight) ? -curSx : curSx) / Vx;
            if (elaspedTime < t_rise)
            {
                curSy = (((airborne)? GetAirborneVyi(numOfAirborneJmps) : Vyi) * elaspedTime) + ((-gravityRise * elaspedTime * elaspedTime * ((Ai_holdSpaceKey) ? 0.8f : 1f)) / 2);
            }
            else
            {
                curSy = ((Ai_holdSpaceKey) ? maxJumpHeight : jumpHeight) + (-gravityFall * (elaspedTime - ((Ai_holdSpaceKey) ? t_maxRise : t_rise))
                    * (elaspedTime - ((Ai_holdSpaceKey) ? t_maxRise : t_rise)) * 0.5f);
                // curSy = (Vyi * elaspedTime) + ((-gravityFall * elaspedTime * elaspedTime) / 2);
            }

            Vector3 jumpPos = position;
            Vector2 newPosition = new Vector2((jumpPos.x - curSx), (jumpPos.y + curSy));

            /* shift this out */
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

    // Simulate Dropdown end position can just use BresenhamCollisionDetection and setting two points

    // Height
    private float GetSingleJumpHeightAtTime (float time, bool Ai_holdSpaceKey)
    {
        float deltaHeight = jumpHeight + ((Vyi * time) + ((-gravityFall * time * time) / 2));
        return deltaHeight;
    }

    private float GetDoubleJumpHeightAtTime(float Sy, float time, bool Ai_holdSpaceKey)
    {
        float airborneVyi = GetAirborneVyi();

        float t_rise1 = (Ai_holdSpaceKey)? t_maxRise : t_rise;
        float t_rise2 = (Ai_holdSpaceKey) ? t_maxAirborneRise : t_airborneRise; // 2 * GetAirborneJumpHeight() / airborneVyi;

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
        // Debug.Log("Sb = " + Sb);
        t_fall1 = Mathf.Sqrt(Mathf.Abs(2 * (-Sb) / gravityFall));
        t_fall2 = Mathf.Sqrt(Mathf.Abs(2 * Sz / gravityFall));
        t_total = t_rise1 + t_fall1 + t_rise2 + t_fall2;

        float deltaHeight;
        // Debug.Log("time : " + time);
        // Debug.Log("t_rise1 + t_fall1 : " + (t_rise1 + t_fall1));
        // Debug.Log("t_rise1 : " + t_rise1);
        // Debug.Log("t_fall1 : " + t_fall1);

        if (time <= t_rise1)
        {
            // Debug.Log("time case 1");
            deltaHeight = (Vyi * time) + ((-gravityRise * time * time * ((Ai_holdSpaceKey) ? 0.8f : 1f)) / 2);
            // Debug.LogWarning(deltaHeight);
            return deltaHeight;
        }
        else if (time + 0.0000001 < t_rise1 + t_fall1) // the 0.0000001 is to fix a bug when at max height
        {
            // Debug.Log("time case 2");
            float new_time = time - (t_rise1);
            deltaHeight = ((Ai_holdSpaceKey) ? maxJumpHeight : jumpHeight) + ((-gravityFall * new_time * new_time) / 2);
            // Debug.LogWarning(deltaHeight);
            return deltaHeight;
        }
        else if (time < t_rise1 + t_fall1 + t_rise2) // fix this like above
        {
            // Debug.Log("time case 3");
            float new_time = Mathf.Abs(time - (t_rise1 + t_fall1));
            // Debug.Log("airborne vyi: " + GetAirborneVyi(0));
            deltaHeight = (((Ai_holdSpaceKey)? maxJumpHeight : jumpHeight) - Sb) 
                + ((GetAirborneVyi(0) * new_time) + ((-gravityRise * new_time * new_time * ((Ai_holdSpaceKey) ? 0.8f : 1f)) / 2));

            /*
            Debug.Log(jumpHeight);
            Debug.Log(Sb);
            Debug.Log(new_time);
            Debug.Log(gravityFall);
            */

            // Debug.LogWarning(deltaHeight);
            return deltaHeight;
        }
        else // fix this like above
        {
            // Debug.Log("time case 4");
            float new_time = time - (t_rise1 + t_fall1 + t_rise2);
            // Debug.Log("airborne vyi: " + GetAirborneVyi(0));
            deltaHeight = ((Ai_holdSpaceKey) ? maxJumpHeight + maxAirborneJumpHeight: jumpHeight + airborneJumpHeight) + Sb
                + ((-gravityFall * new_time * new_time) / 2);

            /*
            Debug.Log(jumpHeight);
            Debug.Log(Sb);
            Debug.Log(new_time);
            Debug.Log(gravityFall);
            */

            // Debug.LogWarning(deltaHeight);
            return deltaHeight;
        }
    }

    // Time
    private float GetRemainingTimeAtSingleJumpTime(float height, bool Ai_holdSpaceKey) { return 0f; }
    private float GetRemainingTimeAtDoubleJumpTime(float Sy, float time, bool Ai_holdSpaceKey)
    {
        // float airborneVyi = GetAirborneVyi();

        float t_rise1 = (Ai_holdSpaceKey) ? t_maxRise : t_rise; // Debug.LogWarning(t_rise1);
        float t_rise2 = (Ai_holdSpaceKey) ? t_maxAirborneRise : t_airborneRise; // Debug.LogWarning(t_rise2); // 2 * GetAirborneJumpHeight() / airborneVyi;

        float t_fall1 = 0f;
        float t_fall2 = 0f;
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

        float Sb = ((Ai_holdSpaceKey) ? maxJumpHeight + maxAirborneJumpHeight : jumpHeight + airborneJumpHeight) - Sy - Sz;

        t_fall1 = Mathf.Sqrt(Mathf.Abs(2 * -Sb / gravityFall)); // Debug.LogError(t_fall1);
        t_fall2 = Mathf.Sqrt(2 * Sz / gravityFall); // Debug.LogError(t_fall2);
        t_total = t_rise1 + t_fall1 + t_rise2 + t_fall2;

        // Debug.Log("t_total in f(x)= " + t_total);
        return t_total - time;
    }
    private float GetTimeOfDoubleJumpAirborneJumpTime(float Sy, bool Ai_holdSpaceKey)
    {

        float t_rise1 = (Ai_holdSpaceKey) ? t_maxRise : t_rise;

        float t_fall1;

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

        float Sb = ((Ai_holdSpaceKey) ? maxJumpHeight + maxAirborneJumpHeight : jumpHeight + airborneJumpHeight) - Sy - Sz;

        t_fall1 = Mathf.Sqrt(Mathf.Abs(2 * -Sb / gravityFall));
        // Debug.Log("t_fall1 : " + t_fall1);
        return t_rise1 + t_fall1;
    }

    // Velocity
    private float GetVyOfDoubleJumpTime(float Sy, float time, float Vyi, bool Ai_holdSpaceKey)
    {
        float timeOfDoubleJump = GetTimeOfDoubleJumpAirborneJumpTime(Sy, Ai_holdSpaceKey);
        float Vy = 0f;

        if (time < timeOfDoubleJump && time < t_rise)
        {
            Vy = Mathf.Sqrt(Vyi * Vyi + 2 * ((Ai_holdSpaceKey) ? 0.8f : 1f) * gravityRise * GetDoubleJumpHeightAtTime(Sy, time, Ai_holdSpaceKey));
        } if (time < timeOfDoubleJump)
        {
            Vy = Mathf.Sqrt(Vyi * Vyi + 2 * gravityFall * GetDoubleJumpHeightAtTime(Sy, time, Ai_holdSpaceKey));
        } else if(time > timeOfDoubleJump && time < t_rise + timeOfDoubleJump)
        {
            Vy = Mathf.Sqrt(Vyi * Vyi + 2 * ((Ai_holdSpaceKey) ? 0.8f : 1f) * gravityRise * GetDoubleJumpHeightAtTime(Sy, time, Ai_holdSpaceKey));
        } else
        {
            Vy = Mathf.Sqrt(Vyi * Vyi + 2 * gravityFall * GetDoubleJumpHeightAtTime(Sy, time, Ai_holdSpaceKey));
        }

        return Vy;
    }
    /*
    private float GetDropdownHeightAtTime(float deltaSy, float time)
    {

    }

    private float GetDropdownSingleJumpHeightAtTime(float deltaSy, float time)
    {

    }
    */

    /// <summary>
    /// Converts points from simulated points into points along the gridgraph
    /// </summary>
    /// <returns>
    /// Returns points along the gridgraph and trips overlapping points
    /// </returns>
    private List<GraphNode> ConvertPositionsIntoGridgraphPoints(List<Vector3> positions)
    {
        List<GraphNode> points = new List<GraphNode>();
        Vector2 previousCorodinates = Vector2.zero;

        foreach (Vector3 vector in positions)
        {
            Vector2 currentCorodinates = Helper.TurnPositionIntoPointOnGridGraph(GridGraphGenerate.gg, vector);
            if (previousCorodinates == Vector2.zero || previousCorodinates != currentCorodinates)
            {
                points.Add(GridGraphGenerate.gg.nodes[(int)(currentCorodinates.y) * GridGraphGenerate.gg.width + (int)currentCorodinates.x]);
                previousCorodinates = currentCorodinates;
                continue;
            }
        }

        return points;
    }

    // later, @v2.0 release, I will go around commenting my functions & variables like this in order to make
    // it more scalable (https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/recommended-tags)

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.black;
        foreach (GraphNode node in ignoreNodes)
        {
            Gizmos.DrawCube((Vector3)node.position, new Vector3(0.5f, 0.5f));
        }

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

        // Drawing unique special waypoint gizmos
        foreach (BaseAiController.specialWaypoint specialWaypoint in specialWaypoints)
        {
            Helper.DrawArrow.ForGizmo(specialWaypoint.nodePosition + Vector3.down * 1f, Vector3.up, Color.green);

            switch (specialWaypoint.waypointType)
            {
                case typeofWaypoint.RUN:
                    break;

                case typeofWaypoint.JUMP:
                    Vector2 oldPosition = Vector2.zero;
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawCube(specialWaypoint.nodePosition, new Vector3(0.5f, 0.5f));

                    Vector3 jumpNodePosition = (Vector3)specialWaypoint.contextJumpNode.position;
                    Vector3 jumpEndNodePosition = (Vector3)specialWaypoint.contextJumpEndNode.position;

                    float Sy_fall = jumpHeight - (jumpEndNodePosition.y - jumpNodePosition.y);
                    float x = Vx * ((2 * jumpHeight / Vyi) + Mathf.Sqrt(2 * Sy_fall / gravityFall)) * ((specialWaypoint.facingRight == false) ? 1 : -1);

                    // BaseAiController.specialWaypoint specialWaypoint = specialWaypoints.FindIndex
                    for (int k = 0; k < resolution; k++)
                    {
                        float curSx = (x / resolution) * k;
                        float curSy = 0f;
                        float elaspedTime = ((specialWaypoint.facingRight)? -curSx : curSx) / Vx;
                        if (elaspedTime < t_rise)
                        {
                            curSy = (Vyi * elaspedTime) + ((-gravityRise * elaspedTime * elaspedTime) / 2);
                        }
                        else
                        {
                            curSy = jumpHeight + (-gravityFall * (elaspedTime - t_rise) * (elaspedTime - t_rise) * 0.5f);
                            // curSy = (Vyi * elaspedTime) + ((-gravityFall * elaspedTime * elaspedTime) / 2);
                        }

                        Vector3 jumpPos = specialWaypoint.nodePosition;
                        Vector2 newPosition = new Vector2((jumpPos.x - curSx), (jumpPos.y + curSy));

                        if (oldPosition != Vector2.zero)
                        {
                            Gizmos.color = Color.cyan;
                            Gizmos.DrawLine(newPosition, oldPosition);
                        }

                        oldPosition = newPosition;

                        if (k == resolution - 1)
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
                    /*
                    oldPosition = Vector2.zero;
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawCube(specialWaypoint.nodePosition, new Vector3(0.5f, 0.5f));

                    jumpNodePosition = (Vector3)specialWaypoint.contextJumpNode.position;
                    jumpEndNodePosition = (Vector3)specialWaypoint.contextJumpEndNode.position;

                    float airborneJumpHeight = GetAirborneJumpHeight(1);

                    Sy_fall = airborneJumpHeight - (jumpEndNodePosition.y - specialWaypoint.nodePosition.y);
                    x = Vx * ((2 * airborneJumpHeight / GetAirborneVyi(1)) + Mathf.Sqrt(2 * Sy_fall / gravityFall)) * ((specialWaypoint.facingRight == false) ? 1 : -1);

                    // BaseAiController.specialWaypoint specialWaypoint = specialWaypoints.FindIndex
                    for (int k = 0; k < resolution; k++)
                    {
                        float curSx = (x / resolution) * k;
                        float curSy = 0f;
                        float elaspedTime = ((specialWaypoint.facingRight) ? -curSx : curSx) / Vx;
                        if (elaspedTime < t_rise)
                        {
                            curSy = (GetAirborneVyi(1) * elaspedTime) + ((-gravityRise * elaspedTime * elaspedTime) / 2);
                        }
                        else
                        {
                            curSy = GetAirborneJumpHeight(1) + (-gravityFall * (elaspedTime - t_rise) * (elaspedTime - t_rise) * 0.5f);
                            // curSy = (Vyi * elaspedTime) + ((-gravityFall * elaspedTime * elaspedTime) / 2);
                        }

                        Vector3 jumpPos = specialWaypoint.nodePosition;
                        Vector2 newPosition = new Vector2((jumpPos.x - curSx), (jumpPos.y + curSy));

                        if (oldPosition != Vector2.zero)
                        {
                            Gizmos.color = Color.cyan;
                            Gizmos.DrawLine(newPosition, oldPosition);
                        }

                        oldPosition = newPosition;

                        if (k == resolution - 1)
                        {
                            curSx = x;
                            elaspedTime = ((specialWaypoint.facingRight) ? -curSx : curSx) / Vx;
                            curSy = GetAirborneJumpHeight(1) + (-gravityFall * (elaspedTime - t_rise) * (elaspedTime - t_rise) * 0.5f);
                            newPosition = new Vector2((jumpPos.x - curSx), (jumpPos.y + curSy));

                            Gizmos.color = Color.cyan;
                            Gizmos.DrawLine(oldPosition, newPosition);
                        }
                    }
                    */
                    break;

                case typeofWaypoint.DODGE:
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawCube(specialWaypoint.nodePosition, new Vector3(0.5f, 0.5f));
                    break;

                default:
                    // typeofWaypoint.NEUTRAL_DODGE
                    break;
            }
        }

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
}
