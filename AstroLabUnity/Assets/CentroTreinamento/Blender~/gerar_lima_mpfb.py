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
for alvo, v in (("l-ear-shape-round", 0.7), ("r-ear-shape-round", 0.7), ("l-ear-scale-decr", 0.15), ("r-ear-scale-decr", 0.15),
                ("mouth-lowerlip-volume-incr", 0.35), ("mouth-upperlip-volume-incr", 0.3), ("nose-scale-vert-incr", 0.1)):
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
_bc = None

Z_GOLA = (rig.matrix_world @ rig.data.bones["neck_01"].head_local).z - 0.006
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
dec = h.modifiers.new("Decimate", "DECIMATE"); dec.ratio = 0.45; dec.vertex_group = "so_uniforme"   # rosto e maos em resolucao cheia
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
from mathutils.kdtree import KDTree
_kd = KDTree(len(me.vertices))
for _v in me.vertices:
    _kd.insert(h.matrix_world @ _v.co, _v.index)
_kd.balance()
_nomes = {g.index: g.name for g in h.vertex_groups}
def _pesos(p):
    _, i_, _ = _kd.find(p)
    return {_nomes[g.group]: g.weight for g in me.vertices[i_].groups if g.group in _nomes and g.weight > 0.01}
def peca(m, osso_nome):
    o = m.criar(None, 1.0)
    vs_ = list(range(len(o.data.vertices)))
    if osso_nome == OS_CABECA:
        o.vertex_groups.new(name=osso_nome).add(vs_, 1.0, "REPLACE")
    else:
        # pesos copiados da pele embaixo da peca (antes 100% num osso: crachá/bandeira/bolsos descolavam do macacao)
        cs = [o.matrix_world @ v.co for v in o.data.vertices]
        bmin = V((min(c.x for c in cs), min(c.y for c in cs), min(c.z for c in cs))); bmax = V((max(c.x for c in cs), max(c.y for c in cs), max(c.z for c in cs)))
        rigida = (bmax - bmin).length < 0.16
        centro_ = sum(cs, V()) / len(cs)
        for vi, c in zip(vs_, cs):
            for nome_, w in (_pesos(centro_) if rigida else _pesos(c)).items():
                (o.vertex_groups.get(nome_) or o.vertex_groups.new(name=nome_)).add([vi], w, "REPLACE")
    pecas.append(o)
    return o

# cabelo: calota que contorna o cranio (cortada na testa/rosto) + coque/rabo de cavalo
c = Malha("Lima_Cabelo"); c.chanfro = 0
# cabelo = casca que segue o cranio real do MPFB (sem touca achatada): faces da cabeca acima da linha do cabelo,
# deslocadas pela normal (volume maior no alto), com sulcos que formam 4 mechas largas ate o coque
def u_cab(p):
    return V(((p.x - cc.x) / (larg / 2), (p.y - cc.y) / (prof / 2), (p.z - cc.z) / (alt / 2)))
def limiar(u):
    s_ = min(1.0, max(0.0, (u.y + 0.45) / 1.0)); s_ = s_ * s_ * (3 - 2 * s_)
    return (0.68 - 0.55 * max(0.0, abs(u.x) - 0.4)) * (1 - s_) + (-0.62) * s_
def tem_cabelo(p):
    u = u_cab(p)
    if abs(u.x) > 0.72 and u.z < 0.12 and u.y < 0.55:          # orelhas e costeletas descobertas
        return False
    return u.z > limiar(u)
osso_cab = h.vertex_groups[OS_CABECA].index if OS_CABECA in h.vertex_groups else None
bmh = bmesh.new(); bmh.from_mesh(h.data); bmh.faces.ensure_lookup_table(); bmh.verts.ensure_lookup_table()
dlh = bmh.verts.layers.deform.active
def da_cabeca(v):
    return dlh and osso_cab is not None and v[dlh].get(osso_cab, 0) > 0.5
sel = [f_ for f_ in bmh.faces if f_.material_index == 0 and all(da_cabeca(v) for v in f_.verts)
       and tem_cabelo(h.matrix_world @ f_.calc_center_median())]
vs = {v for f_ in sel for v in f_.verts}
selset = set(sel)
def espessura(p):
    u = u_cab(p)
    phi = math.atan2(u.x, max(0.05, u.z + 0.6))                     # angulo lateral (mechas convergem para tras)
    sulco = 0.5 + 0.5 * math.cos(phi * 7.5)                         # 4 mechas: cristas largas, sulcos estreitos
    alto = max(0.0, u.z) * 0.012 + max(0.0, 0.4 - abs(u.y)) * 0.004
    borda = min(1.0, max(0.0, (u.z - limiar(u)) / 0.18))           # afina perto da linha do cabelo
    return (0.004 + alto + 0.005 * sulco ** 0.6) * (0.35 + 0.65 * borda), sulco
ext, inn = {}, {}
for v in vs:
    p = h.matrix_world @ v.co; n = (h.matrix_world.to_3x3() @ v.normal).normalized()
    e, _ = espessura(p)
    ext[v] = c.bm.verts.new(p + n * e)
    inn[v] = c.bm.verts.new(p - n * 0.002)
i_esc, i_cl = c.mi(CABELO), c.mi(CABELO_CL)
for f_ in sel:
    pc_ = h.matrix_world @ f_.calc_center_median()
    _, sulco = espessura(pc_)
    nf = c.bm.faces.new([ext[v] for v in f_.verts]); nf.smooth = True
    nf.material_index = i_cl if (sulco > 0.72 and u_cab(pc_).z > 0.1) else i_esc
# aba na borda (fecha a casca ate a pele: sem fresta na linha do cabelo)
for e_ in {e_ for f_ in sel for e_ in f_.edges}:
    if sum(1 for lf in e_.link_faces if lf in selset) == 1:
        a_, b_ = e_.verts
        nf = c.bm.faces.new((ext[a_], ext[b_], inn[b_], inn[a_])); nf.material_index = i_esc; nf.smooth = True
pts_cab = [h.matrix_world @ v.co for v in vs]
bordas = {v for e_ in {e_ for f_ in sel for e_ in f_.edges} if sum(1 for lf in e_.link_faces if lf in selset) == 1 for v in e_.verts}
viz = {v: [w for e_ in v.link_edges for w in e_.verts if w is not v and w in bordas] for v in bordas}
for _ in range(6):                                                  # linha do cabelo lisa (sem degraus da malha)
    for tab in (ext, inn):
        novos = {v: tab[v].co * 0.5 + sum((tab[w].co for w in viz[v]), V()) * (0.5 / max(1, len(viz[v]))) for v in bordas if viz[v]}
        for v, p_ in novos.items():
            tab[v].co = p_
bmh.free()
c.bm.normal_update()
centro_cab = sum(pts_cab, V()) / max(1, len(pts_cab))
for f_ in c.bm.faces:                                               # normais para fora
    if (f_.calc_center_median() - centro_cab).dot(f_.normal) < 0:
        f_.normal_flip()
# coque grande no alto da nuca (legivel de perfil e de costas), com volta de cabelo e elastico verde
traseira = max(p.y for p in pts_cab if abs(p.z - (cc.z + alt * 0.12)) < 0.02)
coque = V((cc.x, traseira + prof * 0.12, cc.z + alt * 0.16))
c.esfera(coque, 1.0, CABELO, 20, 12, escala=(larg * 0.3, prof * 0.22, alt * 0.26))
Mc = Matrix.Translation(coque + V((0, prof * 0.04, 0))) @ Matrix.Rotation(PI / 2, 4, "X")
c.torus((0, 0, 0), larg * 0.2, larg * 0.075, CABELO_CL, 20, 10, M=Mc)
c.torus((0, 0, 0), larg * 0.26, 0.008, VERDE, 20, 6, M=Matrix.Translation(coque - V((0, prof * 0.09, 0))) @ Matrix.Rotation(PI / 2, 4, "X"))
peca(c, OS_CABECA)

# olhos: esclera + iris + pupila + brilho (um pouco maiores: estilo cartoon realista)
o = Malha("Lima_Olhos"); o.chanfro = 0
OLHOS = []
for oc in (olho_e, olho_d):
    r = r_olho * 0.98
    oc = oc + V((0, -r * 0.14, 0))                                  # um pouco para fora: o branco aparece
    o.esfera(oc, r, BRANCO, 16, 10)
    OLHOS.append((oc.copy(), r))
    frente = oc + V((0, -r * 0.84, 0))
    o.esfera(frente, r * 0.5, OLHO, 14, 8, escala=(1, 0.35, 1))                        # iris
    o.esfera(frente + V((0, -r * 0.12, 0)), r * 0.24, TXT, 10, 6, escala=(1, 0.3, 1))   # pupila
    o.esfera(frente + V((r * 0.14, -r * 0.2, r * 0.15)), r * 0.08, BRANCO, 6, 4)         # brilho
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
# labios do MPFB pintados (elipse em volta da fenda, so as faces viradas para frente)
z_lab = boca_c.z - 0.012
for p in h.data.polygons:
    c_ = h.matrix_world @ p.center
    if p.material_index == 0 and p.normal.y < -0.25 and c_.y < yb + 0.01 and ((c_.x - boca_c.x) / 0.023) ** 2 + ((c_.z - z_lab) / 0.0095) ** 2 < 1.0:
        p.material_index = i_lab
peca(o, OS_CABECA)
def marca(o_, nome):
    o_.vertex_groups.new(name=nome).add(list(range(len(o_.data.vertices))), 1.0, "REPLACE")
# labio inferior separado (desce com a boca), interior da boca escondido atras dos labios, palpebras escondidas no olho
bi = Malha("Lima_BocaInt"); bi.chanfro = 0
BOCA_C = V((boca_c.x, yb + 0.009, boca_c.z - 0.0125))
bi.esfera(BOCA_C, 1.0, BOCA, 16, 8, escala=(0.013, 0.003, 0.0075))
marca(peca(bi, OS_CABECA), "SK_boca")
pl_ = Malha("Lima_Palpebras"); pl_.chanfro = 0
for oc_, r_ in ([] if os.environ.get('SEM_PALPEBRA') else OLHOS):
    pl_.esfera(oc_, r_ * 1.07, PELE, 16, 10)
    bmesh.ops.delete(pl_.bm, geom=[v for v in pl_.bm.verts if v.co.y > oc_.y + r_ * 0.15 and (v.co - oc_).length < r_ * 1.2], context="VERTS")
if not os.environ.get("SEM_PALPEBRA"):
    marca(peca(pl_, OS_CABECA), "SK_palpebra")

# uniforme: gola, ziper, cinto com fivela, emblema NASA, cracha "LIMA", bandeira do Brasil no ombro
torso = [h.matrix_world @ v.co for v in me.vertices if me.polygons and any(ossos.get(g.group, "") in (OS_PEITO, osso("spine_02")) and g.weight > 0.5 for g in v.groups)]
tc = sum(torso, V()) / max(1, len(torso))
frente_y = min(p.y for p in torso) if torso else tc.y - 0.12
u = Malha("Lima_Uniforme"); u.chanfro = 0.004
def _yf(x, z):
    y_ = sup_frente(x, z, 0.025)
    return y_ if y_ is not None else frente_y
u.painel_tex((tc.x + 0.085, _yf(tc.x + 0.085, tc.z + 0.07) - 0.006, tc.z + 0.07), 0.1, 0.1, (0, -1, 0), LOGO)   # colado na superficie
u.caixa((tc.x - 0.085, _yf(tc.x - 0.085, tc.z + 0.07) - 0.004, tc.z + 0.07), (0.09, 0.01, 0.045), BRANCO)
# ziper colado na superficie, do pescoco ao cinto
zs = [z_ for z_ in (tc.z + 0.22 - k * 0.03 for k in range(16)) if z_ < Z_GOLA - 0.015]   # so abaixo da gola (antes subia pelo pescoco ate o queixo)
pz = [V((tc.x, sup_frente(tc.x, z) - 0.0015, z)) for z in zs if sup_frente(tc.x, z) is not None]
for a_, b_ in zip(pz[:-1], pz[1:]):
    u.cilindro(a_, b_, 0.0035, MACACAO_ESC, 8)
# bolsos do peito com aba
for sx in (-1, 1):
    x = tc.x + sx * 0.085; z = tc.z - 0.02
    y = sup_frente(x, z)
    if y is not None:
        u.caixa((x, y - 0.006, z), (0.085, 0.01, 0.09), MACACAO_ESC)
        u.caixa((x, y - 0.012, z + 0.042), (0.09, 0.012, 0.025), MACACAO)
        u.caixa((x, y - 0.019, z + 0.04), (0.012, 0.004, 0.012), METAL)
u.texto("LIMA", (tc.x - 0.085, _yf(tc.x - 0.085, tc.z + 0.07) - 0.0095, tc.z + 0.07), 0.028, TXT, espessura=0.002, malha=True)
peca(u, OS_PEITO)
# gola em pe (presa no pescoco/peito)
pesc = rig.matrix_world @ rig.data.bones[osso("neck_01")].head_local
anel = [p for p in VW if abs(p.z - Z_GOLA) < 0.012 and abs(p.x) < 0.11 and abs(p.y - pesc.y) < 0.11]
amin = V((min(p.x for p in anel), min(p.y for p in anel), Z_GOLA)); amax = V((max(p.x for p in anel), max(p.y for p in anel), Z_GOLA))
ac = (amin + amax) / 2
g_ = Malha("Lima_Gola"); g_.chanfro = 0
Mg = Matrix.Translation(ac) @ Matrix.Diagonal(((amax.x - amin.x) / 2 + 0.005, (amax.y - amin.y) / 2 + 0.005, 1.0, 1))
g_.torus((0, 0, 0), 1.0, 0.0065, MACACAO_ESC, 40, 6, M=Mg)      # gola fina e baixa, rente ao decote
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
from mathutils.bvhtree import BVHTree
_dg = bpy.context.evaluated_depsgraph_get()
_bvh = BVHTree.FromObject(h, _dg)
_pel = rig.matrix_world @ rig.data.bones[OS_QUADRIL].head_local
qc = V((_pel.x, _pel.y, zc_))
b = Malha("Lima_Cinto"); b.chanfro = 0
NA = 48
raios = []
for k in range(NA):                                                  # raio real da cintura: raio de fora para dentro
    ph = 2 * PI * k / NA
    d_ = V((math.cos(ph), math.sin(ph), 0))
    hit = _bvh.ray_cast(qc + d_ * 0.4, -d_, 0.4)
    raios.append((hit[0] - qc).length if hit[0] is not None else None)
med = sum(r_ for r_ in raios if r_) / max(1, sum(1 for r_ in raios if r_))
raios = [r_ if r_ else med for r_ in raios]
raios = [max(raios[k - 1], raios[k], raios[(k + 1) % NA]) for k in range(NA)]          # nao afunda nas reentrancias
raios = [(raios[k - 1] + 2 * raios[k] + raios[(k + 1) % NA]) / 4 for k in range(NA)]
print("[Lima] cinto: raio", round(min(raios), 3), "a", round(max(raios), 3))
ani = []
for k in range(NA):
    ph = 2 * PI * k / NA; d_ = V((math.cos(ph), math.sin(ph), 0)); r_ = raios[k]
    ani.append([b.bm.verts.new(qc + d_ * (r_ + dr) + V((0, 0, dz))) for dr, dz in ((-0.002, -0.02), (0.009, -0.02), (0.009, 0.02), (-0.002, 0.02))])
ib = b.mi(MACACAO_ESC)
for k in range(NA):
    r0, r1 = ani[k], ani[(k + 1) % NA]
    for j in range(4):
        f_ = b.bm.faces.new((r0[j], r1[j], r1[(j + 1) % 4], r0[(j + 1) % 4])); f_.material_index = ib
k_f = min(range(NA), key=lambda k: abs(2 * PI * k / NA - 1.5 * PI))                       # frente = -Y
fy = qc.y - raios[k_f] - 0.012
b.caixa((qc.x, fy, zc_), (0.06, 0.008, 0.044), METAL)
b.caixa((qc.x, fy - 0.004, zc_), (0.04, 0.004, 0.026), MACACAO_ESC)
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

print("[Lima] matriz do corpo:", [round(x, 3) for x in h.matrix_world.to_scale()], [round(x, 3) for x in h.matrix_world.translation])
# ---------------------------------------------------------------- 4. shape keys (piscar, boca) e animacoes no proprio rig
from mathutils import Quaternion as Q
me = h.data
def grupo(nome):
    if nome not in h.vertex_groups:
        return []
    gi = h.vertex_groups[nome].index
    return [v.index for v in me.vertices if any(g.group == gi and g.weight > 0.5 for g in v.groups)]
v_palp, v_boca, v_lab = grupo("SK_palpebra"), grupo("SK_boca"), grupo("SK_labio")
h.shape_key_add(name="Basis", from_mix=False)
k_pisca = h.shape_key_add(name="Piscar", from_mix=False)
k_boca = h.shape_key_add(name="Boca", from_mix=False)
base = h.data.shape_keys.key_blocks["Basis"]
olho_de = {}
for vi in v_palp:                                                 # cada vertice da palpebra pertence ao olho mais proximo
    p = me.vertices[vi].co
    olho_de[vi] = min(OLHOS, key=lambda o_: (o_[0] - p).length)
for vi in v_palp:                                                 # basis: palpebra encolhida dentro do globo (invisivel)
    oc_, r_ = olho_de[vi]
    base.data[vi].co = oc_ + (me.vertices[vi].co - oc_) * 0.3
for vi in v_boca:                                                 # basis: interior da boca achatado atras dos labios
    p = me.vertices[vi].co
    base.data[vi].co = V((p.x, p.y, BOCA_C.z + (p.z - BOCA_C.z) * 0.05))
for kb in (k_pisca, k_boca):                                      # as chaves partem da basis
    for i, d_ in enumerate(base.data):
        kb.data[i].co = d_.co.copy()
for vi in v_palp:
    k_pisca.data[vi].co = me.vertices[vi].co.copy()               # palpebra fechada = tamanho cheio, cobre o olho
for vi in v_boca:
    k_boca.data[vi].co = me.vertices[vi].co.copy() + V((0, 0, -0.003))
QUEIXO = 0.0065
for vi in v_lab:
    k_boca.data[vi].co = base.data[vi].co + V((0, -0.001, -QUEIXO))
z_fenda = boca_c.z - 0.0125
for v in me.vertices:                                             # queixo/mandibula descem junto (pele do rosto abaixo da fenda)
    if v.index in olho_de or v.index in v_boca or v.index in v_lab:
        continue
    p = h.matrix_world @ v.co
    dx = abs(p.x - boca_c.x)
    if p.z < z_fenda - 0.0005 and p.z > boca_c.z - 0.075 and dx < 0.045 and p.y < boca_c.y + 0.035:
        w = (1 - (dx / 0.045) ** 2) * min(1.0, (p.z - (boca_c.z - 0.075)) / 0.025) * min(1.0, (boca_c.y + 0.035 - p.y) / 0.02)
        k_boca.data[v.index].co = base.data[v.index].co + V((0, 0, -QUEIXO * max(0.0, w)))
for nome in [n_ for n_ in ("SK_palpebra", "SK_boca", "SK_labio") if n_ in h.vertex_groups]:
    h.vertex_groups.remove(h.vertex_groups[nome])
# a malha passa a mostrar a basis (palpebra/boca escondidas)
for i, d_ in enumerate(base.data):
    me.vertices[i].co = d_.co

k_pisca.value = 0.0; k_boca.value = 0.0                          # Blender 5: chave nova nasce com valor 1
# animacoes: poses em "deltas de mundo" (espaco do armature em repouso), convertidas para rotacao local de cada osso
FPS = 30
bpy.context.scene.render.fps = FPS
B = {b.name: b for b in rig.data.bones}
def ob(n):
    return osso(n)
def dir_rep(n):
    b = B[ob(n)]
    return (b.tail_local - b.head_local).normalized()
def vec(x, y, z):
    return V((x, y, z)).normalized()
def espelho(v):
    return V((-v.x, v.y, v.z))
def pose(spec):
    """spec: osso -> ('dir', vetor) | ('rot', eixo, graus) relativo ao pai | ('mundo', Quaternion). Retorna {osso: quaternion local}."""
    W, out = {}, {}
    for b in rig.data.bones:                                       # bones vem em ordem hierarquica
        wp = W.get(b.parent.name, Q()) if b.parent else Q()
        sp = spec.get(b.name)
        if sp is None:
            w = wp
        elif sp[0] == "dir":
            atual = wp @ (b.tail_local - b.head_local).normalized()
            w = atual.rotation_difference(sp[1]) @ wp
        elif sp[0] == "rot":
            w = wp @ Q(V(sp[1]), math.radians(sp[2]))
        else:
            w = sp[1]
        W[b.name] = w
        Bm = b.matrix_local.to_3x3().to_quaternion()
        out[b.name] = Bm.inverted() @ (wp.inverted() @ w) @ Bm
    return out
def chave(acao, quadro, spec):
    q = pose(spec)
    for pb in rig.pose.bones:
        pb.rotation_mode = "QUATERNION"
        pb.rotation_quaternion = q[pb.name]
        pb.keyframe_insert("rotation_quaternion", frame=quadro, group=pb.name)
BRACO_L = vec(0.16, 0.04, -1)                                     # bracos relaxados ao lado do corpo
ANTEB_L = vec(0.08, -0.22, -1)
def base_pose(resp=0.0, cab=(0, 0, 0)):
    s_ = {ob("upperarm_l"): ("dir", BRACO_L), ob("upperarm_r"): ("dir", espelho(BRACO_L)),
          ob("lowerarm_l"): ("dir", ANTEB_L), ob("lowerarm_r"): ("dir", espelho(ANTEB_L)),
          ob("spine_02"): ("rot", (1, 0, 0), resp), ob("spine_03"): ("rot", (1, 0, 0), resp * 0.6)}
    for lado_, sg in (("l", 1), ("r", -1)):                       # pernas quase paralelas (o repouso do MPFB e em A, abertas)
        s_[ob(f"thigh_{lado_}")] = ("dir", vec(sg * 0.012, 0.0, -1))
        s_[ob(f"calf_{lado_}")] = ("dir", vec(sg * 0.01, 0.02, -1))
        s_[ob(f"foot_{lado_}")] = ("mundo", Q())
    s_[ob("head")] = ("rot", (1, 0, 0), cab[0])
    s_[ob("neck_01")] = ("rot", (0, 0, 1), cab[1])
    return s_
def nova_acao(nome):
    a_ = bpy.data.actions.new(nome)
    a_.use_fake_user = True
    rig.animation_data_create()
    rig.animation_data.action = a_
    return a_
acoes = []
# Idle (2 s): respiracao + leve balanco da cabeca
acoes.append(nova_acao("Idle"))
for f_ in range(0, 61, 10):
    t = 2 * PI * f_ / 60
    chave(acoes[-1], f_, base_pose(resp=1.5 * math.sin(t), cab=(1.5 * math.sin(t + 1), 2.5 * math.sin(t * 0.5), 0)))
# Walk (1 s por ciclo de 2 passos; 1,6 m/s no Unity -> passada 0,8 m, sem escorregar)
acoes.append(nova_acao("Walk"))
for f_ in range(0, 31, 3):
    ph = 2 * PI * f_ / 30
    s_ = base_pose()
    for lado_, sg in (("l", 1), ("r", -1)):
        a_ = math.radians(24) * sg * math.sin(ph)                 # coxa: + para frente
        joelho = math.radians(6 + 42 * max(0.0, sg * math.cos(ph)))   # dobra na fase de balanco
        coxa = vec(0.0, -math.sin(a_), -math.cos(a_))
        coxa = V((sg * 0.012, coxa.y, coxa.z)).normalized()          # passada estreita (antes herdava a abertura do repouso em A)
        canela = Q(V((1, 0, 0)), joelho) @ coxa
        s_[ob(f"thigh_{lado_}")] = ("dir", coxa)
        s_[ob(f"calf_{lado_}")] = ("dir", canela)
        s_[ob(f"foot_{lado_}")] = ("mundo", Q(V((1, 0, 0)), math.radians(-10) * max(0.0, -sg * math.sin(ph))))
        b_ = math.radians(-16) * sg * math.sin(ph)                # bracos: contrarios as pernas
        braco = Q(V((1, 0, 0)), b_) @ (BRACO_L if lado_ == "l" else espelho(BRACO_L))
        s_[ob(f"upperarm_{lado_}")] = ("dir", braco)
        s_[ob(f"lowerarm_{lado_}")] = ("dir", (Q(V((1, 0, 0)), b_ * 1.3 - 0.25) @ (ANTEB_L if lado_ == "l" else espelho(ANTEB_L))).normalized())
    s_[ob("pelvis")] = ("rot", (0, 0, 1), 4 * math.sin(ph))
    s_[ob("spine_02")] = ("rot", (0, 0, 1), -6 * math.sin(ph))
    chave(acoes[-1], f_, s_)
# Talk (3 s): gestos alternando as maos + acenos de cabeca
acoes.append(nova_acao("Talk"))
GESTO = vec(0.25, -1.0, -0.35)
for f_, (gd, ge, cab) in ((0, (0.0, 0.0, 0)), (12, (1.0, 0.0, 3)), (24, (0.8, 0.2, -2)), (38, (0.2, 0.9, 2)), (52, (0.0, 1.0, -1)),
                          (66, (0.6, 0.5, 3)), (80, (0.3, 0.0, 0)), (90, (0.0, 0.0, 0))):
    s_ = base_pose(resp=1.0, cab=(cab, cab * 1.5, 0))
    for lado_, g_ in (("r", gd), ("l", ge)):
        alvo = GESTO if lado_ == "l" else espelho(GESTO)
        rep = ANTEB_L if lado_ == "l" else espelho(ANTEB_L)
        s_[ob(f"lowerarm_{lado_}")] = ("dir", rep.lerp(alvo, g_).normalized())
        s_[ob(f"hand_{lado_}")] = ("rot", (0, 1, 0), (-25 if lado_ == "l" else 25) * g_)
    chave(acoes[-1], f_, s_)
# Point (1,5 s, segura): braco direito apontando para frente/direita
acoes.append(nova_acao("Point"))
for f_, k_ in ((0, 1.0), (22, 1.0), (45, 1.0)):
    s_ = base_pose(resp=0.8 * math.sin(f_ / 45 * 2 * PI), cab=(2, -6, 0))
    s_[ob("upperarm_r")] = ("dir", vec(-0.45, -0.88, 0.12))
    s_[ob("lowerarm_r")] = ("dir", vec(-0.35, -0.94, 0.06))
    s_[ob("hand_r")] = ("dir", vec(-0.3, -0.95, 0.0))
    chave(acoes[-1], f_, s_)
# Wave (1,2 s): braco direito levantado, antebraco balancando
acoes.append(nova_acao("Wave"))
for f_ in range(0, 37, 6):
    ang = math.radians(22) * math.sin(2 * PI * f_ / 18)
    s_ = base_pose(cab=(0, -4, 0))
    s_[ob("upperarm_r")] = ("dir", vec(-0.75, -0.25, 0.55))
    s_[ob("lowerarm_r")] = ("dir", (Q(V((0, 1, 0)), ang) @ vec(-0.15, -0.2, 1.0)).normalized())
    chave(acoes[-1], f_, s_)
for a_ in acoes:
    for fc in (a_.fcurves if hasattr(a_, "fcurves") else []):
        for kp in fc.keyframe_points:
            kp.interpolation = "BEZIER"
rig.animation_data.action = acoes[0]
print(f"[Lima] animacoes: {[a_.name for a_ in acoes]}; shape keys: {[k.name for k in h.data.shape_keys.key_blocks]}")

prev = arg("--preview")
if prev:
    os.makedirs(prev, exist_ok=True)
    preparar_render((320, 540), mundo="#C9D6EA")
    sc_ = bpy.context.scene; sc_.render.line_thickness = 0.8
    ls_ = sc_.view_layers[0].freestyle_settings.linesets[0]; ls_.select_crease = False; ls_.select_border = False
    fs_ = [os.path.join(prev, f"_{k}.png") for k in range(3)]
    renderizar(fs_[0], (0.0, -4.2, 1.0), (0.0, 0, 0.9), 50)
    renderizar(fs_[1], (-4.2, 0.0, 1.0), (0.0, 0, 0.9), 50)                          # perfil
    renderizar(fs_[2], (cc.x - 0.3, cc.y - 0.95, cc.z + 0.02), (cc.x, cc.y + 0.02, cc.z - 0.01), 70)   # close 3/4
    import numpy as np
    ps_ = [np.array(bpy.data.images.load(f_).pixels[:]).reshape(540, 320, 4) for f_ in fs_]
    img = bpy.data.images.new("lado_a_lado", 960, 540); img.pixels = np.concatenate(ps_, axis=1).ravel().tolist()
    img.filepath_raw = os.path.join(prev, "lima_v4.png"); img.file_format = "PNG"; img.save()
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
