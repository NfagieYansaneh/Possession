using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Pathfinding;

public class BaseAiController : MonoBehaviour
{
    public Transform targetPosition;
    public BaseCharacterController baseCharacterController;
    Seeker seeker;
    bool calculatePathing = false;

    // Start is called before the first frame update
    void Start()
    {
        seeker = GetComponent<Seeker>();
        baseCharacterController = GetComponent<BaseCharacterController>();
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(targetPosition.position, 0.2f);
    }

    private void FixedUpdate()
    {
        if (calculatePathing)
        {
            seeker.StartPath(transform.position, targetPosition.position, OnPathComplete);
        }
    }

    // Update is called once per frame
    private void OnGUI()
    {
        calculatePathing = GUI.Toggle(new Rect(500, 20, 135, 80), calculatePathing, new GUIContent("Calculate Pathing"));
    }

    public void OnPathComplete(Path p)
    {
        Debug.Log("Yay, we got a path back. Did it have an error? " + p.error);
    }

}
