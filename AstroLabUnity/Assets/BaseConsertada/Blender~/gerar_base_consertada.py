"""
Gera a versao CONSERTADA da Base Lunar (objetos OK_*), a partir do .blend da base quebrada.

Uso (Windows, PowerShell), a partir da pasta AstroLabUnity:
  & "C:\\Program Files\\Blender Foundation\\Blender 5.2\\blender.exe" -b <copia_de_BaseLunar.blend> `
      --python "Assets/BaseConsertada/Blender~/gerar_base_consertada.py" -- `
      "Assets/BaseConsertada/Modelos/BaseConsertada.fbx" "Assets/BaseConsertada/Blender~/BaseConsertada.blend"

- NAO altera o .blend original: trabalhe sempre numa copia.
- Cada objeto consertado se chama OK_<nome> e fica nas MESMAS coordenadas da base quebrada,
  entao no Unity basta esconder o objeto quebrado e mostrar o OK_ correspondente.
- A tabela quebrado -> consertado do lado do Unity esta em Assets/BaseConsertada/Scripts/PecasDaBase.cs.
"""
import bpy, bmesh, math, sys, os
from mathutils import Vector, Matrix

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
SAIDA_FBX = argv[0] if len(argv) > 0 else None
SAIDA_BLEND = argv[1] if len(argv) > 1 else None

D = bpy.data
COLL = D.collections.new("BaseConsertada")
bpy.context.scene.collection.children.link(COLL)


# ------------------------------------------------------------------ materiais novos
def mat_novo(nome, cor, emissao=None, forca=0.0, alpha=1.0, rough=0.5):
    m = D.materials.get(nome) or D.materials.new(nome)
    m.use_nodes = True
    p = m.node_tree.nodes.get("Principled BSDF")
    p.inputs["Base Color"].default_value = (*cor, 1.0)
    p.inputs["Roughness"].default_value = rough
    if emissao:
        p.inputs["Emission Color"].default_value = (*emissao, 1.0)
        p.inputs["Emission Strength"].default_value = forca
    if alpha < 1.0:
        p.inputs["Alpha"].default_value = alpha
    m.diffuse_color = (*cor, alpha)
    return m

M_LUZ_VERDE = mat_novo("M_Luz_Verde", (0.1, 1.0, 0.25), (0.1, 1.0, 0.25), 4.0)
M_LUZ_BRANCA = mat_novo("M_Luz_Branca", (0.95, 0.95, 1.0), (0.95, 0.95, 1.0), 5.0)
M_AGUA = mat_novo("M_Agua", (0.1, 0.45, 0.8), (0.05, 0.3, 0.6), 0.5, alpha=0.75, rough=0.05)
M_PLANTA_VIVA = mat_novo("M_Planta_Viva", (0.12, 0.45, 0.08), rough=0.7)

def M(nome):
    return D.materials[nome]

# troca de materiais padrao (dano -> conservado)
TROCA_PADRAO = {
    "M_Vidro_Rachado": "M_Vidro",
    "M_Tela_Quebrada": "M_Tela",
    "M_Metal_Enferrujado": "M_Metal",
    "M_Casco_Queimado": "M_Casco",
    "M_Painel_Quebrado": "M_Painel_Solar",
    "M_Planta_Morta": "M_Planta_Viva",
}
LUZ_STATUS = {"M_Luz_Apagada": "M_Luz_Verde", "M_Luz_Laranja": "M_Luz_Verde"}   # equipamentos: luz de status OK
LUZ_TETO = {"M_Luz_Apagada": "M_Luz_Branca"}                                    # corredores/interiores: luz acesa
LUZ_FAROL = {"M_Luz_Apagada": "M_Luz_Laranja"}                                  # farol do hub ligado


def trocar_materiais(me, extra=None):
    mapa = dict(TROCA_PADRAO)
    if extra:
        mapa.update(extra)
    for i, m in enumerate(me.materials):
        if m and m.name in mapa:
            me.materials[i] = M(mapa[m.name])


def copiar(nome_orig, nome_ok, extra=None):
    o = D.objects[nome_orig]
    c = o.copy()
    c.data = o.data.copy()
    c.name = nome_ok
    c.data.name = nome_ok
    COLL.objects.link(c)
    trocar_materiais(c.data, extra)
    return c


def novo_objeto(nome, bm, mats):
    me = D.meshes.new(nome)
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)
    bm.to_mesh(me)
    bm.free()
    for m in mats:
        me.materials.append(m)
    o = D.objects.new(nome, me)
    COLL.objects.link(o)
    return o


# ------------------------------------------------------------------ utilidades de geometria
def ilhas(me):
    bm = bmesh.new(); bm.from_mesh(me); bm.verts.ensure_lookup_table()
    vistos, grupos = set(), []
    for v in bm.verts:
        if v.index in vistos:
            continue
        pilha, g = [v], []
        vistos.add(v.index)
        while pilha:
            a = pilha.pop(); g.append(a.index)
            for e in a.link_edges:
                b = e.other_vert(a)
                if b.index not in vistos:
                    vistos.add(b.index); pilha.append(b)
        mats = {me.materials[f.material_index].name for i in g for f in bm.verts[i].link_faces}
        grupos.append((g, mats))
    bm.free()
    return grupos


def altura_terreno(x, y):
    t = D.objects["Terreno_Lua"]
    dg = bpy.context.evaluated_depsgraph_get()
    te = t.evaluated_get(dg)
    inv = t.matrix_world.inverted()
    ok, loc, _, _ = te.ray_cast(inv @ Vector((x, y, 100.0)), (inv.to_3x3() @ Vector((0, 0, -1))).normalized(), distance=500)
    return (t.matrix_world @ loc).z if ok else 0.0


def cilindro(bm, a, b, r, idx, segs=16):
    """cilindro de a ate b (Vector), material idx"""
    a, b = Vector(a), Vector(b)
    d = b - a
    rot = d.to_track_quat("Z", "Y").to_matrix().to_4x4()
    mtx = Matrix.Translation((a + b) / 2) @ rot
    res = bmesh.ops.create_cone(bm, cap_ends=True, cap_tris=False, segments=segs, radius1=r, radius2=r,
                                depth=d.length, matrix=mtx)
    for f in {f for v in res["verts"] for f in v.link_faces}:
        f.material_index = idx


def esfera(bm, c, r, idx):
    res = bmesh.ops.create_uvsphere(bm, u_segments=12, v_segments=6, radius=r, matrix=Matrix.Translation(Vector(c)))
    for f in {f for v in res["verts"] for f in v.link_faces}:
        f.material_index = idx


def caixa(bm, c, d, idx, rot_z=0.0):
    mtx = Matrix.Translation(Vector(c)) @ Matrix.Rotation(rot_z, 4, "Z") @ Matrix.Diagonal((d[0], d[1], d[2], 1))
    res = bmesh.ops.create_cube(bm, size=1, matrix=mtx)
    for f in {f for v in res["verts"] for f in v.link_faces}:
        f.material_index = idx


def arco(bm, r0, r1, a0, a1, z0, z1, idx, segs=12):
    """placa curva (setor de anel) entre os angulos a0..a1 (graus), raios r0..r1, alturas z0..z1"""
    def anel(r, z):
        return [bm.verts.new((r * math.cos(math.radians(a0 + (a1 - a0) * i / segs)),
                              r * math.sin(math.radians(a0 + (a1 - a0) * i / segs)), z)) for i in range(segs + 1)]
    ib, ob, it, ot = anel(r0, z0), anel(r1, z0), anel(r0, z1), anel(r1, z1)
    faces = []
    for i in range(segs):
        faces += [bm.faces.new((ib[i], ib[i + 1], it[i + 1], it[i])), bm.faces.new((ob[i], ot[i], ot[i + 1], ob[i + 1])),
                  bm.faces.new((ib[i], ob[i], ob[i + 1], ib[i + 1])), bm.faces.new((it[i], it[i + 1], ot[i + 1], ot[i]))]
    faces += [bm.faces.new((ib[0], it[0], ot[0], ob[0])), bm.faces.new((ib[-1], ob[-1], ot[-1], it[-1]))]
    for f in faces:
        f.material_index = idx


# ================================================================== 1. equipamentos (copia + materiais)
for n in ("Inversor_1", "Inversor_3", "Baterias", "Console_Controle", "Estufa_Painel"):
    copiar("REPAIR_" + n, "OK_" + n, LUZ_STATUS)

rec = copiar("REPAIR_Reciclador_H2O", "OK_Reciclador_H2O", {"M_Terra_Vaso": "M_Agua"})
# agua cheia: sobe o topo do volume de agua de ~2.1 m para 3.1 m
me = rec.data
for g, mats in ilhas(me):
    if mats == {"M_Agua"}:
        zmax = max(me.vertices[i].co.z for i in g)
        for i in g:
            if abs(me.vertices[i].co.z - zmax) < 1e-4:
                me.vertices[i].co.z += 1.0

# Inversor 2: igual aos inversores 1/3 (sem a portinhola aberta), no lugar do 2
i1, i2 = D.objects["REPAIR_Inversor_1"], D.objects["REPAIR_Inversor_2"]
def min_corpo(o):
    for g, mats in ilhas(o.data):
        if mats == {"M_Casco"} and len(g) == 8:
            cs = [o.matrix_world @ o.data.vertices[i].co for i in g]
            if max(c.z for c in cs) - min(c.z for c in cs) > 2.1:
                return Vector((min(c.x for c in cs), min(c.y for c in cs), min(c.z for c in cs)))
inv2 = copiar("REPAIR_Inversor_1", "OK_Inversor_2", LUZ_STATUS)
inv2.location = i1.location + (min_corpo(i2) - min_corpo(i1))

# Gerador de O2: fecha a portinhola girando na dobradica
ger = copiar("REPAIR_Gerador_O2", "OK_Gerador_O2", LUZ_STATUS)
me = ger.data
mw = ger.matrix_world
frente_x = None
for g, mats in ilhas(me):
    if mats == {"M_Casco_Escuro"}:
        frente_x = min((mw @ me.vertices[i].co).x for i in g)          # placa escura da frente (face -X)
        y_meio = sum((mw @ me.vertices[i].co).y for i in g) / len(g)
for g, mats in ilhas(me):
    cs = [mw @ me.vertices[i].co for i in g]
    if mats == {"M_Casco"} and len(g) == 8 and min(c.x for c in cs) < frente_x - 0.3:   # a porta aberta
        cantos = sorted({(round(c.x, 4), round(c.y, 4)) for c in cs})
        cantos = [Vector((x, y)) for x, y in cantos]
        h = min(cantos, key=lambda p: abs(p.x - frente_x))                   # dobradica: canto mais perto da frente
        outros = sorted((p for p in cantos if p != h), key=lambda p: (p - h).length)
        b = outros[1]                                                          # canto do outro lado da largura
        ang_atual = math.atan2(b.y - h.y, b.x - h.x)
        alvo = math.pi / 2 if y_meio > h.y else -math.pi / 2
        R = Matrix.Rotation(alvo - ang_atual, 3, "Z")
        mwi = mw.inverted()
        novos = []
        for i in g:
            w = mw @ me.vertices[i].co
            rel = R @ Vector((w.x - h.x, w.y - h.y, 0))
            novos.append((i, Vector((h.x + rel.x, h.y + rel.y, w.z))))
        x_max = max(p.x for _, p in novos)
        dx = (frente_x - 0.002) - x_max                                        # encosta na frente, por fora
        dy = y_meio - (min(p.y for _, p in novos) + max(p.y for _, p in novos)) / 2   # centraliza na frente
        novos = [(i, Vector((p.x, p.y + dy, p.z))) for i, p in novos]
        for i, p in novos:
            me.vertices[i].co = mwi @ Vector((p.x + dx, p.y, p.z))
        porta_x0 = min(p.x for _, p in novos) + dx
        porta_y = (min(p.y for _, p in novos), max(p.y for _, p in novos))
        porta_z = (min(p.z for _, p in novos), max(p.z for _, p in novos))
        # luz/faixa que ficaram atras da porta vao para a frente dela
        for g2, mats2 in ilhas(me):
            if mats2 & {"M_Luz_Verde", "M_Faixa_Azul"}:
                cs2 = [mw @ me.vertices[k].co for k in g2]
                cy = sum(c.y for c in cs2) / len(cs2); cz = sum(c.z for c in cs2) / len(cs2)
                if porta_y[0] <= cy <= porta_y[1] and porta_z[0] <= cz <= porta_z[1]:
                    desl = porta_x0 - 0.002 - max(c.x for c in cs2)
                    for k in g2:
                        w = mw @ me.vertices[k].co
                        me.vertices[k].co = mwi @ Vector((w.x + desl, w.y, w.z))
        break

# Portas
copiar("Porta_Eclusa_Externa_EMPERRADA", "OK_Porta_Eclusa_Externa", LUZ_STATUS)
copiar("Porta_SuporteVida_TRAVADA", "OK_Porta_SuporteVida", LUZ_STATUS)

# ================================================================== 2. estrutura (vidros, luzes, plantas, ferrugem)
for n in ("Hub_Torre_Vidros", "Estufa_Vidros", "SuporteVida_Casco"):
    copiar(n, "OK_" + n)
for n in ("Corredor_Estufa", "Corredor_SuporteVida", "Interior_Eclusa", "Interior_Estufa", "Interior_Hub", "Interior_SuporteVida"):
    copiar(n, "OK_" + n, LUZ_TETO)
copiar("Hub_Mastros_Farol", "OK_Hub_Mastros_Farol", LUZ_FAROL)

# tira os fios soltos pendurados na brecha do casco (dentro do Hub)
ih = D.objects["OK_Interior_Hub"]
bm = bmesh.new(); bm.from_mesh(ih.data); bm.verts.ensure_lookup_table()
remover = []
for g, mats in ilhas(ih.data):
    if mats <= {"M_Amarelo", "M_Vermelho"} and len(g) <= 16:
        cs = [ih.matrix_world @ ih.data.vertices[i].co for i in g]
        cx = sum(c.x for c in cs) / len(cs); cy = sum(c.y for c in cs) / len(cs)
        ang = math.degrees(math.atan2(cy, cx)) % 360
        if 55 <= ang <= 75 and math.hypot(cx, cy) > 8.0 and min(c.z for c in cs) > 2.5:
            remover += [bm.verts[i] for i in g]
bmesh.ops.delete(bm, geom=remover, context="VERTS")
bm.to_mesh(ih.data); bm.free()
print(f"[BaseConsertada] fios soltos removidos: {len(remover)} vertices")

# ================================================================== 3. paineis solares
modelo = D.objects["Painel_R1_C1"]
desloc = []
for o in D.objects:
    if o.name.startswith("Painel_R") and o.type == "MESH":
        desloc.append(o.location.z - altura_terreno(o.location.x, o.location.y))
desloc_z = sum(desloc) / len(desloc)
LINHA_Y = {1: -50.0, 2: -39.5, 3: -29.0, 4: -18.5, 5: -8.0, 6: 2.5}
for o in list(D.objects):
    if o.name.startswith("REPAIR_Painel_R"):
        partes = o.name.split("_")                      # REPAIR Painel R1 C2 CAIDO
        r, c = int(partes[2][1:]), int(partes[3][1:])
        x, y = 42.0 + (c - 1) * 8.5, LINHA_Y[r]
        p = modelo.copy()                                # mesma malha dos paineis intactos
        p.name = f"OK_Painel_R{r}_C{c}"
        p.location = (x, y, altura_terreno(x, y) + desloc_z)
        COLL.objects.link(p)

# ================================================================== 4. antena (prato inteiro apontado para a Terra)
torre = D.objects["Antena_Torre"]
topo = Vector((torre.location.x, torre.location.y, torre.location.z + torre.dimensions.z))
terra = D.objects["Terra_Ceu"].matrix_world.translation
pivo = topo + Vector((0, 0, 0.9))
d = (terra - pivo).normalized()
V = pivo + d * 0.35                                     # vertice do prato
Rd = d.to_track_quat("Z", "Y").to_matrix().to_4x4()
Mdish = Matrix.Translation(V) @ Rd

bm = bmesh.new()
fora = [(0.0, 0.0), (0.53, 0.06), (1.04, 0.24), (1.50, 0.52), (1.88, 0.90)]   # perfil igual ao prato original
dentro = [(0.0, 0.08), (0.52, 0.14), (1.01, 0.31), (1.45, 0.59), (1.82, 0.96)]
SEG = 32
def aneis(perfil):
    rs = []
    for r, z in perfil:
        if r == 0:
            rs.append([bm.verts.new(Mdish @ Vector((0, 0, z)))])
        else:
            rs.append([bm.verts.new(Mdish @ Vector((r * math.cos(2 * math.pi * k / SEG), r * math.sin(2 * math.pi * k / SEG), z)))
                       for k in range(SEG)])
    return rs
A, B = aneis(fora), aneis(dentro)
for anel_lista, inverter in ((A, True), (B, False)):
    for j in range(len(anel_lista) - 1):
        r0, r1 = anel_lista[j], anel_lista[j + 1]
        for k in range(SEG):
            k2 = (k + 1) % SEG
            vs = (r0[0], r1[k], r1[k2]) if len(r0) == 1 else (r0[k], r1[k], r1[k2], r0[k2])
            bm.faces.new(vs[::-1] if inverter else vs)
for k in range(SEG):                                    # borda
    k2 = (k + 1) % SEG
    bm.faces.new((A[-1][k], A[-1][k2], B[-1][k2], B[-1][k]))
# receptor no foco + 3 hastes + mastro de apoio
foco = Mdish @ Vector((0, 0, 1.75))
for k in range(3):
    a = 2 * math.pi * k / 3 + 0.3
    cilindro(bm, Mdish @ Vector((1.7 * math.cos(a), 1.7 * math.sin(a), 0.86)), foco, 0.03, 1, 8)
cilindro(bm, foco - d * 0.05, foco + d * 0.3, 0.13, 2, 12)
cilindro(bm, topo - Vector((0, 0, 0.1)), pivo, 0.12, 1, 12)
cilindro(bm, pivo, V + d * 0.05, 0.09, 1, 12)
esfera(bm, pivo, 0.16, 1)
ant = novo_objeto("OK_Antena_Prato", bm, [M("M_Casco"), M("M_Metal"), M("M_Casco_Escuro")])

# ================================================================== 5. cano do tanque de O2 (tanque -> gerador)
bm = bmesh.new()
y_cano, z_cano, x_cano = -0.8, 3.3, 22.65
p0 = Vector((22.6, y_cano, z_cano))           # juncao com o cano vertical dos tanques
p1 = Vector((x_cano, -2.4, z_cano))
p2 = Vector((22.46, -2.4, z_cano))            # entra na traseira do gerador (x = 22.52)
cilindro(bm, Vector((x_cano, y_cano, z_cano)), p1, 0.12, 0)
cilindro(bm, p1, p2, 0.12, 0)
esfera(bm, p0, 0.16, 1)
esfera(bm, p1, 0.15, 1)
for c in (Vector((x_cano, -1.0, z_cano)), Vector((x_cano, -2.15, z_cano))):
    cilindro(bm, c - Vector((0, 0.03, 0)), c + Vector((0, 0.03, 0)), 0.17, 1)          # flanges
cilindro(bm, Vector((22.56, -2.4, z_cano)), Vector((22.52, -2.4, z_cano)), 0.19, 1)  # flange na parede
novo_objeto("OK_Cano_Tanque_O2", bm, [M("M_Amarelo"), M("M_Metal")])

# ================================================================== 6. capsula de suprimentos (em pe)
cap = copiar("REPAIR_Capsula_Suprimentos", "OK_Capsula_Suprimentos")
cap.rotation_euler = (0.0, -math.pi / 2, math.radians(40.1))
cap.location.x, cap.location.y = -30.0, -32.0
bpy.context.view_layer.update()
zmin = min((cap.matrix_world @ v.co).z for v in cap.data.vertices)
cap.location.z += altura_terreno(-30.0, -32.0) + 0.02 - zmin

# ================================================================== 7. remendo da brecha no casco do Hub
bm = bmesh.new()
arco(bm, 9.36, 9.42, 57.5, 72.5, 1.7, 4.1, 0)            # placa externa
arco(bm, 9.42, 9.45, 57.5, 72.5, 1.7, 1.85, 1, 8)        # moldura amarela
arco(bm, 9.42, 9.45, 57.5, 72.5, 3.95, 4.1, 1, 8)
arco(bm, 9.42, 9.45, 57.5, 58.5, 1.85, 3.95, 1, 2)
arco(bm, 9.42, 9.45, 71.5, 72.5, 1.85, 3.95, 1, 2)
for a in (59.5, 65.0, 70.5):                              # rebites
    for z in (2.05, 2.9, 3.75):
        caixa(bm, (9.44 * math.cos(math.radians(a)), 9.44 * math.sin(math.radians(a)), z), (0.06, 0.06, 0.06), 2, math.radians(a))
arco(bm, 8.94, 8.99, 58.5, 71.5, 1.5, 4.0, 0)            # placa interna (cobre as tabuas)
novo_objeto("OK_Brecha_Casco", bm, [M("M_Casco"), M("M_Amarelo"), M("M_Metal")])

# ================================================================== exportar
ok_objs = list(COLL.objects)
print(f"[BaseConsertada] {len(ok_objs)} objetos OK_ criados:", sorted(o.name for o in ok_objs))

if SAIDA_FBX:
    os.makedirs(os.path.dirname(os.path.abspath(SAIDA_FBX)), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in ok_objs:
        o.select_set(True)
    bpy.context.view_layer.objects.active = ok_objs[0]
    bpy.ops.export_scene.fbx(filepath=os.path.abspath(SAIDA_FBX), use_selection=True, object_types={"MESH"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE",
                             axis_forward="-Z", axis_up="Y", bake_space_transform=False,
                             mesh_smooth_type="OFF", use_mesh_modifiers=True, add_leaf_bones=False,
                             path_mode="AUTO", embed_textures=False)
    print("[BaseConsertada] FBX:", os.path.abspath(SAIDA_FBX))

if SAIDA_BLEND:
    # o .blend salvo guarda so os objetos OK_ (a base quebrada continua no BaseLunar.blend do modelador)
    for o in list(D.objects):
        if o.name not in COLL.objects:
            D.objects.remove(o, do_unlink=True)
    D.orphans_purge(do_recursive=True)
    os.makedirs(os.path.dirname(os.path.abspath(SAIDA_BLEND)), exist_ok=True)
    bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(SAIDA_BLEND), compress=True)
    print("[BaseConsertada] BLEND:", os.path.abspath(SAIDA_BLEND))
