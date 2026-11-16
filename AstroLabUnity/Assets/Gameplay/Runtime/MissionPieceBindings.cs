using System;
using System.Collections.Generic;
using System.Linq;
using AstroLab.Nicholas.Core;

namespace AstroLab.Gameplay
{
    /// <summary>Shared by scene setup and runtime restoration so every modelled piece belongs to one repair.</summary>
    public static class MissionPieceBindings
    {
        public static EquipmentId EquipmentFor(PecasDaBase.Peca piece)
        {
            if (piece == null) throw new ArgumentNullException(nameof(piece));
            switch (piece.categoria)
            {
                case PecasDaBase.Categoria.Energia:
                    return piece.id == "Baterias" ? EquipmentId.SuitCharger : EquipmentId.SolarPanels;
                case PecasDaBase.Categoria.SuporteVida:
                    return piece.id == "Reciclador_H2O" ? EquipmentId.WaterRecycler : EquipmentId.LifeSupport;
                case PecasDaBase.Categoria.Comunicacao:
                case PecasDaBase.Categoria.Suprimentos:
                    return EquipmentId.Communications;
                case PecasDaBase.Categoria.Estufa:
                    return EquipmentId.Greenhouse;
                case PecasDaBase.Categoria.Portas:
                    return piece.id == "Porta_SuporteVida" ? EquipmentId.LifeSupport : EquipmentId.RadiationShelter;
                case PecasDaBase.Categoria.Estrutura:
                    return piece.id == "SuporteVida_Casco" ? EquipmentId.LifeSupport : EquipmentId.RadiationShelter;
                case PecasDaBase.Categoria.Iluminacao:
                    if (piece.id == "Corredor_Estufa") return EquipmentId.Greenhouse;
                    if (piece.id == "Corredor_SuporteVida" || piece.id == "Interior_SuporteVida") return EquipmentId.LifeSupport;
                    return EquipmentId.RadiationShelter;
                default:
                    throw new InvalidOperationException("Peça sem grupo de manutenção: " + piece.id);
            }
        }

        public static bool IsArchitecture(PecasDaBase.Peca piece) => piece != null &&
            (piece.categoria == PecasDaBase.Categoria.Estrutura ||
             piece.categoria == PecasDaBase.Categoria.Iluminacao ||
             piece.categoria == PecasDaBase.Categoria.Portas ||
             piece.id == "Estufa_Vidros" || piece.id == "Interior_Estufa");

        public static Dictionary<EquipmentId, string[]> CreateMap() => PecasDaBase.Todas
            .GroupBy(EquipmentFor).ToDictionary(group => group.Key, group => group.Select(p => p.id).ToArray());

        public static bool TryGetEquipment(string name, out EquipmentId equipment)
        {
            var piece = PecasDaBase.Encontrar(name);
            equipment = piece != null ? EquipmentFor(piece) : default;
            return piece != null;
        }
    }
}
