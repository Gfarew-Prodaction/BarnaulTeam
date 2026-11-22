using System.Collections.Generic;
using DataScripts;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;
using UnityEngine.XR;

public class StartupFlow : MonoBehaviour
{
    [SerializeField] private string _lobbyScene="SampleScene";
    [SerializeField] private string _practiceScene="PracticeScene";
    private enum Page { Loading, Modes, Difficulty, Set, Entering }
    private Page _page;
    private RemoteElementContent _content;
    private Text _title,_status;
    private Slider _progress;
    private Button _retry,_offline;
    private Transform _head,_panel;
    private readonly List<Button> _buttons=new List<Button>();
    private readonly Dictionary<Page,GameObject> _pages=new Dictionary<Page,GameObject>();
    private readonly bool[] _pressed=new bool[2];
    private readonly bool[] _tracked=new bool[2];
    private readonly LineRenderer[] _rays=new LineRenderer[2];
    private readonly List<Material> _materials=new List<Material>();
    public string CurrentPage => _page.ToString();
    private void Awake()
    {
        _content=RemoteElementContent.Instance;
        if(!_content){var service=new GameObject("Persistent Element Content");_content=service.AddComponent<RemoteElementContent>();DontDestroyOnLoad(service);}
        var cameraObject=new GameObject("Startup Camera",typeof(Camera),typeof(AudioListener));cameraObject.transform.SetParent(transform);cameraObject.tag="MainCamera";cameraObject.transform.localPosition=new Vector3(0,1.6f,0);_head=cameraObject.transform;
        var camera=cameraObject.GetComponent<Camera>();camera.backgroundColor=MedicalUITheme.Screen;camera.clearFlags=CameraClearFlags.SolidColor;
        if(!EventSystem.current){var events=new GameObject("Startup EventSystem",typeof(EventSystem),typeof(InputSystemUIInputModule));events.transform.SetParent(transform);}
        BuildPanel();Switch(Page.Loading);
    }
    private void BuildPanel()
    {
        var panel=new GameObject("Startup Panel",typeof(Canvas),typeof(CanvasScaler),typeof(GraphicRaycaster));panel.transform.SetParent(transform);_panel=panel.transform;
        var canvas=panel.GetComponent<Canvas>();canvas.renderMode=RenderMode.WorldSpace;canvas.worldCamera=_head.GetComponent<Camera>();
        var rect=panel.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(900,760);rect.position=new Vector3(0,1.55f,2);rect.localScale=Vector3.one*.0019f;
        var bg=panel.AddComponent<Image>();MedicalUITheme.Surface(bg,MedicalUITheme.Paper);bg.raycastTarget=false;
        _title=Label(_panel,"HeadDogVR",new Vector2(0,285),new Vector2(820,70),36);
        _status=Label(_panel,"Проверка данных…",new Vector2(0,192),new Vector2(800,105),25);
        var bar=new GameObject("Progress",typeof(RectTransform),typeof(Slider));bar.transform.SetParent(_panel,false);var br=bar.GetComponent<RectTransform>();br.sizeDelta=new Vector2(700,12);br.anchoredPosition=new Vector2(0,-330);bar.AddComponent<Image>().color=new Color(.78f,.83f,.83f);
        var fill=new GameObject("Fill",typeof(RectTransform),typeof(Image));fill.transform.SetParent(bar.transform,false);var fr=fill.GetComponent<RectTransform>();fr.anchorMin=Vector2.zero;fr.anchorMax=Vector2.one;fr.offsetMin=fr.offsetMax=Vector2.zero;fill.GetComponent<Image>().color=MedicalUITheme.Teal;
        _progress=bar.GetComponent<Slider>();_progress.fillRect=fr;_progress.interactable=false;
        foreach(Page page in new[]{Page.Loading,Page.Modes,Page.Difficulty,Page.Set}){var group=new GameObject(page.ToString(),typeof(RectTransform));group.transform.SetParent(_panel,false);_pages[page]=group;}
        var loading=_pages[Page.Loading].transform;
        _retry=Button(loading,"Повторить загрузку",-50,()=>{_content.Refresh();Switch(Page.Loading);});
        _offline=Button(loading,"Продолжить без интернета",-150,ShowMenu);
        var modes=_pages[Page.Modes].transform;
        Button(modes,"Обучение",65,()=>EnterMode(AppMode.Learning));
        Button(modes,"Изучение",-40,()=>EnterMode(AppMode.Study));
        Button(modes,"Практика",-145,()=>EnterMode(AppMode.Practice));
        var levels=_pages[Page.Difficulty].transform;
        Button(levels,"Начальный",65,()=>ChooseDifficulty(PracticeDifficulty.Beginner));
        Button(levels,"Средний",-40,()=>ChooseDifficulty(PracticeDifficulty.Intermediate));
        Button(levels,"Продвинутый",-145,()=>ChooseDifficulty(PracticeDifficulty.Advanced));
        Button(levels,"Назад",-260,ShowMenu,220,54);
        var sets=_pages[Page.Set].transform;
        Button(sets,"Кости",95,()=>ChooseSet(PracticeSet.Bones),760,68);
        Button(sets,"Мышцы",10,()=>ChooseSet(PracticeSet.Muscles),760,68);
        Button(sets,"Мозг",-75,()=>ChooseSet(PracticeSet.Brain),760,68);
        Button(sets,"Всё вместе",-160,()=>ChooseSet(PracticeSet.All),760,68);
        Button(sets,"Назад",-260,()=>Switch(Page.Difficulty),220,54);
    }
    private Text Label(Transform parent,string value,Vector2 position,Vector2 size,int fontSize)
    {
        var go=new GameObject(value,typeof(RectTransform),typeof(Text));go.transform.SetParent(parent,false);var rect=go.GetComponent<RectTransform>();rect.anchoredPosition=position;rect.sizeDelta=size;
        var text=go.GetComponent<Text>();text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");text.text=value;text.fontSize=fontSize;text.alignment=TextAnchor.MiddleCenter;text.color=MedicalUITheme.Navy;text.raycastTarget=false;return text;
    }
    private Button Button(Transform parent,string text,float y,UnityEngine.Events.UnityAction action,float width=760,float height=88)
    {
        var go=new GameObject(text.Split('\n')[0],typeof(RectTransform),typeof(Image),typeof(Button));go.transform.SetParent(parent,false);var rect=go.GetComponent<RectTransform>();rect.sizeDelta=new Vector2(width,height);rect.anchoredPosition=new Vector2(0,y);go.GetComponent<Image>().color=MedicalUITheme.Navy;
        var button=go.GetComponent<Button>();button.onClick.AddListener(action);Label(go.transform,text,Vector2.zero,rect.sizeDelta-new Vector2(22,8),23);MedicalUITheme.Button(button);_buttons.Add(button);return button;
    }
    private void Switch(Page page)
    {
        _page=page;foreach(var pair in _pages)pair.Value.SetActive(pair.Key==page);_progress.gameObject.SetActive(page==Page.Loading||page==Page.Entering);
        if(page==Page.Modes){_title.text="HeadDogVR";_status.text="";}
        if(page==Page.Difficulty){_title.text="HeadDogVR";_status.text="Сложность";}
        if(page==Page.Set){_title.text="HeadDogVR";_status.text="Что будем собирать?";}
    }
    public void ShowMenu(){if(_content.HasContent)Switch(Page.Modes);}
    public void EnterMode(AppMode mode)
    {
        if(_page==Page.Entering||!_content.HasContent)return;AppSession.SelectMode(mode);
        if(mode==AppMode.Practice)Switch(Page.Difficulty);else Load(_lobbyScene);
    }
    public void ChooseDifficulty(PracticeDifficulty difficulty){AppSession.SelectDifficulty(difficulty);Switch(Page.Set);}
    public void ChooseSet(PracticeSet set){AppSession.SelectSet(set);Load(_practiceScene);}
    public void EnterLobby()=>EnterMode(AppMode.Study);
    private void Load(string scene)
    {
        if(!AppSession.Load(scene)){_status.text="Не удалось открыть кабинет. Попробуй ещё раз.";return;}
        Switch(Page.Entering);_title.text="HeadDogVR";_status.text="Загрузка…";_progress.value=1;
    }
    private void Update()
    {
        var head=InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);if(head.TryGetFeatureValue(CommonUsages.devicePosition,out var position))_head.localPosition=position;if(head.TryGetFeatureValue(CommonUsages.deviceRotation,out var rotation))_head.localRotation=rotation;
        UpdateController(0,XRNode.LeftHand);UpdateController(1,XRNode.RightHand);
        if(_page!=Page.Loading)return;
        _status.text=_content.Status;_progress.value=_content.Progress;
        bool ready=_content.InitialCheckComplete&&!_content.IsUpdating;
        _retry.gameObject.SetActive(ready&&_content.LastError!=null);_offline.gameObject.SetActive(ready&&_content.LastError!=null&&_content.HasContent);
        if(ready&&_content.LastError==null&&_content.HasContent)ShowMenu();
    }
    private void UpdateController(int index,XRNode node)
    {
        var device=InputDevices.GetDeviceAtXRNode(node);bool valid=device.TryGetFeatureValue(CommonUsages.devicePosition,out var position)&device.TryGetFeatureValue(CommonUsages.deviceRotation,out var rotation);device.TryGetFeatureValue(CommonUsages.triggerButton,out bool pressed);
        if(!valid){if(_rays[index])_rays[index].enabled=false;_pressed[index]=false;_tracked[index]=false;return;}
        if(!_rays[index]){var go=new GameObject("Menu Controller Ray");go.transform.SetParent(transform);var line=go.AddComponent<LineRenderer>();line.positionCount=2;line.startWidth=line.endWidth=.004f;var material=Resources.Load<Material>("UI/CrispPointer");if(!material){material=new Material(Shader.Find("UI/Default"));material.color=Color.cyan;_materials.Add(material);}line.sharedMaterial=material;_rays[index]=line;}
        Vector3 origin=transform.TransformPoint(position),direction=transform.rotation*rotation*Vector3.forward;_rays[index].enabled=true;_rays[index].SetPosition(0,origin);_rays[index].SetPosition(1,origin+direction*3);
        if(_tracked[index]&&pressed&&!_pressed[index])foreach(var button in _buttons){if(!button.gameObject.activeInHierarchy||!button.interactable)continue;var rect=(RectTransform)button.transform;var plane=new Plane(rect.forward,rect.position);if(plane.Raycast(new Ray(origin,direction),out float distance)&&distance<=4&&rect.rect.Contains((Vector2)rect.InverseTransformPoint(origin+direction*distance))){button.onClick.Invoke();break;}}
        _pressed[index]=pressed;_tracked[index]=true;
    }
    private void OnDestroy(){foreach(var material in _materials)if(material)Destroy(material);}
}
