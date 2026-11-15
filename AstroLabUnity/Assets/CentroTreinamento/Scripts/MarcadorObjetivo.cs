using System.Collections;
using UnityEngine;

namespace AstroLab.Centro
{
    // Losango flutuante que marca o painel do objetivo atual.
    public class MarcadorObjetivo : MonoBehaviour
    {
        Vector3 basePos;
        public void Colocar(Vector3 p) { basePos = p; transform.position = p; gameObject.SetActive(true); }
        void Update()
        {
            transform.position = basePos + Vector3.up * (Mathf.Sin(Time.time * 2.4f) * 0.12f);
            transform.Rotate(0, 90f * Time.deltaTime, 0, Space.World);
        }
    }
}
