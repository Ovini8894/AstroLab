using UnityEngine;
using UnityEngine.Events;

namespace AstroLab.Capacete
{
    public class AlvoExploracao : MonoBehaviour
    {
        public string titulo = "Sistema da base";
        [TextArea(2, 5)] public string descricao;
        [TextArea(2, 5)] public string charada;
        public string categoria = "EXPLORAÇÃO";
        public UnityEvent aoInspecionar = new UnityEvent();
    }
}
