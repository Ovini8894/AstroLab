using System.Collections;
using UnityEngine;

namespace AstroLab.Centro
{
    // Braço da centrífuga: gira em torno do eixo vertical com rampa de aceleração.
    public class Centrifuga : MonoBehaviour
    {
        public float rpmMaximo = 24f;
        public float rpmOcioso = 0f;
        public float Rpm { get; private set; }
        float alvo;

        void Start() => alvo = rpmOcioso;
        public void Ligar(float rpm) => alvo = rpm;
        public void Desligar() => alvo = rpmOcioso;
        public float ForcaG => 1f + Mathf.Pow(Rpm / Mathf.Max(1f, rpmMaximo), 2f) * 3f;   // 1 G parado, ~4 G no máximo

        void Update()
        {
            Rpm = Mathf.MoveTowards(Rpm, alvo, Time.deltaTime * 4f);
            transform.Rotate(0f, Rpm * 6f * Time.deltaTime, 0f, Space.World);
        }
    }
}
