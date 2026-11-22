using HurricaneVR.Framework.Core;
using HurricaneVR.Framework.Core.Grabbers;

namespace InteracrSystem
{
    // These hooks also run when HurricaneVR suppresses public UnityEvents during a transfer.
    public class ReturningGrabbable : HVRGrabbable
    {
        protected override void OnBeforeGrabbed(HVRGrabberBase grabber)
        {
            if (TryGetComponent<PracticePiece>(out var practice)) practice.PrepareForGrab();
            else if (TryGetComponent<OnMoveBackItem>(out var movement)) movement.PrepareForGrab();
            base.OnBeforeGrabbed(grabber);
        }
        protected override void OnReleased(HVRGrabberBase grabber)
        {
            base.OnReleased(grabber);
            if (TryGetComponent<PracticePiece>(out var practice)) practice.OnReleasedItem(grabber, this);
            else if (TryGetComponent<OnMoveBackItem>(out var movement)) movement.OnReleasedItem(grabber, this);
        }
        protected override void OnGrabCanceled(HVRGrabberBase grabber)
        {
            base.OnGrabCanceled(grabber);
            if (TryGetComponent<PracticePiece>(out var practice)) practice.OnReleasedItem(grabber, this);
            else if (TryGetComponent<OnMoveBackItem>(out var movement)) movement.OnReleasedItem(grabber, this);
        }
    }
}
