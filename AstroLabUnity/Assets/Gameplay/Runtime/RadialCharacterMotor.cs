using UnityEngine;

namespace AstroLab.Gameplay
{
    /// <summary>Upright capsule sweeps relative to local gravity; native CC remains available for flat training scenes.</summary>
    public sealed class RadialCharacterMotor : MonoBehaviour
    {
        public bool IsGrounded { get; private set; }
        public float Radius { get; private set; }
        public float Height { get; private set; }
        private CapsuleCollider body;
        private Vector3 center;
        private readonly RaycastHit[] hits=new RaycastHit[64];
        private readonly Collider[] overlaps=new Collider[32];
        private const float Skin=.025f;

        private void Awake()
        {
            var original=GetComponent<CharacterController>();Radius=original.radius;Height=original.height;center=original.center;
            original.enabled=false;
            body=GetComponent<CapsuleCollider>();if(!body)body=gameObject.AddComponent<CapsuleCollider>();
            body.radius=Radius;body.height=Height;body.center=center;body.direction=1;
            var rigidbody=GetComponent<Rigidbody>();if(!rigidbody)rigidbody=gameObject.AddComponent<Rigidbody>();
            rigidbody.isKinematic=true;rigidbody.useGravity=false;
        }
        private void Endpoints(Vector3 position,out Vector3 a,out Vector3 b)
        {
            Vector3 c=position+transform.rotation*center;
            float length=Mathf.Max(0,Height*.5f-Radius);a=c+transform.up*length;b=c-transform.up*length;
        }
        private bool Sweep(Vector3 position,Vector3 displacement,out RaycastHit closest)
        {
            Endpoints(position,out var a,out var b);
            int count=Physics.CapsuleCastNonAlloc(a,b,Radius-Skin,displacement.normalized,hits,displacement.magnitude+Skin,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            closest=default;float distance=float.PositiveInfinity;
            for(int i=0;i<count;i++)
            {
                if(hits[i].collider.transform.IsChildOf(transform)||hits[i].distance>=distance)continue;
                closest=hits[i];distance=hits[i].distance;
            }
            return distance<float.PositiveInfinity;
        }
        public CollisionFlags Move(Vector3 displacement)
        {
            Vector3 position=transform.position,up=transform.up;
            bool rising=Vector3.Dot(displacement,up)>.001f;
            ResolvePenetration(ref position);
            Vector3 remaining=displacement;CollisionFlags flags=CollisionFlags.None;
            for(int iteration=0;iteration<5 && remaining.sqrMagnitude>.0000001f;iteration++)
            {
                if(!Sweep(position,remaining,out var hit)){position+=remaining;break;}
                float normal=Vector3.Dot(hit.normal,up);
                flags|=normal>.55f?CollisionFlags.Below:normal<-.55f?CollisionFlags.Above:CollisionFlags.Sides;
                // Step over low thresholds while grounded, after checking head clearance and forward space.
                if(IsGrounded&&!rising&&Mathf.Abs(normal)<.2f)
                {
                    Vector3 horizontal=Vector3.ProjectOnPlane(remaining,up);
                    if(horizontal.sqrMagnitude>.0001f && !Sweep(position,up*.28f,out _) && !Sweep(position+up*.28f,horizontal,out _))
                    {position+=up*.28f+horizontal;remaining=Vector3.zero;break;}
                }
                float advance=Mathf.Clamp(hit.distance-Skin,0,remaining.magnitude);
                Vector3 travelled=remaining.normalized*advance;position+=travelled;
                remaining=Vector3.ProjectOnPlane(remaining-travelled,hit.normal);
                if(advance<.0001f&&Vector3.Dot(remaining,displacement)<=0)break;
            }
            IsGrounded=false;
            if(!rising && ProbeGround(position,out var ground))
            {
                IsGrounded=true;flags|=CollisionFlags.Below;
                float desiredDistance=2*Skin+.07f;
                position-=up*Mathf.Clamp(ground.distance-desiredDistance,0,.28f);
            }
            transform.position=position;return flags;
        }
        private bool ProbeGround(Vector3 position,out RaycastHit closest)
        {
            Endpoints(position,out _,out var lower);
            int count=Physics.SphereCastNonAlloc(lower+transform.up*.07f,Radius-Skin,-transform.up,hits,.36f,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
            closest=default;float distance=float.PositiveInfinity;
            for(int i=0;i<count;i++)
            {
                if(hits[i].collider.transform.IsChildOf(transform)||Vector3.Dot(hits[i].normal,transform.up)<.55f||hits[i].distance>=distance)continue;
                closest=hits[i];distance=hits[i].distance;
            }
            return distance<float.PositiveInfinity;
        }
        private void ResolvePenetration(ref Vector3 position)
        {
            for(int pass=0;pass<3;pass++)
            {
                Endpoints(position,out var a,out var b);
                int count=Physics.OverlapCapsuleNonAlloc(a,b,Radius-Skin,overlaps,Physics.DefaultRaycastLayers,QueryTriggerInteraction.Ignore);
                bool moved=false;
                for(int i=0;i<count;i++)
                {
                    var other=overlaps[i];if(other.transform.IsChildOf(transform))continue;
                    if(Physics.ComputePenetration(body,position,transform.rotation,other,other.transform.position,other.transform.rotation,out var direction,out float depth)&&depth>Skin)
                    {position+=direction*(depth+Skin);moved=true;}
                }
                if(!moved)break;
            }
        }
    }
}
