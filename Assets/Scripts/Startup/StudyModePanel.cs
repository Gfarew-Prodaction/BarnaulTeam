using System.Linq;
using HurricaneVR.Framework.Core;
using InteracrSystem;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class StudyModePanel : MonoBehaviour
{
    private Canvas _panel;
    private void Start()
    {
        InspectionVRInput.EnsureModule();
        _panel=InspectionUI.Panel("Study · Navigation",transform,new Vector2(1000,562),.001f);
        _panel.transform.position=new Vector3(.45f,1.82f,-1.95f);
        MedicalTVScreen.Header(_panel,"Изучение");
        MedicalTVScreen.Label(_panel.transform,"Голова собаки",new Vector2(0,163),new Vector2(900,66),38,MedicalUITheme.Navy,true);
        MedicalTVScreen.Label(_panel.transform,"",new Vector2(0,111),new Vector2(900,38),22,MedicalUITheme.Muted);
        MedicalTVScreen.Card(_panel.transform,"Study · Workflow",new Vector2(-153,-29),new Vector2(594,214));
        MedicalTVScreen.Label(_panel.transform,"Как взять деталь",new Vector2(-153,47),new Vector2(546,30),18,MedicalUITheme.Teal,true);
        MedicalTVScreen.Label(_panel.transform,"Выбери слой и деталь в каталоге.\nВозьми или притяни её кнопкой Grip.\nДля описания и фото положи деталь\nв зону у компьютера.",new Vector2(-153,-36),new Vector2(546,124),23,MedicalUITheme.Navy);
        MedicalTVScreen.Card(_panel.transform,"Study · Controls",new Vector2(312,-29),new Vector2(276,214));
        MedicalTVScreen.Label(_panel.transform,"Управление",new Vector2(312,47),new Vector2(228,30),18,MedicalUITheme.Teal,true);
        MedicalTVScreen.Label(_panel.transform,"Y   Режим модели\nX   Сброс поворота\nA   Сброс размера",new Vector2(312,-35),new Vector2(228,124),23,MedicalUITheme.Navy);
        MedicalTVScreen.Label(_panel.transform,"X и A работают после включения режима модели кнопкой Y.",new Vector2(0,-156),new Vector2(900,42),20,MedicalUITheme.Muted);
        InspectionUI.Button(_panel.transform,"Управление",new Vector2(-310,-224),new Vector2(280,64),()=>VRLearningGuide.ShowControls(transform));
        InspectionUI.Button(_panel.transform,"Обучение",new Vector2(0,-224),new Vector2(280,64),BeginLearning);
        InspectionUI.Button(_panel.transform,"В меню",new Vector2(310,-224),new Vector2(280,64),()=>AppSession.Load("Startup"));
        MedicalTVScreen.Present(_panel);
        InspectionVRInput.RegisterCanvas(_panel);
        if(AppSession.Mode==AppMode.Learning)BeginLearning();
    }
    private void BeginLearning()
    {
        if(GetComponent<VRLearningGuide>())return;
        _panel.gameObject.SetActive(false);AppSession.SelectMode(AppMode.Learning);
        var guide=gameObject.AddComponent<VRLearningGuide>();guide.Finished+=()=>{AppSession.SelectMode(AppMode.Study);_panel.gameObject.SetActive(true);};
    }
    private void OnDestroy(){MedicalTVScreen.Dismiss(_panel);InspectionUI.Forget(_panel);}
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        SceneManager.sceneLoaded-=SceneLoaded;SceneManager.sceneLoaded+=SceneLoaded;SceneLoaded(SceneManager.GetActiveScene(),LoadSceneMode.Single);
    }
    private static void SceneLoaded(Scene scene,LoadSceneMode mode)
    {
        AppSession.BindSceneContext();
        if(scene.name=="SampleScene"&&!FindAnyObjectByType<StudyModePanel>())new GameObject("Study Mode Navigation").AddComponent<StudyModePanel>();
    }
}
