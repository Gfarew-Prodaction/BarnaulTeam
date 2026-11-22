using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class MedicalTVSetup
{
 public const string Name="Medical_TV_Black";
 [MenuItem("Tools/Medical TV/Prepare TV")]
 public static void Prepare()
 {
  ClinicFurnitureSetup.PrepareAssets(new[]{Name});
  var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(ClinicFurnitureSetup.Folder+"/Prefabs/"+Name+".prefab");var go=UnityEngine.Object.Instantiate(prefab);
  string result="";foreach(var f in go.GetComponentsInChildren<MeshFilter>(true)){var r=f.GetComponent<Renderer>();result+=f.name+" | "+f.sharedMesh.bounds+" | world="+(r?r.bounds.ToString():"")+" | "+(r?string.Join(",",r.sharedMaterials.Select(m=>m.name)):"")+"\n";}
  File.WriteAllText("/tmp/tv-meshes.txt",result);UnityEngine.Object.DestroyImmediate(go);
 }
 [MenuItem("Tools/Medical TV/Install in both cabinets")]
 public static void Install()
 {
  if(EditorApplication.isPlaying)throw new Exception("Stop Play Mode first.");
  var scene=SceneManager.GetActiveScene();if(scene.isDirty)EditorSceneManager.SaveScene(scene);Directory.CreateDirectory("Backups/MedicalTV");
  foreach(var path in new[]{"Assets/Scenes/SampleScene.unity","Assets/Scenes/PracticeScene.unity"})
  {
   string backup="Backups/MedicalTV/"+Path.GetFileName(path);if(!File.Exists(backup))File.Copy(path,backup);EditorSceneManager.OpenScene(path);
   var tv=UnityEngine.Object.FindAnyObjectByType<MedicalTVScreen>();
   GameObject go=tv?tv.gameObject:(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(ClinicFurnitureSetup.Folder+"/Prefabs/"+Name+".prefab"));
   go.name="Medical TV · Wall display";go.transform.SetPositionAndRotation(new Vector3(.5f,1.62f,-2.22f),Quaternion.identity);go.transform.localScale=Vector3.one*1.55f;
   var display=go.GetComponentsInChildren<MeshFilter>(true).First(f=>!f.name.StartsWith("UCX")&&f.GetComponent<Renderer>().sharedMaterials.Any(m=>m.name.Contains("Inactive_Display")));
   var mesh=display.sharedMesh;var vertices=mesh.vertices;var triangles=mesh.triangles;float largest=0;Vector3 normal=Vector3.zero;
   for(int i=0;i<triangles.Length;i+=3){var a=display.transform.TransformPoint(vertices[triangles[i]]);var b=display.transform.TransformPoint(vertices[triangles[i+1]]);var c=display.transform.TransformPoint(vertices[triangles[i+2]]);var n=Vector3.Cross(b-a,c-a);if(n.sqrMagnitude>largest){largest=n.sqrMagnitude;normal=n.normalized;}}
   if(Vector3.Dot(normal,Vector3.forward)<0)normal=-normal;
   float front=vertices.Max(v=>Vector3.Dot(display.transform.TransformPoint(v),normal));var center=display.GetComponent<Renderer>().bounds.center;center+=normal*(front-Vector3.Dot(center,normal));
   var anchor=go.transform.Find("TV screen anchor");if(!anchor){anchor=new GameObject("TV screen anchor").transform;anchor.SetParent(go.transform,true);}
   anchor.SetPositionAndRotation(center+normal*.004f,Quaternion.LookRotation(-normal,Vector3.up));
   float width=vertices.Max(v=>Vector3.Dot(display.transform.TransformPoint(v),anchor.right))-vertices.Min(v=>Vector3.Dot(display.transform.TransformPoint(v),anchor.right));
   float height=vertices.Max(v=>Vector3.Dot(display.transform.TransformPoint(v),anchor.up))-vertices.Min(v=>Vector3.Dot(display.transform.TransformPoint(v),anchor.up));
   if(!tv)tv=go.AddComponent<MedicalTVScreen>();tv.Configure(anchor,new Vector2(width*.98f,height*.98f));
   EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());EditorSceneManager.SaveScene(SceneManager.GetActiveScene());
   File.AppendAllText("/tmp/tv-install.txt",path+" screen="+tv.ScreenSize+" anchor="+anchor.position+" normal="+normal+" bounds="+ClinicFurnitureSetup.BoundsOf(go)+"\n");
  }
  EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");AssetDatabase.SaveAssets();File.AppendAllText("/tmp/tv-install.txt","SUCCESS\n");
 }
}
