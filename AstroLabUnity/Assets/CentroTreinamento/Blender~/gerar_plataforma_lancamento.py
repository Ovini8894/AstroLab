"""
Plataforma de lancamento (inspirada no LC-39A) + onibus espacial (orbitador, tanque externo, 2 boosters).

  blender -b --factory-startup --python gerar_plataforma_lancamento.py -- --fbx <Plataforma.fbx> --fbx-onibus <Onibus.fbx>
          [--preview <pasta>] [--vista=nome]

Mesmo sistema de coordenadas do gerar_centro_treinamento.py (a estrada sai do hangar em x = 44 e chega em y = 200).
Escala real aproximada (m): orbitador 37 m, asa 24 m, tanque externo 47 x 8,4 m, boosters 45,5 x 3,7 m, torre ~80 m.

Objetos para o Unity:
  Plataforma_*            concreto, rampa, MLP, torre (FSS), estrutura rotativa, caixa d'agua, postes de luz
  ANIM_Elevador           cabine do elevador da torre (sobe do deck ate o braco de acesso)
  Onibus_Orbitador, Onibus_TanqueExterno, Onibus_Boosters   exterior (LOD0)
  Onibus_Interior         cabine de comando (liga so quando o jogador esta perto da escotilha)
  COL_*, ZONA_*, INT_*, WP_*, SEAT_*   colisores, gatilhos e marcadores
"""
import sys, os, math, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from mathutils import Vector as V, Matrix
from astrolab_lib import (Malha, material, vazio, colisor, limpar_cena, colecao, preparar_render, renderizar,
                          remover_preview, exportar_fbx, contar_tris)

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
def arg(n):
    return args[args.index(n) + 1] if n in args else None

limpar_cena()
COL = colecao("Plataforma")
PI = math.pi
rnd = random.Random(7)
TEX = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Texturas", "T_Logo_NASA.png"))

# ======================================================================= materiais
BRANCO_T = material("OR_Branco", "#F4F2EC")          # telhas brancas do orbitador
PRETO_T = material("OR_Preto", "#2A2C38")            # telhas pretas (barriga)
RCC = material("OR_Cinza_RCC", "#5B5F70")            # bordo de ataque / nariz
ET = material("OR_Laranja_ET", "#D9772E")
ET_ESC = material("OR_Laranja_ET_Escuro", "#B05A22")
SRB = material("OR_SRB", "#EDEBE4")
SRB_ESC = material("OR_SRB_Escuro", "#3A3D4A")
MOTOR = material("OR_Motor", "#7E879A")
MOTOR_INT = material("OR_Motor_Interno", "#2E3550")
VIDRO_CAB = material("OR_Vidro_Cabine", "#1E2A44", alpha=0.55)
TORRE = material("OR_Torre", "#B5452F")
TORRE_CINZA = material("OR_Torre_Cinza", "#8E96A8")
CONCRETO = material("OR_Concreto", "#B8B4A8")
CONCRETO_ESC = material("OR_Concreto_Escuro", "#6E6B66")
GRADE = material("OR_Grade", "#7D8696")
AMARELO = material("CT_Amarelo", "#FFC23A")
VERMELHO = material("CT_Vermelho_NASA", "#E03C2A")
AZUL_ESC = material("CT_Azul_Escuro", "#17306E")
BRANCO = material("CT_Branco", "#F8F5EE")
METAL = material("CT_Metal", "#B4C2D9")
ESCURO = material("CT_Console", "#2E3550")
LUZ = material("CT_Luz", "#FFF4DE", emissao=4.0)
LUZ_VERM = material("CT_Luz_Vermelha", "#FF4A4A", emissao=4.0)
TXT_ESC = material("CT_Texto_Escuro", "#1C1A3D")
TXT_BR = material("CT_Texto_Branco", "#FFFFFF")
LOGO = material("CT_Logo_NASA", "#FFFFFF", alpha=0.99, imagem=TEX)
VIDRO = material("CT_Vidro", "#A8ECFF", alpha=0.3)
# interior
INT_PAREDE = material("OR_Interior", "#C9CCD2")
INT_PISO = material("OR_Piso_Cabine", "#5D667A")
PAINEL = material("OR_Painel", "#3B4256")
PAINEL_CL = material("OR_Painel_Claro", "#7A849A")
MFD = material("OR_Tela_MFD", "#47E6FF", emissao=1.5)
ASSENTO = material("OR_Assento", "#4E5B78")
CINTO = material("OR_Cinto", "#C9A64A")
LUZ_VERDE = material("CT_Luz_Verde", "#59FF86", emissao=4.0)
LUZ_AMBAR = material("CT_Luz_Ambar", "#FFB45C", emissao=4.0)

OBJS = []
def fim(m, uv=1.0):
    o = m.criar(COL, uv)
    if o:
        OBJS.append(o)
    t = getattr(m, "irmao_transp", None)
    if t:
        OBJS.append(t)
    return o

def emp(nome, pos, rz=0.0):
    OBJS.append(vazio(nome, pos, COL, rz))

def col(nome, c, d, rz=0.0):
    OBJS.append(colisor(nome, c, d, COL, rz))

def zona(nome, c, d):
    o = colisor(nome, c, d, COL); o.name = nome; OBJS.append(o)

# ======================================================================= layout
SX, SY = 44.0, 290.0             # eixo do tanque externo
DECK = 6.0                       # topo do deck de concreto
ZM = DECK + 7.5                  # topo da MLP (plataforma movel)
ORB_Z0 = ZM + 6.0                # cauda do orbitador
ORB_Y_BARRIGA = SY - 5.7         # plano da barriga do orbitador (de frente para o tanque)
# orbitador: X local (frente) -> +Z mundo, Y local (esquerda/bombordo) -> -X mundo, Z local (cima) -> -Y mundo
M_ORB = Matrix.Translation(V((SX, ORB_Y_BARRIGA, ORB_Z0))) @ Matrix(((0, -1, 0, 0), (0, 0, -1, 0), (1, 0, 0, 0), (0, 0, 0, 1)))
def O(x, y, z):
    return M_ORB @ V((x, y, z))

ESCOTILHA_L = V((30.2, 2.55, 1.8))                       # escotilha lateral do meio-convés (bombordo)
ESC_W = O(*ESCOTILHA_L)                                  # ~ (41.45, 282.5, 49.7)
BRACO_Z = ESC_W.z - 0.5                                  # piso do braco de acesso = base da escotilha
FSS_C = V((13.0, ESC_W.y))                               # centro da torre (12 x 12 m)
FSS_L = 6.0


# ======================================================================= utilidades
def trelica_face(m, a, b, h0, h1, mat, passo=6.0, r=0.16, diag=True):
    """face de treliça vertical entre os pontos a e b (2D), de h0 a h1."""
    a, b = V((*a, 0)), V((*b, 0))
    n = max(1, int(round((h1 - h0) / passo)))
    for i in range(n + 1):
        z = h0 + (h1 - h0) * i / n
        m.cilindro(a + V((0, 0, z)), b + V((0, 0, z)), r, mat, 6)
        if diag and i < n:
            z2 = h0 + (h1 - h0) * (i + 1) / n
            p, q = (a, b) if i % 2 == 0 else (b, a)
            m.cilindro(p + V((0, 0, z)), q + V((0, 0, z2)), r * 0.7, mat, 6)


def bandeira_eua(m, M, w, h, e=0.01):
    """bandeira dos EUA no plano XY local (frente +Z)."""
    for s in range(13):
        y0 = h / 2 - (s + 1) * h / 13
        p = [M @ V((-w / 2, y0, e)), M @ V((w / 2, y0, e)), M @ V((w / 2, y0 + h / 13, e)), M @ V((-w / 2, y0 + h / 13, e))]
        m.quad(*p, VERMELHO if s % 2 == 0 else BRANCO, duplo=True)
    cw, ch = w * 0.4, h * 7 / 13
    p = [M @ V((-w / 2, h / 2 - ch, e * 2)), M @ V((-w / 2 + cw, h / 2 - ch, e * 2)), M @ V((-w / 2 + cw, h / 2, e * 2)), M @ V((-w / 2, h / 2, e * 2))]
    m.quad(*p, AZUL_ESC, duplo=True)


# ======================================================================= 1. concreto, rampa, MLP
def base():
    m = Malha("Plataforma_Base")
    x0, x1, y0, y1 = SX - 50, SX + 50, 245.0, 336.0
    # deck elevado com taludes (octogono)
    ch = 10.0
    pts = [(x0 + ch, y0), (x1 - ch, y0), (x1, y0 + ch), (x1, y1 - ch), (x1 - ch, y1), (x0 + ch, y1), (x0, y1 - ch), (x0, y0 + ch)]
    m.prisma(pts, -0.6, DECK, CONCRETO)
    pts2 = [(x - (x - SX) * -0.12, y - (y - 290) * -0.12) for x, y in pts]       # talude (mais largo embaixo)
    m.prisma(pts2, -0.6, DECK - 3.0, CONCRETO_ESC)
    # rampa (crawlerway) da estrada ate o deck
    rw = 9.0
    bm_v = [V((SX - rw, 200.0, -0.12)), V((SX + rw, 200.0, -0.12)), V((SX + rw, y0 + 0.5, DECK)), V((SX - rw, y0 + 0.5, DECK))]
    m.quad(*bm_v, CONCRETO)
    for s in (-1, 1):                                                         # laterais da rampa
        xs = SX + s * rw
        lado = [V((xs, 200.0, -0.6)), V((xs, y0 + 0.5, -0.6)), V((xs, y0 + 0.5, DECK)), V((xs, 200.0, -0.12))]
        m.quad(*(lado if s > 0 else lado[::-1]), CONCRETO_ESC)
        for k in range(12):                                                   # mureta/guarda-corpo
            t0, t1 = k / 12, (k + 1) / 12
            a = V((xs, 200 + (y0 - 200) * t0, -0.12 + (DECK + 0.12) * t0))
            b = V((xs, 200 + (y0 - 200) * t1, -0.12 + (DECK + 0.12) * t1))
            m.cilindro(a + V((0, 0, 1.1)), b + V((0, 0, 1.1)), 0.05, AMARELO, 6)
            m.cilindro(a, a + V((0, 0, 1.1)), 0.05, AMARELO, 6)
    # faixas da crawlerway na rampa
    for s in (-1, 1):
        a = V((SX + s * 4.5 - 1.6, 200.0, -0.1)); b = V((SX + s * 4.5 + 1.6, 200.0, -0.1))
        c = V((SX + s * 4.5 + 1.6, y0 + 0.5, DECK + 0.02)); d = V((SX + s * 4.5 - 1.6, y0 + 0.5, DECK + 0.02))
        m.quad(a, b, c, d, GRADE)
    # vala de chamas (escura) dos dois lados da MLP
    m.caixa_ab((SX - 8, 252, DECK - 0.02), (SX + 8, 268, DECK + 0.01), CONCRETO_ESC)
    m.caixa_ab((SX - 8, 312, DECK - 0.02), (SX + 8, 330, DECK + 0.01), CONCRETO_ESC)
    # faixas de pedestre ate a torre
    for k in range(10):
        m.caixa_ab((SX - 2 - k * 2.6, 268.0, DECK), (SX - 0.8 - k * 2.6, 270.0, DECK + 0.015), AMARELO)
    # MLP (plataforma movel de lancamento) sobre 6 pedestais
    mx0, mx1, my0, my1 = SX - 24.5, SX + 24.5, SY - 20.5, SY + 20.5
    for x in (mx0 + 3, SX, mx1 - 3):
        for y in (my0 + 3, my1 - 3):
            m.caixa((x, y, DECK + 1.0), (3.0, 3.0, 2.0), CONCRETO_ESC)
    m.caixa_ab((mx0, my0, DECK + 2.0), (mx1, my1, ZM), TORRE_CINZA)
    m.caixa_ab((mx0 - 0.1, my0 - 0.1, ZM - 0.6), (mx1 + 0.1, my1 + 0.1, ZM - 0.3), ESCURO)
    for (cx, cy, w, d) in ((SX - 6.55, SY, 6.0, 6.0), (SX + 6.55, SY, 6.0, 6.0), (SX, SY - 9.5, 9.0, 5.0)):   # aberturas do escape
        m.caixa((cx, cy, ZM + 0.005), (w, d, 0.02), MOTOR_INT)
    for x in (SX - 6.55, SX + 6.55):                                          # postes de fixacao dos boosters
        for dx, dy in ((-2.2, -2.2), (2.2, -2.2), (-2.2, 2.2), (2.2, 2.2)):
            m.cilindro((x + dx, SY + dy, ZM), (x + dx, SY + dy, ZM + 1.0), 0.35, METAL, 10)
    # mastros de servico da cauda (TSM)
    for s in (-1, 1):
        x = SX + s * 9.5
        m.caixa((x, SY - 12.0, ZM + 5.0), (3.2, 3.2, 10.0), TORRE_CINZA)
        m.caixa((x, SY - 12.0, ZM + 10.2), (3.6, 3.6, 0.4), AMARELO)
    m.texto("MLP-3", (SX, my0 - 0.06, DECK + 5.5), 2.2, TXT_BR, espessura=0.05)
    # caixa d'agua (supressao de som) e postes de iluminacao
    tx, ty = SX + 70, 250.0
    for dx, dy in ((-4, -4), (4, -4), (-4, 4), (4, 4)):
        m.cilindro((tx + dx, ty + dy, -0.2), (tx + dx * 0.6, ty + dy * 0.6, 48), 0.5, METAL, 8)
    m.cilindro((tx, ty, 46), (tx, ty, 52), 7.0, BRANCO, 24)
    m.esfera((tx, ty, 52), 7.0, BRANCO, 24, 8, escala=(1, 1, 0.45))
    m.texto("NASA", (tx, ty - 7.05, 49), 3.0, AZUL_ESC, espessura=0.1)
    for (lx, ly) in ((SX - 48, 248), (SX + 48, 248), (SX - 48, 333), (SX + 48, 333)):
        m.cilindro((lx, ly, DECK), (lx, ly, DECK + 22), 0.35, TORRE_CINZA, 10)
        m.caixa((lx, ly, DECK + 22.6), (4.0, 1.0, 1.4), TORRE_CINZA)
        for k in range(4):
            m.caixa((lx - 1.5 + k, ly - 0.52, DECK + 22.6), (0.8, 0.05, 1.0), LUZ)
    fim(m, 2.0)
    # colisores
    col("COL_Deck", (SX, 290.5, DECK - 3.0), (100, 91, 6.0))
    L = math.hypot(y0 + 0.5 - 200.0, DECK + 0.12)
    ang = math.atan2(DECK + 0.12, y0 + 0.5 - 200.0)
    o = colisor("COL_Rampa", ((y0 + 200) / 2 * 0 + SX, (y0 + 0.5 + 200.0) / 2, (DECK - 0.12) / 2 - 0.25), (2 * rw, L, 0.5), COL)
    o.rotation_euler = (ang, 0, 0); OBJS.append(o)
    for s in (-1, 1):
        o = colisor(f"COL_Rampa_Mureta_{'O' if s < 0 else 'L'}", (SX + s * rw, (y0 + 200.0) / 2, (DECK) / 2 + 0.6), (0.3, L, 1.4), COL)
        o.rotation_euler = (ang, 0, 0); OBJS.append(o)
    col("COL_MLP", (SX, SY, (DECK + ZM) / 2 + 1.0), (49, 41, ZM - DECK - 2.0))
    for s in (-1, 1):
        col(f"COL_TSM_{s}", (SX + s * 9.5, SY - 12.0, ZM + 5.0), (3.2, 3.2, 10.0))
    col("COL_CaixaAgua", (tx, ty, 26), (9, 9, 52))


# ======================================================================= 2. torre fixa (FSS) + elevador + braco de acesso + sala branca
def torre():
    m = Malha("Plataforma_Torre")
    cx, cy = FSS_C
    h = 81.0
    x0, x1, y0, y1 = cx - FSS_L, cx + FSS_L, cy - FSS_L, cy + FSS_L
    cantos = [(x0, y0), (x1, y0), (x1, y1), (x0, y1)]
    for (x, y) in cantos:
        m.caixa((x, y, DECK + h / 2), (0.7, 0.7, h), TORRE)
    for i in range(4):
        trelica_face(m, cantos[i], cantos[(i + 1) % 4], DECK, DECK + h, TORRE, passo=6.1, r=0.18)
    # pisos (grade) nos niveis do chao e do braco + plataformas intermediarias parciais
    for z in [DECK + 0.05] + [DECK + 6.1 * k for k in range(2, 13, 2)]:
        m.caixa_ab((x0, y0, z), (x1, cy - 1.6, z + 0.15), GRADE)
    m.caixa_ab((x0, y0, BRACO_Z - 0.2), (x1, y1, BRACO_Z), GRADE)
    # guarda-corpo do nivel do braco (com abertura para o braco a leste)
    corr = [(x1, cy + 1.4), (x1, y1), (x0, y1), (x0, y0), (x1, y0), (x1, cy - 1.4)]
    for a, b in zip(corr[:-1], corr[1:]):
        m.cilindro((*a, BRACO_Z + 1.1), (*b, BRACO_Z + 1.1), 0.05, AMARELO, 6)
        m.cilindro((*a, BRACO_Z + 0.55), (*b, BRACO_Z + 0.55), 0.04, AMARELO, 6)
        m.cilindro((*a, BRACO_Z), (*a, BRACO_Z + 1.1), 0.05, AMARELO, 6)
    # poço do elevador (3 paredes de tela; porta voltada para leste)
    ex0, ex1, ey0, ey1 = cx - 1.5, cx + 1.5, cy - 1.5, cy + 1.5
    for (a, b) in (((ex0, ey0), (ex0, ey1)), ((ex0, ey1), (ex1, ey1)), ((ex0, ey0), (ex1, ey0))):
        trelica_face(m, a, b, DECK, BRACO_Z + 3.5, TORRE_CINZA, passo=3.0, r=0.07)
    m.caixa_ab((ex0 - 0.2, ey0 - 0.2, BRACO_Z + 3.5), (ex1 + 0.2, ey1 + 0.2, BRACO_Z + 4.5), TORRE_CINZA)
    m.cilindro((cx, cy, BRACO_Z + 3.5), (cx, cy, BRACO_Z + 4.5), 0.6, METAL, 12)
    # placas de sinalizacao
    m.caixa((x1 + 0.05, cy, DECK + 3.8), (0.1, 3.0, 0.9), AZUL_ESC)
    m.texto("ELEVADOR", (x1 + 0.12, cy, DECK + 3.8), 0.45, TXT_BR, rz=PI / 2, espessura=0.01)
    m.caixa((x1 + 0.05, cy, BRACO_Z + 2.6), (0.1, 3.6, 0.8), VERMELHO)
    m.texto("195 FT · BRAÇO", (x1 + 0.12, cy, BRACO_Z + 2.6), 0.38, TXT_BR, rz=PI / 2, espessura=0.01)
    # guindaste (cabeca de martelo) e para-raios com cabos
    m.caixa((cx, cy, DECK + h + 1.5), (26, 2.0, 3.0), TORRE)
    m.caixa((cx + 9, cy, DECK + h - 0.5), (2.0, 2.0, 1.5), TORRE_CINZA)
    m.cilindro((cx, cy, DECK + h + 3), (cx, cy, DECK + h + 24), 0.45, BRANCO, 10, r2=0.18)
    m.cilindro((cx, cy, DECK + h + 24), (cx, cy, DECK + h + 26), 0.18, TORRE, 6)
    for (ax, ay) in ((cx - 80, cy - 10), (cx + 120, cy + 30)):
        m.cilindro((cx, cy, DECK + h + 24), (ax, ay, DECK), 0.03, ESCURO, 4)
    for z in (DECK + 30, DECK + 60, DECK + h + 2):                                  # luzes de aviacao
        for (x, y) in cantos:
            m.esfera((x, y, z), 0.3, LUZ_VERM, 6, 4)
    # braco de oxigenio do tanque ("beanie cap") apontando para o topo do tanque externo
    tz = ZM + 8 + 46.2
    m.caixa(((x1 + SX - 4.2) / 2, cy + 3.5, tz - 0.6), (SX - 4.2 - x1, 1.2, 1.2), TORRE_CINZA)
    m.cilindro((SX, SY, tz - 1.2), (SX, SY, tz + 1.4), 1.6, BRANCO, 16, r2=1.0)
    m.cilindro((SX - 1.6, SY, tz - 0.3), ((x1 + SX) / 2, cy + 3.5, tz - 0.6), 0.25, METAL, 8)
    fim(m, 1.0)

    # braco de acesso da tripulacao (corredor coberto) + sala branca
    b = Malha("Plataforma_BracoAcesso")
    bx0, bx1 = x1, ESC_W.x - 4.0
    w = 1.3
    b.caixa_ab((bx0, cy - w, BRACO_Z - 0.35), (bx1, cy + w, BRACO_Z), GRADE)
    for s in (-1, 1):                                                               # laterais em treliça + teto
        y = cy + s * w
        trelica_face(b, (bx0, y), (bx1, y), BRACO_Z - 0.35, BRACO_Z + 2.6, TORRE_CINZA, passo=1.5, r=0.06)
        b.caixa_ab((bx0, y - 0.03 if s > 0 else y, BRACO_Z + 0.9), (bx1, y + 0.03 if s < 0 else y, BRACO_Z + 0.95), AMARELO)
    b.caixa_ab((bx0, cy - w - 0.1, BRACO_Z + 2.6), (bx1, cy + w + 0.1, BRACO_Z + 2.75), BRANCO)
    b.cilindro((bx0 + 3, cy - 0.6, BRACO_Z - 0.35), (bx0 + 3, cy - 0.6, DECK + 6.1 * 6), 0.25, TORRE_CINZA, 8)  # escora
    # sala branca: caixa branca encostada no orbitador, com porta para o braco
    sx0, sx1 = bx1, ESC_W.x - 0.15
    sw = 1.7
    b.caixa_ab((sx0, cy - sw, BRACO_Z - 0.35), (sx1, cy + sw, BRACO_Z), GRADE)
    for s in (-1, 1):
        y = cy + s * sw
        b.caixa_ab((sx0, min(y, y - 0.12 * s), BRACO_Z), (sx1, max(y, y - 0.12 * s), BRACO_Z + 2.8), BRANCO)
    b.caixa_ab((sx0 - 0.12, cy - sw, BRACO_Z + 2.8), (sx1, cy + sw, BRACO_Z + 3.0), BRANCO)
    b.caixa_ab((sx0 - 0.12, cy - sw, BRACO_Z), (sx0, cy - w, BRACO_Z + 2.8), BRANCO)
    b.caixa_ab((sx0 - 0.12, cy + w, BRACO_Z), (sx0, cy + sw, BRACO_Z + 2.8), BRANCO)
    b.caixa_ab((sx0 - 0.12, cy - w, BRACO_Z + 2.2), (sx0, cy + w, BRACO_Z + 2.8), BRANCO)
    b.caixa_ab((sx1 - 0.3, cy - sw, BRACO_Z + 1.7), (sx1, cy + sw, BRACO_Z + 2.8), BRANCO)   # vedacao sobre a escotilha
    b.texto("SALA BRANCA", (sx0 - 0.14, cy, BRACO_Z + 2.5), 0.22, AZUL_ESC, rz=-PI / 2, espessura=0.005)
    b.caixa((sx0 + 0.6, cy + sw - 0.25, BRACO_Z + 0.9), (0.6, 0.35, 1.8), METAL)               # armario de equipamentos
    b.painel_tex((sx0 + 0.6, cy + sw - 0.44, BRACO_Z + 1.6), 0.4, 0.3, (0, -1, 0), material("CT_Tela", "#47E6FF", emissao=1.5))
    b.caixa((sx0 + 1.6, cy - sw + 0.25, BRACO_Z + 0.45), (1.0, 0.4, 0.9), AZUL_ESC)             # banco
    b.cilindro((sx1 - 0.1, cy, BRACO_Z + 2.6), (sx1 - 0.1, cy, BRACO_Z + 2.75), 0.25, LUZ, 12)
    fim(b, 1.0)

    # cabine do elevador (ANIM): origem no piso, no nivel do deck
    e = Malha("ANIM_Elevador", origem=(cx, cy, DECK + 0.2))
    e.caixa_ab((ex0 + 0.1, ey0 + 0.1, DECK + 0.05), (ex1 - 0.1, ey1 - 0.1, DECK + 0.2), GRADE)
    for (a, bb) in (((ex0 + 0.15, ey0 + 0.15), (ex0 + 0.15, ey1 - 0.15)), ((ex0 + 0.15, ey1 - 0.15), (ex1 - 0.15, ey1 - 0.15)),
                    ((ex0 + 0.15, ey0 + 0.15), (ex1 - 0.15, ey0 + 0.15))):
        e.cilindro((*a, DECK + 0.2), (*bb, DECK + 0.2), 0.04, AMARELO, 6)
        e.cilindro((*a, DECK + 1.2), (*bb, DECK + 1.2), 0.05, AMARELO, 6)
        e.cilindro((*a, DECK + 0.2), (*a, DECK + 2.5), 0.05, AMARELO, 6)
    e.caixa_ab((ex0 + 0.1, ey0 + 0.1, DECK + 2.5), (ex1 - 0.1, ey1 - 0.1, DECK + 2.6), TORRE_CINZA)
    e.caixa((ex0 + 0.3, cy, DECK + 1.2), (0.12, 0.5, 0.6), ESCURO)                                 # botoeira
    e.cilindro((ex0 + 0.37, cy - 0.1, DECK + 1.3), (ex0 + 0.4, cy - 0.1, DECK + 1.3), 0.05, LUZ_VERDE, 8)
    e.cilindro((ex0 + 0.37, cy + 0.1, DECK + 1.3), (ex0 + 0.4, cy + 0.1, DECK + 1.3), 0.05, LUZ_VERM, 8)
    fim(e, 1.0)

    # colisores da torre
    for (x, y) in cantos:
        col(f"COL_FSS_Pilar_{int(x)}_{int(y)}", (x, y, DECK + h / 2), (0.8, 0.8, h))
    col("COL_FSS_Poco_O", (ex0, cy, (DECK + BRACO_Z + 3.5) / 2), (0.25, 3.2, BRACO_Z + 3.5 - DECK))
    col("COL_FSS_Poco_N", (cx, ey1, (DECK + BRACO_Z + 3.5) / 2), (3.2, 0.25, BRACO_Z + 3.5 - DECK))
    col("COL_FSS_Poco_S", (cx, ey0, (DECK + BRACO_Z + 3.5) / 2), (3.2, 0.25, BRACO_Z + 3.5 - DECK))
    col("COL_FSS_Piso_Braco_N", (cx, (ey1 + y1) / 2, BRACO_Z - 0.15), (2 * FSS_L, y1 - ey1, 0.3))
    col("COL_FSS_Piso_Braco_S", (cx, (ey0 + y0) / 2, BRACO_Z - 0.15), (2 * FSS_L, ey0 - y0, 0.3))
    col("COL_FSS_Piso_Braco_L", ((ex1 + x1) / 2, cy, BRACO_Z - 0.15), (x1 - ex1, ey1 - ey0, 0.3))
    col("COL_FSS_Piso_Braco_O", ((ex0 + x0) / 2, cy, BRACO_Z - 0.15), (ex0 - x0, ey1 - ey0, 0.3))
    col("COL_FSS_Grade_N", (cx, y1, BRACO_Z + 0.6), (2 * FSS_L, 0.2, 1.2))
    col("COL_FSS_Grade_S", (cx, y0, BRACO_Z + 0.6), (2 * FSS_L, 0.2, 1.2))
    col("COL_FSS_Grade_O", (x0, cy, BRACO_Z + 0.6), (0.2, 2 * FSS_L, 1.2))
    col("COL_FSS_Grade_L_N", (x1, (cy + 1.4 + y1) / 2, BRACO_Z + 0.6), (0.2, y1 - cy - 1.4, 1.2))
    col("COL_FSS_Grade_L_S", (x1, (cy - 1.4 + y0) / 2, BRACO_Z + 0.6), (0.2, cy - 1.4 - y0, 1.2))
    col("COL_Braco_Piso", ((bx0 + sx1) / 2, cy, BRACO_Z - 0.17), (sx1 - bx0, 2 * sw, 0.35))
    col("COL_Braco_N", ((bx0 + bx1) / 2, cy + w, BRACO_Z + 1.3), (bx1 - bx0, 0.15, 2.6))
    col("COL_Braco_S", ((bx0 + bx1) / 2, cy - w, BRACO_Z + 1.3), (bx1 - bx0, 0.15, 2.6))
    col("COL_SalaBranca_N", ((sx0 + sx1) / 2, cy + sw, BRACO_Z + 1.4), (sx1 - sx0, 0.15, 2.8))
    col("COL_SalaBranca_S", ((sx0 + sx1) / 2, cy - sw, BRACO_Z + 1.4), (sx1 - sx0, 0.15, 2.8))
    col("COL_SalaBranca_Fundo", (sx1, cy, BRACO_Z + 1.4), (0.2, 2 * sw, 2.8))
    col("COL_Elevador_Piso", (cx, cy, DECK + 0.12), (2.8, 2.8, 0.15))
    emp("WP_Elevador_Base", (cx, cy, DECK + 0.2))
    emp("WP_Elevador_Topo", (cx, cy, BRACO_Z))
    emp("INT_Elevador", (ex0 + 0.35, cy, DECK + 1.3))
    emp("INT_Escotilha", (ESC_W.x - 0.4, cy, BRACO_Z + 0.9))
    emp("WP_Torre_Entrada", (x1 + 2.0, cy, DECK))
    zona("ZONA_Elevador", (cx, cy, DECK + 1.3), (2.6, 2.6, 2.4))
    zona("ZONA_SalaBranca", ((sx0 + sx1) / 2, cy, BRACO_Z + 1.4), (sx1 - sx0, 2 * sw, 2.8))
    zona("ZONA_Plataforma", (SX, 290.5, DECK + 3.0), (100, 91, 6.0))
    return (ex0, ex1, ey0, ey1)


# ======================================================================= 3. estrutura rotativa (aberta) — massa de fundo
def estrutura_rotativa():
    m = Malha("Plataforma_EstruturaRotativa")
    piv = V((FSS_C.x + FSS_L, FSS_C.y + FSS_L))
    ang = math.radians(150)                                     # girada para longe do onibus
    R = Matrix.Translation(V((piv.x, piv.y, 0))) @ Matrix.Rotation(ang, 4, "Z")
    L, P, H0, H1 = 30.0, 14.0, DECK + 18.0, DECK + 58.0
    cs = [R @ V((0, 0, 0)), R @ V((L, 0, 0)), R @ V((L, P, 0)), R @ V((0, P, 0))]
    for c in cs:
        m.caixa((c.x, c.y, (DECK + H1) / 2), (0.8, 0.8, H1 - DECK), TORRE)
    for i in range(4):
        a, b = cs[i], cs[(i + 1) % 4]
        trelica_face(m, (a.x, a.y), (b.x, b.y), H0, H1, TORRE, passo=8.0, r=0.2)
    pc = R @ V((L * 0.55, P * 0.5, 0))
    m.caixa((pc.x, pc.y, (H0 + H1) / 2 + 4), (L * 0.6, P * 0.7, H1 - H0 - 6), TORRE_CINZA, rz=ang)   # sala de troca de carga
    m.texto("RSS", (pc.x, pc.y, H1 + 0.6), 3.0, TXT_BR, rz=ang, rx=0, espessura=0.1)
    fim(m, 1.0)
    for i, c in enumerate(cs):
        col(f"COL_RSS_{i}", (c.x, c.y, (DECK + H1) / 2), (0.9, 0.9, H1 - DECK))


# ======================================================================= 4. onibus espacial
# ---- casco do orbitador: secoes superelipticas ao longo de X (cauda 0 -> nariz 37.2), parametro theta (0 = bombordo/esquerda, 90 = cima)
ESTACOES = [  # X, a (meia largura), b (meia altura), zc, n (expoente)
    (0.0, 2.6, 2.7, 2.75, 4.0), (6.0, 2.62, 2.72, 2.78, 4.0), (26.5, 2.6, 2.72, 2.78, 4.0), (28.6, 2.57, 2.84, 2.9, 3.8),
    (31.0, 2.5, 2.86, 2.88, 3.4), (32.4, 2.32, 2.62, 2.72, 3.0), (33.6, 2.0, 2.15, 2.4, 2.7), (34.7, 1.6, 1.6, 2.05, 2.5),
    (35.7, 1.1, 1.08, 1.75, 2.2), (36.6, 0.55, 0.55, 1.5, 2.0), (37.2, 0.06, 0.06, 1.42, 2.0)]

def secao(X):
    for (x0, *p0), (x1, *p1) in zip(ESTACOES[:-1], ESTACOES[1:]):
        if x0 <= X <= x1:
            t = (X - x0) / (x1 - x0)
            return [a + (b - a) * t for a, b in zip(p0, p1)]
    return list(ESTACOES[-1][1:]) if X > ESTACOES[-1][0] else list(ESTACOES[0][1:])

def casco(X, th, inset=0.0):
    a, b, zc, n = secao(X)
    a, b = max(0.02, a - inset), max(0.02, b - inset)
    c, s = math.cos(math.radians(th)), math.sin(math.radians(th))
    y = a * math.copysign(abs(c) ** (2 / n), c)
    z = zc + b * math.copysign(abs(s) ** (2 / n), s)
    return V((X, y, z))

JANELAS = [  # (X0, X1, th0, th1)
    (32.45, 33.25, 48, 62), (32.45, 33.25, 64, 78), (32.45, 33.25, 80, 88), (32.45, 33.25, 92, 100), (32.45, 33.25, 102, 116), (32.45, 33.25, 118, 132),
    (31.3, 32.0, 72, 86), (31.3, 32.0, 94, 108),               # janelas de teto
    (31.75, 32.4, 22, 40), (31.75, 32.4, 140, 158),            # laterais
]

ESCOTILHA_CELULA = (29.7, 30.7, 339.0, 357.0)   # X0, X1, theta0, theta1 (bombordo): ~1,0 x 1,0 m em volta de ESCOTILHA_L

def em_escotilha(xa, xb, ta, tb):
    x0, x1, t0, t1 = ESCOTILHA_CELULA
    return xa >= x0 - 1e-4 and xb <= x1 + 1e-4 and ta >= t0 - 1e-4 and tb <= t1 + 1e-4

def em_janela(xa, xb, ta, tb):
    for (x0, x1, t0, t1) in JANELAS:
        if xa >= x0 - 1e-4 and xb <= x1 + 1e-4 and ta >= t0 - 1e-4 and tb <= t1 + 1e-4:
            return True
    return False

def grade_X():
    xs = set([round(i * 1.5, 3) for i in range(0, 18)] + [26.5, 27.6, 28.6, 29.6, 30.4, 31.0, 31.3, 31.65, 31.75, 32.0, 32.4, 32.45,
             32.85, 33.25, 33.6, 34.2, 34.7, 35.2, 35.7, 36.2, 36.6, 36.9, 37.2, 29.7, 30.7])
    return sorted(xs)

def grade_T():
    ts = set(range(0, 360, 10))
    for j in JANELAS:
        ts.update((j[2], j[3]))
    ts.update((70, 110, 64, 116, 72, 108, 94, 86, 339, 357))
    return sorted(ts)

def cor_casco(p_med, normal):
    if normal.z < -0.32:
        return PRETO_T
    if p_med.x > 35.9:
        return RCC
    if p_med.x > 33.6 and normal.z < 0.2:
        return PRETO_T
    return BRANCO_T

def orbitador():
    m = Malha("Onibus_Orbitador")
    xs, ts = grade_X(), grade_T()
    nt = len(ts)
    for i in range(len(xs) - 1):
        xa, xb = xs[i], xs[i + 1]
        for j in range(nt):
            ta, tb = ts[j], ts[(j + 1) % nt] if j + 1 < nt else 360
            if em_janela(xa, xb, ta, tb) or em_escotilha(xa, xb, ta, tb):
                continue
            p = [casco(xa, ta), casco(xa, tb), casco(xb, tb), casco(xb, ta)]     # normal para fora
            n = (p[1] - p[0]).cross(p[3] - p[0]).normalized()
            pm = sum(p, V()) / 4
            fs = m.quad(*[M_ORB @ q for q in p], cor_casco(pm, n))
            for f in fs:
                m.uv_fixo.discard(f)
    # vidros (escuros, transparentes) + molduras pretas
    for (x0, x1, t0, t1) in JANELAS:
        p = [casco(x0, t0), casco(x1, t0), casco(x1, t1), casco(x0, t1)]
        m.quad(*[M_ORB @ q for q in p], VIDRO_CAB)
        for a, b in zip(p, p[1:] + p[:1]):
            m.cilindro(M_ORB @ a, M_ORB @ b, 0.045, PRETO_T, 4)
    # tampa traseira
    ring = [casco(0.0, t) for t in ts]
    bm = m.bm
    vs = [bm.verts.new(M_ORB @ q) for q in ring]
    f = bm.faces.new(vs[::-1] if True else vs); f.material_index = m.mi(PRETO_T)
    # linhas das portas do compartimento de carga
    for y in (-1.75, 0.0, 1.75):
        a = casco(6.5, 90 + (y / 2.6) * -60); b = casco(26.3, 90 + (y / 2.6) * -60)
        m.cilindro(M_ORB @ (a + V((0, 0, 0.01))), M_ORB @ (b + V((0, 0, 0.01))), 0.03, MOTOR, 4)
    for x in (6.5, 11.5, 16.5, 21.5, 26.3):
        for t0, t1 in ((40, 88), (92, 140)):
            pts = [casco(x, t) + V((0, 0, 0.01)) for t in range(t0, t1 + 1, 8)]
            for a, b in zip(pts[:-1], pts[1:]):
                m.cilindro(M_ORB @ a, M_ORB @ b, 0.025, MOTOR, 4)
    # escotilha lateral (bombordo)
    hc = casco(ESCOTILHA_L.x, 32) * 0 + V((ESCOTILHA_L.x, secao(ESCOTILHA_L.x)[0] + 0.01, ESCOTILHA_L.z))
    Mh = M_ORB @ Matrix.Translation(hc) @ Matrix.Rotation(-PI / 2, 4, "X")
    m.torus((0, 0, 0.04), 0.55, 0.05, PRETO_T, 32, 6, M=Mh)                        # batente da escotilha (aberta)
    x0, x1, t0, t1 = ESCOTILHA_CELULA
    bord = [casco(x0, t0), casco(x1, t0), casco(x1, t1), casco(x0, t1)]
    for a_, b_ in zip(bord, bord[1:] + bord[:1]):
        m.cilindro(M_ORB @ a_, M_ORB @ b_, 0.05, PRETO_T, 6)                       # moldura do recorte
    # porta aberta, dobrada para baixo (dobradica na borda inferior)
    Mp = M_ORB @ Matrix.Translation(V((ESCOTILHA_L.x, secao(ESCOTILHA_L.x)[0] + 0.25, ESCOTILHA_L.z - 0.55))) @ Matrix.Rotation(0.15, 4, "X")
    m.cilindro(Mp @ V((0, 0, -0.04)), Mp @ V((0, 0, 0.04)), 0.5, BRANCO_T, 24)
    m.cilindro(Mp @ V((0, 0, 0.04)), Mp @ V((0, 0, 0.07)), 0.14, VIDRO_CAB, 16)
    # propulsores de manobra (RCS) no nariz
    for th in (20, 160, 60, 120):
        for x in (35.0, 35.4):
            p = casco(x, th, -0.02)
            m.esfera(M_ORB @ p, 0.08, PRETO_T, 6, 4)

    # ---- asas (dupla delta), espessura afinando para a ponta
    for s in (1, -1):                       # 1 = bombordo (Y+), -1 = boreste
        plan = [(26.6, 2.35), (18.0, 4.9), (6.4, 11.95), (3.4, 11.95), (1.3, 2.35)]
        def esp(y):
            return 1.25 * (1 - (abs(y) - 2.35) / 9.6) + 0.22
        top = [m.bm.verts.new(M_ORB @ V((x, s * y, 0.2 + esp(y)))) for x, y in plan]
        bot = [m.bm.verts.new(M_ORB @ V((x, s * y, 0.15))) for x, y in plan]
        if s < 0:
            top.reverse(); bot.reverse()
        ft = m.bm.faces.new(top); fb = m.bm.faces.new(bot[::-1])
        ft.material_index = m.mi(BRANCO_T); fb.material_index = m.mi(PRETO_T)
        n = len(top)
        for i in range(n):
            i2 = (i + 1) % n
            f = m.bm.faces.new((bot[i], bot[i2], top[i2], top[i]))
            # bordo de ataque cinza, demais laterais pretas
            idx_ori = i if s > 0 else (n - 1 - i2) % n
            f.material_index = m.mi(RCC if idx_ori in (0, 1) else PRETO_T)
        bmesh.ops.triangulate(m.bm, faces=[ft, fb])
        # elevons (linha escura) + bandeira / logo no extradorso
        a = M_ORB @ V((3.9, s * 3.0, 0.2 + esp(3.0) + 0.02)); b = M_ORB @ V((3.6, s * 11.5, 0.2 + esp(11.5) + 0.02))
        m.cilindro(a, b, 0.04, MOTOR, 4)
        yc, xc = 7.0, 9.8
        Mw = M_ORB @ Matrix.Translation(V((xc, s * yc, 0.2 + esp(yc) + 0.03))) @ Matrix.Rotation(PI / 2 * s, 4, "Z")
        if s > 0:
            bandeira_eua(m, Mw, 3.4, 1.8, 0.005)
        else:
            m.painel_tex(Mw @ V((0, 0, 0.01)), 3.0, 3.0, (M_ORB.to_3x3() @ V((0, 0, 1))), LOGO, cima=(M_ORB.to_3x3() @ V((1, 0, 0))))

    # ---- deriva (cauda vertical) com leme
    fin = [(11.2, 5.4), (4.1, 15.2), (1.6, 15.2), (0.0, 5.4)]
    def esp_f(z):
        return 0.32 - 0.18 * (z - 5.4) / 9.8
    vs_l = [m.bm.verts.new(M_ORB @ V((x, esp_f(z), z))) for x, z in fin]
    vs_r = [m.bm.verts.new(M_ORB @ V((x, -esp_f(z), z))) for x, z in fin]
    fl = m.bm.faces.new(vs_l[::-1]); fr = m.bm.faces.new(vs_r)
    fl.material_index = fr.material_index = m.mi(BRANCO_T)
    for i in range(4):
        i2 = (i + 1) % 4
        f = m.bm.faces.new((vs_l[i], vs_l[i2], vs_r[i2], vs_r[i]))
        f.material_index = m.mi(RCC if i == 0 else BRANCO_T)
    m.cilindro(O(1.9, 0.0, 6.0), O(3.9, 0.0, 14.8), 0.33, MOTOR, 4)            # linha do leme/freio
    # ---- casulos OMS
    for s in (1, -1):
        Mo = M_ORB @ Matrix.Translation(V((4.2, s * 1.85, 5.35))) @ Matrix.Rotation(PI / 2, 4, "Y") @ Matrix.Diagonal((1.0, 1.25, 1.0, 1))
        m.torno([(0.0, -3.9), (0.75, -3.6), (0.95, -1.5), (0.95, 2.6), (0.7, 3.8), (0.0, 4.2)], BRANCO_T, 14, M=Mo)
        m.torno([(0.12, 0.0), (0.28, -0.7), (0.45, -1.2)], MOTOR, 12,
                M=M_ORB @ Matrix.Translation(V((0.3, s * 1.85, 5.35))) @ Matrix.Rotation(PI / 2, 4, "Y") @ Matrix.Diagonal((1, 1, -1, 1)),
                fechar_base=False, fechar_topo=False)
    # ---- motores principais (SSME) e flape
    for (y, z) in ((0.0, 4.15), (1.35, 2.05), (-1.35, 2.05)):
        Ms = M_ORB @ Matrix.Translation(V((0.0, y, z))) @ Matrix.Rotation(-PI / 2, 4, "Y")
        m.torno([(0.55, -0.3), (0.42, 0.25), (0.48, 0.9), (0.8, 1.8), (1.15, 3.0)], MOTOR, 18, M=Ms, fechar_base=True, fechar_topo=False)
        m.torno([(1.05, 2.95), (0.7, 1.85), (0.42, 0.95)], MOTOR_INT, 18, M=Ms, fechar_base=False, fechar_topo=True)
        m.cilindro(Ms @ V((0, 0, 2.6)), Ms @ V((0, 0, 3.0)), 1.17, METAL, 18, tampas=False)
    m.caixa(O(-0.8, 0, 0.35), (2.6, 6.2, 0.55), PRETO_T)
    m.caixa(O(-0.8, 0, 0.66), (2.6, 6.2, 0.08), BRANCO_T)
    # ---- letras e bandeira na fuselagem (bombordo, voltado para a torre)
    Ml = M_ORB @ Matrix.Translation(V((21.0, secao(21.0)[0] + 0.02, 3.4))) @ Matrix.Rotation(-PI / 2, 4, "X")
    m.texto("UNITED STATES", (0, 0, 0), 0.9, TXT_ESC, rx=0, espessura=0.02, M=Ml @ Matrix.Rotation(PI, 4, "Z") @ Matrix.Rotation(PI, 4, "Y"))
    bandeira_eua(m, Ml @ Matrix.Translation(V((-8.0, 0.0, 0.0))) @ Matrix.Rotation(PI, 4, "Y"), 2.2, 1.2, 0.005)
    m.painel_tex(Ml @ V((8.5, 0.0, 0.02)), 2.4, 2.4, Ml.to_3x3() @ V((0, 0, 1)), LOGO, cima=M_ORB.to_3x3() @ V((0, 0, 1)) * -1)
    fim(m, 1.0)

def tanque_e_boosters():
    t = Malha("Onibus_TanqueExterno")
    z0 = ZM + 8.0
    Mt = Matrix.Translation(V((SX, SY, z0)))
    t.torno([(0.0, -0.6), (2.6, -0.3), (3.7, 0.4), (4.2, 1.6), (4.2, 20.5)], ET, 32, M=Mt, fechar_topo=False)
    t.torno([(4.2, 20.5), (4.25, 21.0), (4.25, 27.6), (4.2, 28.1)], ET_ESC, 32, M=Mt, fechar_base=False, fechar_topo=False)   # intertanque
    t.torno([(4.2, 28.1), (4.2, 38.0), (3.8, 41.0), (2.9, 43.6), (1.8, 45.4), (0.7, 46.6), (0.25, 47.0), (0.0, 47.05)], ET, 32, M=Mt, fechar_base=False)
    for k in range(24):                                                        # nervuras do intertanque
        a = 2 * PI * k / 24
        p = V((SX + math.cos(a) * 4.28, SY + math.sin(a) * 4.28, 0))
        t.caixa((p.x, p.y, z0 + 24.3), (0.16, 0.16, 6.6), ET_ESC, rz=a)
    for z in (z0 + 8, z0 + 14, z0 + 33):
        t.torus((SX, SY, z), 4.21, 0.05, ET_ESC, 32, 4)
    # linha de alimentacao de LO2 e bandeja de cabos (lado do orbitador)
    t.cilindro((SX + 1.6, SY - 4.45, z0 + 2.0), (SX + 1.6, SY - 4.45, z0 + 29), 0.3, ET_ESC, 10)
    t.cilindro((SX - 1.2, SY - 4.35, z0 + 2.0), (SX - 1.2, SY - 4.35, z0 + 44), 0.18, ET_ESC, 8)
    # suportes do orbitador (bipe dianteiro e traseiros)
    for s in (-1, 1):
        t.cilindro((SX + s * 0.8, SY - 4.2, ORB_Z0 + 27.0), (SX + s * 0.3, ORB_Y_BARRIGA, ORB_Z0 + 28.5), 0.15, METAL, 8)
        t.cilindro((SX + s * 1.6, SY - 4.0, ORB_Z0 + 1.5), (SX + s * 1.6, ORB_Y_BARRIGA, ORB_Z0 + 1.5), 0.35, METAL, 10)
    fim(t, 1.0)

    b = Malha("Onibus_Boosters")
    for s in (-1, 1):
        x = SX + s * 6.55
        Mb = Matrix.Translation(V((x, SY, ZM + 0.5)))
        b.torno([(1.25, 0.0), (0.85, 1.3), (0.6, 2.4)], SRB_ESC, 20, M=Mb, fechar_base=False, fechar_topo=False)     # bocal
        b.torno([(2.6, 1.0), (2.45, 2.0), (1.95, 4.2), (1.86, 4.6)], SRB, 24, M=Mb)                                    # saia traseira
        b.torno([(1.86, 4.6), (1.86, 39.6), (1.8, 40.2), (1.5, 41.6), (0.95, 43.6), (0.45, 44.9), (0.0, 45.5)], SRB, 24, M=Mb, fechar_base=False)
        for zj in (9.8, 15.2, 20.6, 26.0, 31.4, 36.6):                                                                 # juntas dos segmentos
            b.torus((x, SY, ZM + 0.5 + zj), 1.875, 0.05, SRB_ESC, 24, 4)
        b.cilindro((x, SY, ZM + 0.5 + 36.6), (x, SY, ZM + 0.5 + 37.8), 1.875, SRB_ESC, 24, tampas=False)          # faixa escura
        for k in range(4):                                                                                             # aletas do saia
            a = PI / 4 + k * PI / 2
            b.caixa((x + math.cos(a) * 2.2, SY + math.sin(a) * 2.2, ZM + 2.3), (0.9, 0.18, 2.4), SRB, rz=a)
        b.cilindro((x - s * 1.86, SY, ZM + 0.5 + 6.0), (SX + s * 4.2, SY, ZM + 0.5 + 6.6), 0.22, METAL, 8)          # fixacoes no tanque
        b.cilindro((x - s * 1.86, SY, ZM + 0.5 + 37.0), (SX + s * 4.2, SY, ZM + 0.5 + 37.0), 0.28, METAL, 8)
        b.cilindro((x - s * 1.6, SY - 1.2, ZM + 8), (x - s * 1.6, SY - 1.2, ZM + 38), 0.12, SRB_ESC, 6)             # tunel de cabos
    fim(b, 1.0)
    # colisores do onibus (caixas simples)
    col("COL_Tanque", (SX, SY, z0 + 23.5), (8.4, 8.4, 47.0))
    for s in (-1, 1):
        col(f"COL_Booster_{s}", (SX + s * 6.55, SY, ZM + 23.0), (3.8, 3.8, 45.5))
    oc = O(18.6, 0, 2.8)
    col("COL_Orbitador", (oc.x, oc.y, oc.z), (5.2, 5.6, 37.2))


# ======================================================================= 5. interior da cabine (basico; P4 detalha)
def interior():
    m = Malha("Onibus_Interior")
    INSET = 0.12
    X0, X1 = 26.8, 34.2
    xs = [x for x in grade_X() if X0 - 1e-6 <= x <= X1 + 1e-6]
    if xs[0] > X0: xs.insert(0, X0)
    ts = grade_T()
    nt = len(ts)
    # casca interna (faces para dentro), com furos nas janelas + "tuneis" das janelas
    for i in range(len(xs) - 1):
        xa, xb = xs[i], xs[i + 1]
        for j in range(nt):
            ta, tb = ts[j], ts[(j + 1) % nt] if j + 1 < nt else 360
            if em_janela(xa, xb, ta, tb):
                continue
            p = [casco(xa, ta, INSET), casco(xb, ta, INSET), casco(xb, tb, INSET), casco(xa, tb, INSET)]   # normal para dentro
            pm = sum(p, V()) / 4
            mat = INT_PISO if pm.z < 2.95 else INT_PAREDE
            m.quad(*[M_ORB @ q for q in p], mat)
    for (x0, x1, t0, t1) in JANELAS:
        cs = [(x0, t0), (x1, t0), (x1, t1), (x0, t1)]
        for (xa, ta), (xb, tb) in zip(cs, cs[1:] + cs[:1]):
            a_o, b_o = casco(xa, ta), casco(xb, tb)
            a_i, b_i = casco(xa, ta, INSET), casco(xb, tb, INSET)
            m.quad(*[M_ORB @ q for q in (a_i, b_i, b_o, a_o)], PAINEL, duplo=True)
    # anteparas (traseira e dianteira) e piso do convés de voo
    for X, inv in ((X0, False), (X1, True)):
        ring = [M_ORB @ casco(X, t, INSET) for t in ts]
        vs = [m.bm.verts.new(p) for p in ring]
        f = m.bm.faces.new(vs if not inv else vs[::-1])
        f.material_index = m.mi(INT_PAREDE)
    FZ = 2.9
    a = secao(30.0)[0] - INSET
    for (x0, x1) in ((X0, 33.0),):
        p = [O(x0, a, FZ), O(x1, a, FZ), O(x1, -a, FZ), O(x0, -a, FZ)]
        m.quad(p[3], p[2], p[1], p[0], INT_PISO)                               # normal para "cima" local (+Z)
    # assentos do comandante (Y+) e piloto (Y-)
    def assento(y):
        P = lambda x, yy, z: O(x, y + yy, z)
        bx = M_ORB.to_3x3()
        def cx(c, d, mat, rx=0.0):
            # caixa no espaco local do orbitador
            Ml = M_ORB @ Matrix.Translation(V(c)) @ Matrix.Rotation(rx, 4, "Y") @ Matrix.Diagonal((d[0], d[1], d[2], 1))
            r = bmesh.ops.create_cube(m.bm, size=1, matrix=Ml)
            m._marca(r["verts"], mat)
        cx((31.0, y, FZ + 0.45), (0.55, 0.55, 0.12), ASSENTO)                 # assento
        cx((30.65, y, FZ + 0.95), (0.12, 0.55, 0.9), ASSENTO, rx=-0.18)       # encosto
        cx((30.58, y, FZ + 1.5), (0.14, 0.32, 0.26), ASSENTO)                 # cabeceira
        for s in (-1, 1):
            cx((31.0, y + s * 0.3, FZ + 0.62), (0.45, 0.06, 0.06), PAINEL_CL)  # bracos
            cx((31.0, y + s * 0.22, FZ + 0.2), (0.1, 0.06, 0.4), PAINEL_CL)    # pes
        cx((30.6, y + 0.15, FZ + 1.0), (0.02, 0.06, 0.8), CINTO)
        cx((30.6, y - 0.15, FZ + 1.0), (0.02, 0.06, 0.8), CINTO)
        # manche (RHC) entre os joelhos e pedais
        m.cilindro(O(31.55, y, FZ + 0.35), O(31.6, y, FZ + 0.75), 0.03, PAINEL, 8)
        m.esfera(O(31.62, y, FZ + 0.82), 0.06, ESCURO, 8, 6, escala=(1, 1, 1.4))
        cx((32.0, y, FZ + 0.12), (0.25, 0.4, 0.08), PAINEL)
    assento(0.62)
    assento(-0.62)
    # painel frontal (inclinado), telas MEDS e glareshield
    def caixa_l(c, d, mat, ry=0.0):
        Ml = M_ORB @ Matrix.Translation(V(c)) @ Matrix.Rotation(ry, 4, "Y") @ Matrix.Diagonal((d[0], d[1], d[2], 1))
        r = bmesh.ops.create_cube(m.bm, size=1, matrix=Ml)
        m._marca(r["verts"], mat)
    caixa_l((32.55, 0, FZ + 1.05), (0.25, 2.9, 1.0), PAINEL, ry=0.32)
    caixa_l((32.35, 0, FZ + 1.62), (0.45, 3.0, 0.08), ESCURO)                  # glareshield
    caixa_l((32.0, 0, FZ + 0.5), (0.9, 0.5, 0.9), PAINEL)                      # console central
    n_painel = (M_ORB.to_3x3() @ (Matrix.Rotation(0.32, 3, "Y") @ V((-1, 0, 0)))).normalized()
    up_painel = (M_ORB.to_3x3() @ (Matrix.Rotation(0.32, 3, "Y") @ V((0, 0, 1)))).normalized()
    for k, y in enumerate((1.15, 0.72, 0.29, -0.14, -0.57, -1.0)):           # 6 telas superiores
        c = O(32.43 + 0.03, y - 0.07, FZ + 1.28) - n_painel * 0.0
        m.painel_tex(c + n_painel * 0.005, 0.36, 0.3, n_painel, MFD, cima=up_painel, uv=((k % 3 / 3, 0.5), ((k % 3 + 1) / 3, 0.5), ((k % 3 + 1) / 3, 1), (k % 3 / 3, 1)))
    for k, y in enumerate((0.95, 0.2, -0.55)):                                # 3 telas inferiores
        c = O(32.6, y - 0.07, FZ + 0.88)
        m.painel_tex(c + n_painel * 0.005, 0.36, 0.3, n_painel, MFD, cima=up_painel, uv=((k / 3, 0), ((k + 1) / 3, 0), ((k + 1) / 3, 0.5), (k / 3, 0.5)))
    # fileiras de chaves e luzes no painel frontal (basico)
    for linha in range(2):
        for k in range(22):
            y = 1.35 - k * 0.128
            c = O(32.66 - linha * 0.06, y, FZ + 0.62 - linha * 0.12)
            m.cilindro(c, c + n_painel * 0.035, 0.012, METAL, 5)
            if k % 3 == 0:
                m.esfera(c + n_painel * 0.01 + up_painel * 0.05, 0.012, (LUZ_VERDE, LUZ_AMBAR, LUZ_VERM)[(k + linha) % 3], 5, 3)
    # painel de teto (overhead) sobre os assentos
    caixa_l((31.2, 0, 5.35), (1.3, 2.2, 0.12), PAINEL)
    n_teto = (M_ORB.to_3x3() @ V((0, 0, -1))).normalized()
    for r_ in range(4):
        for k in range(14):
            c = O(30.75 + r_ * 0.28, 0.95 - k * 0.145, 5.29)
            m.cilindro(c, c + n_teto * 0.04, 0.012, METAL, 5)
            if (k + r_) % 4 == 0:
                m.esfera(c + n_teto * 0.005, 0.016, LUZ_AMBAR, 5, 3)
    # paineis laterais com disjuntores (esquerda L1 / direita R1)
    for s in (1, -1):
        y = s * (secao(31.0)[0] - INSET - 0.18)
        caixa_l((31.1, y, FZ + 1.05), (1.0, 0.12, 0.8), PAINEL)
        n_lat = (M_ORB.to_3x3() @ V((0, -s, 0))).normalized()
        for r_ in range(5):
            for k in range(9):
                c = O(30.7 + k * 0.1, y - s * 0.06, FZ + 0.75 + r_ * 0.13)
                m.cilindro(c, c + n_lat * 0.025, 0.014, ESCURO, 6)
    # posto traseiro (aft flight deck) com 2 janelas de observacao do compartimento de carga
    caixa_l((27.05, 0, FZ + 0.6), (0.4, 2.6, 1.2), PAINEL)
    for y in (0.55, -0.55):
        c = O(26.83, y, FZ + 1.75)
        m.painel_tex(c, 0.75, 0.5, M_ORB.to_3x3() @ V((1, 0, 0)), VIDRO_CAB, cima=M_ORB.to_3x3() @ V((0, 0, 1)))
    for k, y in enumerate((0.8, 0.0, -0.8)):
        c = O(27.26, y, FZ + 0.95)
        m.painel_tex(c, 0.42, 0.32, M_ORB.to_3x3() @ V((1, 0, 0)), MFD, cima=M_ORB.to_3x3() @ V((0, 0, 1)),
                     uv=((k / 3, 0), ((k + 1) / 3, 0), ((k + 1) / 3, 0.5), (k / 3, 0.5)))
    # luz de cabine
    for x in (29.0, 31.0):
        m.cilindro(O(x, 0, 5.45), O(x, 0, 5.5), 0.18, LUZ, 10)
    fim(m, 1.0)
    anc = bpy.data.objects.new("NAVE_ANCORA_INTERIOR", None)
    anc.empty_display_type = "ARROWS"; anc.empty_display_size = 1.0
    COL.objects.link(anc)
    anc.matrix_world = M_ORB @ Matrix.Translation(V((30.0, 0.0, 0.6)))           # X local = nariz, Z local = teto do orbitador
    OBJS.append(anc)
    # olhos do comandante sentado: frente = +X local (mundo +Z), cima = +Z local (mundo -Y)
    olho = O(31.0, 0.62, FZ + 1.33)
    emp("SEAT_CDR_Olho", olho)
    emp("SEAT_PLT_Olho", O(31.0, -0.62, FZ + 1.33))


# ======================================================================= montar
base()
torre()
estrutura_rotativa()
orbitador()
tanque_e_boosters()
interior()

malhas = [o for o in OBJS if o.type == "MESH" and not o.name.startswith(("COL_", "ZONA_"))]
print(f"[Plataforma] {len(malhas)} malhas, {contar_tris(malhas)} triangulos; braco em z={BRACO_Z:.2f}, escotilha {tuple(round(c, 2) for c in ESC_W)}")
for o in sorted(malhas, key=lambda o: -contar_tris([o])):
    print(f"   {o.name:32s} {contar_tris([o]):7d} tris")

PREVIEW = arg("--preview")
if PREVIEW:
    os.makedirs(PREVIEW, exist_ok=True)
    for o in OBJS:
        if o.name.startswith(("COL_", "ZONA_")):
            o.hide_render = True
    # chao provisorio so para o preview
    g = Malha("PREVIEW_Chao"); g.caixa_ab((-150, 120, -0.8), (250, 420, -0.15), material("CT_Grama", "#72B85C")); gc = g.criar(COL)
    preparar_render((1280, 720), sol_rot=(math.radians(55), 0, math.radians(200)))
    vistas = {
        "p01_estrada": ((44, 150, 2.0), (44, 290, 30), 22),
        "p02_geral": ((-60, 180, 60), (40, 290, 35), 24),
        "p03_base": ((30, 255, 7.7), (40, 285, 25), 18),
        "p04_braco": ((16, 282.5, BRACO_Z + 1.7), (42, 282.5, BRACO_Z + 1.2), 18),
        "p05_nariz": ((34, 270, 52), (44, 284, 50), 28),
        "p06_cabine": (tuple(O(31.0, 0.62, 2.9 + 1.33)), tuple(O(33.0, 0.62, 2.9 + 1.33 + 0.25)), 16),
        "p07_traseira": ((44, 255, 12), (44, 285, 22), 24),
    }
    so = [a.split("=")[1] for a in args if a.startswith("--vista=")]
    for nome, (pos, alvo, lente) in vistas.items():
        if so and not any(nome.startswith(s) for s in so):
            continue
        if nome == "p06_cabine":
            renderizar(os.path.join(PREVIEW, nome + ".png"), pos, alvo, lente, cima=(0, -1, 0))   # deitado: cima = -Y
            renderizar(os.path.join(PREVIEW, "p08_cabine_teto.png"), pos, tuple(O(31.6, 0.2, 5.4)), lente, cima=(0, -1, 0))
            renderizar(os.path.join(PREVIEW, "p09_cabine_tras.png"), tuple(O(32.2, -0.3, 4.4)), tuple(O(27.0, 0.3, 3.6)), lente, cima=(0, -1, 0))
            continue
        renderizar(os.path.join(PREVIEW, nome + ".png"), pos, alvo, lente)
    bpy.data.objects.remove(gc, do_unlink=True)
    remover_preview()

# ======================================================================= LODs do onibus (exterior): LOD1 ~40%, LOD2 so a silhueta
def lods():
    for nome in ("Onibus_Orbitador", "Onibus_TanqueExterno", "Onibus_Boosters"):
        o = bpy.data.objects.get(nome)
        for sufixo, razao in (("_L1", 0.4), ("_L2", 0.1)):   # nao usar _LODn: o importador do Unity cria LODGroup sozinho
            c = o.copy(); c.data = o.data.copy(); c.name = nome + sufixo; c.data.name = c.name
            COL.objects.link(c)
            mod = c.modifiers.new("Decimate", "DECIMATE"); mod.ratio = razao; mod.use_collapse_triangulate = True
            dg = bpy.context.evaluated_depsgraph_get()
            me = bpy.data.meshes.new_from_object(c.evaluated_get(dg)); c.modifiers.clear(); c.data = me; me.name = c.name
            OBJS.append(c)
            print(f"   {c.name:32s} {contar_tris([c]):7d} tris")
lods()

fbx_p, fbx_o = arg("--fbx"), arg("--fbx-onibus")
if fbx_p:
    exportar_fbx(fbx_p, [o for o in OBJS if not o.name.startswith("Onibus_")])
if fbx_o:
    onibus = [o for o in OBJS if o.name.startswith(("Onibus_", "NAVE_ANCORA", "SEAT_"))]
    desloc = V((SX, SY, ZM))                         # centro do tanque externo no topo da MLP -> origem
    for o in onibus:
        o.matrix_world = Matrix.Translation(-desloc) @ o.matrix_world
    exportar_fbx(fbx_o, onibus)
    for o in onibus:
        o.matrix_world = Matrix.Translation(desloc) @ o.matrix_world
    print(f"[Onibus] exportado centrado: origem = (x {SX}, y {SY}, z {ZM}) do mapa; LODs separados: _L1/_L2")
