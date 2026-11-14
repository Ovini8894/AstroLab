"""
Base Lunar — crateras com relevo (cartoon) + planeta Terra novo.

Uso (rodar SEMPRE numa cópia do BaseLunar.blend.bak renomeada para .blend):
  blender -b <copia>.blend --python crateras_e_terra.py -- <saida.fbx|-> <saida.blend|-> <pasta_texturas_unity>

- Esculpe ~135 crateras no Terreno_Lua (bacia, borda irregular, fundo plano e pico central nas grandes),
  fora da base, da usina, dos marcadores e do caminho do spawn. O terreno só é refinado perto das crateras.
- Rochas e detritos acompanham o relevo novo; pedregulhos (Rocha_Borda_*) nas bordas das crateras grandes.
- Troca a Terra_Ceu por uma Terra cartoon (texturas geradas aqui) + Terra_Nuvens + Terra_Atmosfera.
- Exporta o FBX com as MESMAS configurações do BaseLunar.fbx original (nomes de objetos preservados).
"""
import bpy, bmesh, math, random, os, sys
import numpy as np
from mathutils import Vector, noise
from mathutils.bvhtree import BVHTree

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
SAIDA_FBX = argv[0] if len(argv) > 0 and argv[0] != "-" else None
SAIDA_BLEND = argv[1] if len(argv) > 1 and argv[1] != "-" else None
DIR_TEX = argv[2] if len(argv) > 2 else None

D = bpy.data
rnd = random.Random(42)
np.random.seed(42)


def hex_srgb(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)])


def srgb_para_linear(c):
    c = np.asarray(c, dtype=float)
    return np.where(c <= 0.04045, c / 12.92, ((c + 0.055) / 1.055) ** 2.4)


# =============================================================== 1. onde NÃO pode ter cratera
IGNORAR = ("Rocha_", "Detrito_", "Terreno_Lua", "Terra_", "REF_")
zonas = []
for o in D.objects:
    if o.name.startswith(IGNORAR):
        continue
    if o.type == "MESH":
        cs = [o.matrix_world @ Vector(c) for c in o.bound_box]
        m = 4.0
        zonas.append((min(c.x for c in cs) - m, min(c.y for c in cs) - m, max(c.x for c in cs) + m, max(c.y for c in cs) + m))
    elif o.type == "EMPTY":
        p = o.matrix_world.translation
        zonas.append((p.x - 6, p.y - 6, p.x + 6, p.y + 6))
zonas.append((-7.0, -74.0, 7.0, -12.0))          # caminho do SPAWN_Player até a eclusa


def livre(x, y, R):
    for x0, y0, x1, y1 in zonas:
        dx = max(x0 - x, 0.0, x - x1)
        dy = max(y0 - y, 0.0, y - y1)
        if dx * dx + dy * dy < R * R:
            return False
    return True


# =============================================================== 2. sorteio das crateras
crateras = []


def semear(n, rmin, rmax, cx, cy, area_max, area_min=0.0):
    feitas = 0
    for _ in range(n * 500):
        if feitas >= n:
            break
        r = rmin * (rmax / rmin) ** rnd.random()                 # mais crateras pequenas que grandes
        a = rnd.random() * 2 * math.pi
        d = math.sqrt(rnd.uniform(area_min ** 2, area_max ** 2))
        x, y = cx + math.cos(a) * d, cy + math.sin(a) * d
        if not (-690 < x < 715 and -700 < y < 700):
            continue
        if not livre(x, y, 1.9 * r):
            continue
        if any((x - c["x"]) ** 2 + (y - c["y"]) ** 2 < (0.85 * (r + c["r"])) ** 2 for c in crateras):
            continue
        crateras.append(dict(x=x, y=y, r=r, k=rnd.uniform(0.85, 1.15),
                             f1=rnd.uniform(0, 6.283), f2=rnd.uniform(0, 6.283),
                             a1=rnd.uniform(0.12, 0.3), a2=rnd.uniform(0.05, 0.14)))
        feitas += 1
    return feitas


CX, CY = 25.0, -20.0
n_g = semear(9, 15, 28, CX, CY, 240, 55)
n_m = semear(32, 7, 13, CX, CY, 220)
n_p = semear(85, 3.2, 6.5, CX, CY, 190)
n_l = semear(14, 30, 75, CX, CY, 640, 300)
print(f"[Crateras] grandes={n_g} medias={n_m} pequenas={n_p} distantes={n_l} total={len(crateras)}")


def deslocamento(xs, ys):
    """altura extra das crateras nos pontos (xs, ys) — numpy"""
    xs = np.asarray(xs, dtype=float); ys = np.asarray(ys, dtype=float)
    dz = np.zeros_like(xs)
    for c in crateras:
        r = c["r"]; R = 2.0 * r
        dx = xs - c["x"]; dy = ys - c["y"]
        m = (np.abs(dx) < R) & (np.abs(dy) < R)
        if not m.any():
            continue
        ddx, ddy = dx[m], dy[m]
        t = np.sqrt(ddx * ddx + ddy * ddy) / r
        ang = np.arctan2(ddy, ddx)
        exagero = 1.25 if r < 7 else 1.0                           # cartoon: as pequenas mais "fundas" para ler de perto
        prof = 0.27 * r * c["k"] * exagero                         # profundidade da bacia
        borda = 0.10 * r * c["k"] * exagero * (1 + c["a1"] * np.sin(3 * ang + c["f1"]) + c["a2"] * np.sin(7 * ang + c["f2"]))
        u = np.clip(1.0 - t * t, 0.0, None)
        if r > 12:
            u = np.minimum(1.0, u * 1.5)                           # fundo plano nas grandes
        bacia = -prof * u ** 1.4
        sig = np.where(t < 1.0, 0.19, 0.40)                        # borda íngreme por dentro, suave por fora
        z = bacia + borda * np.exp(-((t - 1.0) / sig) ** 2)
        if r > 18:
            z = z + 0.3 * prof * np.exp(-(t / 0.15) ** 2)          # pico central
        z[t > 2.0] = 0.0
        dz[m] += z
    return dz


# =============================================================== 3. refinar o terreno perto das crateras
ter = D.objects["Terreno_Lua"]
me = ter.data
bm = bmesh.new(); bm.from_mesh(me)
bm.faces.ensure_lookup_table()
centros = np.array([f.calc_center_median()[:2] for f in bm.faces])
cortes = np.zeros(len(bm.faces), dtype=int)
for c in crateras:
    if c["r"] >= 30:
        continue                                                  # distantes: a grade de 4 m já basta
    R = 2.0 * c["r"] + 4.0
    m = (centros[:, 0] - c["x"]) ** 2 + (centros[:, 1] - c["y"]) ** 2 < R * R
    cortes[m] = np.maximum(cortes[m], 3 if c["r"] < 14 else 1)
f3 = [bm.faces[i] for i in np.nonzero(cortes == 3)[0]]
f1 = [bm.faces[i] for i in np.nonzero(cortes == 1)[0]]
f0 = set(bm.faces[i] for i in np.nonzero(cortes == 0)[0])
set1 = set(f1)
print(f"[Terreno] faces originais={len(bm.faces)} refino1m={len(f3)} refino2m={len(f1)}")
if f3:
    bmesh.ops.subdivide_edges(bm, edges=list({e for f in f3 for e in f.edges}), cuts=3, use_grid_fill=True)
if f1:
    arestas = [e for f in f1 if f.is_valid for e in f.edges if all((lf in set1) or (lf in f0) for lf in e.link_faces)]
    bmesh.ops.subdivide_edges(bm, edges=list(set(arestas)), cuts=1, use_grid_fill=True)
ngons = [f for f in bm.faces if len(f.verts) > 4]
bmesh.ops.triangulate(bm, faces=ngons, quad_method="BEAUTY", ngon_method="BEAUTY")
for f in bm.faces:
    f.smooth = True
bm.to_mesh(me); bm.free()

co = np.empty(len(me.vertices) * 3); me.vertices.foreach_get("co", co); co = co.reshape(-1, 3)
co[:, 2] += deslocamento(co[:, 0], co[:, 1])
me.vertices.foreach_set("co", co.ravel()); me.update()
print(f"[Terreno] verts={len(me.vertices)} faces={len(me.polygons)}")

# =============================================================== 4. rochas/detritos seguem o relevo + pedregulhos nas bordas
movidos = 0
for o in D.objects:
    if o.name.startswith(("Rocha_", "Detrito_")):
        cs = [o.matrix_world @ Vector(c) for c in o.bound_box]
        cx = sum(c.x for c in cs) / 8; cy = sum(c.y for c in cs) / 8
        dz = float(deslocamento([cx], [cy])[0])
        if abs(dz) > 1e-4:
            o.location.z += dz; movidos += 1
print(f"[Rochas] {movidos} rochas/detritos acompanharam o relevo")

dg = bpy.context.evaluated_depsgraph_get()
bvh = BVHTree.FromObject(ter, dg)
malhas_rocha = sorted({o.data for o in D.objects if o.name.startswith("Rocha_0")}, key=lambda m: m.name)
col_rochas = next((o.users_collection[0] for o in D.objects if o.name.startswith("Rocha_0")), bpy.context.scene.collection)
n_borda = 0
for c in crateras:
    if c["r"] < 9 or c["r"] >= 30:
        continue
    for _ in range(int(c["r"] * 0.6)):
        a = rnd.uniform(0, 2 * math.pi); t = rnd.uniform(0.95, 1.35)
        x = c["x"] + math.cos(a) * c["r"] * t; y = c["y"] + math.sin(a) * c["r"] * t
        hit = bvh.ray_cast(Vector((x, y, 400.0)), Vector((0, 0, -1)))
        if hit[0] is None:
            continue
        s = rnd.uniform(0.35, 0.9)
        o = D.objects.new(f"Rocha_Borda_{n_borda:03d}", rnd.choice(malhas_rocha))
        o.scale = (s, s, s * rnd.uniform(0.7, 1.1))
        o.rotation_euler = (0, 0, rnd.uniform(0, 6.283))
        o.location = (x, y, hit[0].z - 0.25 * s)
        col_rochas.objects.link(o)
        n_borda += 1
print(f"[Rochas] {n_borda} pedregulhos nas bordas das crateras")

# =============================================================== 5. Terra cartoon
TW, TH = 2048, 1024


def subir2(a):
    """bilinear 2x, com volta em X (longitude)"""
    h, w = a.shape
    ax = np.empty((h, w * 2))
    ax[:, 0::2] = 0.75 * a + 0.25 * np.roll(a, 1, axis=1)
    ax[:, 1::2] = 0.75 * a + 0.25 * np.roll(a, -1, axis=1)
    cima = np.vstack([ax[:1], ax[:-1]]); baixo = np.vstack([ax[1:], ax[-1:]])
    ay = np.empty((h * 2, w * 2))
    ay[0::2] = 0.75 * ax + 0.25 * cima
    ay[1::2] = 0.75 * ax + 0.25 * baixo
    return ay


def campo(escala, oitavas, deslocar, deformar=0.0):
    w, h = TW // 2, TH // 2
    lon = (np.arange(w) + 0.5) / w * 2 * math.pi - math.pi
    lat = (np.arange(h) + 0.5) / h * math.pi - math.pi / 2
    out = np.empty((h, w))
    off = Vector(deslocar)
    for j in range(h):
        cl, sl = math.cos(lat[j]), math.sin(lat[j])
        for i in range(w):
            p = Vector((cl * math.cos(lon[i]), cl * math.sin(lon[i]), sl)) * escala + off
            if deformar:
                p = p + Vector(noise.noise_vector(p * 0.5)) * deformar
            out[j, i] = noise.fractal(p, 0.9, 2.0, oitavas, noise_basis="PERLIN_ORIGINAL")
    return subir2(out)


lon = ((np.arange(TW) + 0.5) / TW * 2 * math.pi - math.pi)[None, :].repeat(TH, 0)
lat = ((np.arange(TH) + 0.5) / TH * math.pi - math.pi / 2)[:, None].repeat(TW, 1)
alat = np.degrees(np.abs(lat))

cont = campo(1.4, 6, (3.1, 7.7, 1.3), 0.8)
umid = campo(2.2, 4, (11.0, 2.0, 5.0))
thr = np.quantile(cont, 0.66)                                     # ~34% de terra
elev = cont - thr
terra = elev > 0

C = lambda h: hex_srgb(h)
img = np.zeros((TH, TW, 3))
img[:] = C("#2A6FE0")                                             # oceano
img[elev < -0.12] = C("#1D4FC4")                                  # oceano fundo
img[(elev > -0.035) & ~terra] = C("#49B6F2")                      # águas rasas
um = (umid - umid.mean()) / (umid.std() + 1e-6)
img[terra] = C("#4CC35A")                                         # campo
img[terra & (um > 0.55)] = C("#2F9A4A")                           # floresta
img[terra & (alat < 13) & (um < -0.2)] = C("#A8C94A")             # savana
img[terra & (alat > 13) & (alat < 33) & (um < -0.1)] = C("#F0BE52")   # deserto
img[terra & (elev > 0.17)] = C("#9C7556")                         # montanha
img[terra & (elev > 0.25)] = C("#F5FAFF")                         # neve no topo
gelo = alat > (69 + 5 * np.sin(lon * 3) + 3 * um)
img[gelo] = C("#EAF6FF")                                          # calotas polares
# litoral: areia do lado da terra, linha escura do lado do mar (traço de desenho)
viz = np.zeros_like(terra)
for ax_, sh in ((0, 1), (0, -1), (1, 1), (1, -1)):
    viz |= np.roll(terra, sh, axis=ax_)
borda_mar = viz & ~terra & ~gelo
img[borda_mar] = C("#1F3E9A")
viz2 = np.zeros_like(terra)
for ax_, sh in ((0, 1), (0, -1), (1, 1), (1, -1)):
    viz2 |= ~np.roll(terra, sh, axis=ax_)
img[terra & viz2 & ~gelo & (elev < 0.17)] = C("#F6DE9A")

nuv = campo(2.6, 5, (5.5, 9.1, 2.2), 1.2)
nthr = np.quantile(nuv, 0.74)
alfa = np.clip((nuv - nthr) / 0.05, 0, 1) * 0.95
alfa *= np.clip(1.2 - alat / 90.0, 0.3, 1.0)

def salvar_png(nome, rgb, a=None):
    h, w = rgb.shape[:2]
    im = D.images.get(nome) or D.images.new(nome, w, h, alpha=a is not None)
    px = np.ones((h, w, 4))
    px[..., :3] = rgb
    if a is not None:
        px[..., 3] = a
    im.pixels.foreach_set(px.astype(np.float32).ravel())
    if DIR_TEX:
        os.makedirs(DIR_TEX, exist_ok=True)
        im.filepath_raw = os.path.join(DIR_TEX, nome + ".png")
        im.file_format = "PNG"
        im.save()
    im.pack()
    return im

img_terra = salvar_png("T_Terra_Toon", img)
img_nuvem = salvar_png("T_TerraNuvens_Toon", np.ones((TH, TW, 3)), alfa)

# esfera com UV equirretangular (igual ao mapa gerado acima)
def esfera(nome, raio, mat):
    b = bmesh.new()
    bmesh.ops.create_uvsphere(b, u_segments=96, v_segments=48, radius=raio)
    uvl = b.loops.layers.uv.new("UVMap")
    for f in b.faces:
        us = []
        for l in f.loops:
            p = l.vert.co.normalized()
            us.append([math.atan2(p.y, p.x) / (2 * math.pi) + 0.5, math.asin(max(-1.0, min(1.0, p.z))) / math.pi + 0.5])
        if max(u[0] for u in us) - min(u[0] for u in us) > 0.5:
            for u in us:
                if u[0] < 0.5:
                    u[0] += 1.0
        polo = [abs(l.vert.co.normalized().z) > 0.9999 for l in f.loops]
        outros = [us[i][0] for i in range(len(us)) if not polo[i]]
        for i in range(len(us)):
            if polo[i] and outros:
                us[i][0] = sum(outros) / len(outros)
        for l, u in zip(f.loops, us):
            l[uvl].uv = u
        f.smooth = True
    m = D.meshes.new(nome); b.to_mesh(m); b.free()
    m.materials.append(mat)
    return m


def material(nome):
    m = D.materials.get(nome) or D.materials.new(nome)
    m.use_nodes = True
    return m

mt = material("M_Terra")
nt = mt.node_tree; nt.nodes.clear()
tex = nt.nodes.new("ShaderNodeTexImage"); tex.image = img_terra
bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled"); outp = nt.nodes.new("ShaderNodeOutputMaterial")
nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"]); nt.links.new(tex.outputs["Color"], bsdf.inputs["Emission Color"])
bsdf.inputs["Emission Strength"].default_value = 0.35; bsdf.inputs["Roughness"].default_value = 0.6
nt.links.new(bsdf.outputs[0], outp.inputs[0])

mn = material("M_Terra_Nuvens")
nt = mn.node_tree; nt.nodes.clear()
tex = nt.nodes.new("ShaderNodeTexImage"); tex.image = img_nuvem
bsdf = nt.nodes.new("ShaderNodeBsdfPrincipled"); outp = nt.nodes.new("ShaderNodeOutputMaterial")
bsdf.inputs["Base Color"].default_value = (1, 1, 1, 1)
nt.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"]); nt.links.new(bsdf.outputs[0], outp.inputs[0])
if hasattr(mn, "surface_render_method"):
    mn.surface_render_method = "BLENDED"

ma = material("M_Terra_Atmosfera")
nt = ma.node_tree; nt.nodes.clear()
lw = nt.nodes.new("ShaderNodeLayerWeight"); lw.inputs["Blend"].default_value = 0.25
em = nt.nodes.new("ShaderNodeEmission"); em.inputs["Color"].default_value = (*srgb_para_linear(hex_srgb("#7FD4FF")), 1); em.inputs["Strength"].default_value = 2.0
tr = nt.nodes.new("ShaderNodeBsdfTransparent"); mix = nt.nodes.new("ShaderNodeMixShader"); outp = nt.nodes.new("ShaderNodeOutputMaterial")
nt.links.new(lw.outputs["Facing"], mix.inputs[0]); nt.links.new(tr.outputs[0], mix.inputs[1]); nt.links.new(em.outputs[0], mix.inputs[2])
nt.links.new(mix.outputs[0], outp.inputs[0])

terra_obj = D.objects["Terra_Ceu"]
pos = terra_obj.location.copy()
RAIO = 240.0                                                      # antes 90 m: maior no céu (≈8°)
velha = terra_obj.data
terra_obj.data = esfera("Terra_Ceu", RAIO, mt)
terra_obj.scale = (1, 1, 1)
if velha.users == 0:
    D.meshes.remove(velha)
col_terra = terra_obj.users_collection[0]
extras = []
for nome, raio, mat in (("Terra_Nuvens", RAIO * 1.025, mn), ("Terra_Atmosfera", RAIO * 1.10, ma)):
    o = D.objects.get(nome) or D.objects.new(nome, esfera(nome, raio, mat))
    o.location = pos
    if o.name not in col_terra.objects:
        col_terra.objects.link(o)
    extras.append(o)

# gira a Terra para os continentes ficarem virados para a base
w = np.cos(lat)
P = np.stack([np.cos(lat) * np.cos(lon), np.cos(lat) * np.sin(lon), np.sin(lat)], -1)
L = (P[terra & (alat < 55)] * w[terra & (alat < 55)][:, None]).sum(0)
d = -(pos.normalized())
yaw = math.atan2(d.y, d.x) - math.atan2(L[1], L[0])
for o in [terra_obj] + extras:
    o.rotation_euler = (math.radians(12), 0.0, yaw)
print(f"[Terra] raio={RAIO} terra%={terra.mean() * 100:.1f} nuvens%={(alfa > 0.5).mean() * 100:.1f}")

# =============================================================== 6. exportar e salvar
if SAIDA_FBX:
    bpy.ops.object.select_all(action="DESELECT")
    sel = [o for o in D.objects if (o.type == "MESH" and not o.name.startswith("REF_")) or o.type == "EMPTY"]
    for o in sel:
        o.select_set(True)
    bpy.context.view_layer.objects.active = sel[0]
    bpy.ops.export_scene.fbx(filepath=os.path.abspath(SAIDA_FBX), use_selection=True, object_types={"MESH", "EMPTY"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=False, mesh_smooth_type="OFF", use_mesh_modifiers=True,
                             add_leaf_bones=False, path_mode="AUTO", embed_textures=False)
    print("[Export] FBX:", os.path.abspath(SAIDA_FBX), "objetos:", len(sel))
if SAIDA_BLEND:
    bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(SAIDA_BLEND), compress=True)
    print("[Export] BLEND:", os.path.abspath(SAIDA_BLEND))
