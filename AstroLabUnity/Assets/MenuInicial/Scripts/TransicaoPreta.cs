using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace AstroLab.Menu
{
    /// <summary>
    /// Tela preta que atravessa a troca de cena (DontDestroyOnLoad): cobre tudo enquanto a cena nova carrega e
    /// some com fade quando ela estiver ativa. Criada pelo menu no Iniciar.
    /// </summary>
    public sealed class TransicaoPreta : MonoBehaviour
    {
        Image img;
        float duracaoSaida;

        public static TransicaoPreta Criar(float duracaoSaida)
        {
            var go = new GameObject("[AstroLab] Transição Preta", typeof(Canvas), typeof(CanvasGroup));
            DontDestroyOnLoad(go);
            var c = go.GetComponent<Canvas>();
            c.renderMode = RenderMode.ScreenSpaceOverlay;
            c.sortingOrder = 32000;
            var imgGo = new GameObject("Preto", typeof(RectTransform), typeof(Image));
            imgGo.transform.SetParent(go.transform, false);
            var rt = (RectTransform)imgGo.transform;
            rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero;
            var t = go.AddComponent<TransicaoPreta>();
            t.img = imgGo.GetComponent<Image>();
            t.img.color = Color.black;
            t.duracaoSaida = duracaoSaida;
            return t;
        }

        void OnEnable() => SceneManager.activeSceneChanged += AoTrocar;
        void OnDisable() => SceneManager.activeSceneChanged -= AoTrocar;

        void AoTrocar(Scene de, Scene para) => StartCoroutine(Sumir());

        IEnumerator Sumir()
        {
            yield return null;                                   // um quadro para a cena nova montar câmera/UI
            yield return null;
            for (float t = 0; t < duracaoSaida; t += Time.unscaledDeltaTime)
            {
                img.color = new Color(0f, 0f, 0f, 1f - Mathf.SmoothStep(0f, 1f, t / duracaoSaida));
                yield return null;
            }
            Destroy(gameObject);
        }
    }
}
