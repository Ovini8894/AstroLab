"""
L2 - AFT FLIGHT DECK (estacao de re, janelas da baia de carga, braco robotico RMS) e MIDDECK
(lockers, escotilha lateral de bombordo, camara de descompressao, escada entre os decks, corrimaos,
velcro, checklists e feixes de cabos). Coordenadas do orbitador (X nariz, Y bombordo, Z teto).
"""
import math, random
from mathutils import Vector as V, Matrix
import ni_base as B
from ni_base import M, T, R, st, KIT, quadro, painel, etiqueta, friso, tela, hexa, faixa, luz, MARCAS, INSET, FZ, MZ, ANC
import ni_atlas as A
import ni_fd as FD
from ni_fd import P, Dj, Tg, Bt, An, Kn, Sl, Md, Tc, Ti, pares
from astrolab_lib import Malha

RNG = random.Random(2024)
rad = math.radians
XA = 26.8            # anteparo de re (cabine)
XM = 31.5            # parede da frente do middeck (lockers)
ANIM = []            # [(Malha, nome)]: objetos com pivo proprio (exportados separados)


# ======================================================================= anteparos planos (recortados pelo casco)
def antepara(x, sent, z0, z1, furos=(), mat=None, grupo="Casco", passo=0.12):
    """placa plana em X = x entre z0 e z1, limitada pelo casco interno; furos = [(y0, y1, z0, z1)]."""
    m = st(grupo)
    mat = mat or M.parede
    zs = {z0, z1}
    for f in furos:
        zs.update((f[2], f[3]))
    k = 1
    while z0 + k * passo < z1:
        zs.add(z0 + k * passo)
        k += 1
    zs = sorted(z for z in zs if z0 <= z <= z1)
    for za, zb in pares(zs):
        wa, wb = B.meia_largura(x, za), B.meia_largura(x, zb)
        if wa < 0.01 and wb < 0.01:
            continue
        cortes = sorted((f[0], f[1]) for f in furos if f[2] <= za + 1e-6 and zb <= f[3] + 1e-6)
        ya, yb = -wa, -wb
        trechos = []
        for c0, c1 in cortes:
            trechos.append(((ya, yb), (c0, c0)))
            ya = yb = c1
        trechos.append(((ya, yb), (wa, wb)))
        for (a0, b0), (a1, b1) in trechos:
            pts = [V((x, a0, za)), V((x, a1, za)), V((x, b1, zb)), V((x, b0, zb))]
            if sent < 0:
                pts.reverse()
            m.quad(*pts, mat)


def laje(x0, x1, z, esp=0.10, mat=None, grupo="Piso"):
    m = st(grupo, 0.004)
    xs = sorted(set([x0, x1] + [x0 + 0.3 * k for k in range(1, int((x1 - x0) / 0.3) + 1) if x0 + 0.3 * k < x1]))
    for xa, xb in pares(xs):
        def p(i, j, k):
            x = (xa, xb)[i]
            zz = (z - esp, z)[k]
            w = B.meia_largura(x, zz) + 0.01
            return (x, (-w, w)[j], zz)
        hexa(m, [p(i, j, k) for k in (0, 1) for j in (0, 1) for i in (0, 1)], mat or M.piso)
    mj = st("PisoJuntas")
    x = x0 + 0.3
    while x < x1:
        mj.caixa((x, 0, z + 0.001), (0.012, 2 * B.meia_largura(x, z), 0.002), M.moldura)
        x += 0.6


def corrimao(pts, r=0.016, apoios=None):
    """corrimao amarelo com pes (standoffs) ate a parede/forro."""
    m = st("Corrimaos", 0.0)
    m.tubo([V(p) for p in pts], r, M.amarelo, 8)
    for a, b in (apoios or []):
        m.cilindro(V(a), V(b), 0.009, M.metal, 8)
        m.cilindro(V(b), V(b) + (V(b) - V(a)).normalized() * 0.01, 0.022, M.metal, 8)


def velcro(m, F, u, v, w=0.035, h=0.022):
    m.quad(F @ V((u - w / 2, v - h / 2, 0.0012)), F @ V((u + w / 2, v - h / 2, 0.0012)), F @ V((u + w / 2, v + h / 2, 0.0012)),
           F @ V((u - w / 2, v + h / 2, 0.0012)), M.escuro)


def checklist(F, u, v, titulo, ang=0.0):
    """livreto de checklist preso com velcro: capa branca, faixa de titulo, espiral."""
    m = st("Checklists", 0.0)
    Fl = F @ T(u, v, 0.004) @ R(ang, "Z")
    m.caixa((0, 0, 0.004), (0.15, 0.21, 0.008), M.branco, M=Fl)
    m.caixa((0, -0.002, 0.0084), (0.13, 0.17, 0.0008), M.medidor, M=Fl)
    etiqueta(m, Fl, 0, 0.06, titulo, larg=0.12, z=0.0092)
    for k in range(4):
        etiqueta(m, Fl, 0, 0.02 - k * 0.022, ["NOMINAL", "ORBIT OPS", "CREW", "MET"][k], larg=0.07, z=0.0092)
    m.cilindro(Fl @ V((-0.075, 0.105, 0.008)), Fl @ V((0.075, 0.105, 0.008)), 0.005, M.metal, 8)


def feixe(pts, n=4, r=0.011, mat=None):
    """feixe de cabos: n tubos paralelos + abracadeiras."""
    m = st("Cabos", 0.0)
    pts = [V(p) for p in pts]
    d = (pts[-1] - pts[0]).normalized()
    lado = d.cross(V((0, 0, 1)))
    if lado.length < 0.1:
        lado = d.cross(V((0, 1, 0)))
    lado.normalize()
    cima = lado.cross(d).normalized()
    cores = [M.escuro, M.cinto, M.parede_esc, M.escuro, M.moldura]
    for k in range(n):
        off = lado * ((k % 2) - 0.5) * 2.2 * r + cima * ((k // 2) - 0.5) * 2.2 * r
        m.tubo([p + off for p in pts], r, mat or cores[k % len(cores)], 6)
    for a, b in pares(pts):
        L_ = (b - a).length
        for j in range(1, int(L_ / 0.45) + 1):
            c = a + (b - a) * (j * 0.45 / L_)
            Mx = Matrix((lado, (b - a).normalized(), cima)).transposed().to_4x4()
            Mx.translation = c
            m.caixa((0, 0, 0), (r * 6.5, 0.018, r * 6.5), M.metal, M=Mx)


# ======================================================================= aft flight deck
JAN_RE = [(-0.68, -0.24, 4.30, 4.74), (0.24, 0.68, 4.30, 4.74)]


def estacao_re():
    # anteparo com as 2 janelas para a baia de carga
    antepara(XA, 1, FZ - 0.12, B.teto(XA) - 0.002, furos=JAN_RE)
    antepara(XA, 1, MZ - 0.1, FZ - 0.12)
    mj = st("Janelas")
    for y0, y1, z0, z1 in JAN_RE:
        cy, cz, w, h = (y0 + y1) / 2, (z0 + z1) / 2, y1 - y0, z1 - z0
        Fj = quadro((XA, cy, cz), (1, 0, 0), (0, 0, 1))
        for (u, v, du, dv) in ((0, h / 2 + 0.03, w + 0.12, 0.06), (0, -h / 2 - 0.03, w + 0.12, 0.06),
                               (w / 2 + 0.03, 0, 0.06, h), (-w / 2 - 0.03, 0, 0.06, h)):
            mj.caixa((u, v, 0.025), (du, dv, 0.05), M.escuro, M=Fj)
        for (u, v, du, dv) in ((0, h / 2, w, 0.004), (0, -h / 2, w, 0.004), (w / 2, 0, 0.004, h), (-w / 2, 0, 0.004, h)):
            mj.caixa((u, v, -0.06), (du, dv, 0.12), M.moldura, M=Fj)
        st("Vidros").quad(Fj @ V((-w / 2, -h / 2, -0.10)), Fj @ V((w / 2, -h / 2, -0.10)), Fj @ V((w / 2, h / 2, -0.10)),
                          Fj @ V((-w / 2, h / 2, -0.10)), M.vidro, duplo=True)
        for su in (-1, 1):
            for sv in (-1, 1):
                KIT.por("paraf", Fj @ T(su * (w / 2 + 0.03), sv * (h / 2 + 0.03), 0.05))
    xf = XA + 0.04
    Fr = lambda y, z: quadro((xf, y, z), (1, 0, 0), (0, 0, 1))
    # monitores e telas (A1 AFD1, A3 CRT 3/4)
    F = Fr(-1.02, 4.45)
    painel(F, 0.62, 0.62, "A1")
    FD.mdu(F, -0.12, 0.08, A.P_CHECK, "AFD 1")
    FD.preencher(F, 0.62, 0.62, [An(4, 1, o=38), Bt(4, 1, cores=("branca", "apag"))], "A1", u_min=0.06, u_max=0.29,
                 v_top=0.24, v_bot=-0.06)
    FD.preencher(F, 0.62, 0.62, [Tg(8, 4, ["CCTV", "PL BAY", "RMS", "LIGHTS"]), Kn(6, 6)], "A1", v_top=-0.10)
    F = Fr(1.02, 4.45)
    painel(F, 0.62, 0.62, "A3")
    FD.mdu(F, -0.15, 0.08, A.P_BAIA, "CRT 3")
    FD.mdu(F, 0.15, 0.08, A.P_RMS, "CRT 4")
    FD.preencher(F, 0.62, 0.62, [Bt(10, 5, ["CCTV", "PL BAY"], cores=("apag", "verde"), leg=["SEL", "ON"]), Kn(6, 6)], "A3", v_top=-0.10)
    P(Fr(0, 4.52), 0.40, 0.50, "A2", [Ti("MISSION TIMER"), An(6, 3, o=56), Bt(6, 3, cores=("ambar", "apag")), Kn(4, 4, ["TIMER", "MET", "GMT", "INTEG LT"]),
                                      Tg(6, 3, ["TIMER", "MET", "GMT"], pos=("UP", "DOWN"))])
    # fileira de baixo
    P(Fr(-0.90, 3.84), 0.88, 0.50, "A6", [Ti("PAYLOAD / ORBITER"), An(15, 5, o=8), Tg(16, 4), Dj(28, 4), Kn(10, 10)])
    P(Fr(0.0, 3.84), 0.88, 0.50, "A7", [Ti("CCTV / LIGHTS"), Bt(16, 4, ["CCTV", "PL BAY", "RMS", "LIGHTS"], cores=("apag", "branca", "verde", "apag"),
                                       leg=["SEL", "A", "B", "C"]), Tg(16, 4, ["CCTV", "PL BAY", "FLOOD", "LIGHTS"]), Sl(8, 8), Kn(8, 8)])
    P(Fr(0.90, 3.84), 0.88, 0.50, "A8U", [Ti("REMOTE MANIPULATOR"), An(15, 5, o=26),
                                          Tg(12, 4, ["RMS", "PL BAY", "HEATER", "LIGHTS"], int={0: "rms"}), Sl(6, 6, ["RMS", "PL BAY", "CCTV", "MODE", "RATE", "JOINT"],
                                                                                                             ["SINGLE", "AUTO", "DIRECT"]),
                                          Md(8, 8, ["RMS", "PL BAY", "HEATER", "LIGHTS", "MN A", "MN B", "MN C", "AC 1"])])
    # bancada inclinada (A8L) com os controladores do braco
    m = st("Estrutura", 0.006)
    xb, zb, xt, zt_ = 27.22, 3.33, XA + 0.04, 3.56
    hexa(m, [((XA, xb)[i], (-1.32, 1.32)[j], (FZ, (zt_, zb)[i] - 0.03)[k]) for k in (0, 1) for j in (0, 1) for i in (0, 1)], M.parede_esc)
    d = V((xt - xb, 0, zt_ - zb))
    n = V((-d.z, 0, d.x)).normalized()
    if n.z < 0:
        n = -n
    Fd = quadro(((xb + xt) / 2, 0, (zb + zt_) / 2), n, d)
    P(Fd, 2.62, d.length - 0.01, "A8L", [Tg(24, 4), Bt(16, 4, cores=("verde", "apag", "branca", "ambar"))], u_min=-0.62, u_max=0.62)
    mc = st("Controles", 0.003)
    for uc, nome in ((-0.95, "THC"), (0.95, "RHC")):
        c = Fd @ V((uc, 0.0, 0.0))
        mc.torno([(0.05, 0.0), (0.05, 0.014), (0.04, 0.02)], M.escuro, segs=16, M=Fd @ T(uc, 0, 0))
        mc.torno([(0.034, 0.02), (0.038, 0.03), (0.03, 0.042), (0.034, 0.054), (0.022, 0.07)], M.escuro, segs=16, M=Fd @ T(uc, 0, 0), fechar_base=False)
        if nome == "RHC":
            Mg = Fd @ T(uc, 0, 0.07)
            mc.torno([(0.022, 0.0), (0.026, 0.03), (0.028, 0.07), (0.025, 0.10), (0.0, 0.11)], M.tecla, segs=16, M=Mg)
            mc.cilindro(Mg @ V((0, 0, 0.105)), Mg @ V((0, 0, 0.115)), 0.006, M.vermelho, 12)
        else:
            nn = Fd.col[2].xyz
            mc.cilindro(c + nn * 0.07, c + nn * 0.13, 0.009, M.metal, 12)
            mc.caixa((uc, 0, 0.14), (0.10, 0.026, 0.026), M.tecla, M=Fd)
        etiqueta(st("Paineis", 0.003), Fd, uc, -0.085, "RMS " + nome, larg=0.06)
    checklist(Fd, -0.35, 0.0, "RMS OPS", rad(4))
    # paineis laterais de re: L10/L11 (bombordo), R13 (estibordo: portas da baia e radiadores)
    for sgn in (1, -1):
        Fl = quadro((27.65, sgn * 2.26, 3.86), V((0, -sgn, 0.08)), (0, 0, 1))
        st("Estrutura", 0.006).caixa((27.65, sgn * 2.45, 3.86), (1.25, 0.36, 0.92), M.parede_esc)
        if sgn > 0:
            P(Fl @ T(-0.30 * sgn, 0, 0), 0.58, 0.82, "L10", [Ti("PAYLOAD"), An(10, 5, o=2), Tg(12, 4), Dj(20, 4), Dj(20, 4), Kn(6, 6)])
            P(Fl @ T(0.31 * sgn, 0, 0), 0.58, 0.82, "L11", [Ti("PAYLOAD COMM"), Tg(12, 4, ["KU BAND", "S BAND", "UHF", "AUDIO"]), Sl(4, 4), Dj(20, 4),
                                                           Bt(8, 4, cores=("verde", "apag")), Kn(6, 6)])
        else:
            P(Fl @ T(-0.30 * sgn, 0, 0), 0.58, 0.82, "R13U", [Ti("PL BAY DOORS"),
                                                              Tg(6, 3, ["PL BAY", "VENT DOOR", "LIGHTS", "RADIATOR", "HEATER", "FREON"],
                                                                 pos=("OPEN", "CLOSE"), int={0: "portas_baia", 3: "radiadores"}),
                                                              An(10, 5, o=42), Md(5, 5, ["PL BAY", "RADIATOR", "FREON", "HEATER", "VENT DOOR"]), Dj(20, 4)])
            P(Fl @ T(0.31 * sgn, 0, 0), 0.58, 0.82, "R13L", [Ti("PL BAY MECH"), Tg(12, 4, ["PL BAY", "VENT DOOR", "LIGHTS", "HEATER"], pos=("OPEN", "CLOSE")),
                                                            Bt(8, 4, cores=("verde", "ambar"), leg=["OP", "CL"]), Dj(20, 4), Kn(6, 6)])
    # assentos dos especialistas de missao (MS1 / MS2)
    for y in (0.52, -0.52):
        FD.assento(y, X0=29.72)
    MARCAS["SEAT_MS1"] = V((29.72, 0.52, 3.46))
    MARCAS["SEAT_MS2"] = V((29.72, -0.52, 3.46))
    MARCAS["ESTACAO_RE"] = V((27.6, 0.0, 4.5))
    luz((27.5, 0.0, 5.0), "#FFE2B8", 2.5, 0.9)
    st("Luzes").caixa((27.4, 0, B.teto(27.4) - 0.012), (0.30, 0.10, 0.02), M.luz)


def baia():
    """vista pelas janelas de re: baia de carga com portas abertas, longarinas e o braco RMS."""
    m = st("Baia", 0.0)
    x0, x1 = 17.5, XA - 0.05
    ts = list(range(190, 351, 10))
    for ta, tb in pares(ts):
        a0, b0 = B.casco(x0, ta, 0.30), B.casco(x0, tb, 0.30)
        a1, b1 = B.casco(x1, ta, 0.30), B.casco(x1, tb, 0.30)
        m.quad(a0, a1, b1, b0, M.parede, duplo=True)
    for k in range(1, 12):                       # cavernas da baia
        x = x0 + k * (x1 - x0) / 12
        for ta, tb in pares(ts):
            m.quad(B.casco(x, ta, 0.30), B.casco(x, tb, 0.30), B.casco(x, tb, 0.42), B.casco(x, ta, 0.42), M.parede_esc, duplo=True)
    for s in (-1, 1):
        a, b, zc, n = B.secao(22.0)
        y = s * (a - 0.18)
        m.caixa(((x0 + x1) / 2, y, zc), (x1 - x0, 0.30, 0.28), M.parede_esc)
        Md_ = T((x0 + x1) / 2, s * (a + 0.02), zc + 0.12) @ R(s * rad(28), "X")
        m.caixa((0, s * 1.35, 0), (x1 - x0, 2.7, 0.07), M.branco, M=Md_)
        for k in range(4):                        # paineis do radiador
            m.caixa((x0 - (x0 + x1) / 2 + 1.1 + k * 2.2, s * 1.35, s * 0.0 + 0.04), (2.0, 2.4, 0.012), M.metal, M=Md_)
    # braco robotico (RMS) no bombordo, recolhido
    mr = st("Baia", 0.0)
    a, b, zc, n = B.secao(22.0)
    y, z = a - 0.45, zc + 0.30
    pts = [V((25.9, y, z)), V((22.2, y, z)), V((18.4, y - 0.05, z + 0.02))]
    for p, q in pares(pts):
        mr.cilindro(p, q, 0.17, M.branco, 20)
    for p in pts:
        mr.cilindro(p - V((0.22, 0, 0)), p + V((0.22, 0, 0)), 0.21, M.escuro, 20)
    mr.cilindro(pts[-1], pts[-1] - V((0.55, 0, 0)), 0.14, M.metal, 16)
    mr.caixa((25.9, y + 0.2, z - 0.2), (0.4, 0.3, 0.3), M.escuro)


# ======================================================================= middeck
COLS_LOCKER = ["MF14", "MF28", "MF43", "MF57", "MF71", "MA16"]
LETRAS = "ABCDEFGH"


def middeck():
    # piso, parede da frente e anteparo de re
    laje(XA, XM, MZ)
    antepara(XM, -1, MZ, FZ - 0.10)
    # lockers modulares na parede da frente (6 colunas x 6 fileiras)
    Fw = lambda y, z: quadro((XM - 0.002, y, z), (-1, 0, 0), (0, 0, 1))
    ml = st("Lockers", 0.004)
    mf = st("Molduras")
    W, H = 0.44, 0.27
    z0 = 0.80
    cols = [(c - 2.5) * W for c in range(6)]
    for ci, y in enumerate(cols):
        for r in range(6):
            z = z0 + r * H + H / 2
            F = Fw(-y, z)            # u+ = -Y: coluna 0 fica a esquerda de quem olha para a frente
            mf.caixa((0, 0, 0.01), (W, H, 0.02), M.moldura, M=F)
            ml.caixa((0, 0, 0.028), (W - 0.022, H - 0.022, 0.02), M.parede, M=F)
            Fp = F @ T(0, 0, 0.038)
            for su in (-1, 1):
                KIT.por("trava", Fp @ T(su * 0.16, 0, 0))
            etiqueta(ml, Fp, -0.13, H / 2 - 0.035, f"{COLS_LOCKER[ci]}{LETRAS[r]}", larg=0.07)
            friso(ml, Fp, -W / 2 + 0.03, -H / 2 + 0.03, W / 2 - 0.03, H / 2 - 0.03, w=0.002, mat=M.parede_esc)
            for k in range(RNG.randint(0, 3)):
                velcro(ml, Fp, RNG.uniform(-0.12, 0.12), RNG.uniform(-0.08, 0.06))
    # avionicos dos lados dos lockers: placas com grelhas de ventilacao
    for s in (-1, 1):
        for r in range(3):
            z = 0.95 + r * 0.55
            y = s * 1.55
            w = min(0.40, B.meia_largura(XM, z + 0.25) - 1.35 - 0.04)
            if w < 0.12:
                continue
            F = Fw(-(s * (1.34 + w / 2)), z + 0.25)
            painel(F, w, 0.50, f"AV BAY", grupo="Paineis", parafusos=True)
            for k in range(8):
                st("Paineis", 0.003).caixa((0, 0.16 - k * 0.04, 0.003), (w - 0.08, 0.012, 0.006), M.moldura, M=F)
    for (u, z, tit) in ((-0.66, 2.0, "POST INSERTION"), (0.44, 1.46, "DEORBIT PREP"), (1.10, 2.27, "MIDDECK OPS")):
        checklist(Fw(-u, z), 0, 0, tit, rad(RNG.uniform(-5, 5)))
    corrimao([(XM - 0.08, -1.2, 2.50), (XM - 0.08, 1.2, 2.50)],
             apoios=[((XM - 0.08, y, 2.50), (XM - 0.002, y, 2.50)) for y in (-1.1, 0, 1.1)])
    escotilha()
    airlock()
    escada()
    # beliches (sacos de dormir) na parede de estibordo e banheiro (WCS)
    ms = st("Assentos", 0.008)
    for k, x in enumerate((30.95, 30.25)):
        y = -B.meia_largura(x, 1.7) + 0.10
        Fb = quadro((x, y, 1.70), (0, 1, 0), (0, 0, 1))
        ms.caixa((0, 0, 0.06), (0.62, 1.50, 0.12), M.tecido, M=Fb)
        ms.caixa((0, 0.52, 0.125), (0.40, 0.30, 0.03), M.estrutura, M=Fb)
        for v in (-0.45, 0.0, 0.45):
            faixa(st("Cintos"), [Fb @ V((-0.32, v, 0.13)), Fb @ V((0.32, v, 0.13))], 0.04, Fb.col[1].xyz, M.cinto)
        for su in (-1, 1):
            for sv in (-1, 1):
                st("Controles", 0.003).cilindro(Fb @ V((su * 0.3, sv * 0.73, 0.0)), Fb @ V((su * 0.3, sv * 0.73, -0.12)), 0.01, M.metal, 8)
    yw = -B.meia_largura(28.9, 1.6) + 0.02
    mw = st("Estrutura", 0.006)
    mw.caixa((28.95, (yw + -1.30) / 2, (MZ + 2.45) / 2), (0.95, abs(yw + 1.30), 2.45 - MZ), M.parede_esc)
    Fc = quadro((28.95, -1.29, 1.55), (0, 1, 0), (0, 0, 1))
    for k in range(9):                           # cortina franzida
        st("Assentos", 0.008).caixa((-0.40 + k * 0.1, 0, 0.012 + (k % 2) * 0.02), (0.1, 1.7, 0.024), M.tecido, M=Fc)
    etiqueta(st("Paineis", 0.003), Fc @ T(0, 0.92, 0.05), 0, 0, "WCS", larg=0.10)
    # feixes de cabos no canto forro/parede e luzes
    for s in (-1, 1):
        y = s * (B.meia_largura(29.0, 2.65) - 0.06)
        feixe([(XA + 0.1, y, 2.66), (XM - 0.1, y, 2.66)])
    for x in (28.3, 30.3):
        luz((x, 0, 2.6), "#DCE8FF", 3.5, 0.9)
        st("Luzes").caixa((x, 0, FZ - 0.105), (0.6, 0.12, 0.01), M.luz)
    corrimao([(28.6, 0.4, 2.68), (31.3, 0.4, 2.68)], apoios=[((x, 0.4, 2.68), (x, 0.4, FZ - 0.10)) for x in (28.8, 30.0, 31.1)])
    corrimao([(28.6, -0.4, 2.68), (31.3, -0.4, 2.68)], apoios=[((x, -0.4, 2.68), (x, -0.4, FZ - 0.10)) for x in (28.8, 30.0, 31.1)])
    MARCAS["MIDDECK"] = V((29.6, 0.0, 1.5))


def escotilha():
    """escotilha lateral de bombordo (vao X 29.7-30.7, Z 1.3-2.3): moldura e porta que abre dobrando para baixo."""
    xc, zc = 30.2, 1.8
    yi = B.meia_largura(xc, zc) + 0.0
    F = quadro((xc, yi - 0.005, zc), (0, -1, 0), (0, 0, 1))
    m = st("Escotilha", 0.006)
    for (u, v, du, dv) in ((0, 0.56, 1.24, 0.12), (0, -0.56, 1.24, 0.12), (0.56, 0, 0.12, 1.0), (-0.56, 0, 0.12, 1.0)):
        m.caixa((u, v, 0.03), (du, dv, 0.08), M.moldura, M=F)
    for k in range(16):
        a = 2 * math.pi * k / 16
        KIT.por("paraf", F @ T(0.58 * math.cos(a), 0.58 * math.sin(a), 0.07))
    # faixas de aviso na soleira
    for k in range(10):
        m.caixa((-0.45 + k * 0.1, -0.60, 0.072), (0.05, 0.04, 0.004), M.amarelo, M=F)
    etiqueta(m, F, 0, 0.64, "SIDE HATCH", larg=0.16, z=0.072)
    # porta (objeto ANIM: pivo na dobradica de baixo, gira em torno do eixo X do orbitador)
    piv = F @ V((0, -0.50, 0.05))
    md = Malha("ANIM_EscotilhaLateral", origem=tuple(piv))
    md.chanfro = 0.006
    Fd = F @ T(0, 0, 0.05)
    md.torno([(0.0, 0.0), (0.47, 0.0), (0.49, 0.02), (0.49, 0.05), (0.46, 0.07), (0.0, 0.07)], M.parede_esc, segs=32, M=Fd,
             fechar_base=False, fechar_topo=False)
    md.torno([(0.40, 0.071), (0.40, 0.085), (0.36, 0.09)], M.metal, segs=32, M=Fd, fechar_base=False, fechar_topo=False)
    # janela
    md.cilindro(Fd @ V((0, 0.2, 0.07)), Fd @ V((0, 0.2, 0.10)), 0.10, M.escuro, 24)
    md.cilindro(Fd @ V((0, 0.2, 0.101)), Fd @ V((0, 0.2, 0.104)), 0.075, M.vidro, 24)
    # manivela de travamento
    md.cilindro(Fd @ V((0, -0.08, 0.07)), Fd @ V((0, -0.08, 0.12)), 0.035, M.metal, 16)
    md.cilindro(Fd @ V((0, -0.08, 0.115)), Fd @ V((0.20, -0.08, 0.115)), 0.012, M.metal, 10)
    md.cilindro(Fd @ V((0.20, -0.08, 0.115)), Fd @ V((0.20, -0.08, 0.17)), 0.016, M.amarelo, 12)
    for a in (0, 120, 240):                     # trincos
        ang = rad(a + 30)
        md.caixa((0.42 * math.cos(ang), 0.42 * math.sin(ang), 0.08), (0.06, 0.03, 0.02), M.metal, M=Fd @ R(0, "Z"))
    md.cilindro(Fd @ V((-0.3, -0.48, 0.0)), Fd @ V((0.3, -0.48, 0.0)), 0.03, M.metal, 16)       # dobradica
    etiqueta(st("Paineis", 0.003), Fd @ T(0, -0.25, 0.071), 0, 0, "PUSH / ROTATE", larg=0.12)
    ANIM.append((md, "ANIM_EscotilhaLateral"))
    MARCAS["ESCOTILHA"] = V((xc, yi - 0.4, zc))
    corrimao([(29.55, yi - 0.12, 1.25), (29.55, yi - 0.12, 2.35)], apoios=[((29.55, yi - 0.12, z), (29.55, yi, z)) for z in (1.35, 2.25)])
    corrimao([(30.85, yi - 0.12, 1.25), (30.85, yi - 0.12, 2.35)], apoios=[((30.85, yi - 0.12, z), (30.85, yi, z)) for z in (1.35, 2.25)])


def airlock():
    """camara de descompressao interna (a re, no centro-estibordo), com escotilha D voltada para a frente."""
    cx, cy, r = 27.62, -0.48, 0.80
    m = st("Airlock", 0.006)
    base = T(cx, cy, MZ)
    m.torno([(r, 0.0), (r, FZ - 0.10 - MZ)], M.parede, segs=40, M=base, fechar_base=False, fechar_topo=False)
    for z in (0.10, 1.05, 2.0):
        m.torno([(r, z), (r + 0.025, z + 0.01), (r + 0.025, z + 0.06), (r, z + 0.07)], M.parede_esc, segs=40, M=base,
                fechar_base=False, fechar_topo=False)
    # escotilha (D) na face da frente
    F = quadro((cx + r + 0.02, cy, 1.55), (1, 0, 0), (0, 0, 1))
    m.torno([(0.0, 0.0), (0.50, 0.0), (0.52, 0.02), (0.52, 0.06), (0.0, 0.06)], M.parede_esc, segs=32, M=F, fechar_base=False, fechar_topo=False)
    m.torno([(0.53, -0.02), (0.58, -0.02), (0.58, 0.03), (0.53, 0.03)], M.moldura, segs=32, M=F, fechar_base=False, fechar_topo=False)
    m.cilindro(F @ V((0, 0, 0.06)), F @ V((0, 0, 0.11)), 0.05, M.metal, 16)
    m.cilindro(F @ V((-0.22, 0, 0.10)), F @ V((0.22, 0, 0.10)), 0.016, M.metal, 10)
    for su in (-1, 1):
        m.cilindro(F @ V((su * 0.22, 0, 0.10)), F @ V((su * 0.22, 0, 0.16)), 0.02, M.amarelo, 12)
        KIT.por("knob_g", F @ T(su * 0.30, 0.30, 0.06))                    # valvulas de equalizacao
    m.cilindro(F @ V((0, 0.30, 0.06)), F @ V((0, 0.30, 0.09)), 0.08, M.escuro, 24)
    st("Vidros").quad(F @ V((-0.05, 0.25, 0.092)), F @ V((0.05, 0.25, 0.092)), F @ V((0.05, 0.35, 0.092)), F @ V((-0.05, 0.35, 0.092)), M.vidro)
    for k in range(12):
        a = 2 * math.pi * k / 12
        KIT.por("paraf", F @ T(0.555 * math.cos(a), 0.555 * math.sin(a), 0.03))
    etiqueta(st("Paineis", 0.003), F, 0, -0.38, "AIRLOCK", larg=0.16, z=0.062)
    painel(F @ T(0, 0.72, -0.01), 0.42, 0.16, "AW18", grupo="Paineis")
    FD.preencher(F @ T(0, 0.72, -0.01), 0.42, 0.16, [Tg(6, 3, ["DEPRESS", "EQUAL", "LIGHTS"], pos=("OPEN", "CLOSE")),
                                                     ], "AW18", v_top=0.04)
    corrimao([(cx + r + 0.10, cy - 0.70, 0.95), (cx + r + 0.10, cy - 0.70, 2.15)],
             apoios=[((cx + r + 0.10, cy - 0.70, z), (cx + r - 0.10, cy - 0.70, z)) for z in (1.05, 2.05)])
    MARCAS["AIRLOCK"] = V((cx + r + 0.4, cy, 1.5))


def escada():
    """escada entre os decks pelo acesso de bombordo (piso do flight deck)."""
    m = st("Corrimaos", 0.0)
    x0 = FD.FURO[0] + 0.12
    for y in (FD.FURO[2] + 0.08, FD.FURO[3] - 0.08):
        m.tubo([V((x0 + 0.10, y, MZ)), V((x0, y, FZ - 0.1)), V((x0, y, FZ + 0.62))], 0.02, M.amarelo, 12)
    z = MZ + 0.25
    while z < FZ + 0.5:
        t = (z - MZ) / (FZ - 0.1 - MZ)
        x = x0 + 0.10 * (1 - min(1, t))
        m.cilindro((x, FD.FURO[2] + 0.08, z), (x, FD.FURO[3] - 0.08, z), 0.014, M.metal, 10)
        z += 0.28
    MARCAS["ESCADA"] = V((x0 + 0.4, (FD.FURO[2] + FD.FURO[3]) / 2, 1.5))


# ======================================================================= velcro e checklists no flight deck
def extras_fd():
    # livretos sobre as prateleiras laterais (topo do fechamento dos consoles, Z 4.32)
    for y, tit in ((1.95, "ASCENT CHECKLIST"), (-1.95, "ENTRY CHECKLIST")):
        checklist(quadro((30.75, y, 4.32), (0, 0, 1), (1, 0, 0)), 0, 0, tit, rad(RNG.uniform(-8, 8)))


def construir():
    estacao_re()
    baia()
    middeck()
    extras_fd()
