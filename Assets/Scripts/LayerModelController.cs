using UnityEngine;
using HurricaneVR.Framework.ControllerInput;
using HurricaneVR.Framework.Core.Player;

// The physical HMD can always move; only joystick locomotion is suspended.
[DefaultExecutionOrder(-50)]
public class LayerModelController : MonoBehaviour
{
    [SerializeField] private Transform _model;
    [SerializeField] private Transform _table;
    [SerializeField] private Canvas _panel;
    [SerializeField] private float _distance = 1.6f;
    [SerializeField] private float _speed = 65f;
    [SerializeField] private float _scaleSpeed = .65f;
    [SerializeField] private Vector2 _scaleLimits = new Vector2(.5f, 1.8f);
    private Quaternion _initialRotation;
    private Vector3 _initialScale;
    private static LayerModelController _instance;
    private void Awake() => CaptureResetPose();
    public void CaptureResetPose() { if (_model) { _initialRotation = _model.localRotation; _initialScale = _model.localScale; } }
    private HVRPlayerController _player;
    private HVRTeleporter _teleporter;
    private bool _editing, _movement, _rotation, _teleport;
    public bool IsEditing => _editing;
    public int RotationResetCount { get; private set; }
    public int ScaleResetCount { get; private set; }
    public int ModeChangeCount { get; private set; }
    public Transform Model => _model;
    private void OnEnable() { _instance = this; }
    public void Configure(Transform model, Transform table) { _model = model; _table = table; }
    public void SetPanel(Canvas panel) => _panel = panel;
    private void Start()
    {
        if (!_panel) return;
        InspectionVRInput.RegisterCanvas(_panel);
        // Labels and decorations must not intercept the controller's UI ray.
        foreach (var graphic in _panel.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
        {
            var button = graphic.GetComponent<UnityEngine.UI.Button>();
            graphic.raycastTarget = button && button.targetGraphic == graphic;
        }
        if (!FindAnyObjectByType<InspectionVRInput>()) gameObject.AddComponent<InspectionVRInput>();
    }
    public static bool IsNearTable()
    {
        var camera = Camera.main;
        return _instance && _instance._table && camera &&
            Vector3.ProjectOnPlane(camera.transform.position - _instance._table.position, Vector3.up).sqrMagnitude <= _instance._distance * _instance._distance;
    }
    private void Update()
    {
        if (!_player) _player = FindFirstObjectByType<HVRPlayerController>();
        var input = HVRInputManager.Instance;
        if (!_player || !input || !input.LeftController || !input.RightController) { if (_editing) Exit(); return; }
        bool near = IsNearTable();
        if (_editing && !near) Exit();
        if (near && input.LeftController.SecondaryButtonState.JustActivated)
        {
            if (_editing) Exit(); else Enter();
        }
        if (!_editing || !_model) return;
        ApplyModelInput(input.LeftController.JoystickAxis, input.RightController.JoystickAxis.y, Time.deltaTime);
        if (input.LeftController.PrimaryButtonState.JustActivated) ResetRotation();
        if (input.RightController.PrimaryButtonState.JustActivated) ResetScale();
    }
    public void ApplyModelInput(Vector2 left, float zoom, float deltaTime)
    {
        if (!_model) return;
        if (Mathf.Abs(left.x) >= Mathf.Abs(left.y)) left.y = 0; else left.x = 0;
        _model.Rotate(Vector3.up, left.x * _speed * deltaTime, Space.World);
        _model.Rotate(Vector3.right, -left.y * _speed * deltaTime, Space.World);
        float factor = Mathf.Clamp(_model.localScale.x / _initialScale.x * Mathf.Exp(zoom * _scaleSpeed * deltaTime), _scaleLimits.x, _scaleLimits.y);
        _model.localScale = _initialScale * factor;
        Physics.SyncTransforms();
    }
    public void ResetRotation() { if (_model) { _model.localRotation = _initialRotation; Physics.SyncTransforms(); RotationResetCount++; } }
    public void ResetScale() { if (_model) { _model.localScale = _initialScale; Physics.SyncTransforms(); ScaleResetCount++; } }
    private void Enter()
    {
        _movement = _player.MovementEnabled; _rotation = _player.RotationEnabled;
        _player.MovementEnabled = false; _player.RotationEnabled = false;
        _teleporter = _player.GetComponent<HVRTeleporter>();
        if (_teleporter) { _teleport = _teleporter.enabled; _teleporter.enabled = false; }
        _editing = true;
        ModeChangeCount++;
    }
    private void Exit()
    {
        if (!_editing) return;
        if (_player) { _player.MovementEnabled = _movement; _player.RotationEnabled = _rotation; }
        if (_teleporter) _teleporter.enabled = _teleport;
        _editing = false;
        ModeChangeCount++;
    }
    private void OnDisable() { Exit(); if (_instance == this) _instance = null; }
    private void OnDestroy() { InspectionUI.Forget(_panel); }
}
