using UnityEngine;
using UnityEngine.UI;

// No collider or raycast surface: guidance never blocks grabbing or terminal buttons.
public class GuidanceArrow : MonoBehaviour
{
    private Transform _target;
    private LineRenderer _line;
    private Material _material;
    private Canvas _caption;
    private Text _text;
    public void PointAt(Transform target, string caption)
    {
        _target=target;
        if(!_line)
        {
            _line=gameObject.AddComponent<LineRenderer>();_line.positionCount=5;_line.useWorldSpace=true;
            _material=new Material(Shader.Find("Sprites/Default"));_line.sharedMaterial=_material;
            _line.startWidth=_line.endWidth=.007f;_line.startColor=_line.endColor=new Color(.05f,.85f,1);
            _caption=InspectionUI.Panel("Arrow label",transform,new Vector2(460,64),.00065f);
            InspectionUI.Forget(_caption);_caption.GetComponent<GraphicRaycaster>().enabled=false;
            _text=InspectionUI.Text(_caption.transform,Vector2.zero,new Vector2(440,60),24,InspectionUI.Ink);_text.alignment=TextAnchor.MiddleCenter;
        }
        _text.text=caption;gameObject.SetActive(target);
    }
    private void LateUpdate()
    {
        if(!_target){gameObject.SetActive(false);return;}
        var renderer=_target.GetComponent<Renderer>();var end=renderer?renderer.bounds.center:_target.position;
        end+=Vector3.up*.07f;var top=end+Vector3.up*(.25f+Mathf.Sin(Time.unscaledTime*3)*.018f);
        var side=Camera.main?Camera.main.transform.right:Vector3.right;
        _line.SetPositions(new[]{top,end,end+Vector3.up*.055f+side*.035f,end,end+Vector3.up*.055f-side*.035f});
        _caption.transform.position=top+Vector3.up*.04f;
        if(Camera.main)_caption.transform.rotation=Quaternion.LookRotation(_caption.transform.position-Camera.main.transform.position);
    }
    private void OnDestroy(){InspectionUI.Forget(_caption);if(_material)Destroy(_material);}
}
