using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine.UI;
using UnityEngine.Rendering;
using InteracrSystem;
using TMPro;

public static class DogHeadUpgrade
{
    const string Folder="Assets/Models/DogHead/V2";
    [Serializable] class Part { public string source,id; public bool muscle; }
    [Serializable] class Parts { public Part[] parts; }
    static Transform root; static ReturningGrabbable template; static Material ghostMat;
    static readonly Dictionary<string,string> BrainIds = new Dictionary<string,string> {
        {"Bridge","dog_pons"},{"cerebellum","dog_cerebellum"},{"intermediate brain","dog_diencephalon"},
        {"medulla oblongata","dog_medulla"},{"midbrain","dog_midbrain"},{"olfactory bulbs","dog_olfactory_bulbs"},
        {"Retopo_the left hemisphere","dog_left_hemisphere"},{"Retopo_the right hemisphere","dog_right_hemisphere"}
    };
    [MenuItem("Tools/Dog Head/Upgrade complete anatomy")]
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/SampleScene.unity");
        var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        Directory.CreateDirectory("Backups");if(!File.Exists("Backups/SampleScene-before-head-v2.unity"))File.Copy(scene.path,"Backups/SampleScene-before-head-v2.unity");
        var model=GameObject.Find("Dog Head · Layer Model");root=model.transform;
        if(root.Find("Skin")) { File.WriteAllText("/tmp/dog-v2-setup.txt","Already upgraded");return; }
        Directory.CreateDirectory(Folder+"/Parts");Directory.CreateDirectory(Folder+"/Materials");AssetDatabase.Refresh();
        template=UnityEngine.Object.FindObjectsByType<ReturningGrabbable>(FindObjectsInactive.Include,FindObjectsSortMode.None).First();
        var oldSource=UnityEngine.Object.Instantiate(Load("skull_muscles_textured228.fbx"));
        var oldRs=oldSource.GetComponentsInChildren<Renderer>();var bounds=oldRs[0].bounds;foreach(var r in oldRs)bounds.Encapsulate(r.bounds);
        float factor=.65f/Mathf.Max(bounds.size.x,bounds.size.y,bounds.size.z);
        UnityEngine.Object.DestroyImmediate(oldSource);
        foreach(Transform child in root.Cast<Transform>().ToArray())UnityEngine.Object.DestroyImmediate(child.gameObject);
        var bones=Group("Bones and teeth");var muscles=Group("Muscles");var skin=Group("Skin");var brain=Group("Brain");
        ghostMat=Material("Home silhouette",new Color(.12f,.8f,.65f,.2f),true);
        var parts=JsonUtility.FromJson<Parts>(File.ReadAllText("Assets/Models/DogHead/parts.json")).parts;
        var skull=UnityEngine.Object.Instantiate(Load("skull_muscles_textured123.fbx"));
        var records=new List<string>();
        foreach(var r in skull.GetComponentsInChildren<MeshRenderer>())
        {
            string name=Regex.Replace(r.name,@"\.\d+$","");if(name.StartsWith("Retopo_"))name=name.Substring(7);
            var entry=parts.FirstOrDefault(p=>p.source.Equals(name,StringComparison.OrdinalIgnoreCase));
            if(entry==null)throw new Exception("Unmapped mesh "+r.name);
            var group=entry.muscle?muscles:bones;
            string related=entry.id=="dog_levator_nasolabialis_cut"?"dog_levator_nasolabialis":entry.id;
            Create(r,group,entry.id,related,v=>(v-bounds.center)*factor,entry.muscle?new Color(.52f,.14f,.12f):new Color(.87f,.81f,.67f));
            records.Add(r.name+" | "+entry.id+" | "+related);
        }
        UnityEngine.Object.DestroyImmediate(skull);
        var brainSource=UnityEngine.Object.Instantiate(Load("Brain228.fbx"));var br=brainSource.GetComponentsInChildren<MeshRenderer>();
        var bb=br[0].bounds;foreach(var r in br)bb.Encapsulate(r.bounds);
        Vector3 brainCenter=(new Vector3(-26,40,-7.8f)-bounds.center)*factor;
        foreach(var r in br)
        {
            string name=Regex.Replace(r.name,@"\.\d+$","");string id=BrainIds[name];
            string related=id.Contains("hemisphere")?"dog_hemispheres":id;
            Create(r,brain,id,related,v=>brainCenter+(v-bb.center)*.07f,new Color(.77f,.51f,.48f));records.Add(r.name+" | "+id+" | "+related);
        }
        UnityEngine.Object.DestroyImmediate(brainSource);
        var skinSource=UnityEngine.Object.Instantiate(Load("Skin.fbx"));var sr=skinSource.GetComponentsInChildren<MeshRenderer>();
        var main=sr.Single(r=>r.name=="ger_shepherd_LOD0.001");
        Vector3 skinCenter=(new Vector3(-26f,35,-7.8f)-bounds.center)*factor;
        foreach(var r in sr)
        {
            bool eye=r.name.EndsWith(".002")||r.name.EndsWith(".005");
            string id=eye?(r.name.EndsWith(".002")?"dog_right_eye":"dog_left_eye"):
                r.name.EndsWith(".003")?"dog_head_fur":r.name.EndsWith(".115")?"dog_skin_inner":"dog_head_skin";
            Vector3 center=skinCenter;
            Func<Vector3,Vector3> convert=v=>center+(v-main.bounds.center)*factor*.7f;
            { // Keep eyes and skin in the same source coordinate frame.
                var originalConvert=convert;
                float eyeY=(new Vector3(-33.4f,40.4f,-3.1f)-bounds.center).y*factor;
                convert=v=>{var p=originalConvert(v);return new Vector3(p.x-.11f,eyeY+(p.y-eyeY)*1.18f,p.z*1.08f);};
            }
            Create(r,skin,id,eye?"dog_eyes":id,convert,eye?new Color(.82f,.79f,.67f):new Color(.34f,.23f,.15f));
            if(eye){var socketCenter=new Vector3(-.04f,.145f,id=="dog_left_eye"?-.0816f:.0609f);skin.Find(r.name).localPosition=socketCenter;skin.Find(r.name+" · Home silhouette").localPosition=socketCenter;}
            if(id=="dog_head_fur") skin.Find(r.name).gameObject.SetActive(File.Exists("Assets/Models/DogHead/Textures/Dog-hair-BaseColor.png"));
            records.Add(r.name+" | "+id+" | "+(eye?"dog_eyes":id));
        }
        UnityEngine.Object.DestroyImmediate(skinSource);
        // Apply the same hover/ghost behavior to the two original training elements.
        foreach(var item in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ElementDog>(true)).ToArray())
        {
            if(item.transform.IsChildOf(root) || item.GetComponent<ElementInteractionVisuals>())continue;
            var mf=item.GetComponent<MeshFilter>();if(!mf)continue;
            var ghost=new GameObject(item.name+" · Home silhouette");ghost.transform.SetPositionAndRotation(item.transform.position,item.transform.rotation);ghost.transform.localScale=item.transform.lossyScale;
            ghost.AddComponent<MeshFilter>().sharedMesh=mf.sharedMesh;ghost.AddComponent<MeshRenderer>().sharedMaterial=ghostMat;ghost.SetActive(false);
            item.gameObject.AddComponent<ElementInteractionVisuals>().Configure(item.Id,null,ghost,null);
        }
        var choice=model.GetComponent<ChoiceLayer>();choice.ConfigureExtended(bones.gameObject,muscles.gameObject,skin.gameObject,brain.gameObject);choice.ShowSkin();
        var panel=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Canvas>(true)).Single(c=>c.transform.parent&&c.transform.parent.name=="Cube");
        var buttons=panel.GetComponentsInChildren<Button>(true);
        var actions=new UnityEngine.Events.UnityAction[]{choice.ShowSkin,choice.ShowMuscles,choice.ShowBones,choice.ShowBrain};
        var titles=new[]{"Кожа","Мышцы","Кости","Мозг"};
        for(int i=0;i<4;i++) {var b=buttons[i];b.gameObject.SetActive(true);b.onClick=new Button.ButtonClickedEvent();UnityEventTools.AddPersistentListener(b.onClick,actions[i]);var label=b.GetComponentInChildren<TMP_Text>(true);label.text=titles[i];label.enableAutoSizing=true;label.fontSizeMin=8;label.fontSizeMax=30;label.textWrappingMode=TextWrappingModes.NoWrap;}
        foreach(var label in panel.GetComponentsInChildren<TMP_Text>(true)) if(!label.GetComponentInParent<Button>()) {label.text="Слои головы\nY — управление · X/A — сброс";label.enableAutoSizing=true;label.fontSizeMax=26;}
        EditorUtility.SetDirty(choice);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        if(File.Exists("Assets/Models/DogHead/Textures/Dog-hair-BaseColor.png")) DogHeadTextureSetup.Apply();
        File.WriteAllLines("/tmp/dog-v2-parts.txt",records);File.WriteAllText("/tmp/dog-v2-setup.txt","Saved "+records.Count+" anatomy parts");
    }
    static GameObject Load(string name)=>AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Models/DogHead/"+name);
    static Transform Group(string name) {var go=new GameObject(name);go.transform.SetParent(root,false);return go.transform;}
    static string Safe(string s)=>Regex.Replace(s,@"[^a-zA-Z0-9_-]","_");
    static Material Material(string name,Color color,bool transparent=false)
    {
        string path=Folder+"/Materials/"+Safe(name)+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(mat)return mat;
        mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));mat.name=name;mat.color=color;mat.SetFloat("_Smoothness",.25f);
        if(transparent) {mat.SetFloat("_Surface",1);mat.SetFloat("_SrcBlend",(float)BlendMode.SrcAlpha);mat.SetFloat("_DstBlend",(float)BlendMode.OneMinusSrcAlpha);mat.SetFloat("_ZWrite",0);mat.SetFloat("_Cull",0);mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.SetOverrideTag("RenderType","Transparent");mat.renderQueue=3000;}
        AssetDatabase.CreateAsset(mat,path);return mat;
    }
    static void Create(MeshRenderer source,Transform parent,string id,string related,Func<Vector3,Vector3> convert,Color color)
    {
        string key=Safe(source.name);string assetPath=Folder+"/Parts/"+key+".asset";
        var original=source.GetComponent<MeshFilter>().sharedMesh;
        var mesh=UnityEngine.Object.Instantiate(original);mesh.name=key;
        var vertices=original.vertices.Select(v=>convert(source.transform.TransformPoint(v))).ToArray();var b=new Bounds(vertices[0],Vector3.zero);foreach(var v in vertices)b.Encapsulate(v);
        mesh.vertices=vertices.Select(v=>v-b.center).ToArray();
        if(original.normals.Length==original.vertexCount)mesh.normals=original.normals.Select(n=>source.transform.localToWorldMatrix.inverse.transpose.MultiplyVector(n).normalized).ToArray();
        if(source.transform.localToWorldMatrix.determinant<0)for(int sub=0;sub<mesh.subMeshCount;sub++){var tris=mesh.GetTriangles(sub);for(int i=0;i<tris.Length;i+=3){int v=tris[i];tris[i]=tris[i+2];tris[i+2]=v;}mesh.SetTriangles(tris,sub);}
        mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.RecalculateTangents();AssetDatabase.CreateAsset(mesh,assetPath);
        var go=new GameObject(source.name);go.transform.SetParent(parent,false);go.transform.localPosition=b.center;go.layer=LayerMask.NameToLayer("Grabbable");
        go.AddComponent<MeshFilter>().sharedMesh=mesh;
        var mats=new List<Material>();foreach(var sm in source.sharedMaterials) {var m=Material(id+" "+(sm?sm.name:"default"),color);if(sm&&sm.mainTexture){m.SetTexture("_BaseMap",sm.mainTexture);m.color=Color.white;EditorUtility.SetDirty(m);}mats.Add(m);}
        go.AddComponent<MeshRenderer>().sharedMaterials=mats.ToArray();
        var col=go.AddComponent<MeshCollider>();col.sharedMesh=Hull(mesh,key);col.convex=true;
        var rb=go.AddComponent<Rigidbody>();rb.mass=.15f;rb.useGravity=false;rb.interpolation=RigidbodyInterpolation.Interpolate;
        var grab=go.AddComponent<ReturningGrabbable>();EditorUtility.CopySerialized(template,grab);grab.Rigidbody=rb;
        var gs=new SerializedObject(grab);gs.FindProperty("GrabColliders").arraySize=0;gs.FindProperty("CollisionParents").arraySize=0;gs.ApplyModifiedPropertiesWithoutUndo();
        var data=go.AddComponent<ElementDog>();var so=new SerializedObject(data);so.FindProperty("_id").stringValue=id;so.ApplyModifiedPropertiesWithoutUndo();
        go.AddComponent<ElementInfoPresenter>();go.AddComponent<OnMoveBackItem>().SetReturnFrame(parent);
        var ghost=new GameObject(source.name+" · Home silhouette");ghost.layer=LayerMask.NameToLayer("Ignore Raycast");ghost.transform.SetParent(parent,false);ghost.transform.localPosition=b.center;
        ghost.AddComponent<MeshFilter>().sharedMesh=mesh;var gr=ghost.AddComponent<MeshRenderer>();gr.sharedMaterials=Enumerable.Repeat(ghostMat,mesh.subMeshCount).ToArray();gr.shadowCastingMode=ShadowCastingMode.Off;gr.receiveShadows=false;ghost.SetActive(false);
        go.AddComponent<ElementInteractionVisuals>().Configure(related,root,ghost,parent);
    }
    static Mesh Hull(Mesh mesh,string name)
    {
        var vertices=mesh.vertices;var points=new List<Vector3>();
        for(int i=0;i<96;i++){float y=1-2*(i+.5f)/96;float r=Mathf.Sqrt(1-y*y),a=i*2.399963f;var dir=new Vector3(Mathf.Cos(a)*r,y,Mathf.Sin(a)*r);float max=float.NegativeInfinity;var best=vertices[0];foreach(var v in vertices){float d=Vector3.Dot(v,dir);if(d>max){max=d;best=v;}}if(!points.Contains(best))points.Add(best);}
        var hull=new Mesh();hull.name=name+" collider";hull.vertices=points.ToArray();var tris=new int[(points.Count-2)*3];for(int i=0;i<points.Count-2;i++){tris[i*3]=0;tris[i*3+1]=i+1;tris[i*3+2]=i+2;}hull.triangles=tris;hull.RecalculateBounds();AssetDatabase.CreateAsset(hull,Folder+"/Parts/"+name+"_collider.asset");return hull;
    }
}
