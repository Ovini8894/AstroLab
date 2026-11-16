using UnityEngine;

namespace AstroLab.Gameplay
{
    /// <summary>Compact navigable moon. Metres and relief are gameplay scale, not lunar cartography.</summary>
    [DefaultExecutionOrder(-200)]
    public sealed class LunarWorld : MonoBehaviour
    {
        public static LunarWorld Instance { get; private set; }
        public float radius = 220f;
        public float landingPadRadius = 38f;
        public Vector3 Center => transform.position;
        private void Awake(){Instance=this;}
        private void OnDestroy(){if(Instance==this)Instance=null;}

        public float SurfaceRadius(Vector3 direction)
        {
            direction.Normalize();
            float polarDistance=Mathf.Acos(Mathf.Clamp(direction.y,-1,1))*radius;
            float h=1.6f*Mathf.Sin(direction.x*23+direction.z*11)*Mathf.Sin(direction.y*19-direction.z*13);
            // Deterministic bowl-and-rim craters over the entire globe.
            for(int i=0;i<24;i++)
            {
                float y=1-2*(i+.5f)/24f,a=i*2.399963f;
                Vector3 center=new Vector3(Mathf.Cos(a)*Mathf.Sqrt(1-y*y),y,Mathf.Sin(a)*Mathf.Sqrt(1-y*y));
                float d=Mathf.Acos(Mathf.Clamp(Vector3.Dot(direction,center),-1,1))*radius;
                float size=9+(i%5)*3;
                if(d<size*1.35f)
                {
                    float bowl=Mathf.Clamp01(1-(d*d)/(size*size));
                    h-=bowl*bowl*2.8f;
                    h+=Mathf.Exp(-Mathf.Pow((d-size)/(size*.13f),2))*1.2f;
                }
            }
            float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(landingPadRadius,68,polarDistance));
            float flat=direction.y>.5f?radius/direction.y:radius;
            return Mathf.Lerp(flat,radius+h,blend);
        }
        public Vector3 Point(Vector3 direction,float height=0)
        {direction.Normalize();return Center+direction*(SurfaceRadius(direction)+height);}
        public Vector3 FromPlanar(Vector3 planar)
        {
            float distance=new Vector2(planar.x,planar.z).magnitude;
            float angle=distance/radius;
            Vector3 direction=distance<.001f?Vector3.up:new Vector3(Mathf.Sin(angle)*planar.x/distance,Mathf.Cos(angle),Mathf.Sin(angle)*planar.z/distance);
            return Point(direction,planar.y);
        }
        public Vector3 UpAt(Vector3 point)
        {
            Vector3 radial=(point-Center).normalized;
            float angle=Mathf.Acos(Mathf.Clamp(radial.y,-1,1))*radius;
            return Vector3.Slerp(Vector3.up,radial,Mathf.SmoothStep(0,1,Mathf.InverseLerp(landingPadRadius,68,angle))).normalized;
        }
        public float Distance(Vector3 a,Vector3 b)
        {
            Vector3 x=(a-Center).normalized,y=(b-Center).normalized;
            return Mathf.Acos(Mathf.Clamp(Vector3.Dot(x,y),-1,1))*radius;
        }
        public string Bearing(Transform viewer,Vector3 target)
        {
            Vector3 tangent=Vector3.ProjectOnPlane((target-Center).normalized,UpAt(viewer.position)).normalized;
            float front=Vector3.Dot(viewer.forward,tangent),right=Vector3.Dot(viewer.right,tangent);
            return Mathf.Abs(front)>Mathf.Abs(right)?(front>=0?"À FRENTE":"ATRÁS"):(right>=0?"À DIREITA":"À ESQUERDA");
        }
        public static Vector3 Up(Vector3 point)=>Instance?Instance.UpAt(point):Vector3.up;
    }
}
