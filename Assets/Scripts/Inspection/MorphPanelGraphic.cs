using UnityEngine;
using UnityEngine.UI;

// A real changing outline, used both for the card and the stencil mask of its photograph.
[RequireComponent(typeof(CanvasRenderer))]
public class MorphPanelGraphic : MaskableGraphic, ICanvasRaycastFilter
{
    [SerializeField, Range(0, 1)] private float _roundness = 1;
    public float Roundness { get => _roundness; set { _roundness = Mathf.Clamp01(value); SetVerticesDirty(); } }
    protected override void OnPopulateMesh(VertexHelper mesh)
    {
        mesh.Clear();
        Rect rect = GetPixelAdjustedRect();
        float radius = Mathf.Min(rect.width, rect.height) * .5f * _roundness;
        Vector2[] centers = {
            new Vector2(rect.xMax-radius,rect.yMax-radius), new Vector2(rect.xMin+radius,rect.yMax-radius),
            new Vector2(rect.xMin+radius,rect.yMin+radius), new Vector2(rect.xMax-radius,rect.yMin+radius) };
        mesh.AddVert(rect.center, color, Vector2.one*.5f);
        const int steps = 12;
        for (int corner=0; corner<4; corner++) for (int i=0; i<=steps; i++)
        {
            float angle = (corner*90 + i*90f/steps)*Mathf.Deg2Rad;
            Vector2 point = centers[corner] + new Vector2(Mathf.Cos(angle),Mathf.Sin(angle))*radius;
            mesh.AddVert(point,color,new Vector2(Mathf.InverseLerp(rect.xMin,rect.xMax,point.x),Mathf.InverseLerp(rect.yMin,rect.yMax,point.y)));
        }
        int count=4*(steps+1);
        for(int i=0;i<count;i++) mesh.AddTriangle(0,i+1,(i+1)%count+1);
    }
    public bool IsRaycastLocationValid(Vector2 screenPoint, Camera eventCamera)
    {
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(rectTransform,screenPoint,eventCamera,out var local)) return false;
        Rect rect=rectTransform.rect;
        if(!rect.Contains(local)) return false;
        float radius=Mathf.Min(rect.width,rect.height)*.5f*_roundness;
        Vector2 nearest=new Vector2(Mathf.Clamp(local.x,rect.xMin+radius,rect.xMax-radius),Mathf.Clamp(local.y,rect.yMin+radius,rect.yMax-radius));
        return (local-nearest).sqrMagnitude<=radius*radius+.01f;
    }
}
