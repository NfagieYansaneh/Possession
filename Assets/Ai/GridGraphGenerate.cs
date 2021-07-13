using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;

public class GridGraphGenerate : MonoBehaviour
{
    bool drawForLowPenalty = false;
    List<GraphNode> lowPenaltyNodes = new List<GraphNode>();

    public const uint lowPenalty = 0;
    public const uint highPenalty = 3750;

    GridGraph gg;

    // Start is called before the first frame update
    void Start()
    {
        Scan();

        /* gg.GetNodes(node => {
            Debug.Log((Vector3)node.position);
            node.Penalty = (uint)Mathf.Log(node.position.y + 5 * node.position.y);
        }); */

    }

    private void Scan()
    {
        AstarPath.FindAstarPath();
        AstarPath.active.Scan();

        if (AstarPath.active.data.gridGraph.nodes == null)
            AstarPath.active.Scan();


        gg = AstarPath.active.data.gridGraph;

        List<GraphNode> nodes = new List<GraphNode>();
        gg.GetNodes((System.Action<GraphNode>)nodes.Add);

        for (int x = 0; x < gg.width - 1; x++)
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
    }

    private void OnGUI()
    {
        drawForLowPenalty = GUI.Toggle(new Rect(500, 120, 230, 25), drawForLowPenalty, new GUIContent("Draw for low penalty"));
        if(GUI.Button(new Rect(500, 140, 100, 40), new GUIContent("Refresh Grid Graph")))
        {
            Scan();
        }
    }

    private void OnDrawGizmos()
    {
        if (drawForLowPenalty)
        {
            Color color = new Color(0, 255, 0, 50);
            Gizmos.color = color;

            foreach (GraphNode node in lowPenaltyNodes)
            {
                // Debug.Log((Vector3)node.position);
                Gizmos.DrawCube((Vector3)node.position, new Vector3(gg.nodeSize, gg.nodeSize));
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
