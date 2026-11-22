using System;
using System.Linq;
using HurricaneVR.Framework.Core;
using HurricaneVR.Framework.ControllerInput;
using InteracrSystem;
using UnityEngine;
using UnityEngine.UI;

public class VRLearningGuide : MonoBehaviour
{
    private static VRLearningGuide _active;
    private static Canvas _help;
    public static bool HasOpenGuide => _active || _help;
    private void Awake(){_active=this;}
    public const string Controls = "ХОДЬБА: левый стик — движение; правый — поворот персонажа.\nХВАТ: поднеси руку и удерживай боковую кнопку Grip. Отпусти Grip — отпустишь предмет. Для притягивания наведи свободную руку на доступную деталь и удерживай Grip.\nИНТЕРФЕЙС: наведи луч на кнопку и нажми курок указательным пальцем. Луч появляется возле рабочих столов.\nY (левая рука): возле анатомического стола переключает ходьбу / управление моделью. В режиме модели ходьба, поворот персонажа и телепортация отключены.\nЛЕВЫЙ СТИК В РЕЖИМЕ МОДЕЛИ: влево/вправо — поворот вокруг вертикальной оси; вверх/вниз — наклон. За одно движение работает преобладающая ось.\nПРАВЫЙ СТИК В РЕЖИМЕ МОДЕЛИ: вверх — увеличить, вниз — уменьшить.\nX (левая рука): вернуть исходный ПОВОРОТ модели.\nA (правая рука): вернуть исходный РАЗМЕР модели. X и A действуют только в режиме модели. Y снова возвращает ходьбу; отход от стола тоже выключает режим модели.";
    private struct Step { public string Title,Body,Task;public Func<bool> Done;public Func<Transform> Target;public Step(string t,string b,string task,Func<bool>d,Func<Transform>a){Title=t;Body=b;Task=task;Done=d;Target=a;} }
    private Step[] _steps;private int _index,_baseline;private float _entered,_turn;
    private Vector3 _position,_scale;private Quaternion _rotation;private HVRGrabbable _held;
    private LayerModelController _control;private ChoiceLayer _layers;private LayerElementCatalog _catalog;
    private ElementInspectionStation _station;private ElementTray _tray;private HVRGrabbable[] _items;
    private Canvas _canvas;private Text _title,_body,_task,_progress;private Button _next;private GuidanceArrow _arrow;
    public int StepIndex=>_index;public int StepCount=>_steps?.Length??0;public event Action Finished;
    private Transform Head=>Camera.main?Camera.main.transform:null;
    private bool Held=>_items.Any(i=>i&&i.IsHandGrabbed);
    private Transform Part=>_items.FirstOrDefault(i=>i&&i.gameObject.activeInHierarchy)?.transform;
    private bool Near(Transform t)=>t&&Head&&Vector3.ProjectOnPlane(Head.position-t.position,Vector3.up).magnitude<1.35f;
    private void Start()
    {
        _control=FindAnyObjectByType<LayerModelController>();_layers=FindAnyObjectByType<ChoiceLayer>();_catalog=FindAnyObjectByType<LayerElementCatalog>();_station=FindAnyObjectByType<ElementInspectionStation>();_tray=FindAnyObjectByType<ElementTray>();
        _items=_layers.GetComponentsInChildren<HVRGrabbable>(true);
        _steps=new[]{
            new Step("Добро пожаловать", "Пройдём весь рабочий цикл: перемещение, слои, хват, управление моделью, исследование и практика.\n\nНа левой руке находятся X и Y, на правой — A и B. Боковая кнопка под средним пальцем — Grip; передний курок под указательным — для интерфейса.\n\nШаги проверяют действия. Если действие недоступно, можно пропустить конкретный шаг.","Нажми «Далее», чтобы начать.",null,null),
            new Step("Перемещение", "ЛЕВЫЙ СТИК перемещает персонажа по кабинету. Начни с короткого движения. Перед физическим шагом убедись, что в реальной комнате свободно.","Пройди не менее 30 см левым стиком.",()=>Head&&Vector3.ProjectOnPlane(Head.position-_position,Vector3.up).magnitude>.3f,null),
            new Step("Поворот персонажа", "ПРАВЫЙ СТИК влево или вправо поворачивает персонажа. Поворот головы в шлеме позволяет осматриваться независимо от стика.","Отклони правый стик в сторону.",()=>_turn>.25f,null),
            new Step("Анатомический стол", "На столе стоит модель головы, панель слоёв, каталог деталей и поднос на четыре места. Подойди ближе: здесь доступен луч для интерфейса.","Подойди к модели.",()=>Near(_layers.transform),()=>_layers.transform),
            new Step("Выбор слоя", "Наведи луч контроллера на кнопку «Кости» на первом терминале. Нажми ПЕРЕДНИЙ КУРОК.\n\n«Кожа» показывает покров; «Мышцы» показывает только мышцы; «Кости» — скелет; «Мозг» — отделы мозга.","Выбери слой «Кости».",()=>_layers.CurrentLayer==AnatomyLayer.Bones,()=>_layers.transform.parent?.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="Layer screen anchor")),
            new Step("Каталог элементов", "Второй терминал перечисляет элементы текущего слоя. Нажми на название: все части этого элемента подсветятся ГОЛУБЫМ, а стрелка покажет их положение. Кнопки ← / → листают список. Выделение исчезает через 12 секунд или по кнопке.","Нажми название на втором терминале.",()=>_catalog&&_catalog.SelectionCount>_baseline,()=>_catalog?_catalog.Screen:null),
            new Step("Готовность к захвату", "Поднеси свободную руку к кости. ЗЕЛЁНАЯ подсветка и название появляются, когда деталь доступна для захвата. Голубое выделение каталога — отдельная подсказка.","Найди деталь, которая подсветилась зелёным.",()=>_items.Any(i=>i&&i.TryGetComponent<ElementInteractionVisuals>(out var v)&&v.IsHoverHighlighted),()=>Part),
            new Step("Взять в руку", "Удерживай боковую кнопку GRIP, когда деталь доступна для захвата. Деталь следует за рукой. Поворачивай кисть, чтобы рассмотреть её с разных сторон.","Возьми любую кость.",()=>{_held=_items.FirstOrDefault(i=>i&&i.IsHandGrabbed);return _held;},()=>Part),
            new Step("Карточка и парные части", "Рядом с предметом отображается карточка: русское название, название на латыни и краткая описание. Если элемент состоит из нескольких частей, связанная часть отмечается жёлтым. На исходном месте остаётся прозрачный образ.","Рассмотри карточку и нажми «Далее».",null,()=>_held?_held.transform:Part),
            new Step("Отпустить и вернуть", "Отпусти GRIP вдали от подноса и исследовательской зоны. Предмет плавно вернётся в исходное положение. Он не падает на пол; после возврата его можно снова взять.","Отпусти предмет и дождись возврата.",()=>_held&&!_held.IsBeingHeld&&!_held.IsBeingForcedGrabbed&&_held.Rigidbody.isKinematic&&Time.unscaledTime-_entered>1,()=>_held?_held.transform:Part),
            new Step("Притянуть издалека", "Отойди от детали, направь на неё свободную руку и удерживай GRIP. Предмет притянется в руку. Выбирай объект по направлению руки; не пытайся тянуть уже занятый предмет.","Притяни деталь или пропусти шаг, если стоишь вплотную.",()=>_items.Any(i=>i&&i.IsBeingForcedGrabbed),()=>Part),
            new Step("Поднос", "На подносе четыре свободных места. Отпусти деталь над ним: она останется на подносе вместо возврата в голову. Позже её можно снова взять. Если все места заняты, освободи одно.","Положи предмет на поднос.",()=>_tray&&_tray.OccupiedCount>0,()=>_tray?_tray.transform:null),
            new Step("Y · управление моделью", "Нажми Y на ЛЕВОМ контроллере рядом со столом. Ходьба, поворот персонажа и телепортация временно отключатся. Стики теперь управляют всей моделью, а не персонажем. Положение головы в шлеме продолжает отслеживаться.","Нажми Y: включи режим модели.",()=>_control.IsEditing,()=>_layers.transform),
            new Step("Левый стик · поворот", "В режиме модели: левый стик влево/вправо поворачивает голову вокруг вертикальной оси; вверх/вниз наклоняет её. Преобладающая ось выбирается автоматически, чтобы движение было предсказуемым.","Поверни модель левым стиком.",()=>Quaternion.Angle(_rotation,_control.Model.localRotation)>12,()=>_layers.transform),
            new Step("X · сброс поворота", "Нажми X на ЛЕВОМ контроллере. Восстановится исходная ориентация модели. Размер при этом не меняется. Эта кнопка действует в режиме модели.","Нажми X.",()=>_control.RotationResetCount>_baseline,()=>_layers.transform),
            new Step("Правый стик · размер", "В режиме модели отклони ПРАВЫЙ стик вверх, чтобы увеличить голову, вниз — чтобы уменьшить. Размер ограничен; детали сохраняют взаимное расположение.","Измени размер правым стиком.",()=>Vector3.Distance(_scale,_control.Model.localScale)>.025f,()=>_layers.transform),
            new Step("A · сброс размера", "Нажми A на ПРАВОМ контроллере. Восстановится исходный масштаб. Поворот при этом не меняется. Это не кнопка сброса всей сцены.","Нажми A.",()=>_control.ScaleResetCount>_baseline,()=>_layers.transform),
            new Step("Y · снова ходьба", "Ещё раз нажми Y: стики снова двигают и поворачивают персонажа. Если физически отойти от стола, режим модели также выключится автоматически.","Верни режим перемещения кнопкой Y.",()=>!_control.IsEditing,()=>_layers.transform),
            new Step("Исследовательский стол", "У другого стола находится компьютер с подробным описанием и зона для предмета. Возьми деталь с собой и подойди к компьютеру.","Подойди к исследовательскому столу.",()=>_station&&Near(_station.transform),()=>_station?_station.transform:null),
            new Step("Положить в зону", "Отпусти деталь в исследовательской зоне перед компьютером. На экране появятся название, латынь и подробный текст. Вокруг верхней части стола появятся фотографии.","Положи предмет в зону исследования.",()=>_station&&_station.CurrentElement,()=>_station?_station.transform:null),
            new Step("Подробное описание", "Наведи луч на «Ниже» или «Выше» и нажми передний курок. Эти кнопки прокручивают подробное описание. Если сервер недоступен, используются ранее сохранённые данные, когда они есть.","Нажми кнопку прокрутки на компьютере.",()=>_station&&_station.DescriptionScrollCount>_baseline,()=>_station?_station.transform:null),
            new Step("Фотографии", "Наведи луч на круг с фотографией и нажми передний курок. Круг раскроется в большую карточку с изображением и подписью. Фотографии видны рядом со столом; при отходе скрываются. Если у детали нет фото, пропусти шаг.","Открой любую фотографию.",()=>_station&&_station.PhotoOpenCount>_baseline,()=>_station?_station.PhotoGuidanceTarget:null),
            new Step("Закрыть фотографию", "На большой карточке нажми «Закрыть» передним курком. Карточка свернётся обратно в круг. Можно открыть другую фотографию.","Закрой фотографию.",()=>_station&&_station.PhotoCloseCount>_baseline,()=>_station?_station.PhotoGuidanceTarget:null),
            new Step("Как устроена практика", "В меню выбери «Практика», сложность и набор: кости, мышцы, мозг или всё вместе.\n\nНа одном столе лежат детали, на другом — подставка для сборки. Возьми деталь GRIP, поворотом кисти совмести её ориентацию и отпусти в нужном месте. При сборке костей и мышц верная деталь закрепится; неверная вернётся на стол. Детали мозга остаются там, где ты их отпустил на сборке: результат оценивает кнопка «Проверить сборку». Экран показывает прогресс, время и ошибки. «Заново» перезапускает сборку.","Нажми «Далее».",null,null),
            new Step("Неврологическая проверка", "В практике мозга можно оставить сборку неполной или поставить деталь неверно. Отпусти все детали и нажми «Проверить сборку» на телевизоре.\n\nСобака в центре кабинета покажет учебные сценарии: обычную походку при верной сборке, возможное кружение, атаксию или нарушение равновесия при соответствующих ошибках. Если нарушений несколько, они идут по очереди. На экране указаны ошибочные отделы.\n\n«Продолжить сборку» возвращает к исправлению. После правки нажми «Проверить сборку» ещё раз.","Нажми «Далее».",null,null),
            new Step("Сложность и завершение", "НАЧАЛЬНЫЙ: названия и прозрачные контуры свободных мест.\nСРЕДНИЙ: названия, без контуров; одна опорная деталь уже закреплена.\nПРОДВИНУТЫЙ: без названий и контуров; одна закреплённая деталь задаёт положение и ориентацию сборки. Она входит в счётчик и не снимается.\n\nОстальные детали собираешь самостоятельно. Справку по кнопкам можно открыть через «Управление». Теперь можно перейти к свободному изучению.","Нажми «Завершить».",null,null)
        };
        _canvas=InspectionUI.Panel("Learning · Detailed guide",transform,new Vector2(1000,562),.0009f);
        _progress=InspectionUI.Text(_canvas.transform,new Vector2(-130,235),new Vector2(650,34),22,InspectionUI.Accent);
        _title=InspectionUI.Text(_canvas.transform,new Vector2(-130,173),new Vector2(650,76),34,InspectionUI.Ink);
        _body=InspectionUI.Text(_canvas.transform,new Vector2(-130,-8),new Vector2(650,285),25,InspectionUI.Ink);
        _body.resizeTextForBestFit=true;_body.resizeTextMinSize=21;_body.resizeTextMaxSize=25;
        _task=InspectionUI.Text(_canvas.transform,new Vector2(-130,-207),new Vector2(650,98),24,InspectionUI.Accent);
        InspectionUI.Image(_canvas.transform,"Column divider",new Vector2(220,0),new Vector2(2,480),InspectionUI.Accent);
        _next=InspectionUI.Button(_canvas.transform,"Далее",new Vector2(355,168),new Vector2(230,80),Next);
        InspectionUI.Button(_canvas.transform,"Назад",new Vector2(355,69),new Vector2(230,65),()=>Enter(Mathf.Max(0,_index-1)));
        InspectionUI.Button(_canvas.transform,"Пропустить шаг",new Vector2(355,-15),new Vector2(230,65),Next);
        InspectionUI.Button(_canvas.transform,"Управление",new Vector2(355,-115),new Vector2(230,65),()=>ShowControls(transform));
        InspectionUI.Button(_canvas.transform,"В меню",new Vector2(355,-215),new Vector2(230,65),()=>AppSession.Load("Startup"));
        MedicalTVScreen.Present(_canvas);
        _arrow=new GameObject("Tutorial guidance arrow").AddComponent<GuidanceArrow>();_arrow.transform.SetParent(transform,false);_arrow.gameObject.SetActive(false);
        InspectionVRInput.RegisterCanvas(_canvas);Enter(0);
    }
    private void Enter(int index)
    {
        _index=index;_entered=Time.unscaledTime;_turn=0;_position=Head?Head.position:Vector3.zero;_rotation=_control.Model.localRotation;_scale=_control.Model.localScale;
        _baseline=index==5&&_catalog?_catalog.SelectionCount:index==14?_control.RotationResetCount:index==16?_control.ScaleResetCount:index==20&&_station?_station.DescriptionScrollCount:index==21&&_station?_station.PhotoOpenCount:index==22&&_station?_station.PhotoCloseCount:0;
        var step=_steps[index];_progress.text=$"Обучение  {index+1} / {_steps.Length}";_title.text=step.Title;_body.text=step.Body;_task.text=step.Task;
        _next.GetComponentInChildren<Text>().text=index==_steps.Length-1?"Завершить":step.Done==null?"Далее":"Выполни задание";_next.interactable=step.Done==null;
        var target=step.Target?.Invoke();if(target)_arrow.PointAt(target,step.Title);else _arrow.gameObject.SetActive(false);Reposition();
    }
    private void Update()
    {
        if(_steps==null)return;
        if(_index==2&&HVRInputManager.Instance&&HVRInputManager.Instance.RightController&&Mathf.Abs(HVRInputManager.Instance.RightController.JoystickAxis.x)>.4f)_turn+=Time.unscaledDeltaTime;
        if(_steps[_index].Done!=null&&_steps[_index].Done()) {_next.interactable=true;_next.GetComponentInChildren<Text>().text="Далее";_task.text="Готово! Нажми «Далее».";}
    }
    private void Next(){if(_index+1<_steps.Length)Enter(_index+1);else {Finished?.Invoke();Destroy(this);}}
    private void Reposition()
    {
        if(!_canvas)return;
        if(_canvas.GetComponentInParent<MedicalTVScreen>())return;
        if(!Head)return;var forward=Vector3.ProjectOnPlane(Head.forward,Vector3.up).normalized;if(forward.sqrMagnitude<.1f)forward=Vector3.forward;
        var right=Vector3.Cross(Vector3.up,forward);
        var target=_steps!=null?_steps[_index].Target?.Invoke():null;
        float side=target&&Vector3.Dot(target.position-Head.position,right)<-.1f?1:-1;
        _canvas.transform.position=Head.position+forward*1.05f+right*(side*.72f)-Vector3.up*.12f;
        _canvas.transform.rotation=Quaternion.LookRotation(_canvas.transform.position-Head.position);
    }
    private void OnDestroy(){if(_active==this)_active=null;MedicalTVScreen.Dismiss(_canvas);InspectionUI.Forget(_canvas);if(_canvas)Destroy(_canvas.gameObject);if(_arrow)Destroy(_arrow.gameObject);}
    public static void ShowControls(Transform owner)
    {
        if(_help){MedicalTVScreen.Dismiss(_help);InspectionUI.Forget(_help);Destroy(_help.gameObject);}
        var panel=InspectionUI.Panel("Controls · Help",owner,new Vector2(1000,562),.001f);_help=panel;
        var head=Camera.main?Camera.main.transform:owner;panel.transform.position=head.position+head.forward*1.1f;panel.transform.rotation=Quaternion.LookRotation(panel.transform.position-head.position);
        var title=InspectionUI.Text(panel.transform,new Vector2(0,218),new Vector2(900,60),34,InspectionUI.Accent);
        var body=InspectionUI.Text(panel.transform,new Vector2(0,5),new Vector2(900,325),27,InspectionUI.Ink);
        body.resizeTextForBestFit=true;body.resizeTextMinSize=22;body.resizeTextMaxSize=27;
        var paragraphs=Controls.Split('\n');int page=0;int pages=Mathf.CeilToInt(paragraphs.Length/3f);
        Action render=()=>{title.text=$"Управление  {page+1} из {pages}";body.text=string.Join("\n\n",paragraphs.Skip(page*3).Take(3));};
        InspectionUI.Button(panel.transform,"← Назад",new Vector2(-310,-220),new Vector2(280,68),()=>{page=Mathf.Max(0,page-1);render();});
        InspectionUI.Button(panel.transform,"Закрыть",new Vector2(0,-220),new Vector2(280,68),()=>{MedicalTVScreen.Dismiss(panel);InspectionUI.Forget(panel);Destroy(panel.gameObject);});
        InspectionUI.Button(panel.transform,"Далее →",new Vector2(310,-220),new Vector2(280,68),()=>{page=Mathf.Min(pages-1,page+1);render();});
        render();MedicalTVScreen.Present(panel);InspectionVRInput.RegisterCanvas(panel);
    }
}
