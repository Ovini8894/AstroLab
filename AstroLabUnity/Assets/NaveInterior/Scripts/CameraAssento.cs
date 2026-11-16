using AstroLab.Localization;
using AstroLab.Settings;
using UnityEngine;
using UnityEngine.InputSystem;

namespace AstroLab.Nave
{
    // Camera em 1a pessoa presa ao assento: gira so a cabeca, com limites medidos no referencial do assento
    // (o pai). Assim funciona igual com a nave em pe na plataforma ou deitada em orbita.
    public class CameraAssento : MonoBehaviour
    {
        public Vector2 limiteGiro = new Vector2(-115f, 115f);     // esquerda/direita
        public Vector2 limiteInclina = new Vector2(-60f, 80f);    // baixo/cima
        public float sensibilidadeBase = 0.1f;                    // graus por pixel (x GameSettings.MouseSensitivity)
        public float giro, inclina = -14f;
        [Header("Postos (Tab troca): assento do CDR, do PLT e estacao de re")]
        public Transform[] postos;               // marcas irmas do rig (mesmo pai): o rig vai ate elas
        public string[] chavesPosto;             // nave.posto.*

        Camera cam;
        int posto;
        float troca = 1f;
        Vector3 dePos;
        Quaternion deRot;

        void Awake() => cam = GetComponent<Camera>();

        void OnEnable()
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        void Update()
        {
            var m = Mouse.current;
            var kb = Keyboard.current;
            if (kb != null && kb.escapeKey.wasPressedThisFrame)
            {
                bool solto = Cursor.lockState == CursorLockMode.Locked;
                Cursor.lockState = solto ? CursorLockMode.None : CursorLockMode.Locked;
                Cursor.visible = solto;
            }
            var rig = transform.parent;
            if (kb != null && kb.tabKey.wasPressedThisFrame && rig && postos != null && postos.Length > 1)
            {
                dePos = rig.localPosition; deRot = rig.localRotation;
                posto = (posto + 1) % postos.Length;
                troca = 0f; giro = 0f; inclina = -10f;
                var sis = FindFirstObjectByType<SistemasNave>();
                if (sis && chavesPosto != null && posto < chavesPosto.Length) sis.Legenda(chavesPosto[posto]);
            }
            if (troca < 1f && rig)
            {
                troca = Mathf.Min(1f, troca + Time.deltaTime / 0.7f);
                float k = Mathf.SmoothStep(0f, 1f, troca);
                rig.localPosition = Vector3.Lerp(dePos, postos[posto].localPosition, k);
                rig.localRotation = Quaternion.Slerp(deRot, postos[posto].localRotation, k);
            }
            if (m != null && Cursor.lockState == CursorLockMode.Locked)
            {
                var d = m.delta.ReadValue() * sensibilidadeBase * GameSettings.MouseSensitivity;
                giro = Mathf.Clamp(giro + d.x, limiteGiro.x, limiteGiro.y);
                inclina = Mathf.Clamp(inclina + (GameSettings.InvertY ? -d.y : d.y), limiteInclina.x, limiteInclina.y);
            }
            transform.localRotation = Quaternion.Euler(-inclina, giro, 0f);
            if (cam) cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, GameSettings.FieldOfView, 0.2f);
        }
    }
}
