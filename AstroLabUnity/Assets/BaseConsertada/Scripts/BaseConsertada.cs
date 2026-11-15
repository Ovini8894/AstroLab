using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

// Troca as peças quebradas da Base Lunar pelas versões consertadas (objetos OK_*).
//
// Como usar:
//   1. Arraste o prefab Assets/BaseConsertada/Prefabs/BaseConsertada.prefab para a SUA cena (que já tem a BaseLunar).
//   2. No seu script de conserto:
//        var base = FindAnyObjectByType<BaseConsertada>();
//        base.Consertar("REPAIR_Gerador_O2");   // ou "Gerador_O2", ou o nome do objeto que o raycast acertou
//   3. Para reagir a um conserto (som, missão, UI...), use o evento aoConsertar no Inspector ou via código.
public class BaseConsertada : MonoBehaviour
{
    [Tooltip("Raiz da base quebrada (objeto 'BaseLunar' da cena). Se ficar vazio, procura pelo nome.")]
    public Transform baseLunar;

    [Tooltip("Para testes: a cena já começa com a base toda consertada.")]
    public bool comecarTudoConsertado;

    [Serializable] public class EventoPeca : UnityEvent<string> { }

    [Tooltip("Chamado com o id da peça sempre que algo é consertado.")]
    public EventoPeca aoConsertar = new EventoPeca();

    [Tooltip("Chamado com o id da peça sempre que algo volta a ficar quebrado.")]
    public EventoPeca aoQuebrar = new EventoPeca();

    readonly Dictionary<string, GameObject> objetosBase = new Dictionary<string, GameObject>();
    readonly Dictionary<string, GameObject> objetosOk = new Dictionary<string, GameObject>();
    readonly HashSet<string> consertadas = new HashSet<string>();

    public int TotalPecas => PecasDaBase.Todas.Length;
    public int TotalConsertadas => consertadas.Count;
    public bool BaseToda => consertadas.Count == PecasDaBase.Todas.Length;

    void Awake()
    {
        if (!baseLunar)
        {
            var go = GameObject.Find("BaseLunar");
            if (go) baseLunar = go.transform;
        }

        if (baseLunar)
        {
            // fica exatamente por cima da base quebrada
            transform.SetPositionAndRotation(baseLunar.position, baseLunar.rotation);
            transform.localScale = baseLunar.lossyScale;
            foreach (var t in baseLunar.GetComponentsInChildren<Transform>(true))
                if (!objetosBase.ContainsKey(t.name)) objetosBase.Add(t.name, t.gameObject);
        }
        else
        {
            Debug.LogWarning("[BaseConsertada] Não achei o objeto 'BaseLunar' na cena: as peças OK_ vão aparecer, mas as quebradas não serão escondidas.", this);
        }

        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (!t.name.StartsWith("OK_")) continue;
            objetosOk[t.name] = t.gameObject;
            t.gameObject.SetActive(false);
        }

        if (comecarTudoConsertado) ConsertarTudo();
    }

    /// Conserta uma peça. Aceita o id ("Gerador_O2"), "OK_Gerador_O2" ou o nome do objeto quebrado ("REPAIR_Gerador_O2").
    /// Devolve true se consertou agora (false se não existe ou já estava consertada).
    public bool Consertar(string idOuNome)
    {
        var p = PecasDaBase.Encontrar(idOuNome);
        if (p == null)
        {
            Debug.LogWarning($"[BaseConsertada] '{idOuNome}' não é uma peça consertável (veja PecasDaBase.cs).", this);
            return false;
        }
        if (!consertadas.Add(p.id)) return false;
        Aplicar(p, true);
        aoConsertar.Invoke(p.id);
        return true;
    }

    /// Volta uma peça para o estado quebrado (útil para reiniciar a missão).
    public bool Quebrar(string idOuNome)
    {
        var p = PecasDaBase.Encontrar(idOuNome);
        if (p == null || !consertadas.Remove(p.id)) return false;
        Aplicar(p, false);
        aoQuebrar.Invoke(p.id);
        return true;
    }

    public bool EstaConsertada(string idOuNome)
    {
        var p = PecasDaBase.Encontrar(idOuNome);
        return p != null && consertadas.Contains(p.id);
    }

    /// true se o nome (ex.: o objeto que o jogador mirou) é uma peça que ainda dá para consertar.
    public bool PodeConsertar(string idOuNome)
    {
        var p = PecasDaBase.Encontrar(idOuNome);
        return p != null && !consertadas.Contains(p.id);
    }

    public void ConsertarTudo()
    {
        foreach (var p in PecasDaBase.Todas) Consertar(p.id);
    }

    /// Conserta todas as peças de uma categoria (veja PecasDaBase.Categoria), ex.: "Energia".
    public void ConsertarCategoria(string categoria)
    {
        foreach (var p in PecasDaBase.Todas)
            if (p.categoria == categoria) Consertar(p.id);
    }

    /// Quantas peças da categoria já foram consertadas / total.
    public (int consertadas, int total) Progresso(string categoria)
    {
        int c = 0, t = 0;
        foreach (var p in PecasDaBase.Todas)
        {
            if (p.categoria != categoria) continue;
            t++;
            if (consertadas.Contains(p.id)) c++;
        }
        return (c, t);
    }

    /// Esconde (ou mostra de novo) os detritos espalhados pela base (objetos Detrito_*).
    public void LimparDetritos(bool limpar = true)
    {
        foreach (var kv in objetosBase)
            if (kv.Key.StartsWith("Detrito_")) kv.Value.SetActive(!limpar);
    }

    /// Objeto consertado (OK_*) de uma peça, para efeitos, partículas, som etc. Pode ser null.
    public GameObject ObjetoConsertado(string idOuNome)
    {
        var p = PecasDaBase.Encontrar(idOuNome);
        return p != null && objetosOk.TryGetValue(p.objetoOk, out var go) ? go : null;
    }

    void Aplicar(PecasDaBase.Peca p, bool consertar)
    {
        foreach (var nome in p.quebrados)
            if (objetosBase.TryGetValue(nome, out var q)) q.SetActive(!consertar);

        if (objetosOk.TryGetValue(p.objetoOk, out var ok)) ok.SetActive(consertar);
        else Debug.LogWarning($"[BaseConsertada] Objeto {p.objetoOk} não está no prefab (reimporte o BaseConsertada.fbx).", this);
    }
}
