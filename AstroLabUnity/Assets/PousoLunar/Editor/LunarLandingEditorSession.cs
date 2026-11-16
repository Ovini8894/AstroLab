using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AstroLab.PousoLunar.Editor
{
    /// <summary>Comandos tipados locais para a sessão já aberta. Nunca executa código arbitrário.</summary>
    [InitializeOnLoad]
    public static class LunarLandingEditorSession
    {
        const string Folder="Assets/PousoLunar/Documentacao/";
        [Serializable] sealed class Request { public string id=""; public string action=""; }
        static double nextPoll;
        static string lastId;
        static LunarLandingEditorSession() { EditorApplication.update+=Poll; }
        static void Poll()
        {
            if(EditorApplication.timeSinceStartup<nextPoll || EditorApplication.isCompiling || EditorApplication.isUpdating) return;
            nextPoll=EditorApplication.timeSinceStartup+.5;
            string file=Folder+"editor-request.json";
            if(!File.Exists(file)) return;
            var request=JsonUtility.FromJson<Request>(File.ReadAllText(file));
            if(request==null || string.IsNullOrEmpty(request.id) || request.id==lastId) return;
            lastId=request.id;
            try
            {
                string result;
                if(request.action=="status") result=Status();
                else if(request.action=="setup")
                { LunarLandingPresentationEditor.Setup(); result="Apresentação e ambiente configurados."; }
                else if(request.action=="validate")
                { LunarLandingFinalValidation.Run(); result="Validação em Play solicitada."; }
                else throw new InvalidOperationException("Comando local desconhecido: "+request.action);
                File.WriteAllText(Folder+"editor-response.json",JsonUtility.ToJson(new Response {id=request.id,success=true,result=result},true));
            }
            catch(Exception e)
            {
                File.WriteAllText(Folder+"editor-response.json",JsonUtility.ToJson(new Response {id=request.id,success=false,result=e.ToString()},true));
            }
        }
        static string Status()
        {
            var scenes=Enumerable.Range(0,SceneManager.sceneCount).Select(i=>SceneManager.GetSceneAt(i))
                .Select(s=>s.path+" dirty="+s.isDirty);
            return "playing="+EditorApplication.isPlaying+" paused="+EditorApplication.isPaused+
                " runInBackground="+Application.runInBackground+" frame="+Time.frameCount+
                " scenes="+string.Join(";",scenes);
        }
        [Serializable] sealed class Response { public string id; public bool success; public string result; }
    }
}
