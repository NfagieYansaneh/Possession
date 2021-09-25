using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/* TestScript purpose was just to experiment with particle systems that were ultiamtely scrapped very early into the project */

public class TestScript : MonoBehaviour
{
    public ParticleSystem particleSys;

    public void Awake()
    {
        particleSys = GetComponent<ParticleSystem>();
        particleSys.Play();
    }
}
