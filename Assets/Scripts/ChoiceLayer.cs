using System.Collections.Generic;
using UnityEngine;

public enum AnatomyLayer { Skin, Bones, Muscles, Brain }

public class ChoiceLayer : MonoBehaviour
{
    [SerializeField] private List<GameObject> choices = new List<GameObject>();
    [SerializeField] private GameObject _bones, _muscles, _skin, _brain;
    public void Configure(GameObject bones, GameObject muscles) { _bones = bones; _muscles = muscles; }
    public void ConfigureExtended(GameObject bones, GameObject muscles, GameObject skin, GameObject brain)
    { Configure(bones, muscles); _skin = skin; _brain = brain; }
    private void Start() { if (_skin) ShowSkin(); else if (_bones) ShowMuscles(); else if (choices.Count > 0) Rechange(choices[0]); }
    public AnatomyLayer CurrentLayer { get; private set; }
    public event System.Action Changed;
    public GameObject[] LayerRoots => new[] { _skin, _bones, _muscles, _brain };
    public string CurrentLayerName => CurrentLayer == AnatomyLayer.Skin ? "Кожа" : CurrentLayer == AnatomyLayer.Bones ? "Кости" : CurrentLayer == AnatomyLayer.Muscles ? "Мышцы" : "Мозг";
    private void Apply(bool bones, bool muscles, bool skin, bool brain)
    {
        if (_bones) _bones.SetActive(bones); if (_muscles) _muscles.SetActive(muscles);
        if (_skin) _skin.SetActive(skin); if (_brain) _brain.SetActive(brain);
        CurrentLayer = skin ? AnatomyLayer.Skin : brain ? AnatomyLayer.Brain : muscles ? AnatomyLayer.Muscles : AnatomyLayer.Bones;
        Changed?.Invoke();
    }
    public void ShowSkin() => Apply(false, false, true, false);
    public void ShowBones() => Apply(true, false, false, false);
    public void ShowMuscles() => Apply(false, true, false, false);
    public void ShowBrain() => Apply(false, false, false, true);
    public void Rechange(GameObject choiceOnActivate)
    {
        if (!choiceOnActivate) return;
        foreach (var choice in choices) if (choice) choice.SetActive(choice == choiceOnActivate);
        choiceOnActivate.SetActive(true);
    }
}
