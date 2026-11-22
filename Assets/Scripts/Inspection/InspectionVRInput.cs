using System.Collections;
using System.Collections.Generic;
using HurricaneVR.Framework.Core.Grabbers;
using HurricaneVR.Framework.Core.UI;
using HurricaneVR.Framework.Shared;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Reuse HurricaneVR UI pointers and its event module; add missing pointers only when the rig has none.
public class InspectionVRInput : MonoBehaviour
{
    private readonly List<GameObject> _created = new List<GameObject>();
    private Material _rayMaterial;
    public static HVRInputModule EnsureModule()
    {
        var module = HVRInputModule.Instance;
        var events = module ? module.GetComponent<EventSystem>() : EventSystem.current;
        if (!events) events = new GameObject("Inspection EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
        // An existing Input System module can otherwise keep ownership of the EventSystem.
        foreach (var other in events.GetComponents<BaseInputModule>())
            if (other != module) other.enabled = false;
        if (!module) module = events.gameObject.AddComponent<HVRInputModule>();
        events.enabled = true;
        module.enabled = true;
        module.PressButton = HVRButtons.Trigger;
        module.MaxDistance = 5;
        return module;
    }
    public static void RegisterCanvas(Canvas canvas)
    {
        if (!canvas) return;
        var raycaster = canvas.GetComponent<GraphicRaycaster>();
        if (!raycaster) raycaster = canvas.gameObject.AddComponent<GraphicRaycaster>();
        raycaster.enabled = true;
        // The table panel is visible from the reverse side of its Canvas normal.
        raycaster.ignoreReversedGraphics = false;
        EnsureModule().AddCanvas(canvas);
    }
    private IEnumerator Start()
    {
        var module = EnsureModule();
        // Let the existing rig's pointers initialize their cameras before processing UI.
        yield return null;
        if (module.Pointers.Count > 0)
        {
            foreach (var pointer in module.Pointers)
            {
                if (!pointer) continue;
                pointer.InputModule = module;
                if (!pointer.GetComponent<CrispHandRay>()) pointer.gameObject.AddComponent<CrispHandRay>();
            }
            yield break;
        }
        _rayMaterial = new Material(Shader.Find("UI/Default")); _rayMaterial.color = new Color(.3f, .95f, .82f);
        var pending = new List<HVRUIPointer>();
        foreach (var hand in FindObjectsByType<HVRHandGrabber>())
        {
            var go = new GameObject("Inspection UI Pointer"); go.transform.SetParent(hand.transform, false);
            var pointer = go.AddComponent<HVRUIPointer>(); pointer.HandSide = hand.HandSide; pointer.InputModule = module;
            var camera = go.GetComponent<Camera>(); camera.enabled = false; camera.nearClipPlane = .01f; camera.farClipPlane = 5;
            var ray = go.GetComponent<LineRenderer>(); ray.sharedMaterial = _rayMaterial; ray.positionCount = 2;
            ray.startWidth = .002f; ray.endWidth = .001f; ray.enabled = false;
            pending.Add(pointer); _created.Add(go);
        }
        yield return null;
        foreach (var pointer in pending) if (pointer && module)
        {
            module.AddPointer(pointer);
            pointer.gameObject.AddComponent<CrispHandRay>();
        }
    }
    private void OnDestroy()
    {
        foreach (var go in _created) if (go)
        {
            if (HVRInputModule.Instance) HVRInputModule.Instance.Pointers.Remove(go.GetComponent<HVRUIPointer>());
            Destroy(go);
        }
        if (_rayMaterial) Destroy(_rayMaterial);
    }
}
