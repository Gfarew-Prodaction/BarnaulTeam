using HurricaneVR.Framework.Core;
using HurricaneVR.Framework.Core.Grabbers;
using UnityEngine;

namespace InteracrSystem
{
    // Match HurricaneVR's current candidate and its grab rules, not merely a previous hover event.
    public static class ElementGrabAvailability
    {
        public static bool CanHighlight(HVRGrabberBase grabber, HVRGrabbable item)
        {
            if (!grabber || !grabber.isActiveAndEnabled || !item || !item.isActiveAndEnabled ||
                grabber.HoverTarget != item || !grabber.AllowHovering || !grabber.AllowGrabbing ||
                item.IsHandGrabbed || item.IsBeingForcedGrabbed)
                return false;
            if (grabber is HVRForceGrabber force)
            {
                if (!force.HandGrabber || !force.HandGrabber.isActiveAndEnabled ||
                    !force.HandGrabber.AllowGrabbing || !force.Inputs || !force.Inputs.CanDistanceGrab)
                    return false;
            }
            else if (!(grabber is HVRHandGrabber)) return false;
            return grabber.CanHover(item) && grabber.CanGrab(item);
        }
    }
}
