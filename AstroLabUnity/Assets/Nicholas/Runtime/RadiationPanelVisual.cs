using UnityEngine;

namespace AstroLab.Nicholas
{
    /// <summary>
    /// Controla o feedback visual do painel do Abrigo de Radiação (vermelho avariado -> ciano operacional).
    /// </summary>
    public sealed class RadiationPanelVisual : MonoBehaviour
    {
        [SerializeField] private Renderer indicatorRenderer;
        [SerializeField] private Material brokenMaterial;
        [SerializeField] private Material repairedMaterial;

        private MaterialPropertyBlock propertyBlock;

        public void Configure(Renderer indicator, Material brokenMat, Material repairedMat)
        {
            indicatorRenderer = indicator;
            brokenMaterial = brokenMat;
            repairedMaterial = repairedMat;
        }

        public void SetRepaired(bool repaired)
        {
            if (!indicatorRenderer) return;
            Material targetMat = repaired ? repairedMaterial : brokenMaterial;
            if (targetMat != null)
            {
                indicatorRenderer.sharedMaterial = targetMat;
                indicatorRenderer.SetPropertyBlock(null);
            }
            else
            {
                if (propertyBlock == null) propertyBlock = new MaterialPropertyBlock();
                Color fallback = repaired ? new Color(0.15f, 0.90f, 0.95f) : new Color(0.90f, 0.18f, 0.15f);
                propertyBlock.SetColor("_BaseColor", fallback);
                propertyBlock.SetColor("_Color", fallback);
                indicatorRenderer.SetPropertyBlock(propertyBlock);
            }
        }
    }
}
