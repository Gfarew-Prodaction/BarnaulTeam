using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using InteracrSystem;
using TMPro;

public static class DogHeadSceneSetup
{
    [Serializable] class Part { public string source, id; public bool muscle; }
    [Serializable] class Parts { public Part[] parts; }
    [MenuItem("Tools/Dog Head/Install scene model")]
    public static void Build()
    {
        if (GameObject.Find("Dog Head · Layer Model")) {
            var existing=GameObject.Find("Dog Head · Layer Model");
            if(existing.transform.Find("Skin")) return;
            Finish(existing,existing.transform.Find("Bones and teeth").gameObject,existing.transform.Find("Muscles").gameObject); return;
        }
        var asset=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/DogHead/skull_muscles_textured228.fbx");
        if (!asset) throw new Exception("FBX not imported");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        EditorSceneManager.SaveScene(scene);
        Directory.CreateDirectory("Backups"); File.Copy(scene.path,"Backups/SampleScene-before-dog-head.unity",true);
        Directory.CreateDirectory("Assets/Models/DogHead/Parts"); Directory.CreateDirectory("Assets/Models/DogHead/Materials"); AssetDatabase.Refresh();
        var source=UnityEngine.Object.Instantiate(asset);
        var renderers=source.GetComponentsInChildren<MeshRenderer>();
        Bounds bounds=renderers[0].bounds; foreach(var r in renderers) bounds.Encapsulate(r.bounds);
        float scale=.65f/Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
        var root=new GameObject("Dog Head · Layer Model"); Undo.RegisterCreatedObjectUndo(root,"Install dog head");
        root.transform.position=new Vector3(.52f,.69f+bounds.size.y*scale*.5f,-1.35f);
        var bones=new GameObject("Bones and teeth"); bones.transform.SetParent(root.transform,false);
        var muscles=new GameObject("Muscles"); muscles.transform.SetParent(root.transform,false);
        var mapping=JsonUtility.FromJson<Parts>(File.ReadAllText("Assets/Models/DogHead/parts.json"));
        var template=UnityEngine.Object.FindObjectsByType<ReturningGrabbable>(FindObjectsInactive.Include,FindObjectsSortMode.None).First();
        int count=0;
        foreach(var entry in mapping.parts)
        {
            var t=source.GetComponentsInChildren<Transform>().Single(x=>x.name==entry.source);
            var renderer=t.GetComponent<MeshRenderer>(); var original=t.GetComponent<MeshFilter>().sharedMesh;
            var mesh=UnityEngine.Object.Instantiate(original); mesh.name=entry.source;
            Vector3 partCenter=renderer.bounds.center;
            mesh.vertices=original.vertices.Select(v=>(t.TransformPoint(v)-partCenter)*scale).ToArray();
            if (original.normals.Length==original.vertexCount) mesh.normals=original.normals.Select(n=>t.localToWorldMatrix.inverse.transpose.MultiplyVector(n).normalized).ToArray();
            if(t.localToWorldMatrix.determinant<0) for(int sub=0;sub<mesh.subMeshCount;sub++) { var tris=mesh.GetTriangles(sub); for(int i=0;i<tris.Length;i+=3) { int tmp=tris[i];tris[i]=tris[i+2];tris[i+2]=tmp; } mesh.SetTriangles(tris,sub); }
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            AssetDatabase.CreateAsset(mesh,"Assets/Models/DogHead/Parts/"+entry.id+".asset");
            var go=new GameObject(entry.source); go.layer=LayerMask.NameToLayer("Grabbable");
            go.transform.SetParent(entry.muscle?muscles.transform:bones.transform,false);
            go.transform.localPosition=(partCenter-bounds.center)*scale;
            go.AddComponent<MeshFilter>().sharedMesh=mesh;
            var output=go.AddComponent<MeshRenderer>();
            output.sharedMaterials=renderer.sharedMaterials.Select(m=>ConvertMaterial(m,entry.muscle)).ToArray();
            var collider=go.AddComponent<MeshCollider>(); collider.sharedMesh=mesh;collider.convex=true;
            var body=go.AddComponent<Rigidbody>(); body.mass=.15f;body.useGravity=false;body.interpolation=RigidbodyInterpolation.Interpolate;
            var grab=go.AddComponent<ReturningGrabbable>(); EditorUtility.CopySerialized(template,grab);grab.Rigidbody=body;
            // Clear copied scene references; each part discovers its own collider.
            var grabSO=new SerializedObject(grab);grabSO.FindProperty("GrabColliders").arraySize=0;grabSO.FindProperty("CollisionParents").arraySize=0;grabSO.ApplyModifiedPropertiesWithoutUndo();
            var data=go.AddComponent<ElementDog>(); var so=new SerializedObject(data);so.FindProperty("_id").stringValue=entry.id;so.ApplyModifiedPropertiesWithoutUndo();
            go.AddComponent<ElementInfoPresenter>();
            go.AddComponent<OnMoveBackItem>().SetReturnFrame(go.transform.parent);
            count++;
        }
        UnityEngine.Object.DestroyImmediate(source);
        Finish(root,bones,muscles);
    }
    static void Finish(GameObject root,GameObject bones,GameObject muscles)
    {
        foreach(var col in root.GetComponentsInChildren<MeshCollider>(true))
        {
            var original=col.GetComponent<MeshFilter>().sharedMesh;
            string path="Assets/Models/DogHead/Parts/"+original.name+"_collider.asset";
            var hull=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(!hull)
            {
                var vertices=original.vertices;
                var points=new System.Collections.Generic.List<Vector3>();
                for(int i=0;i<96;i++)
                {
                    float y=1f-2f*(i+.5f)/96f;float r=Mathf.Sqrt(1-y*y);float angle=i*2.399963f;
                    var direction=new Vector3(Mathf.Cos(angle)*r,y,Mathf.Sin(angle)*r);
                    var best=vertices[0];float max=float.NegativeInfinity;
                    foreach(var v in vertices) { float dot=Vector3.Dot(v,direction);if(dot>max) {max=dot;best=v;} }
                    if(!points.Contains(best)) points.Add(best);
                }
                hull=new Mesh();hull.name=original.name+" collider";hull.vertices=points.ToArray();
                var tris=new int[(points.Count-2)*3];for(int i=0;i<points.Count-2;i++) {tris[i*3]=0;tris[i*3+1]=i+1;tris[i*3+2]=i+2;} hull.triangles=tris;hull.RecalculateBounds();
                AssetDatabase.CreateAsset(hull,path);
            }
            col.sharedMesh=hull;col.convex=true;
        }
        var chooser=root.GetComponent<ChoiceLayer>() ?? root.AddComponent<ChoiceLayer>(); chooser.Configure(bones,muscles);
        var table=GameObject.Find("VetTable");
        var controller=root.GetComponent<LayerModelController>() ?? root.AddComponent<LayerModelController>();controller.Configure(root.transform,table.transform);
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        var panel=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Canvas>(true)).Single(c=>c.transform.parent && c.transform.parent.name=="Cube");
        controller.SetPanel(panel);
        var buttons=panel.GetComponentsInChildren<Button>(true);
        for(int i=0;i<buttons.Length;i++)
        {
            var button=buttons[i]; if(i>1) { button.gameObject.SetActive(false);continue; }
            button.onClick=new Button.ButtonClickedEvent();
            if(i==0) UnityEventTools.AddPersistentListener(button.onClick,chooser.ShowBones);
            else UnityEventTools.AddPersistentListener(button.onClick,chooser.ShowMuscles);
            var text=button.GetComponentInChildren<TMP_Text>(true);
            text.text=i==0?"Кости":"Мышцы";text.enableAutoSizing=true;text.fontSizeMin=8;text.fontSizeMax=30;text.textWrappingMode=TextWrappingModes.NoWrap;
        }
        foreach(var label in panel.GetComponentsInChildren<TMP_Text>(true))
            if(!label.GetComponentInParent<Button>()) label.text="Слои головы\nY — управление моделью";
        EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        File.WriteAllText("/tmp/dog-head-setup.done","Installed "+root.GetComponentsInChildren<ElementDog>(true).Length+" parts");
    }
    static Material ConvertMaterial(Material source,bool muscle)
    {
        string name=source ? source.name.Replace('/','_').Replace('\\','_') : (muscle?"Muscle":"Bone");
        string path="Assets/Models/DogHead/Materials/"+name+".mat";
        var mat=AssetDatabase.LoadAssetAtPath<Material>(path); if(mat) return mat;
        mat=new Material(Shader.Find("Universal Render Pipeline/Lit")); mat.name=name;
        mat.color=muscle?new Color(.52f,.14f,.12f):new Color(.87f,.81f,.67f);
        if(source && source.mainTexture) mat.SetTexture("_BaseMap",source.mainTexture);
        mat.SetFloat("_Smoothness",.25f);AssetDatabase.CreateAsset(mat,path);return mat;
    }
}
