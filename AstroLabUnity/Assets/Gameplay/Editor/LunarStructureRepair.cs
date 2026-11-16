#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
namespace AstroLab.Gameplay.Editor
{
    public static class LunarStructureRepair
    {
        const string Folder="Assets/Gameplay/Models/";
        public static string Apply()
        {
            if(Application.isPlaying||!UnityEngine.Object.FindAnyObjectByType<LunarWorld>())throw new InvalidOperationException("Abra a Lua fora do Play.");
            var all=UnityEngine.Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include);
            int floors=0,glass=0;
            foreach(var r in all.Where(r=>r.gameObject.activeInHierarchy&&(r.transform.IsChildOf(GameObject.Find("BaseLunar").transform)||r.name.StartsWith("OK_"))))
            {
                if(!new[]{"Hub_Casco","Eclusa_Casco","Abrigo_Casco","Estufa_Estrutura","Corredor_Eclusa","Corredor_Abrigo","OK_SuporteVida_Casco","OK_Corredor_Estufa","OK_Corredor_SuporteVida","OK_Hub_Torre_Vidros","OK_Estufa_Vidros"}.Contains(r.name))continue;
                var filter=r.GetComponent<MeshFilter>();if(!filter||!filter.sharedMesh)continue;
                var original=AssetDatabase.LoadAllAssetsAtPath(r.name.StartsWith("OK_")?"Assets/BaseConsertada/Modelos/BaseConsertada.fbx":"Assets/BaseLunar/Modelos/BaseLunar.fbx").OfType<Mesh>().FirstOrDefault(m=>m.name==r.name);var source=original?original:filter.sharedMesh;var verts=source.vertices;var mesh=UnityEngine.Object.Instantiate(source);bool changed=false;
                for(int s=0;s<source.subMeshCount;s++)
                {
                    string material=r.sharedMaterials[s].name;
                    if(!material.Contains("Piso")&&!material.Contains("Vidro"))continue;
                    var tris=source.GetTriangles(s);var points=tris.Select(i=>r.transform.TransformPoint(verts[i])).Distinct().ToArray();
                    var groups=new Dictionary<string,List<Vector3>>();
                    for(int i=0;i<tris.Length;i+=3)
                    {
                        var a=r.transform.TransformPoint(verts[tris[i]]);var b=r.transform.TransformPoint(verts[tris[i+1]]);var c=r.transform.TransformPoint(verts[tris[i+2]]);
                        Vector3 n=Vector3.Cross(b-a,c-a).normalized;
                        if(material.Contains("Piso")){if(n.y<.9f||a.y<.9f||a.y>1.15f)continue;n=Vector3.up;}
                        else if(n.x<-.001f||(Mathf.Abs(n.x)<.001f&&n.y<-.001f)||(Mathf.Abs(n.x)+Mathf.Abs(n.y)<.001f&&n.z<0))n=-n;
                        float d=Vector3.Dot(n,a);
                        string key=material.Contains("Piso")?"floor":$"{Mathf.Round(n.x*100)},{Mathf.Round(n.y*100)},{Mathf.Round(n.z*100)},{Mathf.Round(d*25)}";
                        if(!groups.ContainsKey(key))groups[key]=new List<Vector3>();groups[key].AddRange(new[]{a,b,c});
                    }
                    if(groups.Count==0)continue;
                    var newIndices=new List<int>();var newVerts=mesh.vertices.ToList();var newUv=mesh.uv.ToList();var newNormals=mesh.normals.ToList();
                    if(newUv.Count!=newVerts.Count)newUv=Enumerable.Repeat(Vector2.zero,newVerts.Count).ToList();
                    foreach(var g in groups.Values)
                    {
                        var normal=material.Contains("Piso")?Vector3.up:Vector3.Cross(g[1]-g[0],g[2]-g[0]).normalized;
                        var u=Vector3.Cross(normal,Mathf.Abs(normal.y)<.9f?Vector3.up:Vector3.forward).normalized;var v=Vector3.Cross(normal,u);
                        var hull=Hull(g.Distinct().ToList(),u,v);if(hull.Count<3)continue;
                        int start=newVerts.Count;
                        foreach(var p in hull){var q=material.Contains("Piso")?new Vector3(p.x,1.025f,p.z):p;newVerts.Add(r.transform.InverseTransformPoint(q));newNormals.Add(r.transform.InverseTransformDirection(normal));newUv.Add(new Vector2(Vector3.Dot(q,u),Vector3.Dot(q,v)));}
                        for(int j=1;j<hull.Count-1;j++)newIndices.AddRange(new[]{start,start+j,start+j+1,start,start+j+1,start+j});
                    }
                    mesh.SetVertices(newVerts);mesh.SetNormals(newNormals);mesh.SetUVs(0,newUv);mesh.SetTriangles(newIndices,s);changed=true;
                    if(material.Contains("Piso"))floors++;else glass++;
                }
                if(r.name=="Eclusa_Casco")
                {
                    for(int s=0;s<mesh.subMeshCount;s++)
                    {
                        
                        var vv=mesh.vertices;var tt=mesh.GetTriangles(s);var keep=new List<int>();
                        for(int i=0;i<tt.Length;i+=3){var p=r.transform.TransformPoint((vv[tt[i]]+vv[tt[i+1]]+vv[tt[i+2]])/3);if(p.z>17.25f&&Mathf.Abs(p.x)<1.65f&&Mathf.Max(r.transform.TransformPoint(vv[tt[i]]).y,Mathf.Max(r.transform.TransformPoint(vv[tt[i+1]]).y,r.transform.TransformPoint(vv[tt[i+2]]).y))<2.3f)continue;keep.AddRange(new[]{tt[i],tt[i+1],tt[i+2]});}
                        mesh.SetTriangles(keep,s);changed=true;
                    }
                }
                if(changed){mesh.RecalculateBounds();Assign(filter,mesh,"Integro_"+r.name);}
                else UnityEngine.Object.DestroyImmediate(mesh);
            }
            RebuildGlass(all);
            var rail=all.First(r=>r&&r.name=="Rampa_Corrimao");rail.gameObject.SetActive(false);
            var old=GameObject.Find("EntradaIntegra");if(old)UnityEngine.Object.DestroyImmediate(old);
            var root=new GameObject("EntradaIntegra");
            Material floor=AssetDatabase.LoadAssetAtPath<Material>("Assets/Gameplay/Materials/M_Piso_DuasFaces.mat");
            Material metal=AssetDatabase.LoadAssetAtPath<Material>("Assets/Gameplay/Materials/M_Metal_DuasFaces.mat");
            var ramp=new Mesh();ramp.vertices=new[]{new Vector3(-1.3f,1.025f,17.1f),new Vector3(1.3f,1.025f,17.1f),new Vector3(-1.3f,.025f,23.8f),new Vector3(1.3f,.025f,23.8f)};ramp.uv=new[]{Vector2.zero,Vector2.right,new Vector2(0,6),new Vector2(1,6)};ramp.triangles=new[]{0,2,1,1,2,3,0,1,2,1,3,2};ramp.RecalculateNormals();
            var deck=new GameObject("RampaContinua",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));deck.transform.SetParent(root.transform);deck.GetComponent<MeshRenderer>().sharedMaterial=floor;Assign(deck.GetComponent<MeshFilter>(),ramp,"Entrada_RampaContinua");
            for(int side=-1;side<=1;side+=2)for(int i=0;i<=6;i++){float z=17.3f+i;float y=1.025f-(z-17.1f)/6.7f;var post=GameObject.CreatePrimitive(PrimitiveType.Cube);post.name="BalizadorRampa";post.transform.SetParent(root.transform);post.transform.position=new Vector3(side*1.45f,y+.38f,z);post.transform.localScale=new Vector3(.07f,.75f,.07f);post.GetComponent<Renderer>().sharedMaterial=metal;}
            var door=all.First(r=>r&&r.name=="OK_Porta_Eclusa_Externa");var cube=GameObject.CreatePrimitive(PrimitiveType.Cube);var dm=UnityEngine.Object.Instantiate(cube.GetComponent<MeshFilter>().sharedMesh);UnityEngine.Object.DestroyImmediate(cube);
            dm.vertices=dm.vertices.Select(p=>door.transform.InverseTransformPoint(new Vector3(p.x*2.6f,p.y*2.3f+2.15f,p.z*.14f+17.2f))).ToArray();
            door.sharedMaterials=new[]{AssetDatabase.LoadAssetAtPath<Material>("Assets/Gameplay/Materials/M_Casco_DuasFaces.mat")};Assign(door.GetComponent<MeshFilter>(),dm,"Entrada_PortaIntegra");
            if(!door.GetComponent<RestoredSlidingDoor>())door.gameObject.AddComponent<RestoredSlidingDoor>();
            var zone=GameObject.Find("BaseLunar").transform.Find("VolumesHabitat/Eclusa");if(zone){zone.localPosition=new Vector3(0,2.4f,14.7f);zone.GetComponent<BoxCollider>().size=new Vector3(5,4.8f,4.8f);}
            EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());EditorSceneManager.SaveOpenScenes();AssetDatabase.SaveAssets();
            return $"Recompostos {floors} pisos e {glass} conjuntos de vidro; rampa contínua e porta externa automática.";
        }
        static void RebuildGlass(MeshRenderer[] all)
        {
            var material=LunarSceneUpgrade.Mat("VidroEstruturalIntegro",new Color(.35f,.7f,.85f));material.SetFloat("_Cull",0);
            foreach(var name in new[]{"OK_Hub_Torre_Vidros","OK_Estufa_Vidros","OK_SuporteVida_Casco"})
            {
                var r=all.First(x=>x&&x.name==name);var f=r.GetComponent<MeshFilter>();var vertices=new List<Vector3>();var tris=new List<int>();
                void Quad(Vector3 a,Vector3 b,Vector3 c,Vector3 d){int n=vertices.Count;vertices.AddRange(new[]{a,b,c,d}.Select(p=>r.transform.InverseTransformPoint(p)));tris.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
                if(name.Contains("Hub"))for(int i=0;i<48;i++){float a=i*Mathf.PI/24,b=(i+1)*Mathf.PI/24;Quad(new Vector3(Mathf.Cos(a)*6.06f,5.5f,Mathf.Sin(a)*6.06f),new Vector3(Mathf.Cos(b)*6.06f,5.5f,Mathf.Sin(b)*6.06f),new Vector3(Mathf.Cos(b)*6.06f,6.7f,Mathf.Sin(b)*6.06f),new Vector3(Mathf.Cos(a)*6.06f,6.7f,Mathf.Sin(a)*6.06f));}
                else if(name.Contains("Estufa"))
                {
                    foreach(float z in new[]{-7.15f,7.15f}){Quad(new Vector3(12,2,z),new Vector3(25.15f,2,z),new Vector3(25.15f,4,z),new Vector3(12,4,z));Quad(new Vector3(12,4,z),new Vector3(25.15f,4,z),new Vector3(25.15f,5.6f,0),new Vector3(12,5.6f,0));}
                    Quad(new Vector3(25.15f,2,-7.15f),new Vector3(25.15f,2,7.15f),new Vector3(25.15f,4,7.15f),new Vector3(25.15f,4,-7.15f));
                }
                else
                {
                    var src=AssetDatabase.LoadAllAssetsAtPath("Assets/BaseConsertada/Modelos/BaseConsertada.fbx").OfType<Mesh>().First(m=>m.name==name);int slot=Array.FindIndex(r.sharedMaterials,m=>m.name.Contains("Vidro"));
                    var points=src.GetTriangles(slot).Select(i=>r.transform.TransformPoint(src.vertices[i])).ToList();var hull=Hull(points,Vector3.right,Vector3.forward);
                    for(int i=0;i<hull.Count;i++){var a=hull[i];var b=hull[(i+1)%hull.Count];if((a.x+b.x)*.5f>-12.8f&&Mathf.Abs((a.z+b.z)*.5f)<2.5f)continue;Quad(new Vector3(a.x,2.2f,a.z),new Vector3(b.x,2.2f,b.z),new Vector3(b.x,3.2f,b.z),new Vector3(a.x,3.2f,a.z));}
                    var mesh=UnityEngine.Object.Instantiate(f.sharedMesh);int start=mesh.vertexCount;var uv=mesh.uv.Concat(Enumerable.Repeat(new Vector2(.5f,.5f),vertices.Count)).ToArray();mesh.vertices=mesh.vertices.Concat(vertices).ToArray();mesh.uv=uv;mesh.SetTriangles(tris.Select(i=>i+start).ToArray(),slot);var mats=r.sharedMaterials;mats[slot]=material;r.sharedMaterials=mats;mesh.RecalculateNormals();Assign(f,mesh,"Integro_"+name);continue;
                }
                var panel=new Mesh();panel.vertices=vertices.ToArray();panel.uv=Enumerable.Repeat(new Vector2(.5f,.5f),panel.vertexCount).ToArray();panel.triangles=tris.ToArray();panel.RecalculateNormals();r.sharedMaterials=new[]{material};Assign(f,panel,"Integro_"+name);
            }
        }
        static void Assign(MeshFilter f,Mesh mesh,string name){var vertices=mesh.vertices;var normals=mesh.normals;if(normals.Length!=vertices.Length)normals=new Vector3[vertices.Length];var tris=mesh.triangles;for(int i=0;i<tris.Length;i+=3){var n=Vector3.Cross(vertices[tris[i+1]]-vertices[tris[i]],vertices[tris[i+2]]-vertices[tris[i]]).normalized;for(int j=0;j<3;j++)if(normals[tris[i+j]].sqrMagnitude<.01f)normals[tris[i+j]]=n;}for(int i=0;i<normals.Length;i++)if(normals[i].sqrMagnitude<.01f)normals[i]=Vector3.up;mesh.normals=normals;mesh.RecalculateBounds();string path=Folder+name+".asset";var asset=AssetDatabase.LoadAssetAtPath<Mesh>(path);if(asset){asset.Clear();asset.indexFormat=mesh.indexFormat;asset.vertices=mesh.vertices;asset.normals=mesh.normals;asset.uv=mesh.uv;asset.subMeshCount=mesh.subMeshCount;for(int s=0;s<mesh.subMeshCount;s++)asset.SetTriangles(mesh.GetTriangles(s),s);asset.RecalculateBounds();UnityEngine.Object.DestroyImmediate(mesh);}else{asset=mesh;AssetDatabase.CreateAsset(asset,path);}f.sharedMesh=asset;var c=f.GetComponent<MeshCollider>();if(c){c.sharedMesh=null;c.sharedMesh=asset;}EditorUtility.SetDirty(asset);}
        static List<Vector3> Hull(List<Vector3> points,Vector3 u,Vector3 v)
        {
            points=points.OrderBy(p=>Vector3.Dot(p,u)).ThenBy(p=>Vector3.Dot(p,v)).ToList();var h=new List<Vector3>();
            float Cross(Vector3 a,Vector3 b,Vector3 c){var x=b-a;var y=c-a;return Vector3.Dot(x,u)*Vector3.Dot(y,v)-Vector3.Dot(x,v)*Vector3.Dot(y,u);}
            foreach(var p in points){while(h.Count>=2&&Cross(h[h.Count-2],h[h.Count-1],p)<=.00001f)h.RemoveAt(h.Count-1);h.Add(p);}int lower=h.Count;
            for(int i=points.Count-2;i>=0;i--){var p=points[i];while(h.Count>lower&&Cross(h[h.Count-2],h[h.Count-1],p)<=.00001f)h.RemoveAt(h.Count-1);h.Add(p);}if(h.Count>1)h.RemoveAt(h.Count-1);return h;
        }
    }
}
#endif





