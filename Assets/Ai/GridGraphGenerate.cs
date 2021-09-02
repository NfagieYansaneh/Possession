using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;

public class GridGraphGenerate : MonoBehaviour
{
    public struct NodeGroupStruct
    {
        public List<GraphNode> allNodes;
        public List<Vector3> allNodePositions;

        public GraphNode highestNode;
        public Vector3 highestNodePosition { get { return (Vector3)highestNode.position; } }

        public GraphNode lowestNode;
        public Vector3 lowestNodePosition { get { return (Vector3)lowestNode.position; } }

        public GraphNode leftistNode;
        public Vector3 leftistNodePosition { get { return (Vector3)leftistNode.position; } }

        public GraphNode rightistNode;
        public Vector3 rightistNodePosition { get { return (Vector3)rightistNode.position; } }

        public GraphNode middleNode;
        public Vector3 middleNodePosition { get { return (Vector3)middleNode.position; } }

        public int Area;

        public NodeGroupStruct(List<GraphNode> nodes, bool leftToRight = true)
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

            foreach (GraphNode node in allNodes)
            {
                allNodePositions.Add((Vector3)node.position);
            }


            float highest = 0f;
            int highestIndex = 0;

            float lowest = 0f;
            int lowestIndex = 0;

            leftistNode = (leftToRight) ? allNodes[0] : allNodes[allNodes.Count - 1];
            rightistNode = (!leftToRight) ? allNodes[0] : allNodes[allNodes.Count - 1];

            Area = allNodes.Count;

            middleNode = allNodes[(int)allNodes.Count / 2];

            for (int i = 0; i < allNodePositions.Count; i++)
            {
                if (i == 0)
                {
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

    public List<NodeGroupStruct> nodeGroups = new List<NodeGroupStruct>();

    bool drawForLowPenalty = false;
    List<GraphNode> lowPenaltyNodes = new List<GraphNode>();

    public const uint lowPenalty = 0;
    public const uint highPenalty = 3750;

    public static GridGraph gg;

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


        for (int x = 0; x < gg.width; x++)
        {
            for (int z = 0; z < gg.depth; z++)
            {
                GraphNode currentNode = gg.nodes[z * gg.width + x];
                if (z == 0 && currentNode.Walkable)
                {
                    currentNode.Penalty = lowPenalty;
                    continue;
                }

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

        bool searchingForNewGroup = true;
        bool requirementReached = false;

        for (int x = 0; x < gg.width; x++)
        {
            for (int z = 0; z < gg.depth; z++)
            {
                GraphNode currentNode = gg.nodes[z * gg.width + x];
                if (currentNode.Walkable)
                {
                    if (currentNode.Penalty == lowPenalty && searchingForNewGroup)
                    {
                        bool forceSkip = false;
                        foreach (NodeGroupStruct nodeGroup in nodeGroups)
                        {
                            if (nodeGroup.allNodes.Contains(currentNode))
                                forceSkip = true;
                        }

                        if (!forceSkip)
                        {
                            bool foundAdjNodes = false;
                            List<GraphNode> nodesToAdd = Helper.FindAdjacentNodes(currentNode, ref foundAdjNodes, AdjNodeSearchDirection.BOTH);

                            if (!foundAdjNodes) Debug.Log("Found no adjacent nodes?");
                            else Debug.Log(nodesToAdd.Count);

                            nodesToAdd.Insert(0, currentNode);

                            NodeGroupStruct newNodeGroup = new NodeGroupStruct(nodesToAdd);
                            nodeGroups.Add(newNodeGroup);

                            searchingForNewGroup = false;
                            requirementReached = false;
                        }
                    }

                    if (currentNode.Penalty == highPenalty && !requirementReached)
                    {
                        requirementReached = true;
                    }
                    else if (currentNode.Penalty == highPenalty && requirementReached)
                    {
                        searchingForNewGroup = true;
                    }
                    else
                    {
                        requirementReached = false;
                    }
                }
            }
        }

        Debug.LogError("About to print # of nodeGroups...");
        Debug.LogError("How many node groups? " + nodeGroups.Count);
        foreach (NodeGroupStruct nodeGroup in nodeGroups)
        {
            Helper.DrawArrow.ForDebugTimed(nodeGroup.middleNodePosition + Vector3.up * 0.5f, Vector3.down, Color.green,     10000f, 0.25f, 90);
            Helper.DrawArrow.ForDebugTimed(nodeGroup.middleNodePosition + Vector3.up * 0.5f, Vector3.right, Color.green,    10000f, 0.25f, 90);
            Helper.DrawArrow.ForDebugTimed(nodeGroup.middleNodePosition + Vector3.up * 0.5f, Vector3.left, Color.green,     10000f, 0.25f, 90);
            Helper.DrawArrow.ForDebugTimed(nodeGroup.middleNodePosition + Vector3.up * 0.5f, Vector3.up, Color.green,       10000f, 0.25f, 90);
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
