using System;
using System.Collections;
using UnityEngine;

namespace AstroLab.Centro
{
    // Elevador da torre de serviço (ANIM_Elevador). O jogador entra na cabine e usa a botoeira [E]:
    // durante a viagem ele vai "preso" na cabine (CharacterController desligado) para não tremer nem cair.
    // Se o jogador voltar ao chão deixando a cabine lá em cima, ela desce sozinha.
    public class ElevadorTorre : MonoBehaviour
    {
        public Transform baseNivel;          // WP_Elevador_Base
        public Transform topoNivel;          // WP_Elevador_Topo
        public Collider zonaCabine;          // ZONA_Elevador (segue a cabine)
        public Interagivel botoeira;         // INT_Elevador (filho da cabine)
        public JogadorFPS jogador;
        public float duracao = 9f;

        public bool NoTopo { get; private set; }
        public bool Viajando { get; private set; }
        public event Action<bool> AoChegar;  // true = chegou no topo

        float yBase, yTopo;

        void Start()
        {
            yBase = transform.position.y;
            yTopo = yBase + (topoNivel.position.y - baseNivel.position.y);
            if (botoeira) botoeira.Interagiu += _ => Acionar();
        }

        bool JogadorNaCabine()
        {
            if (!jogador) return false;
            var p = jogador.transform.position;
            var c = transform.position;
            return Mathf.Abs(p.x - c.x) < 1.4f && Mathf.Abs(p.z - c.z) < 1.4f && p.y > c.y - 0.2f && p.y < c.y + 2.6f;
        }

        public void Acionar()
        {
            if (Viajando) return;
            StartCoroutine(Viajar(!NoTopo, JogadorNaCabine()));
        }

        IEnumerator Viajar(bool subir, bool levarJogador)
        {
            Viajando = true;
            if (botoeira) botoeira.ativo = false;
            CharacterController cc = null;
            Transform paiAntigo = null;
            if (levarJogador)
            {
                cc = jogador.GetComponent<CharacterController>();
                jogador.enabled = false;
                cc.enabled = false;
                paiAntigo = jogador.transform.parent;
                jogador.transform.SetParent(transform, true);
            }
            float de = transform.position.y, para = subir ? yTopo : yBase;
            yield return new WaitForSeconds(0.4f);
            for (float t = 0; t < duracao; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / duracao);
                var p = transform.position; p.y = Mathf.Lerp(de, para, k); transform.position = p;
                yield return null;
            }
            var pf = transform.position; pf.y = para; transform.position = pf;
            if (levarJogador)
            {
                jogador.transform.SetParent(paiAntigo, true);
                jogador.transform.rotation = Quaternion.Euler(0, jogador.transform.eulerAngles.y, 0);
                cc.enabled = true;
                jogador.enabled = true;
            }
            NoTopo = subir;
            Viajando = false;
            if (botoeira) botoeira.ativo = true;
            AoChegar?.Invoke(subir);
        }

        void Update()
        {
            // cabine esquecida lá em cima e o jogador de volta ao chão perto da torre: desce sozinha
            if (NoTopo && !Viajando && jogador && jogador.transform.position.y < yBase + 4f)
            {
                var d = jogador.transform.position - transform.position; d.y = 0;
                if (d.magnitude < 10f) StartCoroutine(Viajar(false, false));
            }
        }
    }
}
