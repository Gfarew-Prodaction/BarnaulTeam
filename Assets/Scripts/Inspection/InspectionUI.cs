using HurricaneVR.Framework.Core.UI;
using UnityEngine;
using UnityEngine.UI;

public static class InspectionUI
{
    public static readonly Color Background = MedicalUITheme.Paper;
    public static readonly Color Accent = MedicalUITheme.Teal;
    public static readonly Color Ink = MedicalUITheme.Navy;
    public static readonly Color Muted = MedicalUITheme.Muted;
    public static Canvas Panel(string name, Transform parent, Vector2 size, float scale)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(GraphicRaycaster));
        go.transform.SetParent(parent, false); go.transform.localScale = Vector3.one * scale;
        var rect = go.GetComponent<RectTransform>(); rect.sizeDelta = size;
        var canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.WorldSpace; canvas.worldCamera = Camera.main;
        var background=Image(go.transform, "Background", Vector2.zero, size, Background);MedicalUITheme.Surface(background,Background);
        if (HVRInputModule.Instance) HVRInputModule.Instance.AddCanvas(canvas);
        return canvas;
    }
    public static Image Image(Transform parent, string name, Vector2 position, Vector2 size, Color color)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(UnityEngine.UI.Image)); go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>(); rect.anchoredPosition = position; rect.sizeDelta = size;
        var image = go.GetComponent<UnityEngine.UI.Image>(); image.color = color; image.raycastTarget = false; return image;
    }
    public static Text Text(Transform parent, Vector2 position, Vector2 size, int fontSize, Color color)
    {
        var go = new GameObject("Text", typeof(RectTransform), typeof(UnityEngine.UI.Text)); go.transform.SetParent(parent, false);
        var rect = go.GetComponent<RectTransform>(); rect.anchoredPosition = position; rect.sizeDelta = size;
        var text = go.GetComponent<UnityEngine.UI.Text>(); text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontSize = fontSize; text.color = color; text.supportRichText = false; text.raycastTarget = false;
        text.alignment = TextAnchor.UpperLeft; return text;
    }
    public static Button Button(Transform parent, string title, Vector2 position, Vector2 size, UnityEngine.Events.UnityAction click)
    {
        var image = Image(parent, title, position, size, MedicalUITheme.Navy); image.raycastTarget = true;
        var button = image.gameObject.AddComponent<UnityEngine.UI.Button>(); button.targetGraphic = image; button.onClick.AddListener(click);
        var label = Text(image.transform, Vector2.zero, size - new Vector2(12, 6), 20, Color.white); label.alignment = TextAnchor.MiddleCenter;
        label.text = title;MedicalUITheme.Button(button);return button;
    }
    public static void Forget(Canvas canvas)
    {
        if (canvas && HVRInputModule.Instance) HVRInputModule.Instance.UICanvases.Remove(canvas);
    }
}
