using UnityEngine;

namespace AstroLab.Menu
{
    /// <summary>
    /// Vida no céu do menu: gira o skybox bem devagar (cópia em memória do material, o asset não é alterado)
    /// e, de vez em quando, risca um meteoro discreto. As estrelas não piscam (não há atmosfera na Lua).
    /// </summary>
    public sealed class CeuMenu : MonoBehaviour
    {
        [Header("Rotação do céu")]
        public float grausPorSegundo = 0.12f;

        [Header("Meteoros")]
        public Camera cameraMenu;
        public TrailRenderer meteoro;
        public Vector2 intervalo = new Vector2(18f, 36f);
        public float distancia = 900f;

        Material ceu;
        float rotacao0;
        float proximo;
        float tVoo = -1f, duracao;
        Vector3 de, para;

        void Start()
        {
            if (RenderSettings.skybox != null)
            {
                ceu = new Material(RenderSettings.skybox) { name = RenderSettings.skybox.name + " (menu)" };
                RenderSettings.skybox = ceu;
                if (ceu.HasProperty("_Rotation")) rotacao0 = ceu.GetFloat("_Rotation");
            }
            if (meteoro) { meteoro.emitting = false; meteoro.gameObject.SetActive(false); }
            proximo = Time.unscaledTime + Random.Range(6f, 12f);       // o primeiro aparece logo
        }

        void OnDestroy() { if (ceu) Destroy(ceu); }

        void Update()
        {
            if (ceu && ceu.HasProperty("_Rotation"))
                ceu.SetFloat("_Rotation", Mathf.Repeat(rotacao0 + Time.unscaledTime * grausPorSegundo, 360f));

            if (!meteoro || !cameraMenu) return;
            if (tVoo < 0f && Time.unscaledTime >= proximo) Lancar();
            if (tVoo >= 0f)
            {
                tVoo += Time.unscaledDeltaTime;
                float p = Mathf.Clamp01(tVoo / duracao);
                meteoro.transform.position = Vector3.Lerp(de, para, p);
                meteoro.widthMultiplier = 2.2f * Mathf.Sin(p * Mathf.PI);
                if (p >= 1f)
                {
                    meteoro.emitting = false;
                    if (tVoo > duracao + meteoro.time) { tVoo = -1f; meteoro.gameObject.SetActive(false); proximo = Time.unscaledTime + Random.Range(intervalo.x, intervalo.y); }
                }
            }
        }

        void Lancar()
        {
            // nasce no alto da tela, longe do título (laterais), e desce em diagonal
            bool esquerda = Random.value < 0.5f;
            var v0 = new Vector3(esquerda ? Random.Range(0.05f, 0.3f) : Random.Range(0.7f, 0.95f), Random.Range(0.72f, 0.95f), distancia);
            var dir = new Vector3(esquerda ? Random.Range(0.12f, 0.25f) : -Random.Range(0.12f, 0.25f), -Random.Range(0.08f, 0.16f), 0f);
            de = cameraMenu.ViewportToWorldPoint(v0);
            para = cameraMenu.ViewportToWorldPoint(v0 + dir);
            duracao = Random.Range(0.55f, 0.9f);
            tVoo = 0f;
            meteoro.gameObject.SetActive(true);
            meteoro.transform.position = de;
            meteoro.Clear();
            meteoro.emitting = true;
        }
    }
}
