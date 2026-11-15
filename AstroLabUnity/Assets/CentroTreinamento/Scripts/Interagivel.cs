using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

namespace AstroLab.Centro
{
    // Ponto de interação ([E]). Não precisa de collider: o InteracaoJogador escolhe o mais próximo à frente do jogador.
    public class Interagivel : MonoBehaviour
    {
        public static readonly List<Interagivel> Todos = new List<Interagivel>();

        [Tooltip("Chave de localização da ação mostrada no prompt, ex.: treino.est2.acao")]
        public string chaveAcao = "ct.interagir";
        public float alcance = 2.6f;
        public bool ativo = true;
        public UnityEvent aoInteragir = new UnityEvent();
        public event Action<Interagivel> Interagiu;

        void OnEnable() => Todos.Add(this);
        void OnDisable() => Todos.Remove(this);

        public void Interagir()
        {
            if (!ativo) return;
            aoInteragir?.Invoke();
            Interagiu?.Invoke(this);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, alcance);
        }
    }
}
