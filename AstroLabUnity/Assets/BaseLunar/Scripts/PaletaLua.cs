using UnityEngine;

// Paleta única do estilo cartoon da Base Lunar (veja Assets/ArtBible.md, seção "Base Lunar").
// Materiais, texturas, luzes, céu e contorno leem as cores daqui.
public static class PaletaLua
{
    static Color Hex(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.magenta;

    // ---- superfícies
    public static readonly Color Casco = Hex("#F4E9D4");          // módulos: creme
    public static readonly Color CascoEscuro = Hex("#3E4C78");    // acabamentos, bases: índigo-ardósia
    public static readonly Color Piso = Hex("#7C8CB8");           // piso interno: azul-lavanda
    public static readonly Color Metal = Hex("#B4C2D9");          // estruturas, torres, canos
    public static readonly Color Faixa = Hex("#2CC6C0");          // faixa de identificação: turquesa
    public static readonly Color Amarelo = Hex("#FFC23A");        // sinalização: amarelo-gema
    public static readonly Color Vermelho = Hex("#E5484D");
    public static readonly Color Branco = Hex("#F8F5EE");
    public static readonly Color Tecido = Hex("#5568C9");
    public static readonly Color Regolito = Hex("#A9A5C0");       // chão da Lua: cinza-lilás (mais escuro que os módulos)
    public static readonly Color Rocha = Hex("#7F7A9E");
    public static readonly Color PainelSolar = Hex("#2E5BE0");
    public static readonly Color Vidro = Hex("#A8ECFF");
    public static readonly Color PlantaViva = Hex("#4CC35A");
    public static readonly Color PlantaMorta = Hex("#A0804F");
    public static readonly Color TerraVaso = Hex("#6E4C3D");
    public static readonly Color Enferrujado = Hex("#C46A35");
    public static readonly Color Queimado = Hex("#4B4250");
    public static readonly Color Agua = Hex("#3AA7F0");
    public static readonly Color Planeta = Hex("#3D8BFF");        // a Terra no céu

    // ---- luzes / brilhos
    public static readonly Color LuzLaranja = Hex("#FF8C3A");     // destaque: tangerina
    public static readonly Color LuzAmbar = Hex("#FFB45C");
    public static readonly Color LuzBranca = Hex("#FFF4DE");
    public static readonly Color LuzVerde = Hex("#59FF86");
    public static readonly Color Tela = Hex("#47E6FF");
    public static readonly Color Sol = Hex("#FFF1D6");            // luz principal quente
    public static readonly Color LuzTerra = Hex("#9DB8FF");       // preenchimento frio

    // ---- estilo
    public static readonly Color Sombra = Hex("#5E5A9E");         // faixa de sombra (multiplica o albedo × sol)
    public static readonly Color MeioTom = Hex("#C4C2E6");        // faixa intermediária
    public static readonly Color AmbienteCeu = Hex("#4A4F8C");    // ambiente vindo de cima
    public static readonly Color AmbienteChao = Hex("#3A3040");   // ambiente vindo de baixo
    public static readonly Color Rim = Hex("#BFD4FF");            // borda iluminada
    public static readonly Color Contorno = Hex("#1C1A3D");       // linhas: índigo bem escuro (nunca preto)
    public static readonly Color CeuTopo = Hex("#0F1236");
    public static readonly Color CeuHorizonte = Hex("#2E2766");
}
