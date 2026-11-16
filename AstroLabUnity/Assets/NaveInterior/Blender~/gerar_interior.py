"""
Gerador do interior do onibus espacial (AstroLab).
  blender -b --factory-startup -P gerar_interior.py -- [--render] [--sem-export] [--cam cdr|teto|botao]
Saidas (Assets/NaveInterior/):
  Modelos/NI_Interior.fbx     malhas estaticas (origem = NAVE_ANCORA_INTERIOR), combinadas por material
  Modelos/NI_Kit.fbx          componentes do kit (1 objeto por tipo, origem na face do painel)
  Modelos/NI_Instancias.json  matrizes (Unity) das instancias do kit, controles interativos, luzes e marcas
  Texturas/T_NI_Etiquetas.png, T_NI_MEDS.png
  Blender~/previews/*.png     render de conferencia (960x540)
"""
import sys, os, time
AQUI = os.path.dirname(os.path.abspath(__file__))
sys.path.insert(0, AQUI)
sys.path.insert(0, os.path.normpath(os.path.join(AQUI, "..", "..", "CentroTreinamento", "Blender~")))
import bpy, math
from mathutils import Vector as V, Matrix
import astrolab_lib as L

ARGS = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
t0 = time.time()
L.limpar_cena()

import ni_base as B
import ni_kit as K
import ni_atlas as A
import ni_fd as FD
import ni_l2 as L2

LOTES = [a.split("=")[1] for a in ARGS if a.startswith("--lotes=")] or ["L1,L2"]
LOTES = LOTES[0].split(",")
FD.construir(tampa_re="L2" not in LOTES)
if "L2" in LOTES:
    L2.construir()
print(f"[ni] geometria {LOTES} registrada ({time.time() - t0:.1f}s); etiquetas {len(B.ETQ.lista)}/320, legendas {len(B.LEG.lista)}/256")

A.gerar_tudo()

col = L.colecao("NI_Interior")
objs = []
for nome, m in B.ST.items():
    o = m.criar(col)
    if o:
        objs.append(o)
    t = getattr(m, "irmao_transp", None)
    if t:
        objs.append(t)
anims = []
for m, nome in L2.ANIM if "L2" in LOTES else []:
    o = m.criar(col)
    o.name = nome
    anims.append((o, V(m.origem)))
    objs.append(o)
colk = L.colecao("NI_Kit")
kit = K.construir()
kobj = {}
for tipo, m in kit.items():
    o = m.criar(colk)
    kobj[tipo] = o

# ---------------------------------------------------------------- contagem
CAPA = {"verde": "capa_verde", "ambar": "capa_amb", "verm": "capa_verm", "branca": "capa_br", "apag": "capa_apag"}


def partes(it):
    """pecas do kit que formam um controle interativo (a ordem: fixa, movel)."""
    if it["tipo"] == "chave":
        return [("base_chave", Matrix()), ("alavanca", B.T(0, 0, 0.0068) @ B.R(-math.radians(24) * it.get("pos", 1), "X"))]
    return [("base_botao", Matrix()), (CAPA[it["cor"]], Matrix())]


tk = {t: B.tris_obj(o) for t, o in kobj.items()}
tris_st = {o.name: B.tris_obj(o) for o in objs}
tris_kit = sum(len(l) * tk[t] for t, l in B.KIT.inst.items())
tris_int = sum(tk[p] for it in B.KIT.interativos for p, _ in partes(it))
print("[tris] estaticos:", sum(tris_st.values()), " ".join(f"{k}={v}" for k, v in sorted(tris_st.items(), key=lambda x: -x[1])))
print("[tris] kit:", tris_kit, " ".join(f"{t}:{len(l)}x{tk[t]}" for t, l in sorted(B.KIT.inst.items(), key=lambda x: -len(x[1]) * tk[x[0]])))
print("[tris] interativos:", tris_int, f"({len(B.KIT.interativos)}):", ", ".join(it["id"] for it in B.KIT.interativos))
print(f"[tris] TOTAL {'+'.join(LOTES)} = {sum(tris_st.values()) + tris_kit + tris_int}")

NI = B.NI
for it in B.KIT.interativos:
    it["partes"] = partes(it)
B.gravar_kit_json(os.path.join(NI, "Modelos", "NI_Instancias.json"), {t: o.name for t, o in kobj.items() if o})

# ---------------------------------------------------------------- preview
if "--render" in ARGS:
    cp = L.colecao("PREVIEW_Kit")
    for t, lst in B.KIT.inst.items():
        for Mw in lst:
            o = bpy.data.objects.new("pv", kobj[t].data)
            o.matrix_world = Mw
            cp.objects.link(o)
    for it in B.KIT.interativos:
        for p, Ml in partes(it):
            o = bpy.data.objects.new("pv", kobj[p].data)
            o.matrix_world = it["M"] @ Ml
            cp.objects.link(o)
    for i, l in enumerate(B.LUZES):
        ld = bpy.data.lights.new(f"PREVIEW_L{i}", "POINT")
        ld.energy = 140 * l["intens"]
        ld.color = L._lin(L.hexcor(l["cor"]))
        ld.shadow_soft_size = 0.2
        o = bpy.data.objects.new(f"PREVIEW_L{i}", ld)
        o.location = l["pos"]
        cp.objects.link(o)
    L.preparar_render(res=(960, 540), mundo="#2A3350", contorno=True, sol_rot=(math.radians(55), 0, math.radians(-100)))
    bpy.context.scene.world.node_tree.nodes["Background"].inputs[1].default_value = 0.45
    cams = {
        "cdr": ((31.0, 0.62, 4.23), (32.2, 0.05, 3.85), 16),
        "teto": ((30.9, 0.2, 4.35), (30.6, 0.0, 5.5), 18),
        "geral": ((28.6, 0.0, 4.4), (32.0, 0.0, 3.9), 18),
        "re": ((28.95, -0.95, 4.45), (26.8, 0.15, 4.05), 16),
        "middeck": ((27.9, 0.9, 1.75), (31.5, -0.6, 1.4), 15),
    }
    escolha = ([a.split("=")[1] for a in ARGS if a.startswith("--cam=")] or ["cdr"])[0].split(",")
    mosaico = "--mosaico" in ARGS and len(escolha) == 4
    if mosaico:
        bpy.context.scene.render.resolution_x, bpy.context.scene.render.resolution_y = 480, 270
    arqs = []
    for nome in escolha:
        pos, alvo, lente = cams[nome]
        arqs.append(os.path.join(AQUI, "previews", f"{LOTES[-1]}_{nome}.png"))
        L.renderizar(arqs[-1], pos, alvo, lente=lente)
    if mosaico:                            # 4 vistas 480x270 -> 1 imagem 960x540
        import numpy as np
        out = np.zeros((540, 960, 4), dtype=np.float32)
        for k, a in enumerate(arqs):
            im = bpy.data.images.load(a)
            px = np.array(im.pixels[:], dtype=np.float32).reshape(270, 480, 4)
            r, c = k // 2, k % 2
            out[(1 - r) * 270:(2 - r) * 270, c * 480:(c + 1) * 480] = px
            bpy.data.images.remove(im)
            os.remove(a)
        img = bpy.data.images.new("mosaico", 960, 540, alpha=True)
        img.pixels[:] = out.ravel()
        img.filepath_raw = os.path.join(AQUI, "previews", f"{LOTES[-1]}_mosaico.png")
        img.file_format = "PNG"
        img.save()
        print("[preview] mosaico", img.filepath_raw)
    for o in list(cp.objects):
        bpy.data.objects.remove(o, do_unlink=True)
    L.remover_preview()

# ---------------------------------------------------------------- exportar
if "--sem-export" not in ARGS:
    for o in objs:
        o.location = (0, 0, 0)            # a origem do FBX e a ancora (os vertices ja estao relativos a ela)
    for o, orig in anims:
        o.location = orig - B.ANC         # pecas ANIM_: pivo proprio (dobradica etc.)
    def exportar(caminho, lista):
        """FBX com a conversao de eixos e de unidade embutida nos vertices: no Unity cada objeto chega com
        rotacao/escala identidade e vertices em metros, no referencial (-x, z, -y) usado pelo JSON."""
        bpy.ops.object.select_all(action="DESELECT")
        for o in lista:
            o.select_set(True)
        bpy.context.view_layer.objects.active = lista[0]
        bpy.ops.export_scene.fbx(filepath=caminho, use_selection=True, object_types={"MESH"}, apply_unit_scale=True,
                                 apply_scale_options="FBX_SCALE_UNITS", axis_forward="-Z", axis_up="Y", bake_space_transform=True,
                                 mesh_smooth_type="FACE", use_mesh_modifiers=True, add_leaf_bones=False, path_mode="AUTO",
                                 embed_textures=False, use_custom_props=False)
        print(f"[export] {caminho}: {len(lista)} objetos")
    exportar(os.path.join(NI, "Modelos", "NI_Interior.fbx"), objs)
    exportar(os.path.join(NI, "Modelos", "NI_Kit.fbx"), [o for o in kobj.values() if o])
print(f"[ni] fim em {time.time() - t0:.1f}s")
