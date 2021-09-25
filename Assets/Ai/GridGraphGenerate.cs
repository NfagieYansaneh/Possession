using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;

/* purpose of GridGraphGenerate is just to generate the grid of nodes that Ai characters will see as these nodes represent the Ai's vision
 */

public class GridGraphGenerate : MonoBehaviour
{
    // NodeGroupStruct are groupings of adjacent groundNodes in order to quickly assess how many 'platforms' we have in our scene 

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

        public GraphNode leftistNode_3IN; // 3 nodes in from our leftist node (if that isn't possible, leftistNoe_3IN will adjust)
        // the 3 node in just makes sure that I can select the leftist node and have a sufficent enough padding without fear of the Ai falling short of reaching the leftist node
        public Vector3 leftistNode_3IN_Position { get { return (Vector3)leftistNode_3IN.position; } }

        public GraphNode rightistNode;
        public Vector3 rightistNodePosition { get { return (Vector3)rightistNode.position; } }

        public GraphNode rightistNode_3IN; // 3 nodes in from our rightest node (if that isn't possible, rightestNoe_3IN will adjust)
        // the 3 node in just makes sure that I can select the rightist node and have a sufficent enough padding without fear of the Ai falling short of reaching the rightist node
        public Vector3 rightistNode_3IN_Position { get { return (Vector3)rightistNode_3IN.position; } }

        public GraphNode middleNode;
        public Vector3 middleNodePosition { get { return (Vector3)middleNode.position; } }

        public int Area; // how much area does this nodeGroup (our platform) take up?

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

            List<GraphNode> buffer = new List<GraphNode>();
            bool foundAdjNodes = false;
            buffer = Helper.FindAdjacentNodes(leftistNode, ref foundAdjNodes, AdjNodeSearchDirection.BOTH, 2);

            if (foundAdjNodes)
            {
                leftistNode_3IN = buffer[buffer.Count - 1];
            }
            else leftistNode_3IN = leftistNode;

            foundAdjNodes = false;
            buffer = Helper.FindAdjacentNodes(rightistNode, ref foundAdjNodes, AdjNodeSearchDirection.BOTH, 2);

            if (foundAdjNodes)
            {
                rightistNode_3IN = buffer[buffer.Count - 1];
            }
            else rightistNode_3IN = rightistNode;

        }
    }

    public static List<NodeGroupStruct> nodeGroups = new List<NodeGroupStruct>();

    bool drawForLowPenalty = false; // just a debugging tool to draw all ground nodes 
    // (doesn't work in build since this is dependent on editor tools and I only need this tool in the editor anyways...

    List<GraphNode> lowPenaltyNodes = new List<GraphNode>(); // low penalty nodes represent ground nodes

    public const uint lowPenalty = 0; // low penalty represents the 0 penalty for walking on a ground node
    public const uint highPenalty = 3750; // high penalty represents the 3750 penalty for traversing through air and is to dissuade Ai paths to unnecessarily move through the air

    public static GridGraph gg;

    void Start()
    {
        nodeGroups.Clear();
        Scan();

    }

    // finds the nodegroup that this node is apart of...
    public static NodeGroupStruct FindThisNodesNodeGroup(GraphNode node)
    {
        for(int index=0; index<nodeGroups.Count;index++)
        {
            NodeGroupStruct nodeGroup = nodeGroups[index];
            if (nodeGroup.allNodes.Contains(node))
                return nodeGroup;
        }

        return nodeGroups[0]; // this will typically never be called, but I had to include this because of compiler error
    }

    // Scans the scene to update GridGraph
    private void Scan()
    {
        AstarPath.FindAstarPath();
        AstarPath.active.Scan();

        if (AstarPath.active.data.gridGraph.nodes == null)
            AstarPath.active.Scan();


        gg = AstarPath.active.data.gridGraph;

        List<GraphNode> nodes = new List<GraphNode>();
        gg.GetNodes((System.Action<GraphNode>)nodes.Add);

        // At this point in the function, we have just stored a grid of nodes that represent all walkable and non-walkable nodes 
        // non-walkable nodes are areas that are being taken up by a ground collider
        // and now I will loop through every node in the grid graph and identity grounded nodes (represented with a low penalty and drawn as a green square)
        // as well as identifying air nodes (represented with a high penalty and drawn as a red square)

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

        // at this point, we are going to be cycling through every node in order to identify all nodeGroups (essentially all platforms in our scene)...

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
                            else; // Debug.Log(nodesToAdd.Count);

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

        // Debugging that helps illustrate where all nodeGroups (essentially platforms) are within the scene
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

        // If I press the "Refresh Grid Graph" button, I refresh the grid graph
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
                // draws a green opaque square at every ground node
                Gizmos.DrawCube((Vector3)node.position, new Vector3(gg.nodeSize, gg.nodeSize));
            }
        }
    }
}
