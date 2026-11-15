using System.Collections;
using UnityEngine;

namespace AstroLab.Centro
{
    // Ponte do ARGOS: acompanha o jogador quando ele está no campo lunar (o arnês fica sobre a cabeça dele).
    public class PonteArgos : MonoBehaviour
    {
        public Transform jogador;
        public ZonaGravidade zona;
        public Vector2 limitesX = new Vector2(-54f, -37f);   // em coordenadas de mundo (Unity)
        Vector3 inicio;

        void Start() => inicio = transform.position;

        void Update()
        {
            float alvoX = inicio.x + Mathf.Sin(Time.time * 0.2f) * 3f;
            if (zona && zona.JogadorDentro && jogador) alvoX = Mathf.Clamp(jogador.position.x, limitesX.x, limitesX.y);
            var p = transform.position;
            p.x = Mathf.MoveTowards(p.x, alvoX, Time.deltaTime * 2.2f);
            transform.position = p;
        }
    }
}
