"""
Instrutora Lima gerada com MPFB2 (MakeHuman para Blender; assets do corpo CC0) + acabamento toon do AstroLab.

  blender -b --python gerar_lima_mpfb.py -- [--fbx ../Modelos/NPC_Lima.fbx] [--preview <pasta>]

- corpo: MPFB, mulher adulta, proporcoes levemente estilizadas (cabeca ~7% maior, olhos maiores), rig "standard"
- uniforme pintado por regiao de osso (pele: cabeca/maos; macacao azul; botas), cabelo/olhos/sobrancelhas/
  gola/cinto/emblema/cracha modelados aqui e presos ao esqueleto (pesos 100% no osso certo)
- alvo ~12k triangulos; materiais com nomes NPC_* (receitas toon em EstiloCentro.cs)
Requer a extensao MPFB instalada no Blender 5.2 (user_default).
"""
import sys, os, math
sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import bpy, bmesh
from mathutils import Vector as V, Matrix
from astrolab_lib import Malha, material, limpar_cena, preparar_render, renderizar, remover_preview, exportar_fbx, contar_tris

args = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
def arg(n):
    return args[args.index(n) + 1] if n in args else None

limpar_cena()
bpy.ops.preferences.addon_enable(module="bl_ext.user_default.mpfb")
from bl_ext.user_default.mpfb.services.humanservice import HumanService
from bl_ext.user_default.mpfb.services.targetservice import TargetService

PI = math.pi
PELE = material("NPC_Pele", "#B9805A")
MACACAO = material("NPC_Macacao", "#2F5BB7")
MACACAO_ESC = material("NPC_Macacao_Escuro", "#22428A")
CABELO = material("NPC_Cabelo", "#2E2019")
BOTA = material("NPC_Bota", "#2B2B33")
OLHO = material("NPC_Olho", "#2A1A12")
BRANCO = material("CT_Branco", "#F8F5EE")
BOCA = material("NPC_Boca", "#8A4A3A")
METAL = material("CT_Metal", "#B4C2D9")
TXT = material("CT_Texto_Escuro", "#1C1A3D")
VERDE = material("CT_Verde_Bandeira", "#1E9A4A")
AMARELO = material("CT_Amarelo", "#FFC23A")
TEX = os.path.normpath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Texturas", "T_Logo_NASA.png"))
LOGO = material("CT_Logo_NASA", "#FFFFFF", alpha=0.99, imagem=TEX)
LABIO = material("NPC_Labio", "#9C5446")
CABELO_CL = material("NPC_Cabelo_Brilho", "#4A3428")
SOBRANCELHA = material("NPC_Sobrancelha", "#1E140F")

# ---------------------------------------------------------------- 1. corpo MPFB
macro = TargetService.get_default_macro_info_dict()
macro.update(gender=0.0, age=0.5, muscle=0.5, weight=0.42, proportions=0.8, height=0.55, cupsize=0.4, firmness=0.6)
macro["race"] = {"african": 0.45, "caucasian": 0.35, "asian": 0.2}
h = HumanService.create_human(mask_helpers=True, detailed_helpers=True, feet_on_ground=True, scale=0.1, macro_detail_dict=macro)
h.name = "Lima_Corpo"
for alvo, v in (("head-scale-vert-incr", 0.32), ("head-scale-horiz-incr", 0.28), ("head-scale-depth-incr", 0.22),
                ("l-eye-scale-incr", 0.45), ("r-eye-scale-incr", 0.45), ("neck-scale-horiz-decr", 0.15)):
    try:
        TargetService.set_target_value(h, alvo, v)
    except Exception as e:
        print("[Lima] alvo ignorado", alvo, e)

# olhos: posicao/raio dos helpers antes de apaga-los
def centro_grupo(nome):
    gi = h.vertex_groups[nome].index
    for m_ in h.modifiers:
        if m_.type == "MASK":
            m_.show_viewport = False                    # a mascara esconde justamente os helpers
    dg = bpy.context.evaluated_depsgraph_get()
    me = h.evaluated_get(dg).to_mesh()
    pts = [h.matrix_world @ v.co for v in me.vertices if any(g.group == gi and g.weight > 0.5 for g in v.groups)]
    h.evaluated_get(dg).to_mesh_clear()
    c = sum(pts, V()) / len(pts)
    r = max((p - c).length for p in pts)
    return c, r
olho_e, r_olho = centro_grupo("helper-l-eye")
boca_c, _ = centro_grupo("helper-upper-teeth")
olho_d, _ = centro_grupo("helper-r-eye")
# o corpo do MPFB ja nasce de frente para -Y (= +Z no Unity)

rig = HumanService.add_builtin_rig(h, "game_engine")
bpy.context.view_layer.objects.active = h
TargetService.bake_targets(h)
for mod in list(h.modifiers):
    if mod.type == "MASK":
        h.modifiers.remove(mod)
bm = bmesh.new(); bm.from_mesh(h.data); bm.verts.ensure_lookup_table()
dl = bm.verts.layers.deform.active
helpers = {h.vertex_groups[n].index for n in ("HelperGeometry", "JointCubes") if n in h.vertex_groups}
bmesh.ops.delete(bm, geom=[v for v in bm.verts if any(i in helpers for i in v[dl].keys())], context="VERTS")
bm.to_mesh(h.data); bm.free()
for g in list(h.vertex_groups):                                  # so ficam os grupos dos ossos
    if g.name not in rig.data.bones:
        h.vertex_groups.remove(g)

# ---------------------------------------------------------------- 2. regioes por osso -> materiais
for m in (PELE, MACACAO, BOTA):
    h.data.materials.append(m)
ossos = {g.index: g.name for g in h.vertex_groups}
def regiao(nome):
    n = nome.lower()
    if any(k in n for k in ("head", "neck", "jaw", "eye", "lip", "tongue", "teeth", "nose", "cheek", "brow", "ear", "chin", "temple", "oris", "levator", "risorius", "special")):
        return 0
    if any(k in n for k in ("wrist", "finger", "metacarpal", "thumb", "hand")):
        return 0
    if any(k in n for k in ("foot", "toe")):
        return 2
    return 1
me = h.data
for p in me.polygons:
    soma = {}
    for vi in p.vertices:
        for g in me.vertices[vi].groups:
            if g.group in ossos:
                soma[g.group] = soma.get(g.group, 0) + g.weight
    if soma:
        p.material_index = regiao(ossos[max(soma, key=soma.get)])
    p.use_smooth = True

h.data.materials.append(LABIO)
i_lab = len(h.data.materials) - 1
for p in me.polygons:
    c = h.matrix_world @ p.center
    if False:                                                       # labios agora sao modelados (pintura ficava serrilhada)
        p.material_index = i_lab

Z_GOLA = (rig.matrix_world @ rig.data.bones["neck_01"].head_local).z + 0.012
for p in me.polygons:
    c = h.matrix_world @ p.center
    if abs(c.x) < 0.14 and c.z < Z_GOLA + 0.05:
        if p.material_index == 0 and c.z < Z_GOLA and c.z > Z_GOLA - 0.25:
            p.material_index = 1                                       # pescoco/peito abaixo do decote: tecido
        elif p.material_index == 1 and c.z >= Z_GOLA:
            p.material_index = 0                                       # acima do decote: pele

# macacao de tecido: infla a regiao do uniforme e suaviza (some a anatomia, fica com cara de roupa folgada)
gu = h.vertex_groups.new(name="uniforme")
idx_u = {vi for p in me.polygons if p.material_index in (1, 2) for vi in p.vertices}
borda = {vi for p in me.polygons if p.material_index == 0 for vi in p.vertices}
gu.add(list(idx_u - borda), 1.0, "REPLACE")
gu.add(list(idx_u & borda), 0.5, "REPLACE")                        # borda pela metade: o tecido nao "abre" na pele
bpy.ops.object.select_all(action="DESELECT"); h.select_set(True); bpy.context.view_layer.objects.active = h
for nome, tipo, cfg in (("Alisar", "SMOOTH", dict(factor=0.85, iterations=18)), ("Folga", "DISPLACE", dict(strength=0.016, direction="NORMAL"))):
    mo = h.modifiers.new(nome, tipo)
    for k, v in cfg.items():
        setattr(mo, k, v)
    mo.vertex_group = "uniforme"
    bpy.ops.object.modifier_move_to_index(modifier=nome, index=0 if tipo == "SMOOTH" else 1)
for nome in ("Alisar", "Folga"):
    bpy.ops.object.modifier_apply(modifier=nome)
h.vertex_groups.remove(h.vertex_groups["uniforme"])   # por nome: a referencia antiga fica invalida apos aplicar modificadores
gu2 = h.vertex_groups.new(name="so_uniforme"); gu2.add(list(idx_u - borda), 1.0, "REPLACE")
dec = h.modifiers.new("Decimate", "DECIMATE"); dec.ratio = 0.5; dec.vertex_group = "so_uniforme"   # rosto e maos em resolucao cheia
bpy.ops.object.modifier_move_to_index(modifier=dec.name, index=0)
bpy.ops.object.modifier_apply(modifier=dec.name)
h.vertex_groups.remove(h.vertex_groups["so_uniforme"])

# ---------------------------------------------------------------- 3. pecas toon presas aos ossos
def osso(nome_parcial):
    for b in rig.data.bones:
        if b.name.lower() == nome_parcial:
            return b.name
    for b in rig.data.bones:
        if nome_parcial in b.name.lower():
            return b.name
    return rig.data.bones[0].name

OS_CABECA, OS_PEITO, OS_QUADRIL = osso("head"), osso("spine_03"), osso("pelvis")
cab = [h.matrix_world @ v.co for v in me.vertices if any(ossos.get(g.group, "") == OS_CABECA and g.weight > 0.6 for g in v.groups)]
cmin = V((min(p.x for p in cab), min(p.y for p in cab), min(p.z for p in cab)))
cmax = V((max(p.x for p in cab), max(p.y for p in cab), max(p.z for p in cab)))
cc = (cmin + cmax) / 2
larg, prof, alt = cmax.x - cmin.x, cmax.y - cmin.y, cmax.z - cmin.z
print(f"[Lima] cabeca {larg:.3f} x {prof:.3f} x {alt:.3f} m, centro {tuple(round(x, 3) for x in cc)}")

pecas = []
VW = [h.matrix_world @ v.co for v in me.vertices]
def sup_frente(x, z, tol=0.02):
    """y da superficie da frente do corpo em (x, z)."""
    c = [p.y for p in VW if abs(p.x - x) < tol and abs(p.z - z) < tol]
    return min(c) if c else None
def lado(y, z, sinal, tol=0.02, xmax=0.35):
    c = [p.x for p in VW if abs(p.y - y) < tol and abs(p.z - z) < tol and 0 < sinal * p.x < xmax]
    return (max(c) if sinal > 0 else min(c)) if c else None
def peca(m, osso_nome):
    o = m.criar(None, 1.0)
    o.vertex_groups.new(name=osso_nome).add(list(range(len(o.data.vertices))), 1.0, "REPLACE")
    pecas.append(o)
    return o

# cabelo: calota que contorna o cranio (cortada na testa/rosto) + coque/rabo de cavalo
c = Malha("Lima_Cabelo"); c.chanfro = 0
topo = V((cc.x, cc.y + prof * 0.06, cc.z + alt * 0.08))
c.esfera(topo, 1.0, CABELO, 28, 18, escala=(larg * 0.53, prof * 0.6, alt * 0.52))
corte = [v for v in c.bm.verts if v.co.y < cc.y - prof * 0.05 and v.co.z < cc.z + alt * 0.34]   # testa livre: as mechas fazem a linha do cabelo
bmesh.ops.delete(c.bm, geom=corte, context="VERTS")
corte2 = [v for v in c.bm.verts if v.co.z < cc.z - alt * 0.12 and v.co.y < cc.y + prof * 0.1]
bmesh.ops.delete(c.bm, geom=corte2, context="VERTS")
import random as _r
rr = _r.Random(11)
# mechas em blocos: franja varrida para um lado + laterais cobrindo a orelha de leve (silhueta irregular)
for k in range(7):
    a = -0.9 + k * 0.3
    pc = V((cc.x + math.sin(a) * larg * 0.42, cc.y - math.cos(a) * prof * 0.28 + prof * 0.05, cc.z + alt * (0.36 - 0.03 * abs(k - 3))))
    m_ = Matrix.Translation(pc) @ Matrix.Rotation(-a * 0.8, 4, "Z") @ Matrix.Rotation(0.5, 4, "X") @ Matrix.Rotation(rr.uniform(-0.2, 0.2), 4, "Y")
    c.esfera((0, 0, 0), 1.0, CABELO if k % 2 else CABELO_CL, 12, 8, M=m_ @ Matrix.Diagonal((larg * 0.16, prof * 0.1, alt * 0.2, 1)))
for s_ in (-1, 1):                                                   # mechas puxadas para tras, por cima da orelha
    for k in range(4):
        pc = V((cc.x + s_ * larg * (0.47 - k * 0.04), cc.y + prof * (0.0 + k * 0.15), cc.z + alt * (0.2 - k * 0.02)))
        m_ = Matrix.Translation(pc) @ Matrix.Rotation(s_ * -0.35, 4, "Z") @ Matrix.Rotation(-0.35, 4, "X")
        c.esfera((0, 0, 0), 1.0, CABELO if k % 2 else CABELO_CL, 12, 8, M=m_ @ Matrix.Diagonal((larg * 0.06, prof * 0.2, alt * 0.12, 1)))
for k, (dx, dz) in enumerate(((-0.02, 0.0), (0.0, -0.012), (0.02, 0.0))):          # bico de viuva suave e irregular
    c.esfera(V((cc.x + dx, cc.y - prof * 0.43, cc.z + alt * 0.4 + dz)), 1.0, CABELO, 10, 6, escala=(larg * 0.12, prof * 0.08, alt * 0.08))
coque = V((cc.x, cc.y + prof * 0.62, cc.z + alt * 0.06))
c.esfera(coque, 1.0, CABELO, 16, 12, escala=(larg * 0.2, prof * 0.2, alt * 0.18))
c.cilindro(coque, coque + V((0, prof * 0.12, -alt * 0.55)), larg * 0.12, CABELO, 16, r2=larg * 0.05)
for k in range(5):                                                                          # coque em "voltas"
    a = 2 * PI * k / 5
    c.esfera(coque + V((math.cos(a) * larg * 0.08, prof * 0.05, math.sin(a) * alt * 0.07)), larg * 0.06, CABELO_CL if k % 2 else CABELO, 10, 6)
c.torus((0, 0, 0), larg * 0.13, 0.009, VERDE, 20, 6, M=Matrix.Translation(coque - V((0, prof * 0.1, 0))) @ Matrix.Rotation(PI / 2, 4, "X"))   # elastico
peca(c, OS_CABECA)

# olhos: esclera + iris + pupila + brilho (um pouco maiores: estilo cartoon realista)
o = Malha("Lima_Olhos"); o.chanfro = 0
for oc in (olho_e, olho_d):
    r = r_olho * 1.05
    oc = oc + V((0, -r * 0.22, 0))                                  # globo quase na orbita: aparece o branco, a palpebra emoldura
    r *= 0.93
    o.esfera(oc, r, BRANCO, 20, 14)
    frente = oc + V((0, -r * 0.84, 0))
    o.esfera(frente, r * 0.68, OLHO, 18, 12, escala=(1, 0.35, 1))                       # iris maior
    o.esfera(frente + V((0, -r * 0.12, 0)), r * 0.34, TXT, 14, 8, escala=(1, 0.3, 1))   # pupila
    o.esfera(frente + V((r * 0.2, -r * 0.24, r * 0.22)), r * 0.14, BRANCO, 8, 6)         # brilho
    s_ = 1 if oc.x > cc.x else -1
    pts_ = []
    for k in range(9):
        t = k / 8
        x = oc.x + s_ * (-0.011 + 0.03 * t)
        z = oc.z + 0.021 + 0.007 * math.sin(PI * min(1, t * 1.25))
        y = sup_frente(x, z, 0.006)
        if y is not None:
            pts_.append((V((x, y - 0.0012, z)), 0.0032 * (0.5 + math.sin(PI * (0.15 + 0.85 * t)))))
    # fita continua (uma tira de quads), sem arestas internas: o contorno so pega a silhueta
    topo_ = [o.bm.verts.new(p_ + V((0, 0, w))) for p_, w in pts_]
    base_ = [o.bm.verts.new(p_ - V((0, 0, w * 0.8))) for p_, w in pts_]
    for i_ in range(len(pts_) - 1):
        f_ = o.bm.faces.new((base_[i_], base_[i_ + 1], topo_[i_ + 1], topo_[i_]))
        f_.normal_update()
        if f_.normal.y > 0:
            f_.normal_flip()
        f_.material_index = o.mi(SOBRANCELHA)
# labios modelados (superior e inferior), colados na frente da boca
yb = min(p.y for p in VW if abs(p.x - boca_c.x) < 0.01 and abs(p.z - (boca_c.z - 0.012)) < 0.01)
o.esfera(V((boca_c.x, yb + 0.001, boca_c.z - 0.008)), 1.0, LABIO, 16, 8, escala=(0.019, 0.006, 0.0042))
o.esfera(V((boca_c.x, yb + 0.002, boca_c.z - 0.0165)), 1.0, LABIO, 16, 8, escala=(0.016, 0.0065, 0.0055))
peca(o, OS_CABECA)

# uniforme: gola, ziper, cinto com fivela, emblema NASA, cracha "LIMA", bandeira do Brasil no ombro
torso = [h.matrix_world @ v.co for v in me.vertices if me.polygons and any(ossos.get(g.group, "") in (OS_PEITO, osso("spine_02")) and g.weight > 0.5 for g in v.groups)]
tc = sum(torso, V()) / max(1, len(torso))
frente_y = min(p.y for p in torso) if torso else tc.y - 0.12
u = Malha("Lima_Uniforme"); u.chanfro = 0.004
u.painel_tex((tc.x + 0.085, frente_y - 0.012, tc.z + 0.07), 0.1, 0.1, (0, -1, 0), LOGO)
u.caixa((tc.x - 0.085, frente_y - 0.008, tc.z + 0.07), (0.09, 0.01, 0.045), BRANCO)
# ziper colado na superficie, do pescoco ao cinto
zs = [tc.z + 0.22 - k * 0.03 for k in range(16)]
pz = [V((tc.x, sup_frente(tc.x, z) - 0.004, z)) for z in zs if sup_frente(tc.x, z) is not None]
for a_, b_ in zip(pz[:-1], pz[1:]):
    u.cilindro(a_, b_, 0.006, MACACAO_ESC, 8)
if pz:
    u.caixa(pz[2] + V((0, -0.006, 0)), (0.014, 0.008, 0.026), METAL)
# bolsos do peito com aba
for sx in (-1, 1):
    x = tc.x + sx * 0.085; z = tc.z - 0.02
    y = sup_frente(x, z)
    if y is not None:
        u.caixa((x, y - 0.006, z), (0.085, 0.01, 0.09), MACACAO_ESC)
        u.caixa((x, y - 0.012, z + 0.042), (0.09, 0.012, 0.025), MACACAO)
        u.caixa((x, y - 0.019, z + 0.04), (0.012, 0.004, 0.012), METAL)
u.texto("LIMA", (tc.x - 0.085, frente_y - 0.015, tc.z + 0.07), 0.028, TXT, espessura=0.002, malha=True)
peca(u, OS_PEITO)
# gola em pe (presa no pescoco/peito)
pesc = rig.matrix_world @ rig.data.bones[osso("neck_01")].head_local
anel = [p for p in VW if abs(p.z - Z_GOLA) < 0.012 and abs(p.x) < 0.11 and abs(p.y - pesc.y) < 0.11]
amin = V((min(p.x for p in anel), min(p.y for p in anel), Z_GOLA)); amax = V((max(p.x for p in anel), max(p.y for p in anel), Z_GOLA))
ac = (amin + amax) / 2
g_ = Malha("Lima_Gola"); g_.chanfro = 0
Mg = Matrix.Translation(ac) @ Matrix.Diagonal(((amax.x - amin.x) / 2 + 0.01, (amax.y - amin.y) / 2 + 0.01, 1.0, 1))
g_.torus((0, 0, 0), 1.0, 0.011, MACACAO_ESC, 40, 6, M=Mg)
g_.torus((0, 0, 0.018), 1.04, 0.009, MACACAO, 40, 6, M=Mg)      # gola em pe, rente ao decote
peca(g_, osso("spine_03"))
# bolsos das pernas (lado de fora das coxas) e bandeira do Brasil no ombro esquerdo
for sx, nome_osso in ((1, "thigh_l"), (-1, "thigh_r")):
    ob = rig.data.bones[osso(nome_osso)]
    z = (rig.matrix_world @ ob.head_local).z - 0.2
    yc = (rig.matrix_world @ ob.head_local).y
    x = lado(yc, z, sx)
    if x is not None:
        bp = Malha(f"Lima_BolsoPerna_{nome_osso}"); bp.chanfro = 0
        bp.caixa((x + sx * 0.008, yc, z), (0.014, 0.11, 0.13), MACACAO_ESC)
        bp.caixa((x + sx * 0.014, yc, z + 0.06), (0.012, 0.115, 0.03), MACACAO)
        peca(bp, osso(nome_osso))
ob = rig.data.bones[osso("upperarm_l")]
pa = rig.matrix_world @ (ob.head_local * 0.7 + ob.tail_local * 0.3)
xb = lado(pa.y, pa.z, 1 if pa.x > 0 else -1, tol=0.03, xmax=0.6)
if xb is not None:
    sx = 1 if pa.x > 0 else -1
    bb = Malha("Lima_Bandeira"); bb.chanfro = 0
    bb.caixa((xb + sx * 0.004, pa.y, pa.z), (0.006, 0.07, 0.048), VERDE)
    bb.prisma([(-0.03, 0), (0, 0.02), (0.03, 0), (0, -0.02)], 0, 0.004, AMARELO,
              M=Matrix.Translation(V((xb + sx * 0.008, pa.y, pa.z))) @ Matrix.Rotation(sx * PI / 2, 4, "Y") @ Matrix.Rotation(PI / 2, 4, "Z"))
    bb.cilindro((xb + sx * 0.009, pa.y, pa.z), (xb + sx * 0.012, pa.y, pa.z), 0.011, material("CT_Azul_Escuro", "#17306E"), 12)
    peca(bb, osso("upperarm_l"))
# punhos e barra (costura escura) nos pulsos e tornozelos
for nome_osso in ("hand_l", "hand_r", "foot_l", "foot_r"):
    pu = Malha(f"Lima_Punho_{nome_osso}"); pu.chanfro = 0
    ob = rig.data.bones[osso(nome_osso)]
    pnt = rig.matrix_world @ ob.head_local + (V((0, 0, 0.03)) if "foot" in nome_osso else V())
    eixo = (rig.matrix_world.to_3x3() @ (ob.tail_local - ob.head_local)).normalized() if "hand" in nome_osso else V((0, 0, 1))
    Mq = Matrix.Translation(pnt) @ eixo.to_track_quat("Z", "Y").to_matrix().to_4x4()
    pu.torus((0, 0, 0), 0.036 if "hand" in nome_osso else 0.05, 0.008, MACACAO_ESC, 24, 6, M=Mq)
    peca(pu, osso(nome_osso))
pecas.remove(pu.criar(None, 1.0)) if False else None
# cinto: secao real da cintura (vertices do corpo numa faixa de altura, sem os bracos)
zc_ = (rig.matrix_world @ rig.data.bones[OS_QUADRIL].head_local).z + 0.1
faixa = [h.matrix_world @ v.co for v in me.vertices if abs((h.matrix_world @ v.co).z - zc_) < 0.025 and abs((h.matrix_world @ v.co).x) < 0.2]
fmin = V((min(p.x for p in faixa), min(p.y for p in faixa), zc_)); fmax = V((max(p.x for p in faixa), max(p.y for p in faixa), zc_))
qc = (fmin + fmax) / 2; rx, ry = (fmax.x - fmin.x) / 2 + 0.008, (fmax.y - fmin.y) / 2 + 0.008
b = Malha("Lima_Cinto"); b.chanfro = 0
b.torus((0, 0, 0), 1.0, 0.016, MACACAO_ESC, 40, 6, M=Matrix.Translation(qc) @ Matrix.Diagonal((rx, ry, 1.0, 1)))
b.caixa((qc.x, fmin.y - 0.01, zc_), (0.06, 0.012, 0.04), METAL)
peca(b, OS_QUADRIL)

# junta tudo num so SkinnedMesh (1 draw call por material)
for p_ in pecas:
    p_.parent = None
bpy.ops.object.select_all(action="DESELECT")
for p_ in pecas:
    p_.select_set(True)
h.select_set(True); bpy.context.view_layer.objects.active = h
bpy.ops.object.join()
h.name = "Lima"
for p in h.data.polygons:
    p.use_smooth = True
mod = [m for m in h.modifiers if m.type == "ARMATURE"]
if not mod:
    a = h.modifiers.new("Armature", "ARMATURE"); a.object = rig
rig.name = "Lima_Rig"
print(f"[Lima] {contar_tris([h])} triangulos, {len(rig.data.bones)} ossos, materiais {[m.name for m in h.data.materials]}")
print("[Lima] ossos-chave:", [b.name for b in rig.data.bones if b.name.lower() in ("root", "pelvis", "spine_01", "spine_03", "neck_01", "head", "upperarm_l", "lowerarm_l", "hand_l", "thigh_l", "calf_l", "foot_l", "jaw")])

prev = arg("--preview")
if prev:
    os.makedirs(prev, exist_ok=True)
    preparar_render((480, 540), mundo="#C9D6EA")
    sc_ = bpy.context.scene; sc_.render.line_thickness = 0.8
    rp = PELE.node_tree.nodes.get("Color Ramp") or next((n for n in PELE.node_tree.nodes if n.type == "VALTORGB"), None)
    if rp and len(rp.color_ramp.elements) == 3:
        rp.color_ramp.elements.remove(rp.color_ramp.elements[1])      # pele: 2 faixas (sem a do meio)
        rp.color_ramp.elements[1].position = 0.18
    ls_ = sc_.view_layers[0].freestyle_settings.linesets[0]; ls_.select_crease = False; ls_.select_border = False
    f1, f2 = os.path.join(prev, "_f.png"), os.path.join(prev, "_p.png")
    renderizar(f1, (0.0, -3.3, 1.0), (0.0, 0, 0.9), 50)
    renderizar(f2, (cc.x, cc.y - 0.95, cc.z), (cc.x, cc.y, cc.z - 0.02), 85)        # close do rosto (frente)
    import numpy as np
    a_, b_ = bpy.data.images.load(f1), bpy.data.images.load(f2)
    pa = np.array(a_.pixels[:]).reshape(540, 480, 4); pb = np.array(b_.pixels[:]).reshape(540, 480, 4)
    img = bpy.data.images.new("lado_a_lado", 960, 540); img.pixels = np.concatenate([pa, pb], axis=1).ravel().tolist()
    img.filepath_raw = os.path.join(prev, "lima_frente_perfil.png"); img.file_format = "PNG"; img.save()
    remover_preview()
fbx = arg("--fbx")
if fbx:
    bpy.ops.object.select_all(action="DESELECT")
    rig.select_set(True); h.select_set(True); bpy.context.view_layer.objects.active = rig
    bpy.ops.export_scene.fbx(filepath=os.path.abspath(fbx), use_selection=True, object_types={"ARMATURE", "MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", axis_forward="-Z", axis_up="Y",
                             add_leaf_bones=False, primary_bone_axis="Y", secondary_bone_axis="X", use_armature_deform_only=True,
                             mesh_smooth_type="FACE", bake_anim=True, bake_anim_use_all_actions=True, bake_anim_use_nla_strips=False)
    print("[Lima] FBX", fbx)
