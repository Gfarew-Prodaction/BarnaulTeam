using System.Collections.Generic;
using UnityEngine;

// A scene tray with four reserved places. Returning elements keep using HurricaneVR grab hooks.
public class ElementTray : MonoBehaviour
{
    [SerializeField] private Transform[] _slots = new Transform[4];
    [SerializeField] private Renderer[] _markers = new Renderer[4];
    [SerializeField] private Material _emptyMaterial;
    [SerializeField] private Material _occupiedMaterial;
    [SerializeField] private Vector3 _zoneCenter = new Vector3(0, .24f, 0);
    [SerializeField] private Vector3 _zoneSize = new Vector3(1.2f, .72f, 1.5f);
    private static readonly List<ElementTray> Active = new List<ElementTray>();
    private readonly OnMoveBackItem[] _occupants = new OnMoveBackItem[4];
    public int OccupiedCount { get { int count=0; foreach(var item in _occupants) if(item) count++; return count; } }
    private bool _ready;

    private void OnEnable()
    {
        _ready = _slots != null && _slots.Length == 4;
        if (_ready) foreach (var slot in _slots) if (!slot) _ready = false;
        if (!_ready) { Debug.LogWarning("Поднос: назначь четыре места в Slots.", this); return; }
        if (!Active.Contains(this)) Active.Add(this);
        ResetMarkers();
    }
    private void OnDisable()
    {
        Active.Remove(this);
        for (int i = 0; i < 4; i++) _occupants[i] = null;
        ResetMarkers();
    }
    private void ResetMarkers()
    {
        if (_markers == null) return;
        foreach (var marker in _markers) if (marker) marker.sharedMaterial = _emptyMaterial;
    }
    private void OnDrawGizmosSelected()
    {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = new Color(.2f, .9f, .75f, .65f);
        Gizmos.DrawWireCube(_zoneCenter, _zoneSize);
    }

    public static bool TryReserveNearest(OnMoveBackItem item, out ElementTray tray, out Vector3 position, out Quaternion rotation)
    {
        tray = null; position = default; rotation = item.transform.rotation;
        float closest = float.PositiveInfinity; int slot = -1;
        foreach (var candidate in Active)
        {
            if (!candidate || !candidate._ready) continue;
            var local = candidate.transform.InverseTransformPoint(item.transform.position);
            if (!new Bounds(candidate._zoneCenter, candidate._zoneSize).Contains(local)) continue;
            for (int i = 0; i < 4; i++)
            {
                if (candidate._occupants[i]) continue;
                float distance = (candidate._slots[i].position - item.transform.position).sqrMagnitude;
                if (distance >= closest) continue;
                closest = distance; tray = candidate; slot = i;
            }
        }
        if (!tray) return false;
        tray._occupants[slot] = item;
        if (tray._markers != null && slot < tray._markers.Length && tray._markers[slot])
            tray._markers[slot].sharedMaterial = tray._occupiedMaterial;
        position = tray._slots[slot].position;
        // Keep the released orientation and put the collider's lowest point above the tray surface.
        var colliders = item.GetComponentsInChildren<Collider>();
        float bottom = float.PositiveInfinity;
        foreach (var collider in colliders) if (collider.enabled && !collider.isTrigger) bottom = Mathf.Min(bottom, collider.bounds.min.y);
        if (!float.IsPositiveInfinity(bottom)) position.y += item.transform.position.y - bottom;
        return true;
    }
    public void Release(OnMoveBackItem item)
    {
        for (int i = 0; i < 4; i++) if (_occupants[i] == item)
        {
            _occupants[i] = null;
            if (_markers != null && i < _markers.Length && _markers[i]) _markers[i].sharedMaterial = _emptyMaterial;
        }
    }
}
