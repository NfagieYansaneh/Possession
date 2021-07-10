using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class BaseAiPathModifier : MonoModifier
{
    public List<GraphNode> jumpNodes = new List<GraphNode>();
    public List<GraphNode> jumpEndNodes = new List<GraphNode>();
    public List<GraphNode> originalNodes;
    public List<int> jumpNodeStartAndEndIDs = new List<int>();

    public BaseCharacterController baseCharacterController;
    public int resolution = 6;

    public void Start()
    {
        baseCharacterController = GetComponent<BaseCharacterController>();
    }

    public override int Order { get { return 60; } }
    public override void Apply(Path path)
    {
        // Debug.Log("Hello???");
        if (path.error || path.vectorPath == null || 
            path.vectorPath.Count <= 3) { return; }

        // Debug.Log("Hello???");
        // List<Vector3> newPath = new List<Vector3>();
        // List<Vector3> originalPath = path.vectorPath;
        originalNodes = path.path;

        jumpNodes.Clear();
        jumpEndNodes.Clear();
        jumpNodeStartAndEndIDs.Clear();

        bool findNextLowPenalty = false;

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

        // throw new System.NotImplementedException();
    }

    public Vector2 CalculateSxSy(Vector3 jumpEndNodePosition, GraphNode node)
    {
        float Vx = 8.5f;
        float jumpHeight = baseCharacterController.jumpHeight;

        Vector3 jumpNodePosition = (Vector3)node.position;

        float Sy = jumpEndNodePosition.y - jumpNodePosition.y;
        float gravityRise = baseCharacterController.gravity * baseCharacterController.gravityMultiplier;
        float gravityFall = baseCharacterController.gravity * baseCharacterController.gravityMultiplier * baseCharacterController.fallingGravityMultiplier;
        float Vyi = Mathf.Sqrt(2 * gravityRise * jumpHeight);

        float Sx = Vx * ((2 * jumpHeight / Vyi) + Mathf.Sqrt(2 * Sy / gravityFall));

        return new Vector2(Sx, Sy);
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        foreach(GraphNode node in jumpNodes)
        {
            Gizmos.DrawCube((Vector3)node.position, new Vector3(0.5f, 0.5f));
        }

        Gizmos.color = Color.gray;
        for (int i=0; i<jumpEndNodes.Count; i++)
        {
            
            float Vx = 8.5f;
            float jumpHeight = baseCharacterController.jumpHeight;

            Vector3 jumpEndNodePosition = (Vector3)jumpEndNodes[i].position;
            Vector3 jumpNodePosition = (Vector3)jumpNodes[i].position;
            jumpNodePosition.y += 0.5f * 3;

            float Sy = jumpEndNodePosition.y - jumpNodePosition.y;
            float gravityRise = baseCharacterController.gravity * baseCharacterController.gravityMultiplier;
            float gravityFall = baseCharacterController.gravity * baseCharacterController.gravityMultiplier * baseCharacterController.fallingGravityMultiplier;
            float Vyi = Mathf.Sqrt(2 * gravityRise * jumpHeight);

            float t_rise = (2 * jumpHeight / Vyi);
            float t_fall = Mathf.Sqrt(2 * Sy / gravityFall);
            float Sx = Vx * (t_rise + t_fall);


            GraphNode jumpAtThisNode;

            for (int j = 0; j < jumpNodeStartAndEndIDs[1 + (2 * i)] ; j++){

                int index = jumpNodeStartAndEndIDs[1 + (2 * i)] - j;
                if (originalNodes[index].Penalty == GridGraphGenerate.highPenalty) continue;

                Vector2 SxSy = CalculateSxSy(jumpEndNodePosition, originalNodes[index]);
                Vector3 nodePosition = (Vector3)originalNodes[index].position;
                // Gizmos.color = Color.black;

                if (SxSy.x + jumpEndNodePosition.x > nodePosition.x)
                {
                    Gizmos.color = Color.black;
                    Gizmos.DrawCube((Vector3)originalNodes[index].position, new Vector3(0.5f, 0.5f));
                    // Debug.LogWarning(nodePosition.x + " : " + nodePosition.y + " : " + (SxSy.x + jumpEndNodePosition.x) + " : " + SxSy.y);
                }
                else
                {
                    // Debug.Log("SxSy.x = " + (SxSy.x + jumpEndNodePosition.x) + " vs " + nodePosition.x);
                    Gizmos.color = Color.cyan;
                    Gizmos.DrawCube((Vector3)originalNodes[index].position, new Vector3(0.5f, 0.5f));
                    // Debug.Log(nodePosition.x + " : " + nodePosition.y + " : " + (SxSy.x + jumpEndNodePosition.x) + " : " + SxSy.y);

                    jumpAtThisNode = originalNodes[index];
                    Vector2 oldPosition = Vector2.zero;

                    for (int k=0; k<resolution; k++)
                    {
                        float curSx = (SxSy.x / resolution) * k;
                        float curSy = 0f;
                        float elaspedTime = curSx / Vx;
                        if (elaspedTime < t_rise)
                        {
                            curSy = (Vyi * elaspedTime) + ((-gravityRise * elaspedTime * elaspedTime) / 2);
                        } else
                        {
                            curSy = jumpHeight + (-gravityFall * (elaspedTime - t_rise) * (elaspedTime - t_rise) * 0.5f);
                            // curSy = (Vyi * elaspedTime) + ((-gravityFall * elaspedTime * elaspedTime) / 2);
                        }

                        Vector3 jumpPos = (Vector3)jumpAtThisNode.position;
                        Vector2 newPosition = new Vector2((jumpPos.x - curSx), (jumpPos.y + curSy));

                        if (oldPosition != Vector2.zero)
                        {
                            Gizmos.color = Color.cyan;
                            Gizmos.DrawLine(newPosition, oldPosition);
                        }

                        oldPosition = newPosition;

                        if (k == resolution - 1)
                        {
                            curSx = SxSy.x;
                            elaspedTime = curSx / Vx;
                            curSy = jumpHeight + (-gravityFall * (elaspedTime - t_rise) * (elaspedTime - t_rise) * 0.5f);
                            newPosition = new Vector2((jumpPos.x - curSx), (jumpPos.y + curSy));

                            Gizmos.color = Color.cyan;
                            Gizmos.DrawLine(oldPosition, newPosition);
                        }
                    }
                    break;
                }
                //Debug.Log(nodePosition.x + " : " + nodePosition.y + " : " + (SxSy.x + jumpEndNodePosition.x) + " : " + SxSy.y);
                // Debug.Break();
            }

            Gizmos.color = Color.gray;
            // float horizontalDisplacementIncrement = Sx / resolution;

            /* for(int j=0; j<resolution; j++)
             {
                 float curSx = horizontalDisplacementIncrement * j;
                 float elapsedTime = curSx / Vx;
                 float curSy;

                 if (curSx > (Sx / 2)) {
                     curSy = (Vyi * elapsedTime) + ((gravityRise * elapsedTime * elapsedTime) / 2);
                 } else
                 {
                     curSy = jumpHeight - (gravityFall * elapsedTime / 2);
                 }

                 Gizmos.DrawSphere(new Vector3(-curSx + jumpEndNodePosition.x + Sx, curSy + jumpNodePosition.y), 0.2f);
             } */

            // Debug.Log("Sy = " + Sy + "m");
            // Debug.Log("Sx = " + Sx + "m");

            Gizmos.DrawCube((Vector3)jumpEndNodes[i].position, new Vector3(0.5f, 0.5f));
            Gizmos.DrawRay((Vector3)jumpEndNodes[i].position, new Vector3(Sx, 0f));

#if UNITY_EDITOR
            Handles.Label((Vector3)jumpEndNodes[i].position + Vector3.up * 1f, new GUIContent("Pre-determined Jump"));
#endif
        }
    }
}
