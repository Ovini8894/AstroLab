using System.Collections;
using UnityEngine;

namespace AstroLab.Centro
{
    // Translação suave entre a posição inicial e um deslocamento (plataforma da NBL, porta de enrolar...).
    public class Deslizador : MonoBehaviour
    {
        public Vector3 deslocamento = new Vector3(0, -5.5f, 0);
        public float duracao = 4f;
        public bool Aberto { get; private set; }
        Vector3 inicio;
        Coroutine rotina;

        void Awake() => inicio = transform.localPosition;

        public void Abrir() => Mover(true);
        public void Fechar() => Mover(false);

        public void Mover(bool abrir)
        {
            if (Aberto == abrir) return;
            Aberto = abrir;
            if (rotina != null) StopCoroutine(rotina);
            rotina = StartCoroutine(Ir(abrir ? inicio + deslocamento : inicio));
        }

        IEnumerator Ir(Vector3 destino)
        {
            var de = transform.localPosition;
            for (float t = 0; t < duracao; t += Time.deltaTime)
            {
                transform.localPosition = Vector3.Lerp(de, destino, Mathf.SmoothStep(0, 1, t / duracao));
                yield return null;
            }
            transform.localPosition = destino;
        }
    }
}
