using UnityEngine;
using UnityEngine.UI;

public class LayerSelectionUI : MonoBehaviour
{
    [SerializeField] private ChoiceLayer _layers;
    [SerializeField] private Button[] _buttons;
    private readonly AnatomyLayer[] _order={AnatomyLayer.Skin,AnatomyLayer.Muscles,AnatomyLayer.Bones,AnatomyLayer.Brain};
    public void Configure(ChoiceLayer layers,Button[] buttons){_layers=layers;_buttons=buttons;}
    private void Start(){if(_layers){_layers.Changed+=Refresh;Refresh();}}
    private void Refresh(){for(int i=0;i<_buttons.Length&&i<_order.Length;i++)if(_buttons[i]){MedicalUITheme.Button(_buttons[i]);((Image)_buttons[i].targetGraphic).color=_layers.CurrentLayer==_order[i]?MedicalUITheme.Teal:MedicalUITheme.Navy;}}
    private void OnDestroy(){if(_layers)_layers.Changed-=Refresh;}
}
