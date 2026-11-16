"""
Terra do menu — versão cartoon com a geografia REAL (para o Brasil aparecer), na mesma paleta da Terra
do crateras_e_terra.py (Base Lunar).

Uso:
  blender -b --factory-startup --python terra_menu.py -- <blue_marble.png> <saida_terra.png> <saida_nuvens.png>

Entrada: mapa equirretangular NASA Blue Marble (domínio público), ex. earth_2700x1350.png do repositório
nasa/virgo (src/virgo/images/earth). Não é copiado para o projeto: só as texturas cartoon geradas.

- Classifica cada pixel (oceano raso/fundo, gelo, deserto, savana, campo, floresta, montanha) e pinta com
  as cores chapadas da paleta; litoral com areia do lado da terra e traço escuro do lado do mar.
- Destaque sutil: verde um pouco mais saturado na Amazônia e nuvens mais ralas sobre o Brasil.
"""
import bpy, sys, os, math
import numpy as np
from mathutils import Vector, noise

argv = sys.argv[sys.argv.index("--") + 1:]
ENTRADA, SAIDA_TERRA, SAIDA_NUVENS = argv[0], argv[1], argv[2]
TW, TH = 2048, 1024


def hex_srgb(h):
    h = h.lstrip("#")
    return np.array([int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4)])


C = hex_srgb

# ---------------------------------------------------------------- lê e reamostra o Blue Marble (linhas de baixo para cima)
im = bpy.data.images.load(os.path.abspath(ENTRADA))
im.colorspace_settings.name = "sRGB"
w0, h0 = im.size
src = np.array(im.pixels[:], dtype=np.float32).reshape(h0, w0, 4)[..., :3]
# pixels do Blender já são lineares para imagens sRGB? (são valores do arquivo em float) — trate como sRGB
ys = ((np.arange(TH) + 0.5) / TH * h0).astype(int).clip(0, h0 - 1)
xs = ((np.arange(TW) + 0.5) / TW * w0).astype(int).clip(0, w0 - 1)
rgb = src[ys][:, xs]
r, g, b = rgb[..., 0], rgb[..., 1], rgb[..., 2]
lum = 0.3 * r + 0.59 * g + 0.11 * b

lon = ((np.arange(TW) + 0.5) / TW * 360.0 - 180.0)[None, :].repeat(TH, 0)
lat = ((np.arange(TH) + 0.5) / TH * 180.0 - 90.0)[:, None].repeat(TW, 1)
alat = np.abs(lat)

# ---------------------------------------------------------------- classificação
mar = (b > r + 0.04) & (b >= g - 0.02) & (lum < 0.55)
gelo = (lum > 0.72) & (np.abs(r - b) < 0.12)
gelo |= alat > 78
terra = ~mar
deserto = terra & ~gelo & (r > g * 1.05) & (r > b * 1.35) & (lum > 0.38)
seco = terra & ~gelo & ~deserto & (r > g * 0.92) & (lum > 0.28)
floresta = terra & ~gelo & ~deserto & ~seco & (g > r * 1.05) & (lum < 0.30)
montanha = terra & ~gelo & (lum > 0.45) & ~deserto & (np.abs(r - g) < 0.06) & (b < r)

# limpa pixels soltos (moda 3x3 simples para terra/mar)
def suaviza(m, n=1):
    for _ in range(n):
        s = sum(np.roll(np.roll(m.astype(np.int8), dy, 0), dx, 1) for dy in (-1, 0, 1) for dx in (-1, 0, 1))
        m = s >= 5
    return m

terra = suaviza(terra, 2)
mar = ~terra

# rótulos por pixel e filtro de maioria (manchas chapadas de desenho, sem pontilhado)
MAR, CAMPO, FLORESTA, SECO, DESERTO, MONTANHA, GELO = range(7)
rot = np.full((TH, TW), CAMPO, dtype=np.int8)
rot[terra & floresta] = FLORESTA
rot[terra & seco] = SECO
rot[terra & deserto] = DESERTO
rot[terra & montanha] = MONTANHA
rot[terra & gelo] = GELO
rot[mar] = MAR
rot[alat > 80] = GELO


def maioria(rotulos, raio=2, passes=2, classes=range(1, 7)):
    for _ in range(passes):
        melhor = rotulos.copy(); cont_max = np.zeros(rotulos.shape, dtype=np.int16)
        for c in classes:
            m = (rotulos == c).astype(np.int16)
            s_ = sum(np.roll(np.roll(m, dy, 0), dx, 1) for dy in range(-raio, raio + 1) for dx in range(-raio, raio + 1))
            troca = (s_ > cont_max) & (rotulos != MAR)
            melhor[troca] = c; cont_max[troca] = s_[troca]
        rotulos = melhor
    return rotulos


rot = maioria(rot)
img = np.zeros((TH, TW, 3))
img[:] = C("#2A6FE0")                                             # oceano
perto = np.zeros_like(terra)
for dy in range(-4, 5):
    for dx in range(-4, 5):
        if dy * dy + dx * dx <= 16:
            perto |= np.roll(np.roll(terra, dy, 0), dx, 1)
img[perto & mar] = C("#49B6F2")                                   # águas rasas
img[rot == CAMPO] = C("#4CC35A")
img[rot == FLORESTA] = C("#2F9A4A")
img[rot == SECO] = C("#A8C94A")
img[rot == DESERTO] = C("#F0BE52")
img[rot == MONTANHA] = C("#9C7556")
img[rot == GELO] = C("#F5FAFF")
floresta = rot == FLORESTA
montanha = rot == MONTANHA
gelo = rot == GELO

# destaque sutil: Amazônia um pouco mais saturada (só a floresta, dentro da paleta)
amazonia = terra & floresta & (lon > -76) & (lon < -46) & (lat > -14) & (lat < 6)
img[amazonia] = C("#24A548")

# litoral: areia do lado da terra, traço escuro do lado do mar (mesmo truque do crateras_e_terra.py)
viz_t = np.zeros_like(terra); viz_m = np.zeros_like(terra)
for ax_, sh in ((0, 1), (0, -1), (1, 1), (1, -1)):
    viz_t |= np.roll(terra, sh, axis=ax_)
    viz_m |= ~np.roll(terra, sh, axis=ax_)
img[viz_t & mar & ~gelo] = C("#1F3E9A")
img[terra & viz_m & ~gelo & ~montanha] = C("#F6DE9A")
print(f"[Terra] terra%={terra.mean() * 100:.1f} floresta%={(terra & floresta).mean() * 100:.1f} amazonia px={amazonia.sum()}")

# ---------------------------------------------------------------- nuvens (ruído na esfera), ralas sobre o Brasil
def campo(escala, oitavas, deslocar, deformar=0.0):
    w, h = TW // 2, TH // 2
    lo = (np.arange(w) + 0.5) / w * 2 * math.pi - math.pi
    la = (np.arange(h) + 0.5) / h * math.pi - math.pi / 2
    out = np.empty((h, w))
    off = Vector(deslocar)
    for j in range(h):
        cl, sl = math.cos(la[j]), math.sin(la[j])
        for i in range(w):
            p = Vector((cl * math.cos(lo[i]), cl * math.sin(lo[i]), sl)) * escala + off
            if deformar:
                p = p + Vector(noise.noise_vector(p * 0.5)) * deformar
            out[j, i] = noise.fractal(p, 0.9, 2.0, oitavas, noise_basis="PERLIN_ORIGINAL")
    return out.repeat(2, 0).repeat(2, 1)


nuv = campo(2.6, 5, (5.5, 9.1, 2.2), 1.2)
nthr = np.quantile(nuv, 0.76)
alfa = np.clip((nuv - nthr) / 0.05, 0, 1) * 0.9
alfa *= np.clip(1.2 - alat / 90.0, 0.3, 1.0)
# Brasil (~ -74..-34 lon, -34..6 lat): nuvens bem ralas, com borda suave
dx = np.clip(np.maximum(-74 - lon, lon + 34) / 8.0, 0, 1)
dy = np.clip(np.maximum(-34 - lat, lat - 6) / 8.0, 0, 1)
fora = np.maximum(dx, dy)
alfa *= 0.18 + 0.82 * fora


def salvar_png(caminho, rgb_, a=None):
    h, w = rgb_.shape[:2]
    out = bpy.data.images.new(os.path.basename(caminho), w, h, alpha=a is not None)
    px = np.ones((h, w, 4), dtype=np.float32)
    px[..., :3] = rgb_
    if a is not None:
        px[..., 3] = a
    out.pixels.foreach_set(px.ravel())
    out.filepath_raw = os.path.abspath(caminho)
    out.file_format = "PNG"
    out.save()
    print("[Salvo]", os.path.abspath(caminho))


salvar_png(SAIDA_TERRA, img)
salvar_png(SAIDA_NUVENS, np.ones((TH, TW, 3)), alfa)
