using UnityEngine;
using UnityEngine.SceneManagement;

public enum AppMode { Learning, Study, Practice }
public enum PracticeDifficulty { Beginner, Intermediate, Advanced }
public enum PracticeSet { Bones, Muscles, Brain, All }

public static class AppSession
{
    public static AppMode Mode { get; private set; } = AppMode.Study;
    public static PracticeDifficulty Difficulty { get; private set; } = PracticeDifficulty.Beginner;
    public static PracticeSet Set { get; private set; } = PracticeSet.Bones;
    public static bool IsLoading { get; private set; }
    public static void SelectMode(AppMode mode) => Mode = mode;
    public static void SelectDifficulty(PracticeDifficulty difficulty) => Difficulty = difficulty;
    public static void SelectSet(PracticeSet set) => Set = set;
    public static string DifficultyName(PracticeDifficulty value) => value == PracticeDifficulty.Beginner ? "Начальный" : value == PracticeDifficulty.Intermediate ? "Средний" : "Продвинутый";
    public static string SetName(PracticeSet value) => value == PracticeSet.Bones ? "Кости" : value == PracticeSet.Muscles ? "Мышцы" : value == PracticeSet.Brain ? "Мозг" : "Всё вместе";
    public static bool Load(string scene)
    {
        if (IsLoading || !Application.CanStreamedLevelBeLoaded(scene)) return false;
        IsLoading = true;
        SceneManager.sceneLoaded -= Loaded;
        SceneManager.sceneLoaded += Loaded;
        SceneManager.LoadSceneAsync(scene);
        return true;
    }
    private static void Loaded(Scene scene, LoadSceneMode mode) { IsLoading = false; BindSceneContext(); }
    public static void BindSceneContext()
    {
        var manager=HurricaneVR.Framework.Core.HVRManager.Instance;
        if(!manager)return;
        manager.PlayerController=Object.FindAnyObjectByType<HurricaneVR.Framework.Core.Player.HVRPlayerController>();
        manager.Camera=Camera.main?Camera.main.transform:null;
    }
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reset()
    {
        Mode = AppMode.Study; Difficulty = PracticeDifficulty.Beginner; Set = PracticeSet.Bones; IsLoading = false;
        SceneManager.sceneLoaded -= Loaded;
    }
}
