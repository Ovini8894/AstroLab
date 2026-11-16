#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using TMPro;
using Object=UnityEngine.Object;
namespace AstroLab.Gameplay.Editor
{
    public static class InteriorDetails
    {
        static Material cream,dark,cyan,gold,glass;
        static Transform root;
        public static void Apply()
        {
            var old=GameObject.Find("DetalhesHabitaveis");if(old)Object.DestroyImmediate(old);
            root=new GameObject("DetalhesHabitaveis").transform;
            cream=LunarSceneUpgrade.Mat("Interior_Ceramica",new Color(.75f,.78f,.83f));
            dark=LunarSceneUpgrade.Mat("Interior_Grafite",new Color(.10f,.13f,.24f));
            cyan=LunarSceneUpgrade.Mat("Interior_Telemetria",new Color(.13f,.72f,.79f));
            gold=LunarSceneUpgrade.Mat("Interior_Identificacao",new Color(.85f,.57f,.19f));
            glass=LunarSceneUpgrade.Mat("Interior_Reservatorios",new Color(.22f,.42f,.6f));
            // Equipment is placed along walls; the hub and corridor centre lines remain open.
            for(int i=0;i<3;i++)Locker(new Vector3(-5.9f+i*.88f,1,-5.6f));
            Table(new Vector3(-5,1,3.7f));
            Box("Banco",new Vector3(-5,1.27f,5.2f),new Vector3(2.2f,.5f,.45f),cream,true);
            for(int i=0;i<4;i++)Box("CaixaDeAmostras",new Vector3(-5.7f+i*.45f,1.62f,3.65f),new Vector3(.32f,.16f,.25f),i%2==0?glass:gold);
            Label("LABORATÓRIO / AMOSTRAS",new Vector3(-5,2.7f,5.9f),.18f,Quaternion.Euler(0,180,0));
            Sign("SUPORTE DE VIDA  /  O2 + H2O",new Vector3(-8.1f,3.45f,0),Quaternion.Euler(0,90,0));
            Sign("ESTUFA  /  ALIMENTOS",new Vector3(8.1f,3.45f,0),Quaternion.Euler(0,-90,0));
            Sign("ECLUSA  /  EXPLORAÇÃO",new Vector3(0,3.45f,8.1f),Quaternion.Euler(0,180,0));
            Sign("ABRIGO  /  RADIAÇÃO",new Vector3(6.1f,3.45f,-5.1f),Quaternion.Euler(0,-45,0));
            // Life-support service wall, away from generator, recycler and charger access.
            Box("EstanteDeConsumiveis",new Vector3(-16.7f,2.0f,4.0f),new Vector3(2.8f,2,.4f),dark,true);
            for(int i=0;i<5;i++)
            {
                Cylinder("CilindroO2",new Vector3(-17.8f+i*.52f,2.05f,3.72f),new Vector3(.26f,.57f,.26f),cream);
                Cylinder("ValvulaO2",new Vector3(-17.8f+i*.52f,2.65f,3.72f),new Vector3(.14f,.05f,.14f),cyan);
            }
            Label("RESERVA / OXIGÊNIO",new Vector3(-16.7f,3.2f,3.70f),.16f,Quaternion.identity);
            Box("InstrumentacaoH2O",new Vector3(-17,2.4f,-4.55f),new Vector3(1.4f,.8f,.12f),dark);
            for(int i=0;i<3;i++)Box("MedidorDePressao",new Vector3(-17.45f+i*.45f,2.48f,-4.46f),new Vector3(.28f,.42f,.035f),cyan);
            Beam("TubulacaoLimpa",new Vector3(-20,3.65f,3.5f),new Vector3(-14,3.65f,3.5f),.08f,cream);
            Sign("RECARGA DO TRAJE  /  [C]\n1 AÇÃO · DENTRO DA BASE",new Vector3(-14,3.3f,-3.9f),Quaternion.identity);
            // Greenhouse service console; does not obstruct the central grow aisle.
            Box("SuporteDoPainelEstufa",new Vector3(14,1.32f,-2.5f),new Vector3(.7f,.62f,.26f),dark,true);
            Beam("LuminariaDeCultivo",new Vector3(15,4.15f,-3),new Vector3(23,4.15f,-3),.06f,cyan);
            Beam("LuminariaDeCultivo",new Vector3(15,4.15f,3),new Vector3(23,4.15f,3),.06f,cyan);
            Box("SensorAmbiental",new Vector3(14.2f,2.7f,2.8f),new Vector3(.5f,.65f,.15f),cream);
            Box("LeituraAmbiental",new Vector3(14.2f,2.7f,2.9f),new Vector3(.36f,.4f,.04f),cyan);
            // Radiation shelter: actual supplies, bed and monitoring, with clear access to its panel.
            Box("MacaDoAbrigo",new Vector3(17.8f,1.45f,-17.7f),new Vector3(2.4f,.9f,.9f),cream,true);
            Box("Colchao",new Vector3(17.8f,1.94f,-17.7f),new Vector3(2.24f,.15f,.8f),glass);
            Box("Travesseiro",new Vector3(18.65f,2.05f,-17.7f),new Vector3(.45f,.15f,.65f),cream);
            for(int i=0;i<3;i++)Box("SuprimentoSelado",new Vector3(20.6f,1.35f,-14.3f+i*.75f),new Vector3(.75f,.7f,.6f),i==1?gold:cream,true);
            Sign("SUPRIMENTOS / USO DA TRIPULAÇÃO",new Vector3(20.8f,3.0f,-14),Quaternion.Euler(0,-90,0));
            Sign("ABRIGO PROTEGIDO\nMANTENÇÃO NO PAINEL CENTRAL",new Vector3(16,3.0f,-18.5f),Quaternion.identity);
            // Subtle route lights raised above the floor, avoiding coplanar decals.
            for(int i=-7;i<=7;i+=2)
            {Box("GuiaNoPiso",new Vector3(i,1.045f,.9f),new Vector3(.6f,.025f,.07f),cyan);}
            for(int i=2;i<=8;i+=2)Box("GuiaEclusa",new Vector3(.9f,1.045f,i),new Vector3(.07f,.025f,.6f),gold);
        }
        static Transform Box(string name,Vector3 pos,Vector3 size,Material mat,bool solid=false)
        {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(root);go.transform.position=pos;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;if(!solid)Object.DestroyImmediate(go.GetComponent<Collider>());return go.transform;}
        static void Cylinder(string name,Vector3 pos,Vector3 size,Material mat)
        {var go=GameObject.CreatePrimitive(PrimitiveType.Cylinder);go.name=name;go.transform.SetParent(root);go.transform.position=pos;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mat;Object.DestroyImmediate(go.GetComponent<Collider>());}
        static void Beam(string name,Vector3 a,Vector3 b,float width,Material mat)
        {var t=Box(name,(a+b)*.5f,new Vector3(width,Vector3.Distance(a,b),width),mat);t.rotation=Quaternion.FromToRotation(Vector3.up,b-a);}
        static void Locker(Vector3 foot)
        {Box("Armario",foot+Vector3.up*.85f,new Vector3(.8f,1.7f,.5f),cream,true);Box("PortaDoArmario",foot+new Vector3(0,.87f,.265f),new Vector3(.68f,1.5f,.035f),dark);Box("Puxador",foot+new Vector3(.23f,.87f,.3f),new Vector3(.035f,.28f,.04f),cyan);}
        static void Table(Vector3 foot)
        {Box("Bancada",foot+Vector3.up*.45f,new Vector3(2.2f,.12f,.8f),cream,true);foreach(int side in new[]{-1,1})Box("PeDaBancada",foot+new Vector3(side*.9f,.22f,0),new Vector3(.12f,.44f,.6f),dark,true);}
        static void Sign(string text,Vector3 pos,Quaternion rotation)
        {var t=Box("PlacaDeSetor",pos,new Vector3(2.9f,.44f,.06f),dark);t.rotation=rotation;Label(text,pos+rotation*new Vector3(0,0,.045f),.14f,rotation*Quaternion.Euler(0,180,0));}
        static void Label(string text,Vector3 pos,float size,Quaternion rotation)
        {var go=new GameObject("Identificacao",typeof(TextMeshPro));go.transform.SetParent(root);go.transform.SetPositionAndRotation(pos,rotation);var tmp=go.GetComponent<TextMeshPro>();tmp.text=text;tmp.fontSize=size*10;tmp.color=new Color(.3f,.9f,.95f);tmp.alignment=TextAlignmentOptions.Center;tmp.rectTransform.sizeDelta=new Vector2(2.8f,.44f);}
    }
}
#endif

