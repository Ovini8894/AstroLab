using UnityEngine;
namespace AstroLab.Gameplay
{
    /// <summary>Original authoring data retained so spherical scene setup is repeatable.</summary>
    public sealed class LunarPlacement : MonoBehaviour
    {
        public Mesh sourceMesh;
        public Matrix4x4 sourceMatrix;
        public bool recorded;
    }
}
