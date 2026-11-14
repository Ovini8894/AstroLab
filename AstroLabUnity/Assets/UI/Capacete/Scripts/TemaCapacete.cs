using UnityEngine;

namespace AstroLab.Capacete
{
    public static class TemaCapacete
    {
        public static readonly Color Ciano = Hex("37D5D0");
        public static readonly Color Texto = Hex("B9D2E8");
        public static readonly Color Secundario = Hex("5D738A");
        public static readonly Color Fundo = Hex("161D2B");
        public static readonly Color Metal = Hex("2D3545");
        public static readonly Color Alerta = Hex("F2B95C");

        public static Color Hex(string hex) => ColorUtility.TryParseHtmlString("#" + hex, out var c) ? c : Color.clear;
        public static Color Alpha(Color color, float alpha) => new Color(color.r, color.g, color.b, alpha);
    }
}
