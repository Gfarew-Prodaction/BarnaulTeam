using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using UnityEditor.Events;
using InteracrSystem;

public static class ClinicFurnitureSetup
{
 public const string Folder="Assets/Models/ClinicFurniture";
 public static readonly string[] Names={"Vet_Table","AGAU_Computer","AGAU_Workdesk","AGAU_Anatomy_Stand","Medical_Terminal_Black","Instrument_Tray"};
 public static string ModelPath(string name)=>Folder+"/"+name+"_FBX/"+name+"_GameReady.fbx";
 [MenuItem("Tools/Clinic Furniture/Prepare materials and prefabs")]
 public static void Prepare() => PrepareAssets(Names);
 public static void PrepareAssets(IEnumerable<string> names)
 {
  Directory.CreateDirectory(Folder+"/Prefabs");AssetDatabase.Refresh();
  foreach(var name in names)
  {
   string folder=Folder+"/"+name+"_FBX";Directory.CreateDirectory(folder+"/Materials");AssetDatabase.Refresh();
   foreach(var guid in AssetDatabase.FindAssets("t:Texture2D",new[]{folder+"/Textures"}))
   {
    var path=AssetDatabase.GUIDToAssetPath(guid);var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(!importer)continue;
    bool normal=path.Contains("Normal"),data=path.Contains("Roughness")||path.Contains("Metallic");
    importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;importer.sRGBTexture=!normal&&!data;importer.alphaSource=TextureImporterAlphaSource.FromInput;
    importer.mipmapEnabled=true;importer.maxTextureSize=path.Contains("2K")||path.Contains("2048")||path.Contains("TR_Steel")||path.Contains("MT_Steel")?2048:1024;importer.textureCompression=TextureImporterCompression.Compressed;importer.SaveAndReimport();
   }
   var json=JObject.Parse(File.ReadAllText(folder+"/PBR_Materials.json"));var materials=new Dictionary<string,Material>();
   foreach(var entry in json.Properties())
   {
    var v=entry.Value;var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.name=entry.Name;
    var color=v["Base Color"];if(color.Type==JTokenType.String){mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/"+(string)color));mat.color=Color.white;}
    else{var c=color.Values<float>().ToArray();mat.color=new Color(c[0],c[1],c[2],c.Length>3?c[3]:1);}
    var packed=AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/Textures/Packed/"+entry.Name+"_MetallicSmoothness.png");mat.SetTexture("_MetallicGlossMap",packed);mat.SetFloat("_Smoothness",1);mat.EnableKeyword("_METALLICSPECGLOSSMAP");
    if(v["Normal"]?.Type==JTokenType.String){mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/"+(string)v["Normal"]));mat.SetFloat("_BumpScale",.55f);mat.EnableKeyword("_NORMALMAP");}
    if(v["Alpha"]?.Type==JTokenType.String){mat.SetFloat("_AlphaClip",1);mat.SetFloat("_Cutoff",.35f);mat.EnableKeyword("_ALPHATEST_ON");mat.SetOverrideTag("RenderType","TransparentCutout");mat.renderQueue=2450;}
    float emission=(float?)v["Emission Strength"]??0;
    if(emission>0){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",new Color(.8f,.92f,1)*emission);if(v["Emission Color"]?.Type==JTokenType.String)mat.SetTexture("_EmissionMap",AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/"+(string)v["Emission Color"]));mat.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;}
    else mat.globalIlluminationFlags=MaterialGlobalIlluminationFlags.EmissiveIsBlack;
    string path=folder+"/Materials/"+entry.Name+".mat";var old=AssetDatabase.LoadAssetAtPath<Material>(path);if(old){EditorUtility.CopySerialized(mat,old);UnityEngine.Object.DestroyImmediate(mat);mat=old;}else AssetDatabase.CreateAsset(mat,path);materials[entry.Name]=mat;
   }
   var model=AssetImporter.GetAtPath(ModelPath(name)) as ModelImporter;if(!model)throw new Exception("Model not imported: "+name);
   model.importCameras=false;model.importLights=false;model.importAnimation=false;model.generateSecondaryUV=true;model.meshCompression=ModelImporterMeshCompression.Low;model.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;
   foreach(var pair in materials)model.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),pair.Key),pair.Value);model.SaveAndReimport();
   var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(ModelPath(name)));go.name=name;
   foreach(var filter in go.GetComponentsInChildren<MeshFilter>(true))
   {
    bool collision=filter.name.StartsWith("UCX_",StringComparison.OrdinalIgnoreCase);
    var r=filter.GetComponent<MeshRenderer>();if(collision){if(r)r.enabled=false;var col=filter.gameObject.AddComponent<MeshCollider>();col.sharedMesh=filter.sharedMesh;col.convex=false;}
    else if(r){r.sharedMaterials=r.sharedMaterials.Select(m=>m&&materials.TryGetValue(m.name,out var mapped)?mapped:m).ToArray();r.receiveGI=ReceiveGI.LightProbes;}
    GameObjectUtility.SetStaticEditorFlags(filter.gameObject,StaticEditorFlags.ContributeGI|StaticEditorFlags.BatchingStatic|StaticEditorFlags.ReflectionProbeStatic);
   }
   PrefabUtility.SaveAsPrefabAsset(go,Folder+"/Prefabs/"+name+".prefab");UnityEngine.Object.DestroyImmediate(go);
  }
  AssetDatabase.SaveAssets();
 }
 public static Bounds BoundsOf(GameObject go){var rs=go.GetComponentsInChildren<Renderer>(true).Where(r=>r.enabled&&!r.name.StartsWith("UCX_")).ToArray();if(rs.Length==0)throw new Exception("No visible meshes: "+go.name);var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);return b;}
 static GameObject Find(string name)=>UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,FindObjectsSortMode.None).FirstOrDefault(t=>t.name==name)?.gameObject;
 static GameObject Place(string asset,Transform parent,Vector3 position,Quaternion rotation,Vector3 scale)
 {
  var go=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/Prefabs/"+asset+".prefab"));
  go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localRotation=rotation;go.transform.localScale=scale;return go;
 }
 static void HideGeometry(GameObject go){foreach(var r in go.GetComponentsInChildren<Renderer>(true))r.enabled=false;foreach(var c in go.GetComponentsInChildren<Collider>(true))c.enabled=false;}
 public static Transform Screen(GameObject go,string name)
 {
  var filter=go.GetComponentsInChildren<MeshFilter>().First(f=>f.name.Contains("Display")&&!f.name.StartsWith("UCX"));var mesh=filter.sharedMesh;var vs=mesh.vertices;var ts=mesh.triangles;float area=0;Vector3 normal=Vector3.zero,center=Vector3.zero;
  for(int i=0;i<ts.Length;i+=3){var a=filter.transform.TransformPoint(vs[ts[i]]);var b=filter.transform.TransformPoint(vs[ts[i+1]]);var c=filter.transform.TransformPoint(vs[ts[i+2]]);var n=Vector3.Cross(b-a,c-a);if(n.magnitude>area){area=n.magnitude;normal=n.normalized;center=(a+b+c)/3;}}
  if(Vector3.Dot(normal,go.transform.forward)<0)normal=-normal;
  var boundsCenter=filter.GetComponent<Renderer>().bounds.center;float front=vs.Max(v=>Vector3.Dot(filter.transform.TransformPoint(v),normal));var onPlane=boundsCenter+normal*(front-Vector3.Dot(boundsCenter,normal));
  var anchor=go.transform.Find(name);if(!anchor){anchor=new GameObject(name).transform;anchor.SetParent(go.transform,true);}anchor.position=onPlane+normal*.003f;anchor.rotation=Quaternion.LookRotation(-normal,go.transform.up);return anchor;
 }
 [MenuItem("Tools/Clinic Furniture/Install in study and practice")]
 public static void Install()
 {
  if(EditorApplication.isPlaying)throw new Exception("Exit Play Mode before installing furniture.");
  var scene=SceneManager.GetActiveScene();if(scene.isDirty)EditorSceneManager.SaveScene(scene);string original=scene.path;
  Directory.CreateDirectory("Backups/ClinicFurniture");
  foreach(var path in new[]{"Assets/Scenes/SampleScene.unity","Assets/Scenes/PracticeScene.unity"})
  {
   EditorSceneManager.OpenScene(path);if(Find("Clinic Furniture · Installed"))continue;
   string backup="Backups/ClinicFurniture/"+Path.GetFileName(path);if(!File.Exists(backup))File.Copy(path,backup);
   if(path.Contains("SampleScene"))InstallStudy();else InstallPractice();
   new GameObject("Clinic Furniture · Installed");EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
  }
  AssetDatabase.SaveAssets();EditorSceneManager.OpenScene(original);File.WriteAllText("/tmp/clinic-installed.txt","SUCCESS: six furniture assets integrated; study UI/tray and practice table references preserved.");
 }
 static void InstallStudy()
 {
  var anatomy=Find("Anatomy Workstation").transform;var research=Find("Research Workstation").transform;
  HideGeometry(Find("VetTable"));HideGeometry(Find("office_desk_with_drawers"));HideGeometry(Find("Cube (2)"));
  var table=Place("Vet_Table",anatomy,new Vector3(.62f,0,-1.35f),Quaternion.identity,new Vector3(1.55f,1,1.3f));
  var stand=Place("AGAU_Anatomy_Stand",anatomy,new Vector3(.51f,.857f,-1.36f),Quaternion.identity,Vector3.one*1.25f);
  var model=Find("Dog Head · Layer Model");var bones=model.transform.Find("Bones and teeth");var rs=bones.GetComponentsInChildren<Renderer>(true).Where(r=>!r.name.Contains("silhouette")).ToArray();float min=rs.Min(r=>r.bounds.min.y);float delta=BoundsOf(stand).max.y+.006f-min;model.transform.position+=Vector3.up*delta;
  foreach(var e in anatomy.GetComponentsInChildren<ElementDog>(true).Where(e=>!e.transform.IsChildOf(model.transform)))e.transform.position+=Vector3.up*.2f;
  foreach(Transform t in anatomy)if(t.name=="Brain · Home silhouette"||t.name=="Eye · Home silhouette")t.position+=Vector3.up*.2f;
  var tray=UnityEngine.Object.FindAnyObjectByType<ElementTray>();var trayRoot=tray.transform;HideGeometry(tray.gameObject);trayRoot.position=new Vector3(1.19f,.857f,-1.35f);
  Place("Instrument_Tray",trayRoot,Vector3.zero,Quaternion.identity,new Vector3(1.7f,1,1.4f));
  var traySO=new SerializedObject(tray);var slots=traySO.FindProperty("_slots");for(int i=0;i<4;i++){var slot=((Transform)slots.GetArrayElementAtIndex(i).objectReferenceValue);slot.localPosition=new Vector3(i%2==0?-.147f:.147f,.014f,i<2?-.095f:.095f);}
  traySO.FindProperty("_markers").arraySize=0;traySO.ApplyModifiedPropertiesWithoutUndo();
  var terminal=Place("Medical_Terminal_Black",anatomy,new Vector3(-.13f,.857f,-1.12f),Quaternion.identity,Vector3.one*1.3f);var layerAnchor=Screen(terminal,"Layer screen anchor");
  var controller=model.GetComponent<LayerModelController>();controller.Configure(model.transform,table.transform);var controllerSO=new SerializedObject(controller);var canvas=(Canvas)controllerSO.FindProperty("_panel").objectReferenceValue;var oldHousing=canvas.transform.parent.gameObject;
  canvas.transform.SetParent(null,true);HideGeometry(oldHousing);canvas.transform.SetParent(layerAnchor,false);canvas.transform.localPosition=Vector3.zero;canvas.transform.localRotation=Quaternion.identity;canvas.transform.localScale=Vector3.one*(.4f/(1.3f*600));var rect=(RectTransform)canvas.transform;rect.sizeDelta=new Vector2(600,350);
  foreach(Transform child in canvas.transform.Cast<Transform>().ToArray())UnityEngine.Object.DestroyImmediate(child.gameObject);
  InspectionUI.Image(canvas.transform,"Background",Vector2.zero,new Vector2(600,350),InspectionUI.Background);
  var label=InspectionUI.Text(canvas.transform,new Vector2(0,128),new Vector2(570,75),25,InspectionUI.Ink);label.alignment=TextAnchor.MiddleCenter;label.text="Слои";
  var chooser=model.GetComponent<ChoiceLayer>();var actions=new UnityEngine.Events.UnityAction[]{chooser.ShowSkin,chooser.ShowMuscles,chooser.ShowBones,chooser.ShowBrain};var labels=new[]{"Кожа","Мышцы","Кости","Мозг"};
  for(int i=0;i<4;i++){var button=InspectionUI.Button(canvas.transform,labels[i],new Vector2(i%2==0?-146:146,i<2?10:-108),new Vector2(278,99),()=>{});button.onClick.RemoveAllListeners();UnityEventTools.AddPersistentListener(button.onClick,actions[i]);button.GetComponentInChildren<Text>().fontSize=27;}
  var desk=Place("AGAU_Workdesk",research,new Vector3(3.05f,0,.80f),Quaternion.Euler(0,270,0),Vector3.one);
  var computer=Place("AGAU_Computer",research,new Vector3(3.06f,.755f,.44f),Quaternion.Euler(0,270,0),Vector3.one);var screen=Screen(computer,"Research screen anchor");
  var logical=Find("Cube (2)").transform;logical.position=screen.position;logical.rotation=Quaternion.Euler(0,180,0);
  var zone=Find("Cube (1)");zone.transform.position=new Vector3(2.85f,.881f,1.17f);var station=new SerializedObject(zone.GetComponent<ElementInspectionStation>());station.FindProperty("_computer").objectReferenceValue=logical;station.FindProperty("_screenAnchor").objectReferenceValue=screen;station.FindProperty("_monitorSize").vector2Value=new Vector2(.393f,.2882f);station.ApplyModifiedPropertiesWithoutUndo();
 }
 static void InstallPractice()
 {
  var assembly=Find("Assembly Table").transform;var supply=Find("Supply Table").transform;HideGeometry(assembly.gameObject);HideGeometry(supply.gameObject);
  Place("Vet_Table",assembly,Vector3.zero,Quaternion.identity,new Vector3(2.15f/1.22f,.78f/.857f,.95f/.61f));
  var stand=Place("AGAU_Anatomy_Stand",assembly,new Vector3(.05f,.78f,0),Quaternion.identity,Vector3.one*1.25f);
  var desk=Place("AGAU_Workdesk",supply,Vector3.zero,Quaternion.Euler(0,180,0),new Vector3(2.4f/1.4f,.78f/.755f,1.15f/.65f));
  Find("Supply surface").transform.localPosition=new Vector3(0,.78f,-.06f);
  var so=new SerializedObject(UnityEngine.Object.FindAnyObjectByType<PracticeSession>());so.FindProperty("_assemblySurfaceHeight").floatValue=BoundsOf(stand).max.y+.006f;so.FindProperty("_supplySize").vector2Value=new Vector2(2.28f,.86f);so.ApplyModifiedPropertiesWithoutUndo();
 }

 static string[] _bakeScenes={"Assets/Scenes/SampleScene.unity","Assets/Scenes/PracticeScene.unity"};static int _bakeIndex;
 [MenuItem("Tools/Clinic Furniture/Bake both rooms")]
 public static void BakeBoth(){if(EditorApplication.isPlaying||Lightmapping.isRunning)throw new Exception("Stop Play Mode / current bake first.");_bakeIndex=0;Directory.CreateDirectory(Folder+"/Lighting");AssetDatabase.Refresh();File.WriteAllText("/tmp/clinic-bake.txt","Baking study room...\n");BakeNext();}
 static void BakeNext(){EditorSceneManager.OpenScene(_bakeScenes[_bakeIndex]);Lightmapping.bakeCompleted-=Baked;Lightmapping.bakeCompleted+=Baked;if(!Lightmapping.BakeAsync())throw new Exception("Cannot start furniture bake.");}
 static void Baked()
 {
  Lightmapping.bakeCompleted-=Baked;var probe=UnityEngine.Object.FindAnyObjectByType<ReflectionProbe>();string tag=_bakeIndex==0?"Study":"Practice";
  bool reflection=probe&&Lightmapping.BakeReflectionProbe(probe,Folder+"/Lighting/"+tag+"Reflection.exr");EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();
  File.AppendAllText("/tmp/clinic-bake.txt",tag+" maps="+LightmapSettings.lightmaps.Length+" probes="+(LightmapSettings.lightProbes?LightmapSettings.lightProbes.count:0)+" reflection="+reflection+"\n");
  if(++_bakeIndex<_bakeScenes.Length)EditorApplication.delayCall+=BakeNext;else{File.AppendAllText("/tmp/clinic-bake.txt","SUCCESS\n");EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");Preview();}
 }
 [MenuItem("Tools/Clinic Furniture/Render current room")]
 public static void Preview()
 {
  string tag=SceneManager.GetActiveScene().name;Directory.CreateDirectory("output/clinic-furniture");
  var rig=Find("TechDemoXRRigOpenXR");var avatars=rig.GetComponentsInChildren<Renderer>(true);var saved=avatars.Select(r=>r.forceRenderingOff).ToArray();foreach(var r in avatars)r.forceRenderingOff=true;
  try{
   var views=new[]{new[]{new Vector3(-.85f,1.65f,2.15f),new Vector3(1.4f,1.15f,-.65f)},new[]{new Vector3(-.12f,1.60f,-.35f),new Vector3(.48f,1.12f,-1.3f)},new[]{new Vector3(1.7f,1.40f,1.05f),new Vector3(3.03f,1f,.78f)}};
   for(int i=0;i<views.Length;i++){var go=new GameObject("Furniture preview");go.hideFlags=HideFlags.HideAndDontSave;var camera=go.AddComponent<Camera>();camera.enabled=false;camera.transform.position=views[i][0];camera.transform.LookAt(views[i][1]);camera.fieldOfView=i==0?70:64;camera.nearClipPlane=.02f;camera.farClipPlane=20;camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
    SaveCamera(camera,"output/clinic-furniture/"+tag+"-"+i+".jpg");if(i==0&&SceneView.lastActiveSceneView){SceneView.lastActiveSceneView.AlignViewToObject(camera.transform);SceneView.lastActiveSceneView.Repaint();}UnityEngine.Object.DestroyImmediate(go);
   }
  }finally{for(int i=0;i<avatars.Length;i++)if(avatars[i])avatars[i].forceRenderingOff=saved[i];}
 }
 public static void SaveCamera(Camera camera,string path)
 {
  var oldTarget=camera.targetTexture;var rt=new RenderTexture(1600,1000,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);camera.targetTexture=rt;camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(1600,1000,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1600,1000),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToJPG(94));RenderTexture.active=old;camera.targetTexture=oldTarget;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);
 }


}
