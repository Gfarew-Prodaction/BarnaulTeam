using System;
using System.Collections.Generic;
using System.Linq;

public enum BrainAssemblyState { Correct, Missing, Misplaced }
public enum NeurologicalScenario { Normal, CircleLeft, CircleRight, CerebellarAtaxia, VestibularAtaxia, ReviewRequired }
[Serializable] public class BrainAssemblyFinding
{
    public string Id,Name;
    public string PieceKey;
    public BrainAssemblyState State;
    public BrainAssemblyFinding(string id,string name,BrainAssemblyState state,string pieceKey=null){Id=id;Name=name;State=state;PieceKey=pieceKey;}
}
public class NeurologicalReport
{
    public readonly List<BrainAssemblyFinding> Findings;
    public readonly List<NeurologicalScenario> Scenarios;
    public bool IsNormal=>Findings.Count>0&&Findings.All(f=>f.State==BrainAssemblyState.Correct);
    public int CorrectCount=>Findings.Count(f=>f.State==BrainAssemblyState.Correct);
    public int MissingCount=>Findings.Count(f=>f.State==BrainAssemblyState.Missing);
    public int MisplacedCount=>Findings.Count(f=>f.State==BrainAssemblyState.Misplaced);
    public NeurologicalReport(IEnumerable<BrainAssemblyFinding> findings)
    {
        Findings=findings.ToList();Scenarios=Findings.Where(f=>f.State!=BrainAssemblyState.Correct).Select(f=>NeurologicalAssessment.ScenarioFor(f.Id)).Distinct().ToList();
        if(IsNormal)Scenarios.Add(NeurologicalScenario.Normal);
        if(Scenarios.Count==0)Scenarios.Add(NeurologicalScenario.ReviewRequired);
    }
}
public static class NeurologicalAssessment
{
    public static NeurologicalScenario ScenarioFor(string id)
    {
        switch(id)
        {
            case "dog_left_hemisphere": return NeurologicalScenario.CircleLeft;
            case "dog_right_hemisphere": return NeurologicalScenario.CircleRight;
            case "dog_cerebellum": return NeurologicalScenario.CerebellarAtaxia;
            // Educational proxy: these gross parts include brainstem vestibular pathways/nuclei.
            // It does not imply that every real lesion of the pons or medulla causes this syndrome.
            case "dog_pons": case "dog_medulla": case "dog_vestibular_system": return NeurologicalScenario.VestibularAtaxia;
            default:return NeurologicalScenario.ReviewRequired;
        }
    }
    public static string Title(NeurologicalScenario scenario)
    {
        switch(scenario)
        {
            case NeurologicalScenario.Normal:return "Нормальная походка";
            case NeurologicalScenario.CircleLeft:return "Возможное кружение влево";
            case NeurologicalScenario.CircleRight:return "Возможное кружение вправо";
            case NeurologicalScenario.CerebellarAtaxia:return "Мозжечковая атаксия";
            case NeurologicalScenario.VestibularAtaxia:return "Вестибулярное нарушение";
            default:return "Требуется исправить сборку";
        }
    }
    public static string Description(NeurologicalScenario scenario)
    {
        switch(scenario)
        {
            case NeurologicalScenario.Normal:return "Все отделы мозга установлены верно. Собака идёт ровно, движения конечностей согласованы, голова сохраняет обычное положение.";
            case NeurologicalScenario.CircleLeft:return "Ошибка левого полушария. Демонстрируется возможное кружение в сторону нарушения — налево. Обрати внимание на устойчивое направление поворота.";
            case NeurologicalScenario.CircleRight:return "Ошибка правого полушария. Демонстрируется возможное кружение в сторону нарушения — направо. Обрати внимание на устойчивое направление поворота.";
            case NeurologicalScenario.CerebellarAtaxia:return "Ошибка мозжечка. Демонстрируются несогласованные, чрезмерно высокие шаги и раскачивание туловища. Размах движений конечностей увеличен.";
            case NeurologicalScenario.VestibularAtaxia:return "В учебном сценарии ошибка области ствола связана с вестибулярными путями: голова наклонена, походка неустойчива, туловище отклоняется в сторону.";
            default:return "Есть ошибка в отделе, для которого отдельная двигательная демонстрация не задана. Собака ожидает. Это не означает нормальную функцию: исправь отмеченные элементы и повтори проверку.";
        }
    }
}
