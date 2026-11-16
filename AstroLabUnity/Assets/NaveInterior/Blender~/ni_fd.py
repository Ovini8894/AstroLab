"""
L1 - FLIGHT DECK (cabine de comando) do interior do onibus espacial.
Coordenadas do orbitador (X nariz, Y bombordo, Z teto) - ver Docs/NAVE_INTERIOR_ESPEC.md.
Registra geometria estatica (B.ST), instancias do kit (B.KIT) e textos dos atlas (ETQ/LEG).

Paineis:  F1 (asa CDR) · F2/F3/F4 (glareshield) · F5 (CDR baixo) · F6 (MDUs CDR) · F7 (centro) · F8 (MDUs PLT)
          F9 (PLT baixo) · F10 (asa PLT) · C1 (teclados) · C2/C3 (console central) · O1-O17 (teto)
          L1/L2 (lateral CDR) · R1/R2 (lateral PLT)
"""
import math, random
from mathutils import Vector as V, Matrix
import ni_base as B
from ni_base import M, T, R, st, KIT, quadro, painel, etiqueta, legenda, friso, tela, hexa, faixa, luz, MARCAS, INSET, FZ, MZ
import ni_atlas as A

PI = math.pi
RNG = random.Random(1981)
rad = math.radians


def pares(seq):
    return list(zip(seq[:-1], seq[1:]))


# ======================================================================= vocabulario (etiquetas / legendas)
SIS = ["MN A", "MN B", "MN C", "ESS 1BC", "ESS 2CA", "ESS 3AB", "CNTL AB1", "CNTL BC2", "CNTL CA3", "AC 1", "AC 2", "AC 3",
       "FC 1", "FC 2", "FC 3", "APU 1", "APU 2", "APU 3", "HYD 1", "HYD 2", "HYD 3", "GPC 1", "GPC 2", "GPC 3", "GPC 4",
       "GPC 5", "MDM FF1", "MDM FF2", "MDM FF3", "MDM FF4", "IMU 1", "IMU 2", "IMU 3", "TACAN", "MLS", "RA 1", "RA 2",
       "ADTA", "RGA", "AA", "OMS L", "OMS R", "RCS F", "RCS L", "RCS R", "HEATER", "ECLSS", "H2O LOOP", "FREON",
       "CABIN FAN", "IMU FAN", "AV BAY", "O2 SPLY", "N2 SPLY", "CRYO O2", "CRYO H2", "LDG GEAR", "BRAKES", "NWS",
       "DRAG CHUTE", "HUD", "MEDS", "IDP 1", "IDP 2", "IDP 3", "IDP 4", "KU BAND", "S BAND", "UHF", "AUDIO", "CCTV",
       "PL BAY", "RMS", "LIGHTS", "FLOOD", "DOME", "ANNUN", "C/W", "FIRE SUPPR", "SMOKE DET", "DPS", "EIU", "MEC 1",
       "MEC 2", "PIC", "SRB SEP", "ET SEP", "ABORT", "OMS ENG", "RCS JET", "VENT DOOR", "STAR TRKR", "COAS", "TIMER",
       "MET", "GMT", "DDU", "ADI", "HSI", "AMI", "AVVI", "FLT CNTLR", "SPD BK", "BODY FLAP", "TRIM", "ATT REF",
       "CRT", "KYBD", "MDU PWR", "DISP", "INTEG LT", "PANEL LT"]
POS2 = [("ON", "OFF"), ("OPEN", "CLOSE"), ("ARM", "SAFE"), ("AUTO", "MAN"), ("ON", "OFF"), ("NORM", "ALT"), ("PRI", "SEC")]
CW = [("O2 PRESS", "verm"), ("H2 PRESS", "verm"), ("FUEL CELL\nREAC", "ambar"), ("FUEL CELL\nSTACK TEMP", "ambar"),
      ("FUEL CELL\nPUMP", "ambar"), ("CABIN\nATM", "verm"), ("O2 HEATER\nTEMP", "ambar"), ("MAIN BUS\nUNDERVOLT", "ambar"),
      ("AC\nVOLTAGE", "ambar"), ("AC\nOVERLOAD", "ambar"), ("FREON\nLOOP", "ambar"), ("AV BAY/\nCABIN AIR", "ambar"),
      ("IMU", "ambar"), ("FWD RCS", "ambar"), ("RCS JET", "ambar"), ("H2O LOOP", "ambar"), ("RGA/ACCEL", "ambar"),
      ("AIR DATA", "ambar"), ("LEFT RHC", "verm"), ("RIGHT/AFT\nRHC", "verm"), ("LEFT OMS", "ambar"),
      ("RIGHT OMS", "ambar"), ("PAYLOAD\nWARNING", "verm"), ("GPC", "verm"), ("FCS\nSATURATION", "ambar"),
      ("OMS KIT", "ambar"), ("PRIMARY C/W", "ambar"), ("FCS\nCHANNEL", "ambar"), ("MPS", "verm"),
      ("BACKUP C/W\nALARM", "verm"), ("APU\nTEMP", "ambar"), ("APU\nOVERSPEED", "ambar"), ("APU\nUNDERSPEED", "ambar"),
      ("HYD PRESS", "ambar"), ("PAYLOAD\nCAUTION", "ambar"), ("LEFT RCS", "ambar"), ("RIGHT RCS", "ambar"),
      ("CABIN\nPRESS", "verm"), ("SM ALERT", "ciano"), ("ET SEP\nMAN", "ambar"), ("SRB SEP\nMAN", "ambar"),
      ("TACAN", "ambar"), ("MLS", "ambar"), ("NWS FAIL", "ambar"), ("ANTISKID", "ambar"), ("BRAKE\nPRESS", "ambar"),
      ("GEAR\nDOWN", "verde"), ("ROLL\nAUTO", "verde"), ("PITCH\nAUTO", "verde"), ("FC 1\nREADY", "verde"),
      ("FC 2\nREADY", "verde"), ("FC 3\nREADY", "verde"), ("APU 1\nREADY", "verde"), ("APU 2\nREADY", "verde"),
      ("APU 3\nREADY", "verde"), ("COMM\nLOCK", "ciano"), ("KU\nTRACK", "ciano"), ("S-BAND\nLOCK", "ciano")]

COR_BOT = {"verde": "bot_verde", "ambar": "bot_amb", "verm": "bot_verm", "branca": "bot_br", "apag": "bot_apag"}
LEG_COR = {"verde": "verde", "ambar": "ambar", "verm": "verm", "branca": "branca", "apag": "ciano"}


# ======================================================================= preenchimento de paineis
PITCH = {"disj": 0.0215, "tog": 0.034, "bot": 0.028, "anun": 0.040, "knob": 0.048, "sel": 0.058, "med": 0.042, "tecl": 0.0165}


def _alt(l):
    t = l["t"]
    if t == "tecl":
        return l["linhas"] * 0.0128 + 0.008
    if t == "esp":
        return l.get("a", 0.02)
    return {"disj": 0.040, "tog": 0.066, "bot": 0.046, "anun": 0.024, "knob": 0.058, "sel": 0.066, "med": 0.098,
            "tit": 0.018}[t]


def _posicoes(n, g, pitch, u0, u1):
    gg = pitch * 0.7
    tot = lambda k: k * pitch + (math.ceil(k / g) - 1) * gg
    n0 = n
    while n > 0 and tot(n) > (u1 - u0):
        n -= 1
    if n < n0:
        print(f"[fd] linha cortada {n0}->{n}")
    W = tot(n)
    s = (u0 + u1) / 2 - W / 2 + pitch / 2
    return [(s + k * pitch + (k // g) * gg, k // g, k % g, k) for k in range(n)]


def _grupos(ps):
    gs = {}
    for u, gi, k, i in ps:
        gs.setdefault(gi, []).append(u)
    return gs


def _quad_rot(m, F, u, v, w, h, ang, mat, z=0.0008):
    """retangulo pequeno girado (marcas de escala dos knobs)."""
    c, s = math.cos(ang), math.sin(ang)
    pts = []
    for du, dv in ((-w / 2, -h / 2), (w / 2, -h / 2), (w / 2, h / 2), (-w / 2, h / 2)):
        pts.append(F @ V((u + du * c - dv * s, v + du * s + dv * c, z)))
    m.quad(*pts, mat)


class Ctx:
    def __init__(self, codigo):
        self.codigo = codigo
        self.off = RNG.randrange(len(SIS))

    def nome(self, nomes, i):
        if nomes:
            return nomes[i % len(nomes)]
        return SIS[(self.off + i) % len(SIS)]


def _linha(m, F, u0, u1, vc, l, cx):
    t = l["t"]
    nomes = l.get("nomes")
    inter = l.get("int", {})
    if t == "tit":
        etiqueta(m, F, (u0 + u1) / 2, vc, l["txt"], larg=min(0.16, u1 - u0))
        friso(m, F, u0, vc - 0.0075, u1, vc - 0.0062, w=0.0012)
        return
    if t == "esp":
        return
    if t == "tecl":
        nb = l.get("blocos", 1)
        cols, lin = l["cols"], l["linhas"]
        larg = cols * 0.0165
        cs = [(u0 + u1) / 2 + (b - (nb - 1) / 2) * (larg + 0.03) for b in range(nb)]
        for c in cs:
            for r in range(lin):
                for k in range(cols):
                    KIT.por("tecla", F @ T(c + (k - (cols - 1) / 2) * 0.0165, vc + ((lin - 1) / 2 - r) * 0.0128, 0))
            friso(m, F, c - larg / 2 - 0.004, vc - lin * 0.0064 - 0.004, c + larg / 2 + 0.004, vc + lin * 0.0064 + 0.004)
        return
    g = l.get("g", 4)
    ps = _posicoes(l["n"], g, PITCH[t], u0, u1)
    if not ps:
        return
    if t == "disj":
        for u, gi, k, i in ps:
            KIT.por("disj_ab" if RNG.random() < 0.06 else "disj", F @ T(u, vc - 0.004, 0))
            etiqueta(m, F, u, vc - 0.0172, str(k + 1), larg=0.016)
        for gi, us in _grupos(ps).items():
            etiqueta(m, F, sum(us) / len(us), vc + 0.0128, cx.nome(nomes, gi), larg=min(0.07, max(us) - min(us) + 0.03))
            friso(m, F, min(us) - 0.0115, vc - 0.0225, max(us) + 0.0115, vc + 0.0195)
        return
    if t == "tog":
        guardas = set(l.get("guarda", ()))
        for u, gi, k, i in ps:
            Mw = F @ T(u, vc - 0.004, 0)
            it = inter.get(i)
            if it:
                KIT.por("chave", Mw, interativo=it, loc="nave." + it, painel=cx.codigo, pos=1)
                KIT.por("guarda", Mw)
                friso(m, F, u - 0.0155, vc - 0.027, u + 0.0155, vc + 0.0205, w=0.0022, z=0.001, mat=M.amarelo)
            elif i in l.get("tampa", ()):
                KIT.por("tog_b", Mw)
                KIT.por("tampa", Mw @ T(0, 0.017, 0))
                friso(m, F, u - 0.0155, vc - 0.027, u + 0.0155, vc + 0.0205, w=0.0022, z=0.001, mat=M.amarelo)
            else:
                KIT.por(RNG.choice(["tog_c", "tog_c", "tog_c", "tog_m", "tog_b"]), Mw)
                if i in guardas or RNG.random() < l.get("pg", 0.12):
                    KIT.por("guarda", Mw)
            a, b = POS2[(cx.off + i) % len(POS2)] if not l.get("pos") else l["pos"]
            etiqueta(m, F, u, vc + 0.0285, cx.nome(nomes, i), larg=0.032)
            etiqueta(m, F, u, vc + 0.0145, a, larg=0.014)
            etiqueta(m, F, u, vc - 0.0225, b, larg=0.014)
        for gi, us in _grupos(ps).items():
            friso(m, F, min(us) - 0.016, vc - 0.0285, max(us) + 0.016, vc + 0.0335)
        return
    if t == "bot":
        cores = l.get("cores", ["apag"])
        legs = l.get("leg")
        for u, gi, k, i in ps:
            cor = cores[i % len(cores)]
            Mw = F @ T(u, vc - 0.004, 0)
            it = inter.get(i)
            if it:
                KIT.por("botao", Mw, interativo=it, loc="nave." + it, painel=cx.codigo, cor=cor)
                friso(m, F, u - 0.0135, vc - 0.0175, u + 0.0135, vc + 0.0095, w=0.0022, z=0.001, mat=M.amarelo)
            else:
                KIT.por(COR_BOT[cor], Mw)
                if legs:
                    legenda(m, F, u, vc - 0.004, legs[i % len(legs)], LEG_COR[cor], acesa=cor != "apag", larg=0.0145,
                            z=0.0088)
            etiqueta(m, F, u, vc + 0.0175, cx.nome(nomes, i), larg=0.026)
        for gi, us in _grupos(ps).items():
            friso(m, F, min(us) - 0.0135, vc - 0.0185, max(us) + 0.0135, vc + 0.0225)
        return
    if t == "anun":
        itens = l.get("itens") or CW
        o = l.get("o", RNG.randrange(len(itens)))
        for u, gi, k, i in ps:
            KIT.por("anun", F @ T(u, vc, 0))
            txt, cor = itens[(o + i) % len(itens)]
            legenda(m, F, u, vc, txt, cor, acesa=(cor == "verde" or RNG.random() < 0.10), larg=0.031, z=0.0012)
        return
    if t == "knob":
        for u, gi, k, i in ps:
            KIT.por("knob_g" if l.get("grande") else "knob", F @ T(u, vc - 0.006, 0) @ R(rad(RNG.uniform(-120, 120)), "Z"))
            for a in range(7):
                ang = rad(-135 + 45 * a)
                r = 0.0175 if l.get("grande") else 0.0145
                _quad_rot(m, F, u - r * math.sin(ang), vc - 0.006 + r * math.cos(ang), 0.0011, 0.0034, ang, M.linha)
            etiqueta(m, F, u, vc + 0.022, cx.nome(nomes, i), larg=0.034)
        return
    if t == "sel":
        posl = l.get("posicoes", ["OFF", "1", "2"])
        for u, gi, k, i in ps:
            angs = [-40, 0, 40][:len(posl)]
            KIT.por("seletor", F @ T(u, vc - 0.008, 0) @ R(rad(-RNG.choice(angs)), "Z"))
            for a, txt in zip(angs, posl):
                ang = rad(-a)
                etiqueta(m, F, u - 0.0215 * math.sin(ang), vc - 0.008 + 0.0215 * math.cos(ang), txt, larg=0.016)
            etiqueta(m, F, u, vc + 0.027, cx.nome(nomes, i), larg=0.036)
        return
    if t == "med":
        for u, gi, k, i in ps:
            KIT.por("medidor", F @ T(u, vc - 0.006, 0))
            etiqueta(m, F, u, vc + 0.041, cx.nome(nomes, i), larg=0.036)
        return
    raise ValueError(t)


def preencher(F, w, h, linhas, codigo="", grupo="Paineis", v_top=None, v_bot=None, u_min=None, u_max=None, mold=0.010):
    m = st(grupo, 0.003)
    u0 = (-w / 2 + mold + 0.014) if u_min is None else u_min
    u1 = (w / 2 - mold - 0.014) if u_max is None else u_max
    top = (h / 2 - mold - 0.026) if v_top is None else v_top
    bot = (-h / 2 + mold + 0.006) if v_bot is None else v_bot
    alts = [_alt(l) for l in linhas]
    while linhas and sum(alts) > top - bot:
        print(f"[fd] {codigo}: linha descartada ({linhas[-1]['t']})")
        linhas, alts = linhas[:-1], alts[:-1]
    sobra = (top - bot) - sum(alts)
    gap = min(sobra / max(1, len(linhas) - 1), 0.026) if len(linhas) > 1 else 0
    v = top - (sobra - gap * (len(linhas) - 1)) / 2
    cx = Ctx(codigo)
    for l, a in zip(linhas, alts):
        _linha(m, F, u0, u1, v - a / 2, l, cx)
        v -= a + gap


def P(F, w, h, codigo, linhas, **kw):
    """painel completo: placa + moldura + Dzus + codigo + conteudo."""
    painel(F, w, h, codigo, mold=kw.pop("mold_p", 0.010), parafusos=kw.pop("parafusos", True))
    preencher(F, w, h, linhas, codigo, **kw)


# atalhos de linha
def Dj(n, g=4, nomes=None): return dict(t="disj", n=n, g=g, nomes=nomes)
def Tg(n, g=4, nomes=None, **kw): return dict(t="tog", n=n, g=g, nomes=nomes, **kw)
def Bt(n, g=4, nomes=None, cores=("apag",), leg=None, **kw): return dict(t="bot", n=n, g=g, nomes=nomes, cores=list(cores), leg=leg, **kw)
def An(n, g=8, itens=None, o=None): return dict(t="anun", n=n, g=g, itens=itens, **({"o": o} if o is not None else {}))
def Kn(n, g=8, nomes=None, grande=False): return dict(t="knob", n=n, g=g, nomes=nomes, grande=grande)
def Sl(n, g=8, nomes=None, posicoes=None): return dict(t="sel", n=n, g=g, nomes=nomes, posicoes=posicoes or ["OFF", "1", "2"])
def Md(n, g=8, nomes=None): return dict(t="med", n=n, g=g, nomes=nomes)
def Tc(linhas, cols, blocos=1): return dict(t="tecl", linhas=linhas, cols=cols, blocos=blocos)
def Ti(txt): return dict(t="tit", txt=txt)


# ======================================================================= casco interno, janelas, piso
def _xs(x0, x1, extra=()):
    return sorted(set([x for x in B.grade_X() if x0 <= x <= x1] + [x0, x1] + list(extra)))


def _ts():
    ts = sorted(set(B.grade_T()))
    return ts + [ts[0] + 360]


def casco_interno(x0=26.8, x1=33.6, tampa_re=True):
    m = st("Casco")
    xs, ts = _xs(x0, x1), _ts()
    Pc = lambda x, t: B.casco(x, t % 360, INSET)
    for xa, xb in pares(xs):
        for ta, tb in pares(ts):
            if B.em_janela(xa, xb, ta, tb) or B.em_escotilha(xa, xb, ta, tb):
                continue
            ps = [Pc(xa, ta), Pc(xb, ta), Pc(xb, tb), Pc(xa, tb)]
            if max(p.z for p in ps) < MZ - 0.02:
                continue
            m.quad(*ps, M.parede)
    # tampas: proa (fecha atras do painel) e anteparo de re do flight deck
    for x, sent in ((x1, -1), (x0, 1))[:2 if tampa_re else 1]:
        vs = [m.bm.verts.new(B.casco(x, t, INSET)) for t in _ts()[:-1]]
        f = m.bm.faces.new(vs)
        f.normal_update()
        if f.normal.x * sent < 0:
            f.normal_flip()
        f.material_index = m.mi(M.parede)
    # nervuras (cavernas) do forro onde nao ha janela: relevo que tira o "tubo liso"
    mr = st("Nervuras", 0.006)
    for x in (27.6, 28.6, 29.6, 30.4):
        ts = _ts()
        for ta, tb in pares(ts):
            a0, b0 = B.casco(x - 0.03, ta, INSET - 0.001), B.casco(x - 0.03, tb, INSET - 0.001)
            a1, b1 = B.casco(x - 0.03, ta, INSET + 0.035), B.casco(x - 0.03, tb, INSET + 0.035)
            c0, d0 = B.casco(x + 0.03, ta, INSET + 0.035), B.casco(x + 0.03, tb, INSET + 0.035)
            c1, d1 = B.casco(x + 0.03, ta, INSET - 0.001), B.casco(x + 0.03, tb, INSET - 0.001)
            if max(a0.z, b0.z) < FZ - 0.05:
                continue
            mr.quad(a0, b0, b1, a1, M.parede_esc, duplo=True)
            mr.quad(a1, b1, d0, c0, M.parede_esc, duplo=True)
            mr.quad(c0, d0, d1, c1, M.parede_esc, duplo=True)


def janelas():
    mv = st("Vidros")
    mr = st("Janelas")
    for (x0, x1, t0, t1) in B.JANELAS:
        xs = _xs(x0, x1)
        ts = sorted(set([t for t in B.grade_T() if t0 <= t <= t1] + [t0, t1]))
        borda = [(x, t0) for x in xs] + [(x1, t) for t in ts[1:]] + [(x, t1) for x in reversed(xs[:-1])] + \
                [(x0, t) for t in reversed(ts[1:-1])]
        borda.append(borda[0])
        r = B.secao((x0 + x1) / 2)[0]
        dt = math.degrees(0.045 / r)
        ex = lambda x, t: ((-0.045 if abs(x - x0) < 1e-6 else 0.045 if abs(x - x1) < 1e-6 else 0.0),
                           (-dt if abs(t - t0) < 1e-6 else dt if abs(t - t1) < 1e-6 else 0.0))
        for (xa, ta), (xb, tb) in pares(borda):
            # vao (reveal) do forro ate o casco externo
            mr.quad(B.casco(xa, ta, INSET), B.casco(xb, tb, INSET), B.casco(xb, tb, 0.0), B.casco(xa, ta, 0.0),
                    M.moldura, duplo=True)
            # aba da moldura sobre o forro
            ea, eb = ex(xa, ta), ex(xb, tb)
            mr.quad(B.casco(xa, ta, INSET - 0.012), B.casco(xb, tb, INSET - 0.012),
                    B.casco(xb + eb[0], tb + eb[1], INSET - 0.012), B.casco(xa + ea[0], ta + ea[1], INSET - 0.012),
                    M.escuro, duplo=True)
            mr.quad(B.casco(xa + ea[0], ta + ea[1], INSET - 0.012), B.casco(xb + eb[0], tb + eb[1], INSET - 0.012),
                    B.casco(xb + eb[0], tb + eb[1], INSET), B.casco(xa + ea[0], ta + ea[1], INSET), M.escuro, duplo=True)
        for xa, xb in pares(xs):
            for ta, tb in pares(ts):
                mv.quad(B.casco(xa, ta, 0.02), B.casco(xb, ta, 0.02), B.casco(xb, tb, 0.02), B.casco(xa, tb, 0.02),
                        M.vidro, duplo=True)


FURO = (27.4, 28.4, 0.55, 1.35)        # acesso entre os decks (bombordo, a re): a escada vem no L2


def piso(x0=26.8, x1=32.35):
    m = st("Piso", 0.004)
    xs = _xs(x0, x1, [FURO[0], FURO[1]] + [round(x0 + 0.3 * k, 3) for k in range(1, 19) if x0 + 0.3 * k < x1])
    zl, zh = FZ - 0.10, FZ
    for xa, xb in pares(xs):
        pecas = [(None, None)]
        if FURO[0] - 1e-6 <= xa and xb <= FURO[1] + 1e-6:
            pecas = [(None, FURO[2]), (FURO[3], None)]
        for lo, hi in pecas:
            def p(i, j, k):
                x = (xa, xb)[i]
                z = (zl, zh)[k]
                w = B.meia_largura(x, z) + 0.01
                y = (-w if lo is None else lo) if j == 0 else (w if hi is None else hi)
                return (x, y, z)
            hexa(m, [p(i, j, k) for k in (0, 1) for j in (0, 1) for i in (0, 1)], M.piso)
    # juntas das placas
    mj = st("PisoJuntas")
    for k in range(int((x1 - x0) / 0.6) + 1):
        x = x0 + 0.3 + k * 0.6
        if x >= x1:
            break
        w = B.meia_largura(x, FZ)
        if FURO[0] < x < FURO[1]:
            for a, b in ((-w, FURO[2]), (FURO[3], w)):
                mj.caixa((x, (a + b) / 2, FZ + 0.001), (0.012, b - a, 0.002), M.moldura)
        else:
            mj.caixa((x, 0, FZ + 0.001), (0.012, 2 * w, 0.002), M.moldura)
    for y in (-1.2, -0.6, 0.6, 1.2):
        mj.caixa(((x0 + x1) / 2, y, FZ + 0.001), (x1 - x0, 0.012, 0.002), M.moldura)
    # brazola do acesso (amarela)
    a0, a1, b0, b1 = FURO
    for c, d in (((a0 - 0.02, (b0 + b1) / 2), (0.04, b1 - b0 + 0.08)), ((a1 + 0.02, (b0 + b1) / 2), (0.04, b1 - b0 + 0.08)),
                 (((a0 + a1) / 2, b0 - 0.02), (a1 - a0, 0.04)), (((a0 + a1) / 2, b1 + 0.02), (a1 - a0, 0.04))):
        st("Controles", 0.003).caixa((c[0], c[1], FZ + 0.012), (d[0], d[1], 0.024), M.amarelo)


# ======================================================================= painel frontal
F0 = quadro((32.0, 0.0, 3.68), (-0.94, 0.0, 0.34), (0, 0, 1))
FW, FH = 1.96, 0.72
ASA = rad(36)


def capuz(F, w, h, luzes=True):
    """glareshield: frente escura saliente, tampo e faixa de luz embaixo (ilumina o painel)."""
    m = st("Glareshield", 0.008)
    m.caixa((0, h / 2 + 0.067, 0.17), (w, 0.112, 0.03), M.escuro, M=F)
    m.caixa((0, h / 2 + 0.128, 0.09), (w, 0.018, 0.23), M.escuro, M=F)
    m.caixa((0, h / 2 + 0.05, 0.035), (w, 0.13, 0.012), M.escuro, M=F)       # fundo da cavidade
    st("Luzes").caixa((0, h / 2 + 0.115, 0.11), (w - 0.04, 0.004, 0.012), M.luz, M=F)
    for s in (-1, 1):
        m.caixa((s * (w / 2 - 0.006), h / 2 + 0.07, 0.10), (0.012, 0.13, 0.20), M.escuro, M=F)


def mdu(F, u, v, pagina, nome):
    KIT.por("mdu", F @ T(u, v, 0))
    tela(st("Telas"), F, u, v - 0.02, 0.192, 0.192, pagina, z=0.010)
    etiqueta(st("Paineis", 0.003), F, u, v - 0.145, nome, larg=0.05)


def painel_frontal():
    # placa de fundo continua (some atras dos subpaineis)
    st("Estrutura", 0.006).caixa((0, 0, -0.05), (FW + 0.02, FH + 0.02, 0.05), M.moldura, M=F0)
    sub = lambda uc, vc: F0 @ T(uc, vc, 0)
    # F6 / F8: MDUs do comandante e do piloto
    for sgn, cod, n1, n2, pg in ((-1, "F6", "CDR 1", "CDR 2", (A.P_PFD, A.P_HSI)), (1, "F8", "PLT 1", "PLT 2", (A.P_HSI, A.P_PFD))):
        F = sub(sgn * 0.655, 0.175)
        painel(F, 0.65, 0.37, cod)
        mdu(F, -0.155, 0.006, pg[0], n1)
        mdu(F, 0.155, 0.006, pg[1], n2)
    # F7: centro (MFD 1/2 em cima, CRT 1/2 embaixo) + botao MEDS
    F = sub(0, 0)
    painel(F, 0.65, FH, "F7")
    mdu(F, -0.155, 0.185, A.P_GNC, "MFD 1")
    mdu(F, 0.155, 0.185, A.P_OMS, "MFD 2")
    mdu(F, -0.155, -0.165, A.P_DPS, "CRT 1")
    mdu(F, 0.155, -0.165, A.P_FALHAS, "CRT 2")
    preencher(F, 0.65, FH, [Bt(5, 5, ["MEDS PWR", "MDU", "DISP", "INTEG LT", "PANEL LT"], cores=("branca", "apag"),
                               leg=["ON", "SEL", "SEL", "ON", "ON"], int={0: "meds"})], "F7", v_top=0.037, v_bot=-0.012)
    # F5 / F9: paineis de baixo
    linhas_f5 = [An(12, 6, o=0), Bt(8, 4, ["LDG GEAR", "LDG GEAR", "DRAG CHUTE", "DRAG CHUTE", "HUD", "HUD", "SPD BK", "BODY FLAP"],
                                    cores=("ambar", "verde", "ambar", "verm", "branca", "apag", "branca", "apag"),
                                    leg=["ARM", "DN", "ARM", "JETT", "PWR", "TEST", "AUTO", "MAN"]),
                 Tg(12, 4, tampa=(3,)), Kn(6, 6, ["ADI", "HSI", "AMI", "AVVI", "DDU", "ATT REF"])]
    linhas_f9 = [An(12, 6, o=24), Bt(8, 4, cores=("verde", "apag", "branca", "apag")), Tg(12, 4, tampa=(8,)),
                 Sl(5, 5, ["ADI", "ATT REF", "HSI", "AIR DATA", "RA 1"], ["LVLH", "INRTL", "REF"])]
    P(sub(-0.655, -0.185), 0.65, 0.35, "F5", linhas_f5)
    P(sub(0.655, -0.185), 0.65, 0.35, "F9", linhas_f9)
    capuz(F0, FW + 0.02, FH)
    # F2 / F3 / F4 na frente do glareshield
    G = F0 @ T(0, FH / 2 + 0.067, 0.186)
    for uc, cod, linhas in (
            (-0.62, "F2", [Bt(9, 9, ["MASTER ALARM", "AUTO", "CSS", "PITCH", "ROLL/YAW", "BODY FLAP", "SPD BK", "AUTO", "CSS"],
                              cores=("verm", "verde", "branca", "verde", "branca", "apag", "branca", "verde", "apag"),
                              leg=["ALARM", "AUTO", "CSS", "AUTO", "CSS", "AUTO", "AUTO", "AUTO", "CSS"], int={0: "master_alarm"})]),
            (0.0, "F3", [An(9, 9, o=46)]),
            (0.62, "F4", [Bt(9, 9, ["AUTO", "CSS", "PITCH", "ROLL/YAW", "BODY FLAP", "SPD BK", "AUTO", "CSS", "MASTER ALARM"],
                               cores=("verde", "branca", "verde", "branca", "apag", "branca", "verde", "apag", "verm"),
                               leg=["AUTO", "CSS", "AUTO", "CSS", "AUTO", "AUTO", "AUTO", "CSS", "ALARM"])])):
        w = 0.52 if uc else 0.40
        P(G @ T(uc, 0, 0), w, 0.096, cod, linhas, mold_p=0.006, parafusos=False, grupo="Paineis", v_top=0.015, v_bot=-0.04)
    # HUDs (combinador de vidro sobre o glareshield)
    for uc in (-0.655, 0.655):
        H = F0 @ T(uc, FH / 2 + 0.137, 0.16) @ R(rad(-15), "X")
        mh = st("Controles", 0.003)
        mh.caixa((0, 0.012, 0), (0.16, 0.024, 0.05), M.escuro, M=H)
        for s in (-1, 1):
            mh.caixa((s * 0.075, 0.075, 0.0), (0.01, 0.13, 0.012), M.escuro, M=H)
        mh.caixa((0, 0.142, 0.0), (0.16, 0.012, 0.012), M.escuro, M=H)
        st("Vidros").quad(H @ V((-0.07, 0.025, 0)), H @ V((0.07, 0.025, 0)), H @ V((0.07, 0.136, 0)), H @ V((-0.07, 0.136, 0)),
                          M.vidro, duplo=True)
    # chapa de pes (do painel ao piso) e pedais
    mk = st("Estrutura", 0.006)
    a, b = F0 @ V((-FW / 2 - 0.01, -FH / 2 - 0.01, -0.03)), F0 @ V((FW / 2 + 0.01, -FH / 2 - 0.01, -0.03))
    mk.quad(V((32.36, a.y, FZ)), V((32.36, b.y, FZ)), b, a, M.parede_esc, duplo=True)
    asas()
    tampo_nariz()


def tampo_nariz():
    """tampo escuro do glareshield ate a base do para-brisa: esconde a tampa do nariz e corta o reflexo."""
    m = st("Glareshield", 0.008)
    xs = [32.10 + k * 0.1 for k in range(15)]
    z = lambda x: 4.215 + (x - 32.10) * (4.30 - 4.215) / 1.4
    for xa, xb in pares(xs):
        wa, wb = B.meia_largura(xa, z(xa)) - 0.01, B.meia_largura(xb, z(xb)) - 0.01
        m.quad(V((xa, -wa, z(xa))), V((xb, -wb, z(xb))), V((xb, wb, z(xb))), V((xa, wa, z(xa))), M.escuro, duplo=True)


def asas():
    for sgn, cod in ((-1, "F1"), (1, "F10")):
        base = F0 @ T(sgn * (FW / 2 + 0.01), 0, 0) @ R(-sgn * ASA, "Y")
        # largura ate a parede (testa os cantos de cima e de baixo)
        wl = 0.4
        while wl < 1.4:
            ok = True
            for v in (-FH / 2, FH / 2 + 0.13):
                p = base @ V((sgn * (wl + 0.02), v, 0.0))
                if abs(p.y) > B.meia_largura(p.x, p.z) - 0.04:
                    ok = False
            if not ok:
                break
            wl += 0.02
        Fw = base @ T(sgn * wl / 2, 0, 0)
        st("Estrutura", 0.006).caixa((0, 0, -0.05), (wl, FH + 0.02, 0.05), M.moldura, M=Fw)
        wc = min(wl - 0.02, 0.92)
        Fc = base @ T(sgn * wc / 2, 0, 0)
        if sgn < 0:
            linhas = [Ti("CAUTION & WARNING"), An(20, 5, o=0), An(20, 5, o=20), Tg(14, 4, ["C/W", "ANNUN", "FLOOD", "INTEG LT"]),
                      Bt(8, 4, cores=("apag", "branca")), Dj(32, 4), Sl(6, 6, ["CRT", "DISP", "ADI", "HSI", "MDU", "KYBD"], ["1", "2", "3"]),
                      Kn(8, 8)]
        else:
            linhas = [Ti("SYSTEMS MGMT"), An(20, 5, o=30), Tg(14, 4), Md(6, 6, ["H2O LOOP", "FREON", "CABIN", "O2 SPLY", "N2 SPLY", "HYD 1"]),
                      Dj(32, 4), Sl(6, 6), Kn(8, 8)]
        P(Fc, wc, FH, cod, linhas)
        capuz(Fw, wl, FH)
        # fechamento ate o piso
        mk = st("Estrutura", 0.006)
        a, b = Fw @ V((-wl / 2, -FH / 2 - 0.01, -0.03)), Fw @ V((wl / 2, -FH / 2 - 0.01, -0.03))
        mk.quad(V((a.x + 0.25, a.y, FZ)), V((b.x + 0.25, b.y, FZ)), b, a, M.parede_esc, duplo=True)


# ======================================================================= console central (C1 teclados, C2, C3)
def console_central():
    m = st("Estrutura", 0.006)
    xa, xb, xc = 30.96, 31.40, 31.70
    zt = lambda x: 3.38 + (x - 31.18) * 0.17         # tampo levemente inclinado para o tripulante
    zk = lambda x: zt(xb) + (x - xb) * 0.40          # face dos teclados
    w = 0.29
    hexa(m, [((xa, xb)[i], (-w, w)[j], (FZ, zt((xa, xb)[i]) - 0.03)[k]) for k in (0, 1) for j in (0, 1) for i in (0, 1)],
         M.parede_esc)
    hexa(m, [((xb, xc)[i], (-w, w)[j], (FZ, zk((xb, xc)[i]) - 0.03)[k]) for k in (0, 1) for j in (0, 1) for i in (0, 1)], M.parede_esc)
    n3 = V((-0.17, 0, 1)).normalized()
    for cod, x0, x1, linhas in (("C3", xa, 31.18, [Tg(10, 5), Dj(16, 4), Kn(5, 5, ["AUDIO", "UHF", "S BAND", "KU BAND", "CCTV"])]),
                                ("C2", 31.18, xb, [Bt(12, 4, ["ROLL/YAW", "PITCH", "SPD BK", "BODY FLAP"], cores=("verde", "apag", "branca", "apag"),
                                                     leg=["AUTO", "CSS", "AUTO", "MAN"]), Sl(4, 4, ["DAP", "ADI", "RA 1", "RA 2"], ["A", "B", "AUTO"]),
                                                  Tg(8, 4)])):
        xm = (x0 + x1) / 2
        F = quadro((xm, 0, zt(xm)), n3, (1, 0, 0))
        P(F, 2 * w - 0.01, (x1 - x0) / math.cos(math.atan(0.17)) - 0.006, cod, linhas)
    d = V((xc - xb, 0, zk(xc) - zk(xb)))
    nk = V((-d.z, 0, d.x)).normalized()
    F = quadro(((xb + xc) / 2, 0, (zk(xb) + zk(xc)) / 2), nk, d)
    P(F, 2 * w - 0.01, d.length - 0.006, "C1", [Tc(4, 6, blocos=2), Tc(2, 6, blocos=2)])


# ======================================================================= paineis do teto (O1-O17)
def frame_teto(xc, yc, desc=0.16):
    e = 0.02
    gx = (B.teto(xc + e, yc) - B.teto(xc - e, yc)) / (2 * e)
    gy = (B.teto(xc, yc + e) - B.teto(xc, yc - e)) / (2 * e)
    n = V((gx, gy, -1)).normalized()
    return quadro((xc, yc, B.teto(xc, yc) - desc), n, (-1, 0, 0))


def frame_lado_alto(xc, ta, tb, prof=0.075):
    pa, pb = B.casco(xc, ta, INSET + prof), B.casco(xc, tb, INSET + prof)
    t = pb - pa
    o = (pa + pb) / 2
    n = t.normalized().cross(V((1, 0, 0)))
    if n.dot(V((xc, 0, 3.6)) - o) < 0:
        n = -n
    cima = t if ta < 90 else -t
    return quadro(o, n, cima), t.length


TETO_AFT = dict(
    O9=[Ti("ECLSS"), Tg(12, 4, ["CABIN FAN", "IMU FAN", "AV BAY", "H2O LOOP"]), Md(5, 5, ["CABIN", "O2 SPLY", "N2 SPLY", "FREON", "H2O LOOP"]),
        Tg(12, 4), An(10, 5, o=10)],
    O10=[Ti("DATA PROCESSING"), Sl(5, 5, ["GPC 1", "GPC 2", "GPC 3", "GPC 4", "GPC 5"], ["RUN", "HALT", "STBY"]),
         Tg(15, 5, ["GPC 1", "GPC 2", "GPC 3", "GPC 4", "GPC 5"], pos=("OUTPUT", "BKUP")), Bt(10, 5, cores=("verde", "apag")),
         Tg(12, 4, ["MDM FF1", "MDM FF2", "MDM FF3", "MDM FF4"])],
    O11=[Ti("NAV AIDS"), Tg(12, 4, ["TACAN", "MLS", "RA 1", "RA 2"]), Sl(4, 4, ["TACAN", "MLS", "ADTA", "RGA"], ["OFF", "GPC", "TEST"]),
         Tg(12, 4, ["IMU 1", "IMU 2", "IMU 3", "STAR TRKR"]), Kn(6, 6)],
    O12=[Ti("FLIGHT CONTROL"), Tg(12, 4, ["FLT CNTLR", "AA", "RGA", "ADTA"]), Bt(10, 5, cores=("verde", "branca")),
         Tg(12, 4, ["MEC 1", "MEC 2", "PIC", "EIU"], tampa=(2, 7)), An(10, 5, o=40)],
)
DISJ_O = ["MN A", "MN B", "MN C", "CNTL AB1", "CNTL BC2", "CNTL CA3", "ESS 1BC", "ESS 2CA", "ESS 3AB", "AC 1", "AC 2", "AC 3"]


def teto():
    # cada painel do teto tem a sua caixa de fundo (do verso da placa ate dentro do casco)
    costas = lambda F, w, h: st("Estrutura", 0.006).caixa((0, 0, -0.03 - 0.16), (w + 0.012, h + 0.012, 0.32), M.parede_esc, M=F)
    # fileira 1 (O9-O12, acima e logo atras das cabecas) e fileira 2 (O13-O17: disjuntores)
    for i, (yc, cod) in enumerate(zip((1.005, 0.335, -0.335, -1.005), ("O9", "O10", "O11", "O12"))):
        F = frame_teto(30.905, yc, 0.17)
        costas(F, 0.66, 0.68)
        P(F, 0.66, 0.68, cod, TETO_AFT[cod])
    for i, yc in enumerate((1.072, 0.536, 0.0, -0.536, -1.072)):
        cod = f"O{13 + i}"
        F = frame_teto(30.16, yc, 0.17)
        nomes = DISJ_O[(i * 3) % len(DISJ_O):] + DISJ_O
        linhas = [Ti(cod + " CIRCUIT BREAKERS")] + [Dj(18, 3, nomes[k:] + nomes) for k in range(0, 12, 3)] + [Tg(10, 5)]
        costas(F, 0.53, 0.70)
        P(F, 0.53, 0.70, cod, linhas)
    # faixa central entre as janelas do teto (O7 luzes, O8 comunicacao)
    costas(frame_teto(31.845, 0, 0.13), 0.52, 0.33)
    costas(frame_teto(31.495, 0, 0.13), 0.52, 0.33)
    P(frame_teto(31.845, 0, 0.13), 0.52, 0.33, "O7", [Ti("LIGHTING"),
      Tg(6, 3, ["FLOOD", "DOME", "PANEL LT", "INTEG LT", "ANNUN", "C/W"], int={0: "luz_flood", 1: "luz_dome"}), Kn(4, 4, ["FLOOD", "DOME", "PANEL LT", "INTEG LT"])])
    P(frame_teto(31.495, 0, 0.13), 0.52, 0.33, "O8", [Ti("AUDIO / COMM"),
      Tg(6, 3, ["AUDIO", "UHF", "S BAND", "KU BAND", "CCTV", "TIMER"], int={0: "comm"}), Kn(4, 4, ["AUDIO", "UHF", "S BAND", "KU BAND"])])
    # paineis altos laterais (entre a janela lateral e as do teto)
    for sgn, cods in ((1, ("O1", "O2", "O3")), (-1, ("O4", "O5", "O6"))):
        ta, tb = (44, 69) if sgn > 0 else (111, 136)
        for xc, cod in zip((31.43, 31.79, 32.13), cods):
            F, h = frame_lado_alto(xc, ta, tb)
            st("Estrutura", 0.006).caixa((0, 0, -0.06), (0.36, h + 0.04, 0.1), M.parede_esc, M=F)
            if cod in ("O1", "O2", "O3"):
                linhas = [Ti({"O1": "ATMOS", "O2": "CRYO", "O3": "RCS / OMS"}[cod]),
                          Md(4, 4, {"O1": ["CABIN", "O2 SPLY", "N2 SPLY", "H2O LOOP"], "O2": ["CRYO O2", "CRYO H2", "FC 1", "FC 2"],
                                    "O3": ["RCS F", "RCS L", "RCS R", "OMS L"]}[cod]), Sl(3, 3), Tg(6, 3), An(6, 3)]
            else:
                linhas = [Ti({"O4": "POWER", "O5": "HEATERS", "O6": "AVIONICS"}[cod]), Tg(6, 3), Dj(12, 3), Tg(6, 3), Kn(4, 4), An(6, 3)]
            P(F, 0.34, h - 0.02, cod, linhas)


# ======================================================================= consoles laterais (L1/L2, R1/R2)
def laterais():
    for sgn in (1, -1):
        y0, y1 = 0.98 * sgn, 1.62 * sgn
        zt = lambda y: 3.50 + (abs(y) - 0.98) * 0.27
        m = st("Estrutura", 0.006)
        hexa(m, [((30.35, 31.42)[i], (y0, y1)[j], (FZ, zt((y0, y1)[j]) - 0.03)[k]) for k in (0, 1) for j in (0, 1) for i in (0, 1)],
             M.parede_esc)
        # forro de fechamento ate a parede, com prateleira
        m.caixa((30.88, sgn * 2.05, (FZ + 4.32) / 2), (1.08, 0.84, 4.32 - FZ), M.parede_esc)
        n = V((0, -sgn * 0.27, 1)).normalized()
        Fc = quadro((30.885, (y0 + y1) / 2, zt((y0 + y1) / 2)), n, (1, 0, 0))
        wc = 0.64 / math.cos(math.atan(0.27)) - 0.01
        Fu = quadro((30.83, sgn * 1.645, 3.99), V((0, -sgn, 0.16)), (0, 0, 1))
        if sgn > 0:
            P(Fc, wc, 1.06, "L2", [Ti("FLIGHT CONTROL"), Tg(12, 4, ["SPD BK", "BODY FLAP", "TRIM", "NWS"]),
                                  Bt(8, 4, cores=("verde", "apag")), Dj(20, 4), Kn(6, 6), An(10, 5, o=12)], v_bot=-0.53, v_top=0.06)
            sbtc_thc(Fc)
            P(Fu, 1.0, 0.56, "L1", [Ti("ENVIRONMENTAL"), An(20, 5, o=4), Tg(16, 4, ["CABIN FAN", "AV BAY", "FREON", "H2O LOOP"],
                                   int={0: "ventilacao"}), Bt(10, 5, ["FIRE SUPPR", "SMOKE DET"], cores=("verm", "ambar"), leg=["DISCH", "ARM"]),
                                   Dj(36, 4), Kn(10, 10)])
        else:
            P(Fc, wc, 1.06, "R2", [Ti("APU / HYDRAULICS"),
                                  Tg(6, 3, ["APU 1", "APU 2", "APU 3", "HYD 1", "HYD 2", "HYD 3"], pos=("START", "OFF"),
                                     int={0: "apu1", 1: "apu2", 2: "apu3"}),
                                  An(12, 6, o=50), Md(6, 6, ["APU 1", "APU 2", "APU 3", "HYD 1", "HYD 2", "HYD 3"]),
                                  Tg(12, 4, ["HEATER", "APU 1", "APU 2", "APU 3"]), Dj(20, 4), Kn(6, 6)])
            P(Fu, 1.0, 0.56, "R1", [Ti("ELECTRICAL POWER"), Md(8, 8, ["MN A", "MN B", "MN C", "FC 1", "FC 2", "FC 3", "AC 1", "AC 2"]),
                                   Tg(6, 3, ["MN A", "MN B", "MN C", "FC 1", "FC 2", "FC 3"], int={0: "mn_a", 1: "mn_b", 2: "mn_c", 3: "fuel_cells"}),
                                   An(15, 5, o=20), Tg(16, 4, ["AC 1", "AC 2", "AC 3", "ESS 1BC"]), Dj(36, 4)])


def sbtc_thc(F):
    """SBTC (alavanca de freio aerodinamico/empuxo) e THC (controle de translacao), na parte da frente do L2."""
    m = st("Controles", 0.003)
    # SBTC: trilho, alavanca e punho em T
    m.caixa((0.14, 0.33, 0.006), (0.07, 0.30, 0.012), M.escuro, M=F)
    m.caixa((0.14, 0.33, 0.0125), (0.014, 0.25, 0.002), M.moldura, M=F)
    for k in range(9):
        etiqueta(st("Paineis", 0.003), F, 0.19, 0.22 + k * 0.028, str(k + 1) if k % 2 == 0 else "", larg=0.016)
    p0, p1 = F @ V((0.14, 0.38, 0.012)), F @ V((0.14, 0.42, 0.17))
    m.cilindro(p0, p1, 0.009, M.metal, 12)
    m.caixa((0.14, 0.425, 0.18), (0.11, 0.035, 0.03), M.escuro, M=F)
    m.cilindro(F @ V((0.14, 0.425, 0.195)), F @ V((0.14, 0.425, 0.203)), 0.007, M.vermelho, 12)
    etiqueta(st("Paineis", 0.003), F, 0.14, 0.50, "SPD BK", larg=0.05)
    # THC: base, fole e cabo em T
    c = F @ V((-0.12, 0.32, 0.0))
    nF = F.col[2].xyz
    m.torno([(0.045, 0.0), (0.045, 0.012), (0.036, 0.018)], M.escuro, segs=16, M=F @ T(-0.12, 0.32, 0))
    m.torno([(0.03, 0.018), (0.034, 0.03), (0.026, 0.042), (0.03, 0.054), (0.018, 0.066)], M.escuro, segs=16, M=F @ T(-0.12, 0.32, 0),
            fechar_base=False)
    m.cilindro(c + nF * 0.06, c + nF * 0.13, 0.009, M.metal, 12)
    m.caixa((-0.12, 0.32, 0.14), (0.10, 0.026, 0.026), M.tecla, M=F)
    etiqueta(st("Paineis", 0.003), F, -0.12, 0.40, "THC", larg=0.04)


# ======================================================================= assentos, RHC e pedais
def assento(y, X0=31.0):
    m = st("Assentos", 0.008)
    for s in (-1, 1):
        m.caixa((X0 - 0.05, y + s * 0.16, FZ + 0.015), (1.1, 0.04, 0.03), M.metal)
        for k in range(6):
            KIT.por("paraf", T(X0 - 0.55 + k * 0.2, y + s * 0.16, FZ + 0.03))
    m.caixa((X0 + 0.05, y, (FZ + 3.27) / 2 + 0.015), (0.36, 0.30, 3.27 - FZ - 0.03), M.estrutura)
    m.caixa((X0 + 0.10, y, 3.31), (0.55, 0.55, 0.07), M.estrutura)
    m.caixa((X0 + 0.11, y, 3.40), (0.52, 0.50, 0.11), M.tecido)
    Mb = T(X0 - 0.17, y, 3.97) @ R(rad(-6), "Y")
    m.caixa((-0.03, 0, 0), (0.06, 0.55, 1.02), M.estrutura, M=Mb)
    m.caixa((0.045, 0, -0.02), (0.09, 0.50, 0.94), M.tecido, M=Mb)
    for s in (-1, 1):                                   # abas laterais do encosto
        m.caixa((0.06, s * 0.255, 0.05), (0.12, 0.03, 0.70), M.tecido, M=Mb)
    m.caixa((0.04, 0, 0.62), (0.13, 0.30, 0.24), M.tecido, M=Mb)    # apoio de cabeca
    for s in (-1, 1):
        m.cilindro(Mb @ V((-0.02, s * 0.1, 0.50)), Mb @ V((-0.02, s * 0.1, 0.54)), 0.012, M.metal, 12)
    # estrutura de tras
    mc = st("Controles", 0.003)
    for s in (-1, 1):
        mc.tubo([V((X0 + 0.0, y + s * 0.2, 3.27)), V((X0 - 0.30, y + s * 0.2, 3.30)), Mb @ V((-0.06, s * 0.2, 0.2))], 0.014, M.metal, 12)
    # cinto de 5 pontos
    mf = st("Cintos", 0.0)
    fv = 0.094
    buckle = V((X0 + 0.10, y, 3.462))
    for s in (-1, 1):
        pts = [Mb @ V((fv, s * 0.10, 0.44)), Mb @ V((fv, s * 0.09, 0.0)), Mb @ V((fv, s * 0.07, -0.42)),
               V((X0 - 0.02, y + s * 0.06, 3.462)), buckle + V((0, s * 0.03, 0))]
        faixa(mf, pts, 0.045, (0, 1, 0), M.cinto)
        mc.caixa(Mb @ V((fv + 0.004, s * 0.095, 0.1)), (0.012, 0.052, 0.03), M.metal)
        faixa(mf, [V((X0 - 0.10, y + s * 0.262, 3.44)), V((X0 - 0.02, y + s * 0.20, 3.462)), V((X0 + 0.06, y + s * 0.11, 3.462)),
                   buckle + V((0, s * 0.03, 0))], 0.045, (1, 0, 0), M.cinto)
    faixa(mf, [V((X0 + 0.37, y, 3.36)), V((X0 + 0.36, y, 3.462)), buckle], 0.045, (0, 1, 0), M.cinto)
    mc.cilindro(buckle, buckle + V((0, 0, 0.014)), 0.034, M.metal, 20)
    mc.caixa(buckle + V((0, 0, 0.016)), (0.05, 0.012, 0.006), M.escuro)
    # mangueira de O2/ventilacao do traje, presa na lateral
    mc.tubo([V((X0 + 0.05, y + 0.31 * (1 if y > 0 else -1), 3.30)), V((X0 + 0.15, y + 0.33 * (1 if y > 0 else -1), 3.20)),
             V((X0 + 0.15, y + 0.32 * (1 if y > 0 else -1), FZ + 0.05))], 0.016, M.escuro, 12)


def rhc(x, y):
    m = st("Controles", 0.003)
    m.caixa((x + 0.03, y, FZ + 0.01), (0.18, 0.14, 0.02), M.escuro)
    for su in (-1, 1):
        for sv in (-1, 1):
            KIT.por("paraf", T(x + 0.03 + su * 0.07, y + sv * 0.055, FZ + 0.02))
    m.cilindro((x + 0.04, y, FZ + 0.02), (x, y, 3.44), 0.028, M.escuro, 16)
    perfil = [(0.05, 0.0)] + [((0.047 if k % 2 else 0.038), 0.011 * (k + 1)) for k in range(7)] + [(0.03, 0.085)]
    m.torno(perfil, M.escuro, segs=16, M=T(x, y, 3.44))
    Mg = T(x, y, 3.52) @ R(rad(10), "Y")
    m.torno([(0.024, 0.0), (0.027, 0.03), (0.029, 0.07), (0.026, 0.105), (0.02, 0.122), (0.0, 0.128)], M.tecla, segs=16, M=Mg)
    m.caixa((0.028, 0, 0.065), (0.012, 0.02, 0.032), M.escuro, M=Mg)
    m.cilindro(Mg @ V((0, 0, 0.124)), Mg @ V((0, 0, 0.134)), 0.007, M.vermelho, 12)
    m.cilindro(Mg @ V((-0.012, 0.012, 0.11)), Mg @ V((-0.014, 0.016, 0.125)), 0.005, M.escuro, 10)


def pedais(y):
    m = st("Controles", 0.003)
    m.caixa((32.25, y, 3.16), (0.10, 0.34, 0.10), M.escuro)
    for s in (-1, 1):
        yy = y + s * 0.11
        m.cilindro((32.22, yy, 3.17), (32.10, yy, 3.12), 0.012, M.metal, 12)
        Mp = T(32.08, yy, 3.08) @ R(rad(-25), "Y")
        m.caixa((0, 0, 0), (0.018, 0.095, 0.20), M.metal, M=Mp)
        for k in range(4):
            m.caixa((-0.011, 0, -0.068 + k * 0.045), (0.004, 0.085, 0.012), M.escuro, M=Mp)


# ======================================================================= montagem do L1
def construir(tampa_re=True):
    casco_interno(tampa_re=tampa_re)
    janelas()
    piso()
    painel_frontal()
    console_central()
    teto()
    laterais()
    for y in (0.62, -0.62):
        assento(y)
        rhc(31.56, y)
        pedais(y)
    for nome, y in (("CDR", 0.62), ("PLT", -0.62)):
        MARCAS["SEAT_" + nome] = V((31.0, y, 3.46))
        MARCAS["OLHO_" + nome] = V((31.0, y, 4.23))
    # luzes (no Unity: poucas realtime + emissivos)
    luz((31.95, 0.62, 4.02), "#FFE2B8", 1.6, 0.9)
    luz((31.95, -0.62, 4.02), "#FFE2B8", 1.6, 0.9)
    luz((29.45, 0.0, 5.3), "#DCE8FF", 4.0, 1.0)
    luz((28.2, 0.0, 5.2), "#DCE8FF", 4.0, 0.8)
    st("Luzes").caixa((29.45, 0, B.teto(29.45) - 0.012), (0.30, 0.10, 0.02), M.luz)
    st("Luzes").caixa((28.2, 0, B.teto(28.2) - 0.012), (0.30, 0.10, 0.02), M.luz)
