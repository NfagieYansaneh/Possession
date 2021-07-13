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

    private void CalculateDropdown()
    {

    }

    private void CalculateDoubleJump()
    {

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
