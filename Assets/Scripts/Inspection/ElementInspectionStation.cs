using System;
using System.Collections.Generic;
using DG.Tweening;
using DataScripts;
using InteracrSystem;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(BoxCollider))]
public class ElementInspectionStation : MonoBehaviour
{
    public int DescriptionScrollCount { get; private set; }
    public int PhotoOpenCount { get; private set; }
    public int PhotoCloseCount { get; private set; }
    public Transform PhotoGuidanceTarget => _bubbles.Count > 0 && _bubbles[0] ? _bubbles[0].transform : transform;

    [SerializeField] private Transform _computer;
    [SerializeField] private Transform _screenAnchor;
    [SerializeField] private Transform _galleryCenter;
    [SerializeField, Min(.1f)] private float _galleryRadius = .65f;
    [SerializeField, Min(0)] private float _bubbleGap = .06f;
    [SerializeField] private float _orbitSpeed = 2.5f;
    [SerializeField, Min(.2f)] private float _playerDistance = 1.6f;
    [SerializeField, Min(0)] private float _distanceHysteresis = .25f;
    private bool _playerNearby;
    private float _orbitAngle;
    [SerializeField] private float _galleryHeight = .6f;
    [SerializeField] private Vector2 _monitorSize = new Vector2(.6f, .44f);
    private static readonly List<ElementInspectionStation> Active = new List<ElementInspectionStation>();
    private BoxCollider _zone;
    private OnMoveBackItem _occupant;
    private ElementDog _element;
    private RemoteElement _shown;
    public ElementDog CurrentElement => _element;
    private Canvas _monitor, _viewer, _zoneMarker;
    private Text _title, _latin, _description, _counter, _viewerTitle, _viewerCaption;
    private Image _viewerImage;
    private ScrollRect _scroll;
    private readonly List<Canvas> _bubbles = new List<Canvas>();
    private readonly List<RemoteElementImage> _photos = new List<RemoteElementImage>();
    private Sprite _circle;
    private Texture2D _circleTexture;
    private Camera _camera;
    private bool _built;
    private float _gallerySide = 1;
    private Canvas _openedBubble;
    private Tween _morphTween;
    private MorphPanelGraphic _viewerOutline, _photoMask;
    private CanvasGroup _viewerDetails;
    private float _morph;
    private const float ViewerScale = .00075f;
    private const float BubbleScale = .001f;


    private void Awake() { _zone = GetComponent<BoxCollider>(); }
    private void OnEnable() { if (!Active.Contains(this)) Active.Add(this); if (_monitor) _monitor.gameObject.SetActive(true); if (_zoneMarker) _zoneMarker.gameObject.SetActive(true); }
    private void Start()
    {
        InspectionVRInput.EnsureModule();
        if (!GetComponent<InspectionVRInput>()) gameObject.AddComponent<InspectionVRInput>();
        if (_computer && Camera.main)
            _gallerySide = Vector3.Dot(_computer.right, Camera.main.transform.position - _computer.position) < 0 ? -1 : 1;
        CreateCircle(); BuildMonitor(); BuildViewer(); BuildZoneMarker(); _built = true; ShowEmpty();
    }
    private void Update()
    {
        if (!_camera || !_camera.isActiveAndEnabled) _camera = Camera.main;
        if (!_built) return;
        if (_occupant && !_occupant.gameObject.activeInHierarchy) Release(_occupant);
        var data = _element ? _element.Data : null;
        if (data != _shown) ShowData(data);
        PositionMonitor();
        _zoneMarker.transform.position = new Vector3(_zone.bounds.center.x, _zone.bounds.min.y + .006f, _zone.bounds.center.z);
        UpdateProximity();
        // Close the expanded photo too, so no floating UI stays behind when leaving the table.
        if (!_playerNearby && (_openedBubble || _viewer.gameObject.activeSelf)) ResetViewer();
        foreach (var bubble in _bubbles)
        {
            bool visible = _playerNearby && bubble != _openedBubble;
            if (bubble.gameObject.activeSelf != visible) bubble.gameObject.SetActive(visible);
        }
        Vector3 center = (_galleryCenter ? _galleryCenter.position : (_computer ? _computer.position : _zone.bounds.center))
            + Vector3.up * _galleryHeight;
        _orbitAngle = Mathf.Repeat(_orbitAngle + _orbitSpeed * Time.deltaTime, 360f);
        // Chord spacing includes the caption width, not just the photo diameter.
        const float cardWidth = 314f * BubbleScale;
        // The orbit is horizontal: changing the anchor's height must not change its radius.
        float radius = _galleryCenter && _computer
            ? Vector3.ProjectOnPlane(_computer.position - _galleryCenter.position, Vector3.up).magnitude
            : _galleryRadius;
        radius = Mathf.Max(.1f, radius);
        if (_bubbles.Count > 1)
            radius = Mathf.Max(radius, (cardWidth + _bubbleGap) / (2 * Mathf.Sin(Mathf.PI / _bubbles.Count)));
        for (int i = 0; i < _bubbles.Count; i++)
        {
            var bubble = _bubbles[i];
            float angle = (_orbitAngle + i * 360f / _bubbles.Count) * Mathf.Deg2Rad;
            Vector3 toward = _computer ? Vector3.ProjectOnPlane(_computer.right * _gallerySide, Vector3.up).normalized : Vector3.back;
            if (toward.sqrMagnitude < .01f) toward = Vector3.back;
            Vector3 right = Vector3.Cross(Vector3.up, toward).normalized;
            bubble.transform.position = center + right * (Mathf.Sin(angle) * radius)
                - toward * (Mathf.Cos(angle) * radius);
            if (_camera) bubble.transform.rotation = Quaternion.LookRotation(bubble.transform.position - _camera.transform.position, Vector3.up);
        }
    }

    private void UpdateProximity()
    {
        if (!_camera) { _playerNearby = false; return; }
        Vector3 anchor = _computer ? _computer.position : _zone.bounds.center;
        float distance = Vector3.ProjectOnPlane(_camera.transform.position - anchor, Vector3.up).magnitude;
        _playerNearby = distance <= _playerDistance + (_playerNearby ? _distanceHysteresis : 0);
    }

    public static bool IsPlayerNearTable()
    {
        foreach (var station in Active)
        {
            if (!station) continue;
            if (!station._camera) station._camera = Camera.main;
            station.UpdateProximity();
            if (station._playerNearby) return true;
        }
        return false;
    }

    public static bool TryReserve(OnMoveBackItem item, out ElementInspectionStation station, out Vector3 position, out Quaternion rotation)
    {
        station = null; position = default; rotation = item.transform.rotation;
        float nearest = float.PositiveInfinity;
        foreach (var candidate in Active)
        {
            if (!candidate || !candidate._zone || candidate._occupant || !item.GetComponent<ElementDog>()) continue;
            var bounds = candidate._zone.bounds; bounds.Expand(.12f);
            if (!bounds.Contains(item.transform.position)) continue;
            float distance = (candidate._zone.bounds.center - item.transform.position).sqrMagnitude;
            if (distance < nearest) { station = candidate; nearest = distance; }
        }
        if (!station) return false;
        station._occupant = item; station._element = item.GetComponent<ElementDog>();
        position = station._zone.bounds.center;
        float bottom = float.PositiveInfinity;
        foreach (var col in item.GetComponentsInChildren<Collider>())
            if (col.enabled && !col.isTrigger) bottom = Mathf.Min(bottom, col.bounds.min.y);
        if (!float.IsPositiveInfinity(bottom))
            position.y = Mathf.Max(position.y, station._zone.bounds.min.y + item.transform.position.y - bottom + .02f);
        if (station._built) station.ShowData(station._element.Data);
        return true;
    }
    public void Release(OnMoveBackItem item)
    {
        if (_occupant != item) return;
        _occupant = null; _element = null; _shown = null;
        if (_built) ShowEmpty();
    }

    private void BuildMonitor()
    {
        _monitor = InspectionUI.Panel("Computer · Element Details", transform, new Vector2(900, 660), _monitorSize.x / 900);
        _monitor.transform.SetParent(null, true);
        _monitor.transform.localScale = Vector3.one * (_monitorSize.x / 900);
        var brand = InspectionUI.Text(_monitor.transform, new Vector2(0, 291), new Vector2(820, 30), 16, InspectionUI.Accent); brand.text = "Информация об элементе";
        _title = InspectionUI.Text(_monitor.transform, new Vector2(0, 232), new Vector2(820, 68), 38, InspectionUI.Ink);
        _title.resizeTextForBestFit = true; _title.resizeTextMinSize = 24; _title.resizeTextMaxSize = 38;
        _latin = InspectionUI.Text(_monitor.transform, new Vector2(0, 176), new Vector2(820, 40), 24, InspectionUI.Accent); _latin.fontStyle = FontStyle.Italic;
        InspectionUI.Image(_monitor.transform, "Rule", new Vector2(0, 142), new Vector2(820, 2), InspectionUI.Accent);
        var viewport = InspectionUI.Image(_monitor.transform, "Description Viewport", new Vector2(0, -40), new Vector2(820, 330), Color.clear);
        viewport.gameObject.AddComponent<RectMask2D>();
        _scroll = viewport.gameObject.AddComponent<ScrollRect>(); _scroll.horizontal = false; _scroll.viewport = viewport.rectTransform;
        _description = InspectionUI.Text(viewport.transform, Vector2.zero, new Vector2(800, 330), 25, InspectionUI.Ink);
        var content = _description.rectTransform; content.anchorMin = new Vector2(.5f, 1); content.anchorMax = new Vector2(.5f, 1); content.pivot = new Vector2(.5f, 1); content.anchoredPosition = Vector2.zero;
        _scroll.content = content; _scroll.movementType = ScrollRect.MovementType.Clamped;
        InspectionUI.Button(_monitor.transform, "Выше", new Vector2(-310, -240), new Vector2(180, 42), () => { DescriptionScrollCount++; _scroll.verticalNormalizedPosition = Mathf.Clamp01(_scroll.verticalNormalizedPosition + .2f); });
        InspectionUI.Button(_monitor.transform, "Ниже", new Vector2(-110, -240), new Vector2(180, 42), () => { DescriptionScrollCount++; _scroll.verticalNormalizedPosition = Mathf.Clamp01(_scroll.verticalNormalizedPosition - .2f); });
        var galleryLabel = InspectionUI.Text(_monitor.transform, new Vector2(220, -240), new Vector2(380, 42), 19, InspectionUI.Muted);
        galleryLabel.text = "Фотографии";
        _counter = InspectionUI.Text(_monitor.transform, new Vector2(0, -298), new Vector2(820, 32), 18, InspectionUI.Accent);
    }
    private void PositionMonitor()
    {
        if (_screenAnchor)
        {
            _monitor.transform.SetPositionAndRotation(_screenAnchor.position, _screenAnchor.rotation);
            _monitor.GetComponent<RectTransform>().sizeDelta = new Vector2(900, _monitorSize.y * 900 / _monitorSize.x);
            return;
        }
        if (!_computer) return;
        Vector3 normal = _computer.right;
        if (_camera && Vector3.Dot(normal, _camera.transform.position - _computer.position) < 0) normal = -normal;
        _monitor.transform.position = _computer.position + normal * (Mathf.Abs(_computer.lossyScale.x) * .5f + .004f);
        _monitor.transform.rotation = Quaternion.LookRotation(-normal, _computer.up);
        _monitor.GetComponent<RectTransform>().sizeDelta = new Vector2(900, _monitorSize.y * 900 / _monitorSize.x);
    }
    private void BuildZoneMarker()
    {
        _zoneMarker = InspectionUI.Panel("Inspection · Placement area", transform, new Vector2(300, 300), .001f);
        _zoneMarker.transform.SetParent(null, true);
        _zoneMarker.transform.localScale = Vector3.one * .001f;
        _zoneMarker.transform.rotation = Quaternion.Euler(90, 0, 0);
        var background = _zoneMarker.transform.Find("Background").GetComponent<Image>();
        background.sprite = _circle; background.color = new Color(.88f, .96f, .95f, .95f);
        var label = InspectionUI.Text(_zoneMarker.transform, Vector2.zero, new Vector2(230, 100), 23, InspectionUI.Accent);
        label.text = "Положи\nдеталь сюда"; label.alignment = TextAnchor.MiddleCenter;
    }
    private void BuildViewer()
    {
        _viewer = InspectionUI.Panel("Photo · Morphing View", transform, new Vector2(980, 800), ViewerScale);
        _viewer.transform.SetParent(null, true);
        _viewer.transform.localScale = Vector3.one * ViewerScale;
        _viewer.transform.Find("Background").gameObject.SetActive(false);
        var background = new GameObject("Morphing card", typeof(RectTransform), typeof(MorphPanelGraphic));
        background.transform.SetParent(_viewer.transform, false);
        var bgRect = background.GetComponent<RectTransform>(); bgRect.anchorMin = Vector2.zero; bgRect.anchorMax = Vector2.one; bgRect.offsetMin = bgRect.offsetMax = Vector2.zero;
        _viewerOutline = background.GetComponent<MorphPanelGraphic>(); _viewerOutline.color = InspectionUI.Background;
        var frame = new GameObject("Morphing photo mask", typeof(RectTransform), typeof(MorphPanelGraphic), typeof(Mask));
        frame.transform.SetParent(_viewer.transform, false);
        _photoMask = frame.GetComponent<MorphPanelGraphic>(); _photoMask.color = Color.white; _photoMask.raycastTarget = false;
        frame.GetComponent<Mask>().showMaskGraphic = false;
        _viewerImage = InspectionUI.Image(frame.transform, "Full photo", Vector2.zero, new Vector2(900,550), Color.white);
        _viewerImage.preserveAspect = true;
        var details = new GameObject("Photo details", typeof(RectTransform), typeof(CanvasGroup)); details.transform.SetParent(_viewer.transform, false);
        var rect = details.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = rect.offsetMax = Vector2.zero;
        _viewerDetails = details.GetComponent<CanvasGroup>();
        _viewerTitle = InspectionUI.Text(details.transform, new Vector2(-60,345), new Vector2(790,65), 32, InspectionUI.Ink);
        _viewerCaption = InspectionUI.Text(details.transform, new Vector2(0,-310), new Vector2(900,100), 24, InspectionUI.Muted);
        InspectionUI.Button(details.transform,"Закрыть",new Vector2(375,350),new Vector2(160,55),ClosePhoto);
        _viewer.gameObject.SetActive(false);
    }
    private void SetMorph(float value)
    {
        _morph=value;
        _viewer.GetComponent<RectTransform>().sizeDelta=Vector2.Lerp(new Vector2(266,266),new Vector2(980,800),value);
        _viewerOutline.Roundness=Mathf.Lerp(1,.065f,value);
        _viewerOutline.color=Color.Lerp(InspectionUI.Accent,InspectionUI.Background,value);
        _photoMask.rectTransform.sizeDelta=Vector2.Lerp(new Vector2(250,250),new Vector2(900,550),value);
        _photoMask.rectTransform.anchoredPosition=Vector2.Lerp(Vector2.zero,new Vector2(0,15),value);
        _photoMask.Roundness=Mathf.Lerp(1,.035f,value);
        _viewerImage.rectTransform.sizeDelta=_photoMask.rectTransform.sizeDelta;
        _viewerDetails.alpha=Mathf.InverseLerp(.4f,.95f,value);
        _viewerDetails.blocksRaycasts=value>.98f;
        _viewerDetails.interactable=value>.98f;
    }
    private void ClosePhoto()
    {
        PhotoCloseCount++;
        _morphTween?.Kill();
        if (!_openedBubble) { ResetViewer(); return; }
        Vector3 from=_viewer.transform.position;
        Quaternion rotation=_viewer.transform.rotation;
        Vector3 scale=_viewer.transform.localScale;
        float start=_morph;
        _morphTween=DOVirtual.Float(0,1,.48f,t =>
        {
            if (!_openedBubble) return;
            Vector3 target=_openedBubble.transform.TransformPoint(new Vector3(0,38,0));
            _viewer.transform.position=Vector3.Lerp(from,target,t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*.025f);
            _viewer.transform.rotation=Quaternion.Slerp(rotation,_openedBubble.transform.rotation,t);
            _viewer.transform.localScale=Vector3.Lerp(scale,Vector3.one*BubbleScale,t);
            SetMorph(Mathf.Lerp(start,0,t));
        }).SetEase(Ease.InOutCubic).SetUpdate(true).OnComplete(ResetViewer);
    }
    private void ResetViewer()
    {
        _morphTween?.Kill(); _morphTween=null;
        if (_openedBubble) _openedBubble.gameObject.SetActive(true);
        _openedBubble=null;
        if (_viewer) _viewer.gameObject.SetActive(false);
    }
    private void ShowData(RemoteElement data)
    {
        _shown = data; ClearBubbles(); _photos.Clear();
        if (data == null) { ShowEmpty(); return; }
        _title.text = data.name; _latin.text = data.nameLatin;
        SetDescription(string.IsNullOrWhiteSpace(data.fullDescription) ? data.shortDescription : data.fullDescription);
        if (data.images != null) foreach (var image in data.images) if (image != null) _photos.Add(image);
        if (_photos.Count == 0) foreach (var url in data.imageUrls ?? Array.Empty<string>())
            _photos.Add(new RemoteElementImage { title = data.name, caption = "", url = url });
        BuildBubbles();
    }
    private void ShowEmpty()
    {
        _photos.Clear();
        _title.text = _element ? "Описание недоступно" : "Положи деталь на стол";
        _latin.text = ""; SetDescription(_element ? "Для этой детали пока нет описания." : "Положи деталь в отмеченное место, чтобы посмотреть описание и фотографии.");
        _counter.text = ""; ClearBubbles(); _viewer.gameObject.SetActive(false);
    }
    private void SetDescription(string text)
    {
        _description.text = text ?? "";
        _description.rectTransform.sizeDelta = new Vector2(800, Mathf.Max(330, _description.preferredHeight + 15));
        _scroll.verticalNormalizedPosition = 1;
    }
    private void BuildBubbles()
    {
        ClearBubbles(); _viewer.gameObject.SetActive(false);
        int count = _photos.Count;
        for (int i = 0; i < count; i++)
        {
            var photo = _photos[i];
            var canvas = InspectionUI.Panel("Photo Bubble · " + photo.title, transform, new Vector2(300, 390), BubbleScale);
            canvas.transform.SetParent(null, true);
            canvas.transform.localScale = Vector3.one * BubbleScale;
            canvas.transform.Find("Background").gameObject.SetActive(false);
            InspectionUI.Image(canvas.transform,"Caption backing",new Vector2(0,-141),new Vector2(314,116),InspectionUI.Background);
            var ring = InspectionUI.Image(canvas.transform, "Circle ring", new Vector2(0, 38), new Vector2(266, 266), InspectionUI.Accent); ring.sprite = _circle;
            var mask = InspectionUI.Image(canvas.transform, "Photo mask", new Vector2(0, 38), new Vector2(250, 250), Color.white); mask.sprite = _circle;
            mask.gameObject.AddComponent<Mask>().showMaskGraphic = false;
            mask.alphaHitTestMinimumThreshold = .1f;
            var image = InspectionUI.Image(mask.transform, "Photo", Vector2.zero, new Vector2(250, 250), Color.white);
            image.sprite = _element.GetImage(photo.url);
            if (image.sprite)
            {
                var fit = image.gameObject.AddComponent<AspectRatioFitter>();
                fit.aspectMode = AspectRatioFitter.AspectMode.EnvelopeParent;
                fit.aspectRatio = image.sprite.rect.width / image.sprite.rect.height;
            }
            if (!image.sprite) { image.color = new Color(.86f, .92f, .93f); var label = InspectionUI.Text(image.transform, Vector2.zero, new Vector2(200, 100), 22, InspectionUI.Accent); label.text = "Фотография\nне добавлена"; label.alignment = TextAnchor.MiddleCenter; }
            var button = mask.gameObject.AddComponent<Button>(); button.targetGraphic = mask; mask.raycastTarget = true;
            button.onClick.AddListener(() => OpenPhoto(photo, canvas));
            var title = InspectionUI.Text(canvas.transform, new Vector2(0, -118), new Vector2(300, 45), 23, InspectionUI.Ink); title.text = photo.title; title.alignment = TextAnchor.MiddleCenter;
            var caption = InspectionUI.Text(canvas.transform, new Vector2(0, -163), new Vector2(300, 48), 16, InspectionUI.Accent); caption.text = photo.caption; caption.alignment = TextAnchor.UpperCenter;
            canvas.gameObject.SetActive(_playerNearby);
            _bubbles.Add(canvas);
        }
        _counter.text = _photos.Count == 0 ? "Фотографий пока нет" : $"Фото: {_photos.Count}. Нажми на изображение, чтобы открыть.";
    }
    private void OpenPhoto(RemoteElementImage photo, Canvas source)
    {
        PhotoOpenCount++;
        ResetViewer();
        _openedBubble=source;
        _viewerTitle.text=photo.title; _viewerCaption.text=photo.caption;
        _viewerImage.sprite=_element ? _element.GetImage(photo.url) : null;
        _viewerImage.enabled=_viewerImage.sprite;
        if (!_viewerImage.sprite) _viewerCaption.text="Изображение ещё не добавлено или не загружено.\n"+photo.caption;
        Vector3 from=source.transform.TransformPoint(new Vector3(0,38,0));
        Quaternion rotation=source.transform.rotation;
        Vector3 target=_camera ? _camera.transform.position+_camera.transform.forward*.95f : from;
        Quaternion targetRotation=_camera ? _camera.transform.rotation : rotation;
        _viewer.transform.SetPositionAndRotation(from,rotation);
        _viewer.transform.localScale=Vector3.one*BubbleScale;
        SetMorph(0);
        _viewer.gameObject.SetActive(true);
        source.gameObject.SetActive(false);
        _morphTween=DOVirtual.Float(0,1,.68f,t =>
        {
            SetMorph(t);
            _viewer.transform.position=Vector3.Lerp(from,target,t)+Vector3.up*(Mathf.Sin(t*Mathf.PI)*.045f);
            _viewer.transform.rotation=Quaternion.Slerp(rotation,targetRotation,t);
            _viewer.transform.localScale=Vector3.one*Mathf.Lerp(BubbleScale,ViewerScale,t);
        }).SetEase(Ease.OutCubic).SetUpdate(true).OnComplete(() => _morphTween=null);
    }
    private void ClearBubbles()
    {
        ResetViewer();
        foreach (var canvas in _bubbles) { InspectionUI.Forget(canvas); if (canvas) { canvas.gameObject.SetActive(false); Destroy(canvas.gameObject); } }
        _bubbles.Clear();
    }
    private void CreateCircle()
    {
        const int size = 128; _circleTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
        var colors = new Color[size * size];
        for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            colors[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(63 - Vector2.Distance(new Vector2(x, y), new Vector2(63.5f, 63.5f))));
        _circleTexture.SetPixels(colors); _circleTexture.Apply();
        _circle = Sprite.Create(_circleTexture, new Rect(0, 0, size, size), Vector2.one * .5f);
    }
    private void OnDisable()
    {
        Active.Remove(this); _occupant = null; _element = null; _shown = null;
        if (_built) { if (_monitor && _zoneMarker && _viewer && _title && _latin && _description && _counter) ShowEmpty(); if (_monitor) _monitor.gameObject.SetActive(false); if (_zoneMarker) _zoneMarker.gameObject.SetActive(false); }
    }
    private void OnDestroy()
    {
        ClearBubbles(); InspectionUI.Forget(_monitor); InspectionUI.Forget(_viewer); InspectionUI.Forget(_zoneMarker);
        if (_monitor) Destroy(_monitor.gameObject); if (_viewer) Destroy(_viewer.gameObject);
        if (_zoneMarker) Destroy(_zoneMarker.gameObject);
        if (_circle) Destroy(_circle); if (_circleTexture) Destroy(_circleTexture);
    }
}
