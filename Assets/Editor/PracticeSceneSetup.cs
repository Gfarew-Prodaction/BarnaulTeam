using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using HurricaneVR.Framework.Core;
using HurricaneVR.Framework.Shared;
using InteracrSystem;

public static class PracticeSceneSetup
{
    public const string ScenePath="Assets/Scenes/PracticeScene.unity";
    private const string Folder="Assets/Practice";
    [MenuItem("Tools/Practice/Create practice scene")]
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new Exception("Exit Play Mode before building the practice scene.");
        if(File.Exists(ScenePath)){File.WriteAllText("/tmp/practice-build.txt","PracticeScene already exists.");return;}
        var current=SceneManager.GetActiveScene();if(current.isDirty)EditorSceneManager.SaveScene(current);
        Directory.CreateDirectory("Backups");if(!File.Exists("Backups/SampleScene-before-practice.unity"))File.Copy("Assets/Scenes/SampleScene.unity","Backups/SampleScene-before-practice.unity");
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var original=GameObject.Find("Dog Head · Layer Model").transform;
        var sources=new List<(ElementDog element,PracticeSet category)>();
        foreach(var group in new[]{("Bones and teeth",PracticeSet.Bones),("Muscles",PracticeSet.Muscles),("Brain",PracticeSet.Brain)})
            foreach(var part in original.Find(group.Item1).GetComponentsInChildren<ElementDog>(true))
                if(part.Id!="dog_teeth"&&part.Id!="dog_teeth_2")sources.Add((part,group.Item2));
        var sessionObject=new GameObject("Practice Session");var session=sessionObject.AddComponent<PracticeSession>();
        var parts=new GameObject("Practice Parts").transform;parts.SetParent(sessionObject.transform,false);
        var assembly=new GameObject("Assembly Targets").transform;assembly.SetParent(sessionObject.transform,false);assembly.position=new Vector3(.45f,1.06f,-1.25f);assembly.localScale=Vector3.one*.8f;
        var ghost=NewMaterial("Practice placement contour",new Color(.15f,.75f,.85f,.12f),true);
        var pieces=new List<PracticePiece>();int index=0;
        foreach(var entry in sources)
        {
            var source=entry.element;var mesh=source.GetComponent<MeshFilter>().sharedMesh;
            var targetObject=new GameObject(source.name+" · Target");targetObject.transform.SetParent(assembly,false);targetObject.transform.localPosition=original.InverseTransformPoint(source.transform.position);targetObject.transform.localRotation=Quaternion.Inverse(original.rotation)*source.transform.rotation;targetObject.layer=LayerMask.NameToLayer("Ignore Raycast");
            targetObject.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=targetObject.AddComponent<MeshRenderer>();renderer.sharedMaterials=Enumerable.Repeat(ghost,mesh.subMeshCount).ToArray();renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;renderer.enabled=false;
            var slot=targetObject.AddComponent<PracticeSlot>();slot.Configure(renderer);
            var go=new GameObject(source.name);go.transform.SetParent(parts,false);go.layer=LayerMask.NameToLayer("Grabbable");
            // Saved supply pose; the selected set is reflowed on this same table when a session starts.
            go.transform.position=new Vector3(2.3f+(index%4)*.23f,.92f,-.8f+(index/4)*.24f);go.transform.localScale=Vector3.one*.8f;index++;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterials=source.GetComponent<MeshRenderer>().sharedMaterials;
            var collider=go.AddComponent<MeshCollider>();collider.sharedMesh=source.GetComponent<MeshCollider>().sharedMesh;collider.convex=true;
            var body=go.AddComponent<Rigidbody>();body.mass=.15f;body.useGravity=false;body.isKinematic=true;body.interpolation=RigidbodyInterpolation.Interpolate;
            var grab=go.AddComponent<ReturningGrabbable>();EditorUtility.CopySerialized(source.GetComponent<ReturningGrabbable>(),grab);grab.Rigidbody=body;
            grab.Grabbed=new VRGrabberEvent();grab.Released=new VRGrabberEvent();grab.HoverEnter=new VRGrabberEvent();grab.HoverExit=new VRGrabberEvent();grab.Activated=new VRGrabberEvent();grab.Deactivated=new VRGrabberEvent();
            grab.HandGrabbed=new VRHandGrabberEvent();grab.HandReleased=new VRHandGrabberEvent();grab.HandFullReleased=new VRHandGrabberEvent();grab.GrabPoints.Clear();grab.StartingSocket=null;
            var gs=new SerializedObject(grab);gs.FindProperty("GrabColliders").arraySize=0;gs.FindProperty("CollisionParents").arraySize=0;gs.ApplyModifiedPropertiesWithoutUndo();
            var element=go.AddComponent<ElementDog>();var data=new SerializedObject(element);data.FindProperty("_id").stringValue=source.Id;data.ApplyModifiedPropertiesWithoutUndo();
            var practice=go.AddComponent<PracticePiece>();practice.Configure(entry.category,slot);go.AddComponent<PracticePieceLabel>();
            go.AddComponent<ElementInteractionVisuals>().Configure(source.Id,sessionObject.transform,null,null);
            pieces.Add(practice);
        }
        var keep=new HashSet<string>{"AGAU · Teaching Room","AGAU · Interior Lighting","Global Volume","HVRGlobal","TechDemoXRRigOpenXR","EventSystem","Practice Session"};
        foreach(var root in scene.GetRootGameObjects())if(!keep.Contains(root.name))UnityEngine.Object.DestroyImmediate(root);
        var top=NewMaterial("Practice table top",new Color(.055f,.14f,.145f));var frame=NewMaterial("Practice table frame",new Color(.65f,.7f,.68f));var dark=NewMaterial("Practice table legs",new Color(.07f,.085f,.09f));
        var assemblyTable=Table("Assembly Table",new Vector3(.4f,0,-1.25f),new Vector2(2.15f,.95f),0,top,frame,dark);
        var supplyTable=Table("Supply Table",new Vector3(2.75f,0,.25f),new Vector2(2.4f,1.15f),90,top,frame,dark);
        var surface=new GameObject("Supply surface").transform;surface.SetParent(supplyTable,false);surface.localPosition=new Vector3(0,.78f,0);
        Cube("Assembly base",assemblyTable,new Vector3(.05f,.798f,0),new Vector3(.68f,.035f,.48f),dark);
        var control=assembly.gameObject.AddComponent<LayerModelController>();control.Configure(assembly,assemblyTable);
        session.Configure(pieces.ToArray(),parts,assembly,assemblyTable,surface,control);
        var rig=GameObject.Find("TechDemoXRRigOpenXR");rig.transform.SetPositionAndRotation(new Vector3(1.35f,.02f,0),Quaternion.Euler(0,217,0));
        Lightmapping.lightingDataAsset=null;LightmapSettings.lightmaps=Array.Empty<LightmapData>();
        EditorSceneManager.SaveScene(scene,ScenePath,false);AssetDatabase.SaveAssets();
        var scenes=EditorBuildSettings.scenes.ToList();if(!scenes.Any(s=>s.path==ScenePath))scenes.Add(new EditorBuildSettingsScene(ScenePath,true));EditorBuildSettings.scenes=scenes.ToArray();
        File.WriteAllText("/tmp/practice-build.txt","Saved "+pieces.Count+" persisted pieces: "+string.Join(", ",pieces.GroupBy(p=>p.Category).Select(g=>g.Key+"="+g.Count()))+"; room and two tables; added to Build Settings.");
    }
    private static Transform Table(string name,Vector3 position,Vector2 size,float yaw,Material top,Material frame,Material dark)
    {
        var table=new GameObject(name).transform;table.position=position;table.rotation=Quaternion.Euler(0,yaw,0);
        Cube("Table top",table,new Vector3(0,.74f,0),new Vector3(size.x,.08f,size.y),top);
        Cube("Underframe",table,new Vector3(0,.66f,0),new Vector3(size.x-.12f,.08f,size.y-.12f),frame);
        foreach(float x in new[]{-1f,1f})foreach(float z in new[]{-1f,1f})Cube("Leg",table,new Vector3(x*(size.x*.5f-.14f),.32f,z*(size.y*.5f-.14f)),new Vector3(.065f,.64f,.065f),dark);
        return table;
    }
    private static void Cube(string name,Transform parent,Vector3 position,Vector3 scale,Material material)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;var renderer=go.GetComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.receiveGI=ReceiveGI.Lightmaps;
        GameObjectUtility.SetStaticEditorFlags(go,StaticEditorFlags.ContributeGI|StaticEditorFlags.BatchingStatic|StaticEditorFlags.ReflectionProbeStatic);
    }
    private static Material NewMaterial(string name,Color color,bool transparent=false)
    {
        string path=Folder+"/Materials/"+name.Replace(' ','_')+".mat";var existing=AssetDatabase.LoadAssetAtPath<Material>(path);if(existing)return existing;
        var mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.name=name;mat.color=color;mat.SetFloat("_Smoothness",.22f);
        if(transparent){mat.SetFloat("_Surface",1);mat.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);mat.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);mat.SetFloat("_ZWrite",0);mat.SetFloat("_Cull",0);mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.SetOverrideTag("RenderType","Transparent");mat.renderQueue=3000;}
        AssetDatabase.CreateAsset(mat,path);return mat;
    }
    [MenuItem("Tools/Practice/Bake practice lighting")]
    public static void Bake()
    {
        if(SceneManager.GetActiveScene().path!=ScenePath)throw new Exception("Open PracticeScene first.");
        Lightmapping.bakeCompleted-=Finished;Lightmapping.bakeCompleted+=Finished;
        File.WriteAllText("/tmp/practice-bake.txt","Baking...");if(!Lightmapping.BakeAsync())throw new Exception("Practice bake did not start");
    }
    private static void Finished()
    {
        Lightmapping.bakeCompleted-=Finished;
        var probe=UnityEngine.Object.FindAnyObjectByType<ReflectionProbe>();bool reflection=probe&&Lightmapping.BakeReflectionProbe(probe,Folder+"/Lighting/PracticeReflection.exr");
        EditorSceneManager.SaveScene(SceneManager.GetActiveScene());AssetDatabase.SaveAssets();File.WriteAllText("/tmp/practice-bake.txt","SUCCESS maps="+LightmapSettings.lightmaps.Length+" probes="+(LightmapSettings.lightProbes?LightmapSettings.lightProbes.count:0)+" reflection="+reflection);
    }
}
