using System;
using System.Collections;
using UnityEngine;

namespace AstroLab.Centro
{
    public enum Vestimenta { MaosNuas, TrajeTreino, TrajeCompleto }

    // O que o jogador está vestindo no Centro de Treinamento (Terra):
    //   MaosNuas       spawn, recepção, médica, centrífuga, VR (macacão de voo azul, mãos nuas)
    //   TrajeTreino    luvas de treino nas estações que pedem traje (NBL, ARGOS); ao sair volta para MaosNuas
    //   TrajeCompleto  depois da Sala de Trajes, até entrar no foguete
    // A troca abaixa as mãos para fora da tela, troca o modelo e sobe de novo (sem "pipocar" no meio da tela).
    public class EstadoVestimentaJogador : MonoBehaviour
    {
        [Tooltip("Pai dos três conjuntos de mãos (filho da câmera). É ele que desce/sobe na troca.")]
        public Transform raizMaos;
        public GameObject maosNuas, luvasTreino, luvasTraje;
        public float descida = 0.45f;
        public float duracao = 0.28f;

        public Vestimenta Atual { get; private set; } = Vestimenta.MaosNuas;
        public bool Trocando { get; private set; }
        public event Action<Vestimenta> AoMudar;

        Vector3 posBase;

        void Awake()
        {
            if (raizMaos) posBase = raizMaos.localPosition;
            Mostrar(Atual);
        }

        public void Definir(Vestimenta v, bool instantaneo = false)
        {
            if (v == Atual && !instantaneo) return;
            StopAllCoroutines();
            if (instantaneo || !raizMaos || !isActiveAndEnabled)
            {
                Atual = v; Mostrar(v); Trocando = false;
                if (raizMaos) raizMaos.localPosition = posBase;
                AoMudar?.Invoke(v);
                return;
            }
            StartCoroutine(Trocar(v));
        }

        IEnumerator Trocar(Vestimenta v)
        {
            Trocando = true;
            var baixo = posBase + Vector3.down * descida;
            for (float t = 0; t < duracao; t += Time.deltaTime)
            {
                raizMaos.localPosition = Vector3.Lerp(posBase, baixo, Mathf.SmoothStep(0, 1, t / duracao));
                yield return null;
            }
            Atual = v;
            Mostrar(v);
            AoMudar?.Invoke(v);
            yield return new WaitForSeconds(0.12f);
            for (float t = 0; t < duracao; t += Time.deltaTime)
            {
                raizMaos.localPosition = Vector3.Lerp(baixo, posBase, Mathf.SmoothStep(0, 1, t / duracao));
                yield return null;
            }
            raizMaos.localPosition = posBase;
            Trocando = false;
        }

        void Mostrar(Vestimenta v)
        {
            if (maosNuas) maosNuas.SetActive(v == Vestimenta.MaosNuas);
            if (luvasTreino) luvasTreino.SetActive(v == Vestimenta.TrajeTreino);
            if (luvasTraje) luvasTraje.SetActive(v == Vestimenta.TrajeCompleto);
        }
    }
}
