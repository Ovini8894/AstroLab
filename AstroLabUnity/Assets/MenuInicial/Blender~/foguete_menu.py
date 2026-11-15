"""
Foguete do menu (botão Iniciar) — conjunto do SPACE SHUTTLE (o mesmo veículo do jogo), low-poly cartoon, sem logos.

Uso:  blender -b --factory-startup --python foguete_menu.py -- <saida.fbx> [<saida.blend|->] [<preview.png|->]

Peças (um objeto "Foguete", 3 materiais: Laranja, Branco, Escuro):
- estágio central laranja com faixas escuras e 4 motores (RS-25) embaixo;
- 2 boosters brancos laterais com nariz cônico, faixas escuras e bocal;
- adaptador + estágio superior brancos, cápsula Orion (cone) e torre de escape (LAS) no topo.
Marcadores: FOGUETE_Base (pé dos motores) e FOGUETE_Ponta (topo) — o Unity usa os dois para achar o eixo.
Unidades: altura total ≈ 10 (o Unity escala).
"""
import bpy, bmesh, math, os, sys
from mathutils import Vector

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
SAIDA_FBX = argv[0] if len(argv) > 0 else None
SAIDA_BLEND = argv[1] if len(argv) > 1 and argv[1] != "-" else None
SAIDA_PREVIEW = argv[2] if len(argv) > 2 and argv[2] != "-" else None

D = bpy.data
for o in list(D.objects):
    D.objects.remove(o, do_unlink=True)

LARANJA, BRANCO, ESCURO = 0, 1, 2
bm = bmesh.new()
SEG = 16


def anel_perfil(perfil, mat, cx=0.0, cy=0.0, seg=SEG):
    """sólido de revolução: perfil = [(raio, z), ...] de baixo para cima; tampa em cima/embaixo se raio > 0"""
    aneis = []
    for r, z in perfil:
        anel = []
        for i in range(seg):
            a = 2 * math.pi * i / seg
            anel.append(bm.verts.new((cx + r * math.cos(a), cy + r * math.sin(a), z)))
        aneis.append(anel)
    faces = []
    for k in range(len(aneis) - 1):
        for i in range(seg):
            j = (i + 1) % seg
            f = bm.faces.new((aneis[k][i], aneis[k][j], aneis[k + 1][j], aneis[k + 1][i]))
            f.material_index = mat if not isinstance(mat, (list, tuple)) else mat[k]
            faces.append(f)
    if perfil[0][0] > 1e-4:
        f = bm.faces.new(list(reversed(aneis[0]))); f.material_index = mat if not isinstance(mat, (list, tuple)) else mat[0]
    if perfil[-1][0] > 1e-4:
        f = bm.faces.new(aneis[-1]); f.material_index = mat if not isinstance(mat, (list, tuple)) else mat[-1]
    return faces


def prisma(pontos2d, eixo_u, eixo_v, normal, espessura, mat, origem, mat_borda=None, bordas_pretas=()):
    """placa poligonal extrudada (asas, deriva). pontos2d em (u, v); bordas_pretas = índices de arestas pretas"""
    o = Vector(origem)
    f0 = [bm.verts.new(o + eixo_u * u + eixo_v * v - normal * espessura / 2) for u, v in pontos2d]
    f1 = [bm.verts.new(o + eixo_u * u + eixo_v * v + normal * espessura / 2) for u, v in pontos2d]
    a = bm.faces.new(list(reversed(f0))); a.material_index = mat
    b = bm.faces.new(f1); b.material_index = mat
    n = len(pontos2d)
    for k in range(n):
        m = (k + 1) % n
        f = bm.faces.new((f0[k], f0[m], f1[m], f1[k]))
        f.material_index = mat_borda if (mat_borda is not None and k in bordas_pretas) else mat


# ---------------------------------------------------------------- tanque externo (laranja)
RT = 0.42
anel_perfil([(0.3, 0.55), (RT, 0.7), (RT, 7.0), (0.36, 7.6), (0.2, 8.1), (0.05, 8.45), (0.0, 8.5)], LARANJA)

# ---------------------------------------------------------------- 2 boosters (SRB) brancos
RB = 0.19
for lado in (-1, 1):
    cx = lado * (RT + RB + 0.03)
    anel_perfil([(RB, 0.45), (RB, 0.6), (RB, 0.7), (RB, 6.6), (RB, 6.7), (0.13, 7.15), (0.0, 7.45)],
                [ESCURO, BRANCO, BRANCO, ESCURO, BRANCO, BRANCO], cx, 0.0, seg=12)
    anel_perfil([(0.17, 0.0), (0.11, 0.38), (0.1, 0.45)], ESCURO, cx, 0.0, seg=10)            # bocal
    for z in (1.0, 6.2):                                                                        # suportes
        anel_perfil([(0.035, z - 0.04), (0.035, z + 0.04)], ESCURO, cx - lado * (RB + 0.01), 0.0, seg=6)

# ---------------------------------------------------------------- orbitador (branco, nariz preto) preso na barriga do tanque
CY = RT + 0.36                                                    # centro da fuselagem (lado +Y do tanque)
RF = 0.3
anel_perfil([(0.22, 0.95), (RF, 1.05), (RF, 4.9), (0.27, 5.35), (0.2, 5.75), (0.09, 6.0), (0.0, 6.08)],
            [BRANCO, BRANCO, BRANCO, BRANCO, ESCURO, ESCURO], 0.0, CY, seg=10)
X, Y, Z = Vector((1, 0, 0)), Vector((0, 1, 0)), Vector((0, 0, 1))
# asa delta (na altura da barriga, voltada para o tanque), borda de ataque preta
for lado in (-1, 1):
    pts = [(0.0, 1.0), (1.2, 1.0), (0.42, 3.0), (0.0, 4.1)]
    pts = [(lado * u, v) for u, v in pts]
    if lado < 0:
        pts = list(reversed(pts))
    prisma(pts, X, Z, Y, 0.07, BRANCO, (0.0, CY - 0.14, 0.0), ESCURO, bordas_pretas=(1, 2) if lado > 0 else (0, 1))
# deriva vertical (para fora, lado +Y), borda preta
prisma([(0.0, 1.0), (0.85, 1.05), (0.85, 1.45), (0.0, 2.5)], Y, Z, X, 0.06, BRANCO, (0.0, CY + RF - 0.05, 0.0), ESCURO, bordas_pretas=(2,))
# 3 motores principais + 2 pods OMS
for dx, dz in ((-0.13, 0.0), (0.13, 0.0), (0.0, 0.12)):
    anel_perfil([(0.11, 0.62), (0.07, 0.88), (0.06, 0.96)], ESCURO, dx, CY + 0.06 + dz, seg=8)
for lado in (-1, 1):
    anel_perfil([(0.0, 0.95), (0.09, 1.05), (0.09, 1.7), (0.0, 1.95)], BRANCO, lado * 0.2, CY + 0.22, seg=8)

bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
me = D.meshes.new("Foguete")
bm.to_mesh(me); bm.free()
for p in me.polygons:
    p.use_smooth = False                                           # facetado: leitura cartoon
me.uv_layers.new(name="UVMap")
for nome in ("Laranja", "Branco", "Escuro"):
    me.materials.append(D.materials.get(nome) or D.materials.new(nome))
D.materials["Laranja"].diffuse_color = (0.95, 0.42, 0.08, 1)
D.materials["Branco"].diffuse_color = (0.95, 0.95, 0.93, 1)
D.materials["Escuro"].diffuse_color = (0.1, 0.1, 0.14, 1)
fog = D.objects.new("Foguete", me)
bpy.context.scene.collection.objects.link(fog)
tris = sum(len(p.vertices) - 2 for p in me.polygons)
print(f"[Foguete] verts={len(me.vertices)} tris={tris}")

for nome, z in (("FOGUETE_Base", 0.0), ("FOGUETE_Ponta", 8.5)):
    e = D.objects.new(nome, None); e.location = (0, 0, z)
    bpy.context.scene.collection.objects.link(e)

if SAIDA_PREVIEW:
    sc = bpy.context.scene
    camd = D.cameras.new("C"); camo = D.objects.new("C", camd); sc.collection.objects.link(camo)
    camo.location = (9, -9, 6); camo.rotation_euler = (math.radians(75), 0, math.radians(45))
    camd.lens = 40
    sc.camera = camo
    sc.render.engine = "BLENDER_WORKBENCH"
    sc.display.shading.light = "STUDIO"; sc.display.shading.color_type = "MATERIAL"
    sc.render.resolution_x, sc.render.resolution_y = 600, 800
    sc.render.filepath = os.path.abspath(SAIDA_PREVIEW)
    bpy.ops.render.render(write_still=True)
    D.objects.remove(camo)

if SAIDA_FBX:
    bpy.ops.object.select_all(action="SELECT")
    os.makedirs(os.path.dirname(os.path.abspath(SAIDA_FBX)), exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=os.path.abspath(SAIDA_FBX), use_selection=True, object_types={"MESH", "EMPTY"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE", axis_forward="-Z", axis_up="Y",
                             bake_space_transform=False, mesh_smooth_type="FACE", add_leaf_bones=False)
    print("[Export] FBX:", os.path.abspath(SAIDA_FBX))
if SAIDA_BLEND:
    bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(SAIDA_BLEND), compress=True)
