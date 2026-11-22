using System.Collections.Generic;
using HurricaneVR.Framework.Core;
using HurricaneVR.Framework.Core.Grabbers;
using InteracrSystem;
using UnityEngine;
using UnityEngine.UI;

public class PracticePieceLabel : MonoBehaviour
{
    private readonly HashSet<HVRGrabberBase> _hoverers=new HashSet<HVRGrabberBase>();
    private HVRGrabbable _grab;
    private PracticePiece _piece;
    private Canvas _label;
    private Text _text;
    private bool _visible=true;
    public bool LabelsAllowed => _visible;
    private void Awake(){_grab=GetComponent<HVRGrabbable>();_piece=GetComponent<PracticePiece>();}
    private void OnEnable(){_grab.HoverEnter.AddListener(Enter);_grab.HoverExit.AddListener(Exit);}
    private void OnDisable(){_grab.HoverEnter.RemoveListener(Enter);_grab.HoverExit.RemoveListener(Exit);_hoverers.Clear();if(_label)_label.gameObject.SetActive(false);}
    public void SetVisible(bool visible){_visible=visible;if(_label&&!visible)_label.gameObject.SetActive(false);}
    private void Enter(HVRGrabberBase hand,HVRGrabbable item)=>_hoverers.Add(hand);
    private void Exit(HVRGrabberBase hand,HVRGrabbable item)=>_hoverers.Remove(hand);
    private void LateUpdate()
    {
        bool show=_visible&&(_grab.IsHandGrabbed||HasHover());
        if(!_label&&show)
        {
            _label=InspectionUI.Panel("Practice · Element name",transform,new Vector2(320,65),.0007f);
            // Name plates are informational; they cannot catch the controller ray.
            foreach(var g in _label.GetComponentsInChildren<Graphic>())g.raycastTarget=false;
            var raycaster=_label.GetComponent<GraphicRaycaster>();if(raycaster)raycaster.enabled=false;
            _text=InspectionUI.Text(_label.transform,Vector2.zero,new Vector2(300,56),24,InspectionUI.Ink);_text.alignment=TextAnchor.MiddleCenter;_text.resizeTextForBestFit=true;_text.resizeTextMinSize=15;_text.resizeTextMaxSize=24;
        }
        if(!_label)return;_label.gameObject.SetActive(show);if(!show)return;
        _text.text=_piece.DisplayName;
        var scale=transform.lossyScale;
        _label.transform.localScale=new Vector3(.0007f/scale.x,.0007f/scale.y,.0007f/scale.z);
        _label.transform.position=transform.position+Vector3.up*.11f;
        if(Camera.main)_label.transform.rotation=Quaternion.LookRotation(_label.transform.position-Camera.main.transform.position,Vector3.up);
    }
    private bool HasHover(){_hoverers.RemoveWhere(h=>!h||h.HoverTarget!=_grab);foreach(var h in _hoverers)if(ElementGrabAvailability.CanHighlight(h,_grab))return true;return false;}
    private void OnDestroy(){InspectionUI.Forget(_label);}
}
