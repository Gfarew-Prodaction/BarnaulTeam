using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class NeurologySetup
{
 public const string Folder="Assets/Models/NeurologyDog";
 [MenuItem("Tools/Neurology/Prepare patient")]
 public static void Prepare()
 {
  Directory.CreateDirectory(Folder+"/Materials");AssetDatabase.Refresh();
  foreach(string guid in AssetDatabase.FindAssets("t:Texture2D",new[]{Folder+"/Textures"})){var importer=(TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));importer.sRGBTexture=true;importer.mipmapEnabled=true;importer.alphaSource=TextureImporterAlphaSource.FromInput;importer.maxTextureSize=1024;importer.SaveAndReimport();}
  var mapping=JObject.Parse(File.ReadAllText(Folder+"/materials.json"));var modelImporter=(ModelImporter)AssetImporter.GetAtPath(Folder+"/GermanShepherd_Neurology.fbx");
  foreach(var entry in mapping.Properties())
  {
   string path=Folder+"/Materials/"+entry.Name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
   bool eye=(bool?)entry.Value["eye"]??false;mat.color=eye?new Color(.035f,.025f,.018f,1):Color.white;mat.SetTexture("_BaseMap",eye?null:AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/"+(string)entry.Value["texture"]));mat.SetFloat("_Smoothness",eye?.8f:.2f);
   if((bool)entry.Value["alpha"]){mat.SetFloat("_AlphaClip",1);mat.SetFloat("_Cutoff",.4f);mat.SetFloat("_Cull",0);mat.EnableKeyword("_ALPHATEST_ON");mat.SetOverrideTag("RenderType","TransparentCutout");mat.renderQueue=2450;}
   EditorUtility.SetDirty(mat);modelImporter.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),entry.Name),mat);
  }
  modelImporter.importCameras=false;modelImporter.importLights=false;modelImporter.importAnimation=true;modelImporter.animationType=ModelImporterAnimationType.Generic;modelImporter.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;modelImporter.optimizeGameObjects=false;modelImporter.meshCompression=ModelImporterMeshCompression.Low;modelImporter.SaveAndReimport();
  var clips=modelImporter.defaultClipAnimations;foreach(var clip in clips){clip.loopTime=true;clip.loopPose=true;clip.lockRootPositionXZ=true;clip.lockRootRotation=true;}modelImporter.clipAnimations=clips;modelImporter.SaveAndReimport();
  string controllerPath=Folder+"/Patient.controller";var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);if(!controller)controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
  var stateMachine=controller.layers[0].stateMachine;foreach(var state in stateMachine.states)stateMachine.RemoveState(state.state);
  var animations=AssetDatabase.LoadAllAssetsAtPath(Folder+"/GermanShepherd_Neurology.fbx").OfType<AnimationClip>().Where(c=>!c.name.StartsWith("__preview__")).ToArray();
  foreach(string name in new[]{"NormalWalk","CircleLeft","CircleRight","CerebellarAtaxia","VestibularAtaxia","ReviewIdle"})
  {var clip=animations.FirstOrDefault(a=>a.name.Contains(name));if(!clip)throw new Exception("Missing animation "+name);var state=stateMachine.AddState(name);state.motion=clip;if(name=="NormalWalk")stateMachine.defaultState=state;}
  var root=new GameObject("Neurological patient");var model=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/GermanShepherd_Neurology.fbx"),root.transform);model.name="German Shepherd · Animated rig";
  var animator=model.GetComponent<Animator>();animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
  var head=model.GetComponentsInChildren<Transform>().First(t=>t.name=="Head");var bounds=Bounds(model);var forward=Vector3.ProjectOnPlane(head.position-bounds.center,Vector3.up);if(forward.sqrMagnitude>.01f)model.transform.rotation=Quaternion.FromToRotation(forward.normalized,Vector3.forward)*model.transform.rotation;
  bounds=Bounds(model);model.transform.localScale*=.72f/bounds.size.y;bounds=Bounds(model);model.transform.position-=new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);
  foreach(var r in model.GetComponentsInChildren<SkinnedMeshRenderer>()){r.updateWhenOffscreen=true;r.shadowCastingMode=ShadowCastingMode.On;var b=r.localBounds;b.Expand(b.size*.4f);r.localBounds=b;}
  const string shadowPath=Folder+"/Textures/ContactShadow.png";
  var shadowTexture=new Texture2D(64,64,TextureFormat.RGBA32,false);var pixels=new Color[64*64];for(int y=0;y<64;y++)for(int x=0;x<64;x++){float d=new Vector2((x-31.5f)/31.5f,(y-31.5f)/31.5f).magnitude;pixels[y*64+x]=new Color(1,1,1,Mathf.Pow(Mathf.Clamp01(1-d),1.5f));}shadowTexture.SetPixels(pixels);shadowTexture.Apply();File.WriteAllBytes(shadowPath,shadowTexture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(shadowTexture);AssetDatabase.ImportAsset(shadowPath,ImportAssetOptions.ForceSynchronousImport);
  var shadowImporter=(TextureImporter)AssetImporter.GetAtPath(shadowPath);shadowImporter.alphaSource=TextureImporterAlphaSource.FromInput;shadowImporter.alphaIsTransparency=true;shadowImporter.mipmapEnabled=false;shadowImporter.SaveAndReimport();
  string shadowMatPath=Folder+"/Materials/ContactShadow.mat";var shadowMat=AssetDatabase.LoadAssetAtPath<Material>(shadowMatPath);if(!shadowMat){shadowMat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(shadowMat,shadowMatPath);}shadowMat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(shadowPath));shadowMat.color=new Color(.025f,.04f,.045f,.65f);shadowMat.SetFloat("_Surface",1);shadowMat.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);shadowMat.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);shadowMat.SetFloat("_ZWrite",0);shadowMat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");shadowMat.SetOverrideTag("RenderType","Transparent");shadowMat.renderQueue=3000;EditorUtility.SetDirty(shadowMat);
  var shadow=GameObject.CreatePrimitive(PrimitiveType.Quad);shadow.name="Soft contact shadow";shadow.transform.SetParent(root.transform,false);shadow.transform.localPosition=new Vector3(0,.012f,0);shadow.transform.localRotation=Quaternion.Euler(90,0,0);shadow.transform.localScale=new Vector3(.55f,1.05f,1);UnityEngine.Object.DestroyImmediate(shadow.GetComponent<Collider>());var shadowRenderer=shadow.GetComponent<Renderer>();shadowRenderer.sharedMaterial=shadowMat;shadowRenderer.shadowCastingMode=ShadowCastingMode.Off;shadowRenderer.receiveShadows=false;
  root.AddComponent<NeurologicalDog>().Configure(animator,shadow.transform);PrefabUtility.SaveAsPrefabAsset(root,Folder+"/NeurologicalPatient.prefab");
  File.WriteAllText("/tmp/neuro-model.txt","Clips: "+string.Join(",",animations.Select(a=>a.name+" "+a.length))+"\nSize: "+Bounds(model)+"\nMeshes: "+model.GetComponentsInChildren<SkinnedMeshRenderer>().Length+"\n");UnityEngine.Object.DestroyImmediate(root);AssetDatabase.SaveAssets();
 }
 public static Bounds Bounds(GameObject model){var rs=model.GetComponentsInChildren<Renderer>();var result=rs[0].bounds;foreach(var r in rs.Skip(1))result.Encapsulate(r.bounds);return result;}
 public static void PrepareTheme()
 {
  const string path="Assets/Resources/UI/MedicalRounded.png";Directory.CreateDirectory("Assets/Resources/UI");if(!File.Exists(path))File.WriteAllBytes(path,MedicalUITheme.Rounded.texture.EncodeToPNG());AssetDatabase.ImportAsset(path,ImportAssetOptions.ForceSynchronousImport);
  var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spriteImportMode=SpriteImportMode.Single;importer.spriteBorder=new Vector4(14,14,14,14);importer.mipmapEnabled=false;importer.alphaIsTransparency=true;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.SaveAndReimport();MedicalUITheme.ReloadSprite();
 }
 [MenuItem("Tools/Neurology/Install patient and restyle scenes")]
 public static void Install()
 {
  if(EditorApplication.isPlaying)throw new Exception("Stop Play Mode first.");PrepareTheme();var current=SceneManager.GetActiveScene();if(current.isDirty)EditorSceneManager.SaveScene(current);Directory.CreateDirectory("Backups/Neurology");
  foreach(string name in new[]{"Startup","SampleScene","PracticeScene"})
  {
   string path="Assets/Scenes/"+name+".unity";string backup="Backups/Neurology/"+name+".unity";if(!File.Exists(backup))File.Copy(path,backup);EditorSceneManager.OpenScene(path);
   foreach(var canvas in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsInactive.Include,FindObjectsSortMode.None))MedicalUITheme.Apply(canvas,canvas.GetComponentInParent<MedicalTVScreen>());
   if(name=="SampleScene")
   {
    var layers=UnityEngine.Object.FindAnyObjectByType<ChoiceLayer>();
    var anchor=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(t=>t.name=="Layer screen anchor");
    var canvas=anchor.GetComponentInChildren<Canvas>(true);var buttons=canvas.GetComponentsInChildren<UnityEngine.UI.Button>(true);
    string[] labels={"Кожа","Мышцы","Кости","Мозг"};var ordered=labels.Select(label=>buttons.First(b=>b.GetComponentInChildren<UnityEngine.UI.Text>().text==label)).ToArray();
    var selection=canvas.GetComponent<LayerSelectionUI>();if(!selection)selection=canvas.gameObject.AddComponent<LayerSelectionUI>();selection.Configure(layers,ordered);
   }
   if(name=="PracticeScene")
   {
    var check=UnityEngine.Object.FindAnyObjectByType<NeurologicalCheck>();if(!check)
    {
     var host=new GameObject("Neurological check · Controller");check=host.AddComponent<NeurologicalCheck>();var stage=new GameObject("Neurological observation area");stage.transform.position=new Vector3(.5f,.005f,1.15f);
     var patient=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/NeurologicalPatient.prefab"));patient.transform.SetParent(stage.transform,false);
     var floor=GameObject.CreatePrimitive(PrimitiveType.Cylinder);floor.name="Patient observation circle";floor.transform.SetParent(stage.transform,false);floor.transform.localPosition=new Vector3(0,0,0);floor.transform.localScale=new Vector3(2.1f,.003f,2.1f);UnityEngine.Object.DestroyImmediate(floor.GetComponent<Collider>());
     string materialPath=Folder+"/Materials/ObservationFloor.mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(materialPath);if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.color=new Color(.56f,.7f,.7f);mat.SetFloat("_Smoothness",.25f);AssetDatabase.CreateAsset(mat,materialPath);}floor.GetComponent<Renderer>().sharedMaterial=mat;
     check.Configure(patient.GetComponent<NeurologicalDog>(),stage);patient.SetActive(false);stage.SetActive(false);
    }
   }
   EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
  }
  AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/PracticeScene.unity");File.WriteAllText("/tmp/neuro-install.txt","SUCCESS");
 }
}
