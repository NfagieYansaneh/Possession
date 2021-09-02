using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;

#if UNITY_EDITOR
using UnityEditor;
#endif

public static class Helper
{
    // Jessy from & RazaTech https://forum.unity.com/threads/re-map-a-number-from-one-range-to-another.119437/
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
    // ??? doesn't work and is not as performan as BresenhamLine nor do i need the percesion of this
    /*public static List<GraphNode> BresenhamBézierCurve(GridGraph gg, Vector2 start, Vector2 end, Vector2 temp, float VyInitial, float VyFinal, float Vx)
    {
        List<Vector2> nodePositionsOnGrid = new List<Vector2>();
        List<GraphNode> nodes = new List<GraphNode>();

        // https://www.math.fsu.edu/~rabert/TeX/parabola/bezier1.gif & https://www.math.fsu.edu/~rabert/TeX/parabola/parabola.html & http://members.chello.at/easyfilter/bresenham.html
        Vector2 point0 = TurnPositionIntoPointOnGridGraph(gg, start);
        Vector2 point2 = TurnPositionIntoPointOnGridGraph(gg, end);

        // Vector2 temp = new Vector2(Vx* ((start.y - end.y) / (VyFinal - VyInitial)), VyInitial * ((start.y - end.y)/(VyFinal - VyInitial))+start.y);
        Vector2 point1 = TurnPositionIntoPointOnGridGraph(gg, temp);

        int Sx = (int)(point2.x - point1.x);
        int Sy = (int)(point2.y - point1.y);

        int Xx = (int)(point0.x - point1.x);
        int Yy = (int)(point0.y - point1.y);
        long Xy;

        double cur = Xx * Sy - Yy * Sx;
        double dx, dy, err;

        if (Xx * Sx <= 0 && Yy * Sy <= 0)
        {
            // Sign of gradient must not change
            return null;
        }

        if(Sx * Sx + Sy * Sy > Xx * Xx + Yy * Yy)
        {
            point2.x = point0.x;
            point0.x = Sx + point1.x;
            point2.y = point0.y;
            point0.y = Sy + point1.y;
            cur -= cur;
        }

        Debug.Log("Processing...");
        if (cur != 0)
        {
            Xx += Sx;
            Xx *= Sx = (point0.x < point2.x) ? 1 : -1;
            Yy *= Sy = (point0.y < point2.y) ? 1 : -1;

            Xy = 2 * Xx * Yy;
            Xx *= Xx;
            Yy *= Yy;
            if(cur*Sx*Sy < 0)
            {
                Xx = -Xx;
                Yy = -Yy;
                Xy = -Xy;
                cur -= cur;
            }

            dx = 4.0 * Sy * cur * (point1.x - point0.x) + Xx - Xy;
            dy = 4.0 * Sx * cur * (point0.y - point1.y) + Yy - Xy;
            Xx += Xx;
            Yy += Yy;
            err = dx + dy + Xy;
            Debug.Log("Processing again...");
            do
            {
                nodePositionsOnGrid.Add(new Vector2(point0.x, point0.y));
                if (point0.x == point2.x && point0.y == point2.y) break;
                // point1.y = 2 * err < dx;
                if (2*err > dy) { point0.x += Sx; dx -= Xy; err += dy += Yy; }
                if (2 * err < dx) { point0.y += Sy; dy -= Xy; err += dx += Xx; }

                Debug.Log("More processing...");
            } while (dy < dx);
        }

        foreach(Vector2 position in nodePositionsOnGrid)
        {
            nodes.Add(gg.nodes[(int)position.y * gg.width + (int)position.x]);
        }

        return nodes;
    }
    */

    // http://members.chello.at/easyfilter/bresenham.html
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

    public static List<GraphNode> BresenhamLineLoopThrough(GridGraph gg, List<Vector2> points)
    {
        List<GraphNode> nodes = new List<GraphNode>();

        int count = points.Count;
        if (count < 2) return nodes;

        for(int index=0; index<count-1; index++)
        {
            List<GraphNode> temp = BresenhamLine(GridGraphGenerate.gg, points[index], points[index + 1]);
            Debug.DrawLine(points[index], points[index + 1], Color.red, 1000f);
            for (int nodeIndex=0; nodeIndex<temp.Count; nodeIndex++)
            {
                // hopefully, this isn't a performance intensive process
                if (!nodes.Contains(temp[nodeIndex])) nodes.Add(temp[nodeIndex]);
            }
        }

        return nodes;
    }
    
    public static bool SearchInDirection(GridGraph gg, GraphNode point, int distanceInNodes, ref GraphNode returnedNode)
    {
        Vector2 pointPosition = TurnPositionIntoPointOnGridGraph(gg, (Vector3)point.position);
        GraphNode currentNodeBeingVetted = null;
        // GraphNode node = null;

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

    public static bool CheckDirectionOfPathInSequence(List<Vector2> vectorPath, Vector2 direction)
    {
        if (vectorPath.Count < 2) return false;

        for (int index = 1; index < vectorPath.Count - 1; index++)
        {
            Debug.Log("Calculated direction: " + (vectorPath[index] - vectorPath[index - 1]).normalized);
            Debug.DrawLine(vectorPath[index], vectorPath[index - 1], Color.magenta, 5f);
            if ((vectorPath[index] - vectorPath[index - 1]).normalized != direction) return false;
        }

        return true;
    }

    // AnomalusUndrdog & Nikolay-Lezhnev https://forum.unity.com/threads/debug-drawarrow.85980/
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

    // AnomalusUndrdog & Nikolay-Lezhnev https://forum.unity.com/threads/debug-drawarrow.85980/
    public static class DrawCapsule
    {
#if UNITY_EDITOR
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
            // just a dream...
            // not anymore!
        }
    }
}
