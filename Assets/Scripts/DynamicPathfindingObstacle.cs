using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;

public class DynamicPathfindingObstacle : MonoBehaviour
{
    BoxCollider2D boxCollider2D;

    private void Start()
    {
        boxCollider2D = GetComponent<BoxCollider2D>();
    }

    // Update is called once per frame
    void Update()
    {
        Bounds bounds = boxCollider2D.bounds;
        AstarPath.active.UpdateGraphs(bounds);
    }
}
