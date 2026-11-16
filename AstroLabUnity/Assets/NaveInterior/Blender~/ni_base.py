"""
Base comum do interior do onibus espacial (AstroLab).

Referencial = o do orbitador (igual ao gerar_plataforma_lancamento.py, que e so lido):
  X = nariz, Y = bombordo (esquerda), Z = teto/baia de carga.
Tudo e modelado em coordenadas do orbitador e, na exportacao, deslocado para a ANCORA
(NAVE_ANCORA_INTERIOR = piso do middeck, X 30.0, Y 0, Z 0.6): a origem do FBX e a ancora.

Registros globais preenchidos pelos geradores de cada lote:
  ST   malhas estaticas (combinadas por material na exportacao)
  KIT  posicoes dos componentes do kit (instancing no Unity) e dos controles interativos
  ETQ  etiquetas (atlas)  ·  LEG  legendas de anunciadores (atlas)  ·  MEDS  paginas das telas (atlas)
"""
import sys, os, math, json
AQUI = os.path.dirname(os.path.abspath(__file__))
NI = os.path.normpath(os.path.join(AQUI, ".."))                                   # Assets/NaveInterior
LIB = os.path.normpath(os.path.join(NI, "..", "CentroTreinamento", "Blender~"))   # astrolab_lib (so leitura)
if LIB not in sys.path:
    sys.path.insert(0, LIB)
import bpy, bmesh
from mathutils import Vector as V, Matrix
import astrolab_lib as L
from astrolab_lib import Malha, material

PI = math.pi
ANC = V((30.0, 0.0, 0.6))
FZ = 2.9          # piso do flight deck
MZ = 0.6          # piso do middeck
INSET = 0.12      # espessura casco -> parede interna


def T(x=0.0, y=0.0, z=0.0):
    return Matrix.Translation(V((x, y, z)))


def R(a, eixo):
    return Matrix.Rotation(a, 4, eixo)


def quadro(o, normal, cima):
    """matriz de um painel: Z = normal (para o jogador), Y = cima do painel, X = direita de quem olha."""
    n = V(normal).normalized()
    up = V(cima)
    up = (up - n * up.dot(n)).normalized()
    x = up.cross(n).normalized()
    M = Matrix((x, up, n)).transposed().to_4x4()
    M.translation = V(o)
    return M


# ======================================================================= casco (copia do exterior, so leitura)
ESTACOES = [  # X, a (meia largura), b (meia altura), zc, n (expoente)
    (0.0, 2.6, 2.7, 2.75, 4.0), (6.0, 2.62, 2.72, 2.78, 4.0), (26.5, 2.6, 2.72, 2.78, 4.0), (28.6, 2.57, 2.84, 2.9, 3.8),
    (31.0, 2.5, 2.86, 2.88, 3.4), (32.4, 2.32, 2.62, 2.72, 3.0), (33.6, 2.0, 2.15, 2.4, 2.7), (34.7, 1.6, 1.6, 2.05, 2.5),
    (35.7, 1.1, 1.08, 1.75, 2.2), (36.6, 0.55, 0.55, 1.5, 2.0), (37.2, 0.06, 0.06, 1.42, 2.0)]


def secao(X):
    for (x0, *p0), (x1, *p1) in zip(ESTACOES[:-1], ESTACOES[1:]):
        if x0 <= X <= x1:
            t = (X - x0) / (x1 - x0)
            return [a + (b - a) * t for a, b in zip(p0, p1)]
    return list(ESTACOES[-1][1:]) if X > ESTACOES[-1][0] else list(ESTACOES[0][1:])


def casco(X, th, inset=0.0):
    a, b, zc, n = secao(X)
    a, b = max(0.02, a - inset), max(0.02, b - inset)
    c, s = math.cos(math.radians(th)), math.sin(math.radians(th))
    y = a * math.copysign(abs(c) ** (2 / n), c)
    z = zc + b * math.copysign(abs(s) ** (2 / n), s)
    return V((X, y, z))


JANELAS = [  # (X0, X1, th0, th1)
    (32.45, 33.25, 48, 62), (32.45, 33.25, 64, 78), (32.45, 33.25, 80, 88), (32.45, 33.25, 92, 100), (32.45, 33.25, 102, 116), (32.45, 33.25, 118, 132),
    (31.3, 32.0, 72, 86), (31.3, 32.0, 94, 108),
    (31.75, 32.4, 22, 40), (31.75, 32.4, 140, 158),
]
ESCOTILHA_CELULA = (29.7, 30.7, 339.0, 357.0)


def em_janela(xa, xb, ta, tb):
    for (x0, x1, t0, t1) in JANELAS:
        if xa >= x0 - 1e-4 and xb <= x1 + 1e-4 and ta >= t0 - 1e-4 and tb <= t1 + 1e-4:
            return True
    return False


def em_escotilha(xa, xb, ta, tb):
    x0, x1, t0, t1 = ESCOTILHA_CELULA
    return xa >= x0 - 1e-4 and xb <= x1 + 1e-4 and ta >= t0 - 1e-4 and tb <= t1 + 1e-4


def grade_X():
    xs = set([round(i * 1.5, 3) for i in range(0, 18)] + [26.5, 27.6, 28.6, 29.6, 30.4, 31.0, 31.3, 31.65, 31.75, 32.0, 32.4, 32.45,
             32.85, 33.25, 33.6, 34.2, 34.7, 35.2, 35.7, 36.2, 36.6, 36.9, 37.2, 29.7, 30.7])
    return sorted(xs)


def grade_T():
    ts = set(range(0, 360, 10))
    for j in JANELAS:
        ts.update((j[2], j[3]))
    ts.update((70, 110, 64, 116, 72, 108, 94, 86, 339, 357))
    return sorted(ts)


def meia_largura(X, z, inset=INSET):
    """meia largura da parede interna na altura z."""
    a, b, zc, n = secao(X)
    a, b = a - inset, b - inset
    t = abs(z - zc) / b
    return 0.0 if t >= 1 else a * (1 - t ** n) ** (1 / n)


def teto(X, y=0.0, inset=INSET):
    a, b, zc, n = secao(X)
    a, b = a - inset, b - inset
    t = abs(y) / a
    return zc if t >= 1 else zc + b * (1 - t ** n) ** (1 / n)


# ======================================================================= materiais (paleta: Assets/ArtBible.md)
class _M:
    pass


M = _M()
M.painel = material("NI_Painel", "#5B6480")            # placas dos paineis (cinza-indigo, fundo das etiquetas)
M.moldura = material("NI_Moldura", "#3E4C78")          # molduras e frisos (indigo-ardosia)
M.escuro = material("NI_Escuro", "#2A2C3D")            # knobs, disjuntores, glareshield (nunca preto)
M.tecla = material("NI_Tecla", "#4A5068")
M.metal = material("NI_Metal", "#C3CEDF")
M.branco = material("NI_Branco", "#F4F2EC")
M.linha = material("NI_Linha", "#E8E6E0")              # frisos brancos que agrupam chaves
M.parede = material("NI_Parede", "#DAD6CC")            # forros da cabine
M.parede_esc = material("NI_Parede_Escura", "#A9AEBE")
M.estrutura = material("NI_Estrutura", "#7C8CB8")
M.piso = material("NI_Piso", "#6E7A9A")
M.tecido = material("NI_Tecido", "#4F6496")            # estofado dos assentos
M.cinto = material("NI_Cinto", "#C9B48A")
M.amarelo = material("NI_Amarelo", "#FFC23A")
M.vermelho = material("NI_Vermelho", "#E03C2A")
M.medidor = material("NI_Medidor", "#E9E2C8")
M.lente_apagada = material("NI_Lente_Apagada", "#3E4C78")
M.lente_verde = material("NI_Lente_Verde", "#59FF86", emissao=3.0)
M.lente_ambar = material("NI_Lente_Ambar", "#FFC23A", emissao=3.0)
M.lente_verm = material("NI_Lente_Vermelha", "#FF4A4A", emissao=3.0)
M.lente_branca = material("NI_Lente_Branca", "#FFF4DE", emissao=3.0)
M.luz = material("NI_Luz", "#FFF4DE", emissao=4.0)
M.vidro = material("NI_Vidro", "#9CC8F0", alpha=0.18)
TEXT_DIR = os.path.join(NI, "Texturas")
ATLAS_ETQ = os.path.join(TEXT_DIR, "T_NI_Etiquetas.png")
ATLAS_MEDS = os.path.join(TEXT_DIR, "T_NI_MEDS.png")


def _mat_tex(nome, cor, caminho, emissao=0.0):
    """material com textura do atlas (a imagem e gerada no fim; aqui so a referencia)."""
    os.makedirs(TEXT_DIR, exist_ok=True)
    img = bpy.data.images.get(os.path.basename(caminho))
    if img is None:
        img = bpy.data.images.new(os.path.basename(caminho), 4, 4)
        img.filepath = caminho
        img.source = "FILE"
    m = material(nome, cor, emissao=emissao, imagem=img)
    return m


M.etiqueta = _mat_tex("NI_Etiquetas", "#FFFFFF", ATLAS_ETQ)
M.legenda = _mat_tex("NI_Legendas", "#FFFFFF", ATLAS_ETQ, emissao=1.0)
M.legenda_acesa = _mat_tex("NI_Legendas_Acesas", "#FFFFFF", ATLAS_ETQ, emissao=3.0)
M.tela = _mat_tex("NI_Tela", "#FFFFFF", ATLAS_MEDS, emissao=1.5)


# ======================================================================= atlas de etiquetas e legendas
ATLAS = 2048
COR_FUNDO_ETQ = "#5B6480"         # = NI_Painel (a etiqueta some na placa)
COR_FUNDO_LEG = "#1E2233"


class Atlas:
    """celulas (px) num retangulo do atlas. Devolve UV (u0, v0, u1, v1) por chave."""

    def __init__(self, x0, y0, cw, ch, cols, rows):
        self.x0, self.y0, self.cw, self.ch, self.cols, self.rows = x0, y0, cw, ch, cols, rows
        self.itens = {}          # chave -> indice
        self.lista = []          # (chave, estilo)

    def uv(self, chave, estilo=None):
        if chave not in self.itens:
            if len(self.lista) >= self.cols * self.rows:
                print(f"[atlas] CHEIO, reaproveitando: {chave}")
                return self._uv(0)
            self.itens[chave] = len(self.lista)
            self.lista.append((chave, estilo))
        return self._uv(self.itens[chave])

    def rect_px(self, i):
        c, r = i % self.cols, i // self.cols
        x = self.x0 + c * self.cw
        y = self.y0 + r * self.ch          # y a partir do TOPO da imagem
        return x, y, self.cw, self.ch

    def _uv(self, i):
        x, y, w, h = self.rect_px(i)
        m = 1.5
        return ((x + m) / ATLAS, 1 - (y + h - m) / ATLAS, (x + w - m) / ATLAS, 1 - (y + m) / ATLAS)


ETQ = Atlas(0, 0, 256, 32, 8, 40)                # 320 etiquetas (texto branco no cinza do painel), aspecto 8:1
LEG = Atlas(0, 1280, 128, 48, 16, 16)            # 256 legendas de anunciador (texto colorido em lente escura)
ASPECTO_ETQ = 8.0
ASPECTO_LEG = 128 / 48


# ======================================================================= malhas estaticas e kit
ST = {}


def st(grupo, chanfro=0.0):
    """malha estatica por grupo (cada grupo vira um objeto, combinado por material)."""
    if grupo not in ST:
        m = Malha("NI_" + grupo, origem=tuple(ANC))
        m.chanfro = chanfro
        ST[grupo] = m
    return ST[grupo]


class Kit:
    def __init__(self):
        self.inst = {}            # tipo -> [Matrix]
        self.interativos = []     # {id, tipo, M, ...}

    def por(self, tipo, Mw, interativo=None, **extra):
        if interativo:
            self.interativos.append(dict(id=interativo, tipo=tipo, M=Mw.copy(), **extra))
        else:
            self.inst.setdefault(tipo, []).append(Mw.copy())

    def total(self):
        return sum(len(v) for v in self.inst.values()) + len(self.interativos)


KIT = Kit()


def etiqueta(m, F, u, v, texto, larg=0.05, z=0.0012):
    """etiqueta do atlas sobre o painel F (coords locais u, v = centro)."""
    if not texto:
        return
    u0, v0, u1, v1 = ETQ.uv(texto)
    h = larg / ASPECTO_ETQ
    p = [F @ V((u - larg / 2, v - h / 2, z)), F @ V((u + larg / 2, v - h / 2, z)),
         F @ V((u + larg / 2, v + h / 2, z)), F @ V((u - larg / 2, v + h / 2, z))]
    m.quad(*p, M.etiqueta, uv=((u0, v0), (u1, v0), (u1, v1), (u0, v1)))


def legenda(m, F, u, v, texto, cor="ambar", acesa=False, larg=0.03, z=0.0035):
    u0, v0, u1, v1 = LEG.uv((texto, cor), cor)
    h = larg / ASPECTO_LEG
    p = [F @ V((u - larg / 2, v - h / 2, z)), F @ V((u + larg / 2, v - h / 2, z)),
         F @ V((u + larg / 2, v + h / 2, z)), F @ V((u - larg / 2, v + h / 2, z))]
    m.quad(*p, M.legenda_acesa if acesa else M.legenda, uv=((u0, v0), (u1, v0), (u1, v1), (u0, v1)))


def friso(m, F, u0, v0, u1, v1, w=0.0016, z=0.0008):
    """contorno branco fino (agrupa chaves, como nos paineis reais)."""
    for a, b in (((u0, v0), (u1, v0)), ((u1, v0), (u1, v1)), ((u1, v1), (u0, v1)), ((u0, v1), (u0, v0))):
        a, b = V((*a, z)), V((*b, z))
        d = (b - a).normalized()
        n = V((-d.y, d.x, 0)) * (w / 2)
        a2, b2 = a - d * (w / 2), b + d * (w / 2)
        m.quad(F @ (a2 - n), F @ (b2 - n), F @ (b2 + n), F @ (a2 + n), M.linha)


def tela(m, F, u, v, w, h, pagina, z=0.0):
    """tela MEDS: quad com a pagina 'pagina' (0..11) do atlas 4x3."""
    c, r = pagina % 4, pagina // 4
    u0, u1 = c / 4 + 0.002, (c + 1) / 4 - 0.002
    v1, v0 = 1 - r / 3 - 0.002, 1 - (r + 1) / 3 + 0.002
    p = [F @ V((u - w / 2, v - h / 2, z)), F @ V((u + w / 2, v - h / 2, z)),
         F @ V((u + w / 2, v + h / 2, z)), F @ V((u - w / 2, v + h / 2, z))]
    m.quad(*p, M.tela, uv=((u0, v0), (u1, v0), (u1, v1), (u0, v1)))


def caixa_F(m, F, c, d, mat):
    """caixa no espaco local do painel F."""
    return m.caixa(c, d, mat, M=F)


def painel(F, w, h, codigo=None, esp=0.03, mold=0.010, grupo="Paineis", parafusos=True):
    """placa de painel com moldura em relevo, Dzus nos cantos e o codigo (F7, O14...). Frente em z = 0."""
    m = st(grupo, 0.003)
    m.caixa((0, 0, -esp / 2), (w, h, esp), M.painel, M=F)
    if mold:
        e = 0.004
        m.caixa((0, h / 2 - mold / 2, e / 2), (w, mold, e), M.moldura, M=F)
        m.caixa((0, -h / 2 + mold / 2, e / 2), (w, mold, e), M.moldura, M=F)
        m.caixa((w / 2 - mold / 2, 0, e / 2), (mold, h - 2 * mold, e), M.moldura, M=F)
        m.caixa((-w / 2 + mold / 2, 0, e / 2), (mold, h - 2 * mold, e), M.moldura, M=F)
    if parafusos:
        k = mold + 0.010
        for su in (-1, 1):
            for sv in (-1, 1):
                KIT.por("paraf", F @ T(su * (w / 2 - k), sv * (h / 2 - k), 0))
        if w > 0.5:
            for sv in (-1, 1):
                KIT.por("paraf", F @ T(0, sv * (h / 2 - k), 0))
    if codigo:
        etiqueta(m, F, -w / 2 + mold + 0.035, h / 2 - mold - 0.012, codigo, larg=0.05)
    return m


def hexa(m, p, mat):
    """bloco de 8 cantos: p[i + 2j + 4k], i = tras/frente (X), j = -Y/+Y, k = baixo/cima. Corrige a orientacao sozinho."""
    vs = [m.bm.verts.new(V(q)) for q in p]
    fl = [(0, 2, 3, 1), (4, 5, 7, 6), (0, 1, 5, 4), (2, 6, 7, 3), (0, 4, 6, 2), (1, 3, 7, 5)]
    c = sum((V(q) for q in p), V()) / 8
    fs = [m.bm.faces.new([vs[i] for i in f]) for f in fl]
    f0 = fs[0]
    f0.normal_update()
    if f0.normal.dot(f0.calc_center_median() - c) < 0:
        for f in fs:
            f.normal_flip()
    idx = m.mi(mat)
    for f in fs:
        f.material_index = idx
    return set(fs)


def faixa(m, pts, larg, lado, mat, esp=0.004):
    """cinto/fita: caixas achatadas ao longo de uma polilinha; 'lado' = direcao da largura."""
    lado = V(lado).normalized()
    for a, b in zip(pts[:-1], pts[1:]):
        a, b = V(a), V(b)
        d = (b - a)
        L_ = d.length
        if L_ < 1e-5:
            continue
        x = lado - d.normalized() * lado.dot(d.normalized())
        x.normalize()
        z = d.normalized().cross(x)
        Mx = Matrix((x, d.normalized(), z)).transposed().to_4x4()
        Mx.translation = (a + b) / 2
        m.caixa((0, 0, 0), (larg, L_ + esp, esp), mat, M=Mx)


LUZES = []          # {pos, cor, alcance, intens}
MARCAS = {}         # nome -> posicao (orbitador)


def luz(pos, cor="#FFE9C8", alcance=3.0, intens=1.2):
    LUZES.append(dict(pos=V(pos), cor=cor, alcance=alcance, intens=intens))


# ======================================================================= exportacao
C_UNITY = Matrix(((-1, 0, 0, 0), (0, 0, 1, 0), (0, -1, 0, 0), (0, 0, 0, 1)))   # Blender (x,y,z) -> Unity (-x, z, -y)


def para_unity(Mw):
    """matriz no referencial da ancora, convertida para o Unity (linhas, 16 floats)."""
    Mu = C_UNITY @ (T(*(-ANC)) @ Mw) @ C_UNITY.inverted()
    return [round(Mu[i][j], 5) for i in range(4) for j in range(4)]


def gravar_kit_json(caminho):
    dados = {"tipos": [], "interativos": []}
    for tipo, lst in sorted(KIT.inst.items()):
        dados["tipos"].append({"tipo": tipo, "m": [x for Mw in lst for x in para_unity(Mw)], "n": len(lst)})
    for it in KIT.interativos:
        d = {k: (para_unity(v) if isinstance(v, Matrix) else v) for k, v in it.items() if k != "M"}
        d["m"] = para_unity(it["M"])
        dados["interativos"].append(d)
    dados["luzes"] = [dict(p=para_unity(T(*l["pos"]))[3::4][:3], cor=l["cor"], alcance=l["alcance"], intens=l["intens"])
                      for l in LUZES]
    dados["marcas"] = [dict(nome=k, p=para_unity(T(*v))[3::4][:3]) for k, v in MARCAS.items()]
    os.makedirs(os.path.dirname(caminho), exist_ok=True)
    with open(caminho, "w", encoding="utf-8") as f:
        json.dump(dados, f, ensure_ascii=False, separators=(",", ":"))
    print(f"[kit] {sum(len(v) for v in KIT.inst.values())} instancias em {len(KIT.inst)} tipos, "
          f"{len(KIT.interativos)} interativos -> {caminho}")


def tris_obj(o):
    """triangulos depois dos modificadores (bevel incluso)."""
    dg = bpy.context.evaluated_depsgraph_get()
    me = o.evaluated_get(dg).to_mesh()
    n = sum(len(p.vertices) - 2 for p in me.polygons)
    o.evaluated_get(dg).to_mesh_clear()
    return n
