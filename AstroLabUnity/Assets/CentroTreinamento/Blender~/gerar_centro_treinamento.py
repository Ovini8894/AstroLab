"""
Centro de Treinamento de Astronautas (inspirado no NASA Johnson Space Center, Houston).

Uso (PowerShell, a partir de AstroLabUnity):
  & "C:\\Program Files\\Blender Foundation\\Blender 5.2\\blender.exe" -b --factory-startup `
      --python "Assets/CentroTreinamento/Blender~/gerar_centro_treinamento.py" -- `
      --fbx "Assets/CentroTreinamento/Modelos/CentroTreinamento.fbx" `
      --blend "Assets/CentroTreinamento/Blender~/CentroTreinamento.blend" `
      --preview "<pasta para PNGs de conferencia>"

Layout (metros, Blender: X leste, Y norte, Z cima):
  praca + spawn (y < 0) -> hangar 120 x 50 x 18 (Building 9 / SVMF) com recepcao e 6 estacoes de treino
  -> porta norte (x = 44) -> estrada ate a plataforma de lancamento (y ~ 240, gerada em gerar_plataforma_lancamento.py)

Estacoes (ordem do tutorial):
  0 Recepcao  1 Avaliacao medica e preparo fisico  2 Centrifuga humana  3 Realidade virtual
  4 Flutuabilidade neutra (NBL)  5 Gravidade reduzida (ARGOS + campo lunar)  6 Sala de trajes -> saida
"""
import sys, os, math, random
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from mathutils import Vector as V, Matrix
from astrolab_lib import (Malha, material, vazio, colisor, limpar_cena, colecao, preparar_render, renderizar,
                          remover_preview, exportar_fbx, contar_tris, letreiros_json)

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
def arg(n):
    return args[args.index(n) + 1] if n in args else None
SAIDA_FBX, SAIDA_BLEND, PREVIEW = arg("--fbx"), arg("--blend"), arg("--preview")
SO_PREVIEW = "--so-preview" in args

limpar_cena()
COL = colecao("CentroTreinamento")
PI = math.pi
rnd = random.Random(42)

# ======================================================================= materiais (nomes = receitas no Unity)
PAREDE = material("CT_Parede", "#EDE6D6")            # fachada creme
PAREDE_INT = material("CT_Parede_Interna", "#D5DAE6")
AZUL = material("CT_Azul_NASA", "#2456B8")           # azul NASA (um pouco mais claro: leitura cartoon)
AZUL_ESC = material("CT_Azul_Escuro", "#17306E")
ESTRUT = material("CT_Estrutura", "#5D6E8C")         # vigas e pilares
METAL = material("CT_Metal", "#B4C2D9")
PISO = material("CT_Piso_Hangar", "#A9B1C2")
AMARELO = material("CT_Amarelo", "#FFC23A")
VERMELHO = material("CT_Vermelho_NASA", "#E03C2A")
BRANCO = material("CT_Branco", "#F8F5EE")
ESCURO = material("CT_Console", "#2E3550")
BORRACHA = material("CT_Borracha", "#33364A")
VIDRO = material("CT_Vidro", "#A8ECFF", alpha=0.3)
TELA = material("CT_Tela", "#47E6FF", emissao=1.5)
TELA_ECG = material("CT_Tela_ECG", "#59FF86", emissao=1.5)
TELA_VR = material("CT_Tela_VR", "#3D8BFF", emissao=1.2)
LUZ = material("CT_Luz", "#FFF4DE", emissao=4.0)
LUZ_VERDE = material("CT_Luz_Verde", "#59FF86", emissao=4.0)
LUZ_AMBAR = material("CT_Luz_Ambar", "#FFB45C", emissao=4.0)
LUZ_VERM = material("CT_Luz_Vermelha", "#FF4A4A", emissao=4.0)
GRAMA = material("CT_Grama", "#72B85C")
ASFALTO = material("CT_Asfalto", "#4A4E5C")
CALCADA = material("CT_Calcada", "#D2CCBE")
FOLHA = material("CT_Folha", "#4C9A48")
FOLHA2 = material("CT_Folha_Clara", "#7CC25A")
TRONCO = material("CT_Tronco", "#7A5A44")
AGUA = material("CT_Agua", "#3AA7F0", alpha=0.55)
AZULEJO = material("CT_Azulejo", "#8FD3F0")
REGOLITO = material("CT_Regolito", "#A9A5C0")
ROCHA = material("CT_Rocha", "#7F7A9E")
TECIDO = material("CT_Tecido_Azul", "#3D5BC9")
LARANJA = material("CT_Laranja", "#FF8C3A")
MADEIRA = material("CT_Madeira", "#C79A6B")
TAPETE = material("CT_Tapete_Azul", "#5E7FD6")
VERDE_BR = material("CT_Verde_Bandeira", "#1E9A4A")
TXT_BRANCO = material("CT_Texto_Branco", "#FFFFFF")
TXT_ESCURO = material("CT_Texto_Escuro", "#1C1A3D")
CONCRETO_P = material("OR_Concreto", "#B8B4A8")
PAINEL_LISO = material("CT_Painel_Liso", "#E6E9F0")
MADEIRA_CL = material("CT_Madeira_Clara", "#D9B98C")
SOFA = material("CT_Sofa", "#3D5BC9")
FOTO_A = material("CT_Foto_Missao", "#FFFFFF")
GRADE_P = material("OR_Grade", "#7D8696")

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
    o = colisor(nome, c, d, COL)
    o.name = nome                      # ZONA_* vira trigger no Unity
    OBJS.append(o)


# ======================================================================= utilidades de props
def orient(normal, cima=(0, 0, 1)):
    """matriz de rotacao: Z local -> normal, Y local -> 'cima' projetado."""
    n = V(normal).normalized()
    up = V(cima)
    if abs(n.dot(up)) > 0.99:
        up = V((0, 1, 0))
    x = up.cross(n).normalized()
    y = n.cross(x).normalized()
    return Matrix(((x.x, y.x, n.x, 0), (x.y, y.y, n.y, 0), (x.z, y.z, n.z, 0), (0, 0, 0, 1)))


TEX_DIR = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Texturas"))
LOGO_PNG = os.path.join(TEX_DIR, "T_Logo_NASA.png")


def meatball_geo(m, M, R):
    """logo 'meatball' da NASA desenhado em geometria (plano XY local, de frente para +Z). So para gerar a textura."""
    az, br, vm = material("LOGO_Azul", "#0B3D91", 1.0), material("LOGO_Branco", "#FFFFFF", 1.0), material("LOGO_Vermelho", "#FC3D21", 1.0)
    m.cilindro(M @ V((0, 0, -0.02 * R)), M @ V((0, 0, 0)), R, az, 64)
    z = R * 0.01
    for sx, sy, sr in ((-0.62, 0.42, 0.035), (-0.35, 0.68, 0.025), (0.12, 0.78, 0.03), (0.5, 0.62, 0.022), (-0.75, -0.05, 0.02),
                       (0.68, -0.38, 0.028), (0.3, -0.72, 0.022), (-0.45, -0.68, 0.03), (0.82, 0.12, 0.02), (-0.15, 0.45, 0.018),
                       (0.55, 0.3, 0.015), (-0.2, -0.85, 0.018)):
        m.cilindro(M @ V((sx * R, sy * R, 0)), M @ V((sx * R, sy * R, z)), sr * R, br, 12)
    Mo = M @ Matrix.Translation(V((0, 0, z))) @ Matrix.Rotation(math.radians(-18), 4, "Z") @ Matrix.Diagonal((1.0, 0.36, 0.05, 1))
    m.torus((0, 0, 0), R * 0.86, R * 0.04, br, 64, 6, M=Mo)
    # vetor vermelho: asa em V com a forquilha no canto inferior esquerdo (2 pecas convexas)
    T, A, Ai, N, Bi, B = (1.34, 0.64), (-1.02, -0.12), (-0.93, -0.27), (-0.33, -0.16), (-0.74, -0.66), (-0.58, -0.76)
    for poli in ((T, A, Ai, N), (T, N, Bi, B)):
        m.prisma([(x * R, y * R) for x, y in poli], z * 2, z * 3, vm, M=M)
    m.texto("NASA", (0, -0.02 * R, z * 4), R * 0.48, br, rx=0, espessura=z, M=M, malha=True)


def gerar_textura_logo():
    """renderiza o meatball (geometria acima) para T_Logo_NASA.png com fundo transparente (1024 px)."""
    os.makedirs(TEX_DIR, exist_ok=True)
    sc = bpy.context.scene
    tmp = Malha("LOGO_tmp")
    meatball_geo(tmp, Matrix.Translation(V((0, 0, -500))), 1.0)
    ob = tmp.criar()
    cd = bpy.data.cameras.new("LOGO_cam"); cd.type = "ORTHO"; cd.ortho_scale = 2.8
    cam = bpy.data.objects.new("LOGO_cam", cd); sc.collection.objects.link(cam)
    cam.location = (0, 0, -490); cam.rotation_euler = (0, 0, 0)
    sc.camera = cam
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_x = sc.render.resolution_y = 1024
    sc.render.film_transparent = True
    sc.render.use_freestyle = False
    sc.view_settings.view_transform = "Standard"
    sc.render.image_settings.color_mode = "RGBA"
    sc.render.filepath = LOGO_PNG
    vis = [(o, o.hide_render) for o in bpy.data.objects if o not in (ob, cam)]
    for o, _ in vis:
        o.hide_render = True
    bpy.ops.render.render(write_still=True)
    for o, h in vis:
        o.hide_render = h
    bpy.data.objects.remove(ob, do_unlink=True); bpy.data.objects.remove(cam, do_unlink=True)
    sc.render.film_transparent = False
    print("[logo]", LOGO_PNG)


gerar_textura_logo()
LOGO = material("CT_Logo_NASA", "#FFFFFF", alpha=0.99, imagem=LOGO_PNG)   # decalque trocavel (textura com alfa)


def meatball(m, centro, R, normal, cima=(0, 0, 1), espessura=None):
    """aplica o logo como decalque (quad com a textura T_Logo_NASA, material CT_Logo_NASA)."""
    n = V(normal).normalized()
    m.painel_tex(V(centro) + n * 0.012, 2.8 * R, 2.8 * R, n, LOGO, cima=cima)


def corrimao(m, pts, h=1.1, mat=None, passo=1.6, r=0.035):
    mat = mat or AMARELO
    for a, b in zip(pts[:-1], pts[1:]):
        a, b = V((*a, 0)), V((*b, 0))
        n = max(1, int((b - a).length / passo))
        for i in range(n + (1 if b == V((*pts[-1], 0)) else 0)):
            p = a + (b - a) * (i / n)
            m.cilindro(p, p + V((0, 0, h)), r, mat, 8)
        m.cilindro(a + V((0, 0, h)), b + V((0, 0, h)), r, mat, 8)
        m.cilindro(a + V((0, 0, h * 0.5)), b + V((0, 0, h * 0.5)), r * 0.8, mat, 6)


def mesa(m, c, larg, prof, h=0.76, rz=0.0, tampo=None, pe=None):
    tampo, pe = tampo or BRANCO, pe or ESTRUT
    R = Matrix.Translation(V(c)) @ Matrix.Rotation(rz, 4, "Z")
    m.caixa(R @ V((0, 0, h - 0.025)), (larg, prof, 0.05), tampo, rz)
    for sx in (-1, 1):
        for sy in (-1, 1):
            p = R @ V((sx * (larg / 2 - 0.06), sy * (prof / 2 - 0.06), 0))
            m.caixa(p + V((0, 0, (h - 0.05) / 2)), (0.05, 0.05, h - 0.05), pe, rz)


def cadeira(m, c, rz=0.0, cor=None):
    cor = cor or TECIDO
    R = Matrix.Translation(V(c)) @ Matrix.Rotation(rz, 4, "Z")
    m.caixa(R @ V((0, 0, 0.47)), (0.48, 0.46, 0.08), cor, rz)
    m.caixa(R @ V((0, 0.22, 0.8)), (0.46, 0.06, 0.55), cor, rz, rx=-0.12)
    m.cilindro(R @ V((0, 0, 0.1)), R @ V((0, 0, 0.43)), 0.03, METAL, 8)
    for k in range(5):
        a = 2 * PI * k / 5 + rz
        p = V(c) + V((math.cos(a) * 0.28, math.sin(a) * 0.28, 0.05))
        m.cilindro(V(c) + V((0, 0, 0.1)), p, 0.022, ESCURO, 6)
        m.esfera(p - V((0, 0, 0.02)), 0.03, BORRACHA, 6, 4)


def monitor(m, c, rz=0.0, larg=0.55, alt=0.34, tela=None):
    tela = tela or TELA
    R = Matrix.Translation(V(c)) @ Matrix.Rotation(rz, 4, "Z")
    m.caixa(R @ V((0, 0.05, 0.01)), (0.22, 0.16, 0.02), ESCURO, rz)
    m.caixa(R @ V((0, 0.06, 0.15)), (0.05, 0.03, 0.26), ESCURO, rz)
    centro = R @ V((0, 0.03, 0.22 + alt / 2))
    m.caixa(centro, (larg, 0.035, alt), ESCURO, rz)
    n = (Matrix.Rotation(rz, 4, "Z") @ V((0, -1, 0)))
    m.painel_tex(centro + n * 0.019, larg - 0.04, alt - 0.04, n, tela)


def luminaria(m, c, r=0.45):
    m.cilindro(V(c) + V((0, 0, 0.0)), V(c) + V((0, 0, 0.35)), r, METAL, 16, r2=0.15)
    m.cilindro(V(c) + V((0, 0, -0.02)), V(c) + V((0, 0, 0.0)), r * 0.85, LUZ, 16)
    m.cilindro(V(c) + V((0, 0, 0.35)), V(c) + V((0, 0, 1.6)), 0.015, ESCURO, 4)


def arvore(m, c, h):
    x, y, z = c
    m.cilindro((x, y, z), (x, y, z + h * 0.45), h * 0.045, TRONCO, 7, r2=h * 0.03)
    for k in range(3):
        a = rnd.uniform(0, 2 * PI)
        d = h * 0.12 if k else 0
        rr = h * (0.26 if k == 0 else 0.19)
        import bmesh as _bm
        mtx = Matrix.Translation(V((x + math.cos(a) * d, y + math.sin(a) * d, z + h * (0.62 + 0.13 * k)))) @ Matrix.Diagonal((1, 1, 0.85, 1))
        res = _bm.ops.create_icosphere(m.bm, subdivisions=1, radius=rr * 1.08, matrix=mtx)
        for v in res["verts"]:
            v.co += V((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-1, 1))) * rr * 0.1
        m._marca(res["verts"], FOLHA if k < 2 else FOLHA2)


def poste(m, c, rz=0.0, h=6.0):
    x, y, z = c
    m.cilindro((x, y, z), (x, y, z + h), 0.08, ESTRUT, 8)
    d = Matrix.Rotation(rz, 4, "Z") @ V((0.9, 0, 0))
    topo = V((x, y, z + h))
    m.cilindro(topo, topo + d + V((0, 0, 0.2)), 0.05, ESTRUT, 6)
    m.caixa(topo + d * 1.25 + V((0, 0, 0.15)), (0.7, 0.3, 0.14), ESTRUT, rz)
    m.caixa(topo + d * 1.25 + V((0, 0, 0.075)), (0.6, 0.22, 0.02), LUZ, rz)


def banco(m, c, rz=0.0, comp=2.0):
    R = Matrix.Translation(V(c)) @ Matrix.Rotation(rz, 4, "Z")
    for k in range(3):
        m.caixa(R @ V((0, -0.18 + k * 0.18, 0.45)), (comp, 0.14, 0.05), MADEIRA, rz)
    m.caixa(R @ V((0, 0.24, 0.72)), (comp, 0.05, 0.28), MADEIRA, rz, rx=0.15)
    for sx in (-1, 1):
        m.caixa(R @ V((sx * (comp / 2 - 0.15), 0, 0.22)), (0.07, 0.5, 0.44), ESTRUT, rz)


FAIXAS = False

def faixa_estacao(m, centro, rz, numero, titulo, larg=None, h_cabo=None):
    """placa suspensa da estacao (frente e verso) com numero em circulo amarelo."""
    if not FAIXAS:
        return
    tam = 0.52
    larg = larg or (len(titulo) * tam * 0.62 + 2.6)
    R = Matrix.Rotation(rz, 4, "Z")
    c = V(centro)
    m.caixa(c, (larg, 0.12, 1.5), AZUL, rz)
    m.caixa(c + V((0, 0, -0.78)), (larg, 0.14, 0.08), AMARELO, rz)
    for lado in (1, -1):
        n = R @ V((0, -lado, 0))
        ex = R @ V((lado, 0, 0))
        pc = c - ex * (larg / 2 - 0.85) + n * 0.065
        m.cilindro(pc, pc + n * 0.02, 0.55, AMARELO, 24)
        m.texto(str(numero), pc + n * 0.035 + V((0, 0, -0.02)), 0.7, TXT_ESCURO, rz=rz + (0 if lado == 1 else PI), espessura=0.01)
        m.texto(titulo, c + ex * 0.6 + n * 0.07, tam, TXT_BRANCO, rz=rz + (0 if lado == 1 else PI), espessura=0.01)
    hc = h_cabo or (17.0 - c.z)
    for s in (-1, 1):
        p = c + R @ V((s * (larg / 2 - 0.4), 0, 0.75))
        m.cilindro(p, p + V((0, 0, hc)), 0.012, ESCURO, 4)


def pedra(m, c, r, mat=None, achatar=0.6):
    import bmesh as _bm
    mtx = Matrix.Translation(V(c)) @ Matrix.Rotation(rnd.uniform(0, PI), 4, "Z") @ Matrix.Diagonal((1.0, rnd.uniform(0.7, 1.15), achatar, 1))
    res = _bm.ops.create_icosphere(m.bm, subdivisions=1, radius=r, matrix=mtx)
    for v in res["verts"]:
        v.co += V((rnd.uniform(-1, 1), rnd.uniform(-1, 1), rnd.uniform(-0.5, 0.5))) * r * 0.22
    m._marca(res["verts"], mat or ROCHA)


def piso_zona(m, x0, y0, x1, y1, mat, borda=None):
    m.caixa_ab((x0, y0, 0.0), (x1, y1, 0.012), mat)
    if borda:
        w = 0.15
        for a, b in (((x0, y0), (x1, y0 + w)), ((x0, y1 - w), (x1, y1)), ((x0, y0), (x0 + w, y1)), ((x1 - w, y0), (x1, y1))):
            m.caixa_ab((a[0], a[1], 0.0), (b[0], b[1], 0.016), borda)


# ======================================================================= dimensoes
HX0, HX1, HY0, HY1, HZ = -60.0, 60.0, 0.0, 50.0, 18.0
ESP = 0.4
POOL = (5.0, 25.0, 27.0, 41.0)            # x0, y0, x1, y1 da piscina (NBL)
POOL_Z = -6.0
PORTA_ENT = (-2.6, 2.6, 3.2)              # entrada (sul)
PORTA_SAI = (40.0, 48.0, 6.0)             # saida para a plataforma (norte)
PAD_Y = 240.0                             # centro da plataforma de lancamento
ESTRADA_X = 44.0


# ======================================================================= 1. terreno, ruas e praca
def terreno():
    m = Malha("Terreno")
    m.chanfro = 0
    # grama em volta do hangar (sem cobrir o piso do hangar nem a piscina)
    g = [((-260, -160), (260, HY0 - 0.5)), ((-260, HY1 + 0.5), (260, 420)), ((-260, HY0 - 0.5), (HX0 - 0.5, HY1 + 0.5)),
         ((HX1 + 0.5, HY0 - 0.5), (260, HY1 + 0.5))]
    for a, b in g:
        m.caixa_ab((a[0], a[1], -0.6), (b[0], b[1], -0.15), GRAMA)
    # calcada em volta do hangar
    for a, b in (((HX0 - 3, HY0 - 3), (HX1 + 3, HY0 - 0.5)), ((HX0 - 3, HY1 + 0.5), (HX1 + 3, HY1 + 3)),
                 ((HX0 - 3, HY0 - 0.5), (HX0 - 0.5, HY1 + 0.5)), ((HX1 + 0.5, HY0 - 0.5), (HX1 + 3, HY1 + 0.5))):
        m.caixa_ab((a[0], a[1], -0.15), (b[0], b[1], -0.08), CALCADA)
    # praca de entrada (sul) com caminho central
    m.caixa_ab((-22, -58, -0.15), (22, -3, -0.08), CALCADA)
    # avenida leste-oeste + estacionamento oeste
    m.caixa_ab((-260, -72, -0.15), (260, -62, -0.12), ASFALTO)
    m.caixa_ab((-62, -60, -0.15), (-26, -8, -0.12), ASFALTO)
    for k in range(9):                                  # vagas
        x = -59 + k * 4
        m.caixa_ab((x, -40, -0.12), (x + 0.15, -30, -0.115), BRANCO)
        m.caixa_ab((x, -24, -0.12), (x + 0.15, -14, -0.115), BRANCO)
    m.caixa_ab((-4, -62, -0.15), (4, -58, -0.12), ASFALTO)
    # faixas da avenida
    for k in range(-26, 27):
        m.caixa_ab((k * 10, -67.1, -0.12), (k * 10 + 5, -66.9, -0.115), AMARELO)
    # estrada da saida norte ate a plataforma (estilo crawlerway: 2 pistas de cascalho + canteiro)
    for a, b in (((ESTRADA_X - 9, HY1 + 3), (ESTRADA_X - 2, PAD_Y - 40)), ((ESTRADA_X + 2, HY1 + 3), (ESTRADA_X + 9, PAD_Y - 40))):
        m.caixa_ab((a[0], a[1], -0.15), (b[0], b[1], -0.1), CALCADA)
    m.caixa_ab((ESTRADA_X - 1.2, HY1 + 3, -0.15), (ESTRADA_X + 1.2, PAD_Y - 40, -0.1), CALCADA)
    # faixas amarelas de pedestre da porta ate a estrada
    for k in range(6):
        m.caixa_ab((ESTRADA_X - 1, HY1 + 3.4 + k * 1.2, -0.1), (ESTRADA_X + 1, HY1 + 4.0 + k * 1.2, -0.095), AMARELO)
    o = fim(m, 2.0)
    col("COL_Chao", (0, 130, -0.45), (520, 580, 0.7))          # topo em -0,10 (entre a grama -0,15 e a calcada -0,08)


# ======================================================================= 2. hangar (estrutura)
def hangar():
    m = Malha("Hangar_Estrutura")
    vid = Malha("Hangar_Vidros")
    # piso (com buraco da piscina)
    px0, py0, px1, py1 = POOL
    for a, b in (((HX0, HY0), (HX1, py0)), ((HX0, py1), (HX1, HY1)), ((HX0, py0), (px0, py1)), ((px1, py0), (HX1, py1))):
        m.caixa_ab((a[0], a[1], -0.5), (b[0], b[1], 0.0), PISO)
    # paredes externas
    def parede(a, b, z0, z1, mat=PAREDE, esp=ESP):
        a, b = V(a), V(b)
        d = b - a
        c = (a + b) / 2
        ang = math.atan2(d.y, d.x)
        m.caixa((c.x, c.y, (z0 + z1) / 2), (d.length, esp, z1 - z0), mat, ang)

    JAN = (11.0, 15.0)                                       # faixa de janelas (alto)
    yS, yN = HY0 - ESP / 2, HY1 + ESP / 2
    # sul: entrada no meio
    e0, e1, eh = PORTA_ENT
    parede((HX0, yS), (-6.0, yS), 0, JAN[0]); parede((6.0, yS), (HX1, yS), 0, JAN[0])
    parede((-6.0, yS), (6.0, yS), 4.2, JAN[0])
    parede((-6.0, yS), (e0 - 0.1, yS), 0, 4.2, AZUL_ESC, 0.5)   # moldura da fachada de vidro
    parede((e1 + 0.1, yS), (6.0, yS), 0, 4.2, AZUL_ESC, 0.5)
    # norte: porta grande da saida
    s0, s1, sh = PORTA_SAI
    parede((HX0, yN), (s0, yN), 0, JAN[0]); parede((s1, yN), (HX1, yN), 0, JAN[0]); parede((s0, yN), (s1, yN), sh, JAN[0])
    # leste / oeste
    parede((HX0 - ESP / 2, HY0 - ESP), (HX0 - ESP / 2, HY1 + ESP), 0, HZ)
    parede((HX1 + ESP / 2, HY0 - ESP), (HX1 + ESP / 2, HY1 + ESP), 0, HZ)
    # faixa azul no topo (sul e norte) + janelas com montantes
    for y in (yS, yN):
        parede((HX0, y), (HX1, y), JAN[1], HZ, AZUL)
        x = HX0
        while x < HX1 - 0.1:
            m.caixa((x + 0.15, y, (JAN[0] + JAN[1]) / 2), (0.3, ESP, JAN[1] - JAN[0]), AZUL_ESC)
            x += 4.0
        m.caixa((HX1 - 0.15, y, (JAN[0] + JAN[1]) / 2), (0.3, ESP, JAN[1] - JAN[0]), AZUL_ESC)
        vid.caixa((0, y, (JAN[0] + JAN[1]) / 2), (HX1 - HX0, 0.06, JAN[1] - JAN[0]), VIDRO)
    for x in (HX0 - ESP / 2, HX1 + ESP / 2):                # faixa azul nas laterais
        m.caixa((x, (HY0 + HY1) / 2, (JAN[1] + HZ) / 2), (ESP + 0.04, HY1 - HY0 + 2 * ESP + 0.04, HZ - JAN[1]), AZUL)
    # vidros da entrada (sul)
    vid.caixa(((-6 + e0) / 2, yS, 2.1), (abs(-6 - e0) - 0.4, 0.05, 4.0), VIDRO)
    vid.caixa(((6 + e1) / 2, yS, 2.1), (abs(6 - e1) - 0.4, 0.05, 4.0), VIDRO)
    vid.caixa((0, yS, 3.7), (e1 - e0, 0.05, 1.0), VIDRO)
    # telhado
    m.caixa_ab((HX0 - 1.0, HY0 - 1.0, HZ), (HX1 + 1.0, HY1 + 1.0, HZ + 0.4), ESTRUT)
    m.caixa_ab((HX0 - 1.0, HY0 - 1.0, HZ + 0.4), (HX1 + 1.0, HY1 + 1.0, HZ + 0.55), PAREDE_INT)
    # revestimento interno: rodape azul + parede clara
    for (a, b) in (((HX0, HY0 + 0.02), (HX1, HY0 + 0.07)), ((HX0, HY1 - 0.07), (HX1, HY1 - 0.02)),
                   ((HX0 + 0.02, HY0), (HX0 + 0.07, HY1)), ((HX1 - 0.07, HY0), (HX1 - 0.02, HY1))):
        m.caixa_ab((a[0], a[1], 0.0), (b[0], b[1], 1.2), AZUL)
    # pilares (perfil I simplificado) e treliças do telhado
    for x in [HX0 + 0.6 + 10 * k for k in range(13)]:
        x = min(x, HX1 - 0.6)
        for y in (HY0 + 0.6, HY1 - 0.6):
            m.caixa((x, y, 8.5), (0.5, 0.18, 17.0), ESTRUT)
            m.caixa((x, y, 8.5), (0.12, 0.6, 17.0), ESTRUT)
            m.caixa((x, y, 0.15), (0.8, 0.8, 0.3), AMARELO)
        # trelica (banzos + montantes + diagonais)
        zb, zt = 15.6, 17.6
        m.caixa((x, 25, zb), (0.3, HY1 - HY0, 0.3), ESTRUT)
        m.caixa((x, 25, zt), (0.3, HY1 - HY0, 0.3), ESTRUT)
        for k in range(11):
            y = HY0 + k * 5
            m.caixa((x, y, (zb + zt) / 2), (0.18, 0.18, zt - zb), ESTRUT)
            if k < 10:
                ya, yb = (y, y + 5) if k % 2 == 0 else (y + 5, y)
                d = math.hypot(5, zt - zb)
                ang = math.atan2(zt - zb, yb - ya)
                m.caixa((x, (ya + yb) / 2, (zb + zt) / 2), (0.14, d, 0.14), ESTRUT, rx=ang)
    for y in [HY0 + 2.5 + 5 * k for k in range(10)]:         # tercas
        m.caixa((0, y, 17.85), (HX1 - HX0, 0.2, 0.3), ESTRUT)
    # ponte rolante (decorativa) e trilhos
    for y in (HY0 + 1.6, HY1 - 1.6):
        m.caixa((0, y, 13.6), (HX1 - HX0, 0.4, 0.5), AMARELO)
    xp = -8.0
    m.caixa((xp, 25, 14.2), (1.2, HY1 - HY0 - 2.4, 0.9), AMARELO)
    m.caixa((xp, 18, 13.5), (1.6, 1.8, 0.8), ESCURO)
    m.cilindro((xp, 18, 13.1), (xp, 18, 9.0), 0.02, ESCURO, 4)
    m.caixa((xp, 18, 8.8), (0.4, 0.3, 0.4), AMARELO)
    m.texto("20 t", (xp, 25 - (HY1 - HY0 - 2.4) / 2 + 0.2, 14.2), 0.5, TXT_ESCURO, rz=PI / 2, espessura=0.01)
    # luminarias de galpao
    for x in [HX0 + 5 + 10 * k for k in range(12)]:
        for y in (10.0, 25.0, 40.0):
            luminaria(m, (x, y, 14.8))
    # fachada: logo NASA grande + nome
    meatball(m, (-34, yS - ESP / 2 - 0.02, 6.0), 3.8, (0, -1, 0))
    m.texto("CENTRO DE TREINAMENTO", (22, yS - ESP / 2 - 0.08, 8.4), 2.0, AZUL_ESC, espessura=0.12)
    m.texto("DE ASTRONAUTAS", (22, yS - ESP / 2 - 0.08, 5.9), 2.0, AZUL_ESC, espessura=0.12)
    m.caixa((22, yS - ESP / 2 - 0.05, 4.4), (30, 0.1, 0.25), VERMELHO)
    # marquise da entrada
    m.caixa_ab((-8, -7.5, 4.4), (8, HY0 - ESP, 4.8), BRANCO)
    m.caixa_ab((-8, -7.5, 4.8), (8, -7.2, 5.3), AZUL)
    for x in (-7.4, 7.4):
        m.cilindro((x, -6.9, -0.1), (x, -6.9, 4.4), 0.22, BRANCO, 16)
    m.texto("NASA", (0, -7.6, 5.05), 0.42, TXT_BRANCO, espessura=0.02)
    # placa da porta norte
    m.caixa(((s0 + s1) / 2, HY1 - 0.1, sh + 1.0), (s1 - s0, 0.15, 1.4), VERMELHO)
    m.texto("PLATAFORMA DE LANÇAMENTO", ((s0 + s1) / 2, HY1 - 0.2, sh + 1.0), 0.48, TXT_BRANCO, rz=0, espessura=0.01)
    m.caixa(((s0 + s1) / 2, HY1 + ESP + 0.05, sh + 1.0), (s1 - s0, 0.15, 1.4), AZUL)
    m.texto("HANGAR DE TREINAMENTO", ((s0 + s1) / 2, HY1 + ESP + 0.15, sh + 1.0), 0.5, TXT_BRANCO, rz=PI, espessura=0.01)
    meatball(m, (20.0, HY1 + ESP / 2 + 0.02, 7.0), 3.4, (0, 1, 0))
    m.texto("NASA", (-10.0, HY1 + ESP / 2 + 0.1, 8.0), 3.0, AZUL_ESC, rz=PI, espessura=0.12)
    # faixas zebradas nas portas
    for k in range(10):
        x = s0 + k * 0.8
        m.caixa((x + 0.2, HY1 - 0.35, 0.006), (0.4, 0.5, 0.012), AMARELO if k % 2 == 0 else ESCURO)
    cores = [VERMELHO, AMARELO, AZUL, LARANJA, VERDE_BR, TECIDO]
    for k in range(12):
        x = HX0 + 5 + 10 * k
        for y, n in ((HY0 + 0.1, 1), (HY1 - 0.1, -1)):
            if (n < 0 and 30 < x < 56) or (n > 0 and -12 < x < 12):
                continue
            c = cores[(k + (0 if n > 0 else 3)) % len(cores)]
            m.cilindro((x, y, 8.0), (x, y + 0.06 * n, 8.0), 1.2, c, 24)
            m.cilindro((x, y + 0.06 * n, 8.0), (x, y + 0.08 * n, 8.0), 0.95, BRANCO, 24)
            m.cilindro((x, y + 0.08 * n, 8.0), (x, y + 0.1 * n, 8.0), 0.7, AZUL_ESC, 24)
            m.texto(f"STS-{30 + k * 7 + (0 if n > 0 else 3)}", (x, y + 0.12 * n, 8.0), 0.22, TXT_BRANCO,
                    rz=PI if n > 0 else 0, espessura=0.005)
    for x in (-58.0, -56.6, 58.0):                              # caixas de transporte (flight cases)
        for k in range(3):
            y = 24.0 + k * 1.3 if x < 0 else 26.0 + k * 1.3
            m.caixa((x, y, 0.4), (1.2, 1.1, 0.8), ESCURO)
            m.caixa((x, y, 0.4), (1.24, 1.14, 0.08), METAL)
            m.caixa((x, y, 1.05), (1.0, 0.9, 0.5), AZUL_ESC if k % 2 else ESCURO)
    for x, y in ((-40, 12), (-10, 38), (20, 8), (45, 40)):     # ar-condicionado no telhado
        m.caixa((x, y, HZ + 1.1), (4.0, 2.5, 1.1), METAL)
        m.cilindro((x - 1, y, HZ + 1.65), (x - 1, y, HZ + 1.75), 0.8, ESCURO, 16)
        m.cilindro((x + 1, y, HZ + 1.65), (x + 1, y, HZ + 1.75), 0.8, ESCURO, 16)
    fim(m, 1.0)
    fim(vid, 1.0)
    col("COL_Caixas_O", (-57.3, 25.3, 0.65), (2.6, 3.8, 1.3))
    col("COL_Caixas_L", (58.0, 27.3, 0.65), (1.3, 3.8, 1.3))
    # colisores das paredes (caixas simples)
    col("COL_Parede_Sul_O", ((HX0 - 6) / 2, yS, HZ / 2), (-6 - HX0, ESP, HZ))
    col("COL_Parede_Sul_L", ((HX1 + 6) / 2, yS, HZ / 2), (HX1 - 6, ESP, HZ))
    col("COL_Parede_Sul_Vidro_O", ((-6 + e0) / 2, yS, 2.1), (abs(-6 - e0), ESP, 4.2))
    col("COL_Parede_Sul_Vidro_L", ((6 + e1) / 2, yS, 2.1), (abs(6 - e1), ESP, 4.2))
    col("COL_Parede_Sul_Topo", (0, yS, (eh + HZ) / 2), (12, ESP, HZ - eh))
    col("COL_Parede_Norte_O", ((HX0 + s0) / 2, yN, HZ / 2), (s0 - HX0, ESP, HZ))
    col("COL_Parede_Norte_L", ((HX1 + s1) / 2, yN, HZ / 2), (HX1 - s1, ESP, HZ))
    col("COL_Parede_Norte_Topo", ((s0 + s1) / 2, yN, (sh + HZ) / 2), (s1 - s0, ESP, HZ - sh))
    col("COL_Parede_Oeste", (HX0 - ESP / 2, 25, HZ / 2), (ESP, HY1 - HY0 + 1, HZ))
    col("COL_Parede_Leste", (HX1 + ESP / 2, 25, HZ / 2), (ESP, HY1 - HY0 + 1, HZ))
    col("COL_Piso_Hangar_A", (0, (HY0 + POOL[1]) / 2, -0.25), (HX1 - HX0, POOL[1] - HY0, 0.5))
    col("COL_Piso_Hangar_B", (0, (POOL[3] + HY1) / 2, -0.25), (HX1 - HX0, HY1 - POOL[3], 0.5))
    col("COL_Piso_Hangar_C", ((HX0 + POOL[0]) / 2, (POOL[1] + POOL[3]) / 2, -0.25), (POOL[0] - HX0, POOL[3] - POOL[1], 0.5))
    col("COL_Piso_Hangar_D", ((HX1 + POOL[2]) / 2, (POOL[1] + POOL[3]) / 2, -0.25), (HX1 - POOL[2], POOL[3] - POOL[1], 0.5))
    col("COL_Marquise_Pilar_O", (-7.4, -6.9, 2.2), (0.45, 0.45, 4.6))
    col("COL_Marquise_Pilar_L", (7.4, -6.9, 2.2), (0.45, 0.45, 4.6))

    # portas animadas
    for lado, nome in ((-1, "ANIM_Porta_Entrada_O"), (1, "ANIM_Porta_Entrada_L")):
        x0 = 0.0 if lado > 0 else e0
        x1 = e1 if lado > 0 else 0.0
        cx = (x0 + x1) / 2
        p = Malha(nome, origem=(cx, yS, 0))
        p.caixa((cx, yS, eh / 2), (x1 - x0 - 0.04, 0.04, eh - 0.05), VIDRO)
        for z in (0.04, eh - 0.04):
            p.caixa((cx, yS, z), (x1 - x0 - 0.04, 0.09, 0.08), AZUL_ESC)
        for x in (x0 + 0.05, x1 - 0.05):
            p.caixa((x, yS, eh / 2), (0.08, 0.09, eh), AZUL_ESC)
        p.caixa((cx - lado * (x1 - x0) / 2 + lado * 0.25, yS - 0.07, 1.05), (0.04, 0.04, 0.8), METAL)
        fim(p)
    pt = Malha("ANIM_Porta_Saida", origem=((s0 + s1) / 2, yN, 0))
    for k in range(int(sh / 0.3)):                                # porta de enrolar (ripas)
        pt.caixa(((s0 + s1) / 2, yN, 0.15 + k * 0.3), (s1 - s0, 0.12 if k % 2 else 0.16, 0.28), METAL)
    pt.caixa(((s0 + s1) / 2, yN - 0.12, 0.08), (s1 - s0, 0.1, 0.16), AMARELO)
    for x in (s0 + 0.2, s1 - 0.2):
        m2 = None
    fim(pt)
    for x in (s0 - 0.25, s1 + 0.25):                          # trilhos da porta
        mm = Malha(f"Porta_Saida_Trilho_{'O' if x < s0 else 'L'}")
        mm.caixa((x, yN - 0.15, sh / 2), (0.3, 0.3, sh), AMARELO)
        fim(mm)


# ======================================================================= 3. recepcao (estacao 0)
def recepcao():
    m = Malha("Est0_Recepcao")
    piso_zona(m, -12, 0.2, 12, 14, TAPETE, AMARELO)
    meatball(m, (0, 5.0, 0.02), 2.6, (0, 0, 1), cima=(0, 1, 0), espessura=0.004)
    # balcao curvo
    for k in range(9):
        a0 = math.radians(200 + k * 15.5); a1 = math.radians(200 + (k + 1) * 15.5)
        R1, R2 = 3.6, 4.3
        pts = [(R1 * math.cos(a0), R1 * math.sin(a0)), (R2 * math.cos(a0), R2 * math.sin(a0)),
               (R2 * math.cos(a1), R2 * math.sin(a1)), (R1 * math.cos(a1), R1 * math.sin(a1))]
        M = Matrix.Translation(V((0, 13.2, 0)))
        m.prisma(pts, 0, 1.0, MADEIRA, M=M)
        m.prisma([(x * 1.0, y * 1.0) for x, y in pts], 1.0, 1.08, BRANCO, M=M)
        m.prisma(pts, 0.0, 0.12, AZUL_ESC, M=M)
    for x in (-1.6, 0.0, 1.6):
        monitor(m, (x, 10.0, 0.76), rz=PI)
    for x in (-1.6, 1.6):
        cadeira(m, (x, 10.8, 0), rz=PI)
    m.caixa((0, 10.1, 0.5), (4.0, 0.6, 0.05), BRANCO)
    # parede-painel atras do balcao
    m.caixa((0, 14.0, 3.0), (18, 0.4, 6.0), AZUL_ESC)
    meatball(m, (-5.2, 13.78, 3.6), 1.7, (0, -1, 0))
    m.texto("PROGRAMA DE TREINAMENTO", (2.6, 13.74, 4.4), 0.62, TXT_BRANCO, espessura=0.02)
    m.texto("DE ASTRONAUTAS", (2.6, 13.74, 3.55), 0.62, TXT_BRANCO, espessura=0.02)
    m.caixa((2.6, 13.77, 2.85), (8.4, 0.04, 0.08), VERMELHO)
    m.texto("Houston, Texas", (2.6, 13.74, 2.3), 0.38, AMARELO, espessura=0.02)
    # telas com a trilha do treino (6 icones)
    for k in range(6):
        x = -8.4 + k * 0.0
    # vasos e bancos
    for x in (-10.5, 10.5):
        m.cilindro((x, 2.0, 0), (x, 2.0, 0.6), 0.45, BRANCO, 16)
        m.esfera((x, 2.0, 1.2), 0.75, FOLHA, 8, 6)
        m.esfera((x + 0.2, 2.2, 1.7), 0.45, FOLHA2, 8, 6)
        banco(m, (x, 7.0, 0), rz=PI / 2 if x < 0 else -PI / 2)
    # vitrine com capsula Mercury (modelo em escala)
    m.cilindro((9.0, 11.5, 0), (9.0, 11.5, 0.9), 0.8, AZUL_ESC, 20)
    m.torno([(0.55, 0.9), (0.6, 0.95), (0.32, 1.75), (0.22, 1.95), (0.0, 2.0)], ESCURO, 20,
            M=Matrix.Translation(V((9.0, 11.5, 0))))
    m.texto("MERCURY 1961", (9.0, 10.68, 0.5), 0.14, TXT_BRANCO, espessura=0.01)
    fim(m, 1.0)
    col("COL_Balcao", (0, 9.6, 0.55), (8.4, 1.6, 1.1))
    col("COL_Painel_Recepcao", (0, 14.0, 3.0), (18, 0.4, 6.0))
    col("COL_Vitrine_Mercury", (9.0, 11.5, 1.0), (1.6, 1.6, 2.0))
    for x in (-10.5, 10.5):
        col(f"COL_Vaso_{int(x)}", (x, 2.0, 0.6), (0.9, 0.9, 1.2))
    faixa = Malha("Est0_Placa")
    faixa_estacao(faixa, (0, 1.6, 6.0), 0, 0, "RECEPÇÃO", larg=7.5)
    fim(faixa)


# ======================================================================= 4. estacao 1 — medico / preparo fisico
def estacao_medica():
    m = Malha("Est1_Medico")
    X0, Y0, X1, Y1 = -50, 2, -21, 21
    piso_zona(m, X0, Y0, X1, Y1, TAPETE, AMARELO)
    # esteiras
    for k, x in enumerate((-46.5, -43.0)):
        y = 6.0
        m.caixa((x, y, 0.15), (0.9, 2.2, 0.3), ESCURO)
        m.caixa((x, y, 0.31), (0.6, 2.0, 0.03), BORRACHA)
        for s in (-1, 1):
            m.cilindro((x + s * 0.4, y + 0.9, 0.3), (x + s * 0.4, y + 0.9, 1.25), 0.04, METAL, 8)
            m.cilindro((x + s * 0.4, y + 0.9, 1.0), (x + s * 0.4, y + 0.1, 1.0), 0.03, METAL, 8)
        m.caixa((x, y + 0.95, 1.35), (0.85, 0.12, 0.35), ESCURO, rx=-0.4)
        m.painel_tex((x, y + 0.87, 1.37), 0.6, 0.22, (0, -0.4, 0.92), TELA)
    # bicicleta ergometrica
    x, y = -39.0, 6.0
    m.caixa((x, y, 0.06), (0.5, 1.3, 0.12), ESCURO)
    m.cilindro((x - 0.06, y + 0.45, 0.42), (x + 0.06, y + 0.45, 0.42), 0.28, ESCURO, 20)
    m.cilindro((x, y - 0.3, 0.1), (x, y - 0.15, 0.8), 0.05, METAL, 8)
    m.caixa((x, y - 0.12, 0.85), (0.28, 0.4, 0.08), BORRACHA)
    m.cilindro((x, y + 0.45, 0.1), (x, y + 0.35, 1.1), 0.05, METAL, 8)
    m.cilindro((x - 0.3, y + 0.32, 1.1), (x + 0.3, y + 0.32, 1.1), 0.03, BORRACHA, 8)
    m.painel_tex((x, y + 0.42, 1.2), 0.22, 0.14, (0, -0.5, 0.86), TELA_ECG)
    # maca de exame + monitor de sinais vitais + suporte de soro
    x, y = -26.0, 17.0
    for sx in (-1, 1):
        for sy in (-1, 1):
            m.cilindro((x + sx * 0.38, y + sy * 0.9, 0), (x + sx * 0.38, y + sy * 0.9, 0.7), 0.035, METAL, 8)
    m.caixa((x, y, 0.72), (0.85, 2.0, 0.08), METAL)
    m.caixa((x, y, 0.82), (0.8, 1.95, 0.14), BRANCO)
    m.caixa((x, y + 0.75, 0.95), (0.6, 0.35, 0.12), BRANCO, rx=0.15)
    m.caixa((x + 1.2, y + 0.6, 0.5), (0.6, 0.45, 1.0), BRANCO)               # carrinho
    m.caixa((x + 1.2, y + 0.6, 1.3), (0.6, 0.2, 0.42), ESCURO)
    m.painel_tex((x + 1.2, y + 0.49, 1.3), 0.52, 0.34, (0, -1, 0), TELA_ECG)
    m.cilindro((x - 0.7, y + 0.8, 0), (x - 0.7, y + 0.8, 1.9), 0.02, METAL, 6)
    m.cilindro((x - 0.95, y + 0.8, 1.9), (x - 0.45, y + 0.8, 1.9), 0.015, METAL, 6)
    m.caixa((x - 0.9, y + 0.8, 1.75), (0.12, 0.05, 0.22), VIDRO)
    # mesa do medico
    mesa(m, (-31, 18.5, 0), 1.8, 0.8)
    monitor(m, (-31, 18.6, 0.76))
    cadeira(m, (-31, 19.5, 0), rz=PI)
    # estante de halteres + banco
    x, y = -47.5, 15.0
    for s in (-1, 1):
        m.caixa((x + s * 1.1, y, 0.5), (0.08, 0.5, 1.0), ESTRUT)
    for k, z in enumerate((0.25, 0.6, 0.95)):
        m.caixa((x, y, z), (2.3, 0.5, 0.04), ESTRUT)
        for j in range(5):
            xx = x - 0.9 + j * 0.45
            r = 0.06 + 0.012 * j
            for s in (-0.13, 0.13):
                m.cilindro((xx, y + s - 0.03, z + 0.02 + r), (xx, y + s + 0.03, z + 0.02 + r), r, BORRACHA, 8)
            m.cilindro((xx, y - 0.13, z + 0.02 + r), (xx, y + 0.13, z + 0.02 + r), 0.015, METAL, 6)
    m.caixa((-43.5, 15.0, 0.42), (0.35, 1.3, 0.12), TECIDO)
    for s in (-0.5, 0.5):
        m.caixa((-43.5, 15.0 + s, 0.2), (0.3, 0.06, 0.4), ESTRUT)
    # balanca + regua de altura
    m.caixa((-35.5, 18.8, 0.04), (0.5, 0.5, 0.08), BRANCO)
    m.cilindro((-35.5, 19.05, 0), (-35.5, 19.05, 2.1), 0.03, METAL, 6)
    m.caixa((-35.5, 19.0, 1.4), (0.12, 0.04, 0.6), BRANCO)
    # divisorias baixas com painel informativo
    for (a, b) in (((X0, Y1), (X1 - 6, Y1)),):
        m.caixa_ab((a[0], a[1] - 0.1, 0), (b[0], b[1] + 0.1, 1.8), PAREDE_INT)
        m.caixa_ab((a[0], a[1] - 0.12, 1.8), (b[0], b[1] + 0.12, 1.9), AZUL)
    # armario de medicamentos (portas de vidro) na parede oeste
    for k in range(4):
        y = 4.0 + k * 1.1
        m.caixa((-49.55, y, 1.0), (0.6, 1.05, 2.0), BRANCO)
        m.caixa((-49.24, y, 1.35), (0.03, 0.9, 0.9), VIDRO)
        for z in (1.05, 1.4, 1.75):
            m.caixa((-49.5, y, z), (0.45, 0.95, 0.03), METAL)
        m.caixa((-49.22, y + 0.38, 0.75), (0.03, 0.05, 0.2), METAL)
    m.caixa((-49.6, 6.1, 2.2), (0.4, 0.5, 0.35), VERDE_BR)
    m.texto("+", (-49.38, 6.1, 2.2), 0.4, TXT_BRANCO, rz=PI / 2, espessura=0.01)
    # quadro de acuidade visual
    m.caixa((-49.65, 12.0, 1.7), (0.05, 0.9, 1.3), BRANCO)
    for k, (t, tam) in enumerate((("E", 0.28), ("F P", 0.18), ("T O Z", 0.13), ("L P E D", 0.1), ("P E C F D", 0.08))):
        m.texto(t, (-49.6, 12.0, 2.15 - k * 0.22), tam, TXT_ESCURO, rz=PI / 2, espessura=0.005)
    # segundo leito com divisoria de cortina
    x, y = -26.0, 11.5
    m.caixa((x, y, 0.45), (0.8, 1.95, 0.25), BRANCO)
    for sx in (-1, 1):
        for sy in (-1, 1):
            m.cilindro((x + sx * 0.36, y + sy * 0.9, 0), (x + sx * 0.36, y + sy * 0.9, 0.35), 0.03, METAL, 6)
    m.cilindro((x - 1.0, y - 1.4, 2.2), (x - 1.0, y + 1.4, 2.2), 0.02, METAL, 6)
    for k in range(9):
        yy = y - 1.3 + k * 0.32
        m.caixa((x - 1.0, yy, 1.25), (0.05, 0.3, 1.9), TAPETE, rz=0.35 if k % 2 else -0.35)
    m.cilindro((x - 1.0, y - 1.4, 0), (x - 1.0, y - 1.4, 2.2), 0.025, METAL, 6)
    m.cilindro((x - 1.0, y + 1.4, 0), (x - 1.0, y + 1.4, 2.2), 0.025, METAL, 6)
    # painel com coracao + ECG
    m.caixa((-38, Y1 - 0.12, 1.1), (3.0, 0.05, 1.2), BRANCO)
    m.painel_tex((-38, Y1 - 0.15, 1.1), 2.8, 1.0, (0, -1, 0), TELA_ECG)
    fim(m, 1.0)
    for i, x in enumerate((-46.5, -43.0)):
        col(f"COL_Esteira_{i}", (x, 6.0, 0.7), (0.95, 2.3, 1.4))
    col("COL_Bicicleta", (-39, 6, 0.6), (0.6, 1.4, 1.2))
    col("COL_Maca", (-25.6, 17.2, 0.5), (2.2, 2.2, 1.0))
    col("COL_Mesa_Medico", (-31, 18.5, 0.4), (1.9, 0.9, 0.8))
    col("COL_Halteres", (-47.5, 15.0, 0.5), (2.4, 0.6, 1.0))
    col("COL_Armario_Remedios", (-49.55, 5.65, 1.0), (0.6, 4.4, 2.0))
    col("COL_Leito_2", (-26.0, 11.5, 0.4), (0.9, 2.0, 0.8))
    col("COL_Cortina", (-27.0, 11.5, 1.1), (0.15, 2.9, 2.2))
    col("COL_Divisoria_Medico", ((X0 + X1 - 6) / 2, Y1, 0.95), (X1 - 6 - X0, 0.25, 1.9))
    faixa = Malha("Est1_Placa")
    faixa_estacao(faixa, (-34, 11.0, 6.5), 0, 1, "AVALIAÇÃO MÉDICA E PREPARO FÍSICO")
    fim(faixa)


# ======================================================================= 5. estacao 2 — centrifuga humana
CENT = (-42.0, 36.0)
def estacao_centrifuga():
    cx, cy = CENT
    m = Malha("Est2_Centrifuga")
    m.cilindro((cx, cy, 0), (cx, cy, 0.015), 10.5, ESCURO, 48)
    m.torus((cx, cy, 0.018), 10.2, 0.12, AMARELO, 48, 4, M=Matrix.Diagonal((1, 1, 0.08, 1)))
    # base do eixo
    m.cilindro((cx, cy, 0), (cx, cy, 0.6), 2.0, ESTRUT, 24)
    m.cilindro((cx, cy, 0.6), (cx, cy, 2.6), 1.1, METAL, 20)
    m.cilindro((cx, cy, 2.6), (cx, cy, 2.75), 1.4, AZUL, 20)
    for k in range(8):
        a = 2 * PI * k / 8
        m.caixa((cx + math.cos(a) * 1.5, cy + math.sin(a) * 1.5, 0.75), (0.5, 0.15, 0.3), AMARELO, a)
    # corrimao em volta (com abertura para o lado da cabine de controle)
    pts = []
    for k in range(34):
        a = math.radians(-20 + k * 10)
        if -20 < math.degrees(a) % 360 < 30 or 330 < math.degrees(a) % 360:
            continue
        pts.append((cx + 11.2 * math.cos(a), cy + 11.2 * math.sin(a)))
    corrimao(m, pts, 1.1, AMARELO, 1.8)
    # luzes de alerta
    for a in (40, 160, 280):
        p = V((cx + 11.2 * math.cos(math.radians(a)), cy + 11.2 * math.sin(math.radians(a)), 0))
        m.cilindro(p, p + V((0, 0, 2.4)), 0.06, ESTRUT, 8)
        m.cilindro(p + V((0, 0, 2.4)), p + V((0, 0, 2.7)), 0.14, LUZ_AMBAR, 12)
    # cabine de controle (vidro voltado para a centrifuga)
    bx, by = -28.5, 39.5
    m.caixa_ab((bx - 3, by - 2.5, 0), (bx + 3, by + 2.5, 0.25), ESTRUT)
    m.caixa_ab((bx - 3, by + 2.3, 0.25), (bx + 3, by + 2.5, 3.2), PAREDE_INT)
    m.caixa_ab((bx + 2.8, by - 2.5, 0.25), (bx + 3, by + 2.5, 3.2), PAREDE_INT)
    m.caixa_ab((bx - 3, by - 2.5, 0.25), (bx + 0.5, by - 2.3, 3.2), PAREDE_INT)
    m.caixa_ab((bx - 3, by - 2.5, 0.25), (bx - 2.8, by + 2.5, 1.0), PAREDE_INT)    # parapeito da janela (oeste)
    m.caixa_ab((bx - 3.1, by - 2.6, 3.2), (bx + 3.1, by + 2.6, 3.45), AZUL)
    for y in (by - 2.4, by, by + 2.4):
        m.caixa((bx - 2.9, y, 2.1), (0.15, 0.15, 2.2), AZUL_ESC)
    for k in range(3):                                         # consoles inclinados
        y = by - 1.5 + k * 1.5
        m.caixa((bx - 2.2, y, 0.65), (0.8, 1.3, 0.8), ESCURO)
        m.caixa((bx - 2.35, y, 1.15), (0.6, 1.3, 0.1), ESCURO, ry=0.5)
        m.painel_tex((bx - 2.32, y, 1.2), 1.1, 0.45, (0.48, 0, 0.88), TELA, cima=(-1, 0, 0))
        for j in range(5):
            m.cilindro((bx - 2.0, y - 0.5 + j * 0.25, 1.06), (bx - 2.0, y - 0.5 + j * 0.25, 1.1), 0.04,
                       (LUZ_VERDE, LUZ_AMBAR, LUZ_VERM, LUZ_VERDE, BRANCO)[j], 8)
        cadeira(m, (bx - 1.2, y, 0.25), rz=-PI / 2)
    m.texto("CONTROLE", (bx - 3.05, by, 2.95), 0.32, TXT_BRANCO, rz=-PI / 2, espessura=0.01)
    fim(m, 1.0)
    vid = Malha("Est2_Centrifuga_Vidros")
    vid.caixa((bx - 2.9, by, 2.1), (0.04, 4.8, 2.2), VIDRO)
    fim(vid)

    # braco giratorio (ANIM): origem no eixo
    b = Malha("ANIM_Centrifuga_Braco", origem=(cx, cy, 2.75))
    z = 3.1
    b.cilindro((cx, cy, 2.75), (cx, cy, 3.6), 0.9, AZUL, 20)
    b.cilindro((cx, cy, 3.6), (cx, cy, 3.9), 0.5, METAL, 16)
    for s in (-0.45, 0.45):                                     # treliça do braço (2 vigas + diagonais)
        b.caixa((cx + 3.9, cy + s, z), (7.8, 0.2, 0.45), AZUL)
        b.caixa((cx - 2.0, cy + s, z), (4.0, 0.2, 0.45), AZUL)
    for k in range(7):
        x = cx + 1.2 + k * 1.0
        b.caixa((x, cy, z), (0.12, 1.0, 0.12), METAL)
    b.caixa((cx - 4.2, cy, z - 0.2), (1.4, 1.6, 1.4), ESTRUT)              # contrapeso
    b.texto("G", (cx - 4.2, cy - 0.81, z - 0.2), 0.8, AMARELO, espessura=0.02)
    # gondola (capsula) pendurada no fim do braço
    gx = cx + 7.9
    b.caixa((gx, cy, z + 0.1), (0.5, 2.2, 0.5), METAL)
    for s in (-1, 1):
        b.cilindro((gx, cy + s * 1.0, z), (gx, cy + s * 1.0, z - 0.7), 0.08, METAL, 10)
    Mg = Matrix.Translation(V((gx, cy, z - 1.7))) @ Matrix.Rotation(PI / 2, 4, "Y")
    b.torno([(0.0, -1.4), (0.75, -1.25), (1.05, -0.7), (1.1, 0.0), (1.05, 0.7), (0.75, 1.25), (0.0, 1.4)], BRANCO, 20, M=Mg)
    b.torus((0, 0, 0), 1.1, 0.06, AZUL, 20, 6, M=Mg)
    b.torus((0, 0, 0.6), 1.07, 0.04, VERMELHO, 20, 6, M=Mg)
    # janela e escotilha na frente (direcao +Y local da gondola)
    b.esfera((gx, cy - 0.98, z - 1.55), 0.42, VIDRO, 12, 8, escala=(1.2, 0.35, 0.8))
    b.texto("NASA", (gx + 0.0, cy + 1.0, z - 1.75), 0.3, AZUL_ESC, rz=PI, espessura=0.02)
    for s in (-1, 1):
        b.cilindro((gx, cy + s * 1.0, z - 0.7), (gx, cy + s * 0.9, z - 1.2), 0.06, METAL, 8)
    fim(b, 1.0)
    col("COL_Centrifuga_Eixo", (cx, cy, 1.5), (2.4, 2.4, 3.0))
    # anel de colisao (corrimao) em segmentos
    for k in range(34):
        a = math.radians(-20 + k * 10 + 5)
        if -20 < math.degrees(a) % 360 < 30 or 330 < math.degrees(a) % 360:
            continue
        col(f"COL_Centrifuga_Grade_{k}", (cx + 11.2 * math.cos(a), cy + 11.2 * math.sin(a), 0.6), (0.2, 2.0, 1.2), rz=a)
    col("COL_Cabine_Controle_N", (bx, by + 2.4, 1.6), (6, 0.25, 3.2))
    col("COL_Cabine_Controle_L", (bx + 2.9, by, 1.6), (0.25, 5, 3.2))
    col("COL_Cabine_Controle_S", (bx - 1.25, by - 2.4, 1.6), (3.5, 0.25, 3.2))
    col("COL_Cabine_Controle_O", (bx - 2.9, by, 1.6), (0.25, 5, 3.2))
    faixa = Malha("Est2_Placa")
    faixa_estacao(faixa, (cx + 4, 24.5, 7.5), 0, 2, "CENTRÍFUGA HUMANA (FORÇA G)")
    fim(faixa)
    return (bx, by)


# ======================================================================= 6. estacao 3 — realidade virtual
def estacao_vr():
    m = Malha("Est3_RealidadeVirtual")
    X0, Y0, X1, Y1 = -26, 31, -4, 49.5
    piso_zona(m, X0, Y0, X1, Y1, ESCURO, LUZ)
    # telão curvo na parede norte (5 paineis)
    for k in range(5):
        a = math.radians(-24 + k * 12)
        c = V((-15 + math.sin(a) * 18, 49.2 - (1 - math.cos(a)) * 18 - 0.0, 4.6))
        n = V((-math.sin(a), -math.cos(a), 0))
        m.caixa(c + n * -0.12, (3.9, 0.2, 4.4), ESCURO, rz=-a)
        m.painel_tex(c - n * 0.0 + n * 0.01, 3.7, 4.2, n, TELA_VR, uv=((k / 5, 0), ((k + 1) / 5, 0), ((k + 1) / 5, 1), (k / 5, 1)))
    # 3 estacoes de VR com mesa, monitores, cadeira e oculos
    for k, x in enumerate((-22.0, -15.0, -8.0)):
        y = 38.0
        mesa(m, (x, y, 0), 1.6, 0.8, tampo=BRANCO)
        monitor(m, (x - 0.35, y + 0.1, 0.76), rz=0.15)
        monitor(m, (x + 0.35, y + 0.1, 0.76), rz=-0.15)
        cadeira(m, (x, y - 1.6, 0), rz=0)
        # oculos VR sobre a mesa
        m.caixa((x + 0.55, y - 0.15, 0.85), (0.2, 0.12, 0.1), ESCURO)
        m.caixa((x + 0.55, y - 0.215, 0.85), (0.17, 0.01, 0.07), TELA)
        m.torus((x + 0.55, y - 0.07, 0.85), 0.11, 0.012, BORRACHA, 12, 4, M=Matrix.Diagonal((1, 1.3, 1, 1)))
        # quadro de rastreamento sobre a cadeira
        for sx in (-1, 1):
            for sy in (-1, 1):
                m.cilindro((x + sx * 1.2, y - 1.6 + sy * 1.2, 0), (x + sx * 1.2, y - 1.6 + sy * 1.2, 2.8), 0.04, ESTRUT, 8)
                m.caixa((x + sx * 1.2, y - 1.6 + sy * 1.2, 2.9), (0.15, 0.15, 0.12), ESCURO)
                m.cilindro((x + sx * 1.2, y - 1.6 + sy * 1.2, 2.9), (x + sx * 1.05, y - 1.6 + sy * 1.05, 2.82), 0.03, LUZ_VERM, 6)
        for s in (-1, 1):
            m.cilindro((x - 1.2, y - 1.6 + s * 1.2, 2.8), (x + 1.2, y - 1.6 + s * 1.2, 2.8), 0.035, ESTRUT, 6)
            m.cilindro((x + s * 1.2, y - 2.8, 2.8), (x + s * 1.2, y - 0.4, 2.8), 0.035, ESTRUT, 6)
    # simulador SAFER: cadeira com braços de controle
    fim(m, 1.0)
    for k, x in enumerate((-22.0, -15.0, -8.0)):
        col(f"COL_Mesa_VR_{k}", (x, 38.0, 0.45), (1.7, 0.9, 0.9))
    col("COL_Telao_VR", (-15, 48.6, 4.6), (20, 1.4, 4.4))
    faixa = Malha("Est3_Placa")
    faixa_estacao(faixa, (-15, 30.5, 7.0), 0, 3, "LABORATÓRIO DE REALIDADE VIRTUAL")
    fim(faixa)


# ======================================================================= 7. estacao 4 — flutuabilidade neutra (NBL)
def estacao_nbl():
    px0, py0, px1, py1 = POOL
    m = Malha("Est4_NBL")
    # paredes e fundo da piscina (azulejo)
    m.caixa_ab((px0 - 0.3, py0 - 0.3, POOL_Z - 0.4), (px1 + 0.3, py1 + 0.3, POOL_Z), AZULEJO)
    for a, b in (((px0 - 0.3, py0 - 0.3), (px1 + 0.3, py0)), ((px0 - 0.3, py1), (px1 + 0.3, py1 + 0.3)),
                 ((px0 - 0.3, py0), (px0, py1)), ((px1, py0), (px1 + 0.3, py1))):
        m.caixa_ab((a[0], a[1], POOL_Z), (b[0], b[1], 0.0), AZULEJO)
    # faixas escuras no fundo (raias)
    for k in range(1, 5):
        y = py0 + k * (py1 - py0) / 5
        m.caixa_ab((px0 + 1, y - 0.15, POOL_Z), (px1 - 1, y + 0.15, POOL_Z + 0.01), AZUL_ESC)
    # borda (coping) branca
    for a, b in (((px0 - 0.8, py0 - 0.8), (px1 + 0.8, py0)), ((px0 - 0.8, py1), (px1 + 0.8, py1 + 0.8)),
                 ((px0 - 0.8, py0), (px0, py1)), ((px1, py0), (px1 + 0.8, py1))):
        m.caixa_ab((a[0], a[1], 0.0), (b[0], b[1], 0.1), BRANCO)
    # mockup da ISS submerso: modulo laboratorio + no + treliça com radiador
    zc = -3.4
    Mmod = Matrix.Translation(V((16, 33, zc))) @ Matrix.Rotation(PI / 2, 4, "Y")
    m.torno([(0, -6.0), (1.6, -5.95), (2.0, -5.6), (2.05, 5.6), (1.6, 5.95), (0, 6.0)], BRANCO, 20, M=Mmod)
    for k in range(-5, 6):
        m.torus((0, 0, k * 1.0), 2.06, 0.05, METAL, 20, 4, M=Mmod)
    for k in range(-4, 5):                                       # corrimaos amarelos de EVA
        for s in (-1, 1):
            p = V((16 + k * 1.2, 33 + s * 0.6, zc + 2.1))
            m.cilindro(p, p + V((0.7, 0, 0)), 0.03, AMARELO, 6)
            m.cilindro(p, p - V((0, 0, 0.1)), 0.025, AMARELO, 6)
            m.cilindro(p + V((0.7, 0, 0)), p + V((0.7, 0, -0.1)), 0.025, AMARELO, 6)
    m.esfera((22.8, 33, zc), 2.2, BRANCO, 16, 10)              # no (node)
    m.cilindro((22.8, 30.5, zc), (22.8, 35.5, zc), 1.0, METAL, 16)
    m.caixa((9.0, 33, zc + 1.2), (0.5, 7.5, 0.5), METAL)       # treliça
    for k in range(6):
        m.caixa((9.0, 29.9 + k * 1.25, zc + 1.2), (0.6, 0.12, 0.6), METAL)
    m.caixa((9.0, 33, zc + 2.6), (2.6, 6.0, 0.08), BRANCO)     # radiador
    m.texto("ISS", (16, 30.97, zc), 0.8, AZUL_ESC, espessura=0.02)
    meatball(m, (16 - 2.5, 30.96, zc + 0.2), 0.6, (0, -1, 0))
    # plataforma do convés com guindaste
    gx, gy = px1 + 2.5, py0 + 3.0
    m.cilindro((gx, gy, 0), (gx, gy, 6.5), 0.3, AMARELO, 12)
    m.cilindro((gx, gy, 6.2), (px1 - 4.0, gy + 1.5, 6.2), 0.22, AMARELO, 10)
    m.caixa((gx, gy, 0.4), (1.4, 1.4, 0.8), ESTRUT)
    # corrimao em volta da piscina (com abertura na escada)
    e = 1.0
    corrimao(m, [(px0 - e, py0 - e), (px1 + e, py0 - e), (px1 + e, py1 + e), (px0 - e, py1 + e), (px0 - e, py0 + 6.0)], 1.1, AMARELO, 1.8)
    corrimao(m, [(px0 - e, py0 + 9.0), (px0 - e, py0 - e)], 1.1, AMARELO, 1.8)
    # escada de mergulho
    for s in (-0.4, 0.4):
        m.cilindro((px0 - 0.2, py0 + 7.5 + s, 1.0), (px0 + 0.25, py0 + 7.5 + s, 0.1), 0.03, METAL, 8)
        m.cilindro((px0 + 0.25, py0 + 7.5 + s, 0.1), (px0 + 0.25, py0 + 7.5 + s, POOL_Z + 1), 0.03, METAL, 8)
    for k in range(10):
        z = -0.4 - k * 0.5
        m.cilindro((px0 + 0.25, py0 + 7.1, z), (px0 + 0.25, py0 + 7.9, z), 0.02, METAL, 6)
    # mesa do diretor de teste (conves)
    mesa(m, (px0 - 3.2, py1 - 2.0, 0), 1.0, 2.4, rz=0, tampo=ESCURO)
    for k in range(3):
        monitor(m, (px0 - 3.3, py1 - 2.8 + k * 0.8, 0.76), rz=-PI / 2)
    cadeira(m, (px0 - 4.3, py1 - 2.0, 0), rz=-PI / 2)
    # armario de equipamento de mergulho (cilindros de ar)
    for k in range(6):
        p = (px1 + 2.2 + (k % 3) * 0.35, py1 - 1.0 - (k // 3) * 0.35, 0)
        m.cilindro(p, (p[0], p[1], 0.75), 0.13, AMARELO if k % 2 else LARANJA, 10)
        m.cilindro((p[0], p[1], 0.75), (p[0], p[1], 0.85), 0.04, METAL, 6)
    fim(m, 1.0)
    # agua (separada: transparente e sem sombra)
    a = Malha("Est4_NBL_Agua")
    a.caixa_ab((px0, py0, -0.45), (px1, py1, -0.4), AGUA)
    fim(a, 4.0)
    # plataforma de descida (ANIM): desce ate o fundo quando o treino comeca
    pl = Malha("ANIM_NBL_Plataforma", origem=(px0 + 3.0, py0 + 3.0, 0.15))
    pc = V((px0 + 3.0, py0 + 3.0, 0.15))
    pl.caixa(pc, (2.4, 2.4, 0.12), METAL)
    corrimao_local = [(pc.x - 1.1, pc.y - 1.1), (pc.x + 1.1, pc.y - 1.1), (pc.x + 1.1, pc.y + 1.1), (pc.x - 1.1, pc.y + 1.1)]
    for (x, y) in corrimao_local:
        pl.cilindro((x, y, pc.z), (x, y, pc.z + 1.0), 0.03, AMARELO, 6)
    for (p, q) in zip(corrimao_local, corrimao_local[1:]):
        pl.cilindro((p[0], p[1], pc.z + 1.0), (q[0], q[1], pc.z + 1.0), 0.03, AMARELO, 6)
    for (x, y) in corrimao_local:
        pl.cilindro((x, y, pc.z + 1.0), (pc.x, pc.y, pc.z + 4.0), 0.012, ESCURO, 4)
    fim(pl)
    # colisores: borda alta em volta da piscina (ninguem cai) + modulo
    ex = 1.0
    col("COL_NBL_S", ((px0 + px1) / 2, py0 - ex, 1.0), (px1 - px0 + 2 * ex, 0.25, 2.0))
    col("COL_NBL_N", ((px0 + px1) / 2, py1 + ex, 1.0), (px1 - px0 + 2 * ex, 0.25, 2.0))
    col("COL_NBL_O", (px0 - ex, (py0 + py1) / 2, 1.0), (0.25, py1 - py0 + 2 * ex, 2.0))
    col("COL_NBL_L", (px1 + ex, (py0 + py1) / 2, 1.0), (0.25, py1 - py0 + 2 * ex, 2.0))
    col("COL_NBL_Guindaste", (gx, gy, 1.0), (1.4, 1.4, 2.0))
    col("COL_NBL_Mesa", (px0 - 3.2, py1 - 2.0, 0.45), (1.1, 2.5, 0.9))
    faixa = Malha("Est4_Placa")
    faixa_estacao(faixa, (16, py0 - 2.5, 7.0), 0, 4, "LABORATÓRIO DE FLUTUABILIDADE NEUTRA")
    fim(faixa)


# ======================================================================= 8. estacao 5 — ARGOS + campo lunar
def estacao_argos():
    m = Malha("Est5_ARGOS")
    X0, Y0, X1, Y1 = 33.0, 2.0, 58.0, 24.0
    # campo lunar: grade com crateras suaves (altura maxima 0.35 m) + borda rampa
    lx0, ly0, lx1, ly1 = 36.0, 5.0, 55.0, 21.0
    N = 38
    crateras = [(40.0, 9.0, 1.6), (48.0, 16.0, 2.4), (52.5, 8.0, 1.2), (43.5, 18.0, 1.0), (45.0, 11.0, 0.8)]
    def h(x, y):
        z = 0.22
        for cx, cy, r in crateras:
            d = math.hypot(x - cx, y - cy) / r
            if d < 1.0:
                z -= 0.28 * (1 - d * d)
            elif d < 1.4:
                z += 0.12 * math.sin((d - 1.0) / 0.4 * PI)
        e = min(x - lx0, lx1 - x, y - ly0, ly1 - y)
        return z * min(1.0, max(0.0, e / 1.2)) + 0.01
    import bmesh
    grade = {}
    nx, ny = N, int(N * (ly1 - ly0) / (lx1 - lx0))
    for j in range(ny + 1):
        for i in range(nx + 1):
            x = lx0 + (lx1 - lx0) * i / nx; y = ly0 + (ly1 - ly0) * j / ny
            grade[i, j] = m.bm.verts.new((x, y, h(x, y)))
    idx = m.mi(REGOLITO)
    for j in range(ny):
        for i in range(nx):
            f = m.bm.faces.new((grade[i, j], grade[i + 1, j], grade[i + 1, j + 1], grade[i, j + 1]))
            f.material_index = idx; f.smooth = True
    # pedras
    for k in range(26):
        x, y = rnd.uniform(lx0 + 1.5, lx1 - 1.5), rnd.uniform(ly0 + 1.5, ly1 - 1.5)
        r = rnd.uniform(0.12, 0.45)
        pedra(m, (x, y, h(x, y) + r * 0.15), r)
    # bandeira e "rover" de ferramentas
    m.cilindro((53.5, 19.5, 0.1), (53.5, 19.5, 2.4), 0.025, METAL, 6)
    m.caixa((53.0, 19.5, 2.1), (0.9, 0.02, 0.6), BRANCO)
    meatball(m, (53.0, 19.48, 2.1), 0.22, (0, -1, 0), espessura=0.01)
    x, y = 38.5, 18.0
    m.caixa((x, y, 0.75), (1.2, 0.6, 0.06), METAL)
    for sx in (-1, 1):
        for sy in (-1, 1):
            m.cilindro((x + sx * 0.55, y + sy * 0.25, 0.2), (x + sx * 0.55, y + sy * 0.25, 0.75), 0.02, METAL, 6)
    for k in range(4):
        m.cilindro((x - 0.4 + k * 0.27, y, 0.78), (x - 0.4 + k * 0.27, y + 0.1, 1.5), 0.015, METAL, 6)
        m.caixa((x - 0.4 + k * 0.27, y + 0.11, 1.55), (0.1, 0.03, 0.12), LARANJA)
    m.caixa((x + 0.3, y - 0.1, 0.85), (0.35, 0.25, 0.2), BRANCO)
    # portico ARGOS: 4 colunas + 2 vigas longas (x) + trilhos
    H = 9.0
    for xx in (X0 + 1.5, X1 - 1.5):
        for yy in (Y0 + 1.0, Y1 - 1.0):
            m.caixa((xx, yy, H / 2), (0.6, 0.6, H), AZUL)
            m.caixa((xx, yy, 0.15), (1.0, 1.0, 0.3), AMARELO)
    for yy in (Y0 + 1.0, Y1 - 1.0):
        m.caixa(((X0 + X1) / 2, yy, H + 0.4), (X1 - X0 - 2.0, 0.6, 0.8), AZUL)
        m.caixa(((X0 + X1) / 2, yy, H + 0.85), (X1 - X0 - 2.0, 0.3, 0.1), METAL)
    m.texto("ARGOS", ((X0 + X1) / 2, Y0 + 0.65, H + 0.4), 0.55, TXT_BRANCO, espessura=0.01)
    # painel do operador
    ox, oy = X0 + 0.8, (Y0 + Y1) / 2
    m.caixa((ox, oy, 0.55), (0.8, 1.8, 1.1), ESCURO)
    m.caixa((ox + 0.05, oy, 1.15), (0.7, 1.8, 0.1), ESCURO, ry=-0.4)
    m.painel_tex((ox + 0.09, oy, 1.18), 1.6, 0.5, (0.39, 0, 0.92), TELA, cima=(1, 0, 0))
    m.cilindro((ox + 0.2, oy + 0.7, 1.1), (ox + 0.2, oy + 0.7, 1.3), 0.05, LUZ_VERM, 8)
    fim(m, 1.0)
    # ponte movel + carro + cabo + arnes (ANIM)
    bx = 45.5
    p = Malha("ANIM_ARGOS_Ponte", origem=(bx, (Y0 + Y1) / 2, H + 0.9))
    p.caixa((bx, (Y0 + Y1) / 2, H + 1.15), (0.8, Y1 - Y0 - 1.0, 0.5), AMARELO)
    for yy in (Y0 + 1.0, Y1 - 1.0):
        p.caixa((bx, yy, H + 1.0), (1.4, 0.8, 0.4), ESTRUT)
    cy = 12.0
    p.caixa((bx, cy, H + 0.7), (1.2, 1.2, 0.6), ESCURO)
    p.cilindro((bx, cy, H + 0.4), (bx, cy, 2.6), 0.015, ESCURO, 4)
    # arnes: barra de ombro + colete
    p.caixa((bx, cy, 2.55), (0.7, 0.08, 0.08), AMARELO)
    for s in (-1, 1):
        p.cilindro((bx + s * 0.33, cy, 2.55), (bx + s * 0.22, cy, 1.95), 0.012, ESCURO, 4)
    p.torno([(0.17, 1.15), (0.2, 1.3), (0.21, 1.6), (0.18, 1.85), (0.1, 1.95)], LARANJA, 10,
            M=Matrix.Translation(V((bx, cy, 0))) @ Matrix.Diagonal((1.25, 0.9, 1, 1)), fechar_base=False)
    fim(p)
    for xx in (X0 + 1.5, X1 - 1.5):
        for yy in (Y0 + 1.0, Y1 - 1.0):
            col(f"COL_ARGOS_Coluna_{int(xx)}_{int(yy)}", (xx, yy, H / 2), (0.7, 0.7, H))
    col("COL_ARGOS_Painel", (ox, oy, 0.6), (0.9, 1.9, 1.2))
    col("COL_ARGOS_Ferramentas", (38.5, 18.0, 0.6), (1.3, 0.7, 1.2))
    faixa = Malha("Est5_Placa")
    faixa_estacao(faixa, (45.5, 1.4, 7.6), 0, 5, "GRAVIDADE REDUZIDA E CAMPO LUNAR")
    fim(faixa)
    zona("ZONA_GravidadeLunar", ((lx0 + lx1) / 2, (ly0 + ly1) / 2, 3.0), (lx1 - lx0, ly1 - ly0, 6.0))   # alto: o pulo lunar passa de 3 m


# ======================================================================= 9. estacao 6 — sala de trajes
def estacao_trajes():
    m = Malha("Est6_Trajes")
    X0, Y0, X1, Y1 = 33.0, 28.0, 59.5, 49.6
    piso_zona(m, X0, Y0, X1, Y1, BRANCO, AZUL)
    # paredes de vidro (oeste e sul) com porta a oeste
    vid = Malha("Est6_Trajes_Vidros")
    for (a, b) in (((X0, Y0), (X0, 35.0)), ((X0, 40.0), (X0, Y1)), ((X0, Y0), (X1, Y0))):
        a, b = V((*a, 0)), V((*b, 0))
        L = (b - a).length
        d = (b - a).normalized()
        n = int(L / 2.5) + 1
        for k in range(n + 1):
            p = a + d * (L * k / n)
            m.caixa((p.x, p.y, 1.6), (0.12, 0.12, 3.2), AZUL_ESC)
        m.caixa(((a + b) / 2).to_tuple()[:2] + (3.25,), (abs(b.x - a.x) + 0.12, abs(b.y - a.y) + 0.12, 0.12), AZUL_ESC)
        m.caixa(((a + b) / 2).to_tuple()[:2] + (0.06,), (abs(b.x - a.x) + 0.12, abs(b.y - a.y) + 0.12, 0.12), AZUL_ESC)
        vid.caixa(((a + b) / 2).to_tuple()[:2] + (1.65,), (max(abs(b.x - a.x), 0.03), max(abs(b.y - a.y), 0.03), 3.1), VIDRO)
    m.texto("SALA DE TRAJES", (X0 + 6.5, Y0 - 0.08, 2.75), 0.38, AZUL_ESC, espessura=0.01)
    # armarios (parede leste)
    for k in range(10):
        y = 31.0 + k * 1.0
        m.caixa((X1 - 0.35, y, 1.0), (0.6, 0.95, 2.0), AZUL if k % 2 else TECIDO)
        m.caixa((X1 - 0.66, y + 0.3, 1.1), (0.03, 0.04, 0.25), METAL)
        for z in (1.7, 1.75, 1.8):
            m.caixa((X1 - 0.66, y, z), (0.02, 0.5, 0.02), ESCURO)
        m.caixa((X1 - 0.66, y, 1.45), (0.02, 0.4, 0.12), BRANCO)
    # bancos
    banco(m, (50.5, 36.0, 0), rz=PI / 2, comp=4.0)
    banco(m, (50.5, 42.0, 0), rz=PI / 2, comp=4.0)
    # suportes de traje (o traje detalhado e colocado no Unity sobre os INT_Traje_Suporte_*)
    for k, x in enumerate((38.0, 42.0)):
        y = 31.0
        m.cilindro((x, y, 0), (x, y, 0.2), 0.7, AZUL_ESC, 24)
        m.cilindro((x, y, 0.2), (x, y, 0.24), 0.65, AMARELO, 24)
        m.cilindro((x, y + 0.45, 0.2), (x, y + 0.45, 1.25), 0.05, METAL, 8)
        m.texto(("EMU · CAMINHADA ESPACIAL", "TRAJE DE LANÇAMENTO")[k], (x, y - 0.71, 0.12), 0.09, TXT_BRANCO, espessura=0.005)
    # estacao de vestir (moldura na parede)
    x, y = 46.0, 47.6
    for s in (-1, 1):
        m.caixa((x + s * 0.9, y, 1.5), (0.12, 0.12, 3.0), METAL)
    m.caixa((x, y, 3.0), (1.92, 0.12, 0.12), METAL)
    m.caixa((x, y - 0.25, 1.3), (1.0, 0.4, 0.1), METAL)
    m.cilindro((x, y - 0.25, 1.35), (x, y - 0.25, 1.5), 0.45, ESCURO, 16)
    # prateleira com capacetes, luvas e botas
    x0 = 49.0
    for z in (0.4, 1.2, 2.0):
        m.caixa((x0 + 2.0, Y1 - 0.4, z), (4.0, 0.6, 0.05), METAL)
    for sx in (0.0, 4.0):
        m.caixa((x0 + sx, Y1 - 0.4, 1.1), (0.06, 0.6, 2.2), METAL)
    for k in range(4):
        cx = x0 + 0.5 + k * 1.0
        m.esfera((cx, Y1 - 0.4, 2.2), 0.17, BRANCO, 12, 8)
        m.esfera((cx, Y1 - 0.53, 2.2), 0.13, AMARELO, 10, 6, escala=(1, 0.5, 0.8))
        m.caixa((cx - 0.12, Y1 - 0.4, 1.3), (0.12, 0.2, 0.15), BRANCO)
        m.caixa((cx + 0.12, Y1 - 0.4, 1.3), (0.12, 0.2, 0.15), BRANCO)
        m.caixa((cx - 0.12, Y1 - 0.45, 0.5), (0.13, 0.3, 0.14), BRANCO)
        m.caixa((cx + 0.12, Y1 - 0.45, 0.5), (0.13, 0.3, 0.14), BRANCO)
    # espelho e quadro de checklist
    m.caixa((37.5, Y1 - 0.1, 1.6), (2.0, 0.05, 2.2), METAL)
    m.caixa((37.5, Y1 - 0.13, 1.6), (1.85, 0.02, 2.05), VIDRO)
    m.caixa((55.5, Y1 - 0.1, 1.8), (3.0, 0.05, 1.6), BRANCO)
    m.texto("CHECKLIST DO TRAJE", (55.5, Y1 - 0.14, 2.4), 0.16, TXT_ESCURO, rz=0, espessura=0.005)
    for k, t in enumerate(("1. Roupa de resfriamento", "2. Calça e torso rígido", "3. Luvas e capacete", "4. Teste de pressão")):
        m.texto(t, (55.5, Y1 - 0.14, 2.1 - k * 0.25), 0.12, TXT_ESCURO, rz=0, espessura=0.005)
    fim(m, 1.0)
    fim(vid)
    col("COL_Trajes_Vidro_O1", (X0, (Y0 + 35.0) / 2, 1.6), (0.2, 35.0 - Y0, 3.2))
    col("COL_Trajes_Vidro_O2", (X0, (40.0 + Y1) / 2, 1.6), (0.2, Y1 - 40.0, 3.2))
    col("COL_Trajes_Vidro_S", ((X0 + X1) / 2, Y0, 1.6), (X1 - X0, 0.2, 3.2))
    col("COL_Armarios", (X1 - 0.35, 35.5, 1.0), (0.7, 10.0, 2.0))
    for k, x in enumerate((38.0, 42.0)):
        col(f"COL_Suporte_Traje_{k}", (x, 31.0, 1.0), (1.4, 1.4, 2.0))
        emp(f"INT_Traje_Suporte_{k}", (x, 31.0, 0.24), rz=PI)
    faixa = Malha("Est6_Placa")
    faixa_estacao(faixa, (46.0, 27.0, 7.2), 0, 6, "SALA DE TRAJES")
    fim(faixa)


# ======================================================================= 10. exterior: praca, letreiro, bandeiras, foguete
def exterior():
    m = Malha("Exterior_Praca")
    # letreiro monumento (estilo entrada do JSC): bloco creme com faixa vermelha, logo e letras em relevo
    sx, sy = 0.0, -26.0
    m.caixa((sx, sy, 1.0), (17, 1.6, 2.2), PAREDE)
    m.caixa((sx, sy, 0.0), (17.6, 2.2, 0.2), ESTRUT)
    m.caixa((sx, sy, 2.16), (17.2, 1.8, 0.12), AZUL_ESC)
    m.caixa((sx + 1.8, sy - 0.81, 0.35), (12.6, 0.05, 0.1), VERMELHO)
    m.caixa((sx - 6.2, sy, 1.75), (3.6, 1.3, 3.5), PAREDE)
    m.caixa((sx - 6.2, sy, 3.56), (3.8, 1.5, 0.12), AZUL_ESC)
    meatball(m, (sx - 6.2, sy - 0.66, 1.95), 1.25, (0, -1, 0))
    m.texto("NASA", (sx + 1.8, sy - 0.84, 1.45), 1.05, AZUL_ESC, espessura=0.08)
    m.texto("CENTRO ESPACIAL JOHNSON", (sx + 1.8, sy - 0.84, 0.68), 0.5, AZUL_ESC, espessura=0.05)
    # canteiro do letreiro
    m.caixa((sx, sy - 1.6, -0.05), (18, 1.2, 0.25), TRONCO)
    for k in range(14):
        m.esfera((sx - 8 + k * 1.23, sy - 1.6, 0.2), 0.42, FOLHA2 if k % 2 else FOLHA, 6, 4, escala=(1, 1, 0.7))
    # paginacao da praca: faixas azuis + canteiros com grama e arvores dos dois lados do caminho
    # caminho-guia ate a porta: duas faixas azuis continuas + setas amarelas apontando para a entrada
    for x in (-3.2, 3.2):
        m.caixa_ab((x - 0.15, -58.0, -0.08), (x + 0.15, -9.0, -0.075), AZUL)
    for y in range(-54, -12, 8):
        for s_ in (-1, 1):
            Mseta = Matrix.Translation(V((s_ * 0.55, y, -0.078))) @ Matrix.Rotation(-s_ * math.radians(40), 4, "Z")
            m.caixa(Mseta @ V((0, 0, 0)), (0.22, 1.5, 0.005), AMARELO)
    for x in (-13.0, 13.0):
        for y0 in (-54.0, -40.0):
            m.caixa_ab((x - 4.5, y0, -0.12), (x + 4.5, y0 + 10, 0.05), CALCADA)
            m.caixa_ab((x - 4.2, y0 + 0.3, 0.05), (x + 4.2, y0 + 9.7, 0.12), GRAMA)
            col(f"COL_Canteiro_{int(x)}_{int(y0)}", (x, y0 + 5, 0.0), (9.0, 10.0, 0.4))
    # mastros com bandeiras: EUA (listras), NASA, Brasil
    for k, x in enumerate((-8.0, 0.0, 8.0)):
        y = -14.0
        m.cilindro((x, y, -0.1), (x, y, 12.0), 0.09, METAL, 10)
        m.esfera((x, y, 12.05), 0.15, AMARELO, 8, 6)
        m.cilindro((x, y, -0.1), (x, y, 0.3), 0.4, ESTRUT, 12)
        fx0, fz1 = x + 0.12, 11.6
        W, Hf = 3.6, 2.2
        if k == 0:                                               # EUA: 13 listras + cantao
            for s in range(13):
                m.caixa((fx0 + W / 2, y, fz1 - Hf / 26 - s * Hf / 13), (W, 0.03, Hf / 13), VERMELHO if s % 2 == 0 else BRANCO)
            m.caixa((fx0 + W * 0.2, y - 0.02, fz1 - Hf * 0.27), (W * 0.4, 0.03, Hf * 0.54), AZUL_ESC)
            for i in range(5):
                for j in range(4):
                    m.caixa((fx0 + 0.16 + i * 0.28, y - 0.04, fz1 - 0.18 - j * 0.28), (0.07, 0.01, 0.07), BRANCO)
        elif k == 1:                                             # NASA: fundo azul + meatball
            m.caixa((fx0 + W / 2, y, fz1 - Hf / 2), (W, 0.03, Hf), AZUL)
            meatball(m, (fx0 + W / 2, y - 0.02, fz1 - Hf / 2), 0.85, (0, -1, 0), espessura=0.01)
        else:                                                    # Brasil
            m.caixa((fx0 + W / 2, y, fz1 - Hf / 2), (W, 0.03, Hf), VERDE_BR)
            Mb = Matrix.Translation(V((fx0 + W / 2, y - 0.02, fz1 - Hf / 2))) @ orient((0, -1, 0))
            m.prisma([(-W * 0.42, 0), (0, Hf * 0.41), (W * 0.42, 0), (0, -Hf * 0.41)], 0, 0.01, AMARELO, M=Mb)
            m.cilindro(Mb @ V((0, 0, 0.01)), Mb @ V((0, 0, 0.02)), Hf * 0.25, AZUL_ESC, 24)
            m.torus((0, 0, 0.025), Hf * 0.27, 0.035, BRANCO, 20, 4, M=Mb @ Matrix.Translation(V((0, -Hf * 0.12, 0))) @ Matrix.Diagonal((1, 1, 0.1, 1)), arco=110) if False else None
            m.caixa(Mb @ V((0, -0.02, 0.025)), (Hf * 0.5, 0.06, 0.01), BRANCO, rz=0, M=None)
    # bancos, postes e lixeiras ao longo do caminho
    for y in (-52.0, -37.0):
        for x in (-8.0, 8.0):
            poste(m, (x, y, -0.08), rz=PI if x > 0 else 0)
    for y in (-48.0, -33.0):
        banco(m, (-7.6, y, -0.08), rz=-PI / 2)
        banco(m, (7.6, y, -0.08), rz=PI / 2)
    # foguete Mercury-Redstone (como no Rocket Park)
    fx, fy = 17.0, -32.0
    m.caixa((fx, fy, 0.3), (4.0, 4.0, 0.8), ESTRUT)
    m.caixa((fx, fy, 0.75), (3.2, 3.2, 0.1), AMARELO)
    Mf = Matrix.Translation(V((fx, fy, 0.8)))
    m.torno([(0.0, 0.0), (0.89, 0.0), (0.89, 17.4), (0.89, 17.6)], BRANCO, 20, M=Mf)
    for z0, z1 in ((14.8, 15.6), (2.0, 2.5)):
        m.cilindro(Mf @ V((0, 0, z0)), Mf @ V((0, 0, z1)), 0.9, TXT_ESCURO, 20)
    m.torno([(0.89, 17.6), (0.94, 18.6), (0.94, 18.7)], BRANCO, 20, M=Mf)
    m.torno([(0.94, 18.7), (0.94, 18.8), (0.4, 20.7), (0.32, 21.4), (0.0, 21.5)], TXT_ESCURO, 20, M=Mf)
    m.cilindro(Mf @ V((0, 0, 21.4)), Mf @ V((0, 0, 22.4)), 0.12, VERMELHO, 8)
    for k in range(3):                                          # torre de escape (3 hastes)
        a = 2 * PI * k / 3
        m.cilindro(Mf @ V((0.3 * math.cos(a), 0.3 * math.sin(a), 21.3)), Mf @ V((0.06 * math.cos(a), 0.06 * math.sin(a), 23.6)), 0.03, VERMELHO, 6)
    m.cilindro(Mf @ V((0, 0, 23.6)), Mf @ V((0, 0, 25.0)), 0.1, VERMELHO, 8, r2=0.03)
    for k in range(4):                                          # aletas
        a = PI / 4 + k * PI / 2
        Ma = Mf @ Matrix.Rotation(a, 4, "Z") @ Matrix.Rotation(PI / 2, 4, "X")
        m.prisma([(0.85, 0.0), (2.2, -0.2), (2.0, 1.4), (0.85, 3.0)], -0.04, 0.04, BRANCO, M=Ma)
    m.texto("UNITED STATES", (fx, fy - 0.9, 0.8 + 9.0), 0.5, VERMELHO, rz=0, rx=PI / 2, espessura=0.02,
            M=Matrix.Translation(V((fx, fy - 0.9, 0.8 + 9.0))) @ Matrix.Rotation(PI / 2, 4, "Y") @ Matrix.Translation(-V((fx, fy - 0.9, 0.8 + 9.0))))
    m.caixa((fx, fy - 2.3, 0.6), (2.4, 0.2, 0.9), AZUL_ESC, rx=-0.3)
    m.texto("MERCURY-REDSTONE", (fx, fy - 2.42, 0.75), 0.18, TXT_BRANCO, rx=PI / 2 - 0.3, espessura=0.01)
    m.texto("1961", (fx, fy - 2.38, 0.45), 0.16, AMARELO, rx=PI / 2 - 0.3, espessura=0.01)
    # carros no estacionamento
    cores = [VERMELHO, BRANCO, AZUL, AMARELO, METAL, TECIDO, LARANJA]
    for k in range(7):
        x = -57 + k * 4 + 2
        for y, rz in ((-35.0, 0), (-19.0, PI)):
            if (k + int(y)) % 3 == 0:
                continue
            Mc = Matrix.Translation(V((x, y, -0.12))) @ Matrix.Rotation(rz + PI / 2, 4, "Z") @ Matrix.Rotation(PI / 2, 4, "X")
            cor = cores[(k * 3 + int(abs(y))) % len(cores)]
            m.prisma([(-2.1, 0.35), (2.1, 0.35), (2.15, 0.85), (1.2, 0.95), (0.6, 1.5), (-1.2, 1.5), (-1.8, 1.0), (-2.15, 0.9)], -0.85, 0.85, cor, M=Mc)
            m.prisma([(0.5, 1.0), (0.95, 1.0), (0.55, 1.42), (0.3, 1.42)], -0.86, 0.86, VIDRO, M=Mc)
            m.prisma([(-1.15, 1.02), (0.2, 1.02), (0.2, 1.42), (-1.1, 1.42)], -0.86, 0.86, VIDRO, M=Mc)
            for wx in (-1.35, 1.35):
                for wz in (-0.8, 0.8):
                    m.cilindro(Mc @ V((wx, 0.38, wz - 0.12)), Mc @ V((wx, 0.38, wz + 0.12)), 0.36, BORRACHA, 12)
    # arvores
    pos = [(x, -9.5) for x in (-20, -15, -10, 30, 35)] + [(x, y) for x in (-24, 24) for y in range(-56, -24, 8)]
    pos += [(x, y) for x in (-15.5, -10.5) for y in (-51.0, -45.0, -37.0, -31.0)] + [(16.5, y) for y in (-51.0, -45.0)]
    for x in (10.5, 14.0):                                         # canteiros da direita: arbustos baixos (nao escondem a fachada)
        for y in (-37.5, -33.5, -50.0, -46.0):
            pedra(m, (x, y, 0.35), 0.75, FOLHA2 if int(x + y) % 2 else FOLHA, achatar=0.8)
    pos += [(x, -78) for x in range(-200, 201, 22)]
    pos += [(HX0 - 8, y) for y in range(-2, 54, 10)] + [(HX1 + 8, y) for y in range(-2, 54, 10)]
    pos += [(ESTRADA_X - 15, y) for y in range(60, int(PAD_Y) - 60, 14)] + [(ESTRADA_X + 15, y) for y in range(67, int(PAD_Y) - 60, 14)]
    pos += [(rnd.uniform(-240, -80), rnd.uniform(70, 380)) for _ in range(30)] + [(rnd.uniform(110, 240), rnd.uniform(70, 380)) for _ in range(30)]
    arv = Malha("Arvores")
    arv.chanfro = 0
    for (x, y) in pos:
        arvore(arv, (x, y, -0.15), rnd.uniform(5.5, 8.5))
    fim(arv, 1.0)
    # placa de direcao na estrada
    m.cilindro((ESTRADA_X + 3.2, 58, -0.1), (ESTRADA_X + 3.2, 58, 3.0), 0.06, METAL, 8)
    m.cilindro((ESTRADA_X + 5.8, 58, -0.1), (ESTRADA_X + 5.8, 58, 3.0), 0.06, METAL, 8)
    m.caixa((ESTRADA_X + 4.5, 58, 2.6), (3.2, 0.08, 1.0), VERDE_BR)
    m.texto("PLATAFORMA 39A", (ESTRADA_X + 4.5, 57.95, 2.75), 0.3, TXT_BRANCO, espessura=0.01)
    m.texto("↑ 180 m", (ESTRADA_X + 4.5, 57.95, 2.35), 0.25, TXT_BRANCO, espessura=0.01)
    fim(m, 1.0)
    col("COL_Letreiro", (sx, sy, 1.1), (17.6, 2.0, 2.2))
    col("COL_Letreiro_Logo", (sx - 6.2, sy, 1.8), (3.8, 1.5, 3.6))
    col("COL_Foguete_Base", (fx, fy, 1.0), (4.0, 4.0, 2.0))
    col("COL_Foguete", (fx, fy, 11.0), (1.9, 1.9, 22.0))
    for k, x in enumerate((-8.0, 0.0, 8.0)):
        col(f"COL_Mastro_{k}", (x, -14.0, 1.0), (0.8, 0.8, 2.0))


# ======================================================================= 11. marcadores do tutorial
ESTACOES = [
    # nome, ponto do NPC, console de interacao, centro/tamanho da zona
    ("Recepcao", (3.5, 7.5), (0.0, 9.4), ((0, 7, 2), (24, 14, 4))),
    ("Medico", (-33.0, 9.5), (-26.5, 17.0), ((-34, 12, 2), (32, 20, 4))),
    ("Centrifuga", (-30.5, 33.5), (-29.8, 39.0), ((-38, 36, 2), (26, 24, 4))),
    ("RealidadeVirtual", (-17.5, 34.5), (-15.0, 37.3), ((-15, 40, 2), (22, 18, 4))),
    ("NBL", (2.5, 28.0), (POOL[0] - 3.0, POOL[3] - 2.0), ((16, 33, 2), (28, 22, 4))),
    ("ARGOS", (34.8, 10.0), (33.9, 13.0), ((45.5, 13, 2), (25, 22, 4))),
    ("Trajes", (36.0, 37.5), (46.0, 47.0), ((46, 39, 2), (26, 21, 4))),
]
def marcadores():
    emp("SPAWN_Player", (0.0, -43.0, -0.08), rz=0.0)
    emp("NPC_Inicio", (1.6, -10.0, -0.08), rz=PI)
    for i, (nome, npc, cons, (zc, zs)) in enumerate(ESTACOES):
        emp(f"EST_{i}_{nome}_NPC", (*npc, 0.0))
        emp(f"EST_{i}_{nome}_Console", (*cons, 1.0))
        zona(f"ZONA_EST_{i}_{nome}", zc, zs)
    # rota do NPC (pontos intermediarios para nao atravessar obstaculos)
    rota = [(0, -10), (0, -2), (0, 4), (-14, 4), (-33, 9.5), (-24, 26), (-30.5, 33.5), (-22, 30.5), (-17.5, 34.5), (-2, 30),
            (2.5, 28.0), (2.5, 23.0), (30.0, 23.0), (34.8, 10.0), (31.0, 24.0), (31.0, 37.5), (36.0, 37.5), (44.0, 44.0), (44.0, 52.0)]
    for k, (x, y) in enumerate(rota):
        emp(f"WP_NPC_{k:02d}", (x, y, 0.0))
    emp("WP_Saida_Hangar", (ESTRADA_X, HY1 + 4, -0.1))
    emp("WP_Estrada_Fim", (ESTRADA_X, PAD_Y - 42, -0.1))
    zona("ZONA_Saida_Hangar", (ESTRADA_X, HY1 + 2.0, 2.0), (8.0, 3.0, 4.0))
    zona("ZONA_Porta_Entrada", (0.0, 0.0, 1.6), (6.0, 7.0, 3.2))



# ======================================================================= L1: predio da recepcao + elevador secreto
# Predio 30 x 20 x 8 m (substitui o hangar). As estacoes vao para salas no subsolo (corredor, lotes L2/L3).
RX0, RX1, RY0, RY1, RZ = -15.0, 15.0, 0.0, 20.0, 8.0
EV = (9.2, 12.4, 20.4, 23.6)            # poco do elevador secreto (x0, x1, y0, y1), atras da parede do fundo
EV_FUNDO = -20.0                        # andar do corredor super secreto

def predio_recepcao():
    m = Malha("Recepcao_Predio")
    vid = Malha("Recepcao_Vidros")
    # ---- piso: porcelanato + faixas + emblema
    m.caixa_ab((RX0, RY0, -0.5), (RX1, RY1, 0.0), PISO)
    for x in (-11.0, 11.0):
        m.caixa_ab((x - 0.2, 0.6, 0.0), (x + 0.2, RY1 - 0.6, 0.006), AZUL)
    m.caixa_ab((-11.0, 18.0, 0.0), (11.0, 18.4, 0.006), AZUL)
    meatball(m, (0, 5.5, 0.012), 2.0, (0, 0, 1), cima=(0, 1, 0))
    m.cilindro((0, 5.5, 0.0), (0, 5.5, 0.008), 3.1, AMARELO, 48)
    m.cilindro((0, 5.5, 0.0), (0, 5.5, 0.01), 2.95, PISO, 48)
    # ---- fachada sul: pele de vidro com caixilhos, porta de vidro de correr, faixa com letreiro
    yS = RY0
    m.caixa_ab((RX0, yS - 0.3, 0.0), (RX1, yS + 0.1, 0.25), AZUL_ESC)                     # soleira
    for k in range(13):
        x = RX0 + 2.5 * k
        if -2.7 < x < 2.7:
            continue
        m.caixa((x, yS, 3.2), (0.14, 0.3, 6.4), AZUL_ESC)
    for z in (3.25, 6.4):
        m.caixa((0, yS, z), (RX1 - RX0, 0.3, 0.14), AZUL_ESC)
    vid.caixa_ab((RX0, yS - 0.03, 0.25), (-2.75, yS + 0.03, 6.4), VIDRO)
    vid.caixa_ab((2.75, yS - 0.03, 0.25), (RX1, yS + 0.03, 6.4), VIDRO)
    vid.caixa_ab((-2.75, yS - 0.03, 3.3), (2.75, yS + 0.03, 6.4), VIDRO)
    # portal da porta (moldura grossa metalica) + trilho
    for x in (-2.75, 2.75):
        m.caixa((x, yS, 1.65), (0.3, 0.5, 3.3), METAL)
    m.caixa((0, yS, 3.25), (5.8, 0.5, 0.3), METAL)
    m.caixa((0, yS - 0.26, 3.25), (5.2, 0.04, 0.16), LUZ)                                  # luz sobre a porta
    # faixa superior (platibanda) e cobertura
    m.caixa_ab((RX0 - 0.3, yS - 0.4, 6.4), (RX1 + 0.3, yS + 0.3, RZ + 0.6), PAREDE)
    m.caixa_ab((RX0 - 0.3, yS - 0.45, RZ + 0.35), (RX1 + 0.3, yS + 0.3, RZ + 0.6), AZUL)
    m.caixa_ab((RX0 - 0.3, yS - 0.45, 6.4), (RX1 + 0.3, yS - 0.38, 6.55), VERMELHO)
    meatball(m, (-11.2, yS - 0.42, 7.45), 0.85, (0, -1, 0))
    m.texto("CENTRO DE TREINAMENTO DE ASTRONAUTAS", (1.2, yS - 0.43, 7.45), 0.62, AZUL_ESC, espessura=0.02)
    # marquise em balanco (sem pilares na frente do letreiro), com tirantes e spots
    m.caixa_ab((-7.0, yS - 6.0, 3.6), (7.0, yS, 3.9), BRANCO)
    m.caixa_ab((-7.0, yS - 6.05, 3.55), (7.0, yS - 5.85, 4.15), AZUL)
    for x in (-6.4, 6.4):
        m.cilindro((x, yS - 5.6, 3.9), (x * 0.9, yS - 0.3, 6.3), 0.05, METAL, 12)
    for x in (-4.5, -1.5, 1.5, 4.5):
        for y in (yS - 4.2, yS - 1.8):
            m.cilindro((x, y, 3.56), (x, y, 3.6), 0.16, LUZ, 16)
    m.texto("RECEPÇÃO · NASA", (0, yS - 6.08, 3.85), 0.26, TXT_BRANCO, espessura=0.01)
    # ---- laterais (creme com aletas verticais e janelas altas) e fundo
    for x, s in ((RX0, -1), (RX1, 1)):
        m.caixa_ab((min(x, x + s * 0.4), RY0, 0.0), (max(x, x + s * 0.4), RY1, RZ + 0.6), PAREDE)
        vid.caixa_ab((min(x, x + s * 0.43), 2.0, 4.6), (max(x, x + s * 0.43), RY1 - 2.0, 6.0), VIDRO)
        for k in range(9):
            y = 1.0 + 2.25 * k
            m.caixa((x + s * 0.55, y, (RZ + 0.6) / 2), (0.3, 0.18, RZ + 0.6), AZUL_ESC)
    m.caixa_ab((RX0, RY1, 0.0), (EV[0], RY1 + 0.4, RZ + 0.6), PAREDE)
    m.caixa_ab((EV[1], RY1, 0.0), (RX1, RY1 + 0.4, RZ + 0.6), PAREDE)
    m.caixa_ab((EV[0], RY1, 3.5), (EV[1], RY1 + 0.4, RZ + 0.6), PAREDE)
    m.caixa_ab((RX0 - 0.3, RY0 - 0.3, RZ + 0.3), (RX1 + 0.3, RY1 + 0.7, RZ + 0.6), PAREDE_INT)  # laje
    m.caixa_ab((RX0 - 0.3, RY1 + 0.4, RZ + 0.3), (RX1 + 0.3, RY1 + 0.7, RZ + 1.0), AZUL)      # platibanda fundo
    # ---- interior: forro com luminarias, rodape, paineis nas paredes, telas
    m.caixa_ab((RX0, RY0, 7.6), (RX1, RY1, 7.7), PAREDE_INT)
    for i in range(5):
        for j in range(3):
            cx, cy = -10 + 5 * i, 4.5 + 5.5 * j
            m.caixa((cx, cy, 7.58), (1.6, 0.7, 0.04), LUZ)
            m.caixa((cx, cy, 7.6), (1.75, 0.85, 0.03), METAL)
    for x, s in ((RX0 + 0.4, 1), (RX1 - 0.4, -1)):
        m.caixa_ab((min(x, x + s * 0.06), RY0 + 0.3, 0.0), (max(x, x + s * 0.06), RY1, 0.15), AZUL)      # rodape
        for k in range(8):                                                                               # paineis lisos
            y0 = 1.0 + 2.25 * k
            m.caixa_ab((min(x, x + s * 0.05), y0 + 0.04, 0.2), (max(x, x + s * 0.05), y0 + 2.21, 4.2), PAINEL_LISO)
            m.caixa_ab((min(x, x + s * 0.07), y0 + 0.04, 1.05), (max(x, x + s * 0.07), y0 + 2.21, 1.35), MADEIRA_CL)
        m.caixa_ab((min(x, x + s * 0.08), RY0 + 0.3, 4.2), (max(x, x + s * 0.08), RY1, 4.35), AZUL)
        m.caixa_ab((min(x, x + s * 0.06), RY0 + 0.3, 4.35), (max(x, x + s * 0.06), RY1, 7.6), PAINEL_LISO)
    # telas: Terra (esquerda) e quadro da missao (direita)
    m.caixa((RX0 + 0.5, 9.0, 2.6), (0.12, 4.4, 2.6), ESCURO)
    m.painel_tex((RX0 + 0.57, 9.0, 2.6), 4.2, 2.4, (1, 0, 0), TELA_VR)
    m.caixa((RX1 - 0.5, 9.0, 2.6), (0.12, 4.4, 2.6), ESCURO)
    m.painel_tex((RX1 - 0.57, 9.0, 2.6), 4.2, 2.4, (-1, 0, 0), TELA)
    m.texto("A TERRA AGORA · ISS", (RX0 + 0.6, 9.0, 4.05), 0.2, TXT_BRANCO, rz=-PI / 2, espessura=0.005)
    m.texto("PRÓXIMAS MISSÕES", (RX1 - 0.6, 9.0, 4.05), 0.2, TXT_BRANCO, rz=PI / 2, espessura=0.005)
    # parede de destaque atras do balcao (sem nada na frente do letreiro)
    m.caixa_ab((-8.0, RY1 - 0.5, 0.0), (7.6, RY1, 5.6), AZUL_ESC)
    m.caixa_ab((-8.0, RY1 - 0.55, 5.6), (7.6, RY1, 5.75), AMARELO)
    meatball(m, (-5.4, RY1 - 0.52, 3.3), 1.5, (0, -1, 0))
    m.texto("PROGRAMA DE TREINAMENTO", (1.6, RY1 - 0.53, 3.9), 0.55, TXT_BRANCO, espessura=0.02)
    m.texto("DE ASTRONAUTAS", (1.6, RY1 - 0.53, 3.15), 0.55, TXT_BRANCO, espessura=0.02)
    m.caixa_ab((-2.4, RY1 - 0.54, 2.6), (5.6, RY1 - 0.5, 2.66), VERMELHO)
    m.texto("Houston, Texas", (1.6, RY1 - 0.53, 2.2), 0.32, AMARELO, espessura=0.01)
    # painel da parede onde fica a passagem secreta (mesmo acabamento das outras paredes)
    m.caixa_ab((7.6, RY1 - 0.25, 0.0), (EV[0], RY1, 5.6), PAINEL_LISO)
    m.caixa_ab((EV[1], RY1 - 0.25, 0.0), (RX1, RY1, 5.6), PAINEL_LISO)
    m.caixa_ab((EV[0], RY1 - 0.25, 3.45), (EV[1], RY1, 5.6), PAINEL_LISO)
    m.caixa_ab((RX0, RY1 - 0.2, 5.6), (RX1, RY1, 7.6), PAINEL_LISO)
    m.caixa_ab((RX0 + 0.4, RY1 - 0.25, 0.0), (-8.0, RY1, 5.6), PAINEL_LISO)
    m.caixa_ab((RX0 + 0.4, RY1 - 0.28, 1.05), (-8.0, RY1 - 0.24, 1.35), MADEIRA_CL)
    m.caixa_ab((7.6, RY1 - 0.28, 1.05), (EV[0], RY1 - 0.24, 1.35), MADEIRA_CL)
    m.caixa_ab((EV[1], RY1 - 0.28, 1.05), (RX1 - 0.4, RY1 - 0.24, 1.35), MADEIRA_CL)
    m.caixa_ab((7.6, RY1 - 0.3, 0.0), (RX1, RY1 - 0.25, 0.15), AZUL)
    # balcao curvo
    for k in range(9):
        a0 = math.radians(200 + k * 15.5); a1 = math.radians(200 + (k + 1) * 15.5)
        R1, R2 = 3.4, 4.1
        pts = [(R1 * math.cos(a0), R1 * math.sin(a0)), (R2 * math.cos(a0), R2 * math.sin(a0)),
               (R2 * math.cos(a1), R2 * math.sin(a1)), (R1 * math.cos(a1), R1 * math.sin(a1))]
        Mb = Matrix.Translation(V((0, 15.0, 0)))
        m.prisma(pts, 0.12, 1.0, MADEIRA, M=Mb)
        m.prisma(pts, 1.0, 1.08, BRANCO, M=Mb)
        m.prisma(pts, 0.0, 0.12, AZUL_ESC, M=Mb)
        m.prisma([(x * 1.0, y * 1.0) for x, y in pts], 0.5, 0.56, LUZ, M=Matrix.Translation(V((0, 15.0, 0))) @ Matrix.Diagonal((1.005, 1.005, 1, 1))) if k % 2 == 0 else None
    for x in (-1.5, 1.5):
        monitor(m, (x, 12.2, 0.76), rz=PI)
        cadeira(m, (x, 13.1, 0), rz=PI)
    m.caixa((0, 12.3, 0.52), (3.8, 0.6, 0.05), BRANCO)
    # mobiliario: bancos, vasos, totem informativo, vitrine Mercury
    for x in (-11.5, 11.5):
        banco(m, (x, 4.0, 0), rz=-PI / 2 if x < 0 else PI / 2)
        banco(m, (x, 13.5, 0), rz=-PI / 2 if x < 0 else PI / 2)
    for (x, y) in ((-13.6, 1.2), (13.6, 1.2), (-13.6, 18.6), (13.6, 18.6), (-8.6, 18.8)):
        m.cilindro((x, y, 0), (x, y, 0.7), 0.42, BRANCO, 24, r2=0.36)
        m.cilindro((x, y, 0.62), (x, y, 0.7), 0.38, TRONCO, 24)
        for dx, dy, dz, r_, mat in ((0, 0, 1.3, 0.62, FOLHA), (0.25, -0.15, 1.75, 0.45, FOLHA2), (-0.25, 0.2, 1.7, 0.42, FOLHA),
                                    (0.05, 0.1, 2.15, 0.32, FOLHA2), (-0.3, -0.25, 1.15, 0.38, FOLHA2)):
            pedra(m, (x + dx, y + dy, dz), r_, mat, achatar=1.0)
    m.caixa((-5.0, 3.0, 0.6), (0.5, 0.4, 1.2), AZUL_ESC)
    m.caixa((-5.0, 2.92, 1.35), (0.6, 0.3, 0.45), ESCURO, rx=-0.35)
    m.painel_tex((-5.0, 2.78, 1.37), 0.5, 0.36, (0, -0.94, 0.34), TELA)
    # estar: 2 sofas em L + mesa de centro com revistas
    def sofa(cx, cy, rz, comp=2.2):
        R = Matrix.Translation(V((cx, cy, 0))) @ Matrix.Rotation(rz, 4, "Z")
        m.caixa(R @ V((0, 0, 0.22)), (comp, 0.85, 0.3), SOFA, rz)
        m.caixa(R @ V((0, 0.05, 0.42)), (comp - 0.1, 0.7, 0.14), SOFA, rz)
        m.caixa(R @ V((0, 0.36, 0.7)), (comp, 0.18, 0.62), SOFA, rz, rx=-0.12)
        for sx in (-1, 1):
            m.caixa(R @ V((sx * (comp / 2 - 0.09), 0, 0.5)), (0.18, 0.85, 0.45), SOFA, rz)
        for k in range(int(comp // 0.75)):
            m.caixa(R @ V((-comp / 2 + 0.45 + k * 0.72, 0.05, 0.5)), (0.66, 0.62, 0.02), AZUL_ESC, rz)
    sofa(-10.5, 6.0, 0.0); sofa(-12.7, 8.4, -PI / 2, 2.0)
    m.cilindro((-10.5, 8.2, 0.0), (-10.5, 8.2, 0.42), 0.55, MADEIRA_CL, 32)
    m.cilindro((-10.5, 8.2, 0.42), (-10.5, 8.2, 0.46), 0.6, BRANCO, 32)
    m.caixa((-10.4, 8.1, 0.47), (0.35, 0.25, 0.02), VERMELHO, rz=0.3)
    m.caixa((-10.65, 8.3, 0.48), (0.3, 0.22, 0.02), AZUL, rz=-0.2)
    # vitrine de vidro com maquete de foguete e capacete
    vx, vy = 10.5, 7.5
    m.caixa_ab((vx - 1.2, vy - 0.6, 0.0), (vx + 1.2, vy + 0.6, 0.9), AZUL_ESC)
    m.caixa_ab((vx - 1.25, vy - 0.65, 0.9), (vx + 1.25, vy + 0.65, 0.95), METAL)
    vid2 = Malha("Recepcao_Vitrine_Vidro")
    vid2.caixa_ab((vx - 1.2, vy - 0.6, 0.95), (vx + 1.2, vy + 0.6, 2.4), VIDRO)
    fim(vid2)
    Mf = Matrix.Translation(V((vx - 0.5, vy, 0.95)))
    m.torno([(0.0, 0.0), (0.12, 0.05), (0.12, 0.9), (0.08, 1.15), (0.0, 1.3)], BRANCO, 16, M=Mf)
    m.torno([(0.12, 0.3), (0.125, 0.55)], LARANJA, 16, M=Mf)
    for k in range(3):
        a = 2 * PI * k / 3
        m.caixa(Mf @ V((math.cos(a) * 0.15, math.sin(a) * 0.15, 0.1)), (0.12, 0.02, 0.2), VERMELHO, rz=a)
    m.cilindro((vx + 0.45, vy, 0.95), (vx + 0.45, vy, 1.15), 0.18, METAL, 24)
    m.esfera((vx + 0.45, vy, 1.42), 0.27, BRANCO, 24, 16)
    m.esfera((vx + 0.45, vy - 0.12, 1.43), 0.2, LUZ_AMBAR, 20, 12, escala=(1, 0.6, 0.8))
    m.texto("MAQUETE SATURN · CAPACETE EMU", (vx, vy - 0.66, 0.6), 0.09, TXT_BRANCO, espessura=0.005)
    # parede de fotos de missoes (lado esquerdo, perto da entrada)
    for i in range(3):
        for j in range(2):
            fx, fz = 3.0 + i * 1.6, 2.0 + j * 1.2
            m.caixa((RX0 + 0.5, fx, fz), (0.06, 1.3, 0.9), MADEIRA_CL)
            m.painel_tex((RX0 + 0.54, fx, fz), 1.15, 0.75, (1, 0, 0), FOTO_A, uv=((i / 3, j / 2), ((i + 1) / 3, j / 2), ((i + 1) / 3, (j + 1) / 2), (i / 3, (j + 1) / 2)))
    m.texto("MISSÕES QUE FIZERAM HISTÓRIA", (RX0 + 0.56, 4.6, 3.05), 0.16, AZUL_ESC, rz=-PI / 2, espessura=0.005)
    m.cilindro((7.5, 5.0, 0), (7.5, 5.0, 0.9), 0.8, AZUL_ESC, 32)
    m.torno([(0.55, 0.9), (0.6, 0.95), (0.32, 1.75), (0.22, 1.95), (0.0, 2.0)], ESCURO, 24, M=Matrix.Translation(V((7.5, 5.0, 0))))
    m.texto("MERCURY 1961", (7.5, 4.18, 0.5), 0.14, TXT_BRANCO, espessura=0.01)
    fim(m, 1.0); fim(vid, 1.0)

    # ---- portas de vidro de correr (ANIM)
    for lado, nome in ((-1, "ANIM_Porta_Entrada_O"), (1, "ANIM_Porta_Entrada_L")):
        x0, x1 = (0.0, 2.6) if lado > 0 else (-2.6, 0.0)
        cx = (x0 + x1) / 2
        p = Malha(nome, origem=(cx, yS, 0))
        p.caixa((cx, yS, 1.6), (x1 - x0 - 0.04, 0.04, 3.1), VIDRO)
        for z in (0.06, 3.15):
            p.caixa((cx, yS, z), (x1 - x0, 0.1, 0.12), AZUL_ESC)
        for x in (x0 + 0.05, x1 - 0.05):
            p.caixa((x, yS, 1.6), (0.1, 0.1, 3.2), AZUL_ESC)
        hx = x1 - 0.25 if lado < 0 else x0 + 0.25
        p.cilindro((hx, yS - 0.09, 0.7), (hx, yS - 0.09, 1.7), 0.022, METAL, 12)
        p.caixa((cx, yS - 0.03, 1.2), (x1 - x0 - 0.3, 0.01, 0.06), AMARELO)              # faixa de seguranca no vidro
        fim(p)

    # ---- painel secreto (ANIM): desliza para dentro da parede revelando o elevador
    ps = Malha("ANIM_Painel_Secreto", origem=(EV[0], RY1 - 0.12, 0))
    ps.caixa_ab((EV[0] + 0.02, RY1 - 0.2, 0.0), (EV[1] - 0.02, RY1 - 0.05, 3.44), PAINEL_LISO)
    ps.caixa_ab((EV[0] + 0.02, RY1 - 0.23, 1.05), (EV[1] - 0.02, RY1 - 0.2, 1.35), MADEIRA_CL)
    ps.caixa_ab((EV[0] + 0.02, RY1 - 0.22, 0.0), (EV[1] - 0.02, RY1 - 0.2, 0.15), AZUL)
    ps.caixa((EV[0] + 1.6, RY1 - 0.22, 2.0), (0.6, 0.03, 0.8), MADEIRA)                     # quadro (disfarce)
    ps.painel_tex((EV[0] + 1.6, RY1 - 0.24, 2.0), 0.5, 0.7, (0, -1, 0), LOGO)
    fim(ps)
    # ---- poco + cabine do elevador secreto (desce ate o corredor)
    w = Malha("Elevador_Secreto_Poco")
    ex0, ex1, ey0, ey1 = EV
    for (a, b) in (((ex0 - 0.3, ey0), (ex0, ey1 + 0.3)), ((ex1, ey0), (ex1 + 0.3, ey1 + 0.3)), ((ex0, ey1), (ex1, ey1 + 0.3))):
        w.caixa_ab((a[0], a[1], EV_FUNDO - 0.5), (b[0], b[1], RZ + 0.6), CONCRETO_P)
    w.caixa_ab((ex0 - 0.3, ey0 - 0.4, EV_FUNDO + 3.5), (ex1 + 0.3, ey0, -0.5), CONCRETO_P)     # frente do poco entre andares
    w.caixa_ab((ex0 - 0.3, ey0 - 0.4, RZ + 0.6), (ex1 + 0.3, ey1 + 0.3, RZ + 0.9), AZUL)         # tampa do poco
    for (a, b) in (((RX0 - 0.5, RY1 + 0.4), (ex0 - 0.3, 24.6)), ((ex1 + 0.3, RY1 + 0.4), (RX1 + 0.5, 24.6)), ((ex0 - 0.3, ey1 + 0.3), (ex1 + 0.3, 24.6))):
        w.caixa_ab((a[0], a[1], -0.15), (b[0], b[1], -0.08), CALCADA)                           # calcada atras do predio
    for z in range(int(EV_FUNDO), 0, 3):                                                     # luzes de passagem no poco
        w.caixa((ex1 - 0.02, (ey0 + ey1) / 2, z + 1.5), (0.04, 0.6, 0.12), LUZ_AMBAR)
    fim(w)
    c = Malha("ANIM_Elevador_Secreto", origem=((ex0 + ex1) / 2, (ey0 + ey1) / 2, 0.0))
    c.caixa_ab((ex0 + 0.05, ey0 + 0.05, -0.15), (ex1 - 0.05, ey1 - 0.05, 0.0), GRADE_P)
    for (a, b) in (((ex0 + 0.05, ey0 + 0.05), (ex0 + 0.15, ey1 - 0.05)), ((ex1 - 0.15, ey0 + 0.05), (ex1 - 0.05, ey1 - 0.05)),
                   ((ex0 + 0.05, ey1 - 0.15), (ex1 - 0.05, ey1 - 0.05))):
        c.caixa_ab((a[0], a[1], 0.0), (b[0], b[1], 3.2), METAL)
    c.caixa_ab((ex0 + 0.05, ey0 + 0.05, 3.2), (ex1 - 0.05, ey1 - 0.05, 3.32), METAL)
    c.caixa((ex0 + 0.18, (ey0 + ey1) / 2, 1.2), (0.05, 2.6, 0.06), LUZ)                       # faixa de luz lateral
    c.caixa((ex1 - 0.18, (ey0 + ey1) / 2, 1.2), (0.05, 2.6, 0.06), LUZ)
    c.caixa(((ex0 + ex1) / 2, (ey0 + ey1) / 2, 3.18), (1.6, 1.6, 0.04), LUZ)
    c.caixa((ex1 - 0.2, ey0 + 0.6, 1.25), (0.06, 0.4, 0.7), ESCURO)                           # botoeira
    for k, mat in enumerate((LUZ_VERDE, LUZ_AMBAR, LUZ_VERM)):
        c.cilindro((ex1 - 0.24, ey0 + 0.6, 1.45 - k * 0.18), (ex1 - 0.27, ey0 + 0.6, 1.45 - k * 0.18), 0.05, mat, 12)
    c.caixa(((ex0 + ex1) / 2, ey1 - 0.2, 2.75), (1.0, 0.04, 0.3), ESCURO)                       # mostrador de andar (TMP no Unity)
    c.caixa_ab((ex0 + 0.15, ey1 - 0.17, 0.95), (ex1 - 0.15, ey1 - 0.12, 1.0), METAL)            # corrimao
    fim(c)
    for lado, nome in ((-1, "ANIM_Elevador_Secreto_Porta_E"), (1, "ANIM_Elevador_Secreto_Porta_D")):
        xm = (ex0 + ex1) / 2
        x0, x1 = (ex0 + 0.1, xm) if lado < 0 else (xm, ex1 - 0.1)
        d = Malha(nome, origem=((x0 + x1) / 2, ey0 + 0.1, 0.0))
        d.caixa_ab((x0, ey0 + 0.06, 0.0), (x1, ey0 + 0.14, 3.15), METAL)
        d.caixa_ab((x0 + 0.1, ey0 + 0.04, 1.4), (x1 - 0.1, ey0 + 0.06, 1.46), AZUL)
        fim(d)
    # ---- patamar do subsolo (vestibulo do corredor, o L2 continua daqui)
    v = Malha("Subsolo_Vestibulo")
    vx0, vx1, vy0, vy1 = 6.5, 15.0, 12.0, EV[2]
    v.caixa_ab((vx0, vy0, EV_FUNDO - 0.4), (vx1, vy1, EV_FUNDO), GRADE_P)
    v.caixa_ab((vx0, vy0, EV_FUNDO + 4.0), (vx1, vy1 + 0.4, EV_FUNDO + 4.3), CONCRETO_P)
    for (a, b) in (((vx0 - 0.3, vy0), (vx0, vy1)), ((vx0, vy0 - 0.3), (vx1, vy0))):
        v.caixa_ab((a[0], a[1], EV_FUNDO), (b[0], b[1], EV_FUNDO + 4.0), CONCRETO_P)
    # parede leste com o portal do corredor (tubo de 6 m, eixo em y = 16.2)
    v.caixa_ab((vx1, vy0, EV_FUNDO), (vx1 + 0.3, 13.2, EV_FUNDO + 5.2), CONCRETO_P)
    v.caixa_ab((vx1, 19.2, EV_FUNDO), (vx1 + 0.3, vy1, EV_FUNDO + 5.2), CONCRETO_P)
    v.caixa_ab((vx0, vy0, EV_FUNDO + 4.0), (vx1 + 0.3, vy1 + 0.4, EV_FUNDO + 5.2), CONCRETO_P)
    v.torus((0, 0, 0), 3.05, 0.14, ESTRUT, 48, 8, M=Matrix.Translation(V((vx1 + 0.15, 16.2, EV_FUNDO + 2.0))) @ Matrix.Rotation(PI / 2, 4, "Y"))
    v.caixa((vx1 - 0.05, 16.2, EV_FUNDO + 4.5), (0.1, 3.4, 0.5), AZUL_ESC)
    v.texto("CORREDOR DE TREINAMENTO", (vx1 - 0.11, 16.2, EV_FUNDO + 4.5), 0.24, TXT_BRANCO, rz=-PI / 2, espessura=0.005)
    v.caixa_ab((vx0, vy1, EV_FUNDO), (ex0 - 0.3, vy1 + 0.4, EV_FUNDO + 4.0), CONCRETO_P)
    v.caixa_ab((ex1 + 0.3, vy1, EV_FUNDO), (vx1, vy1 + 0.4, EV_FUNDO + 4.0), CONCRETO_P)
    for x in (vx0 + 0.6, vx1 - 0.6):
        v.caixa((x, (vy0 + vy1) / 2, EV_FUNDO + 3.9), (0.12, vy1 - vy0 - 1, 0.06), LUZ)
    v.caixa(((vx0 + vx1) / 2, vy1 - 0.05, EV_FUNDO + 3.7), (2.8, 0.1, 0.4), AZUL_ESC)
    v.texto("NÍVEL -1 · ACESSO RESTRITO", ((vx0 + vx1) / 2, vy1 - 0.11, EV_FUNDO + 3.7), 0.22, TXT_BRANCO, rz=PI, espessura=0.005)
    fim(v)

    # ---- colisores
    col("COL_Recepcao_Piso", (0, 10, -0.25), (RX1 - RX0, RY1 - RY0, 0.5))
    col("COL_Recepcao_Fachada_O", ((RX0 - 2.75) / 2, yS, 3.5), (-2.75 - RX0, 0.4, 7.0))
    col("COL_Recepcao_Fachada_L", ((RX1 + 2.75) / 2, yS, 3.5), (RX1 - 2.75, 0.4, 7.0))
    col("COL_Recepcao_Fachada_Topo", (0, yS, 5.5), (5.6, 0.4, 4.4))
    col("COL_Recepcao_Lateral_O", (RX0 - 0.2, 10, 4), (0.4, RY1, 8))
    col("COL_Recepcao_Lateral_L", (RX1 + 0.2, 10, 4), (0.4, RY1, 8))
    col("COL_Recepcao_Fundo_O", ((RX0 + EV[0]) / 2, RY1 - 0.1, 4), (EV[0] - RX0, 0.6, 8))
    col("COL_Recepcao_Fundo_L", ((RX1 + EV[1]) / 2, RY1 - 0.1, 4), (RX1 - EV[1], 0.6, 8))
    col("COL_Recepcao_Fundo_Topo", ((EV[0] + EV[1]) / 2, RY1 - 0.1, 5.7), (EV[1] - EV[0], 0.6, 4.6))
    col("COL_Balcao", (0, 11.6, 0.55), (8.0, 2.0, 1.1))
    col("COL_Vitrine_Mercury", (7.5, 5.0, 1.0), (1.7, 1.7, 2.0))
    col("COL_Totem", (-5.0, 3.0, 0.8), (0.6, 0.6, 1.6))
    col("COL_Sofa_1", (-10.5, 6.0, 0.4), (2.2, 0.9, 0.8)); col("COL_Sofa_2", (-12.7, 8.4, 0.4), (0.9, 2.0, 0.8))
    col("COL_Mesa_Centro", (-10.5, 8.2, 0.25), (1.2, 1.2, 0.5)); col("COL_Vitrine", (10.5, 7.5, 1.2), (2.5, 1.3, 2.4))
    col("COL_Poco_O", (ex0 - 0.15, (ey0 + ey1) / 2, (EV_FUNDO + RZ) / 2), (0.3, ey1 - ey0 + 0.6, RZ - EV_FUNDO))
    col("COL_Poco_L", (ex1 + 0.15, (ey0 + ey1) / 2, (EV_FUNDO + RZ) / 2), (0.3, ey1 - ey0 + 0.6, RZ - EV_FUNDO))
    col("COL_Poco_Fundo", ((ex0 + ex1) / 2, ey1 + 0.15, (EV_FUNDO + RZ) / 2), (ex1 - ex0, 0.3, RZ - EV_FUNDO))
    col("COL_Subsolo_Piso", ((vx0 + vx1) / 2, (vy0 + vy1) / 2, EV_FUNDO - 0.2), (vx1 - vx0, vy1 - vy0, 0.4))
    col("COL_Subsolo_O", (vx0 - 0.15, (vy0 + vy1) / 2, EV_FUNDO + 2), (0.3, vy1 - vy0, 4))
    col("COL_Subsolo_L_S", (vx1 + 0.15, (vy0 + 13.2) / 2, EV_FUNDO + 2), (0.3, 13.2 - vy0, 4))
    col("COL_Subsolo_L_N", (vx1 + 0.15, (19.2 + vy1) / 2, EV_FUNDO + 2), (0.3, vy1 - 19.2, 4))
    col("COL_Subsolo_S", ((vx0 + vx1) / 2, vy0 - 0.15, EV_FUNDO + 2), (vx1 - vx0, 0.3, 4))
    for (nome, x, y) in (("COL_Vaso_1", -13.6, 1.2), ("COL_Vaso_2", 13.6, 1.2), ("COL_Vaso_3", -13.6, 18.6), ("COL_Vaso_4", 13.6, 18.6), ("COL_Vaso_5", -8.6, 18.8)):
        col(nome, (x, y, 0.6), (0.9, 0.9, 1.2))
    # colisor do painel secreto (desliga quando abre) e piso da cabine (segue a cabine)
    col("COL_Painel_Secreto", ((EV[0] + EV[1]) / 2, RY1 - 0.12, 1.75), (EV[1] - EV[0], 0.2, 3.5))
    col("COL_Elevador_Secreto_Piso", ((ex0 + ex1) / 2, (ey0 + ey1) / 2, -0.08), (ex1 - ex0 - 0.1, ey1 - ey0 - 0.1, 0.16))
    # marcadores
    emp("EST_0_Recepcao_NPC", (2.6, 10.0, 0.0))
    emp("EST_0_Recepcao_Console", (0.0, 11.3, 1.0))
    zona("ZONA_EST_0_Recepcao", (0, 10, 2), (28, 18, 4))
    emp("NPC_Elevador_Secreto", ((ex0 + ex1) / 2 + 0.6, (ey0 + ey1) / 2 + 0.6, 0.0))
    emp("WP_Painel_Secreto", ((ex0 + ex1) / 2, RY1 - 1.6, 0.0))
    emp("INT_Painel_Secreto", ((ex0 + ex1) / 2 + 1.2, RY1 - 0.3, 1.2))
    emp("WP_Elevador_Secreto_Topo", ((ex0 + ex1) / 2, (ey0 + ey1) / 2, 0.0))
    emp("WP_Elevador_Secreto_Fundo", ((ex0 + ex1) / 2, (ey0 + ey1) / 2, EV_FUNDO))
    emp("INT_Elevador_Secreto", (ex1 - 0.35, ey0 + 0.6, 1.25))
    emp("WP_Subsolo_Vestibulo", ((vx0 + vx1) / 2, vy0 + 2.0, EV_FUNDO))
    zona("ZONA_Elevador_Secreto", ((ex0 + ex1) / 2, (ey0 + ey1) / 2, 1.4), (ex1 - ex0 - 0.3, ey1 - ey0 - 0.3, 2.6))


# ======================================================================= L3: salas de treinamento (casca em volta de cada estacao)
# (nome, bbox x0, y0, x1, y1, altura, entrada (x, y), direcao para dentro (dx, dy))
SALAS = [
    ("Medico", -50.5, 1.5, -20.5, 21.5, 5.0, (-20.5, 6.0), (-1, 0)),
    ("Centrifuga", -54.0, 24.0, -24.8, 48.0, 9.0, (-24.8, 30.0), (-1, 0)),
    ("RealidadeVirtual", -26.5, 30.6, -3.5, 50.0, 6.0, (-15.0, 30.6), (0, 1)),
    ("NBL", -1.0, 19.5, 32.5, 46.5, 11.0, (-1.0, 28.0), (1, 0)),
    ("ARGOS", 32.6, 1.5, 58.5, 24.5, 12.0, (32.6, 8.0), (1, 0)),
    ("Trajes", 32.6, 27.5, 60.0, 50.0, 5.0, (32.6, 37.5), (1, 0)),
]
PORTA_W, PORTA_H = 3.6, 3.4
TITULOS = {"Medico": "AVALIAÇÃO MÉDICA E PREPARO FÍSICO", "Centrifuga": "CENTRÍFUGA HUMANA", "RealidadeVirtual": "REALIDADE VIRTUAL",
           "NBL": "FLUTUABILIDADE NEUTRA", "ARGOS": "GRAVIDADE REDUZIDA · ARGOS", "Trajes": "SALA DE TRAJES"}

def salas():
    for nome, x0, y0, x1, y1, H, ent, d in SALAS:
        m = Malha(f"Sala_{nome}_Casca")
        # piso (NBL: com o buraco da piscina)
        if nome == "NBL":
            px0, py0, px1, py1 = POOL
            for a, b in (((x0, y0), (x1, py0)), ((x0, py1), (x1, y1)), ((x0, py0), (px0, py1)), ((px1, py0), (x1, py1))):
                m.caixa_ab((a[0], a[1], -0.5), (b[0], b[1], 0.0), PISO)
        else:
            m.caixa_ab((x0, y0, -0.5), (x1, y1, 0.0), PISO)
        # paredes (com a abertura da entrada) + acabamento interno: rodape azul, faixa de madeira, paineis lisos
        paredes = [((x0, y0), (x1, y0)), ((x1, y0), (x1, y1)), ((x1, y1), (x0, y1)), ((x0, y1), (x0, y0))]
        for (a, b) in paredes:
            a, b = V((*a, 0)), V((*b, 0)); dd = b - a; Lw = dd.length; u = dd.normalized()
            n_in = V((-u.y, u.x, 0))                                         # parede percorrida no sentido anti-horario: dentro = esquerda
            e = V((*ent, 0))
            na_parede = abs((e - a).cross(u).z) < 0.05 and -0.01 <= (e - a).dot(u) <= Lw + 0.01
            trechos = [(0.0, Lw)]
            if na_parede:
                t = (e - a).dot(u)
                trechos = [(0.0, t - PORTA_W / 2), (t + PORTA_W / 2, Lw)]
                c_ = a + u * t
                m.caixa((c_.x + n_in.x * 0.0, c_.y + n_in.y * 0.0, (PORTA_H + H) / 2), (abs(u.x) * PORTA_W + abs(u.y) * 0.4, abs(u.y) * PORTA_W + abs(u.x) * 0.4, H - PORTA_H), PAINEL_LISO)
                for s_ in (-1, 1):                                            # batentes da entrada
                    q = c_ + u * s_ * (PORTA_W / 2 + 0.1)
                    m.caixa((q.x, q.y, PORTA_H / 2), (abs(u.x) * 0.2 + abs(u.y) * 0.6, abs(u.y) * 0.2 + abs(u.x) * 0.6, PORTA_H), ESTRUT)
                m.caixa((c_.x, c_.y, PORTA_H + 0.1), (abs(u.x) * (PORTA_W + 0.4) + abs(u.y) * 0.6, abs(u.y) * (PORTA_W + 0.4) + abs(u.x) * 0.6, 0.2), AMARELO)
            for (t0, t1) in trechos:
                if t1 - t0 < 0.05:
                    continue
                pa, pb = a + u * t0, a + u * t1
                cx, cy = (pa.x + pb.x) / 2, (pa.y + pb.y) / 2
                sx, sy = abs(pb.x - pa.x) + abs(u.y) * 0.4, abs(pb.y - pa.y) + abs(u.x) * 0.4
                m.caixa((cx - n_in.x * 0.2, cy - n_in.y * 0.2, H / 2), (sx, sy, H), PAINEL_LISO)
                m.caixa((cx + n_in.x * 0.02, cy + n_in.y * 0.02, 0.08), (sx, sy, 0.16), AZUL)
                m.caixa((cx + n_in.x * 0.02, cy + n_in.y * 0.02, 1.2), (sx, sy, 0.3), MADEIRA_CL)
                m.caixa((cx + n_in.x * 0.02, cy + n_in.y * 0.02, H - 0.25), (sx, sy, 0.12), AZUL)
                # colisor da parede
                col(f"COL_Sala_{nome}_Parede_{len(OBJS)}", (cx - n_in.x * 0.2, cy - n_in.y * 0.2, H / 2), (sx, sy, H))
        # teto com luminarias embutidas
        m.caixa_ab((x0 - 0.4, y0 - 0.4, H), (x1 + 0.4, y1 + 0.4, H + 0.3), PAINEL_LISO)
        for i in range(int((x1 - x0) // 6)):
            for j in range(int((y1 - y0) // 6)):
                cx, cy = x0 + 3 + 6 * i, y0 + 3 + 6 * j
                m.caixa((cx, cy, H - 0.02), (1.6, 0.6, 0.04), LUZ)
        # dutos de ar no teto (2 linhas ao longo do lado maior) com bocas de insuflamento
        longo_x = (x1 - x0) >= (y1 - y0)
        for k in (0.25, 0.75):
            if longo_x:
                yy = y0 + (y1 - y0) * k
                m.cilindro((x0 + 0.5, yy, H - 0.55), (x1 - 0.5, yy, H - 0.55), 0.32, METAL, 24)
                for xx in range(int(x0) + 3, int(x1) - 2, 5):
                    m.caixa((xx, yy, H - 0.9), (0.5, 0.5, 0.12), ESTRUT)
            else:
                xx = x0 + (x1 - x0) * k
                m.cilindro((xx, y0 + 0.5, H - 0.55), (xx, y1 - 0.5, H - 0.55), 0.32, METAL, 24)
                for yy in range(int(y0) + 3, int(y1) - 2, 5):
                    m.caixa((xx, yy, H - 0.9), (0.5, 0.5, 0.12), ESTRUT)
        # faixa de seguranca zebrada no piso, a 0,6 m das paredes
        for k in range(int((x1 - x0) // 0.8)):
            for yy in (y0 + 0.6, y1 - 0.6):
                m.caixa((x0 + 0.4 + k * 0.8, yy, 0.006), (0.4, 0.12, 0.012), AMARELO)
        for k in range(int((y1 - y0) // 0.8)):
            for xx in (x0 + 0.6, x1 - 0.6):
                m.caixa((xx, y0 + 0.4 + k * 0.8, 0.006), (0.12, 0.4, 0.012), AMARELO)
        # equipamento de apoio na parede oposta a entrada: armarios + bancada com monitor + placa da sala (TMP)
        e3 = V((*ent, 0)); dv = V((d[0], d[1], 0))
        fundo = e3 + dv * ((x1 - x0) if d[0] else (y1 - y0)) - dv * 0.45
        lat = V((-dv.y, dv.x, 0))
        for k in range(4):
            q = fundo + lat * (-3.0 + k * 0.9)
            m.caixa((q.x, q.y, 1.0), (abs(lat.x) * 0.85 + abs(dv.x) * 0.55, abs(lat.y) * 0.85 + abs(dv.y) * 0.55, 2.0), AZUL if k % 2 else TECIDO)
            m.caixa((q.x - dv.x * 0.29, q.y - dv.y * 0.29, 1.15), (abs(lat.x) * 0.04 + abs(dv.x) * 0.02, abs(lat.y) * 0.04 + abs(dv.y) * 0.02, 0.3), METAL)
        qb = fundo + lat * 2.4 - dv * 0.1
        m.caixa((qb.x, qb.y, 0.45), (abs(lat.x) * 2.2 + abs(dv.x) * 0.7, abs(lat.y) * 2.2 + abs(dv.y) * 0.7, 0.9), ESCURO)
        m.caixa((qb.x, qb.y, 0.92), (abs(lat.x) * 2.3 + abs(dv.x) * 0.75, abs(lat.y) * 2.3 + abs(dv.y) * 0.75, 0.05), METAL)
        rz_ = math.atan2(-dv.x, dv.y)
        monitor(m, (qb.x - dv.x * 0.1, qb.y - dv.y * 0.1, 0.95), rz=rz_ + PI)
        qs = fundo - dv * 0.02
        m.caixa((qs.x, qs.y, H - 1.4), (abs(lat.x) * 5.0 + abs(dv.x) * 0.08, abs(lat.y) * 5.0 + abs(dv.y) * 0.08, 0.9), AZUL_ESC)
        m.texto(TITULOS[nome], (qs.x - dv.x * 0.06, qs.y - dv.y * 0.06, H - 1.4), 0.42, TXT_BRANCO, rz=math.atan2(dv.y, dv.x) + PI / 2, espessura=0.01)
        col(f"COL_Sala_{nome}_Armarios", ((fundo + lat * -1.65).x, (fundo + lat * -1.65).y, 1.0), (abs(lat.x) * 3.6 + abs(dv.x) * 0.6, abs(lat.y) * 3.6 + abs(dv.y) * 0.6, 2.0))
        col(f"COL_Sala_{nome}_Bancada", (qb.x, qb.y, 0.5), (abs(lat.x) * 2.3 + abs(dv.x) * 0.8, abs(lat.y) * 2.3 + abs(dv.y) * 0.8, 1.0))
        fim(m, 1.0)
        if nome != "NBL":
            col(f"COL_Sala_{nome}_Piso", ((x0 + x1) / 2, (y0 + y1) / 2, -0.25), (x1 - x0, y1 - y0, 0.5))
        else:
            px0, py0, px1, py1 = POOL
            for k, (a, b) in enumerate((((x0, y0), (x1, py0)), ((x0, py1), (x1, y1)), ((x0, py0), (px0, py1)), ((px1, py0), (x1, py1)))):
                col(f"COL_Sala_NBL_Piso_{k}", ((a[0] + b[0]) / 2, (a[1] + b[1]) / 2, -0.25), (b[0] - a[0], b[1] - a[1], 0.5))
        emp(f"SALA_{nome}_Entrada", (ent[0], ent[1], 0.0), rz=math.atan2(d[1], d[0]))

def marcadores_estacoes():
    for i, (nome, npc, cons, (zc, zs)) in enumerate(ESTACOES):
        if i == 0:
            continue                                                   # recepcao ja tem os marcadores do L1
        emp(f"EST_{i}_{nome}_NPC", (*npc, 0.0))
        emp(f"EST_{i}_{nome}_Console", (*cons, 1.0))
        zona(f"ZONA_EST_{i}_{nome}", zc, zs)


def marcadores_l1():
    emp("SPAWN_Player", (0.0, -43.0, -0.08), rz=0.0)
    emp("NPC_Inicio", (1.6, -10.0, -0.08), rz=PI)
    for k, (x, y) in enumerate([(0, -10), (0, -2), (0, 4), (2.6, 10.0)]):
        emp(f"WP_NPC_{k:02d}", (x, y, 0.0))
    zona("ZONA_Porta_Entrada", (0.0, 0.0, 1.6), (6.0, 7.0, 3.2))


# ======================================================================= montar
# L1: o hangar sai; o predio da recepcao ocupa x -15..15, y 0..20 (+ poco do elevador ate y 24.6).
# As funcoes estacao_* continuam aqui para serem reaproveitadas nas salas do subsolo (L3).
HX0, HX1, HY0, HY1 = RX0, RX1, RY0, 24.6
terreno()
predio_recepcao()
exterior()
marcadores_l1()
# L3: estacoes de treino (as mesmas do hangar) + casca de cada sala; o Unity encaixa cada sala na sua porta do corredor
estacao_medica()
estacao_centrifuga()
estacao_vr()
estacao_nbl()
estacao_argos()
estacao_trajes()
salas()
marcadores_estacoes()

malhas = [o for o in OBJS if o.type == "MESH" and not o.name.startswith(("COL_", "ZONA_"))]
print(f"[CentroTreinamento] {len(malhas)} malhas, {contar_tris(malhas)} triangulos; "
      f"{sum(1 for o in OBJS if o.name.startswith('COL_'))} colisores; {sum(1 for o in OBJS if o.type == 'EMPTY')} marcadores")
for o in sorted(malhas, key=lambda o: -contar_tris([o]))[:12]:
    print(f"   {o.name:32s} {contar_tris([o]):7d} tris  {len(o.data.materials)} mats")

if PREVIEW:
    os.makedirs(PREVIEW, exist_ok=True)
    for o in OBJS:
        if o.name.startswith(("COL_", "ZONA_")):
            o.hide_render = True
    preparar_render((1280, 720))
    vistas = {
        "L1_recepcao": ((-1.5, 1.2, 1.7), (2.0, 19.0, 2.6), 20),
        "L1_fachada": ((0, -40, 1.7), (0, 0, 4), 28),
        "01_aerea": ((-70, -110, 95), (5, 20, 0), 24),
        "02_spawn": ((0, -50, 1.7), (0, 0, 5), 24),
        "03_entrada": ((0, -2, 1.7), (0, 25, 3), 18),
        "04_hangar_alto": ((-58, 2, 12), (10, 35, 0), 16),
        "05_medico": ((-24, 4, 2.4), (-42, 14, 0.8), 20),
        "06_centrifuga": ((-25, 28, 3.5), (-42, 36, 2), 20),
        "07_vr": ((-15, 32, 1.8), (-15, 48, 3), 18),
        "08_nbl": ((-1, 20, 4.5), (16, 34, -2.5), 18),
        "09_argos": ((32, 3, 3.5), (48, 14, 1.5), 18),
        "10_trajes": ((34.5, 29, 2.2), (48, 44, 1.2), 18),
        "11_saida": ((44, 75, 2.0), (44, 48, 3), 24),
    }
    so = [a for a in args if a.startswith("--vista=")]
    for nome, (pos, alvo, lente) in vistas.items():
        if so and not any(nome.startswith(s.split("=")[1]) for s in so):
            continue
        renderizar(os.path.join(PREVIEW, nome + ".png"), pos, alvo, lente)
    remover_preview()

if SAIDA_FBX and not SO_PREVIEW:
    exportar_fbx(SAIDA_FBX, OBJS)
    letreiros_json(os.path.join(os.path.dirname(os.path.abspath(SAIDA_FBX)), "..", "Dados", "Letreiros_Centro.json"), "TXT")
if SAIDA_BLEND and not SO_PREVIEW:
    os.makedirs(os.path.dirname(os.path.abspath(SAIDA_BLEND)), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(SAIDA_BLEND), compress=True)
    print("[CentroTreinamento] BLEND:", SAIDA_BLEND)
