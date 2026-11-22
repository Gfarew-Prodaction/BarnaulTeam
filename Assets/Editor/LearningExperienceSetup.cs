using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class LearningExperienceSetup
{
 [MenuItem("Tools/Learning/Install tutorial and catalogue")]
 public static void Install()
 {
  if(EditorApplication.isPlaying)return;
  var current=SceneManager.GetActiveScene();if(current.isDirty)EditorSceneManager.SaveScene(current);
  Directory.CreateDirectory("Backups/LearningExperience");
  foreach(var path in new[]{"Assets/Scenes/SampleScene.unity","Assets/Scenes/PracticeScene.unity"})
  {
   var backup="Backups/LearningExperience/"+Path.GetFileName(path);if(!File.Exists(backup))File.Copy(path,backup);
   EditorSceneManager.OpenScene(path);
   RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.7f,.7f,.7f);RenderSettings.ambientEquatorColor=new Color(.64f,.64f,.64f);RenderSettings.ambientGroundColor=new Color(.5f,.5f,.5f);RenderSettings.ambientIntensity=1;
   foreach(var light in Object.FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None))
   {
    light.color=Color.white;light.useColorTemperature=true;light.colorTemperature=6500;
    if(light.type==LightType.Rectangle)light.intensity=40;else if(light.name.Contains("Anatomy")||light.name.Contains("Research"))light.intensity=7;else if(light.type==LightType.Point)light.intensity=2;
   }
   foreach(var volume in Object.FindObjectsByType<Volume>(FindObjectsInactive.Include,FindObjectsSortMode.None))
   {
    var profile=volume.sharedProfile;if(!profile)continue;
    if(profile.TryGet<WhiteBalance>(out var wb)){wb.temperature.Override(0);wb.tint.Override(0);}
    if(profile.TryGet<ColorAdjustments>(out var ca)){ca.postExposure.Override(.35f);ca.contrast.Override(0);ca.colorFilter.Override(Color.white);}
    if(profile.TryGet<Bloom>(out var bloom))bloom.intensity.Override(.05f);EditorUtility.SetDirty(profile);
   }
   if(path.Contains("SampleScene"))
   {
    var layers=Object.FindAnyObjectByType<ChoiceLayer>();var tray=Object.FindAnyObjectByType<ElementTray>();tray.transform.position=new Vector3(1.19f,.857f,-1.12f);
    var old=Object.FindAnyObjectByType<LayerElementCatalog>();if(!old)
    {
     var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ClinicFurnitureSetup.Folder+"/Prefabs/Medical_Terminal_Black.prefab"));
     go.name="Element catalogue terminal";go.transform.SetParent(GameObject.Find("Anatomy Workstation").transform,false);go.transform.position=new Vector3(1.18f,.857f,-1.55f);go.transform.localScale=Vector3.one*1.3f;
     var screen=ClinicFurnitureSetup.Screen(go,"Catalogue screen anchor");go.AddComponent<LayerElementCatalog>().Configure(layers,screen);
    }
    var anchor=Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(t=>t.name=="Layer screen anchor");
    var header=anchor.GetComponentsInChildren<Text>(true).FirstOrDefault(t=>t.text.Contains("СЛОИ ГОЛОВЫ"));
    if(header){header.text="Слои";header.fontSize=20;header.rectTransform.sizeDelta=new Vector2(570,96);header.rectTransform.anchoredPosition=new Vector2(0,123);}
   }
   EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
  }
  foreach(var guid in AssetDatabase.FindAssets("M_LED_Diffuser_4000K t:Material")){var mat=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));if(mat&&mat.HasProperty("_EmissionColor")){mat.SetColor("_EmissionColor",Color.white*2);EditorUtility.SetDirty(mat);}}
  AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");File.WriteAllText("/tmp/learning-setup.txt","SUCCESS");
 }
}
