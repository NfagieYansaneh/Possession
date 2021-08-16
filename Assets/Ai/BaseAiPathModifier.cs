using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;
using UnityEngine.Events;
using System;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class BaseAiPathModifier : MonoModifier
{
    public List<GraphNode> originalNodes;
    public List<Vector3> originalVectorPath;

    public List<GraphNode> newNodes = new List<GraphNode>();
    public List<Vector3> newVectorPath = new List<Vector3>();

    // Jumping (Single jumps) & gizmos
    public List<GraphNode> jumpNodes = new List<GraphNode>();
    public List<GraphNode> jumpEndNodes = new List<GraphNode>();
    public List<int> jumpNodeStartAndEndIDs = new List<int>(); // going to depreciate this soon
    public List<GraphNode> jumpNodesFinal = new List<GraphNode>(); // This contains the list of calculated and processed jump node positions

    public List<GraphNode> ignoreNodes = new List<GraphNode>();
    public List<GraphNode> adjNodesFromOverhead = new List<GraphNode>();

    public List<GraphNode> debugNodes = new List<GraphNode>(); // just to visualise some data quickly

    // Queuing waypoint positions to insert
    public struct waypointInsertStruct
    {
        public readonly Vector3 position { get { return (Vector3)node.position; } }
        public GraphNode node;
        public GraphNode indexNode;

        public waypointInsertStruct(GraphNode newNode, GraphNode insertAtNode)
        {
            node = newNode;
            indexNode = insertAtNode;
        }
    }

    public List<waypointInsertStruct> waypointInserts = new List<waypointInsertStruct>();

    public BaseCharacterController baseCharacterController;
    public BaseAiController baseAiController;

    public List<BaseAiController.specialWaypoint> specialWaypoints = new List<BaseAiController.specialWaypoint>();

    // For curves
    public int resolution = 6;

    float t_rise;

    float gravityRise;
    float gravityFall;
    float Vx;

    float jumpHeight;
    float airborneJumpHeight;

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

        t_rise = (2 * jumpHeight / Vyi);
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

        // Jump node-ing
        for(int i=0; i<originalNodes.Count-2; i++)
        {
            if(findNextLowPenalty == true && originalNodes[i].Penalty == GridGraphGenerate.lowPenalty)
            {
                jumpEndNodes.Add(originalNodes[i]);
                jumpNodeStartAndEndIDs.Add(i);
                findNextLowPenalty = false;
            }

            if(originalNodes[i].Penalty == GridGraphGenerate.lowPenalty && originalNodes[i + 1].Penalty == GridGraphGenerate.highPenalty)
            {
                if (originalNodes[i + 2].Penalty == GridGraphGenerate.highPenalty)
                {
                    jumpNodes.Add(originalNodes[i]);
                    jumpNodeStartAndEndIDs.Add(i);
                    findNextLowPenalty = true;
                }
            }
        }

        for (int i = 0; i < jumpEndNodes.Count; i++)
        {
            if (CalculateDropdown(jumpNodes[i], jumpEndNodes[i]))
            {
                Debug.Log("Dropdown Pathing Chosen...");
            }
            else if (CalculateSingleJump(jumpNodes[i], jumpEndNodes[i]))
            {
                Debug.Log("Single Jump Pathing Chosen...");
            }
            else if (CalculateSingleJumpDashing(jumpNodes[i], jumpEndNodes[i]))
            {
                Debug.Log("Single Jump + Dashing Pathing Chosen...");
            }
            else if (CalculateDoubleJump(jumpNodes[i], jumpEndNodes[i]))
            {
                Debug.Log("Double Jump Pathing Chosen...");
            } else
            {
                bool overheadFacingRight = false;
                adjNodesFromOverhead = FindVaccantOverheadForOvershoot(jumpEndNodes[i], ref overheadFacingRight);
            }
            
        }

        // Node trimming
        Vector2 oldDirection = Vector2.zero;
        // GraphNode lastFromNode;
        
        
        for (int i = 0; i < Mathf.Min(jumpNodesFinal.Count, jumpNodes.Count); i++) {
            // Debug.Log(jumpEndNodes.Count);
            // Debug.Log(jumpNodes.Count);

            Debug.Log("i : " + i);
            Debug.Log("maxFinal : " + jumpNodesFinal.Count);
            Debug.Log("maxEnds : " + jumpEndNodes.Count);

            TrimInBetween(jumpNodesFinal[i], jumpEndNodes[i]);
        }

        foreach (waypointInsertStruct insert in waypointInserts)
        {
            int index = newNodes.FindIndex(d => d == insert.indexNode) + 1;

            newNodes.Insert(index, insert.node);
            newVectorPath.Insert(index, insert.position);

            // InsertWaypointAtPosition(insert.position, insert.node, true);
        }

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

        if (fromIndex + 1 == toIndex || fromIndex == toIndex) return;

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
                newNodes.RemoveAt(i - offset);
                newVectorPath.RemoveAt(i - offset);
            }

            else break;

            offset++;
        }
    }

    public void ClearAllLists()
    {
        jumpNodes.Clear();
        jumpEndNodes.Clear();
        jumpNodeStartAndEndIDs.Clear();
        jumpNodesFinal.Clear();
        ignoreNodes.Clear();
        specialWaypoints.Clear();
        waypointInserts.Clear();
        adjNodesFromOverhead.Clear();
    }

    enum AdjNodeSearchDirection { LEFT, RIGHT, BOTH }
    private List<GraphNode> FindAdjacentNodes(GraphNode node, ref bool foundAdjNodes, AdjNodeSearchDirection searchDirection)
    {
        List<GraphNode> adjNodes = new List<GraphNode>();
        if (node.Penalty == GridGraphGenerate.highPenalty)
        {
            foundAdjNodes = false;
            return null;
        }

        // Checking adj nodes to the left
        bool stopCurrentScan = false;

        GraphNode currentNodeBeingVetted;
        Vector2 temp = Helper.TurnPositionIntoPointOnGridGraph(GridGraphGenerate.gg, node);
        Vector2 scanNodePoint = temp;

        // checking for adj nodes on the left
        if (searchDirection == AdjNodeSearchDirection.LEFT || searchDirection == AdjNodeSearchDirection.BOTH)
        while (!stopCurrentScan)
        {

            if (scanNodePoint.x == 0f) break;

            for (int z = 0; z < 3; z++)
            {

                if (scanNodePoint.y != 0f)
                    currentNodeBeingVetted = GridGraphGenerate.gg.nodes[(z - 1 + (int)(scanNodePoint.y)) * GridGraphGenerate.gg.width + (int)(scanNodePoint.x - 1)];
                else if (scanNodePoint.y == 0f && z != 2)
                    currentNodeBeingVetted = GridGraphGenerate.gg.nodes[(z + (int)(scanNodePoint.y)) * GridGraphGenerate.gg.width + (int)(scanNodePoint.x - 1)];
                else
                {
                    stopCurrentScan = true;
                    break;
                }

                if (currentNodeBeingVetted.Penalty == GridGraphGenerate.lowPenalty && currentNodeBeingVetted.Walkable)
                {
                    adjNodes.Add(currentNodeBeingVetted);
                    scanNodePoint.x--;

                    if (scanNodePoint.y != 0f)
                        scanNodePoint.y += (z - 1);
                    else
                        scanNodePoint.y += z;

                    if (scanNodePoint.x == 0f) stopCurrentScan = true;

                    Vector3 pos = (Vector3)currentNodeBeingVetted.position;
                    Debug.Log(pos);
                    Helper.DrawArrow.ForDebugTimed(pos + Vector3.down * 1f, Vector3.up, Color.magenta, 3f);
                    foundAdjNodes = true;
                    break;
                }

                else if (z == 2) // On final scan and found no adj node
                {
                    stopCurrentScan = true;
                }
            }

        }

        // Checking adj nodes to the right
        stopCurrentScan = false;
        scanNodePoint = temp;

        if (searchDirection == AdjNodeSearchDirection.RIGHT || searchDirection == AdjNodeSearchDirection.BOTH)
        while (!stopCurrentScan)
        {

            if (scanNodePoint.x == GridGraphGenerate.gg.width) break;

            for (int z = 0; z < 3; z++)
            {

                if (scanNodePoint.y != 0f)
                    currentNodeBeingVetted = GridGraphGenerate.gg.nodes[(z - 1 + (int)(scanNodePoint.y)) * GridGraphGenerate.gg.width + (int)(scanNodePoint.x + 1)];
                else if (scanNodePoint.y == 0f && z != 2)
                    currentNodeBeingVetted = GridGraphGenerate.gg.nodes[(z + (int)(scanNodePoint.y)) * GridGraphGenerate.gg.width + (int)(scanNodePoint.x + 1)];
                else
                {
                    stopCurrentScan = true;
                    break;
                }

                if (currentNodeBeingVetted.Penalty == GridGraphGenerate.lowPenalty && currentNodeBeingVetted.Walkable)
                {
                    adjNodes.Add(currentNodeBeingVetted);
                    scanNodePoint.x++;

                    if (scanNodePoint.y != 0f)
                        scanNodePoint.y += (z - 1);
                    else
                        scanNodePoint.y += z;

                    if (scanNodePoint.x == GridGraphGenerate.gg.width) stopCurrentScan = true;

                    Vector3 pos = (Vector3)currentNodeBeingVetted.position;
                    Helper.DrawArrow.ForDebugTimed(pos + Vector3.down * 1f, Vector3.up, Color.magenta, 3f);
                    foundAdjNodes = true;
                    break;
                }

                else if (z == 2) // On final scan and found no adj node
                {
                    stopCurrentScan = true;
                }
            }

        }

        return adjNodes;
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
        List<GraphNode> adjNodes = FindAdjacentNodes(jumpEndNode, ref foundAdjNodes, AdjNodeSearchDirection.BOTH);
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

        if ((jumpNodePosition.x > -Sx - 0.5f + closestNodePosition.x) && waypointFacingRight ||
            (jumpNodePosition.x < Sx + 0.5f + closestNodePosition.x) && !waypointFacingRight)
        {
            GraphNode dropdownAtThisNode = jumpNode;
            if (!jumpNodesFinal.Contains(dropdownAtThisNode))
            {
                jumpNodesFinal.Add(dropdownAtThisNode);
            }

            Vector2 direction = (waypointFacingRight) ? Vector2.right : Vector2.left;

            BaseAiController.specialWaypoint newSpecialWaypoint = new BaseAiController.specialWaypoint(
                typeofWaypoint.RUN, jumpNode, () => { baseCharacterController.RunWaypointAI(direction); }, waypointFacingRight);

            if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
            {
                baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                specialWaypoints.Add(newSpecialWaypoint);
                // specialNodeCorrespFunction.Add(jumpEndNode);
            }

            Debug.Log("?");
            return true;
        }
        else
        {
            // ...
        }

        // dropdown + single jump (! Rework this !) (Change positioning because of priority)
        Debug.Log("dropdown + single jump");
        waypointFacingRight = false;

        if (jumpEndNodePosition.x > jumpNodePosition.x)
        {
            waypointFacingRight = true;
        }

        Sy = jumpEndNodePosition.y - jumpNodePosition.y; // This is bound to be a negative number

        float airborneVyi = GetAirborneVyi();

        float Sz = 3f; // magic value
        float Sb = GetAirborneJumpHeight() - Sy - Sz;

        float t_dropdown = Mathf.Sqrt(2 * Sb / gravityFall);
        float t_rise = 2 * GetAirborneJumpHeight() / airborneVyi;
        t_fall = Mathf.Sqrt(2 * Sz / gravityFall);
        float t_total = t_dropdown + t_rise;

        Sx = t_total * Vx;

        if ((jumpNodePosition.x < -Sx + closestNodePosition.x) && waypointFacingRight ||
            (jumpNodePosition.x > Sx + closestNodePosition.x) && !waypointFacingRight)
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

            GraphNode jumpAtThisNode = GridGraphGenerate.gg.GetNearest(new Vector3(jumpNodePosition.x + (Vx * t_dropdown) + 
                ((waypointFacingRight)? 1f : -1f), -Sb + jumpNodePosition.y)).node;

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
                typeofWaypoint.AIRBORNE_JUMP, jumpAtThisNode, baseCharacterController.JumpWaypointAI, waypointFacingRight, 0.6f,
                jumpNode, jumpEndNode);

            if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
            {
                baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                specialWaypoints.Add(newSpecialWaypoint);

                waypointInsertStruct newInsert = new waypointInsertStruct(newSpecialWaypoint.node, jumpNode);
                waypointInserts.Add(newInsert);

                Debug.Log("Added a special waypoint");
                // specialNodeCorrespFunction.Add(jumpEndNode);
            }

            return true;
        }
        else
        {
            // ...
        }

        return false;
        // dropdown + double jump ?
    }

    private bool CalculateDoubleJump(GraphNode jumpNode, GraphNode jumpEndNode)
    {
        Debug.Log("Double Jump Chosen");
        Vector3 jumpNodePosition = (Vector3)jumpNode.position;
        Vector3 jumpEndNodePosition = (Vector3)jumpEndNode.position;

        bool waypointFacingRight = false;
        bool isCapableOfOverhead = false;

        if (jumpEndNodePosition.x > jumpNodePosition.x)
        {
            waypointFacingRight = true;
        }

        bool foundAdjNodes = false;

        GraphNode closestNode = FindClosestNode(jumpEndNodePosition, FindAdjacentNodes(jumpEndNode, ref foundAdjNodes, AdjNodeSearchDirection.BOTH));
        Vector3 closestNodePosition = (Vector3)closestNode.position;
        List<GraphNode> adjNodesAtJump = FindAdjacentNodes(jumpNode, ref foundAdjNodes, AdjNodeSearchDirection.BOTH);
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

        foreach (GraphNode node in adjNodesAtJump)
        {
            Vector3 nodePosition = (Vector3)node.position;

            Sy = jumpEndNodePosition.y - nodePosition.y;
            Sz = 3f; // magic value

            if (jumpHeight * 2 < Sy)
            {
                // ..  DON'T PERFORM DOUBLE JUMP

                return false;
            }
            else if (jumpHeight * 2 - Sz < Sy)
            {
                Sz = Sy - ((jumpHeight * 2) - Sz);
            }


            t_fall1 = Mathf.Sqrt(2 * (2 * jumpHeight - Sy - Sz) / gravityFall);
            t_fall2 = Mathf.Sqrt(2 * Sz / gravityFall);
            t_total = t_rise1 + t_fall1 + t_rise2 + t_fall2;

            float Sx = t_total * Vx;

            if(jumpHeight*2 >= Sy && 
                (((nodePosition.x > -Sx + jumpEndNodePosition.x) && waypointFacingRight) ||
                ((nodePosition.x > Sx + jumpEndNodePosition.x) && !waypointFacingRight))){
                isCapableOfOverhead = true;
                Debug.Log("CapabaleOfOverhead : 0");
            }

            if ((nodePosition.x < -Sx + jumpEndNodePosition.x + 0.25f) && waypointFacingRight ||
                (nodePosition.x > Sx + jumpEndNodePosition.x - 0.25f) && !waypointFacingRight)
            {
                Debug.Log("Found a potential node");
                foundAnyPotentialNodes = true;
                potentialNodes.Add(node);
                continue;
            }
            else
            {
                if(jumpHeight*2 >= Sy)
                {
                    isCapableOfOverhead = true;
                    Debug.Log("CapabaleOfOverhead : 0");
                }
                // ...
            }

        }

        if (!foundAnyPotentialNodes)
        {
            if (isCapableOfOverhead)
            {
                Debug.Log("CapableOfOverhead : 1");
                CalculateOvershootWaypoints_S(jumpEndNode, jumpNode, 1.5f, 2);
            }

            return false;
        }
        // if(potentialNodes empty, cycle to adjNodesAtEndJump while using the closest node to jumpEndNode

        if (potentialNodes.Count >= 1)
        {
            // Possible to perform double jump
            GraphNode jumpAtThisNode = FindClosestNode(jumpNodePosition, potentialNodes);
            Vector3 jumpAtThisNodePosition = (Vector3)jumpAtThisNode.position;

            Sy = jumpEndNodePosition.y - jumpAtThisNodePosition.y;
            Sz = 3f; // magic value

            if (jumpHeight * 2 < Sy)
            {
                // ..  DON'T PERFORM DOUBLE JUMP
                // do I need to even check this again??
                return false;
            }
            else if (jumpHeight * 2 - Sz < Sy)
            {
                Sz = Sy - ((jumpHeight * 2) - Sz);
            }

            t_fall1 = Mathf.Sqrt(2 * (2 * jumpHeight - Sy - Sz) / gravityFall);
            t_fall2 = Mathf.Sqrt(2 * Sz / gravityFall);
            t_total = t_rise1 + t_fall1 + t_rise2 + t_fall2;

            // Adding single jump waypoint
            // Finding the position where we need to jump;
            float Sx_Dropdown = Vx * (t_rise1 + t_fall1) + jumpAtThisNodePosition.x;

            float Sy_Dropdown = (jumpHeight - ((gravityFall * (t_fall1) * (t_fall1)) / 2)) + jumpAtThisNodePosition.y;
            Sy_Dropdown = jumpHeight - ((gravityFall * t_fall1 * t_fall1) / 2);

            // rounding to nearest 0.5f
            // float Sx_DropdownRounded = (int)(Sx_Dropdown * 2) / 2f;
            // float Sy_DropdownRounded = (int)(Sy_Dropdown * 2) / 2f;

            // gg.nodes[(z - 1) * gg.width + x];
            GraphNode secondJumpAtThisNode = GridGraphGenerate.gg.GetNearest(new Vector3(jumpNodePosition.x + Sx_Dropdown +
            ((waypointFacingRight) ? 1.25f : -1.25f), Sy_Dropdown + jumpAtThisNodePosition.y)).node;

            // first jump
            if (!jumpNodesFinal.Contains(jumpAtThisNode))
            {
                jumpNodesFinal.Add(jumpAtThisNode);
                BaseAiController.specialWaypoint newSpecialWaypoint = new BaseAiController.specialWaypoint(
                typeofWaypoint.JUMP, jumpAtThisNode, baseCharacterController.JumpWaypointAI, waypointFacingRight, 0.25f, 
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
                jumpNodesFinal.Add(secondJumpAtThisNode);
                BaseAiController.specialWaypoint newSpecialWaypoint = new BaseAiController.specialWaypoint(
                typeofWaypoint.AIRBORNE_JUMP, secondJumpAtThisNode, baseCharacterController.JumpWaypointAI, waypointFacingRight, 0.6f,
                jumpNode, jumpEndNode);

                waypointInsertStruct newInsert = new waypointInsertStruct(newSpecialWaypoint.node, jumpAtThisNode);
                waypointInserts.Add(newInsert);

                if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
                {
                    baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                    specialWaypoints.Add(newSpecialWaypoint);
                    // specialNodeCorrespFunction.Add(jumpEndNode);
                }
            }

            return true;
        }

        // Do overhead double jump calculation
        if(isCapableOfOverhead)
        {
            Debug.Log("CapableOfOverhead");
        }

        return false;
    }

    private bool CalculateSingleJumpDashing(GraphNode jumpNode, GraphNode jumpEndNode)
    {
        Vector3 jumpNodePosition = (Vector3)jumpNode.position;
        Vector3 jumpEndNodePosition = (Vector3)jumpEndNode.position;
        
        bool waypointFacingRight = false;
        
        if (jumpEndNodePosition.x > jumpNodePosition.x)
        {
            waypointFacingRight = true;
        }
        
        bool foundAdjNodes = false;
        
        GraphNode closestNode = FindClosestNode(jumpNodePosition, FindAdjacentNodes(jumpEndNode, ref foundAdjNodes, AdjNodeSearchDirection.BOTH));
        Vector3 closestNodePosition = (Vector3)closestNode.position;
        
        List<GraphNode> adjNodesAtJump = FindAdjacentNodes(jumpNode, ref foundAdjNodes, AdjNodeSearchDirection.BOTH);
        adjNodesAtJump.Insert(0, jumpNode);
        
        float SxDash = baseCharacterController.dodgeDistance;
        float SxRise = t_rise * Vx;
        
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
        
            if ((jumpEndNodePosition.x - nodePosition.x) < SxRise + SxDash)
            {
                // impossible to perform
                continue;
            }
        
            Sz = 1.5f; // magic number
            Sy = jumpEndNodePosition.y - nodePosition.y;
            Sb = jumpHeight - Sz - Sy;
        
            t_fall = Mathf.Sqrt(2 * Sb / gravityFall);
            t_total = t_rise + baseCharacterController.dodgeTime + t_fall;
        
            float Sx = t_total * Vx;
        
            if ((nodePosition.x < Sx + jumpEndNodePosition.x) && waypointFacingRight ||
                (nodePosition.x > Sx + jumpEndNodePosition.x) && !waypointFacingRight)
            {
                foundAnyPotentialNode = true;
        
                // if we want to be more performant. We can just stop our search once we find one node potential node since
                // FindAdjacentNodes is directional
        
                potentialNodes.Add(node);
                continue;
            }
            else
            {
                // ...
            }
        
        }
        
        if (!foundAnyPotentialNode) return false;
        
        // At this point and onwards, it is possible to perform a single jump + dash
        
        GraphNode jumpAtThisNode = FindClosestNode(jumpNodePosition, potentialNodes);
        Vector3 jumpAtThisNodePosition = (Vector3)jumpAtThisNode.position;

        BaseAiController.specialWaypoint newJumpSpecialWaypoint = new BaseAiController.specialWaypoint(
            typeofWaypoint.JUMP, jumpAtThisNode, baseCharacterController.JumpWaypointAI, waypointFacingRight, 0.25f, jumpNode, jumpEndNode);

        bool passCheck = true;
        List<Vector3> buffer = SimulateSingleJump(newJumpSpecialWaypoint, 7, 3, ref passCheck);

        if (passCheck)
        {
            List<GraphNode> points = ConvertPositionsIntoGridgraphPoints(buffer);

            foreach (GraphNode point in points)
            {
                debugNodes.Add(point);
            }
        }
        else
        {
            return false;
        }

        Sz = 1.5f; // magic number
        Sy = jumpEndNodePosition.y - jumpAtThisNodePosition.y;
        Sb = jumpHeight - Sz - Sy;
        
        t_fall = Mathf.Sqrt(2 * Sb / gravityFall);
        t_total = t_rise + baseCharacterController.dodgeTime + t_fall;
        
        // Adding single jump waypoint
        // Finding the position where we need to jump;
        float Sx_Dropdown = Vx * (t_rise + t_fall);
        
        // damn, I need to calculate gravityFall & rise somewhere
        float Sy_Dropdown = (jumpHeight - ((gravityFall * (t_fall) * (t_fall)) / 2));
        
        // rounding to nearest 0.5f
        float Sx_DropdownRounded = (int)(Sx_Dropdown * 2) / 2f;
        float Sy_DropdownRounded = (int)(Sy_Dropdown * 2) / 2f;
        
        // gg.nodes[(z - 1) * gg.width + x];
        GraphNode dodgeAtThisNode = GridGraphGenerate.gg.GetNearest(new Vector3(jumpAtThisNodePosition.x + Sx_Dropdown, Sy_Dropdown + jumpAtThisNodePosition.y)).node;
        
        Vector2 direction = (waypointFacingRight) ? Vector2.right : Vector2.left;
        BaseAiController.specialWaypoint newDodgeSpecialWaypoint = new BaseAiController.specialWaypoint(
        typeofWaypoint.DODGE, dodgeAtThisNode, () => { baseCharacterController.DodgeWaypointAI(direction); }, waypointFacingRight, 0.6f);

        passCheck = true;
        buffer = SimulateDash(newDodgeSpecialWaypoint, 4, 3, ref passCheck);

        if (passCheck)
        {
            List<GraphNode> points = ConvertPositionsIntoGridgraphPoints(buffer);

            foreach (GraphNode point in points)
            {
                debugNodes.Add(point);
            }
        }
        else
        {
            return false;
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
            jumpNodesFinal.Add(dodgeAtThisNode);
        
            if (!baseAiController.specialWaypoints.Contains(newDodgeSpecialWaypoint))
            {
                baseAiController.specialWaypoints.Add(newDodgeSpecialWaypoint);
                specialWaypoints.Add(newDodgeSpecialWaypoint);

                waypointInsertStruct newInsert = new waypointInsertStruct(newDodgeSpecialWaypoint.node, jumpAtThisNode);
                waypointInserts.Add(newInsert);
                // specialNodeCorrespFunction.Add(jumpEndNode);
            }
        }
        
        return true;
    }

    // for a given time, y distance, etc.
    private enum directionEnum { LEFT, MIDDLE, RIGHT };
    // we need an enum on the type of action we are doing, for now, I will assume this is a double jump
    // change the name of 'target' to jumpEndNode
    /*
    private void CalculateOvershootWaypoints(GraphNode target, GraphNode jumpNode, int overshootDistanceInNodes, float timeToJump)
    {
        int direction = 0;
        List<GraphNode> vaccantNodes = FindVaccantOverheadForOvershoot(target, ref direction);
        if (vaccantNodes.Count < 1) return;

        Vector3 targetPosition = (Vector3)target.position;
        Vector3 jumpNodePosition = (Vector3)jumpNode.position;
        

        // double jump overshoot processing

        float timeTowardsOvershoot = Mathf.Abs(targetPosition.x - jumpNodePosition.x) / Vx;
        float overshootTime = (0.5f * (float)overshootDistanceInNodes) / Vx; // distance here shouldm't exist
        // just deduce how much time is left in our jump

        // we have to hamfist in single jump processing into here

        float Sx_timeToJump = 0f;

        if (timeToJump > overshootTime)
            Sx_timeToJump = (timeToJump * Vx) - (overshootTime * Vx);
        else Sx_timeToJump = timeToJump * Vx;

        float Sy_timeToJump = GetDoubleJumpHeightAtTime((targetPosition.y - jumpNodePosition.y), timeToJump);

        float Sx_towardsOvershoot = timeTowardsOvershoot * Vx;
        float Sy_towardsOvershoot = GetDoubleJumpHeightAtTime((targetPosition.y - jumpNodePosition.y), timeTowardsOvershoot);

        float Sx_overshoot = overshootTime * Vx;
        float Sy_overshoot = GetDoubleJumpHeightAtTime((targetPosition.y - jumpNodePosition.y), overshootTime);

        GraphNode calculatedJumpNode = GridGraphGenerate.gg.GetNearest(new Vector3
            (((direction == 0) ? -Sx_timeToJump : Sx_timeToJump) + jumpNodePosition.x,
            Sy_timeToJump + jumpNodePosition.y)).node;

        GraphNode towardsOvershootNode = GridGraphGenerate.gg.GetNearest(new Vector3
            (((direction == 0) ? -Sx_towardsOvershoot : Sx_towardsOvershoot) + jumpNodePosition.x,
            Sy_towardsOvershoot + jumpNodePosition.y)).node;

        GraphNode overshootNode = GridGraphGenerate.gg.GetNearest(new Vector3
            (((direction == 0) ? -Sx_towardsOvershoot-Sx_overshoot : Sx_towardsOvershoot+Sx_overshoot) + jumpNodePosition.x,
            Sy_overshoot + jumpNodePosition.y)).node;

        // jump waypoint
        BaseAiController.specialWaypoint newSpecialWaypoint = new BaseAiController.specialWaypoint(typeofWaypoint.AIRBORNE_JUMP, calculatedJumpNode,
            baseCharacterController.JumpWaypointAI, (direction == 0) ? false : true, 0.6f);

        if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
        {
            baseAiController.specialWaypoints.Add(newSpecialWaypoint);
            specialWaypoints.Add(newSpecialWaypoint);
            // specialNodeCorrespFunction.Add(jumpEndNode);
        }


        newSpecialWaypoint = new BaseAiController.specialWaypoint(typeofWaypoint.RUN, towardsOvershootNode,
            () => { baseCharacterController.RunWaypointAI((direction == 0) ? Vector2.left : Vector2.right); }, (direction == 0) ? false : true, 0.5f);

        if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
        {
            Vector3 calculatedJumpNodePos = (Vector3)calculatedJumpNode.position;
            Vector3 towardsOvershootNodePos = (Vector3)towardsOvershootNode.position;

            // properly arranging nodes to be inserted
            if (direction == 0) // left
            {
                if(calculatedJumpNodePos.x < towardsOvershootNodePos.x)
                {
                    waypointInsertStruct newInsert = new waypointInsertStruct(overshootNode, jumpNode);
                    waypointInserts.Add(newInsert);

                    newInsert = new waypointInsertStruct(calculatedJumpNode, overshootNode);
                    waypointInserts.Add(newInsert);
                } else{
                    waypointInsertStruct newInsert = new waypointInsertStruct(calculatedJumpNode, jumpNode);
                    waypointInserts.Add(newInsert);

                    newInsert = new waypointInsertStruct(overshootNode, calculatedJumpNode);
                    waypointInserts.Add(newInsert);
                }
            } 
            else if (direction == 2) // right
            {
                if (calculatedJumpNodePos.x > towardsOvershootNodePos.x)
                {
                    waypointInsertStruct newInsert = new waypointInsertStruct(overshootNode, jumpNode);
                    waypointInserts.Add(newInsert);

                    newInsert = new waypointInsertStruct(calculatedJumpNode, overshootNode);
                    waypointInserts.Add(newInsert);
                }
                else
                {
                    waypointInsertStruct newInsert = new waypointInsertStruct(calculatedJumpNode, jumpNode);
                    waypointInserts.Add(newInsert);

                    newInsert = new waypointInsertStruct(overshootNode, calculatedJumpNode);
                    waypointInserts.Add(newInsert);
                }
            }

            baseAiController.specialWaypoints.Add(newSpecialWaypoint);
            specialWaypoints.Add(newSpecialWaypoint);
            // specialNodeCorrespFunction.Add(jumpEndNode);
        }

        newSpecialWaypoint = new BaseAiController.specialWaypoint(typeofWaypoint.RUN, overshootNode,
            () => { baseCharacterController.RunWaypointAI((direction == 0) ? Vector2.right : Vector2.left); }, (direction == 0) ? false : true, 0.5f);

        if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
        {
            waypointInsertStruct newInsert = new waypointInsertStruct(newSpecialWaypoint.node, towardsOvershootNode);
            waypointInserts.Add(newInsert);

            baseAiController.specialWaypoints.Add(newSpecialWaypoint);
            specialWaypoints.Add(newSpecialWaypoint);
            // specialNodeCorrespFunction.Add(jumpEndNode);
        }


    }
    */

    private enum TypeofJump { SINGLE, DROPDOWN_SINGLE, SINGLE_DODGE, DOUBLE_JUMP };
    
    // Make sure that distanceFromJumpNode is not too great to the point that distanceFromJumpNode/Vx is more than air time, else overshoot is impossible
    // * link helper & simulating
    // * link collision to overshoot
    // * make overshoot for every type of jump
    // * trimming+
    private void CalculateOvershootWaypoints_S
        (GraphNode target, GraphNode jumpNode, float Sxa=1.5f, int distanceFromJumpNode=0, 
        TypeofJump typeofJump=TypeofJump.DOUBLE_JUMP, bool flip_s=false)
    {
        bool directionOfVaccantNodeFR = false; // FL means facing right
        List<GraphNode> vaccantNodes = FindVaccantOverheadForOvershoot(target, ref directionOfVaccantNodeFR); // pointless function
        // POINTLESS FUNCTION :L
        if (vaccantNodes.Count < 1) return;

        Vector3 targetPosition = (Vector3)target.position;
        Vector3 jumpNodePosition = (Vector3)jumpNode.position;

        GraphNode newJumpNode;
        Vector3 newJumpNodePosition;

        if (distanceFromJumpNode != 0)
        {
            bool foundAdjNodes = false;
            List<GraphNode> adjNodes = FindAdjacentNodes(jumpNode, ref foundAdjNodes,
                (directionOfVaccantNodeFR) ? AdjNodeSearchDirection.RIGHT : AdjNodeSearchDirection.LEFT);

            if (adjNodes.Count >= distanceFromJumpNode)
            {
                newJumpNode = adjNodes[distanceFromJumpNode];
                newJumpNodePosition = (Vector3)newJumpNode.position;
            } else
            {
                Debug.Log("Could not place newJumpNode @ '" + distanceFromJumpNode + "' away from jumpNode");
                Debug.Log("newJumpNode was placed @ '" + adjNodes.Count + "' away from jumpNode");

                newJumpNode = adjNodes[adjNodes.Count];
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
                float Sy = targetPosition.y - jumpNodePosition.y;

                float time_total = GetRemainingTimeAtDoubleJumpTime(Sy, 0f);
                Debug.Log("time_total= " + time_total);

                float Sx = Mathf.Abs(targetPosition.x - newJumpNodePosition.x);
                float time_sx = Sx / Vx;

                float time_sxa = Sxa / Vx;

                float time_sxb = time_total - time_sxa - time_sxa - time_sx;
                Debug.Log("time_sxb= " + time_sxb);
                float Sxb = (time_sxb / 2) * Vx;

                // Sxa_intrude
                // Sxb_intrude

                float airborneJumpTime = GetTimeOfDoubleJumpAirborneJumpTime(Sy);

                float Sya = GetDoubleJumpHeightAtTime(Sy, time_sxa);
                float Syb = GetDoubleJumpHeightAtTime(Sy, time_sxa + time_sxb);

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
                else if (airborneJumpTime < (time_sxb))
                {
                    SxAirborne = Vx * (airborneJumpTime - time_sxa * 2) * ((directionOfVaccantNodeFR) ? 1 : -1);
                    // queueAirborneJumpBeforeB = true;
                }
                else if (airborneJumpTime < (time_sxb * 2))
                {
                    SxAirborne = Vx * (airborneJumpTime - time_sxa * 2 - time_sxb) * ((directionOfVaccantNodeFR) ? 1 : -1);
                } // probably don't need to do any more if statements...

                // facing right!
                Vector2 Sxa_point = new Vector2(((directionOfVaccantNodeFR)? Sxa : -Sxa) + newJumpNodePosition.x, GetSingleJumpHeightAtTime(time_sxa) + newJumpNodePosition.y);
                Vector2 Sxb_point = new Vector2(((directionOfVaccantNodeFR)? -Sxb : Sxb) + newJumpNodePosition.x, GetSingleJumpHeightAtTime(time_sxa + time_sxb) + newJumpNodePosition.y);
                Vector2 airJumpPoint = new Vector2(newJumpNodePosition.x + ((directionOfVaccantNodeFR) ? -1 : 1) * SxAirborne, newJumpNodePosition.y + GetDoubleJumpHeightAtTime(Sy, airborneJumpTime));
                
                // For Sxa
                GraphNode collisionNode = null;
                List<Vector2> points = new List<Vector2>();
                points.Add(newJumpNodePosition);
                points.Add(Sxa_point);

                bool Sxa_pathCollided = BresenhamCollisionDetection(points, 3, ref collisionNode);
                float Sxa_collides;
                bool Sxa_willWait = false;
                float time_sxaCollision;

                if (Sxa_pathCollided)
                {
                    Vector2 collisionNodePosition = (Vector3)collisionNode.position;
                    Sxa_collides = Mathf.Abs(jumpNodePosition.x - collisionNodePosition.x);
                    if (Sxa_collides < Sxa)
                    {
                        // check if its possible to just jump backwards and restart the calculation
                        float diff = (Sxa - Sxa_collides);
                        time_sxaCollision = diff / Vx;

                        Sxa_willWait = true;

                        // create waiting node
                        Sxa = Sxa_collides;
                        time_sxa = Sxa / Vx;
                        Sxa_point = new Vector2(((directionOfVaccantNodeFR) ? Sxa : -Sxa) + newJumpNodePosition.x, GetSingleJumpHeightAtTime(time_sxa) + newJumpNodePosition.y);
                        // set a waiting node
                    }
                }

                // Sxa -> Sxb
                points.Clear();
                points.Add(Sxa_point);
                points.Add(Sxb_point);

                bool Sxb_pathCollided = BresenhamCollisionDetection(points, 3, ref collisionNode);
                float Sxb_collides;
                bool Sxb_willWait = false;
                float time_sxbCollision;

                if (Sxb_pathCollided)
                {
                    Vector2 collisionNodePosition = (Vector3)collisionNode.position;
                    Sxb_collides = Mathf.Abs(jumpNodePosition.x - collisionNodePosition.x);
                    if (Sxb_collides < Sxb)
                    {
                        // check if its possible to just jump backwards and restart the calculation
                        float diff = (Sxb - Sxb_collides);
                        time_sxbCollision = diff / Vx;

                        Sxa_willWait = true;

                        Sxb = Sxb_collides;
                        time_sxb = Sxb_collides / Vx;
                        Sxa_point = new Vector2(((directionOfVaccantNodeFR) ? Sxa : -Sxa) + newJumpNodePosition.x, GetSingleJumpHeightAtTime(time_sxa+time_sxa+time_sxb) + newJumpNodePosition.y);

                        GraphNode calculatedNode_B_WAIT = GridGraphGenerate.gg.GetNearest(Sxa_point).node;
                        BaseAiController.specialWaypoint waitWaypoint = new BaseAiController.specialWaypoint(typeofWaypoint.WAIT, calculatedNode_B_WAIT,
                           () => { baseCharacterController.HoldMovementAi(Vector2.zero, time_sxbCollision); }, false, 0.5f, null, null);

                        if (!baseAiController.specialWaypoints.Contains(waitWaypoint))
                        {
                            baseAiController.specialWaypoints.Add(waitWaypoint);
                            debugNodes.Add(waitWaypoint.node);
                            specialWaypoints.Add(waitWaypoint);
                        }
                        // set a waiting node
                    }
                }

                /*
                // Sxb -> End
                points.Clear();
                points.Add(Sxb_point);
                points.Add(targetPosition);

                bool Final_pathCollided = BresenhamCollisionDetection(points, 3, ref collisionNode);
                // carry out Sy height reduction if possible, would have to send this to the front of the line
                */

                // now we can locate where these nodes are and wait 'x' amount of time...
                // but I need to work on using Bresenham's Algorithm to calculate collision

                Debug.LogError("Sya : " + Sya + " Syb : " + Syb);

                GraphNode calculatedNode_A = GridGraphGenerate.gg.GetNearest(new Vector3(
                    newJumpNodePosition.x + ((directionOfVaccantNodeFR) ? -1 : 1) * Sxa, newJumpNodePosition.y + Sya)).node;

                GraphNode calculatedNode_B = GridGraphGenerate.gg.GetNearest(new Vector3(
                    newJumpNodePosition.x + ((directionOfVaccantNodeFR) ? 1 : -1) * Sxb, newJumpNodePosition.y + Syb)).node;

                Debug.Log("aodwaid: " +GetDoubleJumpHeightAtTime(Sy, airborneJumpTime));
                GraphNode calculatedNode_AirborneJump = GridGraphGenerate.gg.GetNearest(new Vector3(
                    newJumpNodePosition.x + ((directionOfVaccantNodeFR) ? -1 : 1) * SxAirborne, newJumpNodePosition.y + GetDoubleJumpHeightAtTime(Sy, airborneJumpTime))).node;

                // first jump waypoint
                BaseAiController.specialWaypoint newSpecialWaypoint = new BaseAiController.specialWaypoint(typeofWaypoint.JUMP, newJumpNode,
                    baseCharacterController.JumpWaypointAI, (directionOfVaccantNodeFR) ? true : false, 0.25f, newJumpNode, target); // ? im calculating wheather waypointFacingRight wrongly

                if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
                {
                    baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                    debugNodes.Add(newSpecialWaypoint.node);
                    specialWaypoints.Add(newSpecialWaypoint);

                    // specialNodeCorrespFunction.Add(jumpEndNode);
                }

                // airborne jump waypoint
                newSpecialWaypoint = new BaseAiController.specialWaypoint(typeofWaypoint.AIRBORNE_JUMP, calculatedNode_AirborneJump,
                    baseCharacterController.JumpWaypointAI, (directionOfVaccantNodeFR) ? true : false, 0.6f); // ? im calculating wheather waypointFacingRight wrongly

                if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
                {
                    baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                    debugNodes.Add(newSpecialWaypoint.node);
                    specialWaypoints.Add(newSpecialWaypoint);
                    // specialNodeCorrespFunction.Add(jumpEndNode);
                }


                // Node A
                newSpecialWaypoint = new BaseAiController.specialWaypoint(typeofWaypoint.RUN, calculatedNode_A,
                    () => { baseCharacterController.RunWaypointAI((directionOfVaccantNodeFR) ? Vector2.right : Vector2.left); }, (directionOfVaccantNodeFR) ? true : false, 0.6f);

                if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
                {
                    Vector3 calculatedJumpNodePos = (Vector3)calculatedNode_AirborneJump.position;
                    Vector3 towardsOvershootNodePos = (Vector3)calculatedNode_A.position;
                    debugNodes.Add(newSpecialWaypoint.node);

                    if (queueAirborneJumpBeforeA)
                    {
                        waypointInsertStruct newInsert = new waypointInsertStruct(calculatedNode_AirborneJump, newJumpNode);
                        waypointInserts.Add(newInsert);

                        newInsert = new waypointInsertStruct(calculatedNode_A, calculatedNode_AirborneJump);
                        waypointInserts.Add(newInsert);
                    }
                    else
                    {
                        waypointInsertStruct newInsert = new waypointInsertStruct(calculatedNode_A, newJumpNode);
                        waypointInserts.Add(newInsert);

                        newInsert = new waypointInsertStruct(calculatedNode_AirborneJump, calculatedNode_A);
                        waypointInserts.Add(newInsert);
                    }

                    baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                    specialWaypoints.Add(newSpecialWaypoint);
                    // specialNodeCorrespFunction.Add(jumpEndNode);
                }

                // Node B

                newSpecialWaypoint = new BaseAiController.specialWaypoint(typeofWaypoint.RUN, calculatedNode_B,
                    () => { baseCharacterController.RunWaypointAI((directionOfVaccantNodeFR) ? Vector2.left : Vector2.right); }, (directionOfVaccantNodeFR) ? false : true, 0.5f);

                if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
                {
                    debugNodes.Add(newSpecialWaypoint.node);
                    if (queueAirborneJumpBeforeA)
                    {
                        waypointInsertStruct newInsert = new waypointInsertStruct(newSpecialWaypoint.node, calculatedNode_A);
                        waypointInserts.Add(newInsert);
                    }
                    else
                    {
                        waypointInsertStruct newInsert = new waypointInsertStruct(newSpecialWaypoint.node, calculatedNode_AirborneJump);
                        waypointInserts.Add(newInsert);
                    }

                    baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                    specialWaypoints.Add(newSpecialWaypoint);
                    // specialNodeCorrespFunction.Add(jumpEndNode);
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

            retDirection = true;
        }

        if (!notVaccant) vaccantNodes.Add(GridGraphGenerate.gg.nodes[((int)scanNodePointPosition.y) * GridGraphGenerate.gg.width + ((int)scanNodePointPosition.x + 1)]);

        // Checking nodes to the left
        notVaccant = false;
        scanNodePointPosition = Helper.TurnPositionIntoPointOnGridGraph(GridGraphGenerate.gg, scanNodePoint);

        for (int z = 0; z < 3; z++)
        {
            currentNodeBeingVetted = GridGraphGenerate.gg.nodes[(z - 1 + (int)scanNodePointPosition.y) * GridGraphGenerate.gg.width + ((int)scanNodePointPosition.x - 1)];
            if (currentNodeBeingVetted.Penalty == GridGraphGenerate.lowPenalty) notVaccant = true;

            retDirection = false;
        }

        if (!notVaccant) vaccantNodes.Add(GridGraphGenerate.gg.nodes[((int)scanNodePointPosition.y) * GridGraphGenerate.gg.width + ((int)scanNodePointPosition.x - 1)]);

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
            Vector3 pointPosition = Helper.TurnPositionIntoPointOnGridGraph(GridGraphGenerate.gg, point);

            for (int z = 0; z < heightInNodes; z++)
            {
                currentNodeBeingVetted = GridGraphGenerate.gg.nodes[(z + 1 + (int)pointPosition.y) * GridGraphGenerate.gg.width + ((int)pointPosition.x)];
                if (!point.Walkable) return true;
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

    private bool CalculateSingleJump(GraphNode jumpNode, GraphNode jumpEndNode) 
    {

        // float Vx = 8.5f;
        // float jumpHeight = baseCharacterController.jumpHeight;

        Vector3 jumpEndNodePosition = (Vector3)jumpEndNode.position;
        Vector3 jumpNodePosition = (Vector3)jumpNode.position;

        bool waypointFacingRight = false;

        if(jumpEndNodePosition.x > jumpNodePosition.x)
        {
            waypointFacingRight = true;
        }

        GraphNode jumpAtThisNode;

        List<GraphNode> adjNodes = new List<GraphNode>();
        
        bool foundAdjNodes = false;

        if (waypointFacingRight)
            adjNodes = FindAdjacentNodes(jumpNode, ref foundAdjNodes, AdjNodeSearchDirection.LEFT);
        else adjNodes = FindAdjacentNodes(jumpNode, ref foundAdjNodes, AdjNodeSearchDirection.RIGHT);

        adjNodes.Insert(0, jumpNode); // just for convenicene when using foreach loop

        foreach(GraphNode node in adjNodes)
        {

            float Sy_fall = jumpHeight - (jumpEndNodePosition.y - jumpNodePosition.y);
            float t_fall = Mathf.Sqrt(2 * Sy_fall / gravityFall);
            float Sx = Vx * ((2 * jumpHeight / Vyi) + Mathf.Sqrt(2 * Sy_fall / gravityFall)) * ((waypointFacingRight == false) ? 1 : -1);

            Vector3 nodePosition = (Vector3)node.position;

            List<GraphNode> tempNodes = new List<GraphNode>();
            if ((Sx + jumpEndNodePosition.x > nodePosition.x + 0.25f && !waypointFacingRight) ||
                (Sx + jumpEndNodePosition.x < nodePosition.x - 0.25f && waypointFacingRight))
            {

                // newNodes.Remove(node);
                tempNodes.Add(node);
            }
            else 
            if ((Sx + jumpEndNodePosition.x > nodePosition.x - 0.25f && !waypointFacingRight) ||
                    (Sx + jumpEndNodePosition.x < nodePosition.x + 0.25f && waypointFacingRight))
            {
                jumpAtThisNode = node;

                BaseAiController.specialWaypoint newSpecialWaypoint = new BaseAiController.specialWaypoint(
                    typeofWaypoint.JUMP, jumpAtThisNode, baseCharacterController.JumpWaypointAI, waypointFacingRight, 0.25f,
                    jumpNode, jumpEndNode);

                bool passCheck = true;
                List<Vector3> buffer = SimulateSingleJump(newSpecialWaypoint, 7, 3, ref passCheck);

                if (passCheck)
                {
                    List<GraphNode> points = ConvertPositionsIntoGridgraphPoints(buffer);

                    foreach (GraphNode point in points)
                    {
                        debugNodes.Add(point);
                    }
                }
                else
                {
                    return false;
                }

                foreach (GraphNode tempNode in tempNodes)
                {
                    if (!ignoreNodes.Contains(tempNode))
                    {
                        ignoreNodes.Add(tempNode);
                    }
                }

                if (!jumpNodesFinal.Contains(jumpAtThisNode))
                {
                    jumpNodesFinal.Add(jumpAtThisNode);
                }

                if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
                {
                    baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                    specialWaypoints.Add(newSpecialWaypoint);

                    // specialNodeCorrespFunction.Add(jumpEndNode);
                }

                return true;
            }
        }

        return false;
    }

    // Help from http://members.chello.at/~easyfilter/bresenham.html

    private bool BresenhamCollisionDetection(List<Vector2> points, int collisionHeightInNodes, ref GraphNode nodeOfCollision)
    {
        List<GraphNode> nodes = Helper.BresenhamLineLoopThrough(GridGraphGenerate.gg, points);
        foreach (GraphNode node in nodes)
        {
            if (CheckForCollisions(node, collisionHeightInNodes))
            {
                nodeOfCollision = node;
                return false;
            }
        }

        return true;
    }

    /*private List<Vector2> SimulateSingleJump(Vector2 position, Vector2 endPosition, bool facingRight, int simulationResoultion,
        int collisionHeightInNodes, ref bool passCheck, ref GraphNode nodeOfCollision)
    {
        List<Vector2> points = new List<Vector2>();

        Vector2 oldPosition = Vector2.zero;

        Vector2 jumpNodePosition = position;
        Vector2 jumpEndNodePosition = (Vector3)endPosition;
        points.Add(jumpNodePosition);

        Debug.DrawLine(jumpNodePosition, Vector2.zero, Color.magenta, 5f);
        // Simulating jump points
        float Sy_fall = jumpHeight - (jumpEndNodePosition.y - jumpNodePosition.y);
        float x = Vx * ((2 * jumpHeight / Vyi) + Mathf.Sqrt(2 * Sy_fall / gravityFall)) * ((facingRight == false) ? 1 : -1);

        passCheck = true;
        // BaseAiController.specialWaypoint specialWaypoint = specialWaypoints.FindIndex
        for (int k = 0; k < simulationResoultion; k++)
        {
            float curSx = (x / simulationResoultion) * k;
            float curSy = 0f;
            float elaspedTime = ((facingRight) ? -curSx : curSx) / Vx;
            if (elaspedTime < t_rise)
            {
                curSy = (Vyi * elaspedTime) + ((-gravityRise * elaspedTime * elaspedTime) / 2);
            }
            else
            {
                curSy = jumpHeight + (-gravityFall * (elaspedTime - t_rise) * (elaspedTime - t_rise) * 0.5f);
                // curSy = (Vyi * elaspedTime) + ((-gravityFall * elaspedTime * elaspedTime) / 2);
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
                curSy = jumpHeight + (-gravityFall * (elaspedTime - t_rise) * (elaspedTime - t_rise) * 0.5f);
                newPosition = new Vector2((jumpPos.x - curSx), (jumpPos.y + curSy));

                if (CheckForCollisions(newPosition, collisionHeightInNodes))
                {
                    passCheck = false;
                    break;
                }

                points.Add(newPosition);
            }
        }

        return (passCheck)? points : null;
    }*/

    // Simulating jumps and movement

    private List<Vector2> SimulateSingleJump(Vector2 position, Vector2 endPosition, bool facingRight, int simulationResoultion, float Vyi)
    {
        List<Vector2> points = new List<Vector2>();

        Vector2 oldPosition = Vector2.zero;

        Vector2 jumpNodePosition = position;
        Vector2 jumpEndNodePosition = (Vector3)endPosition;
        points.Add(jumpNodePosition);

        Debug.DrawLine(jumpNodePosition, Vector2.zero, Color.magenta, 5f);
        // Simulating jump points
        float Sy_fall = jumpHeight - (jumpEndNodePosition.y - jumpNodePosition.y);
        float x = Vx * ((2 * jumpHeight / Vyi) + Mathf.Sqrt(2 * Sy_fall / gravityFall)) * ((facingRight == false) ? 1 : -1);

        passCheck = true;
        // BaseAiController.specialWaypoint specialWaypoint = specialWaypoints.FindIndex
        for (int k = 0; k < simulationResoultion; k++)
        {
            float curSx = (x / simulationResoultion) * k;
            float curSy = 0f;
            float elaspedTime = ((facingRight) ? -curSx : curSx) / Vx;
            if (elaspedTime < t_rise)
            {
                curSy = (Vyi * elaspedTime) + ((-gravityRise * elaspedTime * elaspedTime) / 2);
            }
            else
            {
                curSy = jumpHeight + (-gravityFall * (elaspedTime - t_rise) * (elaspedTime - t_rise) * 0.5f);
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
                curSy = jumpHeight + (-gravityFall * (elaspedTime - t_rise) * (elaspedTime - t_rise) * 0.5f);
                newPosition = new Vector2((jumpPos.x - curSx), (jumpPos.y + curSy));

                if (CheckForCollisions(newPosition, collisionHeightInNodes))
                {
                    passCheck = false;
                    break;
                }

                points.Add(newPosition);
            }
        }

        return (passCheck) ? points : null;
    }

    private List<Vector2> SimulateDash(Vector2 position, bool facingRight, int simulationResoultion)
    {
        List<Vector3> points = new List<Vector3>();

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

                if (CheckForCollisions(newPosition, heightInNodes))
                {
                    passCheck = false;
                    break;
                }

                points.Add(newPosition);
            }
        }

        return (passCheck) ? points : null;
    }

    // Simulate Dropdown end position can just use BresenhamCollisionDetection and setting two points

    // Height
    private float GetSingleJumpHeightAtTime (float time)
    {
        float deltaHeight = jumpHeight + ((Vyi * time) + ((-gravityFall * time * time) / 2));
        return deltaHeight;
    }
    private float GetDoubleJumpHeightAtTime(float Sy, float time)
    {
        float airborneVyi = GetAirborneVyi();

        float t_rise1 = t_rise;
        float t_rise2 = 2 * GetAirborneJumpHeight() / airborneVyi;

        float t_fall1;
        float t_fall2;
        float t_total;

        float Sz = 3f; // magic value
        if (jumpHeight * 2 < Sy) return 0f;
        else if (jumpHeight * 2 - Sz < Sy)
        {
            Sz = Sy - ((jumpHeight * 2) - Sz);
        }

        float Sb = 2 * jumpHeight - Sy - Sz;

        t_fall1 = Mathf.Sqrt(2 * (-Sb) / gravityFall);
        t_fall2 = Mathf.Sqrt(2 * Sz / gravityFall);
        t_total = t_rise1 + t_fall1 + t_rise2 + t_fall2;

        float deltaHeight;

        if (time <= t_rise1 + t_fall1)
        {
            deltaHeight = ((Vyi * time) + ((-gravityFall * time * time) / 2));
            Debug.LogWarning(deltaHeight);
            return deltaHeight;
        }
        else
        {
            float new_time = time - (t_rise1 + t_fall1);
            Debug.Log("airborne vyi: " + GetAirborneVyi(0));
            deltaHeight = (jumpHeight + Sb) + ((GetAirborneVyi(0) * new_time) + ((-gravityFall * new_time * new_time) / 2));

            /*
            Debug.Log(jumpHeight);
            Debug.Log(Sb);
            Debug.Log(new_time);
            Debug.Log(gravityFall);
            */

            Debug.LogWarning(deltaHeight);
            return deltaHeight;
        }
    }

    // Time
    private float GetRemainingTimeAtSingleJumpTime(float height) { return 0f; }
    private float GetRemainingTimeAtDoubleJumpTime(float Sy, float time)
    {
        float airborneVyi = GetAirborneVyi();

        float t_rise1 = t_rise;
        float t_rise2 = 2 * GetAirborneJumpHeight() / airborneVyi;

        float t_fall1;
        float t_fall2;
        float t_total;

        float Sz = 3f; // magic value
        if (jumpHeight * 2 < Sy) return 0f;
        else if (jumpHeight * 2 - Sz < Sy)
        {
            Sz = Sy - ((jumpHeight * 2) - Sz);
        }

        float Sb = 2 * jumpHeight - Sy - Sz;

        t_fall1 = Mathf.Sqrt(2 * -Sb / gravityFall);
        t_fall2 = Mathf.Sqrt(2 * Sz / gravityFall);
        t_total = t_rise1 + t_fall1 + t_rise2 + t_fall2;

        Debug.Log("t_total in f(x)= " + t_total);
        return t_total - time;
    }
    private float GetTimeOfDoubleJumpAirborneJumpTime(float Sy)
    {

        float t_rise1 = t_rise;

        float t_fall1;

        float Sz = 3f; // magic value
        if (jumpHeight * 2 < Sy) return 0f;
        else if (jumpHeight * 2 - Sz < Sy)
        {
            Sz = Sy - ((jumpHeight * 2) - Sz);
        }
        float Sb = 2 * jumpHeight - Sy - Sz;

        t_fall1 = Mathf.Sqrt(2 * -Sb / gravityFall);
        Debug.Log("t_fall1 : " + t_fall1);
        return t_rise1 + t_fall1;
    }

    // Velocity
    private float GetVyOfDoubleJumpTime(float Sy, float time, float Vyi)
    {
        float timeOfDoubleJump = GetTimeOfDoubleJumpAirborneJumpTime(Sy);
        float Vy = 0f;

        if (time < timeOfDoubleJump && time < t_rise)
        {
            Vy = Mathf.Sqrt(Vyi * Vyi + 2 * gravityRise * GetDoubleJumpHeightAtTime(Sy, time));
        } if (time < timeOfDoubleJump)
        {
            Vy = Mathf.Sqrt(Vyi * Vyi + 2 * gravityFall * GetDoubleJumpHeightAtTime(Sy, time));
        } else if(time > timeOfDoubleJump && time < t_rise + timeOfDoubleJump)
        {
            Vy = Mathf.Sqrt(Vyi * Vyi + 2 * gravityRise * GetDoubleJumpHeightAtTime(Sy, time));
        } else
        {
            Vy = Mathf.Sqrt(Vyi * Vyi + 2 * gravityFall * GetDoubleJumpHeightAtTime(Sy, time));
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
