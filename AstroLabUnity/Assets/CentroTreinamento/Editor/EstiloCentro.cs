using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Random = System.Random;

namespace AstroLab.Centro.Editor
{
    // Estilo visual do Centro de Treinamento (Terra, de dia): mesmo shader cartoon da Lua (AstroLab/Toon),
    // texturas desenhadas por código, céu azul com nuvens e pós-processamento próprio.
    // Os nomes dos materiais vêm do Blender (CT_*, NPC_*) e cada um tem uma receita aqui.
    public static class EstiloCentro
    {
        public const string Raiz = "Assets/CentroTreinamento";
        public const string TexDir = Raiz + "/Texturas";
        public const string MatDir = Raiz + "/Materiais";
        const string TexLua = "Assets/BaseLunar/Texturas/Cartoon";   // reaproveita (só leitura) regolito e tela da Lua

        public static Color Hx(string h) => ColorUtility.TryParseHtmlString(h, out var c) ? c : Color.magenta;
        static Color Hdr(Color srgb, float i) => (srgb.linear * i).gamma;

        // ------------------------------------------------------------------ paleta (igual ao gerador do Blender)
        public static readonly Dictionary<string, string> Cores = new Dictionary<string, string>
        {
            { "CT_Parede", "#EDE6D6" }, { "CT_Parede_Interna", "#D5DAE6" }, { "CT_Azul_NASA", "#2456B8" }, { "CT_Azul_Escuro", "#17306E" },
            { "CT_Estrutura", "#5D6E8C" }, { "CT_Metal", "#B4C2D9" }, { "CT_Piso_Hangar", "#A9B1C2" }, { "CT_Amarelo", "#FFC23A" },
            { "CT_Vermelho_NASA", "#E03C2A" }, { "CT_Branco", "#F8F5EE" }, { "CT_Console", "#2E3550" }, { "CT_Borracha", "#33364A" },
            { "CT_Vidro", "#A8ECFF" }, { "CT_Tela", "#47E6FF" }, { "CT_Tela_ECG", "#59FF86" }, { "CT_Tela_VR", "#3D8BFF" },
            { "CT_Luz", "#FFF4DE" }, { "CT_Luz_Verde", "#59FF86" }, { "CT_Luz_Ambar", "#FFB45C" }, { "CT_Luz_Vermelha", "#FF4A4A" },
            { "CT_Grama", "#72B85C" }, { "CT_Asfalto", "#4A4E5C" }, { "CT_Calcada", "#D2CCBE" }, { "CT_Folha", "#4C9A48" },
            { "CT_Folha_Clara", "#7CC25A" }, { "CT_Tronco", "#7A5A44" }, { "CT_Agua", "#3AA7F0" }, { "CT_Azulejo", "#8FD3F0" },
            { "CT_Regolito", "#A9A5C0" }, { "CT_Rocha", "#7F7A9E" }, { "CT_Tecido_Azul", "#3D5BC9" }, { "CT_Laranja", "#FF8C3A" },
            { "CT_Madeira", "#C79A6B" }, { "CT_Tapete_Azul", "#5E7FD6" }, { "CT_Verde_Bandeira", "#1E9A4A" },
            { "CT_Texto_Branco", "#FFFFFF" }, { "CT_Texto_Escuro", "#1C1A3D" }, { "CT_Logo_NASA", "#FFFFFF" },
            { "NPC_Macacao", "#2F5BB7" }, { "NPC_Macacao_Escuro", "#22428A" }, { "NPC_Pele", "#C68B63" }, { "NPC_Cabelo", "#3B2A22" },
            { "NPC_Bota", "#2B2B33" }, { "No Name", "#FFFFFF" },
            { "OR_Branco", "#F4F2EC" }, { "OR_Preto", "#2A2C38" }, { "OR_Cinza_RCC", "#5B5F70" }, { "OR_Laranja_ET", "#D9772E" },
            { "OR_Laranja_ET_Escuro", "#B05A22" }, { "OR_SRB", "#EDEBE4" }, { "OR_SRB_Escuro", "#3A3D4A" }, { "OR_Motor", "#7E879A" },
            { "OR_Motor_Interno", "#2E3550" }, { "OR_Vidro_Cabine", "#1E2A44" }, { "OR_Torre", "#B5452F" }, { "OR_Torre_Cinza", "#8E96A8" },
            { "OR_Concreto", "#B8B4A8" }, { "OR_Concreto_Escuro", "#6E6B66" }, { "OR_Grade", "#7D8696" },
            { "OR_Interior", "#C9CCD2" }, { "OR_Piso_Cabine", "#5D667A" }, { "OR_Painel", "#3B4256" }, { "OR_Painel_Claro", "#7A849A" },
            { "OR_Tela_MFD", "#47E6FF" },
            { "CT_Painel_Liso", "#E6E9F0" }, { "CT_Madeira_Clara", "#D9B98C" }, { "CT_Sofa", "#3D5BC9" }, { "CT_Foto_Missao", "#FFFFFF" }, { "OR_Assento", "#4E5B78" }, { "OR_Cinto", "#C9A64A" }, { "NPC_Olho", "#1C1A3D" }, { "NPC_Boca", "#8A4A3A" },
        };

        // ------------------------------------------------------------------ materiais
        public static Material Material(string nome)
        {
            Directory.CreateDirectory(MatDir);
            string path = $"{MatDir}/{nome}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            var toon = Shader.Find("AstroLab/Toon");
            if (m == null) { m = new Material(toon); AssetDatabase.CreateAsset(m, path); }
            Configurar(m, nome);
            AssetDatabase.SaveAssetIfDirty(m);
            return m;
        }

        static Texture2D T(string nome) =>
            AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexDir}/{nome}.png") ?? AssetDatabase.LoadAssetAtPath<Texture2D>($"{TexLua}/{nome}.png");

        public static void Configurar(Material m, string nome)
        {
            m.shader = Shader.Find("AstroLab/Toon");
            var cor = Cores.TryGetValue(nome, out var h) ? Hx(h) : Color.white;
            if (!Cores.ContainsKey(nome)) Debug.LogWarning($"[Centro] Material {nome} sem receita: ficou branco.");
            m.SetTexture("_BaseMap", null);
            m.SetTextureScale("_BaseMap", Vector2.one);
            m.SetColor("_BaseColor", cor);
            m.SetColor("_EmissionColor", Color.black);
            m.SetFloat("_EmissaoUsaTextura", 0f);
            m.SetFloat("_SemNevoa", 0f);
            m.SetFloat("_FaixaDesloc", 0f);
            m.SetFloat("_RimForca", 0.22f);
            m.SetFloat("_RimLargura", 0.25f);
            m.SetFloat("_BrilhoTamanho", 0f);
            m.SetFloat("_BrilhoForca", 0.6f);
            Opaco(m);

            void Tex(string t, float escala) { m.SetTexture("_BaseMap", T(t)); m.SetTextureScale("_BaseMap", Vector2.one * escala); }
            void Brilho(float tam, float f) { m.SetFloat("_BrilhoTamanho", tam); m.SetFloat("_BrilhoForca", f); }
            void Luz(float i) { m.SetColor("_EmissionColor", Hdr(cor, i)); m.SetFloat("_RimForca", 0f); }
            void Tela(string t, float i) { m.SetTexture("_BaseMap", T(t)); m.SetColor("_BaseColor", Color.white); m.SetColor("_EmissionColor", Hdr(Color.white, i)); m.SetFloat("_EmissaoUsaTextura", 1f); m.SetFloat("_RimForca", 0f); }

            switch (nome)
            {
                case "CT_Parede": Tex("T_CT_Parede", 0.25f); break;
                case "CT_Parede_Interna": Tex("T_CT_Parede", 0.25f); break;
                case "CT_Azul_NASA": Tex("T_CT_Parede", 0.25f); break;
                case "CT_Piso_Hangar": Tex("T_CT_Piso", 0.125f); m.SetFloat("_RimForca", 0.08f); break;
                case "CT_Grama": Tex("T_CT_Grama", 0.25f); m.SetFloat("_RimForca", 0.05f); break;
                case "CT_Asfalto": Tex("T_CT_Asfalto", 0.25f); m.SetFloat("_RimForca", 0.05f); break;
                case "CT_Calcada": Tex("T_CT_Calcada", 0.25f); m.SetFloat("_RimForca", 0.05f); break;
                case "CT_Azulejo": Tex("T_CT_Azulejo", 0.5f); m.SetFloat("_RimForca", 0.05f); break;
                case "CT_Regolito": Tex("T_Regolito_Toon", 0.5f); m.SetFloat("_FaixaDesloc", 0.1f); m.SetFloat("_RimForca", 0.1f); break;
                case "CT_Tapete_Azul": Tex("T_CT_Tapete", 0.5f); m.SetFloat("_RimForca", 0.05f); break;
                case "CT_Metal": Brilho(0.05f, 0.5f); break;
                case "CT_Vermelho_NASA": case "CT_Amarelo": Brilho(0.04f, 0.35f); break;
                case "CT_Console": case "CT_Azul_Escuro": Brilho(0.03f, 0.3f); break;
                case "CT_Tela": Tela("T_Tela_Toon", 1.6f); break;
                case "CT_Tela_ECG": Tela("T_CT_Tela_ECG", 1.6f); break;
                case "CT_Tela_VR": Tela("T_CT_Tela_VR", 1.3f); break;
                case "CT_Luz": Luz(3.5f); break;
                case "CT_Luz_Verde": case "CT_Luz_Vermelha": Luz(4f); break;
                case "CT_Luz_Ambar": Luz(4f); break;
                case "CT_Texto_Branco": m.SetFloat("_RimForca", 0f); m.SetColor("_EmissionColor", Hdr(cor, 0.25f)); break;
                case "CT_Vidro":
                    m.SetColor("_BaseColor", new Color(cor.r, cor.g, cor.b, 0.22f));
                    Brilho(0.08f, 1.2f); m.SetFloat("_RimForca", 0.5f); Transparente(m); break;
                case "CT_Agua":
                    m.SetColor("_BaseColor", new Color(cor.r, cor.g, cor.b, 0.62f));
                    Tex("T_CT_Agua", 0.25f); m.SetColor("_EmissionColor", Hdr(cor, 0.3f)); Brilho(0.1f, 1f); Transparente(m); break;
                case "CT_Logo_NASA":
                    m.SetTexture("_BaseMap", T("T_Logo_NASA")); m.SetColor("_BaseColor", Color.white);
                    m.SetColor("_EmissionColor", Hdr(Color.white, 0.15f)); m.SetFloat("_EmissaoUsaTextura", 1f);
                    m.SetFloat("_RimForca", 0f); Transparente(m); m.SetFloat("_Cull", (float)CullMode.Back); break;
                case "NPC_Pele": m.SetFloat("_RimForca", 0.35f); break;
                case "CT_Painel_Liso": Brilho(0.03f, 0.15f); break;
                case "CT_Madeira_Clara": Tex("T_CT_Madeira", 0.5f); Brilho(0.03f, 0.2f); break;
                case "CT_Sofa": Tex("T_Tecido_Toon", 1f); m.SetFloat("_RimForca", 0.3f); break;
                case "CT_Foto_Missao": Tela("T_CT_Fotos", 0.35f); break;
                case "OR_Concreto": case "OR_Concreto_Escuro": Tex("T_CT_Piso", 0.0625f); m.SetFloat("_RimForca", 0.06f); break;
                case "OR_Branco": case "OR_SRB": Brilho(0.03f, 0.3f); break;
                case "OR_Laranja_ET": Brilho(0.03f, 0.25f); break;
                case "OR_Grade": Tex("T_CT_Azulejo", 2f); break;
                case "OR_Tela_MFD": Tela("T_OR_MFD", 1.5f); break;
                case "OR_Vidro_Cabine":
                    m.SetColor("_BaseColor", new Color(cor.r, cor.g, cor.b, 0.5f));
                    Brilho(0.1f, 1.4f); m.SetFloat("_RimForca", 0.4f); Transparente(m); break;
                case "NPC_Olho": Brilho(0.12f, 1.4f); break;
            }
            EditorUtility.SetDirty(m);
        }

        static void Opaco(Material m)
        {
            m.SetFloat("_SrcBlend", (float)BlendMode.One); m.SetFloat("_DstBlend", (float)BlendMode.Zero);
            m.SetFloat("_ZWrite", 1); m.SetFloat("_Cull", (float)CullMode.Back);
            m.SetOverrideTag("RenderType", "Opaque");
            m.renderQueue = (int)RenderQueue.Geometry;
        }

        static void Transparente(Material m)
        {
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0); m.SetFloat("_Cull", (float)CullMode.Off);
            m.SetOverrideTag("RenderType", "Transparent");
            m.renderQueue = (int)RenderQueue.Transparent;
        }

        public static bool EhTransparente(Material m) => m && m.renderQueue >= (int)RenderQueue.AlphaTest + 50;

        // Cria/atualiza os materiais de um FBX e liga o remap do importer (rodar de novo após reexportar).
        public static void MateriaisDoModelo(string fbx)
        {
            var importer = (ModelImporter)AssetImporter.GetAtPath(fbx);
            if (importer == null) { Debug.LogError($"[Centro] FBX não encontrado: {fbx}"); return; }
            var nomes = AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<Material>().Select(x => x.name)
                .Concat(importer.GetExternalObjectMap().Keys.Where(k => k.type == typeof(Material)).Select(k => k.name))
                .Distinct().ToList();
            foreach (var n in nomes)
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), n), Material(n));
            importer.SaveAndReimport();
            Debug.Log($"[Centro] {nomes.Count} materiais ligados em {fbx}");
        }

        // ------------------------------------------------------------------ céu e pós
        public static Material Ceu()
        {
            string tex = $"{TexDir}/Ceu_Dia.png";
            if (!File.Exists(tex)) GerarCeu(tex);
            string path = $"{MatDir}/Ceu_Dia.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Skybox/Panoramic")); AssetDatabase.CreateAsset(m, path); }
            m.shader = Shader.Find("Skybox/Panoramic");
            m.SetTexture("_MainTex", AssetDatabase.LoadAssetAtPath<Texture2D>(tex));
            m.SetColor("_Tint", new Color(0.5f, 0.5f, 0.5f));
            m.SetFloat("_Exposure", 1.0f);
            m.SetFloat("_Rotation", 30f);
            EditorUtility.SetDirty(m);
            AssetDatabase.SaveAssetIfDirty(m);
            return m;
        }

        public static VolumeProfile Pos()
        {
            string path = $"{Raiz}/PosProcessamento_Centro.asset";
            var p = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            if (p == null) { p = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(p, path); }
            T Get<T>() where T : VolumeComponent { if (!p.TryGet<T>(out var c)) { c = p.Add<T>(true); AssetDatabase.AddObjectToAsset(c, p); } c.active = true; return c; }
            Get<Tonemapping>().mode.Override(TonemappingMode.Neutral);
            var b = Get<Bloom>(); b.threshold.Override(1.05f); b.intensity.Override(0.45f); b.scatter.Override(0.6f);
            var ca = Get<ColorAdjustments>(); ca.contrast.Override(8f); ca.saturation.Override(12f); ca.postExposure.Override(0.05f);
            var v = Get<Vignette>(); v.intensity.Override(0.16f); v.color.Override(Hx("#1C1A3D")); v.smoothness.Override(0.45f);
            EditorUtility.SetDirty(p);
            AssetDatabase.SaveAssetIfDirty(p);
            return p;
        }

        // ------------------------------------------------------------------ texturas
        sealed class Pincel
        {
            public readonly int W, H;
            public readonly Color[] px;
            public Pincel(int w, int h, Color fundo) { W = w; H = h; px = Enumerable.Repeat(fundo, w * h).ToArray(); }
            void Mix(int x, int y, Color c, float a)
            {
                if (a <= 0) return;
                x = ((x % W) + W) % W; y = ((y % H) + H) % H;
                int i = y * W + x; px[i] = Color.Lerp(px[i], c, Mathf.Clamp01(a) * c.a);
            }
            public void Forma(float x0, float y0, float x1, float y1, Func<float, float, float> sdf, Color c)
            {
                for (int y = Mathf.FloorToInt(y0) - 2; y <= Mathf.CeilToInt(y1) + 2; y++)
                for (int x = Mathf.FloorToInt(x0) - 2; x <= Mathf.CeilToInt(x1) + 2; x++)
                    Mix(x, y, c, Mathf.Clamp01(0.5f - sdf(x + 0.5f, y + 0.5f)));
            }
            public void Ret(float x0, float y0, float x1, float y1, Color c, float r = 0)
            {
                float cx = (x0 + x1) / 2, cy = (y0 + y1) / 2, hx = (x1 - x0) / 2 - r, hy = (y1 - y0) / 2 - r;
                Forma(x0, y0, x1, y1, (x, y) => { float qx = Mathf.Abs(x - cx) - hx, qy = Mathf.Abs(y - cy) - hy; return new Vector2(Mathf.Max(qx, 0), Mathf.Max(qy, 0)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0) - r; }, c);
            }
            public void Circ(float cx, float cy, float r, Color c) => Forma(cx - r, cy - r, cx + r, cy + r, (x, y) => new Vector2(x - cx, y - cy).magnitude - r, c);
            public void Elipse(float cx, float cy, float rx, float ry, Color c) =>
                Forma(cx - rx, cy - ry, cx + rx, cy + ry, (x, y) => (new Vector2((x - cx) / rx, (y - cy) / ry).magnitude - 1f) * Mathf.Min(rx, ry), c);
            public void Linha(float x0, float y0, float x1, float y1, float w, Color c)
            {
                var p0 = new Vector2(x0, y0); var d = new Vector2(x1 - x0, y1 - y0); float l2 = Mathf.Max(d.sqrMagnitude, 1e-4f);
                Forma(Mathf.Min(x0, x1) - w, Mathf.Min(y0, y1) - w, Mathf.Max(x0, x1) + w, Mathf.Max(y0, y1) + w,
                    (x, y) => { var p = new Vector2(x, y) - p0; float t = Mathf.Clamp01(Vector2.Dot(p, d) / l2); return (p - d * t).magnitude - w / 2; }, c);
            }
            public void Salvar(string path, bool mip = true)
            {
                var t = new Texture2D(W, H, TextureFormat.RGBA32, mip);
                t.SetPixels(px); File.WriteAllBytes(path, t.EncodeToPNG());
                UnityEngine.Object.DestroyImmediate(t);
                AssetDatabase.ImportAsset(path);
            }
        }

        static Color G(float v) => new Color(v, v, v, 1);

        public static void GerarTexturas()
        {
            Directory.CreateDirectory(TexDir);
            const int S = 512;
            var rnd = new Random(21);

            // piso do hangar: placas de concreto 4x4 com juntas e manchas suaves (texturas multiplicam a cor do material)
            var p = new Pincel(S, S, G(1f));
            for (int k = 0; k < 60; k++) p.Circ(rnd.Next(S), rnd.Next(S), 6 + rnd.Next(26), G(0.95f + (float)rnd.NextDouble() * 0.04f));
            for (int k = 0; k <= 4; k++) { p.Ret(k * 128 - 2, -2, k * 128 + 2, S + 2, G(0.72f)); p.Ret(-2, k * 128 - 2, S + 2, k * 128 + 2, G(0.72f)); }
            for (int k = 0; k < 300; k++) p.Circ(rnd.Next(S), rnd.Next(S), 1.2f, G(0.88f));
            p.Salvar($"{TexDir}/T_CT_Piso.png");

            // parede: chapa ondulada vertical (frisos claros/escuros)
            p = new Pincel(S, S, G(1f));
            for (int k = 0; k < 16; k++) { float x = k * 32; p.Ret(x + 2, -2, x + 7, S + 2, G(0.86f)); p.Ret(x + 7, -2, x + 10, S + 2, G(0.93f)); }
            p.Ret(-2, 250, S + 2, 254, G(0.82f));
            p.Salvar($"{TexDir}/T_CT_Parede.png");

            // grama: tufos em 2 tons
            p = new Pincel(S, S, G(0.93f));
            for (int k = 0; k < 40; k++) p.Circ(rnd.Next(S), rnd.Next(S), 20 + rnd.Next(50), G(0.88f));
            for (int k = 0; k < 500; k++)
            {
                float x = rnd.Next(S), y = rnd.Next(S);
                p.Linha(x, y, x + rnd.Next(-3, 4), y + 6 + rnd.Next(6), 2f, rnd.Next(2) == 0 ? G(0.8f) : G(1f));
            }
            p.Salvar($"{TexDir}/T_CT_Grama.png");

            // asfalto: pontilhado
            p = new Pincel(S, S, G(0.9f));
            for (int k = 0; k < 1800; k++) p.Circ(rnd.Next(S), rnd.Next(S), 1f + (float)rnd.NextDouble(), G(rnd.Next(2) == 0 ? 0.74f : 1f));
            p.Salvar($"{TexDir}/T_CT_Asfalto.png");

            // calçada: blocos intertravados (meia-junta)
            p = new Pincel(S, S, G(1f));
            for (int j = 0; j < 8; j++)
            {
                float y0 = j * 64, off = (j % 2) * 64;
                p.Ret(-2, y0 - 2, S + 2, y0 + 2, G(0.78f));
                for (int i = 0; i <= 4; i++) { float x = i * 128 + off; p.Ret(x - 2, y0, x + 2, y0 + 64, G(0.78f)); }
                for (int i = 0; i < 4; i++) if ((i + j) % 3 == 0) p.Ret(i * 128 + off + 4, y0 + 4, i * 128 + off + 124, y0 + 60, G(0.95f));
            }
            p.Salvar($"{TexDir}/T_CT_Calcada.png");

            // azulejo da piscina
            p = new Pincel(S, S, G(1f));
            for (int k = 0; k <= 8; k++) { p.Ret(k * 64 - 2, -2, k * 64 + 2, S + 2, G(0.75f)); p.Ret(-2, k * 64 - 2, S + 2, k * 64 + 2, G(0.75f)); }
            p.Salvar($"{TexDir}/T_CT_Azulejo.png");

            // água: reflexos de cáusticas
            p = new Pincel(S, S, G(0.82f));
            for (int k = 0; k < 70; k++)
            {
                float x = rnd.Next(S), y = rnd.Next(S), a = (float)rnd.NextDouble() * Mathf.PI;
                p.Linha(x, y, x + Mathf.Cos(a) * 40, y + Mathf.Sin(a) * 40, 5, G(1f));
            }
            p.Salvar($"{TexDir}/T_CT_Agua.png");

            // tapete: trama diagonal sutil
            p = new Pincel(S, S, G(1f));
            for (int k = -8; k < 16; k++) p.Linha(k * 64, 0, k * 64 + S, S, 6, G(0.94f));
            p.Salvar($"{TexDir}/T_CT_Tapete.png");

            // tela ECG
            var fundo = Hx("#08261C"); var verde = Hx("#59FF86");
            p = new Pincel(S, 256, fundo);
            for (int k = 0; k < 16; k++) { p.Ret(k * 32, -2, k * 32 + 1, 258, Color.Lerp(fundo, verde, 0.2f)); }
            for (int k = 0; k < 8; k++) { p.Ret(-2, k * 32, S + 2, k * 32 + 1, Color.Lerp(fundo, verde, 0.2f)); }
            float yb = 128, ya = yb;
            for (int x = 0; x <= S; x += 4)
            {
                float f = (x % 128) / 128f;
                float y = yb;
                if (f > 0.40f && f < 0.44f) y = yb + 12;
                else if (f > 0.48f && f < 0.5f) y = yb - 14;
                else if (f > 0.5f && f < 0.53f) y = yb + 80;
                else if (f > 0.53f && f < 0.56f) y = yb - 30;
                else if (f > 0.66f && f < 0.74f) y = yb + 16;
                p.Linha(x - 4, ya, x, y, 4, verde); ya = y;
            }
            p.Salvar($"{TexDir}/T_CT_Tela_ECG.png");

            // telão VR: a Terra vista da órbita (arco azul com nuvens) + silhueta da estação
            int W = 1024, H = 256;
            var esp = Hx("#0B1030");
            p = new Pincel(W, H, esp);
            for (int k = 0; k < 220; k++) p.Circ(rnd.Next(W), 90 + rnd.Next(H - 90), rnd.Next(5) == 0 ? 1.4f : 0.7f, Color.white);
            p.Elipse(W / 2f, -1500, 1900, 1610, Hx("#7FD4FF"));
            p.Elipse(W / 2f, -1500, 1880, 1590, Hx("#2F78E8"));
            for (int k = 0; k < 40; k++) p.Elipse(rnd.Next(W), rnd.Next(70), 20 + rnd.Next(40), 5 + rnd.Next(8), new Color(1, 1, 1, 0.85f));
            p.Elipse(310, 40, 120, 35, Hx("#4CB35A"));
            float ix = 700, iy = 175;
            p.Ret(ix - 120, iy - 3, ix + 120, iy + 3, Hx("#C9D3E0"));
            for (int k = -3; k <= 3; k++) if (k != 0) p.Ret(ix + k * 34 - 12, iy - 40, ix + k * 34 + 12, iy + 40, Hx("#D8A23A"));
            p.Ret(ix - 30, iy - 9, ix + 30, iy + 9, Hx("#EEF2F7"), 6);
            p.Salvar($"{TexDir}/T_CT_Tela_VR.png");
            // madeira clara: veios suaves
            p = new Pincel(S, S, G(0.95f));
            for (int k = 0; k < 40; k++) { float y = rnd.Next(S); p.Linha(-10, y, S + 10, y + rnd.Next(-12, 12), 2 + rnd.Next(3), G(0.86f + (float)rnd.NextDouble() * 0.06f)); }
            p.Salvar($"{TexDir}/T_CT_Madeira.png");

            // fotos de missões (3x2 quadros ilustrados): lançamento, Terra vista da Lua, astronauta, ônibus, estação, rover
            W = 768; H = 512;
            p = new Pincel(W, H, Hx("#0B1030"));
            float fw = W / 3f, fh = H / 2f;
            for (int i = 0; i < 3; i++) for (int j = 0; j < 2; j++)
            {
                float x0 = i * fw + 6, y0 = j * fh + 6, x1 = x0 + fw - 12, y1 = y0 + fh - 12, cx = (x0 + x1) / 2, cy = (y0 + y1) / 2;
                int q = j * 3 + i;
                var ceu = q == 0 ? Hx("#6FA8DC") : Hx("#0B1030");
                p.Ret(x0, y0, x1, y1, ceu);
                for (int k = 0; k < 18 && q != 0; k++) p.Circ(x0 + rnd.Next((int)(x1 - x0)), y0 + rnd.Next((int)(y1 - y0)), 1.2f, Color.white);
                switch (q)
                {
                    case 0: p.Ret(cx - 10, y0 + 40, cx + 10, y0 + 180, Color.white, 6); p.Ret(cx - 10, y0 + 20, cx + 10, y0 + 45, Hx("#FFB45C"), 6); p.Circ(cx, y0 + 20, 26, Hx("#FFE3A0")); p.Ret(x0, y0, x1, y0 + 18, Hx("#5A7A4A")); break;
                    case 1: p.Circ(cx + 30, cy + 40, 46, Hx("#2F78E8")); p.Circ(cx + 18, cy + 50, 16, Hx("#4CB35A")); p.Ret(x0, y0, x1, y0 + 60, Hx("#A9A5C0")); break;
                    case 2: p.Circ(cx, cy + 20, 34, Color.white); p.Circ(cx, cy + 22, 22, Hx("#FFB45C")); p.Ret(cx - 30, cy - 70, cx + 30, cy - 4, Color.white, 10); break;
                    case 3: p.Ret(cx - 70, cy - 12, cx + 60, cy + 12, Color.white, 10); p.Ret(cx - 20, cy - 50, cx + 30, cy + 50, Color.white, 4); p.Circ(cx + 60, cy, 12, Hx("#2A2C38")); break;
                    case 4: p.Ret(cx - 90, cy - 4, cx + 90, cy + 4, Hx("#C9D3E0")); for (int k = -2; k <= 2; k++) if (k != 0) p.Ret(cx + k * 34 - 12, cy - 40, cx + k * 34 + 12, cy + 40, Hx("#D8A23A")); p.Ret(cx - 26, cy - 10, cx + 26, cy + 10, Color.white, 6); break;
                    case 5: p.Ret(x0, y0, x1, y0 + 70, Hx("#B9785A")); p.Ret(cx - 50, y0 + 70, cx + 40, y0 + 100, Color.white, 6); p.Circ(cx - 35, y0 + 68, 14, Hx("#2A2C38")); p.Circ(cx + 25, y0 + 68, 14, Hx("#2A2C38")); break;
                }
                p.Ret(x0, y0, x1, y0 + 4, Color.white); p.Ret(x0, y1 - 4, x1, y1, Color.white);
            }
            p.Salvar($"{TexDir}/T_CT_Fotos.png");

            // telas MEDS da cabine: 3x2 mostradores (ADI, bussola HSI, dados, barras, esquema, trajetoria)
            W = 1024; H = 512;
            var fundoM = Hx("#06121C"); var ci = Hx("#47E6FF"); var vd = Hx("#59FF86"); var am = Hx("#FFC23A"); var mg = Hx("#FF6BD6");
            p = new Pincel(W, H, fundoM);
            float cw = W / 3f, chh = H / 2f;
            for (int i = 0; i < 3; i++) for (int j = 0; j < 2; j++)
            {
                float x0 = i * cw, y0 = j * chh;
                p.Ret(x0 + 4, y0 + 4, x0 + cw - 4, y0 + 8, Color.Lerp(fundoM, ci, 0.5f));
                p.Ret(x0 + 4, y0 + chh - 8, x0 + cw - 4, y0 + chh - 4, Color.Lerp(fundoM, ci, 0.5f));
                for (int b = 0; b < 5; b++) p.Ret(x0 + 18 + b * 64, y0 + 14, x0 + 58 + b * 64, y0 + 26, Color.Lerp(fundoM, ci, 0.35f), 3);
            }
            { // ADI (topo esquerda)
                float cx = cw * 0.5f, cy = chh * 1.5f + 6, r = 92;
                p.Circ(cx, cy, r + 6, Hx("#2B3550"));
                p.Forma(cx - r, cy - r, cx + r, cy + r, (x, y) => Mathf.Max(new Vector2(x - cx, y - cy).magnitude - r, (y - cy) - 8), Hx("#7A4B2A"));
                p.Forma(cx - r, cy - r, cx + r, cy + r, (x, y) => Mathf.Max(new Vector2(x - cx, y - cy).magnitude - r, -(y - cy) + 8), Hx("#3A7BD5"));
                p.Linha(cx - r, cy + 8, cx + r, cy + 8, 4, Color.white);
                for (int k = -3; k <= 3; k++) if (k != 0) p.Linha(cx - 22, cy + 8 + k * 22, cx + 22, cy + 8 + k * 22, 2, Color.white);
                p.Linha(cx - 50, cy, cx - 14, cy, 5, am); p.Linha(cx + 14, cy, cx + 50, cy, 5, am); p.Circ(cx, cy, 5, am);
            }
            { // HSI (topo meio)
                float cx = cw * 1.5f, cy = chh * 1.5f + 6, r = 88;
                p.Forma(cx - r - 3, cy - r - 3, cx + r + 3, cy + r + 3, (x, y) => Mathf.Abs(new Vector2(x - cx, y - cy).magnitude - r) - 2.5f, ci);
                for (int k = 0; k < 36; k++)
                {
                    float a = k * 10 * Mathf.Deg2Rad, l = k % 3 == 0 ? 16 : 8;
                    p.Linha(cx + Mathf.Cos(a) * r, cy + Mathf.Sin(a) * r, cx + Mathf.Cos(a) * (r - l), cy + Mathf.Sin(a) * (r - l), 2, ci);
                }
                p.Linha(cx, cy - 60, cx, cy + 60, 5, mg); p.Linha(cx - 14, cy + 44, cx, cy + 62, 4, mg); p.Linha(cx + 14, cy + 44, cx, cy + 62, 4, mg);
                p.Linha(cx - 30, cy - 10, cx + 30, cy - 10, 4, Color.white); p.Linha(cx, cy - 20, cx, cy + 20, 4, Color.white);
            }
            { // dados (topo direita): linhas de "texto"
                float x0 = cw * 2 + 24, y = H - 50;
                for (int k = 0; k < 10; k++, y -= 19)
                {
                    p.Ret(x0, y, x0 + 70 + (k * 37 % 60), y + 9, k % 4 == 0 ? am : ci, 2);
                    p.Ret(x0 + 160, y, x0 + 200 + (k * 23 % 70), y + 9, vd, 2);
                }
            }
            { // barras (baixo esquerda)
                for (int k = 0; k < 6; k++)
                {
                    float x = 30 + k * 52, h2 = 40 + (k * 53 % 140);
                    p.Ret(x, 40, x + 30, 40 + 170, Hx("#14263A"), 3);
                    p.Ret(x, 40, x + 30, 40 + h2, k == 4 ? am : vd, 3);
                }
            }
            { // esquema de sistemas (baixo meio)
                float x0 = cw + 40, y0 = 60;
                for (int k = 0; k < 3; k++) { p.Ret(x0 + k * 95, y0 + 100, x0 + k * 95 + 60, y0 + 150, Color.Lerp(fundoM, ci, 0.3f), 6); p.Circ(x0 + k * 95 + 30, y0 + 40, 18, k == 1 ? am : vd); }
                p.Linha(x0 + 30, y0 + 58, x0 + 30, y0 + 100, 4, ci); p.Linha(x0 + 125, y0 + 58, x0 + 125, y0 + 100, 4, ci); p.Linha(x0 + 220, y0 + 58, x0 + 220, y0 + 100, 4, ci);
                p.Linha(x0 + 60, y0 + 125, x0 + 95, y0 + 125, 4, ci); p.Linha(x0 + 155, y0 + 125, x0 + 190, y0 + 125, 4, ci);
            }
            { // grafico de trajetoria (baixo direita)
                float x0 = cw * 2 + 24, y0 = 40;
                p.Linha(x0, y0, x0, y0 + 180, 2, ci); p.Linha(x0, y0, x0 + 290, y0, 2, ci);
                float yt = y0; for (int x = 0; x <= 280; x += 10) { float y = y0 + 170 * (1 - Mathf.Exp(-x / 90f)); p.Linha(x0 + x - 10, yt, x0 + x, y, 3, am); yt = y; }
                p.Circ(x0 + 120, y0 + 170 * (1 - Mathf.Exp(-120 / 90f)), 7, mg);
            }
            p.Salvar($"{TexDir}/T_OR_MFD.png");
            AssetDatabase.Refresh();
        }

        static void GerarCeu(string path)
        {
            int w = 2048, h = 1024;
            var topo = Hx("#3F7FD6"); var hor = Hx("#CFE6F7"); var chao = Hx("#B9C8B0");
            var p = new Pincel(w, h, hor);
            for (int y = 0; y < h; y++)
            {
                float v = y / (float)(h - 1);
                Color c = v < 0.5f ? Color.Lerp(chao, hor, Mathf.SmoothStep(0, 1, v / 0.5f))
                                   : Color.Lerp(hor, topo, Mathf.SmoothStep(0, 1, Mathf.Pow((v - 0.5f) / 0.5f, 0.7f)));
                for (int x = 0; x < w; x++) p.px[y * w + x] = c;
            }
            var rnd = new Random(5);
            for (int n = 0; n < 26; n++)                      // nuvens de desenho: bolhas brancas com base azulada
            {
                float cx = rnd.Next(w), cy = h * 0.53f + rnd.Next(170), s = 0.6f + (float)rnd.NextDouble() * 0.9f;
                var sombra = Hx("#DCE8F7");
                for (int b = 0; b < 6; b++)
                {
                    float bx = cx + (b - 2.5f) * 34 * s + rnd.Next(-8, 9), by = cy + rnd.Next(0, 16) * s, r = (26 + rnd.Next(18)) * s;
                    p.Circ(bx, by - 4, r, sombra);
                    p.Circ(bx, by + 2, r * 0.92f, Color.white);
                }
                p.Ret(cx - 100 * s, cy - 18 * s, cx + 100 * s, cy - 2 * s, sombra, 8 * s);
            }
            p.Salvar(path, false);
        }
    }
}
