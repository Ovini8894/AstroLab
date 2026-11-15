using System;
using UnityEngine;

namespace AstroLab.Centro
{
    // Trigger do campo lunar do ARGOS: dentro dele o JogadorFPS usa o perfil da Lua, ao sair volta ao da Terra.
    [RequireComponent(typeof(Collider))]
    public class ZonaGravidade : MonoBehaviour
    {
        public PerfilGravidade dentro = PerfilGravidade.Lua;
        public PerfilGravidade fora = PerfilGravidade.Terra;

        public bool JogadorDentro { get; private set; }
        public event Action<bool> AoMudar;

        // histerese: quem saiu do volume NO AR (topo do pulo, borda do campo) continua com gravidade lunar
        // até tocar o chão; só então volta para a da Terra.
        JogadorFPS saindo;
        CharacterController ccSaindo;

        void Reset() => GetComponent<Collider>().isTrigger = true;

        void OnTriggerEnter(Collider c)
        {
            var j = c.GetComponentInParent<JogadorFPS>();
            if (j == null) return;
            saindo = null;
            if (JogadorDentro) return;
            dentro.AplicarEm(j);
            JogadorDentro = true;
            AoMudar?.Invoke(true);
        }

        void OnTriggerExit(Collider c)
        {
            var j = c.GetComponentInParent<JogadorFPS>();
            if (j == null) return;
            saindo = j;
            ccSaindo = j.GetComponent<CharacterController>();
        }

        void Update()
        {
            if (saindo == null || (ccSaindo && ccSaindo.enabled && !ccSaindo.isGrounded)) return;
            fora.AplicarEm(saindo);
            saindo = null;
            JogadorDentro = false;
            AoMudar?.Invoke(false);
        }
    }
}
