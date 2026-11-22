using System.Linq;
using InteracrSystem;
using UnityEngine;
using UnityEngine.UI;

public class NeurologicalCheck : MonoBehaviour
{
    [SerializeField] private NeurologicalDog _dog;
    [SerializeField] private GameObject _stage;
    private PracticeSession _session;
    private Canvas _page;
    private Text _score,_scenario,_body,_note,_sequence;
    private int _index;
    private float _next;
    public NeurologicalReport CurrentReport {get;private set;}
    public NeurologicalScenario CurrentScenario=>CurrentReport!=null?CurrentReport.Scenarios[_index]:NeurologicalScenario.ReviewRequired;
    public void Configure(NeurologicalDog dog,GameObject stage){_dog=dog;_stage=stage;}
    public bool Check(PracticeSession session)
    {
        if(!session.HasBrain)return false;
        var brain=session.ActivePieces.Where(p=>p.Category==PracticeSet.Brain).ToArray();
        if(brain.Any(p=>p.IsHeld||p.IsMoving)){Debug.Log("Перед проверкой отпусти все детали и дождись остановки.");return false;}
        _session=session;
        CurrentReport=new NeurologicalReport(brain.Select(p=>new BrainAssemblyFinding(p.GetComponent<ElementDog>().Id,p.DisplayName,p.EvaluateBrainPlacement(),p.Slot.name)));
        session.ApplyBrainReport(CurrentReport);BuildPage();_stage.SetActive(true);_index=0;ShowScenario();return true;
    }
    private void BuildPage()
    {
        if(_page){MedicalTVScreen.Present(_page);return;}
        _page=InspectionUI.Panel("Neurology · Check result",transform,new Vector2(1000,562),.001f);
        var title=InspectionUI.Text(_page.transform,new Vector2(-120,235),new Vector2(690,40),25,InspectionUI.Accent);title.text="Проверка сборки мозга";
        _score=InspectionUI.Text(_page.transform,new Vector2(-120,181),new Vector2(690,46),23,InspectionUI.Ink);
        _scenario=InspectionUI.Text(_page.transform,new Vector2(-120,104),new Vector2(690,80),32,InspectionUI.Ink);
        _body=InspectionUI.Text(_page.transform,new Vector2(-120,-24),new Vector2(690,206),24,InspectionUI.Ink);_body.resizeTextForBestFit=true;_body.resizeTextMinSize=20;_body.resizeTextMaxSize=24;
        _note=InspectionUI.Text(_page.transform,new Vector2(-120,-183),new Vector2(690,74),20,InspectionUI.Muted);
        _sequence=InspectionUI.Text(_page.transform,new Vector2(-120,-244),new Vector2(690,32),20,InspectionUI.Accent);
        InspectionUI.Image(_page.transform,"Rule",new Vector2(245,0),new Vector2(2,480),InspectionUI.Accent);
        InspectionUI.Button(_page.transform,"Следующий",new Vector2(365,142),new Vector2(210,65),()=>SelectScenario(1));
        InspectionUI.Button(_page.transform,"Предыдущий",new Vector2(365,48),new Vector2(210,65),()=>SelectScenario(-1));
        InspectionUI.Button(_page.transform,"Повторить",new Vector2(365,-56),new Vector2(210,65),ShowScenario);
        InspectionUI.Button(_page.transform,"Продолжить сборку",new Vector2(365,-214),new Vector2(210,65),Close);
        MedicalTVScreen.Present(_page);
    }
    public void SelectScenario(int delta)
    {if(CurrentReport==null)return;_index=(_index+delta+CurrentReport.Scenarios.Count)%CurrentReport.Scenarios.Count;ShowScenario();}
    private void ShowScenario()
    {
        if(CurrentReport==null)return;
        var scenario=CurrentScenario;
        _score.text=$"Верно: {CurrentReport.CorrectCount} / {CurrentReport.Findings.Count}   ·   Не хватает: {CurrentReport.MissingCount}   ·   Ошибок: {CurrentReport.MisplacedCount}";
        _scenario.text=NeurologicalAssessment.Title(scenario);_scenario.color=CurrentReport.IsNormal?MedicalUITheme.Success:MedicalUITheme.Coral;
        var findings=CurrentReport.Findings.Where(f=>f.State!=BrainAssemblyState.Correct&&NeurologicalAssessment.ScenarioFor(f.Id)==scenario).ToArray();
        _body.text=NeurologicalAssessment.Description(scenario)+(findings.Length>0?"\n\n"+string.Join("; ",findings.GroupBy(f=>new {f.Id,f.State}).Select(g=>g.First().Name+(g.Count()>1?" ("+g.Count()+" части)":"")+": "+(g.Key.State==BrainAssemblyState.Missing?"не установлен":"поставлен неверно"))):"");
        _note.text=scenario==NeurologicalScenario.VestibularAtaxia?"Учебное допущение: область ствола включает вестибулярные пути. Ошибка целой детали не соответствует одному конкретному поражению.":"Учебная демонстрация возможных признаков. Походка сама по себе не устанавливает диагноз. Несколько нарушений показаны по очереди.";
        _sequence.text=$"Сценарий {_index+1} / {CurrentReport.Scenarios.Count} · наблюдай за собакой в центре кабинета";
        _dog.Play(scenario);_next=Time.unscaledTime+14;
    }
    private void Update(){if(_page&&_page.gameObject.activeInHierarchy&&CurrentReport!=null&&CurrentReport.Scenarios.Count>1&&Time.unscaledTime>=_next)SelectScenario(1);}
    public void Invalidate(){if(CurrentReport==null)return;Close();CurrentReport=null;}
    public void Close(){if(_page){MedicalTVScreen.Dismiss(_page);InspectionUI.Forget(_page);Destroy(_page.gameObject);_page=null;}if(_dog)_dog.Stop();if(_stage)_stage.SetActive(false);}
    public void ResetCheck(){Close();CurrentReport=null;}
    private void OnDestroy(){Close();}
}
