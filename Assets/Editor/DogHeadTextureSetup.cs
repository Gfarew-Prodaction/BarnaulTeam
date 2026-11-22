using System;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

public static class DogHeadTextureSetup
{
 const string Folder="Assets/Models/DogHead/Textures/";
 const string Materials="Assets/Models/DogHead/V2/Materials/";
 [MenuItem("Tools/Dog Head/Apply supplied textures")]
 public static void Apply()
 {
  if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode before assigning persistent materials.");
  var report=new StringBuilder();int assigned=0;
  foreach(var kind in new[]{"head","body","hair","mouth"}) {
   string path=Folder+"Dog-"+kind+"-BaseColor.png";
   var importer=(TextureImporter)AssetImporter.GetAtPath(path);
   if(!importer)throw new Exception("Texture not imported: "+path);
   importer.textureType=TextureImporterType.Default;importer.sRGBTexture=true;importer.alphaSource=TextureImporterAlphaSource.FromInput;
   importer.alphaIsTransparency=kind=="hair";importer.mipmapEnabled=true;importer.maxTextureSize=1024;importer.textureCompression=TextureImporterCompression.Compressed;importer.SaveAndReimport();
  }
  foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{Materials.TrimEnd('/')})) {
   var path=AssetDatabase.GUIDToAssetPath(guid);var mat=AssetDatabase.LoadAssetAtPath<Material>(path);string name=mat.name;
   if(!new[]{"dog_head_skin","dog_head_fur","dog_skin_inner","dog_left_eye","dog_right_eye"}.Any(name.StartsWith))continue;
   string kind=name.Contains("hair")?"hair":name.Contains("mouth")?"mouth":name.Contains("body")?"body":"head";
   var texture=AssetDatabase.LoadAssetAtPath<Texture2D>(Folder+"Dog-"+kind+"-BaseColor.png");
   mat.SetTexture("_BaseMap",texture);mat.SetTexture("_MainTex",texture);mat.SetColor("_BaseColor",Color.white);mat.SetColor("_Color",Color.white);mat.SetFloat("_Metallic",0);mat.SetFloat("_Smoothness",kind=="hair"?.12f:.25f);
   if(kind=="hair") {
    mat.SetFloat("_Surface",0);mat.SetFloat("_AlphaClip",1);mat.SetFloat("_Cutoff",.35f);mat.SetFloat("_Cull",0);mat.SetFloat("_ZWrite",1);
    mat.SetFloat("_SrcBlend",(float)BlendMode.One);mat.SetFloat("_DstBlend",(float)BlendMode.Zero);mat.SetFloat("_AlphaToMask",1);
    mat.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");mat.EnableKeyword("_ALPHATEST_ON");mat.SetOverrideTag("RenderType","TransparentCutout");mat.renderQueue=(int)RenderQueue.AlphaTest;mat.doubleSidedGI=true;
   }
   EditorUtility.SetDirty(mat);assigned++;report.AppendLine(name+" -> "+texture.name);
  }
  AssetDatabase.SaveAssets();
  // Remap both the anatomy source and the user's separately imported Skin FBX.
  foreach(var modelPath in new[]{"Assets/Models/DogHead/Skin.fbx","Assets/Scripts/Skin.fbx"}) {
   var importer=AssetImporter.GetAtPath(modelPath) as ModelImporter;if(!importer)continue;
   foreach(var source in AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Material>().ToArray()) {
    string target=source.name.Contains("hair")?"dog_head_fur_mtl_wolf_a_hair_a_body_001":source.name.Contains("mouth")?"dog_skin_inner_mtl_ger_shepherd_mouth_org1":source.name.Contains("body")?"dog_head_skin_mtl_ger_shepherd_body_org1":source.name.Contains("head")?"dog_head_skin_mtl_ger_shepherd_head_org1":"dog_left_eye_Material";
    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),source.name),AssetDatabase.LoadAssetAtPath<Material>(Materials+target+".mat"));
   }
   importer.SaveAndReimport();
  }
  var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
  var root=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="Dog Head · Layer Model");
  if(root) {
   var fur=root.transform.Find("Skin/ger_shepherd_LOD0.003");if(!fur)throw new Exception("Fur object not found");fur.gameObject.SetActive(true);
   Directory.CreateDirectory("Backups");if(!File.Exists("Backups/SampleScene-before-textures.unity"))File.Copy(scene.path,"Backups/SampleScene-before-textures.unity");
   EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
   report.AppendLine("Fur enabled; scene saved with existing editor changes preserved.");
  }
  report.AppendLine("Assigned "+assigned+" materials.");File.WriteAllText("/tmp/dog-textures-result.txt",report.ToString());
 }
}
