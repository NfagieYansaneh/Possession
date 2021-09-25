using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Cinemachine;

/* ImpulseHandler purpose is to just form screen shakes and possible controller vibrations, however, I have not incorporated controller vibrations
 * into ImpulseHandler as of now
 */

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
        // implements a screen shake, "force" provides no functionality, but if there is a demand, I can make the screen shake
        // proportional to the "force" by just using "source.GenerateImpulse(force);"
        source.GenerateImpulse();
    }
}
