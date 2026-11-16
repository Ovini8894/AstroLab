using System;

namespace AstroLab.Nicholas.Core
{
    /// <summary>
    /// Trilha didática fictícia sobre objetos históricos reais da Apollo 15.
    /// Marcadores próximos na cena são réplicas digitais, NÃO coordenadas reais.
    /// </summary>
    public static class Apollo15Trail
    {
        [Serializable]
        public sealed class Artifact
        {
            public ArtifactId Id;
            public string Name;
            public string NasaQuery;
            public string History;
            public string ClueToNext;
            public string OfficialSource;
            public Artifact(ArtifactId id, string name, string query, string history, string clue, string source)
            {
                Id = id; Name = name; NasaQuery = query; History = history;
                ClueToNext = clue; OfficialSource = source;
            }
        }

        public static readonly Artifact[] Items =
        {
            new Artifact(ArtifactId.Hammer, "Martelo geológico", "Apollo 15 geology hammer",
                "David Scott usou um martelo geológico em uma demonstração de queda livre na Lua, em 1971.",
                "Sou leve e vim de uma ave. Caí junto com este martelo na Lua. Qual é o próximo objeto?",
                "https://science.nasa.gov/resource/the-apollo-15-hammer-feather-drop/"),
            new Artifact(ArtifactId.Feather, "Pena de falcão", "Apollo 15 hammer feather drop",
                "Uma pena de falcão e um martelo caíram simultaneamente em quase vácuo, evidenciando a aceleração gravitacional independente da massa.",
                "Tenho quatro rodas e levei astronautas para explorar a Lua. Que veículo sou eu?",
                "https://nssdc.gsfc.nasa.gov/planetary/lunar/apollo_15_feather_drop.html"),
            new Artifact(ArtifactId.Rover, "Veículo Lunar Apollo 15 (LRV)", "Apollo 15 lunar roving vehicle",
                "O Lunar Roving Vehicle ampliou a distância que os astronautas podiam percorrer durante a exploração científica.",
                "Quatro rodas me levaram longe. O próximo devolve luz à Terra para medir uma distância enorme.",
                "https://www.nasa.gov/history/50-years-ago-apollo-15-on-the-moon-at-hadley-apennine/"),
            new Artifact(ArtifactId.Retroreflector, "Retrorefletor laser", "Apollo 15 laser ranging retroreflector",
                "A Apollo 15 instalou um arranjo de refletores usado para medir com precisão a distância entre Terra e Lua.",
                "Para voltar, os astronautas decolaram, mas parte da nave permaneceu sobre quatro pernas.",
                "https://www.nasa.gov/history/50-years-ago-three-weeks-until-launch-of-apollo-15/"),
            new Artifact(ArtifactId.FalconDescentStage, "Estágio de descida Falcon", "Apollo 15 Falcon lunar module descent stage",
                "O estágio inferior do módulo lunar Falcon permaneceu na Lua. A cabine fazia parte do estágio superior, que decolou.",
                "Coleção Apollo 15 concluída. Objetos preservados; missão científica registrada.",
                "https://www.nasa.gov/wp-content/uploads/2024/02/final-catalogue-of-manmade-material-on-the-moon.pdf")
        };

        public static Artifact Find(ArtifactId id)
        {
            for (int i = 0; i < Items.Length; i++) if (Items[i].Id == id) return Items[i];
            throw new ArgumentOutOfRangeException(nameof(id));
        }
    }
}
