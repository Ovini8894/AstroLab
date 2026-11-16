using System.Collections;
using UnityEngine;

namespace AstroLab.Centro
{
    // Portas de vidro da entrada: abrem sozinhas quando o jogador (ou a instrutora) chega perto.
    public class PortaAutomatica : MonoBehaviour
    {
        public Transform[] folhas;
        public Vector3[] aberturas;           // deslocamento local de cada folha
        public Transform[] quemAbre;
        public float raio = 3.5f;
        public float velocidade = 2.5f;
        public Collider bloqueio;             // colisor que some quando a porta está aberta
        Vector3[] inicio;
        float aberto;

        void Start()
        {
            inicio = new Vector3[folhas.Length];
            for (int i = 0; i < folhas.Length; i++) inicio[i] = folhas[i].localPosition;
        }

        void Update()
        {
            bool perto = false;
            foreach (var q in quemAbre)
                if (q && (q.position - transform.position).sqrMagnitude < raio * raio) perto = true;
            aberto = Mathf.MoveTowards(aberto, perto ? 1f : 0f, Time.deltaTime * velocidade);
            float e = Mathf.SmoothStep(0, 1, aberto);
            for (int i = 0; i < folhas.Length; i++) folhas[i].localPosition = inicio[i] + aberturas[i] * e;
            if (bloqueio) bloqueio.enabled = aberto < 0.6f;
        }
    }
}
