using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace AstroLab.Asteroids
{
    /// <summary>Visible hub console and live telescope camera. Sky placement is explicitly illustrative.</summary>
    public sealed class AsteroidObservationStation : MonoBehaviour
    {
        public RenderTexture Preview { get; private set; }
        public Transform SkyTarget { get; private set; }
        public TMP_Text SkyLabel { get; private set; }
        private AsteroidTerminal terminal;
        private Camera telescope;
        private Mesh rockMesh;
        private Vector3[] sphereVertices;
        private Material bodyMaterial,screenMaterial,rockMaterial;
        public string TargetId { get; private set; }

        public void Configure(AsteroidTerminal owner)
        {
            terminal=owner;
            transform.position=new Vector3(3.4f,1.85f,5.5f);
            var hit=gameObject.AddComponent<BoxCollider>();hit.size=new Vector3(1.3f,.8f,.5f);
            var shader=Shader.Find("Universal Render Pipeline/Lit");
            bodyMaterial=new Material(shader);bodyMaterial.color=new Color(.12f,.17f,.28f);
            screenMaterial=new Material(Shader.Find("Universal Render Pipeline/Unlit"));screenMaterial.color=new Color(.18f,.85f,.91f);
            Part("Console",PrimitiveType.Cube,Vector3.zero,new Vector3(1.3f,.8f,.5f),bodyMaterial);
            Part("Tela",PrimitiveType.Cube,new Vector3(0,.04f,-.26f),new Vector3(1.12f,.56f,.03f),screenMaterial);
            Part("Pedestal",PrimitiveType.Cube,new Vector3(0,-.58f,0),new Vector3(.5f,.5f,.45f),bodyMaterial);
            Label("OBSERVATÓRIO NASA\n[E] OBSERVAR  ·  [F4]",new Vector3(0,.76f,0),2.2f,new Vector2(2.2f,.65f));
            var mast=new GameObject("TelescopioNoHub").transform;mast.SetParent(transform,false);mast.position=new Vector3(1.5f,7.5f,1.5f);
            var tube=GameObject.CreatePrimitive(PrimitiveType.Cylinder);tube.name="TuboDoTelescopio";tube.transform.SetParent(mast,false);
            tube.transform.localScale=new Vector3(.5f,.85f,.5f);tube.transform.localRotation=Quaternion.Euler(35,0,0);Destroy(tube.GetComponent<Collider>());tube.GetComponent<Renderer>().sharedMaterial=bodyMaterial;
            var cameraObject=new GameObject("CameraDoObservatorio",typeof(Camera));cameraObject.transform.SetParent(mast,false);
            telescope=cameraObject.GetComponent<Camera>();telescope.fieldOfView=10;telescope.nearClipPlane=.1f;telescope.farClipPlane=6000;
            telescope.cullingMask=~(1<<2);telescope.allowHDR=false;telescope.enabled=false;
            Preview=new RenderTexture(768,432,16,RenderTextureFormat.ARGB32);Preview.name="ObservacaoAsteroide";Preview.Create();telescope.targetTexture=Preview;
            var rock=GameObject.CreatePrimitive(PrimitiveType.Sphere);rock.name="AsteroideNoCeu_RepresentacaoIlustrativa";rock.transform.SetParent(transform,false);
            Destroy(rock.GetComponent<Collider>());rock.transform.localScale=new Vector3(12,9,11);
            rockMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit"));rockMaterial.SetFloat("_Smoothness",.08f);rockMaterial.color=new Color(.52f,.48f,.42f);
            rock.GetComponent<Renderer>().sharedMaterial=rockMaterial;rock.GetComponent<Renderer>().shadowCastingMode=ShadowCastingMode.Off;
            rockMesh=Instantiate(rock.GetComponent<MeshFilter>().sharedMesh);sphereVertices=rockMesh.vertices;rock.GetComponent<MeshFilter>().sharedMesh=rockMesh;
            SkyTarget=rock.transform;
            var label=new GameObject("IdentificacaoDeObservacao",typeof(TextMeshPro));label.transform.SetParent(transform,false);
            label.layer=2;SkyLabel=label.GetComponent<TextMeshPro>();SkyLabel.fontSize=6;SkyLabel.color=new Color(.9f,.7f,.4f);SkyLabel.alignment=TextAlignmentOptions.Center;SkyLabel.rectTransform.sizeDelta=new Vector2(25,6);
        }
        public void ShowTarget(AsteroidRecord record)
        {
            if(record==null||!SkyTarget)return;
            if(TargetId!=record.Id)
            {
                TargetId=record.Id;int seed=17;foreach(char c in TargetId)seed=unchecked(seed*31+c);
                var vertices=new Vector3[sphereVertices.Length];
                for(int i=0;i<vertices.Length;i++)
                {
                    var v=sphereVertices[i];float noise=.82f+.18f*Mathf.Sin(v.x*19+v.y*13+v.z*23+(seed%500));vertices[i]=v*noise;
                }
                rockMesh.vertices=vertices;rockMesh.RecalculateNormals();rockMesh.RecalculateBounds();
            }
            SkyTarget.gameObject.SetActive(true);
            SkyLabel.text="ALVO NO OBSERVATÓRIO\nÓrbita ilustrativa · [F4]";
            Update();
        }
        private void Update()
        {
            if(!telescope||!SkyTarget)return;
            Vector3 direction=Quaternion.AngleAxis(Mathf.Sin(Time.unscaledTime*.025f)*24,Vector3.up)*new Vector3(.4f,.85f,1).normalized;
            SkyTarget.position=telescope.transform.position+direction*140;
            SkyTarget.Rotate(Vector3.up,Time.unscaledDeltaTime*5,Space.Self);
            telescope.transform.LookAt(SkyTarget.position,Vector3.up);telescope.enabled=terminal&&terminal.IsOpen;
            SkyLabel.transform.position=SkyTarget.position+Vector3.down*9;
            if(Camera.main)SkyLabel.transform.rotation=Quaternion.LookRotation(SkyLabel.transform.position-Camera.main.transform.position,Camera.main.transform.up);
        }
        private void Part(string name,PrimitiveType shape,Vector3 position,Vector3 scale,Material material)
        {var part=GameObject.CreatePrimitive(shape);part.name=name;part.transform.SetParent(transform,false);part.transform.localPosition=position;part.transform.localScale=scale;Destroy(part.GetComponent<Collider>());part.GetComponent<Renderer>().sharedMaterial=material;}
        private void Label(string text,Vector3 position,float size,Vector2 rect)
        {var go=new GameObject("Identificacao",typeof(TextMeshPro));go.transform.SetParent(transform,false);go.transform.localPosition=position;go.transform.localRotation=Quaternion.Euler(0,180,0);var label=go.GetComponent<TextMeshPro>();label.text=text;label.fontSize=size;label.color=new Color(.2f,.9f,.95f);label.alignment=TextAlignmentOptions.Center;label.rectTransform.sizeDelta=rect;}
        private void OnDestroy()
        {
            if(Preview){Preview.Release();Destroy(Preview);}if(rockMesh)Destroy(rockMesh);
            if(bodyMaterial)Destroy(bodyMaterial);if(screenMaterial)Destroy(screenMaterial);if(rockMaterial)Destroy(rockMaterial);
        }
    }
}
