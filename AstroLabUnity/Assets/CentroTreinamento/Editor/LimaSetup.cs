using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;

namespace AstroLab.Centro.Editor
{
    // Lima (MPFB): import Humanoid + Animator Controller (Idle / Walk / Talk / Point / Wave).
    // Só age se Modelos/NPC_Lima.fbx existir (o FBX entra depois da aprovação do rosto).
    public static class LimaSetup
    {
        public const string Fbx = "Assets/CentroTreinamento/Modelos/NPC_Lima.fbx";
        const string Controller = "Assets/CentroTreinamento/Animacao/Lima.controller";
        static readonly string[] EmLoop = { "Idle", "Walk", "Talk", "Point" };

        public static bool Existe => File.Exists(Fbx);

        [MenuItem("Centro de Treinamento/4. Lima: import Humanoid + Animator")]
        public static RuntimeAnimatorController Preparar()
        {
            if (!Existe) { Debug.LogWarning("[Lima] falta " + Fbx); return null; }
            var imp = (ModelImporter)AssetImporter.GetAtPath(Fbx);
            bool mudou = false;
            if (imp.animationType != ModelImporterAnimationType.Generic) { imp.animationType = ModelImporterAnimationType.Generic; imp.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel; mudou = true; }
            if (imp.importNormals != ModelImporterNormals.Import) { imp.importNormals = ModelImporterNormals.Import; mudou = true; }
            if (!imp.importBlendShapes) { imp.importBlendShapes = true; mudou = true; }
            var clips = imp.defaultClipAnimations;
            if (clips.Length > 0)
            {
                foreach (var c in clips)
                {
                    string n = Nome(c.name);
                    c.name = n;
                    c.loopTime = EmLoop.Contains(n);
                    c.lockRootRotation = c.lockRootHeightY = c.lockRootPositionXZ = true;
                    c.keepOriginalOrientation = c.keepOriginalPositionY = c.keepOriginalPositionXZ = true;
                }
                imp.clipAnimations = clips; mudou = true;
            }
            if (mudou) imp.SaveAndReimport();
            var porNome = AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview"))
                                       .GroupBy(c => Nome(c.name)).ToDictionary(g => g.Key, g => g.First());
            Debug.Log($"[Lima] clips: {string.Join(", ", porNome.Keys)}; humano={AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<Avatar>().FirstOrDefault()?.isHuman}");

            Directory.CreateDirectory(Path.GetDirectoryName(Controller));
            AssetDatabase.DeleteAsset(Controller);
            var ac = AnimatorController.CreateAnimatorControllerAtPath(Controller);
            ac.AddParameter("Andando", AnimatorControllerParameterType.Bool);
            ac.AddParameter("Falando", AnimatorControllerParameterType.Bool);
            ac.AddParameter("VelAnda", AnimatorControllerParameterType.Float);
            ac.AddParameter("Apontar", AnimatorControllerParameterType.Trigger);
            ac.AddParameter("Acenar", AnimatorControllerParameterType.Trigger);
            var sm = ac.layers[0].stateMachine;
            AnimatorState Estado(string n, Vector3 pos)
            {
                var st = sm.AddState(n, pos);
                st.motion = porNome.TryGetValue(n, out var c) ? c : null;
                return st;
            }
            var idle = Estado("Idle", new Vector3(300, 0)); sm.defaultState = idle;
            var walk = Estado("Walk", new Vector3(300, 120)); walk.speedParameterActive = true; walk.speedParameter = "VelAnda";
            var talk = Estado("Talk", new Vector3(560, 0));
            var point = Estado("Point", new Vector3(560, 120));
            var wave = Estado("Wave", new Vector3(560, -120));
            void T(AnimatorState a, AnimatorState b, float dur, params (AnimatorConditionMode m, float v, string p)[] cs)
            {
                var t = a.AddTransition(b); t.hasExitTime = false; t.duration = dur;
                foreach (var c in cs) t.AddCondition(c.m, c.v, c.p);
            }
            T(idle, walk, 0.2f, (AnimatorConditionMode.If, 0, "Andando"));
            T(talk, walk, 0.2f, (AnimatorConditionMode.If, 0, "Andando"));
            T(walk, idle, 0.25f, (AnimatorConditionMode.IfNot, 0, "Andando"), (AnimatorConditionMode.IfNot, 0, "Falando"));
            T(walk, talk, 0.25f, (AnimatorConditionMode.IfNot, 0, "Andando"), (AnimatorConditionMode.If, 0, "Falando"));
            T(idle, talk, 0.3f, (AnimatorConditionMode.If, 0, "Falando"));
            T(talk, idle, 0.4f, (AnimatorConditionMode.IfNot, 0, "Falando"));
            var ap = sm.AddAnyStateTransition(point); ap.hasExitTime = false; ap.duration = 0.25f; ap.AddCondition(AnimatorConditionMode.If, 0, "Apontar"); ap.canTransitionToSelf = false;
            var aw = sm.AddAnyStateTransition(wave); aw.hasExitTime = false; aw.duration = 0.2f; aw.AddCondition(AnimatorConditionMode.If, 0, "Acenar"); aw.canTransitionToSelf = false;
            foreach (var s in new[] { point, wave })
            {
                var v = s.AddTransition(idle); v.hasExitTime = true; v.exitTime = s == point ? 2.5f : 2f; v.duration = 0.35f;
            }
            EditorUtility.SetDirty(ac);
            return ac;
        }

        static string Nome(string bruto)
        {
            var n = bruto.Contains("|") ? bruto.Substring(bruto.LastIndexOf('|') + 1) : bruto;
            return n.Trim();
        }

        // troca o modelo da instrutora: usado pelo "Montar cena" quando a Lima existe
        public static GameObject Instanciar()
        {
            var ac = Preparar();
            var go = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Fbx));
            var anim = go.GetComponent<Animator>();
            if (anim == null) anim = go.AddComponent<Animator>();
            if (anim.avatar == null) anim.avatar = AssetDatabase.LoadAllAssetsAtPath(Fbx).OfType<Avatar>().FirstOrDefault();
            anim.runtimeAnimatorController = ac;
            anim.applyRootMotion = false;
            anim.cullingMode = AnimatorCullingMode.CullUpdateTransforms;
            go.AddComponent<AstroLab.Centro.LimaAnimador>();
            return go;
        }
    }
}
