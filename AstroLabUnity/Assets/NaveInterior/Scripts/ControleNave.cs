using System;
using System.Collections.Generic;
using AstroLab.Localization;
using UnityEngine;

namespace AstroLab.Nave
{
    public enum TipoControle { Chave, Botao }

    // Controle interativo do cockpit (ANIM_<id>): base fixa + parte movel (alavanca ou capa), luz de estado,
    // realce ciano quando o jogador mira e animacao de pressionar. O efeito no sistema fica em SistemasNave.
    [DisallowMultipleComponent]
    public class ControleNave : MonoBehaviour
    {
        public string id;
        public string chaveLoc;                 // nave.<id>
        public TipoControle tipo;
        public Transform movel;
        public Renderer[] pecas;                // recebem o realce
        public Renderer luzEstado;              // lente do botao ou lampada ao lado da chave
        public Color corEstado = new Color(0.35f, 1f, 0.53f);
        public bool ligado;

        public static event Action<ControleNave> OnAcionado;
        public static readonly List<ControleNave> Todos = new List<ControleNave>();

        public string Nome => Loc.Get(chaveLoc);

        static readonly int Emissao = Shader.PropertyToID("_EmissionColor");
        static readonly Color Realce = new Color(0.22f, 0.84f, 0.82f);     // ciano do ArtBible (#37D5D0)
        Quaternion rotLigado, rotDesligado, rotDe;
        Vector3 pos0;
        float t = 1f;
        bool realcado;
        MaterialPropertyBlock mpb;

        void Awake()
        {
            mpb = new MaterialPropertyBlock();
            if (movel)
            {
                rotLigado = movel.localRotation;                      // o gerador posiciona a alavanca "para cima"
                rotDesligado = Quaternion.Inverse(rotLigado);         // mesmo eixo, angulo oposto
                pos0 = movel.localPosition;
            }
            Pose(1f);
            Pintar();
        }

        void OnEnable() => Todos.Add(this);
        void OnDisable() => Todos.Remove(this);

        /// <summary>Clique do jogador: inverte o estado, anima, toca o som e avisa os sistemas.</summary>
        public void Acionar()
        {
            rotDe = movel ? movel.localRotation : Quaternion.identity;
            ligado = !ligado;
            t = 0f;
            SomNave.Clique(transform.position, tipo);
            Pintar();
            OnAcionado?.Invoke(this);
        }

        /// <summary>Define o estado sem disparar eventos (estado inicial da fase).</summary>
        public void Definir(bool valor)
        {
            ligado = valor;
            t = 1f;
            Pose(1f);
            Pintar();
        }

        public void Realcar(bool v)
        {
            if (realcado == v) return;
            realcado = v;
            Pintar();
        }

        void Update()
        {
            if (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / (tipo == TipoControle.Chave ? 0.12f : 0.18f));
                Pose(t);
            }
            if (realcado) Pintar();
        }

        void Pose(float k)
        {
            if (!movel) return;
            if (tipo == TipoControle.Chave)
            {
                var alvo = ligado ? rotLigado : rotDesligado;
                float e = 1f - Mathf.Pow(1f - k, 3f);                 // estalo rapido no fim do curso
                movel.localRotation = k >= 1f ? alvo : Quaternion.SlerpUnclamped(rotDe, alvo, e * 1.06f - 0.06f * e * e);
            }
            else
            {
                float afunda = Mathf.Sin(Mathf.PI * Mathf.Clamp01(k)) * 0.0028f + (ligado ? 0.0008f : 0f);
                movel.localPosition = pos0 + Vector3.down * afunda;   // +Y local = normal do painel
            }
        }

        void Pintar()
        {
            mpb ??= new MaterialPropertyBlock();
            float pulso = realcado ? 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 6f) : 0f;
            foreach (var r in pecas)
            {
                if (!r) continue;
                var c = realcado ? Realce * (0.6f * pulso + 0.25f) : Color.black;
                if (r == luzEstado) c += EstadoCor();
                r.GetPropertyBlock(mpb);
                mpb.SetColor(Emissao, c);
                r.SetPropertyBlock(mpb);
            }
            if (luzEstado && Array.IndexOf(pecas, luzEstado) < 0)
            {
                luzEstado.GetPropertyBlock(mpb);
                mpb.SetColor(Emissao, EstadoCor());
                luzEstado.SetPropertyBlock(mpb);
            }
        }

        Color EstadoCor() => ligado ? corEstado.linear * 2.6f : corEstado.linear * 0.06f;
    }
}
