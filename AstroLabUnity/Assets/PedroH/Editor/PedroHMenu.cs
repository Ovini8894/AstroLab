#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace AstroLab.PedroH.Editor
{
    /// <summary>
    /// Atalhos do menu superior "pedro-h" no Unity Editor para abrir a cena existente,
    /// localizar o controlador da viagem lunar e acessar os scripts criados em Assets/PedroH.
    /// </summary>
    public static class PedroHMenu
    {
        public const string CaminhoCenaNave = "Assets/Scenes/NaveEspacial.unity";
        public const string CaminhoPastaScripts = "Assets/PedroH/Scripts";
        public const string CaminhoDocumentacaoMd = "Assets/PedroH/Documentacao/ARQUITETURA_VIAGEM_FOGUETE.md";

        [MenuItem("pedro-h/1. Abrir Cena Existente (NaveEspacial)", false, 10)]
        public static void AbrirCenaViagemLunar()
        {
            if (!System.IO.File.Exists(CaminhoCenaNave))
            {
                EditorUtility.DisplayDialog(
                    "pedro-h · Cena não encontrada",
                    "A cena " + CaminhoCenaNave + " não foi encontrada no projeto.",
                    "OK");
                return;
            }

            if (SceneManager.GetActiveScene().path != CaminhoCenaNave)
            {
                if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())
                    return;
                EditorSceneManager.OpenScene(CaminhoCenaNave, OpenSceneMode.Single);
            }
        }

        [MenuItem("pedro-h/2. Selecionar Controlador da Viagem (ViagemLunarController)", false, 20)]
        public static void SelecionarControladorGameplay()
        {
            var ctrl = Object.FindAnyObjectByType<ViagemLunarController>();
            if (ctrl != null)
            {
                Selection.activeGameObject = ctrl.gameObject;
                EditorGUIUtility.PingObject(ctrl.gameObject);
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "pedro-h",
                    "Nenhum ViagemLunarController ativo foi encontrado na cena atual.",
                    "OK");
            }
        }

        [MenuItem("pedro-h/3. Acessar Pasta de Scripts (Assets/PedroH/Scripts)", false, 30)]
        public static void AcessarPastaScripts()
        {
            var pasta = AssetDatabase.LoadAssetAtPath<Object>(CaminhoPastaScripts);
            if (pasta != null)
            {
                Selection.activeObject = pasta;
                EditorGUIUtility.PingObject(pasta);
            }
        }

        [MenuItem("pedro-h/4. Abrir Documentação da Viagem Lunar (.md)", false, 40)]
        public static void AbrirDocumentacaoArquitetura()
        {
            var doc = AssetDatabase.LoadAssetAtPath<Object>(CaminhoDocumentacaoMd);
            if (doc != null)
            {
                Selection.activeObject = doc;
                EditorGUIUtility.PingObject(doc);
                AssetDatabase.OpenAsset(doc);
            }
            else if (System.IO.File.Exists(CaminhoDocumentacaoMd))
            {
                EditorUtility.OpenWithDefaultApp(CaminhoDocumentacaoMd);
            }
            else
            {
                EditorUtility.DisplayDialog(
                    "pedro-h · Documentação não encontrada",
                    "O arquivo " + CaminhoDocumentacaoMd + " não foi encontrado no projeto.",
                    "OK");
            }
        }
    }
}
#endif
