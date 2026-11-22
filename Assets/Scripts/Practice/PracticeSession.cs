using System;
using System.Collections.Generic;
using System.Linq;
using HurricaneVR.Framework.Core;
using InteracrSystem;
using UnityEngine;
using UnityEngine.UI;

public class PracticeSession : MonoBehaviour
{
    [SerializeField] private PracticePiece[] _pieces;
    [SerializeField] private Transform _partsRoot, _assemblyRoot, _assemblyTable, _supplySurface;
    [SerializeField] private Vector2 _supplySize = new Vector2(2.28f, 1.02f);
    [SerializeField] private float _assemblySurfaceHeight = .83f;
    [SerializeField] private LayerModelController _modelControl;
    private readonly List<PracticePiece> _active = new List<PracticePiece>();
    private Canvas _panel;
    private Text _title, _progress, _feedback, _hint, _timeValue, _checkValue, _countLabel;
    private PracticePiece _focused;
    private Button _check;
    private bool _brainChecked;
    private int _brainFaults;
    public bool HasBrain => _active.Any(p=>p.Category==PracticeSet.Brain);
    public int SubmittedCount => _active.Count(p=>p.IsSubmitted);
    public Transform AssemblyRoot => _assemblyRoot;
    public bool IsInAssemblyArea(Vector3 position)
    {var p=_assemblyTable.InverseTransformPoint(position);return Mathf.Abs(p.x)<1.05f&&Mathf.Abs(p.z)<.65f&&p.y>.55f&&p.y<1.9f;}
    public void AssemblyChanged(){_brainChecked=false;var check=FindAnyObjectByType<NeurologicalCheck>();if(check)check.Invalidate();}
    public void Attempted(PracticePiece piece){AssemblyChanged();ShowMessage("Деталь размещена. Можно продолжить сборку.",4);Refresh();}
    public void CheckBrain(){var check=FindAnyObjectByType<NeurologicalCheck>();if(check&&HasBrain&&!check.Check(this))ShowMessage("Отпусти детали и подожди, пока они остановятся.",4);}
    public void ApplyBrainReport(NeurologicalReport report)
    {foreach(var piece in _active.Where(p=>p.Category==PracticeSet.Brain))piece.ApplyBrainCheck(report.Findings.First(f=>f.PieceKey==piece.Slot.name).State);if(IsComplete)_finished=Time.unscaledTime;_brainChecked=true;_brainFaults=report.MissingCount+report.MisplacedCount;ShowMessage(_brainFaults==0?"Мозг собран верно. Неврологическая проверка завершена.":"В сборке мозга есть ошибки. Исправь детали и повтори проверку.",8);Refresh();}
    private GuidanceArrow _startArrow;
    public PracticePiece ReferencePiece { get; private set; }
    private float _started, _finished = -1, _messageUntil;
    private int _mistakes;
    private Quaternion _assemblyRotation;
    private readonly Dictionary<PracticePiece, Vector3> _originalSlots = new Dictionary<PracticePiece, Vector3>();
    private static PracticeSession _instance;
    public Transform PartsRoot => _partsRoot;
    public IReadOnlyList<PracticePiece> ActivePieces => _active;
    public PracticeDifficulty Difficulty { get; private set; }
    public PracticeSet Set { get; private set; }
    public int PlacedCount => _active.Count(p=>p.IsPlaced);
    public int TotalCount => _active.Count;
    public int Mistakes => _mistakes;
    public bool IsComplete => TotalCount > 0 && PlacedCount == TotalCount;
    public float PositionTolerance => Difficulty == PracticeDifficulty.Beginner ? .1f : Difficulty == PracticeDifficulty.Intermediate ? .075f : .06f;
    public float RotationTolerance => Difficulty == PracticeDifficulty.Beginner ? 35 : Difficulty == PracticeDifficulty.Intermediate ? 25 : 18;
    public static bool IsNearTable()
    {
        var camera=Camera.main;
        return _instance && camera && (Horizontal(camera.transform.position,_instance._assemblyTable.position)<1.8f || Horizontal(camera.transform.position,_instance._supplySurface.position)<1.8f);
    }
    private static float Horizontal(Vector3 a,Vector3 b)=>Vector3.ProjectOnPlane(a-b,Vector3.up).magnitude;
    public void Configure(PracticePiece[] pieces,Transform parts,Transform assembly,Transform table,Transform surface,LayerModelController control)
    { _pieces=pieces;_partsRoot=parts;_assemblyRoot=assembly;_assemblyTable=table;_supplySurface=surface;_modelControl=control; }
    private void Awake(){_instance=this;_assemblyRotation=_assemblyRoot.localRotation;foreach(var p in _pieces)_originalSlots[p]=p.Slot.transform.localPosition;}
    private void Start()
    {
        AppSession.SelectMode(AppMode.Practice);
        InspectionVRInput.EnsureModule();if(!FindAnyObjectByType<InspectionVRInput>())gameObject.AddComponent<InspectionVRInput>();
        BuildPanel(); Begin(AppSession.Difficulty,AppSession.Set);
    }
    public void Begin(PracticeDifficulty difficulty, PracticeSet set)
    {
        var neurologicalCheck=FindAnyObjectByType<NeurologicalCheck>();if(neurologicalCheck)neurologicalCheck.ResetCheck();
        Difficulty=difficulty;Set=set;AppSession.SelectDifficulty(difficulty);AppSession.SelectSet(set);
        _finished=-1;_mistakes=0;_brainChecked=false;_brainFaults=0;_started=Time.unscaledTime;_focused=null;
        foreach(var piece in _pieces)if(piece&&piece.gameObject.activeInHierarchy)piece.GetComponent<HVRGrabbable>().ForceRelease();
        _active.Clear();
        foreach(var piece in _pieces)
        {
            bool selected=set==PracticeSet.All||piece.Category==set;
            piece.gameObject.SetActive(selected);piece.Slot.gameObject.SetActive(selected);
            if(selected)_active.Add(piece);
            piece.Slot.Show(selected&&difficulty==PracticeDifficulty.Beginner,false);
        }
        _assemblyRoot.localRotation=_assemblyRotation;
        var layout=Layout(_active,out float scale);
        _assemblyRoot.localScale=Vector3.one*scale;
        Bounds assemblyBounds=default;bool first=true;
        foreach(var piece in _active)
        {
            var b=RotatedBounds(piece.GetComponent<MeshFilter>().sharedMesh.bounds,piece.Slot.transform.localRotation,1);
            b.center+=_originalSlots[piece];
            if(first){assemblyBounds=b;first=false;}else assemblyBounds.Encapsulate(b);
        }
        foreach(var piece in _active)piece.Slot.transform.localPosition=_originalSlots[piece]-assemblyBounds.center;
        _assemblyRoot.position=_assemblyTable.TransformPoint(new Vector3(.05f,_assemblySurfaceHeight+assemblyBounds.extents.y*scale,0));
        foreach(var item in layout)
        {
            var piece=item.Piece;
            var position=_supplySurface.TransformPoint(item.Position);
            var rotation=_supplySurface.rotation*item.Rotation;
            piece.Initialize(this,position,rotation,Vector3.one*scale);
            var visual=piece.GetComponent<ElementInteractionVisuals>();if(visual)visual.ShowRelatedHighlight=false;
            var label=piece.GetComponent<PracticePieceLabel>();if(label)label.SetVisible(difficulty!=PracticeDifficulty.Advanced);
        }
        _modelControl.CaptureResetPose();
        ReferencePiece = null;
        if(difficulty != PracticeDifficulty.Beginner && _active.Count > 0)
        {
            var candidates = set == PracticeSet.All ? _active.Where(p=>p.Category==PracticeSet.Bones) : _active;
            ReferencePiece = candidates.OrderByDescending(p=>p.GetComponent<MeshFilter>().sharedMesh.bounds.size.sqrMagnitude).First();
            ReferencePiece.InstallReference();
        }
        if(!_startArrow){_startArrow=new GameObject("Practice · Assembly start arrow").AddComponent<GuidanceArrow>();_startArrow.transform.SetParent(transform,false);}
        _startArrow.PointAt(ReferencePiece?ReferencePiece.transform:_active[0].Slot.transform,ReferencePiece?"Опорная деталь · начало сборки":"Собирай здесь · совмести деталь с контуром");
        Physics.SyncTransforms();
        if(_check)_check.gameObject.SetActive(HasBrain);
        ShowMessage(HasBrain?"Размести части мозга на подставке и нажми «Проверить сборку».":difficulty==PracticeDifficulty.Beginner?"Переноси детали к прозрачным контурам на столе сборки.":"Одна деталь уже на месте. Собери остальные вокруг неё.",8);
        Refresh();
    }
    private struct Arrangement {public PracticePiece Piece;public Vector3 Position, BoundsCenter;public Quaternion Rotation;public Vector3 Size;}
    private List<Arrangement> Layout(List<PracticePiece> pieces,out float scale)
    {
        var order=pieces.OrderByDescending(p=>p.GetComponent<MeshFilter>().sharedMesh.bounds.size.magnitude).ToArray();
        var result=new List<Arrangement>();scale=Set==PracticeSet.Brain?2.2f:.8f;
        for(int attempt=0;attempt<6;attempt++)
        {
            result.Clear();float x=-_supplySize.x*.5f,z=-_supplySize.y*.5f,row=0;
            bool fit=true;
            foreach(var piece in order)
            {
                var bounds=piece.GetComponent<MeshFilter>().sharedMesh.bounds;
                var options=new[]{Quaternion.identity,Quaternion.Euler(90,0,0),Quaternion.Euler(0,0,90)};
                float currentScale=scale;
                var rotation=options.OrderBy(q=>RotatedBounds(bounds,q,currentScale).size.y).First();
                rotation=Quaternion.Euler(0,UnityEngine.Random.value>.5f?180:0,0)*rotation;
                var b=RotatedBounds(bounds,rotation,scale);Vector3 size=b.size;
                if(x+size.x>_supplySize.x*.5f){x=-_supplySize.x*.5f;z+=row+.045f;row=0;}
                if(z+size.z>_supplySize.y*.5f){fit=false;break;}
                result.Add(new Arrangement{Piece=piece,Position=new Vector3(x+size.x*.5f-b.center.x,-b.min.y+.012f,z+size.z*.5f-b.center.z),BoundsCenter=b.center,Rotation=rotation,Size=size});
                x+=size.x+.045f;row=Mathf.Max(row,size.z);
            }
            if(fit)break;
            scale*=.88f;
        }
        if(result.Count!=pieces.Count)throw new InvalidOperationException("Practice supply table cannot fit the selected set.");
        // Centre the packed layout on the supply table, independent of the anatomy's original positions.
        float minX=result.Min(a=>a.Position.x+a.BoundsCenter.x-a.Size.x*.5f),maxX=result.Max(a=>a.Position.x+a.BoundsCenter.x+a.Size.x*.5f);
        float minZ=result.Min(a=>a.Position.z+a.BoundsCenter.z-a.Size.z*.5f),maxZ=result.Max(a=>a.Position.z+a.BoundsCenter.z+a.Size.z*.5f);
        var offset=new Vector3((minX+maxX)*.5f,0,(minZ+maxZ)*.5f);
        for(int i=0;i<result.Count;i++){var a=result[i];a.Position-=offset;result[i]=a;}
        return result;
    }
    private static Bounds RotatedBounds(Bounds b,Quaternion rotation,float scale)
    {
        var result=new Bounds(rotation*b.center*scale,Vector3.zero);
        for(int n=0;n<8;n++){var p=b.center+Vector3.Scale(b.extents,new Vector3((n&1)==0?-1:1,(n&2)==0?-1:1,(n&4)==0?-1:1));result.Encapsulate(rotation*p*scale);}
        return result;
    }
    public bool IsNearestTarget(PracticePiece piece, Vector3 position)
    {
        float own=(piece.Slot.transform.position-position).sqrMagnitude;
        return !_active.Any(other=>other!=piece&&(other.Slot.transform.position-position).sqrMagnitude+.000001f<own);
    }
    public void Placed(PracticePiece piece)
    {
        if(!_active.Contains(piece))return;
        if(IsComplete){_finished=Time.unscaledTime;ShowMessage("Сборка завершена! Все детали установлены.",float.PositiveInfinity);}
        else ShowMessage(Difficulty==PracticeDifficulty.Advanced?"Деталь установлена.":piece.DisplayName+" — на месте.",3);
        Refresh();
    }
    public void Removed(PracticePiece piece){AssemblyChanged();_finished=-1;ShowMessage("Деталь снята со сборки.",2);Refresh();}
    public void Rejected(PracticePiece piece)
    {
        var p=_assemblyTable.InverseTransformPoint(piece.transform.position);
        if(Mathf.Abs(p.x)<1.2f&&Mathf.Abs(p.z)<.8f&&p.y>-.2f&&p.y<1.7f)
        {
            _mistakes++;
            ShowMessage(Difficulty==PracticeDifficulty.Beginner?"Найди контур детали и совмести её положение и поворот.":"Деталь пока не на месте.",3);
        }
        Refresh();
    }
    private void Update()
    {
        _focused=_active.FirstOrDefault(p=>p.IsHeld);
        if(_startArrow&&PlacedCount>(ReferencePiece?1:0))_startArrow.gameObject.SetActive(false);
        foreach(var piece in _pieces)piece.Slot.Show(piece.gameObject.activeInHierarchy&&!piece.IsPlaced&&Difficulty==PracticeDifficulty.Beginner,piece==_focused);
        Refresh();
        if(UnityEngine.InputSystem.Keyboard.current?.escapeKey.wasPressedThisFrame==true)AppSession.Load("Startup");
    }
    private void ShowMessage(string message,float duration){if(_feedback)_feedback.text=message;_messageUntil=Time.unscaledTime+duration;}
    private void Refresh()
    {
        if(!_panel)return;
        _title.text=AppSession.SetName(Set)+"\n"+AppSession.DifficultyName(Difficulty)+" уровень";
        float seconds=(_finished>=0?_finished:Time.unscaledTime)-_started;
        _countLabel.text=HasBrain?"Размещено":"Собрано";
        _progress.text=$"{(HasBrain?SubmittedCount:PlacedCount)} / {TotalCount}";
        _timeValue.text=$"{Mathf.FloorToInt(seconds/60):00}:{Mathf.FloorToInt(seconds%60):00}";
        _checkValue.text=HasBrain?(_brainChecked?(_brainFaults==0?"Верно":$"Ошибок: {_brainFaults}"):"Не проверено"):_mistakes.ToString();
        _checkValue.color=HasBrain&&_brainChecked&&_brainFaults>0?MedicalUITheme.Coral:MedicalUITheme.Navy;
        if(Time.unscaledTime>_messageUntil)_feedback.text=HasBrain?(_brainChecked?(_brainFaults==0?"Мозг собран верно. Проверка завершена.":"Исправь отмеченные ошибки и повтори проверку."):"Нажми «Проверить сборку», когда будешь готов."):IsComplete?"Сборка завершена!":"Возьми деталь со стола и установи её на столе сборки.";
        var next=_focused?_focused:_active.FirstOrDefault(p=>!p.IsPlaced);
        _hint.text=Difficulty==PracticeDifficulty.Beginner&&next?"Подсказка: "+next.DisplayName:ReferencePiece?"Одна деталь уже на месте. Ориентируйся по ней.":"";
    }
    private void BuildPanel()
    {
        _panel=InspectionUI.Panel("Practice · Session panel",transform,new Vector2(1000,562),.001f);
        _panel.transform.position=new Vector3(.45f,1.65f,-1.95f);_panel.transform.rotation=Quaternion.identity;
        MedicalTVScreen.Header(_panel,"Практические занятия");
        _title=MedicalTVScreen.Label(_panel.transform,"",new Vector2(0,163),new Vector2(900,76),30,MedicalUITheme.Navy,true);
        string[] headings={"Собрано","Время","Проверка"};
        Text[] values=new Text[3];
        for(int i=0;i<3;i++){
            float x=-310+i*310;
            MedicalTVScreen.Card(_panel.transform,"Practice · Metric "+i,new Vector2(x,62),new Vector2(280,88));
            var label=MedicalTVScreen.Label(_panel.transform,headings[i],new Vector2(x,86),new Vector2(248,24),16,MedicalUITheme.Teal,true);
            values[i]=MedicalTVScreen.Label(_panel.transform,"",new Vector2(x,48),new Vector2(248,42),30,MedicalUITheme.Navy,true);
            if(i==0)_countLabel=label;
        }
        _progress=values[0];_timeValue=values[1];_checkValue=values[2];
        _feedback=MedicalTVScreen.Label(_panel.transform,"",new Vector2(0,-36),new Vector2(900,80),26,MedicalUITheme.Navy);
        _feedback.resizeTextForBestFit=true;_feedback.resizeTextMinSize=23;_feedback.resizeTextMaxSize=26;
        _hint=MedicalTVScreen.Label(_panel.transform,"",new Vector2(-130,-132),new Vector2(640,74),23,MedicalUITheme.Teal);
        _hint.resizeTextForBestFit=true;_hint.resizeTextMinSize=20;_hint.resizeTextMaxSize=23;
        InspectionUI.Button(_panel.transform,"Заново",new Vector2(-310,-224),new Vector2(280,64),()=>Begin(Difficulty,Set));
        InspectionUI.Button(_panel.transform,"Управление",new Vector2(0,-224),new Vector2(280,64),()=>VRLearningGuide.ShowControls(transform));
        InspectionUI.Button(_panel.transform,"В меню",new Vector2(310,-224),new Vector2(280,64),()=>AppSession.Load("Startup"));
        _check=InspectionUI.Button(_panel.transform,"Проверить сборку",new Vector2(337,-129),new Vector2(226,66),CheckBrain);
        MedicalTVScreen.Present(_panel);MedicalTVScreen.Primary(_check);InspectionVRInput.RegisterCanvas(_panel);_modelControl.SetPanel(_panel);
    }
    private void OnDestroy(){MedicalTVScreen.Dismiss(_panel);InspectionUI.Forget(_panel);if(_instance==this)_instance=null;}
}
