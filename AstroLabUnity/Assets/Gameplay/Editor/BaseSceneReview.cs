#if UNITY_EDITOR
using System.Collections.Generic;
using System.Linq;
using AstroLab.Nicholas;
using AstroLab.Nicholas.Editor;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace AstroLab.Gameplay.Editor
{
    public static class BaseSceneReview
    {
        public static string Apply()
        {
            if (Application.isPlaying) throw new System.InvalidOperationException("Pare o Play antes de aplicar.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            var all=scene.GetRootGameObjects().SelectMany(r=>r.GetComponentsInChildren<Transform>(true)).ToArray();
            var baseRoot=all.First(t=>t.name=="BaseLunar");
            int intact=0;
            foreach(var piece in PecasDaBase.Todas.Where(MissionPieceBindings.IsArchitecture))
            {
                var ok=all.FirstOrDefault(t=>t.name==piece.objetoOk);
                if(!ok)throw new System.InvalidOperationException("Modelo ausente: "+piece.objetoOk);
                ok.gameObject.SetActive(true);intact++;
                foreach(var name in piece.quebrados)
                    foreach(var t in all.Where(t=>t.name==name||t.name==name+"_Rotulo"))t.gameObject.SetActive(false);
            }
            foreach(var t in all.Where(t=>t.name.StartsWith("Detrito_")))t.gameObject.SetActive(false);
            NicholasMissionSceneBuilder.BindRepairTargets();
            var cache=new Dictionary<Material,Material>();
            const string folder="Assets/Gameplay/Materials";
            if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/Gameplay","Materials");
            int sides=0;
            foreach(var renderer in all.Select(t=>t.GetComponent<Renderer>()).Where(r=>r&&
                (r.transform.IsChildOf(baseRoot)||r.name.StartsWith("OK_"))))
            {
                var piece=PecasDaBase.Encontrar(renderer.name);
                bool shell=MissionPieceBindings.IsArchitecture(piece)||renderer.name.Contains("Casco")||renderer.name.StartsWith("Corredor_")||renderer.name.StartsWith("Interior_");
                if(!shell)continue;
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    var source=materials[i];if(!source||!source.HasProperty("_Cull")||source.GetFloat("_Cull")==0)continue;
                    if(!cache.TryGetValue(source,out var clone))
                    {
                        string path=folder+"/"+source.name+"_DuasFaces.mat";
                        clone=AssetDatabase.LoadAssetAtPath<Material>(path);
                        if(!clone){clone=new Material(source);AssetDatabase.CreateAsset(clone,path);}
                        clone.SetFloat("_Cull",0);clone.doubleSidedGI=true;EditorUtility.SetDirty(clone);cache[source]=clone;
                    }
                    materials[i]=clone;sides++;
                }
                renderer.sharedMaterials=materials;renderer.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.TwoSided;
            }
            var mission=Object.FindAnyObjectByType<MissionController>();
            var safety=mission.GetComponent<BaseInteriorSafety>();if(!safety)safety=mission.gameObject.AddComponent<BaseInteriorSafety>();
            var existing=all.FirstOrDefault(t=>t.name=="VolumesHabitat");
            var zonesRoot=existing?existing:new GameObject("VolumesHabitat").transform;
            zonesRoot.SetParent(baseRoot,false);zonesRoot.localPosition=Vector3.zero;zonesRoot.localRotation=Quaternion.identity;
            var zones=new List<BoxCollider>();
            Zone("Hub",new Vector3(0,2.4f,0),new Vector3(17.8f,4.8f,13));
            Zone("HubTransversal",new Vector3(0,2.4f,0),new Vector3(13,4.8f,17.8f));
            Zone("SuporteVida",new Vector3(-17,2.4f,0),new Vector3(9.8f,4.8f,9.8f));
            Zone("Estufa",new Vector3(19,2.6f,0),new Vector3(12,5.2f,12));
            Zone("Abrigo",new Vector3(16.85f,2.4f,-14.14f),new Vector3(11.5f,4.8f,11.5f));
            Zone("CorredorSuporte",new Vector3(-10.5f,2.4f,0),new Vector3(4,4.8f,2.9f));
            Zone("CorredorEstufa",new Vector3(11,2.4f,0),new Vector3(5,4.8f,2.9f));
            Zone("CorredorEclusa",new Vector3(0,2.4f,10.5f),new Vector3(2.9f,4.8f,4));
            Zone("Eclusa",new Vector3(0,2.4f,14.7f),new Vector3(5,4.8f,4.8f));
            Zone("CorredorAbrigo",new Vector3(9.6f,2.4f,-8.1f),new Vector3(2.9f,4.8f,8.8f),-50);
            safety.Configure(zones.ToArray());EditorUtility.SetDirty(safety);
            int artifacts=ApolloReplicaModels.Apply();
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            return $"Estrutura íntegra: {intact}; faces corrigidas: {sides}; volumes: {zones.Count}; réplicas: {artifacts}";

            void Zone(string name,Vector3 center,Vector3 size,float yaw=0)
            {
                var t=zonesRoot.Find(name);if(!t)t=new GameObject(name).transform;
                t.SetParent(zonesRoot,false);t.localPosition=center;t.localRotation=Quaternion.Euler(0,yaw,0);
                var col=t.GetComponent<BoxCollider>();if(!col)col=t.gameObject.AddComponent<BoxCollider>();
                col.isTrigger=true;col.size=size;zones.Add(col);
            }
        }
    }
}
#endif

