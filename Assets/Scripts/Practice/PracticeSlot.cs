using UnityEngine;

public class PracticeSlot : MonoBehaviour
{
    [SerializeField] private Renderer _contour;
    private MaterialPropertyBlock _block;
    public void Configure(Renderer contour) => _contour = contour;
    public void Show(bool visible, bool focused)
    {
        if (!_contour) return;
        _contour.enabled = visible;
        if (!visible) return;
        if (_block == null) _block = new MaterialPropertyBlock();
        _block.SetColor("_BaseColor", focused ? new Color(.12f,.9f,1,.32f) : new Color(.15f,.75f,.85f,.075f));
        _block.SetColor("_Color", focused ? new Color(.12f,.9f,1,.32f) : new Color(.15f,.75f,.85f,.075f));
        _contour.SetPropertyBlock(_block);
    }
}
