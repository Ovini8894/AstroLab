using UnityEngine;

namespace AstroLab.Asteroids
{
    /// <summary>A deliberately illustrative UI shape, never represented as measured geometry.</summary>
    public sealed class AsteroidSilhouette : UnityEngine.UI.MaskableGraphic
    {
        protected override void OnPopulateMesh(UnityEngine.UI.VertexHelper helper)
        {
            helper.Clear(); Rect r = rectTransform.rect; const int count = 18;
            helper.AddVert(Vector3.zero, color, Vector2.zero);
            for (int i = 0; i < count; i++)
            {
                float a = i * Mathf.PI * 2 / count;
                float radius = .39f + .07f * Mathf.Sin(i * 2.3f);
                helper.AddVert(new Vector3(Mathf.Cos(a) * r.width * radius, Mathf.Sin(a) * r.height * radius),
                    new Color(color.r * (i % 3 == 0 ? .75f : 1), color.g, color.b, color.a), Vector2.zero);
                helper.AddTriangle(0, i + 1, (i + 1) % count + 1);
            }
        }
    }
}
