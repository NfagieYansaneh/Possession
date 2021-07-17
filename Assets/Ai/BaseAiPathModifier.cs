using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class BaseAiPathModifier : MonoModifier
{
    public List<GraphNode> originalNodes;
    public List<GraphNode> newNodes = new List<GraphNode>();
    public List<Vector3> newVectorPath = new List<Vector3>();

    // Jumping (Single jumps) & gizmos
    public List<GraphNode> jumpNodes = new List<GraphNode>();
    public List<GraphNode> jumpEndNodes = new List<GraphNode>();
    public List<int> jumpNodeStartAndEndIDs = new List<int>(); // going to depreciate this soon
    public List<GraphNode> jumpNodesFinal = new List<GraphNode>(); // This contains the list of calculated and processed jump node positions

    public List<GraphNode> ignoreNodes = new List<GraphNode>();
    public Vector2 gizmoJumping_SxSy = Vector2.zero;


    public BaseCharacterController baseCharacterController;
    public BaseAiController baseAiController;

    public List<BaseAiController.specialWaypoint> specialWaypoints = new List<BaseAiController.specialWaypoint>();

    // For curves
    public int resolution = 6;

    public void Start()
    {
        baseCharacterController = GetComponent<BaseCharacterController>();
        baseAiController = GetComponent<BaseAiController>();
    }

    public override int Order { get { return 60; } }
    public override void Apply(Path path)
    {
        if (path.error || path.vectorPath == null || 
            path.vectorPath.Count <= 3) { return; }

        ClearAllLists();
        originalNodes = path.path;

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

        CalculateSingleJump();

        // Node trimming
        Vector2 oldDirection = Vector2.zero;
        // GraphNode lastFromNode;
        

        for (int i = 0; i < jumpEndNodes.Count; i++) {
            TrimInBetween(jumpNodesFinal[i], jumpEndNodes[i]);
        }
        
        path.path = newNodes;
        path.vectorPath = newVectorPath;
        // throw new System.NotImplementedException();
    }

    float t_rise;
    float t_fall;
    float gravityRise;
    float gravityFall;
    float Vx;
    float jumpHeight;
    float Vyi;

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

        int offset = 0;
        for(int i=fromIndex+1; i<toIndex; i++)
        {
            newNodes.RemoveAt(i - offset);
            newVectorPath.RemoveAt(i - offset);
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
    }

    public Vector2 CalculateSxSy(Vector3 jumpEndNodePosition, GraphNode node, bool waypointFacingRight)
    {
        Vx = 8.5f;
        jumpHeight = baseCharacterController.jumpHeight;

        Vector3 jumpNodePosition = (Vector3)node.position;

        float Sy_fall = jumpHeight - (jumpEndNodePosition.y - jumpNodePosition.y);
        gravityRise = baseCharacterController.gravity * baseCharacterController.gravityMultiplier;
        gravityFall = baseCharacterController.gravity * baseCharacterController.gravityMultiplier * baseCharacterController.fallingGravityMultiplier;
        Vyi = Mathf.Sqrt(2 * gravityRise * jumpHeight);

        t_rise = (2 * jumpHeight / Vyi);
        t_fall = Mathf.Sqrt(2 * Sy_fall / gravityFall);

        float Sx = Vx * ((2 * jumpHeight / Vyi) + Mathf.Sqrt(2 * Sy_fall / gravityFall)) * ((waypointFacingRight == false) ? 1 : -1) ;

        return new Vector2(Sx, Sy_fall);
    }

    private List<GraphNode> FindAdjacentNodes(GraphNode node)
    {
        List<GraphNode> adjNodes = new List<GraphNode>();
        if (node.Penalty == GridGraphGenerate.highPenalty) return null;
        /*         
         *         for (int x = 0; x < gg.width - 1; x++)
        {
            for (int z = 1; z < gg.depth - 1; z++)
            {
                GraphNode currentNode = gg.nodes[z * gg.width + x];
                if (currentNode != null && currentNode.Walkable)
                {
                    GraphNode nodeBelow = gg.nodes[(z - 1) * gg.width + x];
                    if (!nodeBelow.Walkable)
                    {
                        lowPenaltyNodes.Add(currentNode);
                        currentNode.Penalty = lowPenalty;
                        continue;
                    }

                    currentNode.Penalty = highPenalty;
                }
            }
        }
        */
        // Checking adj nodes to the left
        bool stopCurrentScan = false;

        GraphNode scanNodePoint = node;
        GraphNode currentNodeBeingVetted;

        while (!stopCurrentScan)
        { 
            for (int z = 0; z < 3; z++)
            {
                currentNodeBeingVetted = GridGraphGenerate.gg.nodes[(z - 1 + scanNodePoint.position.z) * GridGraphGenerate.gg.width + (scanNodePoint.position.x - 1)];
                
                if (currentNodeBeingVetted.Penalty == GridGraphGenerate.lowPenalty)
                {
                    adjNodes.Add(currentNodeBeingVetted);
                    scanNodePoint.position.x--;
                    if (scanNodePoint.position.x == GridGraphGenerate.gg.width) stopCurrentScan = true;
                    
                    break;
                }
                
                else if (currentNodeBeingVetted.Penalty == GridGraphGenerate.lowPenalty && z == 2) // On final scan and found no adj node
                {
                    stopCurrentScan = true;
                }
            }
        }

        // Checking adj nodes to the right
        stopCurrentScan = false;

        scanNodePoint = node;
        while (!stopCurrentScan)
        {
            for (int z = 0; z < 3; z++)
            {
                currentNodeBeingVetted = GridGraphGenerate.gg.nodes[(z - 1 + scanNodePoint.position.z) * GridGraphGenerate.gg.width + (scanNodePoint.position.x - 1)];

                if (currentNodeBeingVetted.Penalty == GridGraphGenerate.lowPenalty)
                {
                    adjNodes.Add(currentNodeBeingVetted);
                    scanNodePoint.position.x++;
                    if (scanNodePoint.position.x == GridGraphGenerate.gg.width) stopCurrentScan = true;

                    break;
                }

                else if (currentNodeBeingVetted.Penalty == GridGraphGenerate.lowPenalty && z == 2) // On final scan and found no adj node
                {
                    stopCurrentScan = true;
                }
            }
        }

        return adjNodes;
    }

    private GraphNode FindClosestNode(Vector3 position, List<GraphNode> list)
    {
        GraphNode closestPreviousNode = list[0]; // (don't worry, this assigned value to bound to be reassigned later)
        float previousDistanceSquared = 0f;

        foreach(GraphNode node in list)
        {
            Vector3 nodePosition = (Vector3)node.position;
            float currentDistanceSquared = ((nodePosition.x - position.x) * (nodePosition.x - position.x)) + ((nodePosition.y - position.y) * (nodePosition.y - position.y));

            if (previousDistanceSquared == 0f)
            {
                closestPreviousNode = node;
                previousDistanceSquared = currentDistanceSquared;
            } 
            else if (previousDistanceSquared < currentDistanceSquared)
            {
                closestPreviousNode = node;
                previousDistanceSquared = currentDistanceSquared;
            }
        }

        return closestPreviousNode;
    }

    // you dont need to check for adjNodes like, every time :/

    // Or dropdown jump, or dropdown double jump
    private void CalculateDropdown()
    {
        // dropdown
        for (int i = 0; i < jumpEndNodes.Count; i++)
        {
            Vector3 jumpNodePosition = (Vector3)jumpNodes[i].position;
            Vector3 jumpEndNodePosition = (Vector3)jumpEndNodes[i].position;

            bool waypointFacingRight = false;

            if (jumpEndNodePosition.x > jumpNodePosition.x)
            {
                waypointFacingRight = true;
            }

            GraphNode closestNode = FindClosestNode(jumpEndNodePosition, FindAdjacentNodes(jumpEndNodes[i]));
            Vector3 closestNodePosition = (Vector3)closestNode.position;

            float Sy = jumpEndNodePosition.y - jumpNodePosition.y; // This is bound to be a negative number
            float t_fall = Mathf.Sqrt(2 * Sy / -gravityFall);

            float Sx = Vx * t_fall;

            if((jumpNodePosition.x < -Sx + closestNodePosition.x) && waypointFacingRight ||
                (jumpNodePosition.x > Sx + closestNodePosition.x) && !waypointFacingRight)
            {
                GraphNode dropdownAtThisNode = jumpNodes[i];
                if (!jumpNodesFinal.Contains(dropdownAtThisNode))
                {
                    jumpNodesFinal.Add(dropdownAtThisNode);
                }

                BaseAiController.specialWaypoint newSpecialWaypoint = new BaseAiController.specialWaypoint(
                    typeofWaypoint.RUN, jumpNodes[i], null, waypointFacingRight);

                if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
                {
                    baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                    specialWaypoints.Add(newSpecialWaypoint);
                    // specialNodeCorrespFunction.Add(jumpEndNodes[i]);
                }
            } 
            else
            {
                // ...
            }


        }

        // dropdown + single jump (! Rework this !)
        for (int i = 0; i < jumpEndNodes.Count; i++)
        {
            Vector3 jumpNodePosition = (Vector3)jumpNodes[i].position;
            Vector3 jumpEndNodePosition = (Vector3)jumpEndNodes[i].position;

            bool waypointFacingRight = false;

            if (jumpEndNodePosition.x > jumpNodePosition.x)
            {
                waypointFacingRight = true;
            }

            GraphNode closestNode = FindClosestNode(jumpEndNodePosition, FindAdjacentNodes(jumpEndNodes[i]));
            Vector3 closestNodePosition = (Vector3)closestNode.position;

            float Sy = jumpEndNodePosition.y - jumpNodePosition.y; // This is bound to be a negative number

            float Sfall = Sy - jumpHeight; // remember, Sy cannot be greater than 0

            float t_dropdown = Mathf.Sqrt(2 * Sfall / gravityFall);

            // calculate t_rise
            t_rise = (2 * jumpHeight / Vyi);

            float SdiffFromApex = Sy - (Sfall + jumpHeight);
            t_fall = Mathf.Sqrt(2 * SdiffFromApex / gravityFall);

            float t_total = t_dropdown + t_rise + t_fall;
            float Sx = Vx * t_total;

            if ((jumpNodePosition.x < -Sx + closestNodePosition.x) && waypointFacingRight ||
                (jumpNodePosition.x > Sx + closestNodePosition.x) && !waypointFacingRight)
            {
                // Adding dropdown waypoint
                GraphNode dropdownAtThisNode = jumpNodes[i];
                if (!jumpNodesFinal.Contains(dropdownAtThisNode))
                {
                    jumpNodesFinal.Add(dropdownAtThisNode);
                }

                BaseAiController.specialWaypoint newSpecialWaypoint = new BaseAiController.specialWaypoint(
                    typeofWaypoint.RUN, jumpNodes[i], null, waypointFacingRight);

                if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
                {
                    baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                    specialWaypoints.Add(newSpecialWaypoint);
                    // specialNodeCorrespFunction.Add(jumpEndNodes[i]);
                }

                // Adding single jump waypoint
                // Finding the position where we need to jump;
                float Sx_Dropdown = Vx * t_dropdown;

                // damn, I need to calculate gravityFall & rise somewhere
                float Sy_Dropdown = (gravityFall * t_dropdown * t_dropdown) / 2;

                // rounding to nearest 0.5f
                float Sx_DropdownRounded = (int)(Sx_Dropdown * 2) / 2f;
                float Sy_DropdownRounded = (int)(Sy_Dropdown * 2) / 2f;

                // gg.nodes[(z - 1) * gg.width + x];
                GraphNode jumpAtThisNode = GridGraphGenerate.gg.nodes[((int)(Sy_DropdownRounded / 0.5f) - 1) * GridGraphGenerate.gg.width + (int)(Sx_DropdownRounded / 0.5f)];

                newSpecialWaypoint = new BaseAiController.specialWaypoint(
                    typeofWaypoint.JUMP, jumpAtThisNode, baseCharacterController.PerformJumpAi, waypointFacingRight);

                if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
                {
                    baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                    specialWaypoints.Add(newSpecialWaypoint);
                    // specialNodeCorrespFunction.Add(jumpEndNodes[i]);
                }
            }
            else
            {
                // ...
            }


        }

        // dropdown + double jump ?
    }

    private void CalculateDoubleJump()
    {
        for (int i = 0; i < jumpEndNodes.Count; i++)
        {
            Vector3 jumpNodePosition = (Vector3)jumpNodes[i].position;
            Vector3 jumpEndNodePosition = (Vector3)jumpEndNodes[i].position;

            bool waypointFacingRight = false;

            if (jumpEndNodePosition.x > jumpNodePosition.x)
            {
                waypointFacingRight = true;
            }

            GraphNode closestNode = FindClosestNode(jumpEndNodePosition, FindAdjacentNodes(jumpEndNodes[i]));
            Vector3 closestNodePosition = (Vector3)closestNode.position;
            List<GraphNode> adjNodesAtJump = FindAdjacentNodes(jumpNodes[i]);
            adjNodesAtJump.Add(jumpNodes[i]);

            float t_rise = 2 * jumpHeight / Vyi;

            float Sy;
            float Sz;
            float t_fall1;
            float t_fall2;
            float t_total;

            List<GraphNode> potentialNodes = new List<GraphNode>();

            foreach (GraphNode node in adjNodesAtJump)
            {
                Vector3 nodePosition = (Vector3)node.position;

                Sy = jumpEndNodePosition.y - nodePosition.y;
                Sz = 1.5f; // magic value

                if (jumpHeight * 2 < Sy)
                {
                    // ..  DON'T PERFORM DOUBLE JUMP
                }
                else if (jumpHeight * 2 - Sz < Sy)
                {
                    Sz = Sy - ((jumpHeight * 2) - Sz);
                }

                t_fall1 = Mathf.Sqrt(2 * (2 * jumpHeight - Sy - Sz) / gravityFall);
                t_fall2 = Mathf.Sqrt(2 * Sz / Vyi);
                t_total = t_rise + t_fall1 + t_rise + t_fall2;

                float Sx = t_total * Vx;  

                if ((nodePosition.x < Sx + jumpEndNodePosition.x) && waypointFacingRight ||
                    (nodePosition.x > Sx + jumpEndNodePosition.x) && !waypointFacingRight)
                {
                    potentialNodes.Add(node);
                    continue;
                }
                else
                {
                    // ...
                }

            }

            // if(potentialNodes empty, cycle to adjNodesAtEndJump while using the closest node to jumpEndNode

            // Possible to perform double jump
            GraphNode jumpAtThisNode = FindClosestNode(jumpNodePosition, potentialNodes);
            Vector3 jumpAtThisNodePosition = (Vector3)jumpAtThisNode.position;

            Sy = jumpEndNodePosition.y - jumpAtThisNodePosition.y;
            Sz = 1.5f; // magic value

            if (jumpHeight * 2 < Sy)
            {
                // ..  DON'T PERFORM DOUBLE JUMP
            }
            else if (jumpHeight * 2 - Sz < Sy)
            {
                Sz = Sy - ((jumpHeight * 2) - Sz);
            }
;
            t_fall1 = Mathf.Sqrt(2 * (2 * jumpHeight - Sy - Sz) / gravityFall);
            t_fall2 = Mathf.Sqrt(2 * Sz / Vyi);
            t_total = t_rise + t_fall1 + t_rise + t_fall2;

            // Adding single jump waypoint
            // Finding the position where we need to jump;
            float Sx_Dropdown = Vx * (t_rise + t_fall1) + jumpAtThisNodePosition.x;

            // damn, I need to calculate gravityFall & rise somewhere
            float Sy_Dropdown = (jumpHeight - ((gravityFall * (t_fall1) * (t_fall1)) / 2)) + jumpAtThisNodePosition.y;

            // rounding to nearest 0.5f
            float Sx_DropdownRounded = (int)(Sx_Dropdown * 2) / 2f;
            float Sy_DropdownRounded = (int)(Sy_Dropdown * 2) / 2f;

            // gg.nodes[(z - 1) * gg.width + x];
            GraphNode secondJumpAtThisNode = GridGraphGenerate.gg.nodes[((int)(Sy_DropdownRounded / 0.5f) - 1) * GridGraphGenerate.gg.width + (int)(Sx_DropdownRounded / 0.5f)];

            if (!jumpNodesFinal.Contains(jumpAtThisNode))
            {
                jumpNodesFinal.Add(jumpAtThisNode);
                BaseAiController.specialWaypoint newSpecialWaypoint = new BaseAiController.specialWaypoint(
                typeofWaypoint.JUMP, jumpAtThisNode, baseCharacterController.PerformJumpAi, waypointFacingRight);

                if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
                {
                    baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                    specialWaypoints.Add(newSpecialWaypoint);
                    // specialNodeCorrespFunction.Add(jumpEndNodes[i]);
                }
            }

            if (!jumpNodesFinal.Contains(secondJumpAtThisNode))
            {
                jumpNodesFinal.Add(secondJumpAtThisNode);
                BaseAiController.specialWaypoint newSpecialWaypoint = new BaseAiController.specialWaypoint(
                typeofWaypoint.JUMP, secondJumpAtThisNode, baseCharacterController.PerformJumpAi, waypointFacingRight);

                if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
                {
                    baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                    specialWaypoints.Add(newSpecialWaypoint);
                    // specialNodeCorrespFunction.Add(jumpEndNodes[i]);
                }
            }
        }
    }

    private void CalculateSingleJumpDashing()
    {
        for (int i = 0; i < jumpEndNodes.Count; i++)
        {
            Vector3 jumpNodePosition = (Vector3)jumpNodes[i].position;
            Vector3 jumpEndNodePosition = (Vector3)jumpEndNodes[i].position;

            bool waypointFacingRight = false;

            if (jumpEndNodePosition.x > jumpNodePosition.x)
            {
                waypointFacingRight = true;
            }

            GraphNode closestNode = FindClosestNode(jumpEndNodePosition, FindAdjacentNodes(jumpEndNodes[i]));
            Vector3 closestNodePosition = (Vector3)closestNode.position;
            List<GraphNode> adjNodesAtJump = FindAdjacentNodes(jumpNodes[i]);
            adjNodesAtJump.Add(jumpNodes[i]);

            float SxDash = baseCharacterController.dodgeDistance;
            float t_rise = 2 * jumpHeight / Vyi;

            float SxRise = t_rise * Vx;

            List<GraphNode> potentialNodes = new List<GraphNode>();

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
                    potentialNodes.Add(node);
                    continue;
                }
                else
                {
                    // ...
                }

            }

            // if(potentialNodes empty, cycle to adjNodesAtEndJump while using the closest node to jumpEndNode

            // Possible to perform double jump
            GraphNode jumpAtThisNode = FindClosestNode(jumpNodePosition, potentialNodes);
            Vector3 jumpAtThisNodePosition = (Vector3)jumpAtThisNode.position;

            Sz = 1.5f; // magic number
            Sy = jumpEndNodePosition.y - jumpAtThisNode.y;
            Sb = jumpHeight - Sz - Sy;

            t_fall = Mathf.Sqrt(2 * Sb / gravityFall);
            t_total = t_rise + baseCharacterController.dodgeTime + t_fall;

            // Adding single jump waypoint
            // Finding the position where we need to jump;
            float Sx_Dropdown = Vx * (t_rise + t_fall) + jumpAtThisNodePosition.x;

            // damn, I need to calculate gravityFall & rise somewhere
            float Sy_Dropdown = (jumpHeight - ((gravityFall * (t_fall) * (t_fall)) / 2)) + jumpAtThisNodePosition.y;

            // rounding to nearest 0.5f
            float Sx_DropdownRounded = (int)(Sx_Dropdown * 2) / 2f;
            float Sy_DropdownRounded = (int)(Sy_Dropdown * 2) / 2f;

            // gg.nodes[(z - 1) * gg.width + x];
            GraphNode dodgeAtThisNode = GridGraphGenerate.gg.nodes[((int)(Sy_DropdownRounded / 0.5f) - 1) * GridGraphGenerate.gg.width + (int)(Sx_DropdownRounded / 0.5f)];

            if (!jumpNodesFinal.Contains(jumpAtThisNode))
            {
                jumpNodesFinal.Add(jumpAtThisNode);
                BaseAiController.specialWaypoint newSpecialWaypoint = new BaseAiController.specialWaypoint(
                typeofWaypoint.JUMP, jumpAtThisNode, baseCharacterController.PerformJumpAi, waypointFacingRight);

                if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
                {
                    baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                    specialWaypoints.Add(newSpecialWaypoint);
                    // specialNodeCorrespFunction.Add(jumpEndNodes[i]);
                }
            }

            if (!jumpNodesFinal.Contains(dodgeAtThisNode))
            {
                jumpNodesFinal.Add(dodgeAtThisNode);
                BaseAiController.specialWaypoint newSpecialWaypoint = new BaseAiController.specialWaypoint(
                typeofWaypoint.DODGE, dodgeAtThisNode, null, waypointFacingRight);

                if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
                {
                    baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                    specialWaypoints.Add(newSpecialWaypoint);
                    // specialNodeCorrespFunction.Add(jumpEndNodes[i]);
                }
            }
        }
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

    private void CalculateWaitingWaypoint()
    {

    }

    private void CalculateForesight(GraphNode dynamicNode)
    {

    }

    private List<GraphNode> FindVaccantOverheadForOvershoot(GraphNode scanNodePoint)
    {
        List<GraphNode> adjNodesToTarget = FindAdjacentNodes(scanNodePoint);
        GraphNode currentNodeBeingVetted;
        List<GraphNode> vaccantNodes = new List<GraphNode>(); // Ranges from 0 to 2

        // Checking nodes to the left
        bool notVaccant = false;
        for (int z = 0; z < 3; z++) {
            currentNodeBeingVetted = GridGraphGenerate.gg.nodes[(z - 1 + scanNodePoint.position.z) * GridGraphGenerate.gg.width + (scanNodePoint.position.x - 1)];
            foreach (GraphNode node in adjNodesToTarget)
            {
                if (currentNodeBeingVetted == node)
                {
                    notVaccant = true;
                }
            }
        }

        if (!notVaccant) vaccantNodes.Add(GridGraphGenerate.gg.nodes[(scanNodePoint.position.z) * GridGraphGenerate.gg.width + (scanNodePoint.position.x - 1)]);

        // Checking nodes to the right
        notVaccant = false;
        for (int z = 0; z < 3; z++)
        {
            currentNodeBeingVetted = GridGraphGenerate.gg.nodes[(z - 1 + scanNodePoint.position.z) * GridGraphGenerate.gg.width + (scanNodePoint.position.x + 1)];
            foreach (GraphNode node in adjNodesToTarget)
            {
                if (currentNodeBeingVetted == node)
                {
                    notVaccant = true;
                }
            }
        }

        if (!notVaccant) vaccantNodes.Add(GridGraphGenerate.gg.nodes[(scanNodePoint.position.z) * GridGraphGenerate.gg.width + (scanNodePoint.position.x - 1)]);

        return vaccantNodes;
    }

    private void CalculateSingleJump()
    {
        for (int i = 0; i < jumpEndNodes.Count; i++)
        {

            // float Vx = 8.5f;
            // float jumpHeight = baseCharacterController.jumpHeight;

            Vector3 jumpEndNodePosition = (Vector3)jumpEndNodes[i].position;
            Vector3 jumpNodePosition = (Vector3)jumpNodes[i].position;

            bool waypointFacingRight = false;

            if(jumpEndNodePosition.x > jumpNodePosition.x)
            {
                waypointFacingRight = true;
            }

            GraphNode jumpAtThisNode;
            Vector2 SxSy;

            for (int j = 1; j < jumpNodeStartAndEndIDs[1 + (2 * i)]; j++)
            {

                int index = jumpNodeStartAndEndIDs[1 + (2 * i)] - j;
                if (originalNodes[index].Penalty == GridGraphGenerate.highPenalty) continue;

                SxSy = CalculateSxSy(jumpEndNodePosition, originalNodes[index], waypointFacingRight);
                Vector3 nodePosition = (Vector3)originalNodes[index].position;

                if ((SxSy.x + jumpEndNodePosition.x > nodePosition.x + 0.25f && !waypointFacingRight) ||
                    (SxSy.x + jumpEndNodePosition.x < nodePosition.x - 0.25f && waypointFacingRight))
                {
                    // newNodes.Remove(originalNodes[index]);
                    if (!ignoreNodes.Contains(originalNodes[index]))
                    {
                        ignoreNodes.Add(originalNodes[index]);
                    }
                }
                else
                {
                    jumpAtThisNode = originalNodes[index];
                    if (!jumpNodesFinal.Contains(jumpAtThisNode))
                    {
                        jumpNodesFinal.Add(jumpAtThisNode);
                    }

                    BaseAiController.specialWaypoint newSpecialWaypoint = new BaseAiController.specialWaypoint(
                        typeofWaypoint.JUMP, jumpAtThisNode, baseCharacterController.JumpWaypointAI, waypointFacingRight);

                    if (!baseAiController.specialWaypoints.Contains(newSpecialWaypoint))
                    {
                        baseAiController.specialWaypoints.Add(newSpecialWaypoint);
                        specialWaypoints.Add(newSpecialWaypoint);
                        // specialNodeCorrespFunction.Add(jumpEndNodes[i]);
                    }

                    // for gizmos sake
                    gizmoJumping_SxSy = SxSy;
                    break;
                }
            }
        }
    }

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
            Vector3 nodePosition = (Vector3)node.position;

            Handles.Label((Vector3)node.position + Vector3.up * 1f + Vector3.right * 0.5f,
                new GUIContent("Sx : Sy [" + gizmoJumping_SxSy.x + ", " + gizmoJumping_SxSy.y + "]"));
            Handles.Label((Vector3)node.position + Vector3.up * 0.5f + Vector3.right * 0.5f,
                new GUIContent("Diff in X = " + Mathf.Abs(nodePosition.x - nodePosition.x)));
        }

        // Drawing unique special waypoint gizmos
        foreach (BaseAiController.specialWaypoint specialWaypoint in specialWaypoints)
        {
            switch (specialWaypoint.waypointType)
            {
                case typeofWaypoint.RUN:
                    break;

                case typeofWaypoint.JUMP:
                    Vector2 oldPosition = Vector2.zero;
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawCube(specialWaypoint.nodePosition, new Vector3(0.5f, 0.5f));

                    // BaseAiController.specialWaypoint specialWaypoint = specialWaypoints.FindIndex
                    for (int k = 0; k < resolution; k++)
                    {
                        float curSx = (gizmoJumping_SxSy.x / resolution) * k;
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
                            curSx = gizmoJumping_SxSy.x;
                            elaspedTime = ((specialWaypoint.facingRight) ? -curSx : curSx) / Vx;
                            curSy = jumpHeight + (-gravityFall * (elaspedTime - t_rise) * (elaspedTime - t_rise) * 0.5f);
                            newPosition = new Vector2((jumpPos.x - curSx), (jumpPos.y + curSy));

                            Gizmos.color = Color.cyan;
                            Gizmos.DrawLine(oldPosition, newPosition);
                        }
                    }

                    break;

                case typeofWaypoint.DODGE:
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
