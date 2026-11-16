"""
L2: kit modular do corredor super secreto (tubo de 6 m) + porta hexagonal + caminho em S.

  blender -b --factory-startup --python gerar_corredor.py -- --fbx ../Modelos/Corredor_Kit.fbx --json ../Dados/Corredor_Caminho.json [--preview <pasta>]

Pecas (frame local: frente +Y, comprimento 0..4 m, piso em z = 0, eixo do tubo em z = 2):
  Seg_A (costelas, grade, calhas, faixas de luz) · Seg_B (+ canos, valvulas, flanges) · Seg_C (+ telas e paineis)
  Seg_Porta (abertura lateral a direita, +X, com portal) · Seg_Janela (janela a direita para a casa de maquinas)
  Porta_Hex_Moldura · Porta_Hex_Lamina_0..5 (ANIM, deslizam para dentro da moldura) · Porta_Hex_Anel (luz de status)
  Seg_Fim (tampa com a porta do elevador de saida, L4)
O Unity instancia as pecas ao longo do caminho (JSON) — uma peca bem feita repetida = qualidade + desempenho.
"""
import sys, os, math, json
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from mathutils import Vector as V, Matrix
from astrolab_lib import Malha, material, limpar_cena, colecao, colisor, preparar_render, renderizar, remover_preview, exportar_fbx, contar_tris, letreiros_json, LETREIROS

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
def arg(n):
    return args[args.index(n) + 1] if n in args else None

limpar_cena()
COL = colecao("Corredor")
PI = math.pi
R, L, ZEIXO = 3.0, 4.0, 2.0          # raio do tubo, comprimento do modulo, altura do eixo sobre o piso
SOBRA = 0.06                          # sobreposicao entre modulos (esconde frestas nas curvas)

PAINEL = material("CT_Painel_Liso", "#E6E9F0")
PAINEL2 = material("CT_Parede_Interna", "#D5DAE6")
ESTRUT = material("CT_Estrutura", "#5D6E8C")
METAL = material("CT_Metal", "#B4C2D9")
GRADE = material("OR_Grade", "#7D8696")
ESCURO = material("CT_Console", "#2E3550")
AZUL = material("CT_Azul_NASA", "#2456B8")
AZUL_ESC = material("CT_Azul_Escuro", "#17306E")
AMARELO = material("CT_Amarelo", "#FFC23A")
VERMELHO = material("CT_Vermelho_NASA", "#E03C2A")
LARANJA = material("CT_Laranja", "#FF8C3A")
LUZ = material("CT_Luz", "#FFF4DE", emissao=4.0)
LUZ_CIANO = material("CT_Tela", "#47E6FF", emissao=1.5)
LUZ_VERDE = material("CT_Luz_Verde", "#59FF86", emissao=4.0)
VIDRO = material("CT_Vidro", "#A8ECFF", alpha=0.3)
TELA_VR = material("CT_Tela_VR", "#3D8BFF", emissao=1.2)
BORRACHA = material("CT_Borracha", "#33364A")
TXT = material("CT_Texto_Branco", "#FFFFFF")

OBJS = []
def fim(m, uv=1.0):
    o = m.criar(COL, uv)
    if o:
        OBJS.append(o)
    t = getattr(m, "irmao_transp", None)
    if t:
        OBJS.append(t)
    return o

def col(nome, c, d):
    OBJS.append(colisor(nome, c, d, COL))

def P(ang, r=R, y=0.0):
    """ponto na parede do tubo: ang em graus (0 = topo, +90 = direita (+X), -90 = esquerda)."""
    a = math.radians(ang)
    return V((math.sin(a) * r, y, ZEIXO + math.cos(a) * r))

ANG_PISO = math.degrees(math.acos(-ZEIXO / R))          # angulo onde o piso encontra o tubo (~131.8 graus)

def casca(m, y0, y1, buracos=(), segs=40):
    """parede interna do tubo (faces para dentro), acima do piso; buracos = [(ang0, ang1, y0, y1)]."""
    angs = [-ANG_PISO + (2 * ANG_PISO) * k / segs for k in range(segs + 1)]
    ys = sorted(set([y0, y1] + [b[2] for b in buracos] + [b[3] for b in buracos]))
    for b in buracos:
        angs += [b[0], b[1]]
    angs = sorted(set(round(a, 4) for a in angs))
    for i in range(len(ys) - 1):
        for j in range(len(angs) - 1):
            a0, a1, ya, yb = angs[j], angs[j + 1], ys[i], ys[i + 1]
            if any(b[0] - 1e-3 <= a0 and a1 <= b[1] + 1e-3 and b[2] - 1e-3 <= ya and yb <= b[3] + 1e-3 for b in buracos):
                continue
            faixa = PAINEL if int((a0 + 200) // 30) % 2 == 0 else PAINEL2
            m.quad(P(a0, y=ya), P(a0, y=yb), P(a1, y=yb), P(a1, y=ya), faixa)       # normal para o eixo (vista de dentro)

def base_segmento(m, variante="A", buracos=()):
    y0, y1 = 0.0, L + SOBRA
    casca(m, y0, y1, buracos)
    # piso de grade + rodapes chanfrados
    w = math.sqrt(R * R - ZEIXO * ZEIXO)
    m.caixa_ab((-w + 0.05, y0, -0.12), (w - 0.05, y1, 0.0), GRADE)
    for s in (-1, 1):
        m.caixa_ab((min(s * (w - 0.45), s * (w - 0.05)), y0, 0.0), (max(s * (w - 0.45), s * (w - 0.05)), y1, 0.12), ESTRUT)
        m.caixa_ab((min(s * (w - 0.5), s * (w - 0.45)), y0, 0.12), (max(s * (w - 0.5), s * (w - 0.45)), y1, 0.16), AMARELO)
    # costela estrutural no inicio do modulo (arco + chapas de reforco)
    for k in range(24):
        a0 = -ANG_PISO + 2 * ANG_PISO * k / 24; a1 = -ANG_PISO + 2 * ANG_PISO * (k + 1) / 24
        p0, p1 = P(a0, R - 0.02, 0.15), P(a1, R - 0.02, 0.15)
        q0, q1 = P(a0, R - 0.24, 0.15), P(a1, R - 0.24, 0.15)
        for dy in (0.0, 0.3):
            m.quad(q0 + V((0, dy, 0)), q1 + V((0, dy, 0)), p1 + V((0, dy, 0)), p0 + V((0, dy, 0)), ESTRUT, duplo=True)
        m.quad(q0, q1, q1 + V((0, 0.3, 0)), q0 + V((0, 0.3, 0)), ESTRUT, duplo=True)
    for a in (-100, -55, 0, 55, 100):
        p = P(a, R - 0.25, 0.3)
        m.caixa(p, (0.18, 0.06, 0.18), METAL, ry=math.radians(a))
    # faixa de luz no teto + faixas ciano baixas nas laterais
    for (a0, a1, mat) in ((-6, 6, LUZ), (-104, -100, LUZ_CIANO), (100, 104, LUZ_CIANO)):
        m.quad(P(a0, R - 0.03, 0.5), P(a0, R - 0.03, L - 0.3), P(a1, R - 0.03, L - 0.3), P(a1, R - 0.03, 0.5), mat)
    # calhas de cabo nas laterais altas (com cabos coloridos)
    for s in (-1, 1):
        c = P(s * 48, R - 0.35)
        m.caixa((c.x, L / 2, c.z), (0.42, L + SOBRA, 0.06), METAL, ry=math.radians(s * 48))
        for k, mat in enumerate((AZUL, VERMELHO, AMARELO, BORRACHA)):
            off = (k - 1.5) * 0.08
            cc = P(s * 48, R - 0.42) + V((math.cos(math.radians(s * 48)) * off, 0, -math.sin(math.radians(s * 48)) * off))
            m.cilindro((cc.x, 0, cc.z), (cc.x, L + SOBRA, cc.z), 0.028, mat, 8)
    if variante in ("B",):
        for k, (a, r_, mat) in enumerate(((-112, 0.11, AMARELO), (-118, 0.07, METAL), (112, 0.13, AZUL), (118, 0.07, VERMELHO))):
            c = P(a, R - 0.35)
            m.cilindro((c.x, 0, c.z), (c.x, L + SOBRA, c.z), r_, mat, 16)
            m.cilindro((c.x, 1.95, c.z), (c.x, 2.05, c.z), r_ * 1.45, ESTRUT, 16)            # flange
        c = P(112, R - 0.35)
        m.cilindro((c.x, 2.0, c.z), (c.x - 0.35, 2.0, c.z + 0.12), 0.05, METAL, 10)            # valvula com volante
        m.torus((c.x - 0.38, 2.0, c.z + 0.13), 0.14, 0.02, VERMELHO, 20, 6, M=Matrix.Rotation(PI / 2, 4, "Y") @ Matrix.Translation(V((0, 0, 0))))
    if variante in ("C",):
        for s in (-1, 1):
            c = P(s * 78, R - 0.12, 2.0)
            n = (V((0, 0, ZEIXO)) - V((c.x, 0, c.z))).normalized()
            m.caixa(c, (0.08, 1.7, 1.0), ESCURO, ry=math.radians(s * 78) + PI / 2 * 0)
            m.painel_tex(c + n * 0.05, 1.55, 0.85, n, TELA_VR if s > 0 else LUZ_CIANO, cima=(0, 0, 1))
    # placa de sentido no piso (setas)
    m.caixa((0, 1.0, 0.005), (0.18, 0.7, 0.01), AMARELO)
    return m

def colisores_segmento(nome, porta=False, janela=False):
    w = math.sqrt(R * R - ZEIXO * ZEIXO)
    col(f"COL_{nome}_Piso", (0, L / 2, -0.06), (2 * w, L + SOBRA, 0.12))
    col(f"COL_{nome}_Esq", (-w + 0.2, L / 2, 1.8), (0.4, L + SOBRA, 3.6))
    if not porta:
        col(f"COL_{nome}_Dir", (w - 0.2, L / 2, 1.8), (0.4, L + SOBRA, 3.6))
    else:
        col(f"COL_{nome}_Dir_A", (w - 0.2, 0.3, 1.8), (0.4, 0.6, 3.6))
        col(f"COL_{nome}_Dir_B", (w - 0.2, L - 0.3, 1.8), (0.4, 0.6, 3.6))
    col(f"COL_{nome}_Teto", (0, L / 2, 4.6), (2 * w, L + SOBRA, 0.3))

# ======================================================================= pecas do kit
for v in ("A", "B", "C"):
    m = base_segmento(Malha(f"Seg_{v}"), v)
    fim(m)
    colisores_segmento(f"Seg_{v}")

# segmento com porta (lado direito, +X): buraco na casca + portal retangular que leva a sala
BURACO = (52.0, 116.0, 0.25, L - 0.25)
m = base_segmento(Malha("Seg_Porta"), "A", [BURACO])
w0 = math.sqrt(R * R - ZEIXO * ZEIXO)
for (y0, y1) in ((0.0, 0.25), (L - 0.25, L + SOBRA)):
    pass
# tunel do portal (do tubo ate x = 4.6) com faixas de alerta
for s, y in ((-1, 0.25), (1, L - 0.25)):
    m.caixa_ab((2.2, min(y, y - s * 0.2), 0.0), (4.6, max(y, y - s * 0.2), 3.7), ESTRUT)
m.caixa_ab((2.2, 0.25, 3.5), (4.6, L - 0.25, 3.7), ESTRUT)
m.caixa_ab((2.2, 0.25, -0.12), (4.6, L - 0.25, 0.0), GRADE)
for k in range(8):
    m.caixa((2.25 + k * 0.3, L / 2, 0.006), (0.15, L - 0.6, 0.012), AMARELO if k % 2 == 0 else ESCURO)
fim(m)
colisores_segmento("Seg_Porta", porta=True)
col("COL_Seg_Porta_Portal_A", (3.4, 0.15, 1.85), (2.4, 0.3, 3.7))
col("COL_Seg_Porta_Portal_B", (3.4, L - 0.15, 1.85), (2.4, 0.3, 3.7))

# segmento com janela panoramica para a casa de maquinas (momento do corredor)
JAN = (40.0, 110.0, 0.4, L - 0.4)
m = base_segmento(Malha("Seg_Janela"), "C", [JAN])
for a in (40.0, 110.0):
    p0, p1 = P(a, R, 0.4), P(a, R, L - 0.4)
    m.cilindro(p0, p1, 0.08, ESTRUT, 12)
for y in (0.4, L - 0.4):
    pts = [P(a, R, y) for a in range(40, 111, 10)]
    for a_, b_ in zip(pts[:-1], pts[1:]):
        m.cilindro(a_, b_, 0.08, ESTRUT, 12)
vid = Malha("Seg_Janela_Vidro")
for k in range(7):
    a0, a1 = 40 + k * 10, 50 + k * 10
    vid.quad(P(a0, R + 0.02, 0.4), P(a1, R + 0.02, 0.4), P(a1, R + 0.02, L - 0.4), P(a0, R + 0.02, L - 0.4), VIDRO, duplo=True)
fim(m); fim(vid)
colisores_segmento("Seg_Janela")
# casa de maquinas atras da janela: poco grande com turbinas, canos e passarelas (cenario)
cm = Malha("Seg_Janela_CasaMaquinas")
cm.caixa_ab((3.3, -6.0, -8.0), (16.0, 10.0, -7.6), GRADE)
cm.caixa_ab((15.6, -6.0, -8.0), (16.0, 10.0, 12.0), ESTRUT)
cm.caixa_ab((3.3, -6.0, -8.0), (16.0, -5.6, 12.0), ESTRUT)
cm.caixa_ab((3.3, 9.6, -8.0), (16.0, 10.0, 12.0), ESTRUT)
cm.caixa_ab((3.3, -6.0, 11.6), (16.0, 10.0, 12.0), ESTRUT)
for k, y in enumerate((-2.0, 2.0, 6.0)):                               # 3 turbinas/geradores
    cm.cilindro((7.5, y, -7.6), (7.5, y, -4.0), 1.6, AZUL, 32)
    cm.cilindro((7.5, y, -4.0), (7.5, y, -3.6), 1.75, METAL, 32)
    cm.torus((7.5, y, -5.8), 1.62, 0.08, AMARELO, 32, 6)
    cm.cilindro((7.5, y, -3.6), (7.5, y, 8.0), 0.35, METAL, 16)
    cm.cilindro((7.5, y, 1.0), (15.6, y, 1.0), 0.3, VERMELHO if k == 1 else AMARELO, 16)
    cm.cilindro((7.5, y, -3.0), (7.5, y, -2.95), 0.4, LUZ_VERDE, 16)
cm.caixa_ab((10.5, -6.0, 0.0), (11.5, 10.0, 0.1), GRADE)                # passarela
for y in range(-5, 10, 2):
    cm.cilindro((10.5, y, 0.1), (10.5, y, 1.1), 0.03, AMARELO, 8)
cm.cilindro((10.5, -6, 1.1), (10.5, 10, 1.1), 0.03, AMARELO, 8)
for y in (-3.0, 3.0, 8.0):
    cm.caixa((15.5, y, 6.0), (0.1, 1.8, 0.4), LUZ)
fim(cm)

# porta hexagonal (raio do circulo 1,8 m), plano vertical voltado para -X (para o corredor), centro em z = 1.85
RH, ZH = 1.75, 1.85
def hexv(r, k):
    a = math.radians(60 * k)
    return V((0, math.cos(a) * r, ZH + math.sin(a) * r))
fr = Malha("Porta_Hex_Moldura", origem=(2.6, L / 2, 0))
pc = V((2.6, L / 2, 0))
for k in range(6):
    a_o, b_o = hexv(RH + 0.35, k), hexv(RH + 0.35, k + 1)
    a_i, b_i = hexv(RH, k), hexv(RH, k + 1)
    for dx in (-0.12, 0.12):
        pts = [x + pc + V((dx, 0, 0)) for x in (a_i, b_i, b_o, a_o)]
        fr.quad(*pts, ESTRUT, duplo=True)
    for (p0, p1) in ((a_o, b_o), (a_i, b_i)):
        fr.quad(p0 + pc + V((-0.12, 0, 0)), p1 + pc + V((-0.12, 0, 0)), p1 + pc + V((0.12, 0, 0)), p0 + pc + V((0.12, 0, 0)), ESTRUT, duplo=True)
    m_ = (a_o + b_o) / 2 + pc
    fr.caixa(m_ + V((-0.14, 0, 0)), (0.06, 0.28, 0.28), AMARELO)                 # parafusos/blocos de travamento
fr.caixa(pc + V((-0.15, 0, ZH + RH + 0.7)), (0.08, 2.6, 0.5), AZUL_ESC)           # placa (numero + nome em TMP no Unity)
fim(fr)
anel = Malha("Porta_Hex_Anel", origem=(2.6, L / 2, 0))
for k in range(6):
    a_, b_ = hexv(RH + 0.42, k) + pc + V((-0.16, 0, 0)), hexv(RH + 0.42, k + 1) + pc + V((-0.16, 0, 0))
    anel.cilindro(a_, b_, 0.05, LUZ_VERDE, 8)
fim(anel)
for k in range(6):                                                               # 6 laminas (iris)
    a_, b_ = hexv(RH + 0.02, k), hexv(RH + 0.02, k + 1)
    cen = V((0, 0, ZH))
    lam = Malha(f"Porta_Hex_Lamina_{k}", origem=(2.6, L / 2, ZH))
    pts = [cen + pc, a_ + pc, b_ + pc]
    tri = [(q.y - pc.y, q.z) for q in pts]
    lam.prisma(tri, -0.05, 0.05, METAL if k % 2 == 0 else PAINEL2, M=Matrix.Translation(V((2.6, 0, 0))) @ Matrix(((0, 0, 1, 0), (1, 0, 0, 0), (0, 1, 0, 0), (0, 0, 0, 1))) @ Matrix.Translation(V((0, 0, 0))) @ Matrix.Translation(V((pc.y, 0, 0))) if False else Matrix(((0, 0, 1, 2.6), (1, 0, 0, pc.y), (0, 1, 0, 0), (0, 0, 0, 1))))
    meio = (a_ + b_) / 2 + pc
    lam.cilindro(meio + V((-0.06, 0, 0)) - ((meio - (cen + pc)) * 0.35), meio + V((-0.06, 0, 0)) - ((meio - (cen + pc)) * 0.15), 0.03, AMARELO, 8)
    fim(lam)

# ======================================================================= L4: fim do corredor -> elevador -> quiosque na superficie
# (tudo no espaco do modulo Seg_Fim: o tubo chega por -Y, o poco fica atras da tampa, a superficie esta em z = +20)
SUP = 20.0                                           # o corredor fica 20 m abaixo do terreno
PW, PH = 2.2, 3.2                                    # vao da porta
XP, Y0P, Y1P = 1.7, 0.24, 3.5                        # poco: x -1.7..1.7, y 0.24..3.5
RT = R + 0.05
XZ = Matrix(((1, 0, 0, 0), (0, 0, 1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))   # poligono (x, z) extrudado em y
z_b = ZEIXO - math.sqrt(RT * RT - (PW / 2) ** 2)
arco = [(math.sin(math.radians(a_)) * RT, ZEIXO + math.cos(math.radians(a_)) * RT) for a_ in range(-180, 181, 6)]
arco = [p for p in arco if not (abs(p[0]) < PW / 2 and p[1] < 0)]
ini = min(range(len(arco)), key=lambda k: (arco[k][0] - PW / 2) ** 2 + (arco[k][1] - z_b) ** 2 if arco[k][0] > 0 else 9e9)
contorno = arco[ini:] + arco[:ini]
contorno = [(PW / 2, z_b), (PW / 2, PH), (-PW / 2, PH), (-PW / 2, z_b)] + [p for p in reversed(contorno)]
fm = Malha("Seg_Fim")
fm.prisma(contorno, 0.12, 0.24, ESTRUT, M=XZ)                                  # tampa do tubo com o vao da porta
for s_ in (-1, 1):                                                              # batentes amarelos/pretos
    fm.caixa((s_ * (PW / 2 + 0.08), 0.08, PH / 2), (0.16, 0.12, PH), AMARELO)
fm.caixa((0, 0.08, PH + 0.08), (PW + 0.32, 0.12, 0.16), AMARELO)
for k in range(7):
    fm.caixa((-PW / 2 - 0.08, 0.015, 0.25 + k * 0.45), (0.17, 0.01, 0.12), ESCURO, rx=0.0)
fm.caixa((0, 0.1, PH + 0.5), (2.6, 0.08, 0.5), AZUL_ESC)                       # placa (texto TMP no Unity)
fm.caixa((1.6, 0.08, 1.3), (0.2, 0.06, 0.4), ESCURO)                           # botoeira
fm.cilindro((1.6, 0.04, 1.38), (1.6, 0.0, 1.38), 0.06, LUZ_VERDE, 16)
fm.caixa_ab((-PW / 2, -0.3, -0.15), (PW / 2, (Y0P + Y1P) / 2 + 0.1 - (Y1P - Y0P - 0.3) / 2 - 0.01, 0.0), ESTRUT)   # soleira: piso do tubo -> cabine
fm.caixa_ab((-PW / 2, -0.3, 0.0), (PW / 2, -0.1, 0.012), AMARELO)
fim(fm)
for s_ in (-1, 1):
    col(f"COL_Seg_Fim_{'E' if s_ < 0 else 'D'}", (s_ * (PW / 2 + (RT - PW / 2) / 2), 0.18, 2.0), (RT - PW / 2, 0.2, 4.6))
col("COL_Seg_Fim_Soleira", (0, ((Y0P + Y1P) / 2 + 0.1 - (Y1P - Y0P - 0.3) / 2 - 0.01 - 0.3) / 2, -0.08), (PW, (Y0P + Y1P) / 2 + 0.1 - (Y1P - Y0P - 0.3) / 2 - 0.01 + 0.3, 0.16))
col("COL_Seg_Fim_Topo", (0, 0.18, (PH + ZEIXO + RT) / 2), (PW, 0.2, ZEIXO + RT - PH))

# poco do elevador (do piso do corredor ate o telhado do quiosque), com os vaos de porta embaixo e em cima
po = Malha("Elevador_Poco")
ZT = SUP + 4.2
for s_ in (-1, 1):
    po.caixa((s_ * (XP + 0.1), (Y0P + Y1P) / 2, (ZT - 0.6) / 2), (0.2, Y1P - Y0P + 0.4, ZT + 0.6), PAINEL2)
po.caixa((0, Y1P + 0.1, (ZT - 0.6) / 2), (2 * XP + 0.4, 0.2, ZT + 0.6), PAINEL2)
po.caixa((0, Y0P + 0.05, (PH + SUP) / 2), (2 * XP + 0.4, 0.1, SUP - PH), PAINEL2)    # frente entre as duas portas
po.caixa((0, Y0P + 0.05, (SUP + PH + ZT) / 2), (2 * XP + 0.4, 0.1, ZT - SUP - PH), PAINEL2)
for s_ in (-1, 1):
    po.caixa((s_ * (PW / 2 + (XP + 0.2 - PW / 2) / 2), Y0P + 0.05, SUP + PH / 2), (XP + 0.2 - PW / 2, 0.1, PH), PAINEL2)
    for z_ in range(0, int(ZT), 3):                                                # guias da cabine
        po.caixa((s_ * (XP - 0.05), Y1P - 0.3, z_ + 1.5), (0.08, 0.12, 3.0), METAL)
po.caixa((0, (Y0P + Y1P) / 2, ZT + 0.1), (2 * XP + 0.4, Y1P - Y0P + 0.4, 0.2), ESTRUT)
fim(po)
col("COL_Poco_E", (-(XP + 0.1), (Y0P + Y1P) / 2, ZT / 2), (0.2, Y1P - Y0P, ZT))
col("COL_Poco_D", ((XP + 0.1), (Y0P + Y1P) / 2, ZT / 2), (0.2, Y1P - Y0P, ZT))
col("COL_Poco_Fundo", (0, Y1P + 0.1, ZT / 2), (2 * XP, 0.2, ZT))

# cabine (ANIM: sobe 20 m), portas de correr na frente (-Y)
YC = (Y0P + Y1P) / 2 + 0.1
cb = Malha("ANIM_ElevSaida_Cabine", origem=(0, YC, 0.0))
cb.caixa((0, YC, 0.05), (2 * XP - 0.2, Y1P - Y0P - 0.3, 0.1), BORRACHA)
for s_ in (-1, 1):
    cb.caixa((s_ * (XP - 0.15), YC, 1.6), (0.06, Y1P - Y0P - 0.3, 3.1), METAL)
    cb.cilindro((s_ * (XP - 0.22), YC - 1.0, 0.95), (s_ * (XP - 0.22), YC + 1.0, 0.95), 0.025, AMARELO, 12)
cb.caixa((0, Y1P - 0.2, 1.6), (2 * XP - 0.2, 0.06, 3.1), METAL)
cb.caixa((0, YC, 3.2), (2 * XP - 0.2, Y1P - Y0P - 0.3, 0.1), PAINEL)
cb.caixa((0, YC, 3.13), (1.4, 0.6, 0.04), LUZ)
cb.caixa((0, Y0P + 0.42, PH + 0.2), (2 * XP - 0.2, 0.08, 0.4), AZUL_ESC)          # visor (TMP no Unity)
fim(cb)
for k, s_ in enumerate((-1, 1)):
    pt = Malha(f"ANIM_ElevSaida_Porta{'E' if s_ < 0 else 'D'}", origem=(s_ * 0.55, Y0P + 0.3, 0))
    pt.caixa((s_ * 0.55, Y0P + 0.3, PH / 2), (1.1, 0.05, PH - 0.04), METAL)
    pt.caixa((s_ * 0.55, Y0P + 0.27, PH / 2), (0.05, 0.01, PH - 0.3), ESTRUT)
    fim(pt)

# quiosque na superficie (vidro + faixa azul NASA), porta para -Y (estrada)
qk = Malha("Quiosque")
QX0, QX1, QY0, QY1 = -3.6, 3.6, -4.2, Y1P + 0.3
YCAB = (Y0P + Y1P) / 2 + 0.1 - (Y1P - Y0P - 0.3) / 2 - 0.01                    # borda da frente do piso da cabine
for (a_, b_) in (((QX0 - 0.3, QY0 - 0.6), (QX1 + 0.3, YCAB)),                                        # laje/calcada sem o vao do poco
                 ((QX0 - 0.3, YCAB), (-XP - 0.2, QY1 + 0.3)), ((XP + 0.2, YCAB), (QX1 + 0.3, QY1 + 0.3)),
                 ((-XP - 0.2, Y1P + 0.2), (XP + 0.2, QY1 + 0.3))):
    qk.caixa_ab((a_[0], a_[1], SUP - 0.25), (b_[0], b_[1], SUP + 0.02), PAINEL)
qk.caixa_ab((-PW / 2, YCAB - 0.2, SUP + 0.02), (PW / 2, YCAB, SUP + 0.03), AMARELO)                   # faixa de aviso na borda
qk.caixa_ab((QX0, QY0, SUP + 3.6), (QX1, QY1, SUP + 3.9), PAINEL)                                     # cobertura
qk.caixa_ab((QX0 - 0.6, QY0 - 0.9, SUP + 3.9), (QX1 + 0.6, QY1 + 0.4, SUP + 4.1), AZUL)              # beiral
for (x_, y_) in ((QX0, QY0), (QX1, QY0), (QX0, QY1), (QX1, QY1)):
    qk.caixa((x_, y_, SUP + 1.8), (0.25, 0.25, 3.6), ESTRUT)
for s_ in (-1, 1):                                                                                    # paredes laterais
    qk.caixa((s_ * QX1, (QY0 + QY1) / 2, SUP + 0.5), (0.2, QY1 - QY0, 1.0), PAINEL2)
    qk.caixa((s_ * QX1, (QY0 + QY1) / 2, SUP + 2.3), (0.06, QY1 - QY0 - 0.3, 2.6), VIDRO)
    qk.caixa((s_ * (QX1 - 0.12), (QY0 + QY1) / 2, SUP + 1.0), (0.04, QY1 - QY0, 0.12), AZUL)
for x0_, x1_ in ((QX0, -1.3), (1.3, QX1)):                                                           # frente com vao central
    qk.caixa(((x0_ + x1_) / 2, QY0, SUP + 0.5), (x1_ - x0_, 0.2, 1.0), PAINEL2)
    qk.caixa(((x0_ + x1_) / 2, QY0, SUP + 2.3), (x1_ - x0_, 0.06, 2.6), VIDRO)
qk.caixa((0, QY0 - 0.05, SUP + 3.35), (7.2, 0.1, 0.5), AZUL_ESC)                                       # faixa do letreiro (TMP)
qk.caixa((-2.4, QY0 + 0.6, SUP + 0.5), (1.4, 0.5, 1.0), ESCURO)                                       # totem de informacao
qk.painel_tex((-2.4, QY0 + 0.34, SUP + 1.25), 1.1, 0.5, (0, -1, 0), LUZ_CIANO)
fim(qk)
col("COL_Quiosque_Laje", (0, (QY0 - 0.6 + YCAB) / 2, SUP - 0.12), (QX1 - QX0 + 0.6, YCAB - QY0 + 0.6, 0.25))
for s_ in (-1, 1):
    col(f"COL_Quiosque_Lado_{'E' if s_ < 0 else 'D'}", (s_ * QX1, (QY0 + QY1) / 2, SUP + 1.8), (0.25, QY1 - QY0, 3.6))
    col(f"COL_Quiosque_Frente_{'E' if s_ < 0 else 'D'}", (s_ * (1.3 + QX1) / 2, QY0, SUP + 1.8), (QX1 - 1.3, 0.25, 3.6))
col("COL_Quiosque_Fundo", (0, QY1, SUP + 1.8), (QX1 - QX0, 0.25, 3.6))
col("COL_ElevSaida_TopoBloqueio", (0, Y0P - 0.05, SUP + PH / 2), (PW, 0.1, PH))

for o in OBJS:
    if o.type == "MESH" and not o.name.startswith("COL_"):
        print(f"   {o.name:28s} {contar_tris([o]):6d} tris")

# ======================================================================= caminho em S (plano XY do mapa, piso em z = -20)
INICIO = V((15.3, 16.2, -20.0))        # parede leste do vestibulo do elevador secreto (L1)
RUMO0 = 0.0                             # graus: sai para leste (+X), vira para o norte (rumo a plataforma)
pecas_caminho = [("reta", 8.0), ("arco", 30.0, 90.0), ("reta", 20.0), ("arco", 40.0, 30.0), ("reta", 8.0), ("arco", 40.0, -30.0), ("reta", 60.0), ("arco", 12.0, 90.0), ("reta", 4.0)]   # L4: curva para oeste, quiosque ao lado da estrada
segs = []
pos, rumo = V(INICIO), RUMO0
for p_ in pecas_caminho:
    if p_[0] == "reta":
        n = int(round(p_[1] / L))
        for _ in range(n):
            segs.append((V(pos), rumo)); pos += V((math.cos(math.radians(rumo)), math.sin(math.radians(rumo)), 0)) * L
    else:
        _, raio, ang = p_
        comp = abs(math.radians(ang)) * raio; n = int(round(comp / L)); dang = ang / n
        for _ in range(n):
            meio = rumo + dang / 2
            segs.append((V(pos), meio)); pos += V((math.cos(math.radians(meio)), math.sin(math.radians(meio)), 0)) * L
            rumo += dang
segs.append((V(pos), rumo))
N = len(segs)
SALAS = {4: ("Medico", 1), 11: ("Centrifuga", 1),            # centrifuga por fora da curva (por dentro batia no corredor)
          18: ("RealidadeVirtual", 1), 26: ("NBL", -1), 33: ("ARGOS", 1), 40: ("Trajes", -1)}
JANELAS = {15: 1, 29: 1}
seq = "ABAC"
lista = []
for i, (p_, r_) in enumerate(segs):
    if i == N - 1:
        tipo = "Seg_Fim"
    elif i in SALAS:
        tipo = "Seg_Porta"
    elif i in JANELAS:
        tipo = "Seg_Janela"
    else:
        tipo = "Seg_" + seq[i % 4]
    lado = SALAS[i][1] if i in SALAS else (JANELAS.get(i, 1))
    U = lambda v: [round(-v.x, 4), round(v.z, 4), round(-v.y, 4)]
    # rumo no plano do Blender (graus a partir de +X); o modulo anda em +Y local: rotacao Z = rumo - 90
    lista.append({"i": i, "tipo": tipo, "pos": U(p_), "rumoBlender": round(r_, 3), "lado": lado,
                  "sala": SALAS[i][0] if i in SALAS else ""})
print(f"[Corredor] {N} modulos, ~{N * L:.0f} m; fim em {tuple(round(c, 1) for c in pos)}")
saida_json = arg("--json")
if saida_json:
    os.makedirs(os.path.dirname(os.path.abspath(saida_json)), exist_ok=True)
    json.dump({"modulos": lista, "comprimento": L, "raio": R, "eixo": ZEIXO}, open(saida_json, "w", encoding="utf-8"), ensure_ascii=False, indent=1)
    print("[Corredor] caminho:", saida_json)

prev = arg("--preview")
if prev:
    os.makedirs(prev, exist_ok=True)
    for o in OBJS:
        if o.name.startswith("COL_"):
            o.hide_render = True
    # monta 4 modulos em fila (A, B, Porta com a porta hexagonal, C) so para o preview
    base = [o for o in OBJS if o.type == "MESH" and not o.name.startswith("COL_")]
    for o in base:
        o.hide_render = True
    vis = []
    for k, nome in enumerate(("Seg_A", "Seg_B", "Seg_Porta", "Seg_C", "Seg_Janela")):
        for o in base:
            if o.name == nome or (nome == "Seg_Porta" and o.name.startswith("Porta_Hex")) or (nome == "Seg_Janela" and o.name.startswith("Seg_Janela")):
                c = o.copy(); bpy.context.scene.collection.objects.link(c); c.hide_render = False
                c.location = o.location + V((0, k * L, 0)); vis.append(c)
    luz = bpy.data.lights.new("p", "POINT"); luz.energy = 400; lo = bpy.data.objects.new("p", luz); bpy.context.scene.collection.objects.link(lo); lo.location = (0, 10, 3.5)
    preparar_render((960, 540), mundo="#20263A")
    renderizar(os.path.join(prev, "corredor.png"), (-1.2, -1.5, 1.7), (1.2, 9.0, 1.6), 18)
    for c in vis:
        bpy.data.objects.remove(c, do_unlink=True)
    remover_preview()
fbx = arg("--fbx")
if fbx:
    exportar_fbx(fbx, OBJS)
    letreiros_json(os.path.join(os.path.dirname(os.path.abspath(fbx)), "..", "Dados", "Letreiros_Corredor.json"), "TXTC")
