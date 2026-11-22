using System.Collections.Generic;
using System.Linq;
using InteracrSystem;
using UnityEngine;
using UnityEngine.UI;

public class LayerElementCatalog : MonoBehaviour
{
    [SerializeField] private ChoiceLayer _layers;
    [SerializeField] private Transform _screen;
    private readonly Dictionary<int,ElementDog[]> _parts=new Dictionary<int,ElementDog[]>();
    private List<IGrouping<string,ElementDog>> _entries=new List<IGrouping<string,ElementDog>>();
    private Canvas _canvas; private Text _title,_status; private Button[] _rows;
    private GuidanceArrow _arrow;private int _page;private float _until,_refresh;
    public string SelectedId {get;private set;}
    public int SelectionCount {get;private set;}
    public int EntryCount=>_entries.Count;
    public Transform Screen=>_screen;
    public void Configure(ChoiceLayer layers,Transform screen){_layers=layers;_screen=screen;}
    private void Start()
    {
        var roots=_layers.LayerRoots;
        for(int i=0;i<roots.Length;i++)_parts[i]=roots[i]?roots[i].GetComponentsInChildren<ElementDog>(true):new ElementDog[0];
        _canvas=InspectionUI.Panel("Layer · Available elements",_screen,new Vector2(800,490),1);
        _canvas.transform.localScale=Vector3.one*(.4f/(_screen.lossyScale.x*800));
        _title=InspectionUI.Text(_canvas.transform,new Vector2(0,206),new Vector2(760,58),28,InspectionUI.Ink);
        _rows=new Button[6];for(int i=0;i<6;i++){int row=i;_rows[i]=InspectionUI.Button(_canvas.transform,"",new Vector2(0,137-i*48),new Vector2(760,43),()=>SelectRow(row));}
        _status=InspectionUI.Text(_canvas.transform,new Vector2(0,-151),new Vector2(760,38),20,InspectionUI.Accent);
        InspectionUI.Button(_canvas.transform,"←",new Vector2(-315,-210),new Vector2(100,46),()=>{_page=Mathf.Max(0,_page-1);Render();});
        InspectionUI.Button(_canvas.transform,"Снять выделение",new Vector2(0,-210),new Vector2(460,46),ClearSelection);
        InspectionUI.Button(_canvas.transform,"→",new Vector2(315,-210),new Vector2(100,46),()=>{_page=Mathf.Min((_entries.Count-1)/6,_page+1);Render();});
        _arrow=new GameObject("Catalog selection arrow").AddComponent<GuidanceArrow>();_arrow.transform.SetParent(transform,false);_arrow.gameObject.SetActive(false);
        _layers.Changed+=Reload;Reload();InspectionVRInput.EnsureModule();InspectionVRInput.RegisterCanvas(_canvas);
    }
    private void Reload()
    {
        ClearSelection();_page=0;
        int[] indices=_layers.CurrentLayer==AnatomyLayer.Skin?new[]{0}:_layers.CurrentLayer==AnatomyLayer.Brain?new[]{3}:_layers.CurrentLayer==AnatomyLayer.Muscles?new[]{2}:new[]{1};
        _entries=indices.SelectMany(i=>_parts[i]).Where(p=>p).GroupBy(p=>p.Id).OrderBy(g=>g.Key).ToList();Render();
    }
    private void SelectRow(int row){int index=_page*6+row;if(index<_entries.Count)Select(_entries[index].Key);}
    public void Select(string id)
    {
        var entry=_entries.FirstOrDefault(g=>g.Key==id);if(entry==null)return;
        ClearSelection();SelectedId=id;SelectionCount++;_until=Time.unscaledTime+12;
        foreach(var item in entry)if(item.TryGetComponent<ElementInteractionVisuals>(out var visual))visual.CatalogSelected=true;
        var target=entry.FirstOrDefault(p=>p.gameObject.activeInHierarchy);
        if(target)_arrow.PointAt(target.transform,entry.First().Data?.name??id);Render();
    }
    public void ClearSelection()
    {
        foreach(var item in _parts.Values.SelectMany(p=>p))if(item&&item.TryGetComponent<ElementInteractionVisuals>(out var visual))visual.CatalogSelected=false;
        SelectedId=null;if(_arrow)_arrow.gameObject.SetActive(false);if(_status)_status.text="Нажми на название, чтобы найти деталь.";
    }
    private void Update(){if(SelectedId!=null&&Time.unscaledTime>_until)ClearSelection();if(Time.unscaledTime>_refresh){_refresh=Time.unscaledTime+1;Render();}}
    private void Render()
    {
        if(!_title)return;_title.text=$"{_layers.CurrentLayerName} · {_entries.Count} элементов  /  {_page+1} из {Mathf.Max(1,Mathf.CeilToInt(_entries.Count/6f))}";
        for(int i=0;i<6;i++){int n=_page*6+i;_rows[i].gameObject.SetActive(n<_entries.Count);if(n>=_entries.Count)continue;var e=_entries[n];var label=_rows[i].GetComponentInChildren<Text>();label.text=(e.Key==SelectedId?"● ":"")+(e.First().Data?.name??e.Key)+(e.Count()>1?$" · {e.Count()} части":"");label.resizeTextForBestFit=true;label.resizeTextMinSize=16;label.resizeTextMaxSize=25;}
        if(SelectedId!=null)_status.text="Выбранная деталь подсвечена на модели.";
    }
    private void OnDestroy(){if(_layers)_layers.Changed-=Reload;ClearSelection();InspectionUI.Forget(_canvas);}
}
