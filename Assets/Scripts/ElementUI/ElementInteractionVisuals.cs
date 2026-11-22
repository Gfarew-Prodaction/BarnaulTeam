using System.Collections.Generic;
using HurricaneVR.Framework.Core;
using HurricaneVR.Framework.Core.Grabbers;
using UnityEngine;

namespace InteracrSystem
{
    [RequireComponent(typeof(HVRGrabbable))]
    public class ElementInteractionVisuals : MonoBehaviour
    {
        [SerializeField] private string _groupId;
        [SerializeField] private Transform _scope;
        [SerializeField] private GameObject _ghost;
        [SerializeField] private Transform _homeFrame;
        public bool ShowRelatedHighlight { get; set; } = true;
        [SerializeField] private Color _hoverColor = new Color(.12f, 1f, .28f, 1);
        [SerializeField] private Color _relatedColor = new Color(1f, .72f, .1f, 1);
        private static readonly HashSet<ElementInteractionVisuals> Active = new HashSet<ElementInteractionVisuals>();
        private readonly HashSet<HVRGrabberBase> _hoverers = new HashSet<HVRGrabberBase>();
        private HVRGrabbable _item;
        private Renderer[] _renderers;
        private MaterialPropertyBlock[] _original;
        private MaterialPropertyBlock _tint;
        private Vector3 _homePosition;
        private Quaternion _homeRotation;
        private int _state = -1;
        public bool CatalogSelected { get; set; }
        public bool IsRelatedHighlighted { get; private set; }
        public bool IsHoverHighlighted { get; private set; }
        public GameObject Ghost => _ghost;
        public string GroupId => _groupId;
        public void Configure(string groupId, Transform scope, GameObject ghost, Transform homeFrame)
        { _groupId = groupId; _scope = scope; _ghost = ghost; _homeFrame = homeFrame; }
        private void Awake()
        {
            _item = GetComponent<HVRGrabbable>();
            _renderers = GetComponentsInChildren<Renderer>(true);
            _original = new MaterialPropertyBlock[_renderers.Length];
            for (int i = 0; i < _renderers.Length; i++) { _original[i] = new MaterialPropertyBlock(); _renderers[i].GetPropertyBlock(_original[i]); }
            _tint = new MaterialPropertyBlock();
            _homePosition = _homeFrame ? _homeFrame.InverseTransformPoint(transform.position) : transform.position;
            _homeRotation = _homeFrame ? Quaternion.Inverse(_homeFrame.rotation) * transform.rotation : transform.rotation;
        }
        private void OnEnable()
        {
            Active.Add(this); _state = -1;
            _item.HoverEnter.AddListener(HoverEnter); _item.HoverExit.AddListener(HoverExit);
        }
        private void HoverEnter(HVRGrabberBase hand, HVRGrabbable item)
        { if (hand is HVRHandGrabber || hand is HVRForceGrabber) _hoverers.Add(hand); }
        private void HoverExit(HVRGrabberBase hand, HVRGrabbable item) => _hoverers.Remove(hand);
        private void LateUpdate()
        {
            _hoverers.RemoveWhere(h => !h || !h.isActiveAndEnabled || h.HoverTarget != _item);
            IsHoverHighlighted = false;
            foreach (var grabber in _hoverers)
                if (ElementGrabAvailability.CanHighlight(grabber, _item)) { IsHoverHighlighted = true; break; }
            IsRelatedHighlighted = false;
            if (ShowRelatedHighlight && !string.IsNullOrWhiteSpace(_groupId)) foreach (var other in Active)
                if (other && other != this && other._scope == _scope && other._groupId == _groupId && other._item.IsHandGrabbed)
                { IsRelatedHighlighted = true; break; }
            int state = IsHoverHighlighted ? 1 : CatalogSelected ? 3 : IsRelatedHighlighted ? 2 : 0;
            if (state != _state)
            {
                _state = state;
                for (int i = 0; i < _renderers.Length; i++)
                {
                    if (!_renderers[i]) continue;
                    if (state == 0) _renderers[i].SetPropertyBlock(_original[i]);
                    else
                    {
                        _renderers[i].GetPropertyBlock(_tint);
                        var color = state == 1 ? _hoverColor : state == 3 ? new Color(.05f,.8f,1,1) : _relatedColor;
                        _tint.SetColor("_BaseColor", color);
                        _tint.SetColor("_Color", color);
                        _renderers[i].SetPropertyBlock(_tint);
                    }
                }
            }
            if (_ghost)
            {
                var position = _homeFrame ? _homeFrame.TransformPoint(_homePosition) : _homePosition;
                var rotation = _homeFrame ? _homeFrame.rotation * _homeRotation : _homeRotation;
                bool away = _item.IsBeingHeld || _item.IsBeingForcedGrabbed || Vector3.Distance(transform.position, position) > .006f || Quaternion.Angle(transform.rotation, rotation) > 3;
                if (_ghost.activeSelf != away) _ghost.SetActive(away);
            }
        }
        private void OnDisable()
        {
            Active.Remove(this); _hoverers.Clear();
            if (_item) { _item.HoverEnter.RemoveListener(HoverEnter); _item.HoverExit.RemoveListener(HoverExit); }
            if (_renderers != null) for(int i=0;i<_renderers.Length;i++) if(_renderers[i]) _renderers[i].SetPropertyBlock(_original[i]);
            if (_ghost) _ghost.SetActive(false);
            IsRelatedHighlighted = IsHoverHighlighted = false;
            CatalogSelected = false;
        }
    }
}
