using System.Collections;
using UnityEngine;

namespace AstroLab.Centro
{
    // Ponte do ARGOS: acompanha o jogador quando ele está no campo lunar (o arnês fica sobre a cabeça dele).
    public class PonteArgos : MonoBehaviour
    {
        public Transform jogador;
        public ZonaGravidade zona;
        public float meioCurso = 8.5f;                           // metros para cada lado, no espaço local da sala
        Vector3 inicio;

        void Start() => inicio = transform.localPosition;

        void Update()
        {
            var pai = transform.parent;
            float alvoX = inicio.x + Mathf.Sin(Time.time * 0.2f) * 3f;
            if (zona && zona.JogadorDentro && jogador && pai)
                alvoX = Mathf.Clamp(pai.InverseTransformPoint(jogador.position).x, inicio.x - meioCurso, inicio.x + meioCurso);
            var p = transform.localPosition;
            p.x = Mathf.MoveTowards(p.x, alvoX, Time.deltaTime * 2.2f);
            transform.localPosition = p;
        }
    }
}
