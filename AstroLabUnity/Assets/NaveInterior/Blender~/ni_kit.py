"""
KIT de componentes do cockpit: cada peca e modelada UMA vez (aqui) e repetida por instancing no Unity.
Referencial local de toda peca: origem na face do painel, Z para fora (para o jogador), Y = cima do painel.
Medidas reais aproximadas (disjuntor ~9 mm, alavanca ~2 cm, botao luminoso ~2 cm).
"""
import math
from mathutils import Vector as V, Matrix
from ni_base import Malha, M, T, R

PI = math.pi


def _m(nome):
    m = Malha("KIT_" + nome)
    m.chanfro = 0.0          # pecas de 1-3 cm: o chanfro ja esta no perfil
    return m


def disjuntor(aberto=False):
    """circuit breaker: botao escuro com chanfro; 'aberto' (disparado) mostra a faixa branca."""
    m = _m("DisjuntorAberto" if aberto else "Disjuntor")
    m.torno([(0.0068, 0.0010), (0.0046, 0.0018)], M.metal, segs=8, fechar_base=False, fechar_topo=False)
    z = 0.0018
    if aberto:
        m.torno([(0.0046, z), (0.0046, z + 0.004)], M.branco, segs=8, fechar_base=False, fechar_topo=False)
        z += 0.004
    m.torno([(0.0046, z), (0.0042, z + 0.0076), (0.0, z + 0.0087)], M.escuro, segs=8,
            fechar_base=False, fechar_topo=False)
    return m


def chave(pos=1):
    """toggle: porca sextavada, bucha e alavanca conica com ponta esferica. pos: 1 cima, 0 centro, -1 baixo."""
    nome = {1: "ChaveCima", 0: "ChaveCentro", -1: "ChaveBaixo"}[pos]
    m = _m(nome)
    m.torno([(0.0064, 0.0), (0.0064, 0.0032), (0.0038, 0.0032), (0.0038, 0.0075)], M.metal, segs=6, fechar_base=False,
            fechar_topo=True)
    a = math.radians(24) * pos
    d = V((0, math.sin(a), math.cos(a)))
    p0 = V((0, 0, 0.0068))
    p1 = p0 + d * 0.019
    m.cilindro(p0, p1, 0.0015, M.metal, 6, r2=0.0022, tampas=False)
    m.esfera(p1 + d * 0.0012, 0.0029, M.metal, 6, 3)
    return m


def alavanca(pos=1):
    """so a alavanca (para os controles interativos: o pivo fica na base, gira em X)."""
    m = _m("Alavanca")
    d = V((0, 0, 1))
    p0 = V((0, 0, 0.0))
    p1 = p0 + d * 0.019
    m.cilindro(p0, p1, 0.0015, M.metal, 8, r2=0.0022)
    m.esfera(p1 + d * 0.0012, 0.0029, M.metal, 8, 4)
    return m


def base_chave():
    m = _m("BaseChave")
    m.torno([(0.0064, 0.0), (0.0064, 0.0032), (0.0038, 0.0032), (0.0038, 0.0075)], M.metal, segs=6, fechar_base=False,
            fechar_topo=True)
    return m


def guarda():
    """guarda de arame (duas orelhas + travessa) que protege a chave de um toque acidental."""
    m = _m("Guarda")
    for s in (-1, 1):
        m.caixa((s * 0.0105, 0, 0.011), (0.0022, 0.026, 0.022), M.metal)
        m.caixa((s * 0.0105, 0, 0.0015), (0.006, 0.03, 0.003), M.metal)
    m.cilindro((-0.0105, 0.013, 0.021), (0.0105, 0.013, 0.021), 0.0016, M.metal, 6, tampas=False)
    m.cilindro((-0.0105, -0.013, 0.021), (0.0105, -0.013, 0.021), 0.0016, M.metal, 6, tampas=False)
    return m


def tampa():
    """tampa articulada vermelha (chave critica). Pivo na dobradica: origem = aresta de cima, gira em X local."""
    m = _m("Tampa")
    w, h, p, e = 0.026, 0.034, 0.026, 0.0016
    m.caixa((0, -h / 2, p), (w, h, e), M.vermelho)                        # frente
    m.caixa((0, -e / 2, p / 2), (w, e, p), M.vermelho)                    # topo (junto da dobradica)
    for s in (-1, 1):
        m.caixa((s * (w / 2 - e / 2), -h / 2, p / 2), (e, h, p), M.vermelho)
    m.caixa((0, -h - 0.002, p - 0.002), (0.012, 0.004, 0.003), M.vermelho)  # aba para abrir
    m.cilindro((-w / 2, 0, 0.002), (w / 2, 0, 0.002), 0.0022, M.metal, 8)  # dobradica
    m.caixa((0, 0.002, 0.0015), (w + 0.004, 0.006, 0.003), M.metal)
    return m


def botao(lente, nome):
    """botao luminoso quadrado (light pushbutton): moldura escura + capa de lente."""
    m = _m(nome)
    m.caixa((0, 0, 0.002), (0.021, 0.021, 0.004), M.escuro)
    m.caixa((0, 0, 0.0055), (0.0165, 0.0165, 0.003), M.escuro)
    m.caixa((0, 0, 0.0078), (0.0150, 0.0150, 0.0016), lente)
    return m


def capa_botao(lente, nome):
    """so a capa (parte que afunda ao apertar) para os interativos."""
    m = _m(nome)
    m.caixa((0, 0, 0.0055), (0.0165, 0.0165, 0.003), M.escuro)
    m.caixa((0, 0, 0.0078), (0.0150, 0.0150, 0.0016), lente)
    return m


def base_botao():
    m = _m("BaseBotao")
    m.caixa((0, 0, 0.002), (0.021, 0.021, 0.004), M.escuro)
    return m


def knob(grande=False):
    """knob rotativo: saia com marca branca + corpo recartilhado."""
    m = _m("KnobGrande" if grande else "Knob")
    s = 1.35 if grande else 1.0
    m.torno([(0.0105 * s, 0.0), (0.0105 * s, 0.0018), (0.0075 * s, 0.0026)], M.escuro, segs=8, fechar_base=False,
            fechar_topo=True)
    m.torno([(0.0066 * s, 0.0026), (0.0066 * s, 0.0135 * s), (0.0056 * s, 0.0152 * s), (0.0, 0.0156 * s)], M.escuro,
            segs=8, fechar_base=False, fechar_topo=False)
    m.caixa((0, 0.0086 * s, 0.0023), (0.0014, 0.0045 * s, 0.0008), M.branco)
    m.caixa((0, 0.003 * s, 0.0157 * s), (0.0012, 0.0045 * s, 0.0004), M.branco)
    return m


def seletor():
    """chave rotativa com barra (pointer knob)."""
    m = _m("Seletor")
    m.torno([(0.011, 0.0), (0.011, 0.002), (0.008, 0.003)], M.escuro, segs=12, fechar_base=False)
    m.caixa((0, 0.0, 0.009), (0.008, 0.026, 0.012), M.escuro)
    m.caixa((0, 0.0105, 0.0152), (0.0016, 0.005, 0.0006), M.branco)
    return m


def anunciador():
    """moldura de anunciador (a legenda colorida vem do atlas, na malha estatica): anel unico de 24 triangulos."""
    m = _m("Anunciador")
    w, h, e, b = 0.036, 0.0165, 0.0045, 0.0022
    o = [(-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2)]
    i = [(-w / 2 + b, -h / 2 + b), (w / 2 - b, -h / 2 + b), (w / 2 - b, h / 2 - b), (-w / 2 + b, h / 2 - b)]
    for k in range(4):
        a0, a1, i0, i1 = o[k], o[(k + 1) % 4], i[k], i[(k + 1) % 4]
        m.quad((*a0, e), (*a1, e), (*i1, e), (*i0, e), M.escuro)                  # topo
        m.quad((*a0, 0), (*a1, 0), (*a1, e), (*a0, e), M.escuro)                  # lado de fora
        m.quad((*i1, 0), (*i0, 0), (*i0, e), (*i1, e), M.escuro)                  # lado de dentro
    return m


def parafuso():
    """fixador Dzus de 1/4 de volta (cantos das placas)."""
    m = _m("Parafuso")
    m.torno([(0.0042, 0.0003), (0.0031, 0.0015), (0.0, 0.0018)], M.metal, segs=6, fechar_base=False, fechar_topo=False)
    m.quad((-0.0023, -0.0004, 0.00185), (0.0023, -0.0004, 0.00185), (0.0023, 0.0004, 0.00185), (-0.0023, 0.0004, 0.00185), M.escuro)
    return m


def tecla():
    """tecla de borda dos MDUs / teclado."""
    m = _m("Tecla")
    m.caixa((0, 0, 0.0025), (0.0135, 0.0095, 0.005), M.tecla)
    m.caixa((0, 0, 0.0052), (0.0115, 0.0075, 0.0008), M.tecla)
    return m


def medidor():
    """medidor de fita vertical (O1-O3): moldura, janela creme, ponteiro."""
    m = _m("Medidor")
    m.caixa((0, 0, 0.004), (0.034, 0.072, 0.008), M.escuro)
    m.caixa((0, 0.004, 0.0082), (0.022, 0.054, 0.0008), M.medidor)
    for k in range(9):
        lw, y = (0.006 if k % 2 == 0 else 0.0035), -0.02 + k * 0.006
        m.quad((-0.006 - lw / 2, y - 0.00035, 0.0087), (-0.006 + lw / 2, y - 0.00035, 0.0087), (-0.006 + lw / 2, y + 0.00035, 0.0087),
               (-0.006 - lw / 2, y + 0.00035, 0.0087), M.escuro)
    m.caixa((0.004, 0.006, 0.009), (0.010, 0.0016, 0.0008), M.vermelho)
    return m


def mdu():
    """moldura do MDU (Multifunction Display Unit): tela 0,19 m, 6 teclas de borda, 2 knobs de brilho."""
    m = _m("MDU")
    W, H, P = 0.27, 0.255, 0.032
    s = 0.196          # abertura da tela
    yb = -0.02         # tela levemente acima do centro (teclas embaixo)
    top = H / 2 - (yb + s / 2)
    bot = (yb - s / 2) + H / 2
    lat = (W - s) / 2
    m.caixa((0, H / 2 - top / 2, P / 2 - 0.004), (W, top, P), M.escuro)
    m.caixa((0, -H / 2 + bot / 2, P / 2 - 0.004), (W, bot, P), M.escuro)
    for sg in (-1, 1):
        m.caixa((sg * (W / 2 - lat / 2), yb, P / 2 - 0.004), (lat, s, P), M.escuro)
    # rebaixo em volta do vidro
    for sg in (-1, 1):
        m.caixa((0, yb + sg * (s / 2 + 0.002), P - 0.004 + 0.001), (s + 0.008, 0.004, 0.002), M.tecla)
        m.caixa((sg * (s / 2 + 0.002), yb, P - 0.004 + 0.001), (0.004, s, 0.002), M.tecla)
    for k in range(6):
        x = -0.0825 + k * 0.033
        m.caixa((x, -H / 2 + 0.022, P - 0.004 + 0.003), (0.020, 0.012, 0.006), M.tecla)
        m.caixa((x, -H / 2 + 0.022, P - 0.004 + 0.0062), (0.017, 0.009, 0.0008), M.tecla)
    for sg in (-1, 1):
        c = V((sg * 0.115, H / 2 - 0.016, P - 0.004))
        m.cilindro(c, c + V((0, 0, 0.008)), 0.0065, M.tecla, 14)
    return m


def trava():
    """trava de 1/4 de volta das portas dos lockers do middeck."""
    m = _m("Trava")
    m.caixa((0, 0, 0.0015), (0.034, 0.016, 0.003), M.metal)
    m.caixa((0, 0, 0.0055), (0.028, 0.008, 0.005), M.escuro)
    return m


def construir():
    """devolve {tipo: Malha}. Os nomes de tipo sao a chave usada no JSON de instancias."""
    return {
        "disj": disjuntor(False), "disj_ab": disjuntor(True),
        "tog_c": chave(1), "tog_m": chave(0), "tog_b": chave(-1),
        "guarda": guarda(), "tampa": tampa(),
        "bot_apag": botao(M.lente_apagada, "BotaoApagado"), "bot_verde": botao(M.lente_verde, "BotaoVerde"),
        "bot_amb": botao(M.lente_ambar, "BotaoAmbar"), "bot_verm": botao(M.lente_verm, "BotaoVermelho"),
        "bot_br": botao(M.lente_branca, "BotaoBranco"),
        "knob": knob(False), "knob_g": knob(True), "seletor": seletor(),
        "anun": anunciador(), "trava": trava(), "paraf": parafuso(), "tecla": tecla(), "medidor": medidor(), "mdu": mdu(),
        # partes dos interativos (base fixa + parte movel)
        "base_chave": base_chave(), "alavanca": alavanca(), "base_botao": base_botao(),
        "capa_apag": capa_botao(M.lente_apagada, "CapaApagada"), "capa_verm": capa_botao(M.lente_verm, "CapaVermelha"),
        "capa_verde": capa_botao(M.lente_verde, "CapaVerde"), "capa_amb": capa_botao(M.lente_ambar, "CapaAmbar"),
        "capa_br": capa_botao(M.lente_branca, "CapaBranca"),
    }
