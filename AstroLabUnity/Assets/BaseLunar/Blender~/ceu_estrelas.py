"""
Céu estrelado cartoon da Base Lunar — renderizado no Blender (Cycles) como panorama equirretangular.

Uso:  blender -b --factory-startup --python ceu_estrelas.py -- <saida.png> [<saida.blend>] [largura]

Estrelas procedurais (Voronoi 3D na direção da visão), em 4 camadas de tamanho, redondas com brilho suave
(nada de sprite em cruz), cores quentes/frias, Via Láctea com poeira de estrelas e nebulosas sutis
nas cores da paleta (PaletaLua: CeuTopo #0F1236, CeuHorizonte #2E2766).
"""
import bpy, sys, os, math

argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
SAIDA_PNG = argv[0] if len(argv) > 0 else "//ceu.png"
SAIDA_BLEND = argv[1] if len(argv) > 1 and argv[1] != "-" else None
LARG = int(argv[2]) if len(argv) > 2 else 8192


def lin(h):
    h = h.lstrip("#")
    c = [int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)]
    return tuple((x / 12.92 if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4) for x in c) + (1.0,)


sc = bpy.context.scene
for o in list(sc.objects):
    bpy.data.objects.remove(o, do_unlink=True)
w = bpy.data.worlds.new("Ceu_Lua_Estrelas"); sc.world = w
w.use_nodes = True
nt = w.node_tree; N = nt.nodes; Lk = nt.links
N.clear()


def no(tipo, **props):
    n = N.new(tipo)
    for k, v in props.items():
        setattr(n, k, v)
    return n


def mat(op, a, b=None, valor_b=None, clamp=False):
    m = no("ShaderNodeMath", operation=op, use_clamp=clamp)
    Lk.new(a, m.inputs[0])
    if b is not None:
        Lk.new(b, m.inputs[1])
    elif valor_b is not None:
        m.inputs[1].default_value = valor_b
    return m.outputs[0]


coord = no("ShaderNodeTexCoord")
dirv = coord.outputs["Generated"]                       # no mundo: direção da visão
sep = no("ShaderNodeSeparateXYZ"); Lk.new(dirv, sep.inputs[0])
z = sep.outputs["Z"]

# ---------------------------------------------------------------- gradiente do céu
grad = no("ShaderNodeMapRange", interpolation_type="SMOOTHSTEP")
grad.inputs["From Min"].default_value = -0.02; grad.inputs["From Max"].default_value = 0.7
Lk.new(z, grad.inputs["Value"])
ramp = no("ShaderNodeValToRGB")
ramp.color_ramp.elements[0].color = lin("#2E2766")      # horizonte violeta
ramp.color_ramp.elements[1].color = lin("#0F1236")      # topo azul-marinho
e = ramp.color_ramp.elements.new(0.35); e.color = lin("#1B1A4E")
Lk.new(grad.outputs["Result"], ramp.inputs["Fac"])
cor_total = ramp.outputs["Color"]


def somar(a, b):
    mx = no("ShaderNodeMix", data_type="RGBA", blend_type="ADD")
    mx.inputs["Factor"].default_value = 1.0
    Lk.new(a, mx.inputs[6]); Lk.new(b, mx.inputs[7])
    return mx.outputs[2]


def escalar(cor, fator_socket=None, fator=1.0):
    mx = no("ShaderNodeMix", data_type="RGBA", blend_type="MULTIPLY")
    mx.inputs["Factor"].default_value = 1.0
    Lk.new(cor, mx.inputs[6])
    if fator_socket is not None:
        cmb = no("ShaderNodeCombineXYZ")
        for i in range(3):
            Lk.new(fator_socket, cmb.inputs[i])
        Lk.new(cmb.outputs[0], mx.inputs[7])
    else:
        mx.inputs[7].default_value = (fator, fator, fator, 1)
    return mx.outputs[2]


# ---------------------------------------------------------------- Via Láctea (faixa inclinada)
dot = no("ShaderNodeVectorMath", operation="DOT_PRODUCT")
Lk.new(dirv, dot.inputs[0]); dot.inputs[1].default_value = (0.35, -0.55, 0.76)
d2 = mat("MULTIPLY", dot.outputs["Value"], dot.outputs["Value"])
faixa = mat("EXPONENT", mat("MULTIPLY", d2, valor_b=-28.0))   # exp(-28 * dot²)
ruido_v = no("ShaderNodeTexNoise", noise_dimensions="3D")
ruido_v.inputs["Scale"].default_value = 3.5; ruido_v.inputs["Detail"].default_value = 8; ruido_v.inputs["Roughness"].default_value = 0.6
Lk.new(dirv, ruido_v.inputs["Vector"])
mr = no("ShaderNodeMapRange"); mr.inputs["From Min"].default_value = 0.42; mr.inputs["From Max"].default_value = 0.7
Lk.new(ruido_v.outputs["Fac"], mr.inputs["Value"])
via = mat("MULTIPLY", faixa, mr.outputs["Result"])

# nebulosas (roxo/turquesa da paleta), bem sutis
neb = no("ShaderNodeTexNoise", noise_dimensions="3D")
neb.inputs["Scale"].default_value = 1.6; neb.inputs["Detail"].default_value = 6; neb.inputs["Distortion"].default_value = 0.8
Lk.new(dirv, neb.inputs["Vector"])
nramp = no("ShaderNodeValToRGB")
nramp.color_ramp.elements[0].position = 0.45; nramp.color_ramp.elements[0].color = (0, 0, 0, 1)
nramp.color_ramp.elements[1].position = 0.75; nramp.color_ramp.elements[1].color = lin("#5A3E9E")
e = nramp.color_ramp.elements.new(0.62); e.color = lin("#1F6F8B")
Lk.new(neb.outputs["Fac"], nramp.inputs["Fac"])
alto = no("ShaderNodeMapRange"); alto.inputs["From Min"].default_value = 0.0; alto.inputs["From Max"].default_value = 0.3
Lk.new(z, alto.inputs["Value"])
neb_mask = mat("MULTIPLY", mat("ADD", via, valor_b=0.25), alto.outputs["Result"])
cor_total = somar(cor_total, escalar(nramp.outputs["Color"], mat("MULTIPLY", neb_mask, valor_b=0.26)))
cor_total = somar(cor_total, escalar(no_cor := no("ShaderNodeRGB").outputs[0], mat("MULTIPLY", mat("MULTIPLY", via, alto.outputs["Result"]), valor_b=0.17)))
no_cor.node.outputs[0].default_value = lin("#8E7BD6")


# ---------------------------------------------------------------- camadas de estrelas
def estrelas(escala, densidade, tamanho, brilho, halo=0.0, mascara=None):
    vor = no("ShaderNodeTexVoronoi", voronoi_dimensions="3D", feature="F1")
    vor.inputs["Scale"].default_value = escala; vor.inputs["Randomness"].default_value = 1.0
    Lk.new(dirv, vor.inputs["Vector"])
    rnd = no("ShaderNodeSeparateColor"); Lk.new(vor.outputs["Color"], rnd.inputs[0])
    r, g, b = rnd.outputs[0], rnd.outputs[1], rnd.outputs[2]
    existe = mat("GREATER_THAN", r, valor_b=1.0 - densidade)
    tam = mat("MULTIPLY", mat("ADD", g, valor_b=0.45), valor_b=tamanho)     # tamanho varia por estrela
    t = mat("DIVIDE", vor.outputs["Distance"], tam)
    nucleo = mat("POWER", mat("SUBTRACT", no_um := no("ShaderNodeValue").outputs[0], t, clamp=True), valor_b=2.2)
    no_um.node.outputs[0].default_value = 1.0
    luz = nucleo
    if halo > 0:
        t2 = mat("DIVIDE", t, valor_b=6.0)
        h = mat("POWER", mat("SUBTRACT", no_um, t2, clamp=True), valor_b=4.0)
        luz = mat("ADD", luz, mat("MULTIPLY", h, valor_b=halo))
    intens = mat("MULTIPLY", mat("MULTIPLY", luz, existe), mat("MULTIPLY", mat("ADD", b, valor_b=0.35), valor_b=brilho))
    if mascara is not None:
        intens = mat("MULTIPLY", intens, mascara)
    cor = no("ShaderNodeMix", data_type="RGBA")
    Lk.new(b, cor.inputs["Factor"])
    cor.inputs[6].default_value = lin("#FFD9A8")      # quente
    cor.inputs[7].default_value = lin("#BCD4FF")      # fria
    branca = no("ShaderNodeMix", data_type="RGBA"); branca.inputs["Factor"].default_value = 0.55
    Lk.new(cor.outputs[2], branca.inputs[6]); branca.inputs[7].default_value = (1, 1, 1, 1)
    return escalar(branca.outputs[2], intens)


acima = no("ShaderNodeMapRange"); acima.inputs["From Min"].default_value = -0.03; acima.inputs["From Max"].default_value = 0.04
Lk.new(z, acima.inputs["Value"])
ceu_visivel = acima.outputs["Result"]

cor_total = somar(cor_total, estrelas(260, 0.55, 0.13, 0.55, mascara=ceu_visivel))                  # poeira
cor_total = somar(cor_total, estrelas(520, 0.75, 0.17, 0.85, mascara=mat("MULTIPLY", via, ceu_visivel)))  # poeira da Via Láctea
cor_total = somar(cor_total, estrelas(85, 0.42, 0.07, 1.0, halo=0.25, mascara=ceu_visivel))        # médias
cor_total = somar(cor_total, estrelas(26, 0.3, 0.035, 1.6, halo=0.5, mascara=ceu_visivel))         # brilhantes
cor_total = somar(cor_total, estrelas(8, 0.22, 0.02, 2.4, halo=0.9, mascara=ceu_visivel))          # heroínas

bg = no("ShaderNodeBackground"); bg.inputs["Strength"].default_value = 1.0
Lk.new(cor_total, bg.inputs["Color"])
out = no("ShaderNodeOutputWorld"); Lk.new(bg.outputs[0], out.inputs["Surface"])

# ---------------------------------------------------------------- câmera panorâmica + render
cam_d = bpy.data.cameras.new("CamCeu"); cam = bpy.data.objects.new("CamCeu", cam_d)
sc.collection.objects.link(cam); sc.camera = cam
cam_d.type = "PANO"
if hasattr(cam_d, "panorama_type"):
    cam_d.panorama_type = "EQUIRECTANGULAR"
else:
    cam_d.cycles.panorama_type = "EQUIRECTANGULAR"
cam.rotation_euler = (math.radians(90), 0, 0)            # horizonte no meio da imagem, +Z para cima
sc.render.engine = "CYCLES"
sc.cycles.samples = 16
sc.cycles.use_denoising = False
sc.cycles.filter_width = 1.2
sc.render.resolution_x = LARG; sc.render.resolution_y = LARG // 2; sc.render.resolution_percentage = 100
sc.view_settings.view_transform = "Standard"; sc.view_settings.look = "None"
sc.render.image_settings.file_format = "PNG"; sc.render.image_settings.color_mode = "RGB"; sc.render.image_settings.color_depth = "8"
sc.render.filepath = os.path.abspath(SAIDA_PNG)
if SAIDA_BLEND:
    bpy.ops.wm.save_as_mainfile(filepath=os.path.abspath(SAIDA_BLEND), compress=True)
bpy.ops.render.render(write_still=True)
print("[Ceu] PNG:", os.path.abspath(SAIDA_PNG))
