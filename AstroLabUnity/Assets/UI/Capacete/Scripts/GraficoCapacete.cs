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
                // Painel translúcido de ficção científica com cantos chanfrados discretos
                var p = new[] {
                    new Vector2(.035f, 0), new Vector2(.965f, 0),
                    new Vector2(1, .14f), new Vector2(1, .86f),
                    new Vector2(.965f, 1), new Vector2(.035f, 1),
                    new Vector2(0, .86f), new Vector2(0, .14f)
                };
                Polygon(vh, p, TemaCapacete.Alpha(TemaCapacete.Fundo, .70f), r);
                Path(vh, p, Mathf.Min(espessura, 1.3f), TemaCapacete.Alpha(color, .40f), r, true);

                // Detalhes geométricos tecnológicos sutis nos cantos
                float cw = 0.045f;
                float w = Mathf.Min(espessura, 1.5f);
                Line(vh, new Vector2(0, .26f), new Vector2(0, .14f), w, color, r);
                Line(vh, new Vector2(0, .14f), new Vector2(cw, 0), w, color, r);
                Line(vh, new Vector2(cw, 0), new Vector2(cw + .035f, 0), w, color, r);

                Line(vh, new Vector2(1 - cw - .035f, 1), new Vector2(1 - cw, 1), w, color, r);
                Line(vh, new Vector2(1 - cw, 1), new Vector2(1, .86f), w, color, r);
                Line(vh, new Vector2(1, .86f), new Vector2(1, .74f), w, color, r);
                return;
            }
            if (forma == Forma.Visor)
            {
                // Moldura do visor translúcida e suave para não competir com o cenário
                var edge = new[] {
                    new Vector2(0, .20f), new Vector2(.013f, .09f), new Vector2(.058f, .026f),
                    new Vector2(.17f, .008f), new Vector2(.5f, 0), new Vector2(.83f, .008f),
                    new Vector2(.942f, .026f), new Vector2(.987f, .09f), new Vector2(1, .20f),
                    new Vector2(1, .80f), new Vector2(.984f, .91f), new Vector2(.936f, .973f),
                    new Vector2(.82f, .995f), new Vector2(.5f, 1), new Vector2(.18f, .995f),
                    new Vector2(.064f, .973f), new Vector2(.016f, .91f), new Vector2(0, .80f)
                };
                // A dark peripheral ring frames a smaller aperture; instruments remain inside it.
                int ring=vh.currentVertCount;
                for(int i=0;i<edge.Length;i++)
                {
                    var outer=P(i<=3?new Vector2(0,0):i==4?new Vector2(.5f,0):i<=8?new Vector2(1,0):i<=12?new Vector2(1,1):i==13?new Vector2(.5f,1):new Vector2(0,1),r);
                    var inner=P(new Vector2(.045f+edge[i].x*.91f,.075f+edge[i].y*.85f),r);
                    vh.AddVert(outer,TemaCapacete.Alpha(TemaCapacete.Fundo,.96f),Vector2.zero);
                    vh.AddVert(inner,TemaCapacete.Alpha(TemaCapacete.Fundo,.90f),Vector2.zero);
                }
                for(int i=0;i<edge.Length;i++)
                {
                    int a=ring+i*2,b=ring+((i+1)%edge.Length)*2;
                    vh.AddTriangle(a,b,a+1);vh.AddTriangle(a+1,b,b+1);
                }
                var aperture=new Vector2[edge.Length];
                for(int i=0;i<edge.Length;i++)aperture[i]=new Vector2(.045f+edge[i].x*.91f,.075f+edge[i].y*.85f);
                Path(vh,aperture,4,TemaCapacete.Alpha(color,.55f),r,true);
                return;
            }
            if (forma == Forma.Mira)
            {
                // Retículo limpo, minimalista e preciso
                float w = Mathf.Min(espessura, 1.2f);
                Line(vh, new Vector2(.26f, .5f), new Vector2(.42f, .5f), w, color, r);
                Line(vh, new Vector2(.58f, .5f), new Vector2(.74f, .5f), w, color, r);
                Line(vh, new Vector2(.5f, .26f), new Vector2(.5f, .42f), w, color, r);
                Line(vh, new Vector2(.5f, .58f), new Vector2(.5f, .74f), w, color, r);
                Line(vh, new Vector2(.485f, .5f), new Vector2(.515f, .5f), w, color, r);
                return;
            }
            if (forma == Forma.Oxigenio)
            {
                Path(vh,new[]{new Vector2(.5f,.88f),new Vector2(.5f,.54f),new Vector2(.32f,.33f)},espessura,color,r);
                Line(vh,new Vector2(.5f,.54f),new Vector2(.68f,.33f),espessura,color,r);
                var left=new[]{new Vector2(.42f,.70f),new Vector2(.28f,.67f),new Vector2(.18f,.48f),new Vector2(.14f,.20f),new Vector2(.22f,.13f),new Vector2(.42f,.22f),new Vector2(.42f,.70f)};
                Path(vh,left,espessura,color,r);
                var right=new Vector2[left.Length]; for(int i=0;i<left.Length;i++)right[i]=new Vector2(1-left[i].x,left[i].y);
                Path(vh,right,espessura,color,r);
                return;
            }
            if (forma == Forma.Bateria)
            {
                Path(vh,new[]{new Vector2(.08f,.22f),new Vector2(.86f,.22f),new Vector2(.86f,.78f),new Vector2(.08f,.78f)},espessura,color,r,true);
                Path(vh,new[]{new Vector2(.86f,.40f),new Vector2(.95f,.40f),new Vector2(.95f,.60f),new Vector2(.86f,.60f)},espessura,color,r);
                float barWidth = Mathf.Max(1.2f, espessura * 1.15f);
                for(int i=0;i<3;i++)Line(vh,new Vector2(.27f+i*.20f,.35f),new Vector2(.27f+i*.20f,.65f),barWidth,color,r);
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
