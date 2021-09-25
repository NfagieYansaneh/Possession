using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;

#if UNITY_EDITOR
using UnityEditor;
#endif

/* Helper.cs just stores some universal helpful tools that can be called upon by other scripts
 */

public static class Helper
{
    // This specific function was acquired from...
    // Jessy from & RazaTech https://forum.unity.com/threads/re-map-a-number-from-one-range-to-another.119437/
    // So i do not take credit for this, this a just a 3rd party tool that assissted me in writing this program
    public static float Remap(this float from, float fromMin, float fromMax, float toMin, float toMax)
    {
        var fromAbs = from - fromMin;
        var fromMaxAbs = fromMax - fromMin;

        var normal = fromAbs / fromMaxAbs;

        var toMaxAbs = toMax - toMin;
        var toAbs = toMaxAbs * normal;

        var to = toAbs + toMin;

        return to;
    }

    // Finds adjacent ground nodes from a specified node
    public static List<GraphNode> FindAdjacentNodes(GraphNode node, ref bool foundAdjNodes, AdjNodeSearchDirection searchDirection, int count = 0, GraphNode startCounterPastNode = null, int maxHeightDisplacementInNodes = 10)
    {
        List<GraphNode> adjNodes = new List<GraphNode>();
        if (node.Penalty == GridGraphGenerate.highPenalty) // GridGraphGenerate.highPenalty represents the penalty value an air node would possess
        {
            foundAdjNodes = false;
            return null;
        }

        // Checking adj nodes to the left
        bool stopCurrentScan = false;

        GraphNode currentNodeBeingVetted = null;
        Vector2 temp = Helper.TurnPositionIntoPointOnGridGraph(GridGraphGenerate.gg, node);
        Vector2 scanNodePoint = temp;

        // counter just allows us to put a cap on the number of adjacent nodes we find
        bool counterActive = false;
        bool counterPaused = (startCounterPastNode != null && startCounterPastNode != node); // counter will be paused if startCounterPastNode exists. 
        // Thus we will turn on the counter later once we reach this the counter node if startCOunterPastNode exists...

        int counterIndex = 0;
        if (count != 0) counterActive = true;

        // checking for adjacent nodes on the left
        if (searchDirection == AdjNodeSearchDirection.LEFT || searchDirection == AdjNodeSearchDirection.BOTH)
            while (!stopCurrentScan && (!counterActive || (counterActive && counterIndex < count)))
            {

                if (scanNodePoint.x == 0f || scanNodePoint.y > GridGraphGenerate.gg.Depth) break;
                if (maxHeightDisplacementInNodes < Mathf.Abs(scanNodePoint.y - temp.y))
                {
                    break;
                }

                // scanning to the left of our scanNodePoint as we check the bottom left adjacent node, middle left adjacent node, and upper left adjacent node
                for (int z = 0; z < 3; z++)
                {

                    if (scanNodePoint.y < GridGraphGenerate.gg.Depth - 1 && scanNodePoint.y != 0f)
                    {
                        currentNodeBeingVetted = GridGraphGenerate.gg.nodes[(z - 1 + (int)(scanNodePoint.y)) * GridGraphGenerate.gg.width + (int)(scanNodePoint.x - 1)];
                    }
                    else if ((scanNodePoint.y == GridGraphGenerate.gg.Depth - 1 || scanNodePoint.y == 0f) && z == 0)
                        currentNodeBeingVetted = GridGraphGenerate.gg.nodes[(int)(scanNodePoint.x - 1)];
                    else if (scanNodePoint.y == GridGraphGenerate.gg.Depth - 1 && z > 0)
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

                        if (scanNodePoint.x == 0) stopCurrentScan = true;

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

                if (startCounterPastNode != null && startCounterPastNode == currentNodeBeingVetted)
                    counterPaused = false;

                if (!counterPaused)
                    counterIndex++;
            }

        // Checking adjacent nodes to the right
        stopCurrentScan = false;
        scanNodePoint = temp;

        counterPaused = (startCounterPastNode != null && startCounterPastNode != node);
        counterIndex = 0;

        if (searchDirection == AdjNodeSearchDirection.RIGHT || searchDirection == AdjNodeSearchDirection.BOTH)
            while (!stopCurrentScan && (!counterActive || (counterActive && counterIndex < count)))
            {

                if (scanNodePoint.x == GridGraphGenerate.gg.width || scanNodePoint.y > GridGraphGenerate.gg.Depth) break;
                if (maxHeightDisplacementInNodes < Mathf.Abs(scanNodePoint.y - temp.y)) break;

                // scanning to the right of our scanNodePoint as we check the bottom right adjacent node, middle right adjacent node, and upper right adjacent node
                for (int z = 0; z < 3; z++)
                {

                    if (scanNodePoint.y < GridGraphGenerate.gg.Depth - 1 && scanNodePoint.y != 0f)
                        currentNodeBeingVetted = GridGraphGenerate.gg.nodes[(z - 1 + (int)(scanNodePoint.y)) * GridGraphGenerate.gg.width + (int)(scanNodePoint.x + 1)];
                    else if ((scanNodePoint.y == GridGraphGenerate.gg.Depth - 1 || scanNodePoint.y == 0f) && z == 0)
                        currentNodeBeingVetted = GridGraphGenerate.gg.nodes[(int)(scanNodePoint.x + 1)];
                    else if (scanNodePoint.y == GridGraphGenerate.gg.Depth - 1 && z > 0)
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

                if (startCounterPastNode != null && startCounterPastNode == currentNodeBeingVetted)
                    counterPaused = false;

                if (!counterPaused)
                    counterIndex++;
            }

        return adjNodes;
    }

    // takes a vector position and returns this as cooridantes on the Grid Graph because the Grid Graph is essentially a grid of nodes that represent what the Ai can see
    public static Vector2 TurnPositionIntoPointOnGridGraph(GridGraph gg, Vector3 position)
    {
        int x = (int)Helper.Remap(position.x,
            -gg.width * 0.5f * gg.nodeSize + gg.center.x,
            gg.width * 0.5f * gg.nodeSize + gg.center.x,
            0,
            gg.width);

        int depth = (int)Helper.Remap(
            position.y,
            -gg.depth * 0.5f * gg.nodeSize + gg.center.y,
            gg.depth * 0.5f * gg.nodeSize + gg.center.y,
            0,
            gg.depth);

        Vector2 vector = new Vector2(x, depth);

        return vector;
    }

    // takes a node and returns this as cooridantes on the Grid Graph because the Grid Graph is essentially a grid of nodes that represent what the Ai can see
    public static Vector2 TurnPositionIntoPointOnGridGraph(GridGraph gg, GraphNode node)
    {
        Vector3 position = (Vector3)node.position;

        int x = (int)Helper.Remap(position.x, 
            -gg.width * 0.5f * gg.nodeSize + gg.center.x, 
            gg.width * 0.5f * gg.nodeSize + gg.center.x, 
            0, 
            gg.width);

        int depth = (int)Helper.Remap(
            position.y, 
            -gg.depth * 0.5f * gg.nodeSize + gg.center.y,
            gg.depth * 0.5f * gg.nodeSize + gg.center.y, 
            0, 
            gg.depth);

        Vector2 vector = new Vector2(x, depth);

        return vector;
    }

    // http://members.chello.at/easyfilter/bresenham.html

    // This specific code was acquired from...
    // http://members.chello.at/easyfilter/bresenham.html
    // So i do not take credit for this, this a just a 3rd party tool that assissted me in writing this program

    // BresenhamLine is a line drawing algorithm that allows me to define a start and end position of a line apart of a grid, and determine all the 
    // connecting squares to form a line from the start and end position. We can use this especially for our GridGraph that is essentially a grid of nodes that represent what the Ai can see
    public static List<GraphNode> BresenhamLine(GridGraph gg, Vector2 start, Vector2 end)
    {
        List<Vector2> nodePositionsOnGrid = new List<Vector2>();
        List<GraphNode> nodes = new List<GraphNode>();

        Vector2 point0 = TurnPositionIntoPointOnGridGraph(gg, start);
        Vector2 point1 = TurnPositionIntoPointOnGridGraph(gg, end);

        int dx = (int)Mathf.Abs(point1.x - point0.x);
        int Sx = (point0.x < point1.x) ? 1 : -1;
        int dy = (int)-Mathf.Abs(point1.y - point0.y);
        int Sy = (point0.y < point1.y) ? 1 : -1;

        int err = dx + dy;
        int e2;

        for (; ; )
        {
            nodePositionsOnGrid.Add(new Vector2(point0.x, point0.y));
            if (point0.x == point1.x && point0.y == point1.y) break;
            e2 = 2 * err;
            if (e2 >= dy) { err += dy; point0.x += Sx; } /* e_xy+e_x > 0 */
            if (e2 <= dx) { err += dx; point0.y += Sy; } /* e_xy+e_y < 0 */
        }

        foreach (Vector2 position in nodePositionsOnGrid)
        {
            nodes.Add(gg.nodes[(int)position.y * gg.width + (int)position.x]);
        }

        return nodes;
    }

    // Custom function I created that loops to a set of points and applies the BresenhamLine to each point of points as 
    // now instead of a start and end position, we are handling a chain of position that could represent a path
    public static List<GraphNode> BresenhamLineLoopThrough(GridGraph gg, List<Vector2> points)
    {
        List<GraphNode> nodes = new List<GraphNode>();

        int count = points.Count;
        if (count < 2) return nodes;

        for(int index=0; index<count-1; index++)
        {
            List<GraphNode> temp = BresenhamLine(GridGraphGenerate.gg, points[index], points[index + 1]);
            Debug.DrawLine(points[index], points[index + 1], Color.red, 1f);
            for (int nodeIndex=0; nodeIndex<temp.Count; nodeIndex++)
            {
                if (!nodes.Contains(temp[nodeIndex])) nodes.Add(temp[nodeIndex]);
            }
        }

        return nodes;
    }
    
    // seraches downwards in order to find the next groundNode from a point. If we do find a groundNode within a certain number of nodes
    // that is defined by "distanceInNodes" we will return true, else, we will return false
    // the returnedNode also represents the groundNode that we hit if we return true, else we will return the last node we searched if we return false
    public static bool SearchInDirection(GridGraph gg, GraphNode point, int distanceInNodes, ref GraphNode returnedNode)
    {
        Vector2 pointPosition = TurnPositionIntoPointOnGridGraph(gg, (Vector3)point.position);
        GraphNode currentNodeBeingVetted = null;

        for (int z = 0; z < distanceInNodes; z++)
        {
            if (0 >= (-z - 1 + (int)pointPosition.y) * gg.width + ((int)pointPosition.x)) return true;
            returnedNode = gg.nodes[(-z - 1 + (int)pointPosition.y) * gg.width + ((int)pointPosition.x)];

            currentNodeBeingVetted = gg.nodes[(-z - 1 + (int)pointPosition.y) * gg.width + ((int)pointPosition.x)];
            if (currentNodeBeingVetted.Walkable && currentNodeBeingVetted.Penalty == GridGraphGenerate.lowPenalty) return true;
        }

        returnedNode = gg.nodes[(-distanceInNodes - 1 + (int)pointPosition.y) * gg.width + ((int)pointPosition.x)];
        return false;
    }
    
    // Loops through a vector path that is esstiantlly a list of points and compares each pair or conseqcutive points in order
    // to deduce if they "heading" in a uniform direction defined by "Vector2 direction" and returns true if that is the case. Else,
    // we will be returning false
    public static bool CheckDirectionOfPathInSequence(List<Vector2> vectorPath, Vector2 direction, int sequenceCap)
    {
        if (vectorPath.Count < sequenceCap) return false;

        for (int index = sequenceCap; index < vectorPath.Count; index++)
        {
            for (int i = 1; i < sequenceCap; i++)
            {
                if ((vectorPath[index] - vectorPath[index - 1]).normalized != direction) return false;
            }
        }

        return true;
    }

    // This specific code was acquired from...
    // AnomalusUndrdog & Nikolay-Lezhnev https://forum.unity.com/threads/debug-drawarrow.85980/
    // So i do not take credit for this, this a just a 3rd party tool that assissted me in writing this program
    // especially when it came to debugging elements in my code
    public static class DrawArrow
    {
        public static void ForGizmo(Vector3 pos, Vector3 direction, float arrowHeadLength = 0.25f, float arrowHeadAngle = 20.0f)
        {
            Gizmos.DrawRay(pos, direction);
            DrawArrowEnd(true, pos, direction, Gizmos.color, arrowHeadLength, arrowHeadAngle);
        }

        public static void ForGizmo(Vector3 pos, Vector3 direction, Color color, float arrowHeadLength = 0.25f, float arrowHeadAngle = 20.0f)
        {
            Gizmos.color = color;
            Gizmos.DrawRay(pos, direction);
            DrawArrowEnd(true, pos, direction, color, arrowHeadLength, arrowHeadAngle);
        }

        public static void ForDebug(Vector3 pos, Vector3 direction, float arrowHeadLength = 0.25f, float arrowHeadAngle = 20.0f)
        {
            Debug.DrawRay(pos, direction);
            DrawArrowEnd(false, pos, direction, Gizmos.color, arrowHeadLength, arrowHeadAngle);
        }

        public static void ForDebug(Vector3 pos, Vector3 direction, Color color, float arrowHeadLength = 0.25f, float arrowHeadAngle = 20.0f)
        {
            Debug.DrawRay(pos, direction, color);
            DrawArrowEnd(false, pos, direction, color, arrowHeadLength, arrowHeadAngle);
        }

        private static void DrawArrowEnd(bool gizmos, Vector3 pos, Vector3 direction, Color color, float arrowHeadLength = 0.25f, float arrowHeadAngle = 20.0f)
        {
            Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(arrowHeadAngle, 0, 0) * Vector3.back;
            Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(-arrowHeadAngle, 0, 0) * Vector3.back;
            Vector3 up = Quaternion.LookRotation(direction) * Quaternion.Euler(0, arrowHeadAngle, 0) * Vector3.back;
            Vector3 down = Quaternion.LookRotation(direction) * Quaternion.Euler(0, -arrowHeadAngle, 0) * Vector3.back;
            if (gizmos)
            {
                Gizmos.color = color;
                Gizmos.DrawRay(pos + direction, right * arrowHeadLength);
                Gizmos.DrawRay(pos + direction, left * arrowHeadLength);
                Gizmos.DrawRay(pos + direction, up * arrowHeadLength);
                Gizmos.DrawRay(pos + direction, down * arrowHeadLength);
            }
            else
            {
                Debug.DrawRay(pos + direction, right * arrowHeadLength, color);
                Debug.DrawRay(pos + direction, left * arrowHeadLength, color);
                Debug.DrawRay(pos + direction, up * arrowHeadLength, color);
                Debug.DrawRay(pos + direction, down * arrowHeadLength, color);
            }
        }

        public static void ForDebugTimed(Vector3 pos, Vector3 direction, Color color, float time = 0.5f, float arrowHeadLength = 0.25f, float arrowHeadAngle = 20.0f)
        {
            Debug.DrawRay(pos, direction, color, time);
            DrawArrowEndTimed(false, pos, direction, color, time, arrowHeadLength, arrowHeadAngle);
        }

        private static void DrawArrowEndTimed(bool gizmos, Vector3 pos, Vector3 direction, Color color, float time = 0.5f, float arrowHeadLength = 0.25f, float arrowHeadAngle = 20.0f)
        {
            Vector3 right = Quaternion.LookRotation(direction) * Quaternion.Euler(arrowHeadAngle, 0, 0) * Vector3.back;
            Vector3 left = Quaternion.LookRotation(direction) * Quaternion.Euler(-arrowHeadAngle, 0, 0) * Vector3.back;
            Vector3 up = Quaternion.LookRotation(direction) * Quaternion.Euler(0, arrowHeadAngle, 0) * Vector3.back;
            Vector3 down = Quaternion.LookRotation(direction) * Quaternion.Euler(0, -arrowHeadAngle, 0) * Vector3.back;

            Debug.DrawRay(pos + direction, right * arrowHeadLength, color, time);
            Debug.DrawRay(pos + direction, left * arrowHeadLength, color, time);
            Debug.DrawRay(pos + direction, up * arrowHeadLength, color, time);
            Debug.DrawRay(pos + direction, down * arrowHeadLength, color, time);
        }
    }

    // This specific code was acquired from...
    // AnomalusUndrdog & Nikolay-Lezhnev https://forum.unity.com/threads/debug-drawarrow.85980/
    // So i do not take credit for this, this a just a 3rd party tool that assissted me in writing this program
    // especially when it came to debugging elements in my code
    public static class DrawCapsule
    {
#if UNITY_EDITOR // we have to make sure to not compile this code if we are building our code for a release since this can cause some compiler errors
        public static void DrawWireCapsule(Vector3 _pos, Quaternion _rot, float _radius, float _height, Color _color = default(Color))
        {
            if (_color != default(Color))
                Handles.color = _color;
            Matrix4x4 angleMatrix = Matrix4x4.TRS(_pos, _rot, Handles.matrix.lossyScale);
            using (new Handles.DrawingScope(angleMatrix))
            {
                var pointOffset = (_height - (_radius * 2)) / 2;

                //draw sideways
                Handles.DrawWireArc(Vector3.up * pointOffset, Vector3.left, Vector3.back, -180, _radius);
                Handles.DrawLine(new Vector3(0, pointOffset, -_radius), new Vector3(0, -pointOffset, -_radius));
                Handles.DrawLine(new Vector3(0, pointOffset, _radius), new Vector3(0, -pointOffset, _radius));
                Handles.DrawWireArc(Vector3.down * pointOffset, Vector3.left, Vector3.back, 180, _radius);
                //draw frontways
                Handles.DrawWireArc(Vector3.up * pointOffset, Vector3.back, Vector3.left, 180, _radius);
                Handles.DrawLine(new Vector3(-_radius, pointOffset, 0), new Vector3(-_radius, -pointOffset, 0));
                Handles.DrawLine(new Vector3(_radius, pointOffset, 0), new Vector3(_radius, -pointOffset, 0));
                Handles.DrawWireArc(Vector3.down * pointOffset, Vector3.back, Vector3.left, -180, _radius);
                //draw center
                Handles.DrawWireDisc(Vector3.up * pointOffset, Vector3.up, _radius);
                Handles.DrawWireDisc(Vector3.down * pointOffset, Vector3.up, _radius);

            }
        }
#endif

        public static void ForGizmo(Vector3 pos, Vector3 rotation)
        {
            // ...
        }
    }
}
