using UnityEngine;
using Arcana.Core;

namespace Arcana
{
    [CreateAssetMenu(menuName="ARCANA/Lab configuration")]
    public sealed class LabConfig : ScriptableObject
    {
        public Model model=new Model();
        public float samplingDistance=.028f,simplificationTolerance=.008f,maxContactJump=.24f;
        public double initialReserve=.002,defaultPreload=.008;
        public void Validate(){model.Validate();if(samplingDistance<=0||simplificationTolerance<=0||maxContactJump<samplingDistance||initialReserve<0)throw new System.ArgumentException("Invalid lab configuration");}
        void OnValidate(){try{Validate();}catch(System.Exception ex){Debug.LogError(ex.Message,this);}}
    }
    public static class Coordinates
    {
        // Proper orthogonal rotation: research (x,y,z) -> Unity (x,z,-y), Y up.
        public static Vector3 ToUnity(D3 p)=>new Vector3((float)p.x,(float)p.z,(float)-p.y);
        public static D3 ToResearch(Vector3 p)=>new D3(p.x,-p.z,p.y);
        public static D3 Data(Vector3 p)=>new D3(p.x,p.y,p.z);
        public static Vector3 Vector(D3 p)=>new Vector3((float)p.x,(float)p.y,(float)p.z);
    }
    public static class Palette
    {
        public static Color Hex(string value){ColorUtility.TryParseHtmlString(value,out var color);return color;}
        public static readonly Color Stone=Hex("#D8C8A5"),Floor=Hex("#9B8970"),Wood=Hex("#684A32"),Ink=Hex("#182E35"),Brass=Hex("#C9A35B"),Paper=Hex("#F3E8CB"),Teal=Hex("#39C6B2"),Red=Hex("#DB775D"),Sky=Hex("#BBD6D9"),Sun=Hex("#FFF0D5");
    }
}
