"""
Atlas gerados no proprio Blender (render ortografico em Workbench, cores exatas, sem luz):
  T_NI_Etiquetas.png (2048x2048): etiquetas brancas no cinza do painel (topo) + legendas de anunciador (base)
  T_NI_MEDS.png      (2048x1536): 12 paginas de 512x512 das telas MEDS (glass cockpit)
1 unidade de mundo = 1 pixel; y do mundo para cima = v da imagem.
"""
import os, math, glob
import bpy, bmesh
from mathutils import Vector as V
import ni_base as B
from astrolab_lib import hexcor, _lin

FONTES = bpy.utils.system_resource("DATAFILES", path="fonts") or ""


def _fonte(nome):
    for p in glob.glob(os.path.join(FONTES, nome + "*")):
        try:
            return bpy.data.fonts.load(p, check_existing=True)
        except Exception as e:
            print("[atlas] fonte falhou:", p, e)
    return None


class Tela:
    """cena de desenho 2D (px)."""

    def __init__(self, nome, W, H, fundo):
        self.sc = bpy.data.scenes.new(nome)
        self.W, self.H = W, H
        self.z = 0.0
        self.objs = []
        self.mono = _fonte("DejaVuSansMono")
        self.sans = _fonte("Inter")
        sc = self.sc
        sc.render.engine = "BLENDER_WORKBENCH"
        sc.display.shading.light = "FLAT"
        sc.display.shading.color_type = "OBJECT"
        sc.display.render_aa = "16"
        sc.view_settings.view_transform = "Standard"
        sc.render.resolution_x, sc.render.resolution_y = W, H
        sc.render.resolution_percentage = 100
        sc.render.film_transparent = False
        sc.render.image_settings.file_format = "PNG"
        sc.render.image_settings.color_mode = "RGB"
        w = bpy.data.worlds.new(nome + "_mundo")
        w.color = _lin(hexcor(fundo))
        sc.world = w
        cd = bpy.data.cameras.new(nome + "_cam")
        cd.type = "ORTHO"
        cd.ortho_scale = max(W, H)
        cam = bpy.data.objects.new(nome + "_cam", cd)
        sc.collection.objects.link(cam)
        cam.location = (W / 2, H / 2, 100)
        cd.clip_end = 1000
        sc.camera = cam

    def _z(self):
        self.z += 0.01
        return self.z

    def _obj(self, me, cor):
        ob = bpy.data.objects.new("d", me)
        ob.color = (*_lin(hexcor(cor)), 1)
        self.sc.collection.objects.link(ob)
        self.objs.append(ob)
        return ob

    # y aqui e do TOPO da imagem para baixo (como em editores de imagem)
    def poli(self, pts, cor):
        z = self._z()
        bm = bmesh.new()
        vs = [bm.verts.new((x, self.H - y, z)) for x, y in pts]
        bm.faces.new(vs)
        me = bpy.data.meshes.new("p")
        bm.to_mesh(me)
        bm.free()
        return self._obj(me, cor)

    def ret(self, x0, y0, x1, y1, cor):
        return self.poli([(x0, y0), (x1, y0), (x1, y1), (x0, y1)], cor)

    def linha(self, x0, y0, x1, y1, w, cor):
        d = V((x1 - x0, y1 - y0))
        if d.length < 1e-6:
            return
        d.normalize()
        n = V((-d.y, d.x)) * (w / 2)
        return self.poli([(x0 - n.x, y0 - n.y), (x1 - n.x, y1 - n.y), (x1 + n.x, y1 + n.y), (x0 + n.x, y0 + n.y)], cor)

    def circ(self, cx, cy, r, cor, a0=0.0, a1=360.0, segs=64):
        pts = [(cx, cy)] if a1 - a0 < 359.9 else []
        n = max(8, int(segs * (a1 - a0) / 360))
        for i in range(n + (1 if pts else 0)):
            a = math.radians(a0 + (a1 - a0) * i / n)
            pts.append((cx + r * math.cos(a), cy - r * math.sin(a)))
        return self.poli(pts, cor)

    def anel(self, cx, cy, r, w, cor, segs=64):
        for i in range(segs):
            a, b = 2 * PI * i / segs, 2 * PI * (i + 1) / segs
            self.linha(cx + r * math.cos(a), cy - r * math.sin(a), cx + r * math.cos(b), cy - r * math.sin(b), w, cor)

    def texto(self, s, x, y, tam, cor, alinhar="LEFT", mono=True, larg_max=None):
        cu = bpy.data.curves.new("t", "FONT")
        cu.body = s
        cu.size = tam
        cu.align_x = alinhar
        cu.align_y = "CENTER"
        f = self.mono if mono else self.sans
        if f:
            cu.font = f
        ob = self._obj(cu, cor)
        ob.location = (x, self.H - y, self._z())
        if larg_max:
            ob["larg_max"] = larg_max
        return ob

    def ajustar(self):
        """encolhe textos que passam da largura maxima (medida real depois de avaliar a cena)."""
        dg = self.sc.view_layers[0].depsgraph
        dg.update()
        for ob in self.objs:
            lm = ob.get("larg_max")
            if not lm:
                continue
            w = ob.evaluated_get(dg).dimensions.x
            if w > lm:
                k = lm / w
                ob.scale = (k, k, 1)

    def render(self, caminho):
        self.ajustar()
        self.sc.render.filepath = caminho
        os.makedirs(os.path.dirname(caminho), exist_ok=True)
        bpy.ops.render.render(write_still=True, scene=self.sc.name)
        print("[atlas]", caminho, f"{len(self.objs)} objetos")
        for ob in self.objs:
            d = ob.data
            bpy.data.objects.remove(ob, do_unlink=True)
            if isinstance(d, bpy.types.Mesh):
                bpy.data.meshes.remove(d)
            else:
                bpy.data.curves.remove(d)
        bpy.data.scenes.remove(self.sc)


PI = math.pi
CORES_LEG = {"ambar": "#FFC23A", "verde": "#59FF86", "verm": "#FF4A4A", "branca": "#FFF4DE", "ciano": "#47E6FF"}


def gerar_etiquetas():
    t = Tela("ATLAS_ETQ", B.ATLAS, B.ATLAS, B.COR_FUNDO_ETQ)
    A = B.ETQ
    for i, (txt, _) in enumerate(A.lista):
        x, y, w, h = A.rect_px(i)
        t.texto(txt, x + w / 2, y + h / 2 + 1, h * 0.78, "#F4F2EC", "CENTER", mono=False, larg_max=w * 0.94)
    G = B.LEG
    t.ret(0, G.y0, B.ATLAS, B.ATLAS, B.COR_FUNDO_LEG)
    for i, ((txt, cor), _) in enumerate(G.lista):
        x, y, w, h = G.rect_px(i)
        t.ret(x + 3, y + 3, x + w - 3, y + h - 3, "#262B40")
        linhas = txt.split("\n")
        tam = h * (0.34 if len(linhas) > 1 else 0.46)
        for k, ln in enumerate(linhas):
            yy = y + h / 2 + (k - (len(linhas) - 1) / 2) * tam * 1.05
            t.texto(ln, x + w / 2, yy + 1, tam, CORES_LEG.get(cor, "#FFC23A"), "CENTER", mono=False, larg_max=w * 0.88)
    t.render(B.ATLAS_ETQ)


# ======================================================================= paginas MEDS
VERDE, CIANO, BRANCO, AMBAR, MAG, VERM = "#59FF86", "#47E6FF", "#F4F2EC", "#FFC23A", "#FF6AD5", "#FF4A4A"
FUNDO_MEDS = "#070B16"


def _cab(t, x, y, titulo, sub=""):
    t.texto(titulo, x + 16, y + 22, 20, BRANCO)
    if sub:
        t.texto(sub, x + 496, y + 22, 16, CIANO, "RIGHT")
    t.linha(x + 10, y + 38, x + 502, y + 38, 2, "#3E4C78")


def _linhas_texto(t, x, y, linhas, y0=60, tam=15, passo=21, cor=VERDE):
    for k, ln in enumerate(linhas):
        if isinstance(ln, tuple):
            ln, c = ln
        else:
            c = cor
        t.texto(ln, x + 18, y + y0 + k * passo, tam, c)


def _barras(t, x, y, grupos, y0=90, alt=300):
    n = len(grupos)
    larg = 480 / n
    for i, (nome, val, cor) in enumerate(grupos):
        cx = x + 16 + larg * i + larg / 2
        t.ret(cx - 16, y + y0, cx + 16, y + y0 + alt, "#1B2236")
        t.ret(cx - 16, y + y0 + alt * (1 - val), cx + 16, y + y0 + alt, cor)
        for k in range(6):
            yy = y + y0 + alt * k / 5
            t.linha(cx - 22, yy, cx - 16, yy, 2, BRANCO)
        t.texto(nome, cx, y + y0 + alt + 22, 14, BRANCO, "CENTER")
        t.texto(f"{int(val * 100)}", cx, y + y0 - 14, 14, VERDE, "CENTER")


def pagina_pfd(t, x, y):
    _cab(t, x, y, "A/E PFD", "MM 102")
    cx, cy, r = x + 256, y + 250, 138
    t.circ(cx, cy, r + 6, "#2E3550")
    t.circ(cx, cy, r, "#C9CCD2", 0, 180)                       # ceu (metade de cima)
    t.circ(cx, cy, r, "#3A3F52", 180, 360)                     # terra
    t.linha(cx - r, cy, cx + r, cy, 3, BRANCO)
    for p in (-60, -30, 30, 60):
        w = 50 if abs(p) == 60 else 32
        c = "#1C1A3D" if p < 0 else BRANCO
        t.linha(cx - w, cy + p, cx + w, cy + p, 2, c)
        t.texto(str(abs(p) // 3), cx + w + 16, cy + p, 12, c, "CENTER")
    t.anel(cx, cy, r + 3, 3, BRANCO, 72)
    for a in range(0, 360, 30):
        aa = math.radians(a)
        t.linha(cx + (r + 4) * math.cos(aa), cy - (r + 4) * math.sin(aa), cx + (r + 14) * math.cos(aa),
                cy - (r + 14) * math.sin(aa), 2, BRANCO)
    t.linha(cx - 60, cy, cx - 18, cy, 6, AMBAR)                # simbolo do aviao
    t.linha(cx + 18, cy, cx + 60, cy, 6, AMBAR)
    t.circ(cx, cy, 6, AMBAR)
    for sx, rot, val in ((x + 40, "ALPHA", "2.4"), (x + 472, "H", "126K"), (x + 432, "HDOT", "+1.9K")):
        t.ret(sx - 18, y + 110, sx + 18, y + 390, "#1B2236")
        for k in range(12):
            t.linha(sx - 18, y + 110 + k * 24, sx - 8, y + 110 + k * 24, 2, BRANCO)
        t.ret(sx - 24, y + 238, sx + 24, y + 262, "#070B16")
        t.texto(val, sx, y + 250, 12, VERDE, "CENTER")
        t.texto(rot, sx, y + 98, 12, CIANO, "CENTER")
    t.texto("MECO 08:31", x + 256, y + 430, 18, MAG, "CENTER")
    t.texto("THROT 104", x + 110, y + 470, 15, VERDE, "CENTER")
    t.texto("G 2.9", x + 256, y + 470, 15, VERDE, "CENTER")
    t.texto("KEAS 41", x + 400, y + 470, 15, VERDE, "CENTER")


def pagina_hsi(t, x, y):
    _cab(t, x, y, "HORIZ SIT", "OPS 1")
    cx, cy, r = x + 256, y + 270, 175
    t.circ(cx, cy, r, "#10182B")
    t.anel(cx, cy, r, 3, BRANCO, 96)
    for a in range(0, 360, 10):
        aa = math.radians(a)
        L = 18 if a % 30 == 0 else 9
        t.linha(cx + r * math.cos(aa), cy - r * math.sin(aa), cx + (r - L) * math.cos(aa), cy - (r - L) * math.sin(aa), 2, BRANCO)
    for a, s in ((90, "N"), (0, "E"), (270, "S"), (180, "W")):
        aa = math.radians(a)
        t.texto(s, cx + (r - 36) * math.cos(aa), cy - (r - 36) * math.sin(aa), 22, BRANCO, "CENTER")
    t.linha(cx, cy + 130, cx, cy - 130, 4, MAG)
    t.poli([(cx, cy - 145), (cx - 12, cy - 125), (cx + 12, cy - 125)], MAG)
    t.linha(cx - 30, cy + 10, cx + 30, cy + 10, 5, AMBAR)
    t.linha(cx, cy - 18, cx, cy + 30, 5, AMBAR)
    t.texto("HDG 042", x + 40, y + 480, 15, VERDE)
    t.texto("XTRK 0.3", x + 330, y + 480, 15, VERDE)


def pagina_gnc(t, x, y):
    _cab(t, x, y, "GNC SYS SUMM 1", "1011/   /018")
    _linhas_texto(t, x, y, [
        "RCS     F    L    R", "  JET  ISOL  ISOL ISOL", "  MANF   OP   OP   OP",
        "  He P 3104 3096 3112", "  PRPLT  98   97   98",
        ("SURF  POS  MOM", CIANO), "  L OB  -1.2  0", "  L IB  -1.1  0", "  R IB  -1.1  0", "  R OB  -1.2  0",
        "  AIL    0.0", "  RUD    0.1", "  SPD BRK 0.0",
        ("DPS  1 2 3 4 5", CIANO), "  GPC  * * * * -", "  MDM FF 1 2 3 4", "  FA   1 2 3 4",
        "IMU  1 2 3   ACC 1 2 3"])


def pagina_oms(t, x, y):
    _cab(t, x, y, "OMS/MPS", "SPEC 51")
    _barras(t, x, y, [("He L", 0.82, VERDE), ("He R", 0.80, VERDE), ("OXID", 0.64, CIANO), ("FUEL", 0.66, CIANO),
                      ("Pc L", 0.0, AMBAR), ("Pc R", 0.0, AMBAR)], y0=80, alt=290)
    t.texto("MPS He  L 4.1K  C 4.0K  R 4.1K", x + 256, y + 450, 14, VERDE, "CENTER")
    t.texto("LH2 ULL 33.2   LO2 ULL 21.0", x + 256, y + 476, 14, VERDE, "CENTER")


def pagina_apu(t, x, y):
    _cab(t, x, y, "APU/HYD", "SM 86")
    _barras(t, x, y, [("FU 1", 0.94, VERDE), ("FU 2", 0.93, VERDE), ("FU 3", 0.95, VERDE),
                      ("HYD 1", 0.12, AMBAR), ("HYD 2", 0.12, AMBAR), ("HYD 3", 0.12, AMBAR)], y0=80, alt=290)
    t.texto("APU  OFF   OFF   OFF", x + 256, y + 450, 15, AMBAR, "CENTER")
    t.texto("SPEED  0%    0%    0%", x + 256, y + 476, 15, VERDE, "CENTER")


def pagina_elet(t, x, y):
    _cab(t, x, y, "ELECTRIC", "SM 67")
    _linhas_texto(t, x, y, [("        FC1   FC2   FC3", CIANO), "VOLTS   30.8  30.6  30.9", "AMPS    248   262   241",
                            ("MAIN    A     B     C", CIANO), "VOLTS   29.1  29.0  29.2", "AMPS    214   228   205",
                            ("ESS     1BC   2CA   3AB", CIANO), "VOLTS   29.0  29.0  29.1",
                            ("AC      1     2     3", CIANO), "VOLTS   117   118   117", "AMPS    6.4   6.1   6.8",
                            ("CRYO    O2    H2", CIANO), "TK1 QTY  92%   91%", "TK2 QTY  93%   92%"], y0=64, passo=24)


def pagina_dps(t, x, y):
    _cab(t, x, y, "DPS UTILITY", "SPEC 1")
    _linhas_texto(t, x, y, [("GPC/BUS STATUS", CIANO), " GPC    1  2  3  4  5", " MODE   R  R  R  R  S",
                            " STRING 1  2  3  4  -", " LL     1  2  3  4  -", ("MEMORY", CIANO), " CONFIG  1 OPS 1  G1",
                            " LOAD    MMU 1  ITEM 1", " STATUS  READY", ("TIME", CIANO), " GMT  312/14:02:51",
                            " MET  000/00:00:00", " T-  00:09:00 HOLD"], y0=64, passo=26)


def pagina_spec(t, x, y):
    _cab(t, x, y, "SM TABLE MAINT", "SPEC 60")
    _linhas_texto(t, x, y, ["PARAM ID  LIM  NOISE", "1 0213   12.8  0.2", "2 0215   14.6  0.2", "3 0217   80.0  1.0",
                            "4 0412  103.0  0.5", "5 0414   99.0  0.5", "6 0610    3.0  0.1", "7 0612    3.0  0.1",
                            ("FDA  ENA  INH", CIANO), " 11 ENA", " 12 INH", ("SM TIMER 00:00:00", AMBAR)], y0=64, passo=27)


def pagina_check(t, x, y):
    _cab(t, x, y, "PRE-LAUNCH", "CDR")
    itens = ["CABIN LIGHTS ......... ON", "MEDS PWR ............. ON", "FUEL CELLS ........... ON",
             "APU 1 2 3 ........... START", "COMM ................. CHECK", "MASTER ALARM ......... RESET",
             "HATCH ................ CLOSE", "BODY FLAP ............ AUTO"]
    for k, s in enumerate(itens):
        t.ret(x + 18, y + 64 + k * 46, x + 36, y + 82 + k * 46, "#3E4C78")
        t.texto(s, x + 48, y + 73 + k * 46, 16, CIANO if k % 2 == 0 else BRANCO)


def pagina_baia(t, x, y):
    _cab(t, x, y, "CCTV  A", "PLB")
    t.ret(x + 10, y + 46, x + 502, y + 502, "#1E2A44")
    t.poli([(x + 60, y + 502), (x + 200, y + 150), (x + 312, y + 150), (x + 452, y + 502)], "#4A5068")
    for k in range(7):
        yy = y + 170 + k * 50
        f = (yy - (y + 150)) / 352
        t.linha(x + 200 - 140 * f, yy, x + 312 + 140 * f, yy, 2, "#7C8CB8")
    t.ret(x + 220, y + 250, x + 292, y + 330, "#B9D2E8")
    t.circ(x + 420, y + 110, 40, "#47A3FF")
    t.texto("PAN +012  TILT -034", x + 256, y + 486, 14, BRANCO, "CENTER")


def pagina_rms(t, x, y):
    _cab(t, x, y, "PDRS STATUS", "SPEC 94")
    pts = [(x + 120, y + 420), (x + 160, y + 300), (x + 300, y + 180), (x + 420, y + 220), (x + 440, y + 260)]
    for a, b in zip(pts, pts[1:]):
        t.linha(*a, *b, 8, "#B9D2E8")
    for p in pts:
        t.circ(*p, 10, AMBAR)
    _linhas_texto(t, x, y, [("JOINT  POS", CIANO), "SH Y  +012.4", "SH P  +064.0", "EL P  -098.2", "WR P  +034.1",
                            "WR Y  -002.0", "WR R  +000.4", ("MODE  SINGLE", AMBAR)], y0=64, tam=14, passo=22)


def pagina_falhas(t, x, y):
    _cab(t, x, y, "FAULT SUMM", "")
    _linhas_texto(t, x, y, ["", ("  -- NO FAULTS --", VERDE), "", "SM ALERT       0", "CAUTION        0", "WARNING        0",
                            ("MSG RESET  ACK", CIANO)], y0=80, passo=34)


PAGINAS = [pagina_pfd, pagina_hsi, pagina_gnc, pagina_oms, pagina_apu, pagina_elet, pagina_dps, pagina_spec,
           pagina_check, pagina_baia, pagina_rms, pagina_falhas]
P_PFD, P_HSI, P_GNC, P_OMS, P_APU, P_ELET, P_DPS, P_SPEC, P_CHECK, P_BAIA, P_RMS, P_FALHAS = range(12)


def gerar_meds():
    t = Tela("ATLAS_MEDS", 2048, 1536, FUNDO_MEDS)
    for i, f in enumerate(PAGINAS):
        x, y = (i % 4) * 512, (i // 4) * 512
        t.ret(x + 2, y + 2, x + 510, y + 510, "#0B1020")
        f(t, x, y)
    t.render(B.ATLAS_MEDS)


def gerar_tudo():
    gerar_etiquetas()
    gerar_meds()
    for img in bpy.data.images:
        if img.filepath and os.path.basename(img.filepath) in ("T_NI_Etiquetas.png", "T_NI_MEDS.png"):
            img.reload()
