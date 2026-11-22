using System.Collections;
using DG.Tweening;
using HurricaneVR.Framework.Core;
using HurricaneVR.Framework.Core.Grabbers;
using InteracrSystem;
using UnityEngine;

[RequireComponent(typeof(ReturningGrabbable), typeof(Rigidbody), typeof(ElementDog))]
public class PracticePiece : MonoBehaviour
{
    [SerializeField] private PracticeSet _category;
    [SerializeField] private PracticeSlot _slot;
    [SerializeField] private Vector3 _sourcePosition;
    [SerializeField] private Quaternion _sourceRotation = Quaternion.identity;
    [SerializeField] private Vector3 _partScale = Vector3.one;
    private HVRGrabbable _grab;
    private Rigidbody _body;
    private Collider[] _colliders;
    private bool[] _triggers;
    private Tween _movement;
    private Coroutine _release;
    private PracticeSession _session;
    public PracticeSet Category => _category;
    public PracticeSlot Slot => _slot;
    public bool IsPlaced { get; private set; }
    public bool IsReference { get; private set; }
    public bool IsAttempted { get; private set; }
    public bool IsSubmitted => IsReference || IsPlaced || IsAttempted;
    public bool IsHeld => _grab && (_grab.IsHandGrabbed || _grab.IsBeingHeld || _grab.IsBeingForcedGrabbed);
    public bool IsMoving => _movement != null && _movement.IsActive();
    public Vector3 SourcePosition => _sourcePosition;
    public Quaternion SourceRotation => _sourceRotation;
    public string DisplayName => GetComponent<ElementDog>().Data?.name ?? name;
    public void Configure(PracticeSet category, PracticeSlot slot) { _category = category; _slot = slot; }
    private void Awake()
    {
        _grab = GetComponent<HVRGrabbable>(); _body = GetComponent<Rigidbody>();
        _colliders = GetComponentsInChildren<Collider>(); _triggers = new bool[_colliders.Length];
        for (int i=0;i<_colliders.Length;i++) _triggers[i] = _colliders[i].isTrigger;
    }
    public void Initialize(PracticeSession session, Vector3 sourcePosition, Quaternion sourceRotation, Vector3 scale)
    {
        CancelMovement(); _session = session; IsPlaced = false; IsAttempted = false; IsReference = false; _grab.CanBeGrabbed = true;
        _sourcePosition = sourcePosition; _sourceRotation = sourceRotation; _partScale = scale;
        transform.SetParent(session.PartsRoot, true); transform.SetPositionAndRotation(sourcePosition, sourceRotation); transform.localScale = scale;
        RestoreTriggers(); Park();
    }
    public void PrepareForGrab()
    {
        if (IsReference) return;
        CancelMovement();
        if(IsAttempted){IsAttempted=false;_session?.AssemblyChanged();}
        if (IsPlaced) { IsPlaced = false; _session?.Removed(this); }
        transform.SetParent(null, true); RestoreTriggers();
        _body.isKinematic = false; _body.useGravity = false; _body.WakeUp();
    }
    public void InstallReference()
    {
        CancelMovement(); Park();
        transform.SetParent(_slot.transform,false); transform.localPosition=Vector3.zero; transform.localRotation=Quaternion.identity; transform.localScale=Vector3.one;
        foreach(var c in _colliders) if(c)c.isTrigger=true;
        IsReference = IsPlaced = true; _grab.CanBeGrabbed = false;
        Physics.SyncTransforms();
    }
    public void OnReleasedItem(HVRGrabberBase hand, HVRGrabbable item)
    {
        if (!isActiveAndEnabled || item != _grab) return;
        if (_release != null) StopCoroutine(_release);
        _release = StartCoroutine(ReleasedWhenFree());
    }
    private IEnumerator ReleasedWhenFree()
    {
        yield return null; _release = null;
        if (IsHeld) yield break;
        TryPlaceOrReturn();
    }
    public bool TryPlaceOrReturn()
    {
        if (!_session || IsHeld || IsPlaced) return false;
        if(_category==PracticeSet.Brain && _session.IsInAssemblyArea(transform.position))
        {
            Park();transform.SetParent(_session.AssemblyRoot,true);IsAttempted=true;IsPlaced=false;
            foreach(var collider in _colliders)if(collider)collider.isTrigger=true;
            Physics.SyncTransforms();_session.Attempted(this);return true;
        }
        bool position = Vector3.Distance(transform.position, _slot.transform.position) <= _session.PositionTolerance;
        bool rotation = Quaternion.Angle(transform.rotation, _slot.transform.rotation) <= _session.RotationTolerance;
        if (position && rotation && _session.IsNearestTarget(this, transform.position))
        {
            Move(true); return true;
        }
        _session.Rejected(this); Move(false); return false;
    }
    public BrainAssemblyState EvaluateBrainPlacement()
    {
        if(!IsSubmitted)return BrainAssemblyState.Missing;
        bool correct=Vector3.Distance(transform.position,_slot.transform.position)<=_session.PositionTolerance && Quaternion.Angle(transform.rotation,_slot.transform.rotation)<=_session.RotationTolerance && _session.IsNearestTarget(this,transform.position);
        return correct?BrainAssemblyState.Correct:BrainAssemblyState.Misplaced;
    }
    public void ApplyBrainCheck(BrainAssemblyState state)
    {IsPlaced=state==BrainAssemblyState.Correct;}
    private void Move(bool intoSlot)
    {
        _movement?.Kill(); Park();
        Vector3 from = transform.position; Quaternion rotation = transform.rotation; Vector3 scale = transform.lossyScale;
        transform.SetParent(null, true);
        _movement = DOVirtual.Float(0,1,.32f,t =>
        {
            if (!this) return;
            var position = intoSlot ? _slot.transform.position : _sourcePosition;
            var targetRotation = intoSlot ? _slot.transform.rotation : _sourceRotation;
            var targetScale = intoSlot ? _slot.transform.lossyScale : _partScale;
            transform.SetPositionAndRotation(Vector3.Lerp(from,position,t),Quaternion.Slerp(rotation,targetRotation,t));
            transform.localScale = Vector3.Lerp(scale,targetScale,t);
        }).SetEase(Ease.InOutSine).OnComplete(() =>
        {
            _movement = null;
            if (intoSlot)
            {
                transform.SetParent(_slot.transform,false); transform.localPosition=Vector3.zero;transform.localRotation=Quaternion.identity;transform.localScale=Vector3.one;
                // Installed parts remain selectable while allowing inner structures to pass through them.
                foreach(var c in _colliders) if(c)c.isTrigger=true;
                IsPlaced = true; _session.Placed(this);
            }
            else { transform.SetParent(_session.PartsRoot,true);transform.SetPositionAndRotation(_sourcePosition,_sourceRotation);transform.localScale=_partScale; }
            Park(); Physics.SyncTransforms();
        });
    }
    private void Park()
    {
        if (!_body.isKinematic) { _body.linearVelocity=Vector3.zero;_body.angularVelocity=Vector3.zero; }
        _body.useGravity=false;_body.collisionDetectionMode=CollisionDetectionMode.Discrete;_body.isKinematic=true;
    }
    private void RestoreTriggers() { for(int i=0;i<_colliders.Length;i++)if(_colliders[i])_colliders[i].isTrigger=_triggers[i]; }
    private void CancelMovement() { _movement?.Kill();_movement=null;if(_release!=null){StopCoroutine(_release);_release=null;} }
    private void OnDisable(){CancelMovement();}
}
