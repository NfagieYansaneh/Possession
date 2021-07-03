using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

public class ImpulseHandler : MonoBehaviour
{
    public CinemachineImpulseSource source;

    // Start is called before the first frame update
    private void Awake()
    {
        // source = GetComponent<CinemachineImpulseSource>();
    }

    public void Shake(float force)
    {
        // implement force tiers later
        source.GenerateImpulse();
    }
}
