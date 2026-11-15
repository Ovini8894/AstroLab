using AstroLab.Nicholas.Core;
using UnityEngine;

namespace AstroLab.Nicholas
{
    public sealed class MissionHistoricalTarget : MonoBehaviour
    {
        [SerializeField] private ArtifactId artifact;
        public ArtifactId Artifact { get { return artifact; } }
        public void Configure(ArtifactId id) { artifact = id; }
        public string Label { get { return "Registro didático — " + Apollo15Trail.Find(artifact).Name; } }
    }
}
