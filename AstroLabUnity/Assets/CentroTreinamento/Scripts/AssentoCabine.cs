using AstroLab.Settings;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AstroLab.Centro
{
    // Jogador sentado na cadeira do comandante, com o ônibus em pé na plataforma: deitado de costas,
    // olhando para o painel e as janelas (que apontam para o céu). Mouse olha em volta com limites.
    public class AssentoCabine : MonoBehaviour
    {
        public Transform olho;                // SEAT_CDR_Olho
        public float limiteYaw = 75f, limitePitchCima = 55f, limitePitchBaixo = 40f;

        public bool Sentado { get; private set; }

        JogadorFPS jogador;
        Transform cam;
        Quaternion baseRot;
        float yaw, pitch;

        public void Sentar(JogadorFPS j)
        {
            jogador = j;
            cam = j.GetComponentInChildren<Camera>().transform;
            j.enabled = false;
            var cc = j.GetComponent<CharacterController>(); if (cc) cc.enabled = false;
            var inter = j.GetComponent<InteracaoJogador>(); if (inter) inter.enabled = false;
            var apl = j.GetComponent<AplicadorConfiguracoesJogador>(); if (apl) apl.TravarCamera(true);
            // frente = céu (eixo do ônibus), cima = para o "teto" do orbitador
            baseRot = Quaternion.LookRotation(Vector3.up, Vector3.forward);
            j.transform.SetPositionAndRotation(olho.position - baseRot * cam.localPosition, baseRot);
            cam.localRotation = Quaternion.identity;
            yaw = pitch = 0;
            Sentado = true;
            Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false;
        }

        void LateUpdate()
        {
            if (!Sentado || cam == null) return;
            var m = Mouse.current;
            if (m != null && Cursor.lockState == CursorLockMode.Locked)
            {
                var d = m.delta.ReadValue() * AplicadorConfiguracoesJogador.SensibilidadeBase * GameSettings.MouseSensitivity;
                yaw = Mathf.Clamp(yaw + d.x, -limiteYaw, limiteYaw);
                pitch = Mathf.Clamp(pitch + (GameSettings.InvertY ? d.y : -d.y), -limitePitchCima, limitePitchBaixo);
            }
            cam.localRotation = Quaternion.Euler(pitch, yaw, 0);
        }
    }
}
