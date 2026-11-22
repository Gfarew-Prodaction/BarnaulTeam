using HurricaneVR.Framework.Core.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.EventSystems;

[RequireComponent(typeof(HVRUIPointer), typeof(LineRenderer))]
public class CrispHandRay : MonoBehaviour
{
    [SerializeField, Min(.001f)] private float _width = .004f;
    private HVRUIPointer _pointer;
    private LineRenderer _line;
    private Transform _dot;
    private int _mask;
    private bool _gated;
    private void Awake()
    {
        _pointer=GetComponent<HVRUIPointer>(); _line=GetComponent<LineRenderer>();
        _line.sharedMaterial=Resources.Load<Material>("UI/CrispPointer");
        _line.useWorldSpace=true; _line.positionCount=2; _line.alignment=LineAlignment.View;
        _line.widthCurve=AnimationCurve.Constant(0,1,1); _line.widthMultiplier=_width;
        _line.colorGradient = new Gradient {
            colorKeys = new[] { new GradientColorKey(Color.white,0), new GradientColorKey(Color.white,1) },
            alphaKeys = new[] { new GradientAlphaKey(1,0), new GradientAlphaKey(1,1) }
        };
        _line.numCapVertices=4; _line.numCornerVertices=0;
        _line.shadowCastingMode=ShadowCastingMode.Off; _line.receiveShadows=false; _line.generateLightingData=false;
        _mask=~LayerMask.GetMask("Player","Hand","DynamicPose","Ignore Raycast");
        var dot=GameObject.CreatePrimitive(PrimitiveType.Sphere); dot.name="Pointer target";
        var collider=dot.GetComponent<Collider>(); collider.enabled=false; Destroy(collider);
        dot.layer=LayerMask.NameToLayer("Ignore Raycast");
        var renderer=dot.GetComponent<Renderer>(); renderer.sharedMaterial=_line.sharedMaterial;
        renderer.shadowCastingMode=ShadowCastingMode.Off; renderer.receiveShadows=false;
        _dot=dot.transform; dot.SetActive(false);
    }
    private void Update()
    {
        bool allowed = MedicalTVScreen.IsNearScreen() || VRLearningGuide.HasOpenGuide || ElementInspectionStation.IsPlayerNearTable() || LayerModelController.IsNearTable() || PracticeSession.IsNearTable();
        if (!allowed && !_gated)
        {
            // Cancel hover/press when leaving so an invisible ray cannot keep a button held.
            var data = _pointer.PointerEventData;
            if (data != null)
            {
                if (data.pointerPress) ExecuteEvents.Execute(data.pointerPress, data, ExecuteEvents.pointerUpHandler);
                if (data.dragging && data.pointerDrag) ExecuteEvents.Execute(data.pointerDrag, data, ExecuteEvents.endDragHandler);
                foreach (var hovered in data.hovered)
                    if (hovered) ExecuteEvents.Execute(hovered, data, ExecuteEvents.pointerExitHandler);
                data.hovered.Clear(); data.pointerEnter = null; data.pointerPress = null;
                data.rawPointerPress = null; data.pointerDrag = null; data.dragging = false; data.eligibleForClick = false;
            }
            _pointer.CurrentUIElement = null;
        }
        _gated = !allowed;
        _pointer.enabled = allowed;
        if (!allowed) Hide();
    }
    private void LateUpdate()
    {
        if (!_pointer.isActiveAndEnabled) { Hide(); return; }
        float distance=_pointer.InputModule ? _pointer.InputModule.MaxDistance : 5;
        Vector3 origin=transform.position, direction=transform.forward;
        bool hitUI=_pointer.CurrentUIElement && _pointer.PointerEventData!=null;
        Vector3 end=origin+direction*distance;
        if (hitUI) end=_pointer.PointerEventData.pointerCurrentRaycast.worldPosition;
        bool hitPhysics=Physics.Raycast(origin,direction,out var hit,Vector3.Distance(origin,end),_mask,QueryTriggerInteraction.Ignore);
        if (hitPhysics) end=hit.point;
        _line.enabled=true; _line.SetPosition(0,origin); _line.SetPosition(1,end);
        _dot.gameObject.SetActive(hitUI || hitPhysics);
        if (_dot.gameObject.activeSelf)
        {
            _dot.position=end-direction*.006f;
            _dot.localScale=Vector3.one*Mathf.Clamp(Vector3.Distance(origin,end)*.006f,.008f,.018f);
        }
    }
    private void Hide() { if (_line) _line.enabled=false; if (_dot) _dot.gameObject.SetActive(false); }
    private void OnDisable() => Hide();
    private void OnDestroy() { if (_dot) Destroy(_dot.gameObject); }
}
