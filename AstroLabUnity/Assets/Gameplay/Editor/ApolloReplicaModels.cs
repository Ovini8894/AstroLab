#if UNITY_EDITOR
using System.Linq;
using AstroLab.Nicholas;
using AstroLab.Nicholas.Core;
using UnityEditor;
using UnityEngine;

namespace AstroLab.Gameplay.Editor
{
    /// <summary>Stylized teaching replicas, modelled in scene units rather than generic beacons.</summary>
    public static class ApolloReplicaModels
    {
        private static Material silver, dark, gold, white, blue;
        public static int Apply()
        {
            silver=Material("Aluminio",new Color(.65f,.72f,.79f),.75f);
            dark=Material("Borracha",new Color(.08f,.09f,.14f),.05f);
            gold=Material("IsolamentoDourado",new Color(.81f,.55f,.19f),.55f);
            white=Material("TecidoClaro",new Color(.89f,.87f,.77f),.05f);
            blue=Material("Cristal",new Color(.32f,.68f,.85f),.65f);
            int count=0;
            foreach(var target in Object.FindObjectsByType<MissionHistoricalTarget>(FindObjectsInactive.Include))
            {
                Transform root=target.transform;
                foreach(var name in new[]{"Base","Haste","Topo_Emissivo","ReplicaDetalhada"})
                {var child=root.Find(name);if(child)Object.DestroyImmediate(child.gameObject);}
                var model=new GameObject("ReplicaDetalhada").transform;model.SetParent(root,false);
                switch(target.Artifact)
                {
                    case ArtifactId.Hammer: Hammer(model);break;
                    case ArtifactId.Feather: Feather(model);break;
                    case ArtifactId.Rover: Rover(model);break;
                    case ArtifactId.Retroreflector: Reflector(model);break;
                    case ArtifactId.FalconDescentStage: Falcon(model);break;
                }
                var renderers=model.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;
                foreach(var r in renderers)bounds.Encapsulate(r.bounds);
                var box=root.GetComponent<BoxCollider>();if(!box)box=root.gameObject.AddComponent<BoxCollider>();
                box.center=root.InverseTransformPoint(bounds.center);
                box.size=bounds.size+new Vector3(.18f,.15f,.18f);
                EditorUtility.SetDirty(target);count++;
            }
            return count;
        }
        private static Material Material(string name,Color color,float metal)
        {
            const string folder="Assets/Gameplay/Materials";
            if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/Gameplay","Materials");
            string path=folder+"/Apollo_"+name+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
            mat.SetColor("_BaseColor",color);mat.SetFloat("_Metallic",metal);mat.SetFloat("_Smoothness",.32f);EditorUtility.SetDirty(mat);return mat;
        }
        private static Transform Part(Transform parent,string name,PrimitiveType type,Vector3 pos,Vector3 scale,Material mat,Vector3 rotation=default)
        {
            var go=GameObject.CreatePrimitive(type);go.name=name;go.transform.SetParent(parent,false);
            go.transform.localPosition=pos;go.transform.localScale=scale;go.transform.localEulerAngles=rotation;
            Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<Renderer>().sharedMaterial=mat;return go.transform;
        }
        private static void Beam(Transform root,string name,Vector3 a,Vector3 b,float thickness,Material mat)
        {
            var part=Part(root,name,PrimitiveType.Cylinder,(a+b)*.5f,new Vector3(thickness,Vector3.Distance(a,b)*.5f,thickness),mat);
            part.localRotation=Quaternion.FromToRotation(Vector3.up,(b-a).normalized);
        }
        private static void Plinth(Transform root)
        {
            Part(root,"SuporteExpositivo",PrimitiveType.Cylinder,new Vector3(0,.09f,0),new Vector3(.8f,.09f,.8f),dark);
            Part(root,"PlacaApollo15",PrimitiveType.Cube,new Vector3(0,.22f,.3f),new Vector3(.55f,.05f,.16f),gold,new Vector3(-20,0,0));
        }
        private static void Hammer(Transform root)
        {
            Plinth(root);
            Beam(root,"CaboDeAluminio",new Vector3(-.16f,.23f,0),new Vector3(.13f,.95f,0),.045f,silver);
            Beam(root,"Empunhadura",new Vector3(-.16f,.24f,0),new Vector3(-.06f,.49f,0),.065f,dark);
            for(int i=0;i<6;i++)Part(root,"AnelDoCabo",PrimitiveType.Cylinder,new Vector3(-.145f+i*.015f,.27f+i*.038f,0),new Vector3(.071f,.009f,.071f),silver,new Vector3(0,0,-22));
            Part(root,"CabecaDoMartelo",PrimitiveType.Cube,new Vector3(.12f,.96f,0),new Vector3(.39f,.13f,.14f),silver,new Vector3(0,0,-22));
            Part(root,"FaceDeImpacto",PrimitiveType.Cube,new Vector3(-.065f,1.035f,0),new Vector3(.04f,.15f,.16f),dark,new Vector3(0,0,-22));
            Part(root,"PontaDeGeologia",PrimitiveType.Cube,new Vector3(.31f,.885f,0),new Vector3(.16f,.06f,.10f),silver,new Vector3(0,0,-40));
        }
        private static void Feather(Transform root)
        {
            Plinth(root);
            var vertices=new System.Collections.Generic.List<Vector3>();
            var triangles=new System.Collections.Generic.List<int>();
            for(int side=-1;side<=1;side+=2)
            for(int i=0;i<22;i++)
            {
                float t=i/22f,u=(i+1)/22f;
                Vector3 a=new Vector3(-.10f+t*.25f,.33f+t*.70f,.008f);
                Vector3 b=new Vector3(-.10f+u*.25f,.33f+u*.70f,.008f);
                Vector3 c=a+new Vector3(side*Mathf.Sin(t*Mathf.PI)*(.13f+(side>0?.025f:0)),.04f,0);
                Vector3 d=b+new Vector3(side*Mathf.Sin(u*Mathf.PI)*(.13f+(side>0?.025f:0)),.04f,0);
                int n=vertices.Count;vertices.AddRange(new[]{a,b,c,d,a,b,c,d});
                triangles.AddRange(new[]{n,n+1,n+2,n+2,n+1,n+3,n+6,n+5,n+4,n+7,n+5,n+6});
            }
            MeshPart(root,"VexiloDaPena","Pena",vertices,triangles,white);
            Beam(root,"RaqueDaPena",new Vector3(-.14f,.21f,0),new Vector3(.17f,1.1f,0),.016f,white);
            // Each barb is a tapered blade with a visible central spine and an asymmetric outline.
            for(int side=-1;side<=1;side+=2)
            for(int i=0;i<22;i++)
            {
                float t=i/21f;float width=Mathf.Sin(t*Mathf.PI)*(.13f+(side>0?.025f:0));
                var a=new Vector3(-.10f+t*.25f,.33f+t*.70f,0);
                var b=a+new Vector3(side*width,.07f,-.012f);
                Beam(root,"BarbaDaPena",a,b,.012f,i%4==0?silver:white);
            }
        }
        private static void Rover(Transform root)
        {
            Part(root,"Chassi",PrimitiveType.Cube,new Vector3(0,.55f,0),new Vector3(1.45f,.18f,2.2f),gold);
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
            {
                var center=new Vector3(x*.89f,.39f,z*.83f);
                Part(root,"RodaMalha",PrimitiveType.Cylinder,center,new Vector3(.76f,.14f,.76f),dark,new Vector3(0,0,90));
                Part(root,"CuboDaRoda",PrimitiveType.Cylinder,center+new Vector3(x*.15f,0,0),new Vector3(.26f,.025f,.26f),silver,new Vector3(0,0,90));
                for(int j=0;j<12;j++)
                {
                    float a=j*Mathf.PI/6;
                    Beam(root,"RaioDaRoda",center+new Vector3(x*.15f,0,0),center+new Vector3(x*.15f,Mathf.Sin(a)*.34f,Mathf.Cos(a)*.34f),.014f,silver);
                }
                Beam(root,"Suspensao",new Vector3(x*.5f,.55f,z*.62f),center,.06f,silver);
                Part(root,"Paralama",PrimitiveType.Cube,center+new Vector3(0,.43f,0),new Vector3(.39f,.045f,.82f),gold);
            }
            for(int side=-1;side<=1;side+=2)
            {
                Part(root,"Assento",PrimitiveType.Cube,new Vector3(side*.37f,.79f,.05f),new Vector3(.54f,.09f,.61f),white);
                Part(root,"Encosto",PrimitiveType.Cube,new Vector3(side*.37f,1.08f,-.23f),new Vector3(.54f,.63f,.07f),white,new Vector3(-12,0,0));
                Beam(root,"EstruturaDoAssento",new Vector3(side*.59f,.65f,-.25f),new Vector3(side*.59f,1.34f,-.31f),.035f,silver);
            }
            Beam(root,"MastroDaAntena",new Vector3(-.5f,.62f,.88f),new Vector3(-.5f,1.9f,.88f),.035f,silver);
            Part(root,"AntenaDirecional",PrimitiveType.Sphere,new Vector3(-.5f,1.94f,.88f),new Vector3(.61f,.61f,.08f),silver,new Vector3(12,20,0));
            Beam(root,"HasteDoControle",new Vector3(0,.65f,.5f),new Vector3(0,1.05f,.4f),.035f,dark);
            Part(root,"ControleEmT",PrimitiveType.Cube,new Vector3(0,1.06f,.4f),new Vector3(.27f,.045f,.05f),dark);
            Part(root,"CaixaDeEquipamento",PrimitiveType.Cube,new Vector3(0,.84f,-.89f),new Vector3(1.1f,.43f,.38f),silver);
            Beam(root,"AntenaVertical",new Vector3(.5f,.7f,-.8f),new Vector3(.5f,2,-.8f),.018f,silver);
        }
        private static void Reflector(Transform root)
        {
            Part(root,"BaseDoRefletor",PrimitiveType.Cube,new Vector3(0,.12f,0),new Vector3(1.1f,.16f,.8f),gold);
            var array=new GameObject("MatrizDeRefletores").transform;array.SetParent(root,false);array.localPosition=new Vector3(0,.55f,0);array.localEulerAngles=new Vector3(-30,0,0);
            Part(array,"Quadro",PrimitiveType.Cube,Vector3.zero,new Vector3(.98f,.1f,.7f),silver);
            for(int row=0;row<10;row++)for(int col=0;col<10;col++)
                Part(array,"Prisma",PrimitiveType.Cylinder,new Vector3(-.43f+col*.095f,.066f,-.30f+row*.066f),new Vector3(.054f,.018f,.054f),blue);
            Beam(root,"SuporteEsquerdo",new Vector3(-.4f,.18f,0),new Vector3(-.4f,.55f,0),.045f,silver);
            Beam(root,"SuporteDireito",new Vector3(.4f,.18f,0),new Vector3(.4f,.55f,0),.045f,silver);
        }
        private static void Falcon(Transform root)
        {
            var vertices=new System.Collections.Generic.List<Vector3>();
            var triangles=new System.Collections.Generic.List<int>();
            for(int i=0;i<8;i++)
            {
                float a=i*Mathf.PI/4,b=(i+1)*Mathf.PI/4;
                Vector3 v=new Vector3(Mathf.Cos(a)*1.1f,0,Mathf.Sin(a)*1.1f),w=new Vector3(Mathf.Cos(b)*1.1f,0,Mathf.Sin(b)*1.1f);
                int n=vertices.Count;
                vertices.AddRange(new[]{v+Vector3.up*.75f,w+Vector3.up*.75f,v+Vector3.up*1.95f,w+Vector3.up*1.95f});
                triangles.AddRange(new[]{n,n+2,n+1,n+1,n+2,n+3});
            }
            MeshPart(root,"EstagioOctogonal","Falcon",vertices,triangles,gold);
            Part(root,"PlataformaSuperior",PrimitiveType.Cube,new Vector3(0,1.99f,0),new Vector3(1.7f,.13f,1.7f),dark);
            Part(root,"MotorDeDescida",PrimitiveType.Cylinder,new Vector3(0,.65f,0),new Vector3(.72f,.28f,.72f),dark);
            for(int i=0;i<4;i++)
            {
                float a=i*Mathf.PI/2+Mathf.PI/4;Vector3 v=new Vector3(Mathf.Cos(a),0,Mathf.Sin(a));
                var foot=v*1.9f+Vector3.up*.14f;
                Part(root,"Sapata",PrimitiveType.Cylinder,foot,new Vector3(.68f,.07f,.68f),silver);
                Beam(root,"PernaDoModulo",v*.8f+Vector3.up*1.55f,foot+Vector3.up*.12f,.12f,silver);
                Beam(root,"TravamentoDaPerna",v*.7f+Vector3.up*.86f,v*1.6f+Vector3.up*.42f,.06f,dark);
                Part(root,"PainelDeIsolamento",PrimitiveType.Cube,v*.97f+Vector3.up*1.35f,new Vector3(.8f,.89f,.08f),gold,new Vector3(0,-a*Mathf.Rad2Deg+90,0));
            }
            for(int side=-1;side<=1;side+=2)Beam(root,"LongarinaDaEscada",new Vector3(side*.20f,.3f,1.65f),new Vector3(side*.20f,2,1.04f),.035f,silver);
            for(int i=0;i<8;i++)Beam(root,"DegrauDaEscada",new Vector3(-.2f,.36f+i*.21f,1.63f-i*.075f),new Vector3(.2f,.36f+i*.21f,1.63f-i*.075f),.03f,silver);
            Part(root,"BandeiraDoModulo",PrimitiveType.Cube,new Vector3(.35f,1.5f,1.08f),new Vector3(.4f,.24f,.015f),white);
        }
        private static void MeshPart(Transform root,string name,string assetName,System.Collections.Generic.List<Vector3> vertices,System.Collections.Generic.List<int> triangles,Material material)
        {
            const string folder="Assets/Gameplay/Models";
            if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/Gameplay","Models");
            string path=folder+"/Apollo_"+assetName+".asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(!mesh){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
            mesh.name="Apollo_"+assetName;mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();EditorUtility.SetDirty(mesh);
            var part=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));part.transform.SetParent(root,false);
            part.GetComponent<MeshFilter>().sharedMesh=mesh;part.GetComponent<MeshRenderer>().sharedMaterial=material;
        }
    }
}
#endif
