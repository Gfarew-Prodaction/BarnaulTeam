using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

// A physical, scene-authored screen. Only its content changes between modes/pages.
public class MedicalTVScreen : MonoBehaviour
{
    [SerializeField] private Transform _screen;
    [SerializeField] private Vector2 _size;
    private readonly List<Canvas> _pages=new List<Canvas>();
    public Transform Screen => _screen;
    public Vector2 ScreenSize => _size;
    public Canvas CurrentPage => _pages.Count>0?_pages[_pages.Count-1]:null;
    private void Start(){var module=InspectionVRInput.EnsureModule();module.MaxDistance=Mathf.Max(5,module.MaxDistance);}
    public void Configure(Transform screen,Vector2 size){_screen=screen;_size=size;}
    public static bool Present(Canvas page)
    {
        var tv=FindAnyObjectByType<MedicalTVScreen>();if(!tv||!tv._screen||!page)return false;
        tv._pages.RemoveAll(p=>!p);
        if(tv.CurrentPage&&tv.CurrentPage!=page)tv.CurrentPage.gameObject.SetActive(false);
        tv._pages.Remove(page);tv._pages.Add(page);
        page.transform.SetParent(tv._screen,false);page.transform.localPosition=Vector3.zero;page.transform.localRotation=Quaternion.identity;
        ((RectTransform)page.transform).sizeDelta=new Vector2(1000,562);
        float scale=Mathf.Min(tv._size.x/1000,tv._size.y/562);
        var parentScale=tv._screen.lossyScale;
        page.transform.localScale=new Vector3(scale/parentScale.x,scale/parentScale.y,scale/parentScale.z);
        page.gameObject.SetActive(true);Style(page);InspectionVRInput.RegisterCanvas(page);return true;
    }
    public static void Dismiss(Canvas page)
    {
        var tv=page?page.GetComponentInParent<MedicalTVScreen>():null;if(!tv)return;
        bool top=tv.CurrentPage==page;tv._pages.Remove(page);tv._pages.RemoveAll(p=>!p);
        if(page)page.gameObject.SetActive(false);
        if(top&&tv.CurrentPage)tv.CurrentPage.gameObject.SetActive(true);
    }
    public static bool IsNearScreen()
    {
        var tv=FindAnyObjectByType<MedicalTVScreen>();var camera=Camera.main;
        return tv&&tv._screen&&camera&&Vector3.Distance(camera.transform.position,tv._screen.position)<4.5f;
    }
    private static void Style(Canvas page)
    {
        MedicalUITheme.Apply(page);
        if(page.transform.Find("TV · Edge"))return;
        InspectionUI.Image(page.transform,"TV · Edge",new Vector2(-491,0),new Vector2(6,514),MedicalUITheme.Teal);
        // Small medical mark sits in the safe corner, outside text and controller targets.
        InspectionUI.Image(page.transform,"TV · Cross horizontal",new Vector2(470,248),new Vector2(20,5),MedicalUITheme.Teal);
        InspectionUI.Image(page.transform,"TV · Cross vertical",new Vector2(470,248),new Vector2(5,20),MedicalUITheme.Teal);
    }
    public static Text Label(Transform parent,string value,Vector2 position,Vector2 size,int fontSize,Color color,bool bold=false)
    {
        var text=InspectionUI.Text(parent,position,size,fontSize,color);text.text=value;
        text.fontStyle=bold?FontStyle.Bold:FontStyle.Normal;return text;
    }
    public static void Header(Canvas page,string section)
    {
        Label(page.transform,section,new Vector2(-20,239),new Vector2(860,30),18,MedicalUITheme.Teal,true);
        InspectionUI.Image(page.transform,"TV · Header rule",new Vector2(0,210),new Vector2(900,2),new Color(.79f,.85f,.84f));
    }
    public static void Card(Transform parent,string name,Vector2 position,Vector2 size)
    {
        var image=InspectionUI.Image(parent,name,position,size,new Color(.89f,.93f,.92f));
        MedicalUITheme.Surface(image,image.color);
    }
    public static void Primary(Button button)
    {
        if(button.targetGraphic is Image image)MedicalUITheme.Surface(image,MedicalUITheme.Teal);
    }
}
