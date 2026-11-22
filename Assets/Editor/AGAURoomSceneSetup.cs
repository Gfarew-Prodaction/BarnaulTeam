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

public static class AGAURoomSceneSetup
{
 public const string Folder="Assets/Models/AGAURoom";
 public const string RoomName="AGAU · Teaching Room";
 public static readonly Vector3 Center=new Vector3(1.25f,0,0);
 static GameObject Find(string name)=>SceneManager.GetActiveScene().GetRootGameObjects().FirstOrDefault(g=>g.name==name);
 [MenuItem("Tools/AGAU Room/Install room and lighting")]
 public static void Install()
 {
  if(EditorApplication.isPlaying)throw new Exception("Exit Play Mode before installing the room.");
  var scene=SceneManager.GetActiveScene();if(scene.path!="Assets/Scenes/SampleScene.unity")throw new Exception("Open SampleScene first.");
  var existingRoom=Find(RoomName);
  if(existingRoom){if(!Find("Anatomy Workstation")&&!Find("AGAU · Interior Lighting"))UnityEngine.Object.DestroyImmediate(existingRoom);else{File.WriteAllText("/tmp/agau-room-status.txt","Room already installed.");return;}}
  Directory.CreateDirectory("Backups");if(!File.Exists("Backups/SampleScene-before-AGAU-room.unity"))File.Copy(scene.path,"Backups/SampleScene-before-AGAU-room.unity");
  PrepareMaterials();
  var asset=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/AGAU_Room_Complete_GameReady.fbx");
  var room=(GameObject)PrefabUtility.InstantiatePrefab(asset,scene);room.name=RoomName;room.transform.position=Center+new Vector3(2.5f,0,2.5f);
  foreach(var r in room.GetComponentsInChildren<MeshRenderer>(true)){
   if(r.name.StartsWith("UCX_")){
    r.enabled=false;var col=r.GetComponent<MeshCollider>();if(!col)col=r.gameObject.AddComponent<MeshCollider>();col.sharedMesh=r.GetComponent<MeshFilter>().sharedMesh;col.convex=false;
   }else{
    r.enabled=true;r.receiveGI=ReceiveGI.Lightmaps;
    GameObjectUtility.SetStaticEditorFlags(r.gameObject,StaticEditorFlags.ContributeGI|StaticEditorFlags.BatchingStatic|StaticEditorFlags.ReflectionProbeStatic|StaticEditorFlags.OccluderStatic|StaticEditorFlags.OccludeeStatic);
   }
  }
  // Both existing stations fit inside the room at their current locations; keep their world poses.
  Group("Anatomy Workstation",new[]{"VetTable","Cube","Dog Head · Layer Model","Element Tray - 4 Places","Brain","Eye","Brain · Home silhouette","Eye · Home silhouette"});
  Group("Research Workstation",new[]{"office_desk_with_drawers","Cube (1)","Cube (2)","Inspection Gallery Center"});
  FitGallery();
  foreach(var stationName in new[]{"VetTable","office_desk_with_drawers"}){
   var station=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).First(t=>t.name==stationName);
   foreach(var r in station.GetComponentsInChildren<MeshRenderer>()){
    GameObjectUtility.SetStaticEditorFlags(r.gameObject,StaticEditorFlags.ContributeGI|StaticEditorFlags.BatchingStatic|StaticEditorFlags.ReflectionProbeStatic);
    r.receiveGI=ReceiveGI.LightProbes;
   }
  }
  var plane=Find("Plane");if(plane)plane.SetActive(false);
  var oldLight=Find("Directional Light");if(oldLight)oldLight.SetActive(false);
  var rig=Find("TechDemoXRRigOpenXR");rig.transform.SetPositionAndRotation(new Vector3(1.05f,.02f,-.05f),Quaternion.Euler(0,199,0));
  var zone=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).First(t=>t.name=="Cube (1)");if(zone.GetComponent<MeshRenderer>())zone.GetComponent<MeshRenderer>().enabled=false;
  var monitor=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).First(t=>t.name=="Cube (2)");
  var shell=new Material(Shader.Find("Universal Render Pipeline/Lit"));shell.name="Research monitor housing";shell.color=new Color(.025f,.035f,.038f);shell.SetFloat("_Smoothness",.3f);AssetDatabase.CreateAsset(shell,Folder+"/Materials/ResearchMonitor.mat");monitor.GetComponent<MeshRenderer>().sharedMaterial=shell;
  SetupLighting();
  AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
  File.WriteAllText("/tmp/agau-room-status.txt","Installed room, stations, player spawn, colliders and lighting. Ready to bake.");
 }
 static void Group(string name,string[] children){var group=new GameObject(name);foreach(var n in children){var go=Find(n);if(go)go.transform.SetParent(group.transform,true);}}
 public static void PrepareMaterials()
 {
  Directory.CreateDirectory(Folder+"/Materials");Directory.CreateDirectory(Folder+"/Lighting");AssetDatabase.Refresh();
  foreach(var guid in AssetDatabase.FindAssets("t:Texture2D",new[]{Folder+"/Textures"})){
   string path=AssetDatabase.GUIDToAssetPath(guid);var importer=AssetImporter.GetAtPath(path) as TextureImporter;if(!importer)continue;
   bool normal=path.Contains("_Normal"),data=path.Contains("Roughness")||path.Contains("Metallic");
   importer.textureType=normal?TextureImporterType.NormalMap:TextureImporterType.Default;importer.sRGBTexture=!normal&&!data;importer.flipGreenChannel=false;
   importer.mipmapEnabled=true;importer.maxTextureSize=path.Contains("2K")||path.Contains("2048")?2048:1024;importer.textureCompression=TextureImporterCompression.Compressed;importer.SaveAndReimport();
  }
  var json=JObject.Parse(File.ReadAllText(Folder+"/PBR_Materials.json"));var materials=new Dictionary<string,Material>();
  foreach(var entry in json.Properties()){
   var value=entry.Value;var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.name=entry.Name;var textures=value["textures"] as JObject;
   if(textures?["Base Color"]!=null){mat.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/"+(string)textures["Base Color"]));mat.color=Color.white;}
   else{var c=value["Base Color"]?.Values<float>().ToArray();mat.color=c!=null?new Color(c[0],c[1],c[2],c[3]):Color.white;}
   mat.SetFloat("_Metallic",(float?)(value["Metallic"])??0);mat.SetFloat("_Smoothness",1-((float?)(value["Roughness"])??.7f));
   var packed=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/Textures/Packed/"+entry.Name+"_MetallicSmoothness.png");
   if(packed){mat.SetTexture("_MetallicGlossMap",packed);mat.SetFloat("_Smoothness",1);mat.EnableKeyword("_METALLICSPECGLOSSMAP");}
   if(textures?["Normal"]!=null){mat.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"/"+(string)textures["Normal"]));mat.SetFloat("_BumpScale",.65f);mat.EnableKeyword("_NORMALMAP");}
   if(entry.Name=="M_LED_Diffuser_4000K"){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",new Color(1,.83f,.6f)*2.8f);mat.globalIlluminationFlags=MaterialGlobalIlluminationFlags.BakedEmissive;}
   else mat.globalIlluminationFlags=MaterialGlobalIlluminationFlags.EmissiveIsBlack;
   var path=Folder+"/Materials/"+entry.Name+".mat";var existing=AssetDatabase.LoadAssetAtPath<Material>(path);
   if(existing){EditorUtility.CopySerialized(mat,existing);UnityEngine.Object.DestroyImmediate(mat);mat=existing;EditorUtility.SetDirty(mat);}else AssetDatabase.CreateAsset(mat,path);
   materials.Add(entry.Name,mat);
  }
  var model=(ModelImporter)AssetImporter.GetAtPath(Folder+"/AGAU_Room_Complete_GameReady.fbx");
  foreach(var entry in materials)model.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),entry.Key),entry.Value);
  model.SaveAndReimport();AssetDatabase.SaveAssets();
 }
 static Light LightAt(Transform parent,string name,Vector3 position,LightType type,float intensity,float kelvin,LightmapBakeType bake)
 {
  var go=new GameObject(name);go.transform.SetParent(parent,false);go.transform.position=position;go.transform.rotation=Quaternion.Euler(90,0,0);
  var light=go.AddComponent<Light>();light.type=type;light.intensity=intensity;light.useColorTemperature=true;light.colorTemperature=kelvin;light.range=7;light.lightmapBakeType=bake;return light;
 }
 public static void SetupLighting()
 {
  var root=new GameObject("AGAU · Interior Lighting").transform;
  RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.3f,.34f,.38f);RenderSettings.ambientEquatorColor=new Color(.16f,.17f,.18f);RenderSettings.ambientGroundColor=new Color(.08f,.075f,.065f);RenderSettings.ambientIntensity=.55f;
  RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.reflectionIntensity=.7f;RenderSettings.fog=false;RenderSettings.sun=null;
  foreach(float x in new[]{-.25f,2.75f})foreach(float z in new[]{-1.5f,1.5f}){
   var l=LightAt(root,"Ceiling · Baked softbox",new Vector3(x,2.94f,z),LightType.Rectangle,18f,4200,LightmapBakeType.Baked);l.areaSize=new Vector2(.65f,.65f);l.shadows=LightShadows.Soft;
  }
  var lab=LightAt(root,"Anatomy · Neutral task light",new Vector3(.6f,2.75f,-1.25f),LightType.Spot,6.2f,4600,LightmapBakeType.Realtime);lab.spotAngle=105;lab.innerSpotAngle=65;lab.shadows=LightShadows.None;
  var research=LightAt(root,"Research · Neutral task light",new Vector3(3,2.75f,.8f),LightType.Spot,6f,4600,LightmapBakeType.Realtime);research.spotAngle=105;research.innerSpotAngle=65;research.shadows=LightShadows.None;
  var emblem=LightAt(root,"Emblem · Warm wall wash",new Vector3(2.7f,2.75f,-1.6f),LightType.Spot,1.4f,3300,LightmapBakeType.Realtime);emblem.transform.LookAt(new Vector3(2.7f,2.2f,-2.46f));emblem.spotAngle=85;emblem.innerSpotAngle=55;emblem.shadows=LightShadows.None;
  var fill=LightAt(root,"Room · Subtle cool fill",new Vector3(1.25f,2.65f,.4f),LightType.Point,1.2f,5600,LightmapBakeType.Realtime);fill.shadows=LightShadows.None;
  var probes=new GameObject("Room · Light probes");probes.transform.SetParent(root,false);var group=probes.AddComponent<LightProbeGroup>();var points=new List<Vector3>();
  foreach(float x in new[]{-.5f,1.25f,3f})foreach(float z in new[]{-1.7f,0f,1.7f})foreach(float y in new[]{.55f,1.25f,2f})points.Add(new Vector3(x,y,z));group.probePositions=points.ToArray();
  var reflectionGO=new GameObject("Room · Baked reflection");reflectionGO.transform.SetParent(root,false);reflectionGO.transform.position=Center+Vector3.up*1.6f;var probe=reflectionGO.AddComponent<ReflectionProbe>();probe.mode=ReflectionProbeMode.Baked;probe.resolution=128;probe.size=new Vector3(5,3.2f,5);probe.boxProjection=true;probe.blendDistance=.3f;probe.intensity=.75f;probe.hdr=true;
  var profile=ScriptableObject.CreateInstance<VolumeProfile>();profile.name="AGAU Room Atmosphere";
  var tone=profile.Add<Tonemapping>(true);tone.mode.Override(TonemappingMode.ACES);
  var bloom=profile.Add<Bloom>(true);bloom.intensity.Override(.12f);bloom.threshold.Override(1.2f);bloom.scatter.Override(.5f);bloom.highQualityFiltering.Override(false);
  var colors=profile.Add<ColorAdjustments>(true);colors.postExposure.Override(.2f);colors.contrast.Override(5);colors.saturation.Override(-3);
  var white=profile.Add<WhiteBalance>(true);white.temperature.Override(3);
  AssetDatabase.CreateAsset(profile,Folder+"/Lighting/RoomAtmosphere.asset");foreach(var c in profile.components)AssetDatabase.AddObjectToAsset(c,profile);
  var volume=Find("Global Volume").GetComponent<Volume>();volume.sharedProfile=profile;volume.isGlobal=true;
  foreach(var camera in Find("TechDemoXRRigOpenXR").GetComponentsInChildren<Camera>(true))if(camera.CompareTag("MainCamera")){camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;camera.backgroundColor=new Color(.04f,.05f,.055f);camera.clearFlags=CameraClearFlags.SolidColor;}
  var settings=new LightingSettings();settings.name="AGAU Room Baked Lighting";settings.bakedGI=true;settings.realtimeGI=false;settings.lightmapper=LightingSettings.Lightmapper.UnityComputeGPU;settings.directSampleCount=32;settings.indirectSampleCount=128;settings.environmentSampleCount=64;settings.lightmapResolution=20;settings.lightmapMaxSize=1024;settings.maxBounces=3;settings.compressLightmaps=true;settings.ao=true;settings.aoMaxDistance=.35f;settings.aoExponentIndirect=1.2f;
  AssetDatabase.CreateAsset(settings,Folder+"/Lighting/RoomLightingSettings.lighting");Lightmapping.lightingSettings=settings;
 }
 public static void FitGallery(){
  var anchor=UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include).FirstOrDefault(t=>t.name=="Inspection Gallery Center");
  if(anchor)anchor.position=new Vector3(2.15f,1.5f,.95f);
 }
 public static void RetuneLighting(){
  foreach(var l in Find("AGAU · Interior Lighting").GetComponentsInChildren<Light>()){
   if(l.type==LightType.Rectangle){l.transform.position=new Vector3(l.transform.position.x,2.94f,l.transform.position.z);l.intensity=18;}
   else if(l.name.StartsWith("Anatomy"))l.intensity=6.2f;
   else if(l.name.StartsWith("Research"))l.intensity=6;
   else if(l.type==LightType.Point)l.intensity=1.2f;
  }
  RenderSettings.ambientSkyColor=new Color(.3f,.34f,.38f);RenderSettings.ambientEquatorColor=new Color(.16f,.17f,.18f);RenderSettings.ambientGroundColor=new Color(.08f,.075f,.065f);
  EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
 }
 [MenuItem("Tools/AGAU Room/Bake room lighting")]
 public static void Bake(){Lightmapping.bakeCompleted-=FinishedBake;Lightmapping.bakeCompleted+=FinishedBake;File.WriteAllText("/tmp/agau-room-status.txt","Baking lightmaps and light probes...");if(!Lightmapping.BakeAsync())throw new Exception("Lightmapper failed to start");}
 public static void FinishedBake(){
  Lightmapping.bakeCompleted-=FinishedBake;
  var probe=UnityEngine.Object.FindObjectsByType<ReflectionProbe>().FirstOrDefault(p=>p.name=="Room · Baked reflection");
  bool reflected=probe&&Lightmapping.BakeReflectionProbe(probe,Folder+"/Lighting/RoomReflection.exr");
  EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();File.WriteAllText("/tmp/agau-room-status.txt","BAKED lightmaps="+LightmapSettings.lightmaps.Length+" probes="+(LightmapSettings.lightProbes?LightmapSettings.lightProbes.count:0)+" reflection="+reflected);
 }
 [MenuItem("Tools/AGAU Room/Render preview")]
 public static void RenderPreview(){
  Directory.CreateDirectory("output/agau-room");
  var rig=Find("TechDemoXRRigOpenXR");var avatars=rig.GetComponentsInChildren<Renderer>(true);var hidden=avatars.Select(r=>r.forceRenderingOff).ToArray();foreach(var r in avatars)r.forceRenderingOff=true;
  try{
  var views=new[]{new[]{new Vector3(-.85f,1.65f,2.15f),new Vector3(1.4f,1.25f,-.7f)},new[]{new Vector3(-.12f,1.35f,-.65f),new Vector3(.55f,1.04f,-1.35f)},new[]{new Vector3(1.35f,1.5f,.65f),new Vector3(3.12f,1,.55f)}};
  for(int i=0;i<views.Length;i++){
   var go=new GameObject("Room preview camera");go.hideFlags=HideFlags.HideAndDontSave;var camera=go.AddComponent<Camera>();camera.enabled=false;camera.transform.position=views[i][0];camera.transform.LookAt(views[i][1]);camera.fieldOfView=72;camera.nearClipPlane=.03f;camera.farClipPlane=20;camera.allowHDR=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.03f,.04f,.05f);camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;
   var rt=new RenderTexture(1600,1000,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);camera.targetTexture=rt;camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;var tex=new Texture2D(1600,1000,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1600,1000),0,0);tex.Apply();File.WriteAllBytes("output/agau-room/room-"+i+".jpg",tex.EncodeToJPG(93));RenderTexture.active=old;if(i==0&&SceneView.lastActiveSceneView){SceneView.lastActiveSceneView.AlignViewToObject(camera.transform);SceneView.lastActiveSceneView.Repaint();}camera.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(tex);UnityEngine.Object.DestroyImmediate(go);
  }
  File.WriteAllText("/tmp/agau-room-preview.txt","SUCCESS");
  }finally{for(int i=0;i<avatars.Length;i++)if(avatars[i])avatars[i].forceRenderingOff=hidden[i];}
 }
}
