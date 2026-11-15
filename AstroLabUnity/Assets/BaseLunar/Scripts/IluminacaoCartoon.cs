using UnityEngine;

// Liga o estilo cartoon na cena: cores das faixas de sombra, ambiente em hemisfério, borda iluminada e
// contorno. Fica num objeto da cena (criado pelo menu Missão Lunar > Estilo Cartoon > Aplicar iluminação).
// Sem este componente na cena, o contorno fica desligado e o shader Toon usa valores padrão.
[ExecuteAlways]
public class IluminacaoCartoon : MonoBehaviour
{
    [Header("Faixas do cel shading")]
    public Color sombra = PaletaLua.Sombra;
    public Color meioTom = PaletaLua.MeioTom;
    [Range(-1f, 1f)] public float limiteMeioTom = 0.38f;
    [Range(-1f, 1f)] public float limiteSombra = 0.02f;
    [Range(0.001f, 0.15f)] public float suavidade = 0.02f;

    [Header("Ambiente (hemisfério)")]
    public Color ambienteCeu = PaletaLua.AmbienteCeu;
    public Color ambienteChao = PaletaLua.AmbienteChao;

    [Header("Borda e contorno")]
    public Color bordaIluminada = PaletaLua.Rim;
    public bool contorno = true;
    public Color corContorno = PaletaLua.Contorno;

    static readonly int Ativo = Shader.PropertyToID("_ToonAtivo");
    static readonly int Sombra = Shader.PropertyToID("_ToonSombra");
    static readonly int Meio = Shader.PropertyToID("_ToonMeioTom");
    static readonly int Ceu = Shader.PropertyToID("_ToonCeu");
    static readonly int Chao = Shader.PropertyToID("_ToonChao");
    static readonly int Rim = Shader.PropertyToID("_ToonRim");
    static readonly int LMeio = Shader.PropertyToID("_ToonLimiteMeio");
    static readonly int LSombra = Shader.PropertyToID("_ToonLimiteSombra");
    static readonly int Suave = Shader.PropertyToID("_ToonSuavidade");
    static readonly int ContornoAtivo = Shader.PropertyToID("_ContornoCartoonAtivo");
    static readonly int ContornoCor = Shader.PropertyToID("_ContornoCartoonCor");

    void OnEnable() => Aplicar();
    void OnValidate() => Aplicar();
    void Update() => Aplicar();

    void OnDisable()
    {
        Shader.SetGlobalFloat(Ativo, 0f);
        Shader.SetGlobalFloat(ContornoAtivo, 0f);
    }

    public void Aplicar()
    {
        Shader.SetGlobalFloat(Ativo, 1f);
        Shader.SetGlobalColor(Sombra, sombra.linear);
        Shader.SetGlobalColor(Meio, meioTom.linear);
        Shader.SetGlobalColor(Ceu, ambienteCeu.linear);
        Shader.SetGlobalColor(Chao, ambienteChao.linear);
        Shader.SetGlobalColor(Rim, bordaIluminada.linear);
        Shader.SetGlobalFloat(LMeio, limiteMeioTom);
        Shader.SetGlobalFloat(LSombra, limiteSombra);
        Shader.SetGlobalFloat(Suave, suavidade);
        Shader.SetGlobalFloat(ContornoAtivo, contorno ? 1f : 0f);
        Shader.SetGlobalColor(ContornoCor, corContorno.linear);
    }
}
