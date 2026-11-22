using UnityEngine;
using UnityEngine.UI;

// One palette shared by the menu, terminals, TV, captions and object cards.
public static class MedicalUITheme
{
    public static readonly Color Paper=new Color(.965f,.953f,.929f,1);
    public static readonly Color Navy=new Color(.09f,.19f,.29f,1);
    public static readonly Color Teal=new Color(.14f,.46f,.51f,1);
    public static readonly Color Muted=new Color(.35f,.43f,.49f,1);
    public static readonly Color Coral=new Color(.73f,.36f,.29f,1);
    public static readonly Color Success=new Color(.22f,.48f,.37f,1);
    public static readonly Color Screen=new Color(.055f,.12f,.18f,1);
    public static readonly Color LightInk=new Color(.95f,.95f,.92f,1);
    public static readonly Color LightAccent=new Color(.46f,.83f,.8f,1);
    private static Sprite _rounded;
    public static Sprite Rounded
    {
        get
        {
            if(_rounded)return _rounded;
            _rounded=Resources.Load<Sprite>("UI/MedicalRounded");if(_rounded)return _rounded;
            const int size=64;const float radius=12;
            var texture=new Texture2D(size,size,TextureFormat.RGBA32,false);texture.name="Medical UI · Rounded surface";texture.wrapMode=TextureWrapMode.Clamp;
            var pixels=new Color[size*size];
            for(int y=0;y<size;y++)for(int x=0;x<size;x++)
            {
                var p=new Vector2(x+.5f,y+.5f);var closest=new Vector2(Mathf.Clamp(p.x,radius,size-radius),Mathf.Clamp(p.y,radius,size-radius));
                pixels[y*size+x]=new Color(1,1,1,Mathf.Clamp01(radius-(p-closest).magnitude+.5f));
            }
            texture.SetPixels(pixels);texture.Apply();
            _rounded=Sprite.Create(texture,new Rect(0,0,size,size),Vector2.one*.5f,100,0,SpriteMeshType.FullRect,new Vector4(14,14,14,14));_rounded.name="Medical UI · Rounded";return _rounded;
        }
    }
    public static void ReloadSprite()=>_rounded=Resources.Load<Sprite>("UI/MedicalRounded");
    public static void Surface(Image image,Color color)
    {image.color=color;image.sprite=Rounded;image.type=Image.Type.Sliced;image.pixelsPerUnitMultiplier=1;}
    public static void Button(Button button)
    {
        var image=button.targetGraphic as Image;if(image)Surface(image,Navy);
        var colors=button.colors;colors.normalColor=Color.white;colors.highlightedColor=new Color(.68f,.9f,.89f);colors.selectedColor=new Color(.75f,.92f,.9f);colors.pressedColor=new Color(.49f,.75f,.74f);colors.disabledColor=new Color(.5f,.56f,.59f,.6f);colors.fadeDuration=.12f;button.colors=colors;
        foreach(var text in button.GetComponentsInChildren<Text>(true)){text.color=LightInk;text.fontStyle=FontStyle.Bold;}
    }
    public static void Apply(Canvas canvas,bool dark=false)
    {
        if(!canvas)return;
        var background=canvas.transform.Find("Background");if(background&&background.TryGetComponent<Image>(out var bg))Surface(bg,dark?Screen:Paper);
        else if(canvas.TryGetComponent<Image>(out var root))Surface(root,dark?Screen:Paper);
        foreach(var text in canvas.GetComponentsInChildren<Text>(true))
        {
            if(text.GetComponentInParent<Button>())continue;
            if(text.color==Teal||text.color==LightAccent||text.fontStyle==FontStyle.Italic)text.color=dark?LightAccent:Teal;
            else if(text.color==Muted)text.color=dark?new Color(.67f,.77f,.8f):Muted;
            else if(text.color!=Coral&&text.color!=Success)text.color=dark?LightInk:Navy;
        }
        foreach(var button in canvas.GetComponentsInChildren<Button>(true))Button(button);
    }
}
