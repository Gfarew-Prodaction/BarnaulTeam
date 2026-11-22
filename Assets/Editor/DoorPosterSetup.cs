using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
[InitializeOnLoad]
public static class DoorPosterSetup
{
 const string Request="/tmp/braindog-door-poster-request";
 static DoorPosterSetup(){EditorApplication.update+=Poll;}
 static void Poll(){if(!File.Exists(Request)||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||BuildPipeline.isBuildingPlayer)return;File.Delete(Request);try{Install();}catch(Exception e){File.WriteAllText("/tmp/braindog-door-poster-status",e.ToString());}}
 [MenuItem("Tools/AGAU Room/Install door poster")]
 public static void Install(){
  const string folder="Assets/Models/AGAURoom/Decor";
  AssetDatabase.ImportAsset(folder+"/DogMotivationPoster.png",ImportAssetOptions.ForceSynchronousImport);
  var importer=(TextureImporter)AssetImporter.GetAtPath(folder+"/DogMotivationPoster.png");importer.npotScale=TextureImporterNPOTScale.None;importer.maxTextureSize=2048;importer.mipmapEnabled=true;importer.textureCompression=TextureImporterCompression.CompressedHQ;importer.wrapMode=TextureWrapMode.Clamp;importer.SaveAndReimport();
  var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(folder+"/DogMotivationPoster.png");
  var mat=AssetDatabase.LoadAssetAtPath<Material>(folder+"/DogMotivationPoster.mat");if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Unlit"));AssetDatabase.CreateAsset(mat,folder+"/DogMotivationPoster.mat");}mat.SetTexture("_BaseMap",texture);mat.SetColor("_BaseColor",Color.white);EditorUtility.SetDirty(mat);
  var frame=AssetDatabase.LoadAssetAtPath<Material>(folder+"/PosterFrame.mat");if(!frame){frame=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(frame,folder+"/PosterFrame.mat");}frame.color=new Color(.035f,.045f,.05f);EditorUtility.SetDirty(frame);
  Directory.CreateDirectory("Backups/DoorPoster");string report="";
  foreach(var path in new[]{"Assets/Scenes/SampleScene.unity","Assets/Scenes/PracticeScene.unity"}){
   var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
   var door=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Renderer>(true)).First(r=>r.name.StartsWith("SM_Door_Interactive_Leaf"));
   if(!File.Exists("Backups/DoorPoster/"+Path.GetFileName(path)))File.Copy(path,"Backups/DoorPoster/"+Path.GetFileName(path));
   var prior=door.transform.Find("Dog motivation poster");if(prior)UnityEngine.Object.DestroyImmediate(prior.gameObject);
   var root=new GameObject("Dog motivation poster");SceneManager.MoveGameObjectToScene(root,scene);root.transform.SetParent(door.transform,false);root.transform.position=new Vector3(door.bounds.max.x+.012f,1.52f,door.bounds.center.z);root.transform.rotation=Quaternion.identity;
   float width=.72f,height=width*texture.height/texture.width;
   var border=GameObject.CreatePrimitive(PrimitiveType.Cube);border.name="Thin dark frame";border.transform.SetParent(root.transform,false);border.transform.localPosition=new Vector3(-.009f,0,0);border.transform.localScale=new Vector3(.012f,height+.028f,width+.028f);UnityEngine.Object.DestroyImmediate(border.GetComponent<Collider>());border.GetComponent<Renderer>().sharedMaterial=frame;
   var poster=GameObject.CreatePrimitive(PrimitiveType.Quad);poster.name="Poster image";poster.transform.SetParent(root.transform,false);poster.transform.localRotation=Quaternion.Euler(0,-90,0);poster.transform.localScale=new Vector3(width,height,1);UnityEngine.Object.DestroyImmediate(poster.GetComponent<Collider>());var renderer=poster.GetComponent<Renderer>();renderer.sharedMaterial=mat;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;renderer.receiveShadows=false;
   EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);report+=path+" poster="+root.transform.position+" size="+width+"x"+height+" parent="+door.name+"\n";
   if(path.Contains("SampleScene"))Preview(root.transform.position);
   if(opened)EditorSceneManager.CloseScene(scene,true);
  }
  AssetDatabase.SaveAssets();File.WriteAllText("/tmp/braindog-door-poster-status","SUCCESS\n"+report);
 }
 static void Preview(Vector3 center){
  Directory.CreateDirectory("output/door-poster");var go=new GameObject("Poster preview camera");go.hideFlags=HideFlags.HideAndDontSave;var c=go.AddComponent<Camera>();c.enabled=false;c.transform.position=center+new Vector3(2.1f,.05f,0);c.transform.LookAt(center-new Vector3(0,.27f,0));c.fieldOfView=63;c.nearClipPlane=.03f;c.farClipPlane=15;var rt=new RenderTexture(1200,1000,24);c.targetTexture=rt;var previous=RenderTexture.active;try{c.Render();RenderTexture.active=rt;var tex=new Texture2D(1200,1000,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1200,1000),0,0);tex.Apply();File.WriteAllBytes("output/door-poster/door.jpg",tex.EncodeToJPG(94));UnityEngine.Object.DestroyImmediate(tex);if(SceneView.lastActiveSceneView){SceneView.lastActiveSceneView.AlignViewToObject(c.transform);SceneView.lastActiveSceneView.Repaint();}}finally{RenderTexture.active=previous;c.targetTexture=null;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(go);}
 }
}
