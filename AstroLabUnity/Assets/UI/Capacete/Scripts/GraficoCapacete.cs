using UnityEngine;
using UnityEngine.UI;

namespace AstroLab.Capacete
{
    [AddComponentMenu("AstroLab/Capacete/Gráfico vetorial")]
    [RequireComponent(typeof(CanvasRenderer))]
    public class GraficoCapacete : MaskableGraphic
    {
        public enum Forma { Painel, Visor, Oxigenio, Bateria, Localizacao, Mira }
        public Forma forma;
        [Range(0.5f, 8f)] public float espessura = 2f;

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var r = rectTransform.rect;
            if (forma == Forma.Painel)
            {
                var p = new[] { new Vector2(.045f,0),new Vector2(.955f,0),new Vector2(1,.18f),new Vector2(1,.82f),new Vector2(.955f,1),new Vector2(.045f,1),new Vector2(0,.82f),new Vector2(0,.18f) };
                Polygon(vh, p, TemaCapacete.Alpha(TemaCapacete.Fundo, .86f), r);
                Path(vh,p,espessura*4,TemaCapacete.Alpha(color,.045f),r,true);
                Path(vh,p,espessura,color,r,true);
                return;
            }
            if (forma == Forma.Visor)
            {
                var edge = new[] {new Vector2(0,.20f),new Vector2(.013f,.09f),new Vector2(.058f,.026f),new Vector2(.17f,.008f),new Vector2(.5f,0),new Vector2(.83f,.008f),new Vector2(.942f,.026f),new Vector2(.987f,.09f),new Vector2(1,.20f),new Vector2(1,.80f),new Vector2(.984f,.91f),new Vector2(.936f,.973f),new Vector2(.82f,.995f),new Vector2(.5f,1),new Vector2(.18f,.995f),new Vector2(.064f,.973f),new Vector2(.016f,.91f),new Vector2(0,.80f)};
                Path(vh,edge,25,TemaCapacete.Alpha(TemaCapacete.Fundo,.86f),r,true);
                Path(vh,edge,9,TemaCapacete.Alpha(color,.06f),r,true);
                Path(vh,edge,2,TemaCapacete.Alpha(color,.5f),r,true);
                Line(vh,new Vector2(.26f,.979f),new Vector2(.41f,.964f),2,color,r);
                Line(vh,new Vector2(.59f,.964f),new Vector2(.74f,.979f),2,color,r);
                Line(vh,new Vector2(.49f,.976f),new Vector2(.51f,.976f),3,color,r);
                return;
            }
            if (forma == Forma.Mira)
            {
                Line(vh,new Vector2(.27f,.5f),new Vector2(.42f,.5f),1.5f,color,r);
                Line(vh,new Vector2(.58f,.5f),new Vector2(.73f,.5f),1.5f,color,r);
                Line(vh,new Vector2(.5f,.27f),new Vector2(.5f,.42f),1.5f,color,r);
                Line(vh,new Vector2(.5f,.58f),new Vector2(.5f,.73f),1.5f,color,r);
                return;
            }
            if (forma == Forma.Oxigenio)
            {
                Path(vh,new[]{new Vector2(.5f,.92f),new Vector2(.5f,.52f),new Vector2(.31f,.30f)},espessura,color,r);
                Line(vh,new Vector2(.5f,.52f),new Vector2(.69f,.30f),espessura,color,r);
                var left=new[]{new Vector2(.42f,.74f),new Vector2(.32f,.73f),new Vector2(.22f,.60f),new Vector2(.12f,.37f),new Vector2(.1f,.15f),new Vector2(.17f,.09f),new Vector2(.39f,.17f),new Vector2(.44f,.28f),new Vector2(.42f,.74f)};
                Path(vh,left,espessura,color,r);
                var right=new Vector2[left.Length]; for(int i=0;i<left.Length;i++)right[i]=new Vector2(1-left[i].x,left[i].y);
                Path(vh,right,espessura,color,r);
                return;
            }
            if (forma == Forma.Bateria)
            {
                Path(vh,new[]{new Vector2(.08f,.2f),new Vector2(.87f,.2f),new Vector2(.87f,.8f),new Vector2(.08f,.8f)},espessura,color,r,true);
                Path(vh,new[]{new Vector2(.87f,.4f),new Vector2(.97f,.4f),new Vector2(.97f,.6f),new Vector2(.87f,.6f)},espessura,color,r);
                for(int i=0;i<4;i++)Line(vh,new Vector2(.22f+i*.16f,.32f),new Vector2(.22f+i*.16f,.68f),r.width*.10f,color,r);
                return;
            }
            if(forma == Forma.Localizacao)
            {
                var points=new Vector2[25];
                for(int i=0;i<25;i++){float a=(i/24f*300f-60f)*Mathf.Deg2Rad;points[i]=new Vector2(.5f+Mathf.Cos(a)*.29f,.64f+Mathf.Sin(a)*.27f);}
                Path(vh,points,espessura,color,r);
                Path(vh,new[]{points[0],new Vector2(.5f,.07f),points[24]},espessura,color,r);
                return;
            }
        }

        static Vector2 P(Vector2 p,Rect r)=>new Vector2(r.x+p.x*r.width,r.y+p.y*r.height);
        static void Polygon(VertexHelper vh,Vector2[] points,Color c,Rect r)
        {
            int start=vh.currentVertCount;var center=Vector2.zero;foreach(var p in points)center+=P(p,r);center/=points.Length;
            vh.AddVert(center,c,Vector2.zero);foreach(var p in points)vh.AddVert(P(p,r),c,Vector2.zero);
            for(int i=0;i<points.Length;i++)vh.AddTriangle(start,start+i+1,start+(i+1)%points.Length+1);
        }
        static void Path(VertexHelper vh,Vector2[] p,float w,Color c,Rect r,bool close=false)
        {for(int i=1;i<p.Length;i++)Line(vh,p[i-1],p[i],w,c,r);if(close)Line(vh,p[p.Length-1],p[0],w,c,r);}
        static void Line(VertexHelper vh,Vector2 a,Vector2 b,float width,Color c,Rect r)
        {
            a=P(a,r);b=P(b,r);var d=b-a;if(d.sqrMagnitude<.0001f)return;var n=new Vector2(-d.y,d.x).normalized*width*.5f;int i=vh.currentVertCount;
            vh.AddVert(a-n,c,Vector2.zero);vh.AddVert(a+n,c,Vector2.zero);vh.AddVert(b+n,c,Vector2.zero);vh.AddVert(b-n,c,Vector2.zero);
            vh.AddTriangle(i,i+1,i+2);vh.AddTriangle(i,i+2,i+3);
        }
    }
}
