using System;
using System.Collections.Generic;
using UnityEngine;

namespace AstroLab.Centro
{
    // Instrutora: anda por uma lista de pontos, espera o jogador se ele ficar para trás, olha para ele quando perto.
    // Animação procedural nas peças do modelo (Perna_E/D, Braco_E/D, Cabeca, Corpo) — sem esqueleto.
    public class NpcInstrutor : MonoBehaviour
    {
        public Transform jogador;
        public float velocidade = 1.8f;
        public float distanciaEspera = 9f;       // se o jogador ficar mais longe que isso, ela para e espera
        public float alturaOlhos = 1.66f;
        public LayerMask chao = ~0;

        public bool Andando => caminho.Count > 0;
        public bool Falando { get; set; }

        Transform cabeca, corpo, bE, bD, pE, pD;
        Quaternion qCab, qBE, qBD, qPE, qPD;
        Vector3 posCorpo;
        readonly List<Vector3> caminho = new List<Vector3>();
        Action aoChegar;
        float fase, mistura, gesto;

        void Awake()
        {
            cabeca = Achar("Cabeca"); corpo = Achar("Corpo");
            bE = Achar("Braco_E"); bD = Achar("Braco_D"); pE = Achar("Perna_E"); pD = Achar("Perna_D");
            if (cabeca) qCab = cabeca.localRotation;
            if (bE) qBE = bE.localRotation; if (bD) qBD = bD.localRotation;
            if (pE) qPE = pE.localRotation; if (pD) qPD = pD.localRotation;
            if (corpo) posCorpo = corpo.localPosition;
        }

        Transform Achar(string n)
        {
            foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == n) return t;
            return null;
        }

        public void IrPara(IList<Vector3> pontos, Action fim = null)
        {
            caminho.Clear();
            caminho.AddRange(pontos);
            aoChegar = fim;
            if (caminho.Count == 0) { var f = aoChegar; aoChegar = null; f?.Invoke(); }
        }

        public void Teleportar(Vector3 pos, float yaw)
        {
            caminho.Clear();
            transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0));
            AjustarAoChao();
        }

        void Update()
        {
            float dt = Time.deltaTime;
            bool esperando = false;
            Vector3 alvoOlhar = transform.position + transform.forward * 3f + Vector3.up * alturaOlhos;
            float distJog = jogador ? Vector3.Distance(Plano(jogador.position), Plano(transform.position)) : 0f;

            if (caminho.Count > 0)
            {
                esperando = jogador && distJog > distanciaEspera;
                if (!esperando)
                {
                    var alvo = caminho[0];
                    var d = Plano(alvo) - Plano(transform.position);
                    float passo = velocidade * dt;
                    if (d.magnitude <= passo)
                    {
                        transform.position = new Vector3(alvo.x, transform.position.y, alvo.z);
                        caminho.RemoveAt(0);
                        if (caminho.Count == 0) { var f = aoChegar; aoChegar = null; f?.Invoke(); }
                    }
                    else
                    {
                        transform.position += d.normalized * passo;
                        Girar(d, dt, 7f);
                    }
                    AjustarAoChao();
                }
                else if (jogador) Girar(Plano(jogador.position) - Plano(transform.position), dt, 4f);
            }
            else if (jogador && (distJog < 7f || (Falando && distJog < 15f)))
                Girar(Plano(jogador.position) - Plano(transform.position), dt, Falando ? 8f : 3f);   // fala sempre de frente

            if (jogador && distJog < 8f) alvoOlhar = jogador.position + Vector3.up * 1.6f;

            // ---------------------------------------------------------- animação
            bool anda = caminho.Count > 0 && !esperando;
            mistura = Mathf.MoveTowards(mistura, anda ? 1f : 0f, dt * 4f);
            fase += dt * Mathf.Lerp(1.2f, velocidade * 4.2f, mistura);
            gesto = Mathf.MoveTowards(gesto, Falando ? 1f : 0f, dt * 3f);
            float s = Mathf.Sin(fase);
            float resp = Mathf.Sin(Time.time * 1.6f);
            if (pE) pE.localRotation = qPE * Quaternion.Euler(s * 26f * mistura, 0, 0);
            if (pD) pD.localRotation = qPD * Quaternion.Euler(-s * 26f * mistura, 0, 0);
            if (bE) bE.localRotation = qBE * Quaternion.Euler(-s * 20f * mistura + resp * 1.5f, 0, -3f);
            float gestoD = gesto * (-35f + Mathf.Sin(Time.time * 2.3f) * 12f);
            if (bD) bD.localRotation = qBD * Quaternion.Euler(s * 20f * mistura + resp * 1.5f + gestoD, 0, 3f + gesto * 6f);
            if (corpo) corpo.localPosition = posCorpo + Vector3.up * (Mathf.Abs(s) * 0.025f * mistura + resp * 0.004f);
            if (cabeca)
            {
                var dir = cabeca.parent.InverseTransformDirection((alvoOlhar - cabeca.position).normalized);
                float yaw = Mathf.Clamp(Mathf.Atan2(dir.x, dir.z) * Mathf.Rad2Deg, -55f, 55f);
                float pitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(dir.y, -1, 1)) * Mathf.Rad2Deg, -25f, 25f);
                float aceno = gesto * Mathf.Sin(Time.time * 4.1f) * 4f;
                var alvoQ = qCab * Quaternion.Euler(pitch + aceno, yaw, 0);
                cabeca.localRotation = Quaternion.Slerp(cabeca.localRotation, alvoQ, 1 - Mathf.Exp(-6f * dt));
            }
        }

        void Girar(Vector3 d, float dt, float rapidez)
        {
            if (d.sqrMagnitude < 1e-4f) return;
            var alvo = Quaternion.LookRotation(d.normalized, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, alvo, 1 - Mathf.Exp(-rapidez * dt));
        }

        void AjustarAoChao()
        {
            var o = transform.position + Vector3.up * 1.2f;
            if (Physics.Raycast(o, Vector3.down, out var hit, 3f, chao, QueryTriggerInteraction.Ignore))
                transform.position = new Vector3(transform.position.x, hit.point.y, transform.position.z);
        }

        static Vector3 Plano(Vector3 v) => new Vector3(v.x, 0, v.z);
    }
}
