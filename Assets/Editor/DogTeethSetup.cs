using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using InteracrSystem;

public static class DogTeethSetup
{
 public const string Folder="Assets/Models/DogHead/Teeth";
 public const string Fbx=Folder+"/teeth_dog.fbx";
 public static string Id(string name)=>name.StartsWith("chisels")?"dog_incisors":name.StartsWith("fangs")?"dog_canines":name.StartsWith("premolars")?"dog_premolars":name.StartsWith("molars")?"dog_molars":throw new Exception("Unmapped tooth: "+name);
 [MenuItem("Tools/Dog Teeth/Export alignment points")]
 public static void Dump()
 {
  if(EditorApplication.isPlaying)throw new Exception("Exit Play Mode first.");var current=SceneManager.GetActiveScene();if(current.isDirty)EditorSceneManager.SaveScene(current);EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
  var model=GameObject.Find("Dog Head · Layer Model");var old=model.GetComponentsInChildren<ElementDog>(true).Where(e=>e.Id=="dog_teeth"||e.Id=="dog_teeth_2").ToArray();
  if(old.Length!=2)throw new Exception("Need both original tooth groups to establish alignment.");
  var asset=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Fbx));
  var target=new JArray();var source=new JArray();
  foreach(var part in old){var vs=part.GetComponent<MeshFilter>().sharedMesh.vertices;int step=Mathf.Max(1,vs.Length/5000);for(int i=0;i<vs.Length;i+=step){var p=model.transform.InverseTransformPoint(part.transform.TransformPoint(vs[i]));target.Add(new JArray(p.x,p.y,p.z));}}
  foreach(var mf in asset.GetComponentsInChildren<MeshFilter>()){var vs=mf.sharedMesh.vertices;int step=Mathf.Max(1,vs.Length/700);for(int i=0;i<vs.Length;i+=step){var p=mf.transform.TransformPoint(vs[i]);source.Add(new JArray(p.x,p.y,p.z));}}
  File.WriteAllText("/tmp/teeth-alignment-points.json",new JObject{{"source",source},{"target",target}}.ToString());UnityEngine.Object.DestroyImmediate(asset);
 }
 static Material SaveMaterial(string name,Color color,bool transparent=false)
 {
  var path=Folder+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat)return mat;mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.color=color;mat.SetFloat("_Smoothness",.28f);
  if(transparent){mat.SetFloat("_Surface",1);mat.SetFloat("_SrcBlend",5);mat.SetFloat("_DstBlend",10);mat.SetFloat("_ZWrite",0);mat.SetFloat("_Cull",0);mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.renderQueue=3000;}
  AssetDatabase.CreateAsset(mat,path);return mat;
 }
 static Mesh SaveMesh(Mesh mesh,string name)
 {
  var path=Folder+"/Parts/"+name+".asset";var old=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(old){EditorUtility.CopySerialized(mesh,old);UnityEngine.Object.DestroyImmediate(mesh);return old;}AssetDatabase.CreateAsset(mesh,path);return mesh;
 }
 static Mesh Hull(Mesh mesh,string name)
 {
  var vs=mesh.vertices;var points=new List<Vector3>();for(int i=0;i<96;i++){float y=1-2*(i+.5f)/96;float r=Mathf.Sqrt(1-y*y),a=i*2.399963f;var d=new Vector3(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r);var best=vs.OrderByDescending(v=>Vector3.Dot(v,d)).First();if(!points.Contains(best))points.Add(best);}
  var hull=new Mesh{name=name+" hull",vertices=points.ToArray()};var tris=new int[(points.Count-2)*3];for(int i=0;i<points.Count-2;i++){tris[i*3]=0;tris[i*3+1]=i+1;tris[i*3+2]=i+2;}hull.triangles=tris;hull.RecalculateBounds();return SaveMesh(hull,name+"_hull");
 }
 [MenuItem("Tools/Dog Teeth/Install labelled teeth")]
 public static void Install()
 {
  if(EditorApplication.isPlaying)throw new Exception("Exit Play Mode first.");Directory.CreateDirectory(Folder+"/Parts");Directory.CreateDirectory("Backups/DogTeeth");AssetDatabase.Refresh();
  var json=JObject.Parse(File.ReadAllText(Folder+"/alignment.json"));var rows=(JArray)json["matrix"];var fit=Matrix4x4.identity;for(int r=0;r<4;r++)for(int c=0;c<4;c++)fit[r,c]=(float)rows[r][c];
  EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");var scene=SceneManager.GetActiveScene();string backup="Backups/DogTeeth/SampleScene.unity";if(!File.Exists(backup))File.Copy(scene.path,backup);
  var model=GameObject.Find("Dog Head · Layer Model").transform;var bones=model.Find("Bones and teeth");
  var legacy=model.GetComponentsInChildren<ElementDog>(true).Where(e=>e.Id=="dog_teeth"||e.Id=="dog_teeth_2"||new[]{"dog_incisors","dog_canines","dog_premolars","dog_molars"}.Contains(e.Id)).ToArray();
  foreach(var part in legacy){var visual=new SerializedObject(part.GetComponent<ElementInteractionVisuals>());var ghost=visual.FindProperty("_ghost").objectReferenceValue;if(ghost)UnityEngine.Object.DestroyImmediate(ghost);UnityEngine.Object.DestroyImmediate(part.gameObject);}
  var template=bones.GetComponentsInChildren<ReturningGrabbable>(true).First();var enamel=SaveMaterial("Tooth enamel",new Color(.94f,.9f,.8f));var ghostMat=SaveMaterial("Tooth home silhouette",new Color(.12f,.8f,.65f,.2f),true);
  var source=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(Folder+"/teeth_runtime.fbx"));var pieces=new List<ElementDog>();
  foreach(var mf in source.GetComponentsInChildren<MeshFilter>())
  {
   string id=Id(mf.name),key=mf.name.Replace('.','_');var matrix=fit*mf.transform.localToWorldMatrix;var vs=mf.sharedMesh.vertices.Select(v=>matrix.MultiplyPoint3x4(v)).ToArray();var bounds=new Bounds(vs[0],Vector3.zero);foreach(var v in vs)bounds.Encapsulate(v);
   var mesh=UnityEngine.Object.Instantiate(mf.sharedMesh);mesh.name=key;mesh.vertices=vs.Select(v=>v-bounds.center).ToArray();
   if(matrix.determinant<0)for(int sub=0;sub<mesh.subMeshCount;sub++){var t=mesh.GetTriangles(sub);for(int i=0;i<t.Length;i+=3){int x=t[i];t[i]=t[i+2];t[i+2]=x;}mesh.SetTriangles(t,sub);}
   mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.RecalculateTangents();mesh=SaveMesh(mesh,key);var go=new GameObject(mf.name);go.layer=LayerMask.NameToLayer("Grabbable");go.transform.SetParent(bones,false);go.transform.localPosition=bounds.center;
   go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=Enumerable.Repeat(enamel,mesh.subMeshCount).ToArray();go.AddComponent<MeshCollider>().sharedMesh=Hull(mesh,key);go.GetComponent<MeshCollider>().convex=true;
   var body=go.AddComponent<Rigidbody>();body.mass=.015f;body.useGravity=false;body.isKinematic=true;body.interpolation=RigidbodyInterpolation.Interpolate;
   var grab=go.AddComponent<ReturningGrabbable>();EditorUtility.CopySerialized(template,grab);ResetGrab(grab,body);
   var element=go.AddComponent<ElementDog>();var data=new SerializedObject(element);data.FindProperty("_id").stringValue=id;data.ApplyModifiedPropertiesWithoutUndo();pieces.Add(element);
   go.AddComponent<ElementInfoPresenter>();go.AddComponent<OnMoveBackItem>().SetReturnFrame(bones);
   var ghost=new GameObject(mf.name+" · Home silhouette");ghost.layer=LayerMask.NameToLayer("Ignore Raycast");ghost.transform.SetParent(bones,false);ghost.transform.localPosition=bounds.center;ghost.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=ghost.AddComponent<MeshRenderer>();renderer.sharedMaterials=Enumerable.Repeat(ghostMat,mesh.subMeshCount).ToArray();renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;ghost.SetActive(false);
   go.AddComponent<ElementInteractionVisuals>().Configure(id,model,ghost,bones);
  }
  UnityEngine.Object.DestroyImmediate(source);
  foreach(var text in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<UnityEngine.UI.Text>(true)))text.text=text.text.Replace("Кости + мышцы","Мышцы").Replace("Кости и мышцы","Мышцы");
  foreach(var text in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TMPro.TMP_Text>(true)))text.text=text.text.Replace("Кости + мышцы","Мышцы").Replace("Кости и мышцы","Мышцы");
  EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
  // Preserve the existing practice scene, its tables, controls and neurological mechanism.
  var records=pieces.Select(e=>new ToothRecord{Id=e.Id,Name=e.name,Position=model.InverseTransformPoint(e.transform.position),Rotation=Quaternion.Inverse(model.rotation)*e.transform.rotation,Mesh=e.GetComponent<MeshFilter>().sharedMesh,Hull=e.GetComponent<MeshCollider>().sharedMesh,Materials=e.GetComponent<MeshRenderer>().sharedMaterials}).ToArray();
  EditorSceneManager.OpenScene("Assets/Scenes/PracticeScene.unity");scene=SceneManager.GetActiveScene();backup="Backups/DogTeeth/PracticeScene.unity";if(!File.Exists(backup))File.Copy(scene.path,backup);
  var session=UnityEngine.Object.FindAnyObjectByType<PracticeSession>();var so=new SerializedObject(session);var slots=(Transform)so.FindProperty("_assemblyRoot").objectReferenceValue;var parts=(Transform)so.FindProperty("_partsRoot").objectReferenceValue;var surface=(Transform)so.FindProperty("_supplySurface").objectReferenceValue;
  var old=so.FindProperty("_pieces");var all=Enumerable.Range(0,old.arraySize).Select(i=>(PracticePiece)old.GetArrayElementAtIndex(i).objectReferenceValue).ToList();
  foreach(var p in all.Where(p=>new[]{"dog_incisors","dog_canines","dog_premolars","dog_molars"}.Contains(p.GetComponent<ElementDog>().Id)).ToArray()){all.Remove(p);UnityEngine.Object.DestroyImmediate(p.Slot.gameObject);UnityEngine.Object.DestroyImmediate(p.gameObject);}
  template=all.First().GetComponent<ReturningGrabbable>();var contourMat=all.First().Slot.GetComponent<MeshRenderer>().sharedMaterial;
  foreach(var tooth in records)
  {
   var target=new GameObject(tooth.Name+" · Target");target.transform.SetParent(slots,false);target.transform.localPosition=tooth.Position;target.transform.localRotation=tooth.Rotation;target.layer=LayerMask.NameToLayer("Ignore Raycast");target.AddComponent<MeshFilter>().sharedMesh=tooth.Mesh;var renderer=target.AddComponent<MeshRenderer>();renderer.sharedMaterials=Enumerable.Repeat(contourMat,tooth.Mesh.subMeshCount).ToArray();renderer.enabled=false;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;var slot=target.AddComponent<PracticeSlot>();slot.Configure(renderer);
   var go=new GameObject(tooth.Name);go.transform.SetParent(parts,false);go.layer=LayerMask.NameToLayer("Grabbable");go.AddComponent<MeshFilter>().sharedMesh=tooth.Mesh;go.AddComponent<MeshRenderer>().sharedMaterials=tooth.Materials;var collider=go.AddComponent<MeshCollider>();collider.sharedMesh=tooth.Hull;collider.convex=true;var body=go.AddComponent<Rigidbody>();body.mass=.015f;body.useGravity=false;body.isKinematic=true;var grab=go.AddComponent<ReturningGrabbable>();EditorUtility.CopySerialized(template,grab);ResetGrab(grab,body);
   var element=go.AddComponent<ElementDog>();var data=new SerializedObject(element);data.FindProperty("_id").stringValue=tooth.Id;data.ApplyModifiedPropertiesWithoutUndo();go.AddComponent<ElementInteractionVisuals>().Configure(tooth.Id,parts,null,parts);go.AddComponent<ElementInfoPresenter>();var piece=go.AddComponent<PracticePiece>();piece.Configure(PracticeSet.Bones,slot);go.AddComponent<PracticePieceLabel>();all.Add(piece);
  }
  old.arraySize=all.Count;for(int i=0;i<all.Count;i++)old.GetArrayElementAtIndex(i).objectReferenceValue=all[i];so.ApplyModifiedPropertiesWithoutUndo();
  var method=typeof(PracticeSession).GetMethod("Layout",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance);object[] args={all,0f};var layout=(System.Collections.IEnumerable)method.Invoke(session,args);float scale=(float)args[1];
  foreach(var item in layout){var type=item.GetType();var piece=(PracticePiece)type.GetField("Piece").GetValue(item);var p=(Vector3)type.GetField("Position").GetValue(item);var rotation=(Quaternion)type.GetField("Rotation").GetValue(item);piece.transform.SetPositionAndRotation(surface.TransformPoint(p),surface.rotation*rotation);piece.transform.localScale=Vector3.one*scale;}
  EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");File.WriteAllText("/tmp/teeth-install.txt","SUCCESS: 36 named teeth, four JSON groups; practice="+all.Count+" parts; label=Мышцы.");
 }
 class ToothRecord{public string Id,Name;public Vector3 Position;public Quaternion Rotation;public Mesh Mesh,Hull;public Material[] Materials;}
 static void ResetGrab(ReturningGrabbable grab,Rigidbody body)
 {
  grab.Rigidbody=body;grab.Grabbed=new HurricaneVR.Framework.Core.VRGrabberEvent();grab.Released=new HurricaneVR.Framework.Core.VRGrabberEvent();grab.HoverEnter=new HurricaneVR.Framework.Core.VRGrabberEvent();grab.HoverExit=new HurricaneVR.Framework.Core.VRGrabberEvent();grab.Activated=new HurricaneVR.Framework.Core.VRGrabberEvent();grab.Deactivated=new HurricaneVR.Framework.Core.VRGrabberEvent();grab.HandGrabbed=new HurricaneVR.Framework.Core.VRHandGrabberEvent();grab.HandReleased=new HurricaneVR.Framework.Core.VRHandGrabberEvent();grab.HandFullReleased=new HurricaneVR.Framework.Core.VRHandGrabberEvent();grab.GrabPoints.Clear();grab.StartingSocket=null;var so=new SerializedObject(grab);so.FindProperty("GrabColliders").arraySize=0;so.FindProperty("CollisionParents").arraySize=0;so.ApplyModifiedPropertiesWithoutUndo();
 }
}
