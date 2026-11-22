using System.Collections;
using DG.Tweening;
using HurricaneVR.Framework.Core;
using HurricaneVR.Framework.Core.Grabbers;
using UnityEngine;
using InteracrSystem;

[RequireComponent(typeof(ReturningGrabbable), typeof(Rigidbody))]
public class OnMoveBackItem : MonoBehaviour
{
    [SerializeField, Min(.01f)] private float _duration = .5f;
    [SerializeField] private Transform _returnFrame;
    private Vector3 _framePosition;
    private Quaternion _frameRotation;
    private Vector3 _frameScale;
    public void SetReturnFrame(Transform frame) => _returnFrame = frame;
    private HVRGrabbable _item;
    private Rigidbody _body;
    private Vector3 _startPosition;
    private Quaternion _startRotation;
    private RigidbodyConstraints _heldConstraints;
    private bool _heldGravity;
    private Tween _return;
    private Coroutine _releaseCheck;
    private ElementTray _tray;
    private ElementInspectionStation _station;
    private bool _started;

    private void Awake()
    {
        _item = GetComponent<HVRGrabbable>();
        _body = GetComponent<Rigidbody>();
        // Capture once for this scene instance; grabs and returns never change this pose.
        _startPosition = _body.position;
        _startRotation = _body.rotation;
        if (_returnFrame)
        {
            _framePosition = _returnFrame.InverseTransformPoint(_startPosition);
            _frameRotation = Quaternion.Inverse(_returnFrame.rotation) * _startRotation;
            _frameScale = transform.localScale;
        }
        _heldConstraints = _body.constraints;
        _heldGravity = _body.useGravity;
    }
    private void Start()
    {
        _started = true;
        if (!_item.IsBeingHeld) Park();
    }
    private void OnEnable() { if (_started && _body && _item && !_item.IsBeingHeld) Park(); }
    public void PrepareForGrab()
    {
        CancelReturn();
        if (_tray) _tray.Release(this);
        _tray = null;
        if (_station) _station.Release(this);
        _station = null;
        _body.isKinematic = false;
        _body.constraints = _heldConstraints;
        _body.useGravity = _heldGravity;
        _body.WakeUp();
        if (_returnFrame) transform.SetParent(null, true);
    }

    // Kept public so existing Inspector event bindings remain compatible.
    public void OnReleasedItem(HVRGrabberBase grabber, HVRGrabbable grabbable)
    {
        if (!isActiveAndEnabled || grabbable != _item) return;
        if (_releaseCheck != null) StopCoroutine(_releaseCheck);
        _releaseCheck = StartCoroutine(ReturnWhenFree());
    }
    private IEnumerator ReturnWhenFree()
    {
        // HurricaneVR may release one grabber before attaching another during a hand swap/force pull.
        yield return null;
        _releaseCheck = null;
        if (_item.IsBeingHeld || _item.IsBeingForcedGrabbed) yield break;
        BeginReturn();
    }
    private void BeginReturn()
    {
        _return?.Kill();
        Park();
        if (_tray) _tray.Release(this);
        _tray = null;
        if (_station) _station.Release(this);
        _station = null;
        Vector3 destination = _returnFrame ? _returnFrame.TransformPoint(_framePosition) : _startPosition;
        Quaternion destinationRotation = _returnFrame ? _returnFrame.rotation * _frameRotation : _startRotation;
        bool returningHome = true;
        if (ElementInspectionStation.TryReserve(this, out _station, out var stationPosition, out var stationRotation))
        { destination = stationPosition; destinationRotation = stationRotation; returningHome = false; }
        else if (ElementTray.TryReserveNearest(this, out _tray, out var trayPosition, out var trayRotation))
        { destination = trayPosition; destinationRotation = trayRotation; returningHome = false; }
        Vector3 from = _body.position;
        Quaternion rotation = _body.rotation;
        Vector3 fromScale = transform.localScale;
        _return = DOVirtual.Float(0, 1, _duration, t =>
        {
            if (!_body) return;
            if (returningHome && _returnFrame) { destination = _returnFrame.TransformPoint(_framePosition); destinationRotation = _returnFrame.rotation * _frameRotation; }
            if (returningHome && _returnFrame)
                transform.localScale = Vector3.LerpUnclamped(fromScale, Vector3.Scale(_returnFrame.lossyScale, _frameScale), t);
            _body.position = Vector3.LerpUnclamped(from, destination, t);
            _body.rotation = Quaternion.SlerpUnclamped(rotation, destinationRotation, t);
        }).SetEase(Ease.InOutSine).SetUpdate(UpdateType.Fixed)
          .OnComplete(() =>
          {
              _return = null;
              _body.position = destination;
              _body.rotation = destinationRotation;
              if (returningHome && _returnFrame) { transform.SetParent(_returnFrame, true); transform.localScale = _frameScale; }
              Park();
          });
    }
    private void Park()
    {
        // ReturningGrabbable restores dynamics before every grab, including silent transfers.
        if (!_body.isKinematic)
        {
            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;
        }
        _body.useGravity = false;
        _body.constraints = _heldConstraints;
        _body.collisionDetectionMode = CollisionDetectionMode.Discrete;
        _body.isKinematic = true;
        _body.Sleep();
    }
    private void CancelReturn()
    {
        if (_releaseCheck != null) { StopCoroutine(_releaseCheck); _releaseCheck = null; }
        _return?.Kill(); _return = null;
    }
    private void OnDisable()
    {
        CancelReturn();
        if (_tray) _tray.Release(this);
        _tray = null;
        if (_station) _station.Release(this);
        _station = null;
        if (_body)
        {
            _body.constraints = _heldConstraints;
            _body.isKinematic = false;
            _body.useGravity = _heldGravity;
        }
    }
}
