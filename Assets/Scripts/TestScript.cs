using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestScript : MonoBehaviour
{
    public ParticleSystem particleSys;

    public void Awake()
    {
        particleSys = GetComponent<ParticleSystem>();
        particleSys.Play();
    }
}
