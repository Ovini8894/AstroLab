"""
Menu Inicial — cenário 3D do fundo do menu (estilo cartoon da Base Lunar).

Uso (gera tudo do zero, não precisa de .blend de entrada):
  blender -b --factory-startup --python menu_lua.py -- <saida.fbx> <saida.blend|-> <emblema.png|->

Gera:
- Lua_Superficie: calota de uma "Lua pequena" (raio 70 m) vista de cima — horizonte curvo cobrindo a base da
  tela. Só a faixa que a câmera do menu enxerga é modelada (sem faces de trás/de baixo, sem collider).
  Crateras com o mesmo perfil do crateras_e_terra.py (bacia, borda irregular, fundo plano, pico central).
- Lua_Rochas: pedregulhos chanfrados espalhados nas bordas das crateras.
- Bandeira_Mastro: mastro (o tecido que balança é gerado no Unity, em BandeiraOndulante.cs).
- Terra / Terra_Nuvens / Terra_Atmosfera: esferas de raio 1 com UV equirretangular (texturas T_Terra_Toon).
- Marcadores: CAM_Menu (câmera), CAM_Alvo (para onde ela olha), BANDEIRA_Topo (ponta do mastro).
- emblema.png: emblema da bandeira (textura separada e trocável no Unity).

Coordenadas (Blender, Z para cima): o pé da câmera fica na origem, a câmera olha para +Y.
"""
import bpy, bmesh, math, random, os, sys
import numpy as np
from mathutils import Vector, noise

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
SAIDA_FBX = argv[0] if len(argv) > 0 and argv[0] != "-" else None
SAIDA_BLEND = argv[1] if len(argv) > 1 and argv[1] != "-" else None
SAIDA_EMBLEMA = argv[2] if len(argv) > 2 and argv[2] != "-" else None

D = bpy.data
rnd = random.Random(7)

# limpa a cena padrão
for o in list(D.objects):
    D.objects.remove(o, do_unlink=True)
for m in list(D.meshes):
    D.meshes.remove(m)

R = 95.0                    # raio da Lua do menu (curva suave no horizonte)
H_CAM = 14.0                # altura da câmera sobre o chão
CENTRO = Vector((0.0, 0.0, -R))
TETA_MAX = 0.555            # rad (~31,8°) — um pouco além do horizonte (acos(95/109) ≈ 29,4°)
PASSO = 0.22                # m entre vértices da grade

BANDEIRA_XY = (-10.6, 23.8)  # à esquerda, ~26 m da câmera


# =============================================================== crateras
crateras = []


def cabe(x, y, r):
    if (x - BANDEIRA_XY[0]) ** 2 + (y - BANDEIRA_XY[1]) ** 2 < (r * 1.6 + 3.0) ** 2:
        return False
    return not any((x - c["x"]) ** 2 + (y - c["y"]) ** 2 < (0.85 * (r + c["r"])) ** 2 for c in crateras)


def semear(n, rmin, rmax, dmin, dmax, amin=-70, amax=70):
    feitas = 0
    for _ in range(n * 400):
        if feitas >= n:
            break
        r = rmin * (rmax / rmin) ** rnd.random()
        a = math.radians(rnd.uniform(amin, amax))
        d = math.sqrt(rnd.uniform(dmin ** 2, dmax ** 2))
        x, y = math.sin(a) * d, math.cos(a) * d
        if not cabe(x, y, r):
            continue
        crateras.append(dict(x=x, y=y, r=r, k=rnd.uniform(0.85, 1.15),
                             f1=rnd.uniform(0, 6.283), f2=rnd.uniform(0, 6.283),
                             a1=rnd.uniform(0.12, 0.3), a2=rnd.uniform(0.05, 0.14)))
        feitas += 1
    return feitas


# grandes perto do horizonte (bordas recortam a silhueta), médias e pequenas no meio
crateras.append(dict(x=13.0, y=33.0, r=8.0, k=1.0, f1=1.0, f2=2.0, a1=0.2, a2=0.08, destaque=True))     # sob a Terra
crateras.append(dict(x=-22.0, y=29.0, r=6.5, k=1.0, f1=3.0, f2=0.5, a1=0.25, a2=0.1, destaque=True))  # atrás da bandeira
crateras.append(dict(x=1.5, y=22.0, r=5.0, k=1.0, f1=0.3, f2=4.0, a1=0.18, a2=0.06, destaque=True))    # centro
n_g = semear(4, 3.5, 5.0, 24, 40)
n_m = semear(30, 1.8, 3.6, 13, 40)
n_p = semear(80, 0.5, 1.5, 11, 42)
print(f"[Crateras] total={len(crateras)} (destaque=3, grandes={n_g}, médias={n_m}, pequenas={n_p})")


def deslocamento(xs, ys):
    """altura extra (m) nos pontos (xs, ys) — mesmo perfil do crateras_e_terra.py, exagerado p/ cartoon"""
    xs = np.asarray(xs, dtype=float); ys = np.asarray(ys, dtype=float)
    dz = np.zeros_like(xs)
    for c in crateras:
        r = c["r"]; Rr = 2.0 * r
        dx = xs - c["x"]; dy = ys - c["y"]
        m = (np.abs(dx) < Rr) & (np.abs(dy) < Rr)
        if not m.any():
            continue
        ddx, ddy = dx[m], dy[m]
        t = np.sqrt(ddx * ddx + ddy * ddy) / r
        ang = np.arctan2(ddy, ddx)
        f = 0.62 if c.get("destaque") else 0.40                    # ~40% mais rasas; só 3 em destaque
        prof = 0.30 * r * c["k"] * f
        borda = 0.075 * r * c["k"] * f * 1.6 * (1 + c["a1"] * np.sin(3 * ang + c["f1"]) + c["a2"] * np.sin(7 * ang + c["f2"]))
        u = np.clip(1.0 - t * t, 0.0, None)
        if r > 5:
            u = np.minimum(1.0, u * 1.5)
        bacia = -prof * u ** 1.4
        sig = np.where(t < 1.0, 0.17, 0.42)
        z = bacia + borda * np.exp(-((t - 1.0) / sig) ** 2)
        if c.get("destaque") and r > 6.0:
            z = z + 0.32 * prof * np.exp(-(t / 0.16) ** 2)
        z[t > 2.0] = 0.0
        dz[m] += z
    return dz


def ondulacao(xs, ys):
    """relevo largo e suave entre as crateras (o Toon pega meio-tom nas encostas)"""
    out = np.empty(len(xs))
    for i, (x, y) in enumerate(zip(xs, ys)):
        p = Vector((x * 0.018, y * 0.018, 3.3))
        out[i] = noise.fractal(p, 0.5, 2.0, 3, noise_basis="PERLIN_ORIGINAL") * 0.4
    return out


def na_esfera(x, y, dz):
    """plano tangente (azimutal equidistante) -> esfera de raio R + dz"""
    rho = math.hypot(x, y)
    th = rho / R
    if rho < 1e-9:
        d = Vector((0, 0, 1))
    else:
        d = Vector((math.sin(th) * x / rho, math.sin(th) * y / rho, math.cos(th)))
    return CENTRO + d * (R + dz)


# =============================================================== superfície
xs_g = np.arange(-62.0, 62.0 + 1e-6, PASSO)
ys_g = np.arange(6.0, 64.0 + 1e-6, PASSO)
NX, NY = len(xs_g), len(ys_g)
GX, GY = np.meshgrid(xs_g, ys_g)            # [NY, NX]
fx, fy = GX.ravel(), GY.ravel()
rho = np.hypot(fx, fy)
az = np.degrees(np.arctan2(fx, fy))
dentro = (rho / R < TETA_MAX) & (np.abs(az) < 66)

dz = deslocamento(fx, fy) + ondulacao(fx, fy)
th = rho / R
with np.errstate(invalid="ignore", divide="ignore"):
    sx = np.where(rho > 1e-9, fx / rho, 0.0); sy = np.where(rho > 1e-9, fy / rho, 0.0)
dirs = np.stack([np.sin(th) * sx, np.sin(th) * sy, np.cos(th)], -1)
pts = np.array(CENTRO)[None, :] + dirs * (R + dz)[:, None]

idx = -np.ones(NX * NY, dtype=np.int64)
idx[dentro] = np.arange(dentro.sum())
verts = pts[dentro]
faces = []
I = idx.reshape(NY, NX)
a = I[:-1, :-1]; b = I[:-1, 1:]; c = I[1:, 1:]; d_ = I[1:, :-1]
ok = (a >= 0) & (b >= 0) & (c >= 0) & (d_ >= 0)
quads = np.stack([a[ok], b[ok], c[ok], d_[ok]], -1)

me = D.meshes.new("Lua_Superficie")
me.vertices.add(len(verts)); me.vertices.foreach_set("co", verts.astype(np.float32).ravel())
me.loops.add(len(quads) * 4); me.loops.foreach_set("vertex_index", quads.astype(np.int32).ravel())
me.polygons.add(len(quads))
me.polygons.foreach_set("loop_start", (np.arange(len(quads)) * 4).astype(np.int32))
me.update(calc_edges=True)
uv = me.uv_layers.new(name="UVMap")
uvs = np.stack([fx[dentro], fy[dentro]], -1)[quads.ravel()] / 3.0    # 1 UV a cada 3 m (material repete a 0,5)
uv.data.foreach_set("uv", uvs.astype(np.float32).ravel())
me.polygons.foreach_set("use_smooth", np.ones(len(quads), dtype=bool))
lua = D.objects.new("Lua_Superficie", me)
bpy.context.scene.collection.objects.link(lua)
print(f"[Lua] verts={len(me.vertices)} faces={len(me.polygons)}")


def altura(x, y):
    return float(deslocamento([x], [y])[0] + ondulacao([x], [y])[0])


# =============================================================== rochas
def rocha_malha(semente, sub=1):
    b = bmesh.new()
    bmesh.ops.create_icosphere(b, subdivisions=sub, radius=1.0)
    rr = random.Random(semente)
    off = Vector((rr.uniform(0, 50), rr.uniform(0, 50), rr.uniform(0, 50)))
    for v in b.verts:
        n = noise.noise(v.co * 1.3 + off)
        v.co *= 1.0 + 0.32 * n
        v.co.z *= 0.62
    return b


rochas_bm = bmesh.new()
LIMITE_ROCHAS = 40.0                                              # longe do horizonte (~53 m): silhueta limpa
n_rochas = 0


def por_rocha(x, y, s, semente):
    global n_rochas
    b = rocha_malha(semente, 1 if s < 0.5 else 2)
    rot = rnd.uniform(0, 6.283)
    z0 = altura(x, y)
    base = na_esfera(x, y, z0)
    up = (base - CENTRO).normalized()
    # base local: up = normal da esfera
    t1 = up.cross(Vector((0, 0, 1)))
    t1 = Vector((1, 0, 0)) if t1.length < 1e-6 else t1.normalized()
    t2 = up.cross(t1)
    cr, sr = math.cos(rot), math.sin(rot)
    for v in b.verts:
        lx, ly, lz = v.co.x * s, v.co.y * s, v.co.z * s * rnd.uniform(0.9, 1.1)
        rx, ry = lx * cr - ly * sr, lx * sr + ly * cr
        v.co = base + t1 * rx + t2 * ry + up * (lz - 0.28 * s)
    m = D.meshes.new("tmp"); b.to_mesh(m); b.free()
    rochas_bm.from_mesh(m); D.meshes.remove(m)
    n_rochas += 1


for c in crateras:
    if c["r"] < 2.0:
        continue
    for _ in range(int(c["r"] * 0.6)):
        a = rnd.uniform(0, 2 * math.pi); t = rnd.uniform(1.0, 1.5)
        x = c["x"] + math.cos(a) * c["r"] * t; y = c["y"] + math.sin(a) * c["r"] * t
        if math.hypot(x, y) > LIMITE_ROCHAS:
            continue
        por_rocha(x, y, rnd.uniform(0.12, 0.32) * (1.0 if c["r"] < 5 else 1.25), rnd.randrange(10 ** 6))
for _ in range(35):                                               # soltas pelo chão
    a = math.radians(rnd.uniform(-60, 60)); dd = rnd.uniform(14, LIMITE_ROCHAS)
    x, y = math.sin(a) * dd, math.cos(a) * dd
    if (x - BANDEIRA_XY[0]) ** 2 + (y - BANDEIRA_XY[1]) ** 2 < 4:
        continue
    por_rocha(x, y, rnd.uniform(0.1, 0.26), rnd.randrange(10 ** 6))
# uma pedra maior perto da bandeira (composição)
por_rocha(BANDEIRA_XY[0] - 2.4, BANDEIRA_XY[1] + 1.2, 0.5, 99)

me_r = D.meshes.new("Lua_Rochas")
rochas_bm.to_mesh(me_r); rochas_bm.free()
for p in me_r.polygons:
    p.use_smooth = False                                            # facetado: leitura cartoon
uvr = me_r.uv_layers.new(name="UVMap")
for loop in me_r.loops:
    co = me_r.vertices[loop.vertex_index].co
    uvr.data[loop.index].uv = (co.x * 0.7 + co.z * 0.3, co.y * 0.7 + co.z * 0.5)
o_r = D.objects.new("Lua_Rochas", me_r)
bpy.context.scene.collection.objects.link(o_r)
print(f"[Rochas] {n_rochas}")

# =============================================================== bandeira (estilo Apollo): mastro + haste + tecido
bx, by = BANDEIRA_XY
base = na_esfera(bx, by, altura(bx, by))
up = (base - CENTRO).normalized()
ALT_MASTRO = 4.6
FLAG_L, FLAG_H = 2.4, 1.55                                         # tecido (m)
CAM_POS = Vector((0.0, 0.0, H_CAM))

# direção em que o tecido se estende: para a direita da tela, um pouco voltado para a câmera
d = Vector((bx, by, 0.0)).normalized()                             # câmera -> bandeira (horizontal)
direita = Vector((d.y, -d.x, 0.0))
ang = math.radians(38.0)
e = (direita * math.cos(ang) - d * math.sin(ang)).normalized()
e = (e - up * e.dot(up)).normalized()                              # perpendicular ao mastro
frente = up.cross(e).normalized()
topo_pos = base + up * (ALT_MASTRO - 0.08)
if frente.dot(CAM_POS - topo_pos) < 0:
    frente = -frente


def cilindro(b, p0, p1, raio, seg=12):
    """cilindro de p0 a p1 (coordenadas do mundo, relativas à base do mastro)"""
    eixo = p1 - p0
    r = bmesh.ops.create_cone(b, cap_ends=True, segments=seg, radius1=raio, radius2=raio, depth=eixo.length)
    q = Vector((0, 0, 1)).rotation_difference(eixo.normalized())
    for v in r["verts"]:
        v.co = q @ v.co + (p0 + p1) / 2


b = bmesh.new()
cilindro(b, up * -0.3, up * ALT_MASTRO, 0.07)                     # mastro (30 cm enterrado)
r = bmesh.ops.create_cone(b, cap_ends=True, segments=12, radius1=0.11, radius2=0.06, depth=0.12)   # colar na base
q = Vector((0, 0, 1)).rotation_difference(up)
for v in r["verts"]:
    v.co = q @ v.co + up * 0.02
r = bmesh.ops.create_uvsphere(b, u_segments=12, v_segments=8, radius=0.075)                       # ponteira
for v in r["verts"]:
    v.co = v.co + up * (ALT_MASTRO + 0.04)
h0 = up * (ALT_MASTRO - 0.08)
cilindro(b, h0 - e * 0.02, h0 + e * (FLAG_L + 0.08), 0.035, 10)   # haste horizontal (Apollo)
r = bmesh.ops.create_uvsphere(b, u_segments=10, v_segments=6, radius=0.035)                       # tampa da haste
for v in r["verts"]:
    v.co = v.co + h0 + e * (FLAG_L + 0.08)
me_m = D.meshes.new("Bandeira_Mastro"); b.to_mesh(me_m); b.free()
for p_ in me_m.polygons:
    p_.use_smooth = True
me_m.uv_layers.new(name="UVMap")
mastro = D.objects.new("Bandeira_Mastro", me_m)
bpy.context.scene.collection.objects.link(mastro)
mastro.location = base

# tecido: grade 24x16 pendurada na haste, com dobras fixas (amassado das bandeiras Apollo).
# UV 1 = textura; UV 2 ("Param") = posição (u ao longo da haste, v de baixo=0 a cima=1) usada pela física no Unity.
NX, NY = 24, 16
O = h0 + e * 0.08 - up * 0.04                                      # canto de cima, junto ao mastro (relativo à base)


def dobras(u, v):
    x = u * FLAG_L; y = (1 - v) * FLAG_H
    return 0.035 * (math.sin(x * 7.3 + 0.6) * 0.6 + math.sin(x * 13.1 - y * 3.0) * 0.25) * (0.35 + 0.65 * (1 - v)) * (0.3 + 0.7 * u)


def pos_tecido(u, v):
    return O + e * (u * FLAG_L) - up * ((1 - v) * FLAG_H) + frente * dobras(u, v)


bm_t = bmesh.new()
uv_tex = bm_t.loops.layers.uv.new("UVMap")
uv_par = bm_t.loops.layers.uv.new("Param")
grade = [[bm_t.verts.new(pos_tecido(i / NX, j / NY)) for i in range(NX + 1)] for j in range(NY + 1)]
for j in range(NY):
    for i in range(NX):
        f = bm_t.faces.new((grade[j][i], grade[j][i + 1], grade[j + 1][i + 1], grade[j + 1][i]))
        f.material_index = 0
        for l, (jj, ii) in zip(f.loops, ((j, i), (j, i + 1), (j + 1, i + 1), (j + 1, i))):
            l[uv_tex].uv = (ii / NX, jj / NY); l[uv_par].uv = (ii / NX, jj / NY)

# emblema: quadrado no centro, 6 mm à frente do tecido (segue as mesmas dobras)
E = FLAG_H * 0.78
NE = 12
ge = []
for j in range(NE + 1):
    linha = []
    for i in range(NE + 1):
        u = 0.5 + (i / NE - 0.5) * E / FLAG_L
        v = 0.5 + (j / NE - 0.5) * E / FLAG_H
        linha.append((bm_t.verts.new(pos_tecido(u, v) + frente * 0.006), (i / NE, j / NE), (u, v)))
    ge.append(linha)
for j in range(NE):
    for i in range(NE):
        cant = (ge[j][i], ge[j][i + 1], ge[j + 1][i + 1], ge[j + 1][i])
        f = bm_t.faces.new([c[0] for c in cant])
        f.material_index = 1
        for l, c in zip(f.loops, cant):
            l[uv_tex].uv = c[1]; l[uv_par].uv = c[2]
bm_t.normal_update()
for f in bm_t.faces:                                               # frente do tecido virada para a câmera
    if f.normal.dot(frente) < 0:
        f.normal_flip()
for f in bm_t.faces:
    f.smooth = True
me_t = D.meshes.new("Bandeira_Tecido"); bm_t.to_mesh(me_t); bm_t.free()
for nome in ("Tecido", "Emblema"):
    me_t.materials.append(D.materials.get(nome) or D.materials.new(nome))
tecido = D.objects.new("Bandeira_Tecido", me_t)
bpy.context.scene.collection.objects.link(tecido)
tecido.location = base

topo = D.objects.new("BANDEIRA_Topo", None)
topo.location = topo_pos
bpy.context.scene.collection.objects.link(topo)

# =============================================================== câmera (marcadores)
cam = D.objects.new("CAM_Menu", None); cam.location = (0.0, 0.0, H_CAM)
alvo = D.objects.new("CAM_Alvo", None); alvo.location = (0.0, 60.0, H_CAM - 60.0 * math.tan(math.radians(19.5)))
for o in (cam, alvo):
    bpy.context.scene.collection.objects.link(o)


# =============================================================== Terra (raio 1; posição/escala no Unity)
def esfera(nome, raio):
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
    o = D.objects.new(nome, m)
    bpy.context.scene.collection.objects.link(o)
    return o


esfera("Terra", 1.0)
esfera("Terra_Nuvens", 1.025)
esfera("Terra_Atmosfera", 1.10)

# =============================================================== preview (vista da câmera do menu)
SAIDA_PREVIEW = argv[3] if len(argv) > 3 and argv[3] != "-" else None
if SAIDA_PREVIEW:
    sc = bpy.context.scene
    camd = D.cameras.new("PreviewCam"); camd.sensor_fit = "VERTICAL"; camd.angle = math.radians(42.0); camd.clip_end = 3000
    camo = D.objects.new("PreviewCam", camd); sc.collection.objects.link(camo)
    camo.location = cam.location
    camo.rotation_mode = "QUATERNION"
    camo.rotation_quaternion = (alvo.location - cam.location).to_track_quat("-Z", "Y")
    sc.camera = camo
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "STUDIO"; sc.display.shading.color_type = "SINGLE"
    sc.display.shading.single_color = (0.66, 0.65, 0.75)
    sc.display.shading.show_shadows = True; sc.display.shading.show_cavity = True
    sc.display.light_direction = (-0.8, 0.2, 0.35)
    sc.render.resolution_x, sc.render.resolution_y = 1280, 720
    for n in ("Terra", "Terra_Nuvens", "Terra_Atmosfera"):
        D.objects[n].hide_render = True
    sc.render.filepath = os.path.abspath(SAIDA_PREVIEW)
    bpy.ops.render.render(write_still=True)
    for n in ("Terra", "Terra_Nuvens", "Terra_Atmosfera"):
        D.objects[n].hide_render = False
    D.objects.remove(camo)
    print("[Preview]", os.path.abspath(SAIDA_PREVIEW))

# =============================================================== exportar
if SAIDA_FBX:
    bpy.ops.object.select_all(action="DESELECT")
    for o in D.objects:
        o.select_set(True)
    bpy.context.view_layer.objects.active = lua
    os.makedirs(os.path.dirname(os.path.abspath(SAIDA_FBX)), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=os.path.abspath(SAIDA_FBX), use_selection=True, object_types={"MESH", "EMPTY"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=False, mesh_smooth_type="OFF", use_mesh_modifiers=True,
                             add_leaf_bones=False, path_mode="AUTO", embed_textures=False)
    print("[Export] FBX:", os.path.abspath(SAIDA_FBX))
if SAIDA_BLEND:
    bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(SAIDA_BLEND), compress=True)
    print("[Export] BLEND:", os.path.abspath(SAIDA_BLEND))


# =============================================================== emblema (textura separada, trocável)
def emblema(caminho, N=1024):
    """Emblema estilizado de agência espacial: disco azul, estrelas, órbita branca, faixa vermelha e 'NASA'.
    Desenhado em numpy (formas) + texto renderizado pelo Blender (fonte padrão)."""
    yy, xx = np.mgrid[0:N, 0:N]
    u = (xx + 0.5) / N * 2 - 1
    v = (yy + 0.5) / N * 2 - 1
    rgba = np.zeros((N, N, 4))

    def pinta(mask_suave, cor):
        a = np.clip(mask_suave, 0, 1)[..., None]
        c = np.array([*cor, 1.0])
        rgba[:] = rgba * (1 - a) + c * a

    px = 2.0 / N
    rr = np.hypot(u, v)
    disco = np.clip((0.86 - rr) / px, 0, 1)
    pinta(disco, (0.043, 0.239, 0.569))                      # #0B3D91
    pinta(disco * np.clip((rr - 0.80) / px, 0, 1), (0.035, 0.18, 0.45))   # aro

    # estrelas
    sr = random.Random(3)
    for _ in range(26):
        while True:
            x0, y0 = sr.uniform(-0.75, 0.75), sr.uniform(-0.75, 0.75)
            if math.hypot(x0, y0) < 0.74:
                break
        rs = sr.uniform(0.008, 0.02)
        pinta(np.clip((rs - np.hypot(u - x0, v - y0)) / px, 0, 1), (1, 1, 1))

    # órbita (elipse inclinada, só o contorno)
    ang = math.radians(-25)
    ca, sa = math.cos(ang), math.sin(ang)
    ue, ve = u * ca - v * sa, u * sa + v * ca
    el = np.hypot(ue / 0.78, ve / 0.26)
    pinta(np.clip((0.022 - np.abs(el - 1.0) * 0.26) / px, 0, 1) * (rr < 0.95), (1, 1, 1))

    # faixa vermelha (vetor curvo atravessando o disco, pontas para fora)
    curva = v - (0.55 * u + 0.25 * u * u * u - 0.05)
    larg = 0.055 * (1 - 0.6 * np.abs(u))
    faixa = np.clip((larg - np.abs(curva)) / px, 0, 1) * (np.abs(u) < 0.98)
    pinta(faixa, (0.988, 0.239, 0.129))                      # #FC3D21

    # texto "NASA" renderizado pelo Blender
    cena = bpy.data.scenes.new("Emblema")
    cena.render.resolution_x = N; cena.render.resolution_y = N
    cena.render.film_transparent = True
    cena.render.engine = "BLENDER_WORKBENCH"
    cena.display.shading.light = "FLAT"
    cena.display.shading.color_type = "SINGLE"
    cena.display.shading.single_color = (1, 1, 1)
    cena.view_settings.view_transform = "Standard"
    txt_c = bpy.data.curves.new("NASA", "FONT"); txt_c.body = "NASA"
    txt_c.align_x = "CENTER"; txt_c.align_y = "CENTER"; txt_c.size = 0.52
    txt_c.space_character = 1.05; txt_c.extrude = 0.0
    txt = bpy.data.objects.new("NASA", txt_c); cena.collection.objects.link(txt)
    txt.location = (0, -0.01, 0)
    camd = bpy.data.cameras.new("CamEmblema"); camd.type = "ORTHO"; camd.ortho_scale = 2.0
    camo = bpy.data.objects.new("CamEmblema", camd); camo.location = (0, 0, 5); cena.collection.objects.link(camo)
    cena.camera = camo
    tmp = os.path.join(os.path.dirname(os.path.abspath(caminho)), "_emblema_texto.png")
    cena.render.filepath = tmp
    cena.render.image_settings.file_format = "PNG"; cena.render.image_settings.color_mode = "RGBA"
    bpy.ops.render.render(write_still=True, scene=cena.name)
    im = bpy.data.images.load(tmp)
    t = np.array(im.pixels[:]).reshape(im.size[1], im.size[0], 4)
    a_txt = t[..., 3]
    if a_txt.shape != (N, N):
        a_txt = np.zeros((N, N))
    # sombra do texto (deslocada) + texto branco
    sombra = np.roll(np.roll(a_txt, -int(N * 0.006), 0), int(N * 0.006), 1)
    pinta(sombra * disco * 0.55, (0.02, 0.08, 0.22))
    pinta(a_txt * disco, (1, 1, 1))
    bpy.data.images.remove(im)
    os.remove(tmp)

    out = bpy.data.images.new("Emblema", N, N, alpha=True)
    out.pixels.foreach_set(rgba.astype(np.float32).ravel())
    out.filepath_raw = os.path.abspath(caminho)
    out.file_format = "PNG"
    out.save()
    print("[Emblema]", os.path.abspath(caminho))


if SAIDA_EMBLEMA:
    os.makedirs(os.path.dirname(os.path.abspath(SAIDA_EMBLEMA)), exist_ok=True)
    emblema(SAIDA_EMBLEMA)
