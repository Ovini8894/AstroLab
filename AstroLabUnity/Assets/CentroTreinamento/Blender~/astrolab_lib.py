"""
Biblioteca de modelagem procedural do AstroLab (Blender 5.2, headless).

Usada por gerar_centro_treinamento.py, gerar_plataforma_lancamento.py e gerar_traje.py.
Convencoes (iguais a Base Lunar): 1 unidade = 1 metro, Z para cima, exporta FBX com -Z forward / Y up,
entao Blender (x, y, z) -> Unity (-x, z, -y).

Prefixos de objeto lidos pelo Unity:
  COL_*   caixa de colisao invisivel (vira BoxCollider, sem renderer)
  ANIM_*  peca animada por script (pivo = origem do objeto)
  INT_*   ponto de interacao / marcador (Empty)
  SPAWN_* / WP_* marcadores (Empty)
"""
import bpy, bmesh, math, os
from mathutils import Vector, Matrix

D = bpy.data
V = Vector


# ======================================================================= cena
def limpar_cena():
    for o in list(D.objects):
        D.objects.remove(o, do_unlink=True)
    for col in (D.meshes, D.materials, D.curves, D.cameras, D.lights, D.images):
        for b in list(col):
            col.remove(b)
    for c in list(D.collections):
        D.collections.remove(c)


def colecao(nome):
    c = D.collections.get(nome)
    if c is None:
        c = D.collections.new(nome)
        bpy.context.scene.collection.children.link(c)
    return c


# ======================================================================= materiais
# Cada material guarda a cor de preview (sRGB) e, opcionalmente, emissao. No Unity os materiais sao
# recriados no shader AstroLab/Toon pelas receitas do Editor (EstiloCentro.cs) — o nome e a chave.
_MATS = {}
# acabamento (passe de qualidade): segmentos minimos pelo tamanho, chanfro nas quinas, letreiros via TextMeshPro
LETREIROS = []           # textos viram dados (TMP no Unity): {texto, matriz, tam, cor, alinhar}
TEXTO_TMP = True


def segs_min(r, segs):
    if r >= 0.25:
        return max(segs, 32)
    if r >= 0.08:
        return max(segs, 20)
    if r >= 0.02:
        return max(segs, 12)
    return segs
TRANSPARENTES = set()   # nomes de materiais com alfa: vao para um objeto <nome>_Transp (sem sombra no Unity)


def _lin(c):
    return tuple(((x / 12.92) if x <= 0.04045 else ((x + 0.055) / 1.055) ** 2.4) for x in c)


def hexcor(h):
    h = h.lstrip("#")
    return tuple(int(h[i:i + 2], 16) / 255.0 for i in (0, 2, 4))


def material(nome, cor, emissao=0.0, alpha=1.0, imagem=None):
    """cor em hex ('#RRGGBB') ou tupla sRGB. Preview: toon de 3 faixas (Shader to RGB + rampa constante)."""
    if nome in _MATS:
        return _MATS[nome]
    if isinstance(cor, str):
        cor = hexcor(cor)
    m = D.materials.new(nome)
    if alpha < 1.0:
        TRANSPARENTES.add(nome)
    m.diffuse_color = (*_lin(cor), alpha)
    m.use_backface_culling = alpha >= 1.0      # igual ao Unity (Toon: Cull Back) -> o preview mostra faces invertidas
    nt = m.node_tree
    nt.nodes.clear()
    out = nt.nodes.new("ShaderNodeOutputMaterial")
    tex = None
    if imagem:
        tex = nt.nodes.new("ShaderNodeTexImage")
        tex.image = imagem if not isinstance(imagem, str) else D.images.load(imagem, check_existing=True)
        cor_sock = tex.outputs["Color"]
        if alpha < 1.0:                                   # decalque: usa o alfa da imagem
            em = nt.nodes.new("ShaderNodeEmission"); tr = nt.nodes.new("ShaderNodeBsdfTransparent")
            mix = nt.nodes.new("ShaderNodeMixShader")
            nt.links.new(cor_sock, em.inputs["Color"])
            nt.links.new(tex.outputs["Alpha"], mix.inputs["Fac"])
            nt.links.new(tr.outputs[0], mix.inputs[1]); nt.links.new(em.outputs[0], mix.inputs[2])
            nt.links.new(mix.outputs[0], out.inputs[0])
            m.surface_render_method = "BLENDED"
            _MATS[nome] = m
            return m
    else:
        rgb = nt.nodes.new("ShaderNodeRGB")
        rgb.outputs[0].default_value = (*_lin(cor), 1)
        cor_sock = rgb.outputs[0]
    if emissao > 0:
        em = nt.nodes.new("ShaderNodeEmission")
        nt.links.new(cor_sock, em.inputs["Color"])
        em.inputs["Strength"].default_value = emissao
        nt.links.new(em.outputs[0], out.inputs[0])
    else:
        dif = nt.nodes.new("ShaderNodeBsdfDiffuse")
        s2r = nt.nodes.new("ShaderNodeShaderToRGB")
        ramp = nt.nodes.new("ShaderNodeValToRGB")
        ramp.color_ramp.interpolation = "CONSTANT"
        el = ramp.color_ramp.elements
        el[0].position = 0.0; el[0].color = (0.30, 0.29, 0.50, 1)       # sombra indigo (igual Unity)
        el[1].position = 0.12; el[1].color = (0.70, 0.69, 0.85, 1)      # meio-tom
        e3 = el.new(0.32); e3.color = (1, 1, 1, 1)
        mul = nt.nodes.new("ShaderNodeMix"); mul.data_type = "RGBA"; mul.blend_type = "MULTIPLY"
        mul.inputs["Factor"].default_value = 1.0
        nt.links.new(dif.outputs[0], s2r.inputs[0])
        nt.links.new(s2r.outputs["Color"], ramp.inputs["Fac"])
        nt.links.new(cor_sock, mul.inputs["A"])
        nt.links.new(ramp.outputs["Color"], mul.inputs["B"])
        if alpha < 1.0:
            tr = nt.nodes.new("ShaderNodeBsdfTransparent")
            mix = nt.nodes.new("ShaderNodeMixShader")
            mix.inputs["Fac"].default_value = alpha
            em2 = nt.nodes.new("ShaderNodeEmission")
            nt.links.new(mul.outputs["Result"], em2.inputs["Color"])
            nt.links.new(tr.outputs[0], mix.inputs[1]); nt.links.new(em2.outputs[0], mix.inputs[2])
            nt.links.new(mix.outputs[0], out.inputs[0])
            m.surface_render_method = "BLENDED"
        else:
            em2 = nt.nodes.new("ShaderNodeEmission")
            nt.links.new(mul.outputs["Result"], em2.inputs["Color"])
            nt.links.new(em2.outputs[0], out.inputs[0])
    _MATS[nome] = m
    return m


# ======================================================================= construtor de malha
class Malha:
    """Acumula geometria (em coordenadas de mundo) num bmesh; materiais por nome."""

    def __init__(self, nome, origem=(0, 0, 0)):
        self.nome = nome
        self.bm = bmesh.new()
        self.uv = self.bm.loops.layers.uv.new("UVMap")
        self.mats = []
        self.origem = V(origem)
        self.uv_fixo = set()   # faces com UV explicito (nao recebem projecao em caixa)
        self.chanfro = 0.015   # largura do bevel nas quinas (0 = sem); o modificador e aplicado na exportacao

    def mi(self, mat):
        nome = mat.name if hasattr(mat, "name") else mat
        if nome not in self.mats:
            self.mats.append(nome)
        return self.mats.index(nome)

    def _marca(self, verts, mat):
        idx = self.mi(mat)
        fs = {f for v in verts for f in v.link_faces}
        for f in fs:
            f.material_index = idx
        return fs

    # ---------------------------------------------------------- primitivas
    def caixa(self, c, d, mat, rz=0.0, rx=0.0, ry=0.0, M=None):
        mtx = (M or Matrix()) @ Matrix.Translation(V(c)) @ Matrix.Rotation(rz, 4, "Z") @ Matrix.Rotation(ry, 4, "Y") \
            @ Matrix.Rotation(rx, 4, "X") @ Matrix.Diagonal((d[0], d[1], d[2], 1))
        r = bmesh.ops.create_cube(self.bm, size=1, matrix=mtx)
        return self._marca(r["verts"], mat)

    def caixa_ab(self, a, b, mat):
        a, b = V(a), V(b)
        return self.caixa((a + b) / 2, (abs(b.x - a.x), abs(b.y - a.y), abs(b.z - a.z)), mat)

    def cilindro(self, a, b, r, mat, segs=12, r2=None, tampas=True):
        a, b = V(a), V(b)
        d = b - a
        if d.length < 1e-6:
            return set()
        rot = d.to_track_quat("Z", "Y").to_matrix().to_4x4()
        mtx = Matrix.Translation((a + b) / 2) @ rot
        segs = segs_min(max(r, r2 or 0), segs)
        res = bmesh.ops.create_cone(self.bm, cap_ends=tampas, cap_tris=False, segments=segs, radius1=r,
                                    radius2=r if r2 is None else r2, depth=d.length, matrix=mtx)
        fs = self._marca(res["verts"], mat)
        eixo = d.normalized()
        for f in fs:
            f.normal_update()
            f.smooth = abs(f.normal.dot(eixo)) < 0.95
        return fs

    def esfera(self, c, r, mat, u=12, v=8, escala=(1, 1, 1), M=None):
        mtx = (M or Matrix()) @ Matrix.Translation(V(c)) @ Matrix.Diagonal((*escala, 1))
        rr = r * max(escala) * (M.to_scale().length / 1.732 if M is not None else 1.0)
        u = segs_min(rr, u); v = max(v, u // 2)
        res = bmesh.ops.create_uvsphere(self.bm, u_segments=u, v_segments=v, radius=r, matrix=mtx)
        fs = self._marca(res["verts"], mat)
        for f in fs:
            f.smooth = True
        return fs

    def torus(self, c, R, r, mat, segs=24, segs2=8, M=None, arco=360.0):
        """anel no plano XY local. arco < 360 faz um pedaco aberto."""
        M = (M or Matrix()) @ Matrix.Translation(V(c))
        segs = max(segs, 48 if R >= 1.0 else 32 if R >= 0.25 else segs)
        fechado = arco >= 359.9
        n = segs if fechado else segs + 1
        aneis = []
        for i in range(n):
            a = math.radians(arco) * i / segs
            ca, sa = math.cos(a), math.sin(a)
            anel = []
            for j in range(segs2):
                b = 2 * math.pi * j / segs2
                rr = R + r * math.cos(b)
                anel.append(self.bm.verts.new(M @ V((rr * ca, rr * sa, r * math.sin(b)))))
            aneis.append(anel)
        fs = []
        for i in range(segs if fechado else segs):
            i2 = (i + 1) % n
            for j in range(segs2):
                j2 = (j + 1) % segs2
                fs.append(self.bm.faces.new((aneis[i][j], aneis[i2][j], aneis[i2][j2], aneis[i][j2])))
        idx = self.mi(mat)
        for f in fs:
            f.material_index = idx
            f.smooth = True
        return set(fs)

    def torno(self, perfil, mat, segs=24, M=None, fechar_base=True, fechar_topo=True):
        """superficie de revolucao em torno do Z local. perfil = [(raio, z), ...] de baixo para cima."""
        M = M or Matrix()
        segs = segs_min(max(r for r, _ in perfil) * M.to_scale().length / 1.732, segs)
        aneis = []
        for r, z in perfil:
            if r <= 1e-5:
                aneis.append([self.bm.verts.new(M @ V((0, 0, z)))])
            else:
                aneis.append([self.bm.verts.new(M @ V((r * math.cos(2 * math.pi * k / segs), r * math.sin(2 * math.pi * k / segs), z)))
                              for k in range(segs)])
        fs = []
        for j in range(len(aneis) - 1):
            a, b = aneis[j], aneis[j + 1]
            for k in range(segs):
                k2 = (k + 1) % segs
                # ordem anti-horaria vista de fora: normais para FORA (o shader Toon descarta as faces de tras)
                if len(a) == 1:
                    fs.append(self.bm.faces.new((a[0], b[k2], b[k])))
                elif len(b) == 1:
                    fs.append(self.bm.faces.new((a[k], a[k2], b[0])))
                else:
                    fs.append(self.bm.faces.new((a[k], a[k2], b[k2], b[k])))
        for f in fs:
            f.smooth = True
        if fechar_base and len(aneis[0]) > 2:
            fs.append(self.bm.faces.new(list(reversed(aneis[0]))))
        if fechar_topo and len(aneis[-1]) > 2:
            fs.append(self.bm.faces.new(aneis[-1]))
        idx = self.mi(mat)
        for f in fs:
            f.material_index = idx
        return set(fs)

    def prisma(self, pts, z0, z1, mat, M=None):
        """poligono 2D (xy) extrudado de z0 a z1 (pode ser concavo)."""
        M = M or Matrix()
        n = len(pts)
        base = [self.bm.verts.new(M @ V((x, y, z0))) for x, y in pts]
        topo = [self.bm.verts.new(M @ V((x, y, z1))) for x, y in pts]
        area = sum(pts[i][0] * pts[(i + 1) % n][1] - pts[(i + 1) % n][0] * pts[i][1] for i in range(n))
        if area < 0:
            base.reverse(); topo.reverse()
        fs = [self.bm.faces.new(list(reversed(base))), self.bm.faces.new(topo)]
        for i in range(n):
            i2 = (i + 1) % n
            fs.append(self.bm.faces.new((base[i], base[i2], topo[i2], topo[i])))
        tri = bmesh.ops.triangulate(self.bm, faces=fs[:2], quad_method="BEAUTY", ngon_method="EAR_CLIP")
        idx = self.mi(mat)
        todas = set(fs[2:]) | set(tri["faces"])
        for f in todas:
            f.material_index = idx
        return todas

    def quad(self, p0, p1, p2, p3, mat, uv=((0, 0), (1, 0), (1, 1), (0, 1)), duplo=False):
        """quad com UV explicito (telas, placas, logos). p0..p3 anti-horario visto de frente."""
        vs = [self.bm.verts.new(V(p)) for p in (p0, p1, p2, p3)]
        f = self.bm.faces.new(vs)
        f.material_index = self.mi(mat)
        for loop, t in zip(f.loops, uv):
            loop[self.uv].uv = t
        self.uv_fixo.add(f)
        out = {f}
        if duplo:
            vs2 = [self.bm.verts.new(V(p)) for p in (p3, p2, p1, p0)]
            f2 = self.bm.faces.new(vs2)
            f2.material_index = f.material_index
            for loop, t in zip(f2.loops, (uv[3], uv[2], uv[1], uv[0])):
                loop[self.uv].uv = t
            self.uv_fixo.add(f2)
            out.add(f2)
        return out

    def painel_tex(self, centro, largura, altura, normal, mat, uv=((0, 0), (1, 0), (1, 1), (0, 1)), cima=(0, 0, 1)):
        """quad texturizado centrado, de frente para 'normal'."""
        n = V(normal).normalized()
        up = V(cima)
        if abs(n.dot(up)) > 0.99:
            up = V((0, 1, 0))
        dx = up.cross(n).normalized() * (largura / 2)
        dy = n.cross(dx).normalized() * (altura / 2)
        c = V(centro)
        return self.quad(c - dx - dy, c + dx - dy, c + dx + dy, c - dx + dy, mat, uv)

    def tubo(self, pontos, r, mat, segs=8):
        """cano por uma polilinha, com esferas nas juntas."""
        for a, b in zip(pontos[:-1], pontos[1:]):
            self.cilindro(a, b, r, mat, segs, tampas=False)
        for p in pontos[1:-1]:
            self.esfera(p, r, mat, segs, max(4, segs // 2))
        self.esfera(pontos[0], r, mat, segs, max(4, segs // 2))
        self.esfera(pontos[-1], r, mat, segs, max(4, segs // 2))

    def texto(self, s, pos, tam, mat, rz=0.0, rx=math.pi / 2, espessura=0.02, alinhar="CENTER", fonte=None, M=None, malha=False):
        """texto. Por padrao vira um LETREIRO TextMeshPro no Unity (nitido a qualquer distancia); malha=True so onde
        precisa ser geometria (logo renderizado, peca animada). Fica em pe (rx=90), virado para -Y; rz gira em Z."""
        if TEXTO_TMP and not malha:
            W = (M or Matrix()) @ Matrix.Translation(V(pos)) @ Matrix.Rotation(rz, 4, "Z") @ Matrix.Rotation(rx, 4, "X")
            LETREIROS.append({"texto": s, "W": W, "tam": tam, "cor": mat.name if hasattr(mat, "name") else mat,
                              "alinhar": alinhar, "espessura": espessura})
            return set()
        cu = D.curves.new("tmp_txt", "FONT")
        cu.body = s
        cu.size = tam
        cu.extrude = espessura / 2
        cu.align_x = alinhar
        cu.align_y = "CENTER"
        cu.resolution_u = 3
        if fonte:
            cu.font = fonte
        ob = D.objects.new("tmp_txt", cu)
        bpy.context.scene.collection.objects.link(ob)
        dg = bpy.context.evaluated_depsgraph_get()
        me = ob.evaluated_get(dg).to_mesh()
        mtx = (M or Matrix()) @ Matrix.Translation(V(pos)) @ Matrix.Rotation(rz, 4, "Z") @ Matrix.Rotation(rx, 4, "X")
        me.transform(mtx)
        n0 = len(self.bm.verts)
        self.bm.from_mesh(me)
        self.bm.verts.ensure_lookup_table()
        novos = self.bm.verts[n0:]
        ob.evaluated_get(dg).to_mesh_clear()
        D.objects.remove(ob, do_unlink=True)
        D.curves.remove(cu)
        idx = self.mi(mat)
        fs = {f for v in novos for f in v.link_faces}
        for f in fs:
            f.material_index = idx
        return fs

    def incorporar(self, outra, M=None):
        """copia a geometria de outra Malha (ou mesh) aplicando a matriz M."""
        me = D.meshes.new("tmp")
        outra.bm.to_mesh(me)
        if M is not None:
            me.transform(M)
        n0 = len(self.bm.verts)
        nf0 = len(self.bm.faces)
        self.bm.from_mesh(me)
        self.bm.faces.ensure_lookup_table()
        for f in self.bm.faces[nf0:]:
            f.material_index = self.mi(outra.mats[f.material_index]) if f.material_index < len(outra.mats) else 0
        D.meshes.remove(me)

    # ---------------------------------------------------------- finalizar
    def uv_caixa(self, escala=1.0):
        """projecao em caixa (mundo): textura repete a cada 'escala' metros."""
        for f in self.bm.faces:
            if f in self.uv_fixo:
                continue
            n = f.normal
            ax = max(range(3), key=lambda i: abs(n[i]))
            for loop in f.loops:
                co = loop.vert.co
                if ax == 0:
                    u, v = (co.y if n.x > 0 else -co.y), co.z
                elif ax == 1:
                    u, v = (-co.x if n.y > 0 else co.x), co.z
                else:
                    u, v = co.x, (co.y if n.z > 0 else -co.y)
                loop[self.uv].uv = (u / escala, v / escala)

    def criar(self, col=None, uv_escala=1.0, suave=False, separar_transp=True):
        if len(self.bm.faces) == 0:
            self.bm.free()
            return None
        idx_t = {i for i, n in enumerate(self.mats) if n in TRANSPARENTES}
        if separar_transp and idx_t and any(f.material_index not in idx_t for f in self.bm.faces):
            # copia so as faces transparentes para um objeto irmao (vidros, agua, logos: sem sombra no Unity)
            t = Malha(self.nome + "_Transp", self.origem)
            self.bm.faces.index_update()
            fixo_orig = {f.index for f in self.uv_fixo if f.is_valid}
            t.bm.free(); t.bm = self.bm.copy(); t.uv = t.bm.loops.layers.uv.active; t.mats = list(self.mats)
            t.bm.faces.ensure_lookup_table(); self.bm.faces.ensure_lookup_table()
            t.uv_fixo = {t.bm.faces[i] for i in fixo_orig}
            bmesh.ops.delete(t.bm, geom=[f for f in t.bm.faces if f.material_index not in idx_t], context="FACES")
            bmesh.ops.delete(self.bm, geom=[f for f in self.bm.faces if f.material_index in idx_t], context="FACES")
            self.uv_fixo = {f for f in self.uv_fixo if f.is_valid}
            ot = t.criar(col, uv_escala, suave, separar_transp=False)
            self.irmao_transp = ot
        bmesh.ops.remove_doubles(self.bm, verts=self.bm.verts, dist=1e-5)
        self.bm.normal_update()
        # "auto smooth" a 32 graus: tudo suave, arestas vivas marcadas como duras
        for f in self.bm.faces:
            f.smooth = True
        for e in self.bm.edges:
            if len(e.link_faces) == 2 and e.calc_face_angle(0.0) > math.radians(32):
                e.smooth = False
        self.uv_fixo = {f for f in self.uv_fixo if f.is_valid}
        self.uv_caixa(uv_escala)
        bmesh.ops.translate(self.bm, verts=self.bm.verts, vec=-self.origem)
        me = D.meshes.new(self.nome)
        self.bm.to_mesh(me)
        self.bm.free()
        for n in self.mats:
            me.materials.append(_MATS.get(n) or D.materials.get(n))
        if suave:
            for p in me.polygons:
                p.use_smooth = True
        ob = D.objects.new(self.nome, me)
        ob.location = self.origem
        (col or bpy.context.scene.collection).objects.link(ob)
        if self.chanfro:
            acabamento(ob, self.chanfro)
        return ob


def acabamento(ob, largura=0.015):
    """bevel (2 segmentos, harden normals) nas quinas > 40 graus + weighted normal: tira a cara de "caixa"."""
    b = ob.modifiers.new("Chanfro", "BEVEL")
    b.width = largura; b.segments = 2; b.limit_method = "ANGLE"; b.angle_limit = math.radians(40)
    b.harden_normals = True; b.use_clamp_overlap = True; b.miter_outer = "MITER_ARC"
    w = ob.modifiers.new("NormaisPonderadas", "WEIGHTED_NORMAL"); w.keep_sharp = True; w.weight = 50


def letreiros_json(caminho, prefixo="TXT"):
    """grava os letreiros para o Unity: posicao/eixos ja convertidos (Blender x,y,z -> Unity -x,z,-y)."""
    import json
    def U(v):
        return [round(-v.x, 4), round(v.z, 4), round(-v.y, 4)]
    itens = []
    for i, L in enumerate(LETREIROS):
        W = L["W"]
        itens.append({"nome": f"{prefixo}_{i:03d}", "texto": L["texto"], "pos": U(W.translation),
                      "direita": U(W.col[0].xyz.normalized()), "cima": U(W.col[1].xyz.normalized()),
                      "normal": U(W.col[2].xyz.normalized()), "tam": round(L["tam"] * W.col[1].xyz.length, 4),
                      "cor": L["cor"], "alinhar": L["alinhar"]})
    os.makedirs(os.path.dirname(os.path.abspath(caminho)), exist_ok=True)
    with open(caminho, "w", encoding="utf-8") as f:
        json.dump({"letreiros": itens}, f, ensure_ascii=False, indent=1)
    print(f"[letreiros] {len(itens)} em {caminho}")


def vazio(nome, pos, col=None, rz=0.0):
    e = D.objects.new(nome, None)
    e.location = V(pos)
    e.rotation_euler = (0, 0, rz)
    e.empty_display_size = 0.5
    (col or bpy.context.scene.collection).objects.link(e)
    return e


def colisor(nome, c, d, col=None, rz=0.0):
    """caixa de colisao: cubo unitario escalado pelo objeto (BoxCollider no Unity = bounds locais)."""
    me = D.meshes.get("COL_unitario")
    if me is None:
        bm = bmesh.new(); bmesh.ops.create_cube(bm, size=1); me = D.meshes.new("COL_unitario"); bm.to_mesh(me); bm.free()
    o = D.objects.new(nome if nome.startswith("COL_") else "COL_" + nome, me)
    o.location = V(c); o.scale = V(d); o.rotation_euler = (0, 0, rz)
    o.display_type = "WIRE"
    (col or bpy.context.scene.collection).objects.link(o)
    return o


# ======================================================================= render de preview (autoavaliacao)
def preparar_render(res=(1280, 720), sol_rot=(math.radians(50), 0, math.radians(35)), mundo="#9CC8F0", contorno=True):
    sc = bpy.context.scene
    sc.render.engine = "BLENDER_EEVEE"
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.film_transparent = False
    sc.view_settings.view_transform = "Standard"
    w = D.worlds.get("Mundo") or D.worlds.new("Mundo")
    w.use_nodes = True
    bg = w.node_tree.nodes.get("Background")
    bg.inputs[0].default_value = (*_lin(hexcor(mundo)), 1)
    bg.inputs[1].default_value = 0.6
    sc.world = w
    sol = D.objects.get("PREVIEW_Sol")
    if sol is None:
        ld = D.lights.new("PREVIEW_Sol", "SUN"); ld.energy = 3.5; ld.angle = 0.05
        sol = D.objects.new("PREVIEW_Sol", ld); sc.collection.objects.link(sol)
    sol.rotation_euler = sol_rot
    sc.render.use_freestyle = contorno
    if contorno:
        sc.render.line_thickness_mode = "ABSOLUTE"; sc.render.line_thickness = 1.2
        vl = sc.view_layers[0]
        vl.use_freestyle = True
        fs = vl.freestyle_settings
        ls = fs.linesets[0] if fs.linesets else fs.linesets.new("L")
        ls.select_by_visibility = True
        ls.select_silhouette = True; ls.select_border = True; ls.select_crease = True
        ls.linestyle.color = (0.11, 0.10, 0.24)
        fs.crease_angle = math.radians(140)


def renderizar(caminho, pos, alvo, lente=28.0, ortho=None, cima=None):
    sc = bpy.context.scene
    cam = D.objects.get("PREVIEW_Cam")
    if cam is None:
        cd = D.cameras.new("PREVIEW_Cam"); cam = D.objects.new("PREVIEW_Cam", cd); sc.collection.objects.link(cam)
    cam.data.lens = lente
    cam.data.clip_end = 3000
    if ortho:
        cam.data.type = "ORTHO"; cam.data.ortho_scale = ortho
    else:
        cam.data.type = "PERSP"
    cam.location = V(pos)
    f = (V(alvo) - V(pos)).normalized()
    if cima is None:
        cam.rotation_euler = f.to_track_quat("-Z", "Y").to_euler()
    else:                                                  # camera com "cima" arbitrario (ex.: astronauta deitado)
        z = -f
        x = V(cima).cross(z).normalized()
        y = z.cross(x)
        cam.rotation_euler = Matrix((x, y, z)).transposed().to_euler()
    sc.camera = cam
    sc.render.filepath = caminho
    bpy.ops.render.render(write_still=True)
    print("[preview]", caminho)


def remover_preview():
    for n in ("PREVIEW_Cam", "PREVIEW_Sol"):
        o = D.objects.get(n)
        if o:
            D.objects.remove(o, do_unlink=True)


# ======================================================================= exportar
def exportar_fbx(caminho, objetos):
    os.makedirs(os.path.dirname(os.path.abspath(caminho)), exist_ok=True)
    bpy.ops.object.select_all(action="DESELECT")
    for o in objetos:
        o.select_set(True)
    bpy.context.view_layer.objects.active = objetos[0]
    bpy.ops.export_scene.fbx(filepath=os.path.abspath(caminho), use_selection=True, object_types={"MESH", "EMPTY"},
                             apply_unit_scale=True, apply_scale_options="FBX_SCALE_NONE",
                             axis_forward="-Z", axis_up="Y", bake_space_transform=False,
                             mesh_smooth_type="FACE", use_mesh_modifiers=True, add_leaf_bones=False,
                             path_mode="AUTO", embed_textures=False, use_custom_props=False)
    tris = sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objetos if o.type == "MESH" and not o.name.startswith("COL_"))
    print(f"[export] {caminho}: {len(objetos)} objetos, ~{tris} triangulos")
    return tris


def contar_tris(objs):
    return sum(sum(len(p.vertices) - 2 for p in o.data.polygons) for o in objs if o.type == "MESH" and not o.name.startswith("COL_"))
