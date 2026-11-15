"""
Maos em primeira pessoa do Centro de Treinamento (cartoon low-poly), 3 variantes:
  Mao_Nua_E/D      pele + manga do macacao de voo azul (spawn ate a VR)
  Luva_Treino_E/D  luva de treino (NBL, ARGOS) + manga do traje de treino
  Luva_Traje_E/D   luva do traje da missao (depois da Sala de Trajes) — combina com o traje do P3
+ Marcador_Objetivo (losango chanfrado com anel) usado sobre os paineis.

Coordenadas: as maos sao modeladas JA na posicao local da camera do jogador (mesma pose das maos do time,
MaosEScanner: mao em (+-0.255, -0.258, 0.45) no Unity). U(x, y, z) converte Unity-camera -> Blender.
Sem hierarquia no FBX (o Unity monta os prefabs).

  blender -b --factory-startup --python gerar_maos.py -- --fbx ../Modelos/Maos_Centro.fbx --preview <pasta>
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy
from mathutils import Vector as V, Matrix
from astrolab_lib import Malha, material, limpar_cena, colecao, preparar_render, renderizar, remover_preview, exportar_fbx, contar_tris

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
def arg(n):
    return args[args.index(n) + 1] if n in args else None

limpar_cena()
COL = colecao("Maos")
PI = math.pi

def U(x, y, z):
    """Unity (camera local) -> Blender"""
    return V((-x, -z, y))

# Matriz que leva o "espaco da mao" (Unity: x direita, y cima/dorso, z frente/dedos) para o Blender, na pose final.
def pose(lado):
    s = -1 if lado == "E" else 1
    base = Matrix(((-1, 0, 0, 0), (0, 0, -1, 0), (0, 1, 0, 0), (0, 0, 0, 1)))      # Unity -> Blender (linear)
    pos = Matrix.Translation(V((s * 0.215, -0.235, 0.36)))                         # punho um pouco atras da palma
    rot = Matrix.Rotation(math.radians(-s * 14), 4, "Y") @ Matrix.Rotation(math.radians(-34), 4, "X") @ Matrix.Rotation(math.radians(s * 8), 4, "Z")
    return base @ pos @ rot @ Matrix.Scale(1.32, 4)

PELE = material("MV_Pele", "#D9A07A")
UNHA = material("MV_Unha", "#F0C7B0")
MANGA = material("MV_Manga_Macacao", "#2F5BB7")
PUNHO = material("MV_Punho_Macacao", "#22428A")
BOTAO = material("MV_Metal", "#B4C2D9")
LT = material("MV_Luva_Treino", "#DCE5ED")
LT_J = material("MV_Luva_Treino_Junta", "#2F3A4F")
LT_P = material("MV_Luva_Treino_Placa", "#9FB4CC")
MANGA_T = material("MV_Manga_Treino", "#E6E9EF")
LX = material("MV_Luva_Traje", "#F1F0EA")
LX_J = material("MV_Luva_Traje_Junta", "#3E4C78")
LX_P = material("MV_Luva_Traje_Protecao", "#C9D3E0")
LX_F = material("MV_Luva_Traje_Faixa", "#2456B8")
MANGA_X = material("MV_Manga_Traje", "#F4F2EC")

objs = []

def mao(nome, lado, estilo):
    """estilo: 'nua' | 'treino' | 'traje'"""
    s = 1 if lado == "E" else -1           # polegar do lado de dentro (a conversao Unity->Blender espelha o X)
    M = pose(lado)
    m = Malha(nome)
    luva = estilo != "nua"
    k = 1.32 if luva else 1.0              # luvas engrossam dedos e palma
    cor = {"nua": PELE, "treino": LT, "traje": LX}[estilo]
    junta = {"nua": PELE, "treino": LT_J, "traje": LX_J}[estilo]
    P = lambda x, y, z: M @ V((x, y, z))

    # palma: elipsoide achatado + dorso levemente abaulado
    Mp = M @ Matrix.Translation(V((0, 0, 0.052))) @ Matrix.Diagonal((0.047 * (1.06 if luva else 1), 0.021 * k, 0.056, 1))
    m.esfera((0, 0, 0), 1.0, cor, 14, 10, M=Mp)
    if luva:                                                     # placa protetora no dorso
        Mq = M @ Matrix.Translation(V((0, 0.02 * k, 0.05))) @ Matrix.Diagonal((0.036, 0.008, 0.04, 1))
        m.esfera((0, 0, 0), 1.0, LT_P if estilo == "treino" else LX_P, 10, 6, M=Mq)

    # dedos: indicador, medio, anelar, minimo (do polegar para fora)
    dedos = [(0.030, 1.0, 4), (0.010, 1.08, 1), (-0.010, 1.0, -3), (-0.029, 0.82, -8)]
    for i, (dx, comp, abre) in enumerate(dedos):
        x0 = s * dx
        base = V((x0, 0.002, 0.1))
        ang_y = math.radians(-s * abre * 0.6)
        dirx = Matrix.Rotation(ang_y, 3, "Y")
        segs = [(0.044 * comp, 7), (0.029 * comp, 14), (0.022 * comp, 12)]        # (comprimento, dobra em graus)
        r = 0.0098 * k * (0.92 if i == 3 else 1.0)
        p = base
        dobra = 3.0
        pts = [p]
        for L, d in segs:
            dobra += d
            dvec = dirx @ (Matrix.Rotation(math.radians(dobra), 3, "X") @ V((0, 0, 1)))
            dvec = V((dvec.x, -abs(dvec.y) if dvec.y < 0 else -dvec.y, dvec.z))     # dobra para baixo (palma)
            q = p + dvec * L
            pts.append(q)
            p = q
        for j in range(3):
            rr0, rr1 = r * (1 - 0.08 * j), r * (1 - 0.08 * (j + 1))
            m.cilindro(P(*pts[j]), P(*pts[j + 1]), rr0, cor, 8, r2=rr1, tampas=False)
            m.esfera(P(*pts[j]), rr0 * (1.12 if luva else 1.03), junta if (luva and j > 0) else cor, 8, 6)
        m.esfera(P(*pts[3]), r * 0.76, cor, 8, 6)
        if not luva:                                                               # unha
            t = pts[3] + (pts[3] - pts[2]).normalized() * -0.006
            Mu = Matrix.Translation(P(*(t + V((0, r * 0.62, 0))))) @ (M.to_3x3().to_4x4()) @ Matrix.Diagonal((r * 0.75, r * 0.25, r * 0.9, 1))
            m.esfera((0, 0, 0), 1.0, UNHA, 8, 4, M=Mu)
        elif estilo == "traje":                                                    # protecao nas pontas (borracha)
            m.esfera(P(*pts[3]), r * 0.82, LX_J, 8, 6, escala=(1, 0.8, 1))

    # polegar: sai do lado de dentro da palma, aponta para frente e para dentro
    b0 = V((s * 0.042, -0.004, 0.03))
    b1 = b0 + V((s * 0.018, -0.006, 0.026))
    b2 = b1 + V((s * 0.006, -0.004, 0.022))
    b3 = b2 + V((s * 0.0, -0.002, 0.017))
    rp = 0.0128 * k
    m.esfera(P(*b0), rp * 1.25, cor, 10, 8)
    for a, b, ra, rb in ((b0, b1, rp * 1.2, rp), (b1, b2, rp, rp * 0.92), (b2, b3, rp * 0.92, rp * 0.84)):
        m.cilindro(P(*a), P(*b), ra, cor, 8, r2=rb, tampas=False)
        m.esfera(P(*b), rb * (1.1 if luva else 1.0), junta if luva else cor, 8, 6)
    if not luva:
        Mu = Matrix.Translation(P(*(b3 + V((0, rp * 0.6, -0.004))))) @ M.to_3x3().to_4x4() @ Matrix.Diagonal((rp * 0.7, rp * 0.25, rp * 0.85, 1))
        m.esfera((0, 0, 0), 1.0, UNHA, 8, 4, M=Mu)

    # punho e manga
    if estilo == "nua":
        m.cilindro(P(0, 0, 0.012), P(0, 0, -0.03), 0.03, PELE, 12, r2=0.029)
        m.cilindro(P(0, -0.002, -0.022), P(0, -0.004, -0.052), 0.044, PUNHO, 14)      # punho da manga
        m.cilindro(P(0, -0.004, -0.052), P(0, -0.02, -0.34), 0.046, MANGA, 14, r2=0.054, tampas=False)
    else:
        f = LT_J if estilo == "treino" else LX_F
        m.cilindro(P(0, 0, 0.015), P(0, 0, -0.04), 0.036 * 1.08, cor, 12, r2=0.038)   # punho da luva
        m.torus((0, 0, 0), 0.042, 0.008, f, 16, 6, M=M @ Matrix.Translation(V((0, 0, -0.035))))
        m.cilindro(P(0, 0, -0.03), P(0, 0, -0.06), 0.047, BOTAO if estilo == "traje" else LT_J, 16)  # anel/rolamento
        mg = MANGA_T if estilo == "treino" else MANGA_X
        m.cilindro(P(0, -0.002, -0.06), P(0, -0.02, -0.34), 0.054, mg, 14, r2=0.06, tampas=False)
        if estilo == "traje":
            m.cilindro(P(0, -0.008, -0.12), P(0, -0.009, -0.135), 0.0565, LX_F, 14)  # faixa azul no antebraco
    o = m.criar(COL, 1.0)
    for p in o.data.polygons:                  # tudo suave (cartoon), exceto faces muito pequenas de tampa
        p.use_smooth = True
    objs.append(o)
    return o

for estilo, nome in (("nua", "Mao_Nua"), ("treino", "Luva_Treino"), ("traje", "Luva_Traje")):
    for lado in ("E", "D"):
        mao(f"{nome}_{lado}", lado, estilo)

# ---------------------------------------------------------------- marcador de objetivo (losango chanfrado + anel)
AMB = material("CT_Luz_Ambar", "#FFB45C", emissao=4.0)
mk = Malha("Marcador_Objetivo", origem=(0, 0, 0))
mk.torno([(0.0, -0.36), (0.05, -0.31), (0.2, -0.04), (0.22, 0.0), (0.2, 0.04), (0.05, 0.31), (0.0, 0.36)], AMB, 8)
mk.torus((0, 0, 0), 0.32, 0.018, AMB, 24, 6)                     # anel horizontal em volta do losango
marc = mk.criar(COL, 1.0)
for p in marc.data.polygons:
    p.use_smooth = False
objs.append(marc)

for o in objs:
    print(f"   {o.name:22s} {contar_tris([o]):6d} tris")

prev = arg("--preview")
if prev:
    os.makedirs(prev, exist_ok=True)
    preparar_render((960, 540), mundo="#B8C7DE")
    marc.hide_render = True
    # camera no lugar dos olhos do jogador (origem), olhando para frente (Unity +Z = Blender -Y)
    for estilo in ("Mao_Nua", "Luva_Treino", "Luva_Traje"):
        for o in objs:
            o.hide_render = not o.name.startswith(estilo)
        cam_fov = 75.0
        lente = 18.0 / math.tan(math.radians(cam_fov) / 2) * (9 / 16)
        renderizar(os.path.join(prev, f"maos_{estilo}.png"), (0, 0, 0), (0, -1, 0), lente)
    for o in objs:
        o.hide_render = not o.name.startswith("Mao_Nua_D")
    renderizar(os.path.join(prev, "maos_detalhe.png"), U(0.12, 0.02, 0.35), U(0.24, -0.27, 0.5), 50)
    remover_preview()
fbx = arg("--fbx")
if fbx:
    for o in objs:
        o.hide_render = False
    exportar_fbx(fbx, objs)
