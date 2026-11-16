#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using AstroLab.Nicholas;
using AstroLab.Nicholas.Core;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Object=UnityEngine.Object;

namespace AstroLab.Gameplay.Editor
{
    public static class LunarSceneUpgrade
    {
        const string AssetsRoot="Assets/Gameplay/Models";
        public static string Apply()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Pare o Play antes de configurar a Lua.");
            var scene=UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if(scene.name!="CapacetePedro")throw new InvalidOperationException("Abra a cena CapacetePedro.");
            // Reuse the restoration setup, retaining all mission and replica information.
            BaseSceneReview.Apply();
            var root=GameObject.Find("BaseLunar").transform;
            var planet=Object.FindAnyObjectByType<LunarWorld>();
            if(!planet)planet=new GameObject("LuaNavegavel").AddComponent<LunarWorld>();
            planet.radius=220;planet.transform.position=new Vector3(0,-planet.radius,0);
            if(!planet.GetComponent<LunarExplorationGuide>())planet.gameObject.AddComponent<LunarExplorationGuide>();
            var ground=Mat("Lua_Regolito",new Color(.48f,.45f,.59f));
            Vector3[] faces={Vector3.up,Vector3.down,Vector3.right,Vector3.left,Vector3.forward,Vector3.back};
            const int resolution=80;
            for(int f=0;f<faces.Length;f++)
            {
                Vector3 up=faces[f],axisA=new Vector3(up.y,up.z,up.x),axisB=Vector3.Cross(up,axisA);
                var vertices=new Vector3[(resolution+1)*(resolution+1)];var uv=new Vector2[vertices.Length];
                var triangles=new int[resolution*resolution*6];int t=0;
                for(int y=0;y<=resolution;y++)for(int x=0;x<=resolution;x++)
                {
                    int i=x+y*(resolution+1);Vector2 percent=new Vector2(x/(float)resolution,y/(float)resolution);
                    Vector3 normal=(up+(percent.x-.5f)*2*axisA+(percent.y-.5f)*2*axisB).normalized;
                    vertices[i]=normal*planet.SurfaceRadius(normal);uv[i]=percent*32;
                    if(x<resolution&&y<resolution)
                    {triangles[t++]=i;triangles[t++]=i+resolution+2;triangles[t++]=i+resolution+1;triangles[t++]=i;triangles[t++]=i+1;triangles[t++]=i+resolution+2;}
                }
                var mesh=SaveMesh("Lua_Face"+f,vertices,triangles,uv);
                var child=planet.transform.Find("Terreno_"+f);if(!child)child=new GameObject("Terreno_"+f).transform;
                child.SetParent(planet.transform,false);child.localPosition=Vector3.zero;child.localRotation=Quaternion.identity;child.localScale=Vector3.one;
                var filter=child.GetComponent<MeshFilter>();if(!filter)filter=child.gameObject.AddComponent<MeshFilter>();filter.sharedMesh=mesh;
                var renderer=child.GetComponent<MeshRenderer>();if(!renderer)renderer=child.gameObject.AddComponent<MeshRenderer>();renderer.sharedMaterial=ground;
                var collider=child.GetComponent<MeshCollider>();if(!collider)collider=child.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=null;collider.sharedMesh=mesh;
            }
            int hidden=0,wrapped=0;
            var all=root.GetComponentsInChildren<Transform>(true);
            foreach(var node in all)
                if(node.name=="Terreno_Lua"||node.name.StartsWith("Rocha_")||node.name.StartsWith("Rocha_Borda")||node.name.StartsWith("Cratera"))
                {node.gameObject.SetActive(false);hidden++;}
            foreach(var node in all.Concat(Object.FindAnyObjectByType<BaseConsertada>().GetComponentsInChildren<Transform>(true)))
            {
                if(!node.GetComponent<MeshFilter>()||!node.GetComponent<MeshRenderer>())continue;
                if(node.name.StartsWith("Rocha")||node.name=="Terreno_Lua"||node.name.StartsWith("Cratera")||node.name.StartsWith("Terra_")||node.name.StartsWith("Detrito"))continue;
                var renderer=node.GetComponent<MeshRenderer>();
                var saved=node.GetComponent<LunarPlacement>();
                if(!saved && (new Vector2(renderer.bounds.center.x,renderer.bounds.center.z).magnitude<39||renderer.bounds.center.magnitude>500))continue;
                Warp(node,planet);wrapped++;
            }
            ScatterRocks(planet,Mat("Lua_Rochas",new Color(.29f,.28f,.38f)));
            ClipShelter(root);
            MoveGreenhousePanel(root);
            // Longer exploratory trail over different sides of the moon, within return-trip suit budgets.
            var locations=new Dictionary<ArtifactId,Vector3>
            {
                {ArtifactId.Hammer,new Vector3(55,0,45)}, {ArtifactId.Feather,new Vector3(-90,0,100)},
                {ArtifactId.Rover,new Vector3(-160,0,-65)}, {ArtifactId.Retroreflector,new Vector3(135,0,-155)},
                {ArtifactId.FalconDescentStage,new Vector3(0,0,-285)}
            };
            foreach(var artifact in Object.FindObjectsByType<MissionHistoricalTarget>(FindObjectsInactive.Include))
            {
                Vector3 planar=locations[artifact.Artifact];Vector3 pos=planet.FromPlanar(planar);
                artifact.transform.SetPositionAndRotation(pos,Quaternion.FromToRotation(Vector3.up,planet.UpAt(pos)));
            }
            if(!Object.FindAnyObjectByType<AstroLab.Asteroids.AsteroidTerminal>())
                new GameObject("ObservatorioNASA").AddComponent<AstroLab.Asteroids.AsteroidTerminal>();
            InteriorDetails.Apply();
            int duplicates=RemoveCoincidentFaces(root);
            FinishVisuals();
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);
            return $"Lua física: raio {planet.radius} m, 6 setores; {wrapped} modelos adaptados; {hidden} elementos planos ocultos; {duplicates} faces coincidentes removidas; 5 réplicas distribuídas.";
        }
        public static Material Mat(string name,Color color)
        {
            const string folder="Assets/Gameplay/Materials";
            if(!AssetDatabase.IsValidFolder(folder))AssetDatabase.CreateFolder("Assets/Gameplay","Materials");
            string path=folder+"/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat){mat=new Material(Shader.Find("AstroLab/Toon"));AssetDatabase.CreateAsset(mat,path);}
            mat.shader=Shader.Find("AstroLab/Toon");mat.SetColor("_BaseColor",color);EditorUtility.SetDirty(mat);return mat;
        }
        public static string FinishVisuals()
        {
            int replaced=0;
            var root=GameObject.Find("BaseLunar").transform;
            foreach(var renderer in root.GetComponentsInChildren<MeshRenderer>(true).Concat(Object.FindAnyObjectByType<BaseConsertada>().GetComponentsInChildren<MeshRenderer>(true)))
            {
                if(renderer.name.StartsWith("REPAIR_")||renderer.name.StartsWith("Rocha")||renderer.name.StartsWith("Terra"))continue;
                if(new Vector2(renderer.bounds.center.x,renderer.bounds.center.z).magnitude>39)continue;
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    var source=materials[i];if(!source)continue;
                    string sourceName=source.name.Replace("_DuasFaces","");
                    string clean=sourceName=="M_Casco_Queimado"?"M_Casco":sourceName=="M_Metal_Enferrujado"?"M_Metal":sourceName=="M_Vidro_Rachado"?"M_Vidro":null;
                    if(clean==null)continue;
                    var material=AssetDatabase.LoadAssetAtPath<Material>("Assets/Gameplay/Materials/"+clean+"_DuasFaces.mat");
                    if(!material)material=AssetDatabase.LoadAssetAtPath<Material>("Assets/BaseLunar/Materiais/"+clean+".mat");
                    if(material){materials[i]=material;replaced++;}
                }
                renderer.sharedMaterials=materials;
            }
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
            return replaced+" materiais estruturais danificados substituídos na cena lunar.";
        }
        static Mesh SaveMesh(string name,Vector3[] vertices,int[] triangles,Vector2[] uv=null)
        {
            if(!AssetDatabase.IsValidFolder(AssetsRoot))AssetDatabase.CreateFolder("Assets/Gameplay","Models");
            string path=AssetsRoot+"/"+name+".asset";var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(!mesh){mesh=new Mesh();AssetDatabase.CreateAsset(mesh,path);}else mesh.Clear();
            mesh.indexFormat=UnityEngine.Rendering.IndexFormat.UInt32;mesh.vertices=vertices;mesh.triangles=triangles;
            if(uv!=null)mesh.uv=uv;mesh.RecalculateNormals();mesh.RecalculateBounds();mesh.name=name;EditorUtility.SetDirty(mesh);return mesh;
        }
        static void Warp(Transform node,LunarWorld world)
        {
            var placement=node.GetComponent<LunarPlacement>();if(!placement)placement=node.gameObject.AddComponent<LunarPlacement>();
            var filter=node.GetComponent<MeshFilter>();
            if(!placement.recorded){placement.sourceMesh=filter.sharedMesh;placement.sourceMatrix=node.localToWorldMatrix;placement.recorded=true;}
            var source=placement.sourceMesh;
            var vertices=source.vertices.Select(v=>world.FromPlanar(placement.sourceMatrix.MultiplyPoint3x4(v))).ToArray();
            string path=AssetsRoot+"/Esferico_"+node.name+".asset";
            var clone=AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if(!clone){clone=Object.Instantiate(source);AssetDatabase.CreateAsset(clone,path);}
            clone.vertices=vertices;
            var sourceNormals=source.normals;var normals=new Vector3[vertices.Length];
            var normalMatrix=placement.sourceMatrix.inverse.transpose;
            for(int i=0;i<normals.Length;i++)
            {
                var original=i<sourceNormals.Length?normalMatrix.MultiplyVector(sourceNormals[i]).normalized:Vector3.up;
                normals[i]=Quaternion.FromToRotation(Vector3.up,world.UpAt(vertices[i]))*original;
                if(normals[i].sqrMagnitude<.01f)normals[i]=world.UpAt(vertices[i]);
            }
            clone.normals=normals;clone.RecalculateBounds();EditorUtility.SetDirty(clone);
            node.SetPositionAndRotation(Vector3.zero,Quaternion.identity);node.localScale=Vector3.one;filter.sharedMesh=clone;
            var collider=node.GetComponent<MeshCollider>();if(collider){collider.sharedMesh=null;collider.sharedMesh=clone;}
            EditorUtility.SetDirty(placement);
        }
        static void ScatterRocks(LunarWorld world,Material material)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            Vector3[] oct={Vector3.up,Vector3.down,Vector3.right,Vector3.left,Vector3.forward,Vector3.back};
            int[] indices={0,4,2,0,3,4,0,5,3,0,2,5,1,2,4,1,4,3,1,3,5,1,5,2};
            for(int i=0;i<360;i++)
            {
                float y=1-2*(i+.5f)/360f,a=i*2.399963f;Vector3 n=new Vector3(Mathf.Cos(a)*Mathf.Sqrt(1-y*y),y,Mathf.Sin(a)*Mathf.Sqrt(1-y*y));
                if(Mathf.Acos(n.y)*world.radius<72)continue;
                Vector3 point=world.Point(n)-world.Center;Quaternion q=Quaternion.FromToRotation(Vector3.up,n);
                float size=.3f+(i%7)*.17f;int offset=vertices.Count;
                foreach(var v in oct)vertices.Add(point+q*Vector3.Scale(v,new Vector3(size,size*.5f,size*.8f)));
                foreach(int j in indices)triangles.Add(offset+j);
            }
            var mesh=SaveMesh("Lua_RochasGlobais",vertices.ToArray(),triangles.ToArray());
            var child=world.transform.Find("RochasGlobais");if(!child)child=new GameObject("RochasGlobais",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider)).transform;
            child.SetParent(world.transform,false);child.GetComponent<MeshFilter>().sharedMesh=mesh;child.GetComponent<MeshRenderer>().sharedMaterial=material;
            child.GetComponent<MeshCollider>().sharedMesh=mesh;
        }
        static void ClipShelter(Transform root)
        {
            var node=root.GetComponentsInChildren<Transform>(true).First(t=>t.name=="Abrigo_Cobertura_Regolito");
            var filter=node.GetComponent<MeshFilter>();var original=filter.sharedMesh;
            const string path=AssetsRoot+"/Abrigo_Regolito_Contido.asset";
            var mesh=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(!mesh){mesh=Object.Instantiate(original);AssetDatabase.CreateAsset(mesh,path);}
            var vertices=mesh.vertices;
            for(int i=0;i<vertices.Length;i++)
            {Vector3 p=node.TransformPoint(vertices[i]);p.z=Mathf.Min(p.z,-8.3f);vertices[i]=node.InverseTransformPoint(p);}
            mesh.vertices=vertices;mesh.RecalculateBounds();filter.sharedMesh=mesh;
            var collider=node.GetComponent<MeshCollider>();if(collider)collider.sharedMesh=mesh;EditorUtility.SetDirty(mesh);
        }
        static void MoveGreenhousePanel(Transform root)
        {
            var repaired=Object.FindAnyObjectByType<BaseConsertada>().transform;
            foreach(var parent in new[]{root,repaired})
            foreach(var node in parent.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="REPAIR_Estufa_Painel"||t.name=="OK_Estufa_Painel"))
                node.position=new Vector3(14,1.6f,-2.5f);
        }
        static int RemoveCoincidentFaces(Transform root)
        {
            var all=root.GetComponentsInChildren<MeshFilter>(false).Concat(Object.FindAnyObjectByType<BaseConsertada>().GetComponentsInChildren<MeshFilter>(false));
            var seen=new HashSet<string>();int removed=0;
            foreach(var filter in all)
            {
                if(filter.name.StartsWith("Rocha")||filter.GetComponent<LunarPlacement>()||filter.name.StartsWith("Terra"))continue;
                var source=filter.sharedMesh;if(!source||source.vertexCount>100000)continue;
                var vertices=source.vertices;var submeshes=new List<int[]>();int count=0;
                for(int s=0;s<source.subMeshCount;s++)
                {
                    var kept=new List<int>();var tris=source.GetTriangles(s);
                    for(int i=0;i<tris.Length;i+=3)
                    {
                        string[] keys={Key(filter.transform.TransformPoint(vertices[tris[i]])),Key(filter.transform.TransformPoint(vertices[tris[i+1]])),Key(filter.transform.TransformPoint(vertices[tris[i+2]]))};Array.Sort(keys,StringComparer.Ordinal);
                        if(seen.Add(string.Join("/",keys)))kept.AddRange(new[]{tris[i],tris[i+1],tris[i+2]});else count++;
                    }
                    submeshes.Add(kept.ToArray());
                }
                if(count==0)continue;
                string path=AssetsRoot+"/SemSobreposicao_"+filter.name+".asset";var clone=AssetDatabase.LoadAssetAtPath<Mesh>(path);
                if(!clone){clone=Object.Instantiate(source);AssetDatabase.CreateAsset(clone,path);}
                for(int s=0;s<submeshes.Count;s++)clone.SetTriangles(submeshes[s],s);
                filter.sharedMesh=clone;var collider=filter.GetComponent<MeshCollider>();if(collider)collider.sharedMesh=clone;
                EditorUtility.SetDirty(clone);removed+=count;
            }
            return removed;
        }
        static string Key(Vector3 p)=>Mathf.RoundToInt(p.x*1000)+","+Mathf.RoundToInt(p.y*1000)+","+Mathf.RoundToInt(p.z*1000);
    }
}
#endif

