using System;

// Tabela de todas as peças consertáveis da Base Lunar.
// Cada peça diz qual objeto consertado (OK_*) aparece e quais objetos quebrados da BaseLunar somem.
// Os objetos OK_* vêm de Assets/BaseConsertada/Modelos/BaseConsertada.fbx (gerado pelo script do Blender).
public static class PecasDaBase
{
    public static class Categoria
    {
        public const string Energia = "Energia";
        public const string SuporteVida = "SuporteVida";
        public const string Comunicacao = "Comunicacao";
        public const string Estrutura = "Estrutura";
        public const string Estufa = "Estufa";
        public const string Iluminacao = "Iluminacao";
        public const string Portas = "Portas";
        public const string Suprimentos = "Suprimentos";
    }

    public sealed class Peca
    {
        public readonly string id;          // ex.: "Gerador_O2"
        public readonly string categoria;   // ex.: Categoria.SuporteVida
        public readonly string objetoOk;    // ex.: "OK_Gerador_O2"
        public readonly string[] quebrados; // objetos da BaseLunar que somem quando consertar

        public Peca(string id, string categoria, params string[] quebrados)
        {
            this.id = id;
            this.categoria = categoria;
            objetoOk = "OK_" + id;
            this.quebrados = quebrados;
        }
    }

    public static readonly Peca[] Todas =
    {
        // ---------------- Energia: usina solar, inversores e baterias
        new Peca("Painel_R1_C2", Categoria.Energia, "REPAIR_Painel_R1_C2_CAIDO"),
        new Peca("Painel_R1_C3", Categoria.Energia, "REPAIR_Painel_R1_C3_QUEBRADO"),
        new Peca("Painel_R1_C6", Categoria.Energia, "REPAIR_Painel_R1_C6_CAIDO"),
        new Peca("Painel_R1_C8", Categoria.Energia, "REPAIR_Painel_R1_C8_SUPORTE_VAZIO"),
        new Peca("Painel_R2_C1", Categoria.Energia, "REPAIR_Painel_R2_C1_CAIDO"),
        new Peca("Painel_R2_C3", Categoria.Energia, "REPAIR_Painel_R2_C3_QUEBRADO"),
        new Peca("Painel_R2_C4", Categoria.Energia, "REPAIR_Painel_R2_C4_SUPORTE_VAZIO"),
        new Peca("Painel_R2_C7", Categoria.Energia, "REPAIR_Painel_R2_C7_SUPORTE_VAZIO"),
        new Peca("Painel_R2_C8", Categoria.Energia, "REPAIR_Painel_R2_C8_QUEBRADO"),
        new Peca("Painel_R2_C9", Categoria.Energia, "REPAIR_Painel_R2_C9_QUEBRADO"),
        new Peca("Painel_R3_C3", Categoria.Energia, "REPAIR_Painel_R3_C3_QUEBRADO"),
        new Peca("Painel_R3_C5", Categoria.Energia, "REPAIR_Painel_R3_C5_QUEBRADO"),
        new Peca("Painel_R3_C6", Categoria.Energia, "REPAIR_Painel_R3_C6_QUEBRADO"),
        new Peca("Painel_R3_C7", Categoria.Energia, "REPAIR_Painel_R3_C7_SUPORTE_VAZIO"),
        new Peca("Painel_R3_C8", Categoria.Energia, "REPAIR_Painel_R3_C8_QUEBRADO"),
        new Peca("Painel_R4_C1", Categoria.Energia, "REPAIR_Painel_R4_C1_CAIDO"),
        new Peca("Painel_R4_C3", Categoria.Energia, "REPAIR_Painel_R4_C3_QUEBRADO"),
        new Peca("Painel_R4_C4", Categoria.Energia, "REPAIR_Painel_R4_C4_CAIDO"),
        new Peca("Painel_R4_C7", Categoria.Energia, "REPAIR_Painel_R4_C7_QUEBRADO"),
        new Peca("Painel_R5_C7", Categoria.Energia, "REPAIR_Painel_R5_C7_CAIDO"),
        new Peca("Painel_R5_C9", Categoria.Energia, "REPAIR_Painel_R5_C9_QUEBRADO"),
        new Peca("Painel_R6_C5", Categoria.Energia, "REPAIR_Painel_R6_C5_SUPORTE_VAZIO"),
        new Peca("Painel_R6_C9", Categoria.Energia, "REPAIR_Painel_R6_C9_QUEBRADO"),
        new Peca("Inversor_1", Categoria.Energia, "REPAIR_Inversor_1"),
        new Peca("Inversor_2", Categoria.Energia, "REPAIR_Inversor_2"),
        new Peca("Inversor_3", Categoria.Energia, "REPAIR_Inversor_3"),
        new Peca("Baterias", Categoria.Energia, "REPAIR_Baterias"),

        // ---------------- Suporte de vida
        new Peca("Gerador_O2", Categoria.SuporteVida, "REPAIR_Gerador_O2"),
        new Peca("Cano_Tanque_O2", Categoria.SuporteVida, "REPAIR_Cano_Tanque_O2"),
        new Peca("Reciclador_H2O", Categoria.SuporteVida, "REPAIR_Reciclador_H2O"),

        // ---------------- Comunicação
        new Peca("Antena_Prato", Categoria.Comunicacao, "REPAIR_Antena_Prato", "Antena_Pedaco_Caido"),
        new Peca("Console_Controle", Categoria.Comunicacao, "REPAIR_Console_Controle"),

        // ---------------- Estrutura (o remendo da brecha só aparece, não esconde nada)
        new Peca("Brecha_Casco", Categoria.Estrutura),
        new Peca("Hub_Torre_Vidros", Categoria.Estrutura, "Hub_Torre_Vidros"),
        new Peca("SuporteVida_Casco", Categoria.Estrutura, "SuporteVida_Casco"),

        // ---------------- Estufa
        new Peca("Estufa_Painel", Categoria.Estufa, "REPAIR_Estufa_Painel"),
        new Peca("Estufa_Vidros", Categoria.Estufa, "Estufa_Vidros"),
        new Peca("Interior_Estufa", Categoria.Estufa, "Interior_Estufa"),

        // ---------------- Iluminação (luzes acesas)
        new Peca("Corredor_Estufa", Categoria.Iluminacao, "Corredor_Estufa"),
        new Peca("Corredor_SuporteVida", Categoria.Iluminacao, "Corredor_SuporteVida"),
        new Peca("Interior_Eclusa", Categoria.Iluminacao, "Interior_Eclusa"),
        new Peca("Interior_Hub", Categoria.Iluminacao, "Interior_Hub"),
        new Peca("Interior_SuporteVida", Categoria.Iluminacao, "Interior_SuporteVida"),
        new Peca("Hub_Mastros_Farol", Categoria.Iluminacao, "Hub_Mastros_Farol"),

        // ---------------- Portas
        new Peca("Porta_Eclusa_Externa", Categoria.Portas, "Porta_Eclusa_Externa_EMPERRADA"),
        new Peca("Porta_SuporteVida", Categoria.Portas, "Porta_SuporteVida_TRAVADA"),

        // ---------------- Suprimentos
        new Peca("Capsula_Suprimentos", Categoria.Suprimentos, "REPAIR_Capsula_Suprimentos"),
    };

    /// Aceita o id ("Gerador_O2"), o nome consertado ("OK_Gerador_O2") ou o nome de um objeto quebrado
    /// ("REPAIR_Gerador_O2", "Antena_Pedaco_Caido"...). Devolve null se não for uma peça consertável.
    public static Peca Encontrar(string nome)
    {
        if (string.IsNullOrEmpty(nome)) return null;
        foreach (var p in Todas)
        {
            if (p.id == nome || p.objetoOk == nome) return p;
            if (Array.IndexOf(p.quebrados, nome) >= 0) return p;
        }
        return null;
    }
}
