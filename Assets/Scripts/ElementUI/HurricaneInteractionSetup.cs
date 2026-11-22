using HurricaneVR.Framework.Core.Grabbers;
using UnityEngine;

namespace InteracrSystem
{
    // Scene-level choice of HurricaneVR's existing distance-grab mode.
    public class HurricaneInteractionSetup : MonoBehaviour
    {
        private void Start()
        {
            foreach (var force in FindObjectsByType<HVRForceGrabber>())
            {
                force.GrabStyle = HVRForceGrabMode.ForcePull;
                force.RequiresFlick = false;
            }
        }
    }
}
