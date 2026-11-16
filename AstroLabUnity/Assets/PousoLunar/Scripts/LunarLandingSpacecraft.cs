using System;
using UnityEngine;

namespace AstroLab.PousoLunar
{
    /// <summary>
    /// Montagem determinística da instância, sem alterar FBX/importador. Os filhos gerados são
    /// transitórios para não serializar cópias de materiais ou malhas dos colegas na cena.
    /// </summary>
    [ExecuteAlways, DefaultExecutionOrder(-200), DisallowMultipleComponent]
    public sealed class LunarLandingSpacecraft : MonoBehaviour
    {
        public LunarLandingController controller;
        public GameObject sourceModel;
        public Material plumeMaterial;
        public Material gasMaterial;
        public Material dustMaterial;
        [Min(.1f)] public float modelHeight = 8;
        [Tooltip("Offset visual, nunca altera LandingPoint. Modelo original NÃO tem pernas.")]
        public Vector3 visualOffset;
        public Vector3 visualEuler;
        [Serializable]
        public sealed class RCSSocket
        {
            public string label;
            public Vector3 sourcePosition;
            public Vector3 exhaustDirection;
            public RCSSocket(string name, Vector3 position, Vector3 direction)
            { label=name; sourcePosition=position; exhaustDirection=direction; }
        }
        [Header("Sockets no referencial métrico original do FBX (ver INSPECAO_MODELO.md)")]
        public Vector3[] engineNozzlePositions = { new Vector3(0,3,9.85f), new Vector3(-1.35f,3,7.75f), new Vector3(1.35f,3,7.75f) };
        public Vector3 engineExhaustDirection = Vector3.down;
        public RCSSocket[] rcsSockets = {
            new RCSSocket("Nose_Lateral_Left",new Vector3(-1.47f,41,8.26f),new Vector3(-1,0,.18f)),
            new RCSSocket("Nose_Lateral_Right",new Vector3(1.47f,41,8.26f),new Vector3(1,0,.18f)),
            new RCSSocket("Nose_Dorsal_Left",new Vector3(-.87f,41,9.04f),new Vector3(-.25f,0,1)),
            new RCSSocket("Nose_Dorsal_Right",new Vector3(.87f,41,9.04f),new Vector3(.25f,0,1)),
            new RCSSocket("Aft_Adapted_Lateral_Left",new Vector3(-2.5f,8,9.6f),new Vector3(-1,0,-.18f)),
            new RCSSocket("Aft_Adapted_Lateral_Right",new Vector3(2.5f,8,9.6f),new Vector3(1,0,-.18f)),
            new RCSSocket("Aft_Adapted_Ventral_Left",new Vector3(-2.3f,8,6.15f),Vector3.back),
            new RCSSocket("Aft_Adapted_Ventral_Right",new Vector3(2.3f,8,6.15f),Vector3.back)
        };
        public LunarLandingMainEngine mainEngine;
        public LunarLandingRCS rcs;
        public LunarLandingDust lunarDust;
        public Transform GeneratedModel { get; private set; }
        Transform generatedEffects;

        void OnEnable() => EnsureBuilt();

        [ContextMenu("Montar nave e efeitos")]
        public void EnsureBuilt()
        {
            if (GeneratedModel || !controller || !controller.visualSocket || !sourceModel) return;
            if (!mainEngine || !rcs || !lunarDust || !plumeMaterial || !gasMaterial || !dustMaterial)
            {
                Debug.LogError("Pouso: configuração de VFX incompleta no prefab.", this); return;
            }
            // Instantiate de um rig em testes pode copiar filhos transitórios; nunca os duplica.
            for (int i=controller.visualSocket.childCount-1;i>=0;i--)
            {
                var child=controller.visualSocket.GetChild(i);
                if ((child.gameObject.hideFlags & HideFlags.DontSaveInEditor) != 0 &&
                    (child.name=="FinalSpacecraftModel" || child.name=="SpacecraftPropulsion"))
                    DestroyImmediate(child.gameObject);
            }
            var model = Instantiate(sourceModel, controller.visualSocket, false);
            model.name = "FinalSpacecraftModel";
            GeneratedModel = model.transform;
            // Não transporta tanque, boosters, interiores ou LODs duplicados à Lua.
            MeshFilter exterior = null;
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                bool keep = renderer.name == "Onibus_Orbitador" || renderer.name == "Onibus_Orbitador_Transp";
                renderer.enabled = keep;
                if (renderer.name == "Onibus_Orbitador") exterior = renderer.GetComponent<MeshFilter>();
            }
            foreach (var lod in model.GetComponentsInChildren<LODGroup>(true)) lod.enabled = false;
            foreach (var collider in model.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            if (!exterior || !exterior.sharedMesh)
            {
                Debug.LogError("Pouso: malha Onibus_Orbitador não encontrada; montagem recusada.", this);
                DisposeGenerated(); return;
            }
            // Usa bounds locais importados, não AABB mundial da nave inclinada em aproximação.
            Bounds bounds = BoundsInSocket(exterior, controller.visualSocket);
            float scale = modelHeight / Mathf.Max(.001f, bounds.size.y);
            GeneratedModel.localScale *= scale;
            GeneratedModel.localPosition = visualOffset + Quaternion.Euler(visualEuler) *
                (GeneratedModel.localPosition - new Vector3(bounds.center.x, bounds.min.y, bounds.center.z)) * scale;
            GeneratedModel.localRotation = Quaternion.Euler(visualEuler) * GeneratedModel.localRotation;

            generatedEffects = Child(controller.visualSocket, "SpacecraftPropulsion", visualOffset);
            generatedEffects.localRotation = Quaternion.Euler(visualEuler);
            var engineRoot = Child(generatedEffects, "MainEngineSystem", Vector3.zero);
            Vector3[] nozzles = engineNozzlePositions ?? Array.Empty<Vector3>();
            mainEngine.controller = controller;
            mainEngine.nozzles = new LunarLandingMainEngine.Nozzle[nozzles.Length];
            for (int i = 0; i < nozzles.Length; i++)
            {
                Transform socket = Child(engineRoot, "SSME_" + i, FromSource(nozzles[i]));
                socket.localRotation = Quaternion.LookRotation(engineExhaustDirection.sqrMagnitude > .001f ? engineExhaustDirection : Vector3.down, Vector3.forward);
                var core = Jet(socket,"InnerPlume",plumeMaterial,.16f,10,.27f,5,210, new Color(.73f,.82f,.91f,.9f));
                var outer = Jet(socket,"VacuumExpansion",plumeMaterial,.26f,7,.42f,24,150,new Color(.22f,.84f,.82f,.2f));
                var glow = Jet(socket,"NozzleGlow",plumeMaterial,.32f,.03f,.1f,0,40,new Color(.73f,.82f,.91f,1));
                var light = Child(socket,"EngineLight",Vector3.zero).gameObject.AddComponent<Light>();
                light.type = LightType.Point; light.color = new Color(.22f,.84f,.82f); light.range = 2; light.intensity = 1.2f;
                light.shadows = LightShadows.None; light.enabled = false;
                mainEngine.nozzles[i] = new LunarLandingMainEngine.Nozzle { core=core, outer=outer, glow=glow, light=light };
            }
            var rcsRoot = Child(generatedEffects,"RCSSystem",Vector3.zero);
            // Nas estações do nariz existem marcas RCS reais incorporadas à malha.
            // As estações traseiras são uma adaptação visual explícita, não bocais modelados no FBX.
            var entries = new System.Collections.Generic.List<LunarLandingRCS.Thruster>();
            if (rcsSockets != null) foreach (var entry in rcsSockets)
                if (entry != null && entry.exhaustDirection.sqrMagnitude > .001f)
                    AddRCS(entries,rcsRoot,entry.label,entry.sourcePosition,entry.exhaustDirection);
            rcs.controller = controller; rcs.localCenterOfMass = new Vector3(0,modelHeight*.5f,0) + visualOffset;
            rcs.thrusters = entries.ToArray();
            lunarDust.controller = controller; lunarDust.mainEngine = mainEngine;
            lunarDust.grains = Jet(generatedEffects,"LunarBallisticDust",dustMaterial,.055f,0,.8f,0,400,new Color(.66f,.65f,.75f,.72f));
            lunarDust.touchdownGrains = Jet(generatedEffects,"TouchdownGrains",dustMaterial,.07f,0,.7f,0,120,new Color(.66f,.65f,.75f,.8f));
            ConfigureDust(lunarDust.grains); ConfigureDust(lunarDust.touchdownGrains);
            foreach (var system in new[]{lunarDust.grains,lunarDust.touchdownGrains})
            {
                var collision=system.collision; collision.enabled=true; collision.type=ParticleSystemCollisionType.Planes;
                collision.SetPlane(0,controller.landingPoint); collision.bounce=0; collision.lifetimeLoss=1;
            }
            controller.effects.mainEngine = mainEngine; controller.effects.rcs = rcs; controller.effects.lunarDust = lunarDust;
            if (controller.landingPoint) lunarDust.SetGround(controller.landingPoint.position,controller.landingPoint.rotation);
            HideGenerated(model.transform); HideGenerated(generatedEffects);
            mainEngine.ResetEffects(); rcs.ResetEffects(); lunarDust.ResetEffects();
        }

        Vector3 FromSource(Vector3 point) => (point - new Vector3(0,3,11.925f)) * (modelHeight / 40.2f);

        void AddRCS(System.Collections.Generic.List<LunarLandingRCS.Thruster> list, Transform parent,
            string label, Vector3 position, Vector3 direction)
        {
            var socket = Child(parent,"RCS_"+label,FromSource(position));
            socket.localRotation = Quaternion.LookRotation(direction.normalized,Vector3.up);
            var jet = Jet(socket,"GasJet",gasMaterial,.045f,4,.13f,20,40,new Color(.73f,.82f,.91f,.35f));
            list.Add(new LunarLandingRCS.Thruster { label=label, socket=socket, jet=jet });
        }

        static Bounds BoundsInSocket(MeshFilter filter, Transform socket)
        {
            Bounds b = filter.sharedMesh.bounds;
            Matrix4x4 matrix = socket.worldToLocalMatrix * filter.transform.localToWorldMatrix;
            Bounds result = new Bounds(matrix.MultiplyPoint3x4(b.center),Vector3.zero);
            for (int i=0;i<8;i++) result.Encapsulate(matrix.MultiplyPoint3x4(b.center + Vector3.Scale(b.extents,
                new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
            return result;
        }

        static Transform Child(Transform parent,string name,Vector3 position)
        {
            var t = new GameObject(name).transform; t.SetParent(parent,false); t.localPosition=position; return t;
        }

        static ParticleSystem Jet(Transform parent,string name,Material material,float size,float speed,
            float lifetime,float angle,int budget,Color tint)
        {
            var ps = Child(parent,name,Vector3.zero).gameObject.AddComponent<ParticleSystem>();
            ps.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main; main.playOnAwake=false; main.loop=true; main.useUnscaledTime=true;
            main.simulationSpace=ParticleSystemSimulationSpace.World; main.maxParticles=budget;
            main.startSize=size; main.startSpeed=speed; main.startLifetime=lifetime; main.startColor=tint;
            main.gravityModifier=0; main.cullingMode=ParticleSystemCullingMode.AlwaysSimulate;
            var emission = ps.emission; emission.rateOverTime=0;
            var shape=ps.shape; shape.enabled=true; shape.shapeType=ParticleSystemShapeType.Cone;
            shape.angle=angle; shape.radius=size*.3f; shape.length=.001f;
            var color=ps.colorOverLifetime; color.enabled=true;
            var gradient=new Gradient(); gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},
                new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.08f),new GradientAlphaKey(.4f,.6f),new GradientAlphaKey(0,1)});
            color.color=gradient;
            var sizes=ps.sizeOverLifetime; sizes.enabled=true;
            sizes.size=new ParticleSystem.MinMaxCurve(1,new AnimationCurve(new Keyframe(0,.3f),new Keyframe(.5f,1.25f),new Keyframe(1,0)));
            var renderer=ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial=material;
            renderer.renderMode=ParticleSystemRenderMode.Billboard;
            renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off; renderer.receiveShadows=false;
            ps.useAutoRandomSeed=false; ps.randomSeed=107;
            return ps;
        }

        static void ConfigureDust(ParticleSystem ps)
        {
            var shape=ps.shape; shape.enabled=false;
            var main=ps.main; main.loop=false;
        }

        static void HideGenerated(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
                t.gameObject.hideFlags = HideFlags.DontSaveInEditor | HideFlags.DontSaveInBuild;
        }

        void OnDisable() => DisposeGenerated();
        [ContextMenu("Remontar nave e VFX (aplica sockets)")]
        public void Rebuild() { DisposeGenerated(); EnsureBuilt(); }
        public void ClearGenerated() => DisposeGenerated();
        void DisposeGenerated()
        {
            if (mainEngine) mainEngine.ResetEffects(); if (rcs) rcs.ResetEffects(); if (lunarDust) lunarDust.ResetEffects();
            if (GeneratedModel) DestroyImmediate(GeneratedModel.gameObject);
            if (generatedEffects) DestroyImmediate(generatedEffects.gameObject);
            if (mainEngine) mainEngine.nozzles = Array.Empty<LunarLandingMainEngine.Nozzle>();
            if (rcs) rcs.thrusters = Array.Empty<LunarLandingRCS.Thruster>();
            if (lunarDust) { lunarDust.grains=null; lunarDust.touchdownGrains=null; }
            GeneratedModel=null; generatedEffects=null;
        }
    }
}
