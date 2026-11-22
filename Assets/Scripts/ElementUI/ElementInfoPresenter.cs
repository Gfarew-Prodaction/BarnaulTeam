using System.Collections.Generic;
using DataScripts;
using HurricaneVR.Framework.Core;
using HurricaneVR.Framework.Core.Grabbers;
using UnityEngine;
using UnityEngine.UI;

namespace InteracrSystem
{
    [RequireComponent(typeof(ElementDog), typeof(HVRGrabbable))]
    public class ElementInfoPresenter : MonoBehaviour
    {
        [SerializeField] private float _cardWidth = .32f;
        [SerializeField] private float _cardHeight = .23f;
        [SerializeField] private float _gap = .045f;
        private ElementDog _element;
        private HVRGrabbable _grabbable;
        private readonly HashSet<HVRGrabberBase> _hoverers = new HashSet<HVRGrabberBase>();
        private Canvas _hover, _card;
        private Text _hoverName, _name, _latin, _description;
        private Image _icon;
        private RemoteElement _shownData;
        private Camera _camera;
        private Collider[] _colliders;
        private Texture2D _roundedTexture;
        private Sprite _rounded;
        private static readonly Color PanelBackground = MedicalUITheme.Paper;
        private static readonly Color Ink = MedicalUITheme.Navy;
        private static readonly Color Accent = MedicalUITheme.Teal;

        private void Awake()
        {
            _element = GetComponent<ElementDog>();
            _grabbable = GetComponent<HVRGrabbable>();
            _colliders = GetComponentsInChildren<Collider>();
            CreateRoundedSprite();
            BuildUI();
        }
        private void OnEnable()
        {
            _grabbable.HoverEnter.AddListener(OnHoverEnter);
            _grabbable.HoverExit.AddListener(OnHoverExit);
        }
        private void OnDisable()
        {
            _grabbable.HoverEnter.RemoveListener(OnHoverEnter);
            _grabbable.HoverExit.RemoveListener(OnHoverExit);
            _hoverers.Clear();
            if (_hover) _hover.gameObject.SetActive(false);
            if (_card) _card.gameObject.SetActive(false);
        }
        private void OnHoverEnter(HVRGrabberBase grabber, HVRGrabbable item)
        {
            if (grabber is HVRHandGrabber || grabber is HVRForceGrabber) _hoverers.Add(grabber);
        }
        private void OnHoverExit(HVRGrabberBase grabber, HVRGrabbable item) => _hoverers.Remove(grabber);

        private void LateUpdate()
        {
            if (!_camera || !_camera.isActiveAndEnabled) _camera = Camera.main;
            if (!_camera) return;
            _hoverers.RemoveWhere(g => !g || !g.isActiveAndEnabled || g.HoverTarget != _grabbable);
            bool held = _grabbable.IsHandGrabbed;
            bool hovered = false;
            foreach (var grabber in _hoverers)
                if (ElementGrabAvailability.CanHighlight(grabber, _grabbable)) { hovered = true; break; }
            _hover.gameObject.SetActive(hovered);
            _card.gameObject.SetActive(held);
            if (!held && !hovered) return;
            var data = _element.Data;
            if (data != _shownData || string.IsNullOrEmpty(_hoverName.text)) UpdateText(data);
            Bounds bounds = new Bounds(transform.position, Vector3.zero);
            bool found = false;
            foreach (var col in _colliders)
            {
                if (!col || !col.enabled || col.isTrigger) continue;
                if (!found) { bounds = col.bounds; found = true; }
                else bounds.Encapsulate(col.bounds);
            }
            Vector3 right = _camera.transform.right;
            Vector3 up = _camera.transform.up;
            // Project the actual world bounds onto the viewer's right axis, even when the item rotates.
            float extent = Vector3.Dot(new Vector3(Mathf.Abs(right.x), Mathf.Abs(right.y), Mathf.Abs(right.z)), bounds.extents);
            _hover.transform.SetPositionAndRotation(new Vector3(bounds.center.x, bounds.max.y + _gap + .025f, bounds.center.z), _camera.transform.rotation);
            _card.transform.SetPositionAndRotation(bounds.center + right * (extent + _gap + _cardWidth / 2) + up * .015f, _camera.transform.rotation);
        }

        private void UpdateText(RemoteElement data)
        {
            _shownData = data;
            string title = data != null && !string.IsNullOrWhiteSpace(data.name) ? data.name : "Данные не найдены";
            _hoverName.text = title;
            _name.text = title;
            _latin.text = data != null ? data.nameLatin : "";
            _description.text = data != null && !string.IsNullOrWhiteSpace(data.shortDescription)
                ? data.shortDescription : "Описание пока не добавлено.";
            _icon.sprite = data != null ? _element.GetImage(data.iconUrl) : null;
            _icon.enabled = _icon.sprite;
        }

        private Canvas NewCanvas(string label, Vector2 size)
        {
            var go = new GameObject(label, typeof(RectTransform), typeof(Canvas));
            // Build at a fixed world scale; OnDestroy cleans up both independent canvases.
            go.transform.SetParent(transform, false);
            var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace;
            var rect = go.GetComponent<RectTransform>(); rect.sizeDelta = size;
            go.transform.SetParent(null, true);
            go.transform.localScale = Vector3.one * .001f;
            var bg = go.AddComponent<Image>(); bg.sprite = _rounded; bg.type = Image.Type.Sliced; bg.color = PanelBackground; bg.raycastTarget = false;
            go.SetActive(false); return canvas;
        }

        private void BuildUI()
        {
            _hover = NewCanvas("Element · Hover title", new Vector2(300, 64));
            AddImage(_hover.transform, new Vector2(-135, 0), new Vector2(4, 30), Accent);
            _hoverName = AddText(_hover.transform, new Vector2(6, 0), new Vector2(260, 50), 25, Ink, FontStyle.Bold);
            _hoverName.alignment = TextAnchor.MiddleCenter;
            _card = NewCanvas("Element · Held info", new Vector2(_cardWidth * 1000, _cardHeight * 1000));
            float left = -_cardWidth * 500;
            AddImage(_card.transform, new Vector2(left + 3, 0), new Vector2(4, _cardHeight * 1000 - 32), Accent);
            var category = AddText(_card.transform, new Vector2(0, 94), new Vector2(278, 24), 12, Accent, FontStyle.Bold);
            category.text = "АНАТОМИЧЕСКИЙ АТЛАС  /  BRAINDOG";
            _name = AddText(_card.transform, new Vector2(0, 57), new Vector2(278, 48), 27, Ink, FontStyle.Bold);
            _name.resizeTextForBestFit = true; _name.resizeTextMinSize = 18; _name.resizeTextMaxSize = 27;
            _latin = AddText(_card.transform, new Vector2(0, 23), new Vector2(278, 28), 18, Accent, FontStyle.Italic);
            AddImage(_card.transform, new Vector2(0, 0), new Vector2(278, 1), new Color(.3f, .5f, .52f, .45f));
            _description = AddText(_card.transform, new Vector2(0, -52), new Vector2(278, 88), 18, MedicalUITheme.Muted, FontStyle.Normal);
            _description.alignment = TextAnchor.UpperLeft;
            _description.resizeTextForBestFit = true; _description.resizeTextMinSize = 13; _description.resizeTextMaxSize = 18;
            _icon = AddImage(_card.transform, new Vector2(132, 94), new Vector2(20, 20), Color.white);
            _icon.preserveAspect = true; _icon.enabled = false;
        }
        private Image AddImage(Transform root, Vector2 position, Vector2 size, Color color)
        {
            var go = new GameObject("Decoration", typeof(RectTransform), typeof(Image)); go.transform.SetParent(root, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchoredPosition = position; rect.sizeDelta = size;
            var image = go.GetComponent<Image>(); image.color = color; image.raycastTarget = false; return image;
        }
        private Text AddText(Transform root, Vector2 position, Vector2 size, int fontSize, Color color, FontStyle style)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text)); go.transform.SetParent(root, false);
            var rect = go.GetComponent<RectTransform>(); rect.anchoredPosition = position; rect.sizeDelta = size;
            var text = go.GetComponent<Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = fontSize; text.color = color; text.fontStyle = style; text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Truncate;
            text.supportRichText = false; text.raycastTarget = false; return text;
        }
        private void CreateRoundedSprite()
        {
            const int size = 64; const float radius = 12;
            _roundedTexture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Max(radius - x - .5f, x + .5f - (size - radius));
                float dy = Mathf.Max(radius - y - .5f, y + .5f - (size - radius));
                float d = new Vector2(Mathf.Max(0, dx), Mathf.Max(0, dy)).magnitude;
                pixels[y * size + x] = new Color(1, 1, 1, Mathf.Clamp01(radius - d));
            }
            _roundedTexture.SetPixels(pixels); _roundedTexture.Apply();
            _rounded = Sprite.Create(_roundedTexture, new Rect(0, 0, size, size), Vector2.one * .5f, 100, 0, SpriteMeshType.FullRect, Vector4.one * 14);
        }
        private void OnDestroy()
        {
            if (_hover) Destroy(_hover.gameObject);
            if (_card) Destroy(_card.gameObject);
            if (_rounded) Destroy(_rounded);
            if (_roundedTexture) Destroy(_roundedTexture);
        }
    }
}
