"""
NPC instrutora do Centro de Treinamento (macacao de voo azul da NASA), em pecas articuladas:
  NPC_Instrutora (raiz nos pes) > Corpo, Cabeca (pivo no pescoco), Braco_E/D (pivo no ombro), Perna_E/D (pivo no quadril)
A caminhada e o olhar sao feitos por script no Unity (NpcInstrutor.cs) — sem esqueleto, barato e robusto.
Frente do personagem = -Y no Blender = +Z (forward) no Unity.

  blender -b --factory-startup --python gerar_npc.py -- --fbx ../Modelos/NPC_Instrutora.fbx --preview <pasta>
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from mathutils import Vector as V, Matrix
from astrolab_lib import Malha, material, vazio, limpar_cena, colecao, preparar_render, renderizar, remover_preview, exportar_fbx, contar_tris

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
def arg(n):
    return args[args.index(n) + 1] if n in args else None

limpar_cena()
COL = colecao("NPC")
PI = math.pi
TEX = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Texturas", "T_Logo_NASA.png"))

MACACAO = material("NPC_Macacao", "#2F5BB7")
MACACAO_ESC = material("NPC_Macacao_Escuro", "#22428A")
PELE = material("NPC_Pele", "#C68B63")
CABELO = material("NPC_Cabelo", "#3B2A22")
BOTA = material("NPC_Bota", "#2B2B33")
OLHO = material("NPC_Olho", "#1C1A3D")
BRANCO = material("CT_Branco", "#F8F5EE")
TXT = material("CT_Texto_Escuro", "#1C1A3D")
VERDE = material("CT_Verde_Bandeira", "#1E9A4A")
AMARELO = material("CT_Amarelo", "#FFC23A")
METAL = material("CT_Metal", "#B4C2D9")
LOGO = material("CT_Logo_NASA", "#FFFFFF", alpha=0.99, imagem=TEX) if os.path.exists(TEX) else BRANCO

raiz = None
pecas = []

def peca(m, pai=raiz):
    # sem hierarquia no FBX (empties pais ganham escala 100 na conversao de unidades): o Unity cria a raiz
    o = m.criar(COL, 1.0)
    for x in (o, getattr(m, "irmao_transp", None)):
        if x:
            pecas.append(x)
    return o

# ---------------------------------------------------------------- corpo (tronco + quadril + pescoco)
c = Malha("Corpo", origem=(0, 0, 0.92))
c.chanfro = 0.006
Mt = Matrix.Diagonal((1.0, 0.66, 1.0, 1))
c.torno([(0.0, 0.82), (0.155, 0.84), (0.175, 0.92), (0.165, 1.02), (0.158, 1.1), (0.18, 1.24), (0.205, 1.34),
         (0.2, 1.4), (0.15, 1.46), (0.07, 1.49), (0.0, 1.495)], MACACAO, 20, M=Mt)
c.torus((0, 0, 0.99), 0.168, 0.022, MACACAO_ESC, 20, 6, M=Mt)                 # cinto
c.caixa((0, -0.112, 0.99), (0.07, 0.02, 0.05), METAL)                          # fivela
c.torus((0, 0, 1.465), 0.07, 0.022, MACACAO_ESC, 16, 6, M=Matrix.Diagonal((1.0, 0.9, 1.0, 1)))   # gola
c.caixa((0, -0.128, 1.18), (0.012, 0.01, 0.42), MACACAO_ESC)                   # ziper
c.caixa((-0.09, -0.135, 1.31), (0.11, 0.012, 0.075), BRANCO)                   # plaqueta de nome
c.texto("LIMA", (-0.09, -0.143, 1.31), 0.035, TXT, espessura=0.002, malha=True)
c.painel_tex((0.095, -0.138, 1.31), 0.13, 0.13, (0, -1, 0), LOGO)              # meatball no peito
for s in (-1, 1):                                                              # bolsos das pernas (na calca)
    c.caixa((s * 0.17, -0.02, 0.98), (0.03, 0.09, 0.1), MACACAO_ESC)
peca(c)

# ---------------------------------------------------------------- cabeca
h = Malha("Cabeca", origem=(0, 0, 1.5))
h.chanfro = 0
HZ = 1.705
# pescoco faz parte da cabeca: entra ~4 cm no tronco (escondido pela gola) e ~5 cm dentro da cabeca,
# entao a juncao nao abre quando ela vira/inclina a cabeca
h.cilindro((0, 0, 1.43), (0, 0, 1.6), 0.058, PELE, 16, r2=0.052)
h.esfera((0, 0, HZ), 0.145, PELE, 18, 12, escala=(0.9, 0.96, 1.06))
h.esfera((0, 0.03, HZ + 0.03), 0.148, CABELO, 18, 12, escala=(0.95, 0.97, 1.02))     # cabelo (atras e em cima)
h.esfera((-0.05, -0.085, HZ + 0.085), 0.075, CABELO, 12, 8, escala=(1.3, 0.7, 0.55))  # franja de lado
for s in (-1, 1):
    h.esfera((s * 0.115, 0.03, HZ - 0.01), 0.06, CABELO, 10, 8, escala=(0.5, 1.0, 1.4))   # laterais
h.esfera((0, 0.15, HZ + 0.02), 0.055, CABELO, 10, 8)                                    # prendedor do rabo
h.cilindro((0, 0.17, HZ + 0.0), (0, 0.19, HZ - 0.17), 0.04, CABELO, 10, r2=0.022)       # rabo de cavalo
for s in (-1, 1):
    h.esfera((s * 0.047, -0.118, HZ - 0.005), 0.026, BRANCO, 10, 8, escala=(1, 0.4, 1.25))
    h.esfera((s * 0.045, -0.127, HZ - 0.008), 0.015, OLHO, 8, 6, escala=(1, 0.5, 1.3))
    h.caixa((s * 0.05, -0.122, HZ + 0.04), (0.042, 0.012, 0.009), CABELO, ry=s * 0.18)
    h.esfera((s * 0.128, 0.0, HZ - 0.01), 0.022, PELE, 8, 6, escala=(0.6, 1, 1.3))
h.esfera((0, -0.137, HZ - 0.045), 0.015, PELE, 8, 6, escala=(0.8, 1.0, 1.5))           # nariz
Mb = Matrix.Translation(V((0, -0.128, HZ - 0.075))) @ Matrix.Rotation(PI / 2, 4, "X") @ Matrix.Rotation(math.radians(205), 4, "Z")
h.torus((0, 0, 0), 0.03, 0.0055, material("NPC_Boca", "#8A4A3A"), 10, 4, M=Mb, arco=130)   # sorriso
peca(h)

# ---------------------------------------------------------------- bracos (pivo no ombro)
for s, lado in ((-1, "E"), (1, "D")):
    om = V((s * 0.205, 0, 1.39))
    b = Malha(f"Braco_{lado}", origem=om)
    co = V((s * 0.235, 0.005, 1.13)); pu = V((s * 0.25, -0.03, 0.91))
    b.esfera(om, 0.062, MACACAO, 12, 8)
    b.cilindro(om, co, 0.058, MACACAO, 12, r2=0.052)
    b.esfera(co, 0.052, MACACAO, 10, 6)
    b.cilindro(co, pu + V((0, 0, 0.03)), 0.05, MACACAO, 12, r2=0.044)
    b.cilindro(pu + V((0, 0, 0.035)), pu + V((0, 0, 0.005)), 0.046, MACACAO_ESC, 12)   # punho da manga
    b.esfera(pu - V((0, 0, 0.05)), 0.056, PELE, 10, 8, escala=(0.7, 1.0, 1.3))        # mao
    b.esfera(pu - V((0, 0.045, 0.035)), 0.022, PELE, 6, 4, escala=(1, 1, 1.6))          # polegar
    if s < 0:                                                                          # bandeira do Brasil no ombro esq.
        n = V((-1, 0, 0))
        b.caixa(om + V((-0.07, 0, -0.07)), (0.008, 0.075, 0.05), VERDE)
        b.caixa(om + V((-0.075, 0, -0.07)), (0.004, 0.045, 0.03), AMARELO, rx=0.0)
    peca(b)

# ---------------------------------------------------------------- pernas (pivo no quadril)
for s, lado in ((-1, "E"), (1, "D")):
    qd = V((s * 0.095, 0, 0.9))
    p = Malha(f"Perna_{lado}", origem=qd)
    jo = V((s * 0.1, -0.01, 0.5)); to = V((s * 0.1, 0.0, 0.13))
    p.esfera(qd, 0.085, MACACAO, 12, 8)
    p.cilindro(qd, jo, 0.08, MACACAO, 12, r2=0.066)
    p.esfera(jo, 0.066, MACACAO, 10, 6)
    p.cilindro(jo, to, 0.064, MACACAO, 12, r2=0.055)
    p.esfera(V((s * 0.1, -0.06, 0.07)), 0.07, BOTA, 12, 8, escala=(0.85, 1.6, 0.95))
    p.cilindro(V((s * 0.1, 0.0, 0.1)), V((s * 0.1, 0.0, 0.2)), 0.062, BOTA, 12)
    p.caixa(V((s * 0.1, -0.05, 0.012)), (0.12, 0.25, 0.024), material("CT_Borracha", "#33364A"))
    peca(p)

print(f"[NPC] {len(pecas)} pecas, {contar_tris(pecas)} triangulos")

prev = arg("--preview")
if prev:
    os.makedirs(prev, exist_ok=True)
    preparar_render((900, 900), mundo="#C9D6EA")
    renderizar(os.path.join(prev, "npc_frente.png"), (0.9, -2.6, 1.3), (0, 0, 1.0), 50)
    renderizar(os.path.join(prev, "npc_rosto.png"), (0.25, -0.75, 1.65), (0, 0, 1.6), 60)
    remover_preview()
fbx = arg("--fbx")
if fbx:
    exportar_fbx(fbx, pecas)
