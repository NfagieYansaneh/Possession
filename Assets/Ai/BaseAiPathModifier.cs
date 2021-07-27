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
            Mathf.Pow(baseCharacterController.successiveJumpHeightReduction, baseCharacterController.airJumpsPerformed));
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
                adjNodesFromOverhead = FindVaccantOverheadForOvershoot(jumpEndNodes[i]);
                Debug.Log("Overhead");
            }
            
        }

        // Node trimming
        Vector2 oldDirection = Vector2.zero;
        // GraphNode lastFromNode;
        
        
        for (int i = 0; i < jumpNodesFinal.Count; i++) {
            // Debug.Log(jumpEndNodes.Count);
            // Debug.Log(jumpNodes.Count);

            TrimInBetween(jumpNodesFinal[i], jumpEndNodes[i]);
        }

        foreach (waypointInsertStruct insert in waypointInserts)
        {
            int index = newNodes.FindIndex(d => d == insert.indexNode) + 1;

            newNodes.Insert(index, insert.node);
            newVectorPath.Insert(index, insert.position);

            // InsertWaypointAtPosition(insert.position, insert.node, true);
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

        bool foundAdjNodes = false;
        List<GraphNode> adjNodes = FindAdjacentNodes(jumpEndNode, ref foundAdjNodes, AdjNodeSearchDirection.BOTH);
        if (foundAdjNodes == false) return false;

        // dropdown

        bool waypointFacingRight = false;

        if (jumpEndNodePosition.x > jumpNodePosition.x)
        {
            waypointFacingRight = true;
        }

        GraphNode closestNode = FindClosestNode(jumpEndNodePosition, adjNodes);
        Vector3 closestNodePosition = (Vector3)closestNode.position;

        float Sy = jumpEndNodePosition.y - jumpNodePosition.y; // This is bound to be a negative number
        float t_fall = Mathf.Sqrt(2 * Sy / -gravityFall);

        float Sx = Vx * t_fall;

        if ((jumpNodePosition.x > -Sx + closestNodePosition.x) && waypointFacingRight ||
            (jumpNodePosition.x < Sx + closestNodePosition.x) && !waypointFacingRight)
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
        Vector3 jumpNodePosition = (Vector3)jumpNode.position;
        Vector3 jumpEndNodePosition = (Vector3)jumpEndNode.position;

        bool waypointFacingRight = false;

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

            if ((nodePosition.x < Sx + jumpEndNodePosition.x) && waypointFacingRight ||
                (nodePosition.x > Sx + jumpEndNodePosition.x) && !waypointFacingRight)
            {
                foundAnyPotentialNodes = true;
                potentialNodes.Add(node);
                continue;
            }
            else
            {
                // ...
            }

        }

        if (!foundAnyPotentialNodes) return false;
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
        
        if (!jumpNodesFinal.Contains(jumpAtThisNode))
        {
            jumpNodesFinal.Add(jumpAtThisNode);

            BaseAiController.specialWaypoint newSpecialWaypoint = new BaseAiController.specialWaypoint(
            typeofWaypoint.JUMP, jumpAtThisNode, baseCharacterController.JumpWaypointAI, waypointFacingRight, 0.25f, jumpNode, jumpEndNode);
        
            if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
            {
                baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                specialWaypoints.Add(newSpecialWaypoint);
                // specialNodeCorrespFunction.Add(jumpEndNode);
            }
        }
        
        if (!jumpNodesFinal.Contains(dodgeAtThisNode))
        {
            jumpNodesFinal.Add(dodgeAtThisNode);

            Vector2 direction = (waypointFacingRight) ? Vector2.right : Vector2.left;

            BaseAiController.specialWaypoint newSpecialWaypoint = new BaseAiController.specialWaypoint(
            typeofWaypoint.DODGE, dodgeAtThisNode, () => { baseCharacterController.DodgeWaypointAI(direction); }, waypointFacingRight, 0.6f);
        
            if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
            {
                baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                specialWaypoints.Add(newSpecialWaypoint);

                waypointInsertStruct newInsert = new waypointInsertStruct(newSpecialWaypoint.node, jumpAtThisNode);
                waypointInserts.Add(newInsert);
                // specialNodeCorrespFunction.Add(jumpEndNode);
            }
        }
        
        return true;
    }

    private void CalculateOvershootWaypoints(GraphNode target, float time)
    {
        List<GraphNode> vaccantNodes = FindVaccantOverheadForOvershoot(target);
        GraphNode targetVaccantNode;

        if (vaccantNodes.Count < 1) return;

        else if (vaccantNodes.Count == 1) targetVaccantNode = vaccantNodes[1];

        else targetVaccantNode = vaccantNodes[2]; // priotises right

        Vector3 targetPosition = (Vector3)target.position;
        Vector3 targetVaccantNodePosition = (Vector3)targetVaccantNode.position;

        bool facingRight = false;

        if (targetVaccantNodePosition.x > targetPosition.x) facingRight = true;

        float Sxa = time * Vx / 2;
        float Sxb = -Sxa;

        if(facingRight)
        {
            Sxa *= -1;
            Sxb *= -1;
        }

        /*
        BaseAiController.specialWaypoint specialWaypoint_1D_X = new BaseAiController.specialWaypoint(
            typeofWaypoint.RUN, X, null);

        BaseAiController.specialWaypoint specialWaypoint_1D_X = new BaseAiController.specialWaypoint(
            typeofWaypoint.RUN, X, null);
        */

    }

    // Apart of interception
    private void CalculateWaitingWaypoint()
    {

    }

    // GridGraph[] futureGraphs
    private void CalculateForesight(GraphNode dynamicNode)
    {

    }

    // Add direction control
    private List<GraphNode> FindVaccantOverheadForOvershoot(GraphNode scanNodePoint)
    {
        GraphNode currentNodeBeingVetted;
        List<GraphNode> vaccantNodes = new List<GraphNode>(); // Ranges from 0 to 2

        // Checking nodes to the left
        bool notVaccant = false;
        Vector3 scanNodePointPosition = Helper.TurnPositionIntoPointOnGridGraph(GridGraphGenerate.gg, scanNodePoint);

        for (int z = 0; z < 3; z++) {
            currentNodeBeingVetted = GridGraphGenerate.gg.nodes[(z - 1 + (int)scanNodePointPosition.y) * GridGraphGenerate.gg.width + ((int)scanNodePointPosition.x - 1)];
            if (currentNodeBeingVetted.Penalty == GridGraphGenerate.lowPenalty) notVaccant = true;
        }

        if (!notVaccant) vaccantNodes.Add(GridGraphGenerate.gg.nodes[((int)scanNodePointPosition.y) * GridGraphGenerate.gg.width + ((int)scanNodePointPosition.x - 1)]);

        // Checking nodes to the right
        notVaccant = false;
        for (int z = 0; z < 3; z++)
        {
            currentNodeBeingVetted = GridGraphGenerate.gg.nodes[(z - 1 + (int)scanNodePointPosition.y) * GridGraphGenerate.gg.width + ((int)scanNodePointPosition.x + 1)];
            if (currentNodeBeingVetted.Penalty == GridGraphGenerate.lowPenalty) notVaccant = true;
        }

        if (!notVaccant) vaccantNodes.Add(GridGraphGenerate.gg.nodes[((int)scanNodePointPosition.y) * GridGraphGenerate.gg.width + ((int)scanNodePointPosition.x + 1)]);

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

            for (int z = 0; z < heightInNodes; z++)
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

            if ((Sx + jumpEndNodePosition.x > nodePosition.x + 0.25f && !waypointFacingRight) ||
                (Sx + jumpEndNodePosition.x < nodePosition.x - 0.25f && waypointFacingRight))
            {
                // newNodes.Remove(node);
                if (!ignoreNodes.Contains(node))
                {
                    ignoreNodes.Add(node);
                }
            }
            else 
            if ((Sx + jumpEndNodePosition.x > nodePosition.x - 0.25f && !waypointFacingRight) ||
                    (Sx + jumpEndNodePosition.x < nodePosition.x + 0.25f && waypointFacingRight))
            { 
                jumpAtThisNode = node;
                if (!jumpNodesFinal.Contains(jumpAtThisNode))
                {
                    jumpNodesFinal.Add(jumpAtThisNode);
                }

                BaseAiController.specialWaypoint newSpecialWaypoint = new BaseAiController.specialWaypoint(
                    typeofWaypoint.JUMP, jumpAtThisNode, baseCharacterController.JumpWaypointAI, waypointFacingRight, 0.25f,
                    jumpNode, jumpEndNode);

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


    /// <summary>
    /// Simulates each point along a single jump with a given resolution and carries
    /// out a boolean check and events at it simulated point.
    /// </summary>
    /// 
    /// <returns>
    /// If our simulated single jump passes the test, we return all points along the
    /// simulation.
    /// </returns>
    private List<Vector3> SimulateSingleJump(Func<bool> Check, UnityAction action)
    {
        Check();
        action.Invoke();
        List<Vector3> points = new List<Vector3>();
        return points;
    }

    // later, @v2.0 release, I will go around commenting my functions & variables like this in order to make
    // it more scalable (https://docs.microsoft.com/en-us/dotnet/csharp/language-reference/xmldoc/recommended-tags)

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.black;
        foreach(GraphNode node in ignoreNodes)
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
