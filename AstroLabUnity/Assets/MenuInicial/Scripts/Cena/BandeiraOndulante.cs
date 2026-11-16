using UnityEngine;

namespace AstroLab.Menu
{
    /// <summary>
    /// Bandeira lunar no estilo Apollo (malha modelada no Blender: Bandeira_Tecido, com tecido + emblema e dobras
    /// fixas). O tecido fica preso no mastro e na haste horizontal do topo; a parte solta se move só pela
    /// GRAVIDADE DA LUA (1,62 m/s²), sem vento e quase sem amortecimento (não há ar).
    ///
    /// Ciclo: de tempos em tempos (um leve tremor do mastro) a parte solta é LEVANTADA rápido, depois cai devagar,
    /// flutuando, e balança algumas vezes com amplitude decrescente até parar.
    /// Modelo leve: cada coluna do tecido é um pêndulo pendurado na haste (θ'' = -(g/L)·sen θ - c·θ'), acoplado às
    /// vizinhas por uma mola fraca; a coluna do mastro fica presa.
    ///
    /// A malha precisa da 2ª UV ("Param", gerada no menu_lua.py): u = posição ao longo da haste (0 no mastro),
    /// v = altura (0 embaixo, 1 na haste). A deformação é somada à forma original (as dobras do Blender ficam).
    /// </summary>
    [RequireComponent(typeof(MeshFilter))]
    public sealed class BandeiraOndulante : MonoBehaviour
    {
        [Header("Física lunar")]
        [Tooltip("Gravidade da Lua (m/s²).")]
        public float gravidade = 1.62f;
        [Tooltip("Amortecimento (sem ar: quase zero).")]
        [Range(0f, 1f)] public float amortecimento = 0.12f;
        [Tooltip("Mola entre colunas vizinhas (deixa o tecido contínuo).")]
        public float acoplamento = 6f;
        [Tooltip("Comprimento efetivo do pêndulo, em fração da altura da bandeira.")]
        [Range(0.3f, 1.5f)] public float comprimentoRelativo = 0.85f;
        [Range(8, 64)] public int colunas = 25;

        [Header("Levantadas (tremor do mastro)")]
        public Vector2 intervaloImpulso = new Vector2(6f, 10f);
        [Tooltip("Ângulo (rad) que a ponta solta sobe num impulso normal.")]
        public float anguloImpulso = 0.42f;
        [Tooltip("Tempo (s) da subida rápida.")]
        public float tempoSubida = 0.35f;

        Mesh malha;
        Vector3[] baseV, vert;
        Vector2[] param;
        Vector3 origem, eixoHaste, baixo, frente;
        float largura, altura;

        float[] th, vel, comp, alvoSubida, deSubida, inicioSubida;
        float proximoImpulso;

        /// <summary>Ângulo atual da ponta solta (rad) — útil para depuração.</summary>
        public float AnguloPonta => th != null && th.Length > 0 ? th[th.Length - 1] : 0f;

        void Awake()
        {
            var mf = GetComponent<MeshFilter>();
            if (mf.sharedMesh == null || !mf.sharedMesh.isReadable)
            {
                Debug.LogWarning("[Menu] Bandeira: malha ausente ou sem Read/Write; a bandeira fica parada.");
                enabled = false; return;
            }
            malha = Instantiate(mf.sharedMesh);
            malha.name = mf.sharedMesh.name + " (animada)";
            malha.MarkDynamic();
            mf.sharedMesh = malha;
            baseV = malha.vertices;
            vert = new Vector3[baseV.Length];
            var uv2 = new System.Collections.Generic.List<Vector2>();
            malha.GetUVs(1, uv2);
            if (uv2.Count != baseV.Length)
            {
                Debug.LogWarning("[Menu] Bandeira: malha sem a UV 'Param'; a bandeira fica parada.");
                enabled = false; return;
            }
            param = uv2.ToArray();
            DescobrirEixos();

            th = new float[colunas]; vel = new float[colunas]; comp = new float[colunas];
            alvoSubida = new float[colunas]; deSubida = new float[colunas]; inicioSubida = new float[colunas];
            var r = new System.Random(17);
            for (int i = 0; i < colunas; i++) comp[i] = altura * comprimentoRelativo * (0.92f + 0.16f * (float)r.NextDouble());
            proximoImpulso = Time.time + 1.5f;
        }

        /// <summary>Acha a haste, o "para baixo" e a frente do tecido no espaço da malha (pelos cantos da UV Param).</summary>
        void DescobrirEixos()
        {
            int Canto(Vector2 alvo)
            {
                int melhor = 0; float dMin = float.MaxValue;
                for (int i = 0; i < param.Length; i++) { float d = (param[i] - alvo).sqrMagnitude; if (d < dMin) { dMin = d; melhor = i; } }
                return melhor;
            }
            var a = baseV[Canto(new Vector2(0, 1))];
            var b = baseV[Canto(new Vector2(1, 1))];
            var c = baseV[Canto(new Vector2(0, 0))];
            largura = Vector3.Distance(a, b);
            altura = Vector3.Distance(a, c);
            eixoHaste = (b - a) / largura;
            baixo = (c - a) / altura;
            origem = a;
            frente = Vector3.Cross(eixoHaste, baixo).normalized;
            var cam = Camera.main;
            if (cam && Vector3.Dot(transform.TransformDirection(frente), cam.transform.position - transform.TransformPoint(a)) < 0f)
                frente = -frente;
        }

        /// <summary>Levanta a parte solta (forca 1 = impulso normal; o lançamento usa ~2,4).</summary>
        public void Impulso(float forca = 1f)
        {
            if (th == null) return;
            float agora = Time.time;
            for (int i = 1; i < th.Length; i++)
            {
                float s = i / (float)(th.Length - 1);
                // a ponta sobe mais; um pequeno atraso do mastro para a ponta (o tremor se propaga)
                alvoSubida[i] = Mathf.Max(th[i], anguloImpulso * forca * Mathf.Pow(s, 0.9f) * Random.Range(0.92f, 1.08f));
                inicioSubida[i] = agora + s * 0.12f;
                deSubida[i] = float.NaN;
            }
        }

        void Update()
        {
            if (Time.time >= proximoImpulso)
            {
                Impulso(1f);
                proximoImpulso = Time.time + Random.Range(intervaloImpulso.x, intervaloImpulso.y);
            }
            Simular(Mathf.Min(Time.deltaTime, 0.1f));
            Animar();
        }

        void Simular(float dt)
        {
            const float passo = 1f / 120f;
            int n = Mathf.Max(1, Mathf.CeilToInt(dt / passo));
            float h = dt / n;
            float agora = Time.time;
            for (int k = 0; k < n; k++)
            {
                for (int i = 1; i < th.Length; i++)                                   // coluna 0 presa no mastro
                {
                    if (alvoSubida[i] > 0f && agora >= inicioSubida[i])
                    {
                        // subida rápida (ease-out); ao chegar, solta com velocidade zero e a gravidade assume
                        if (float.IsNaN(deSubida[i])) deSubida[i] = th[i];
                        float p = Mathf.Clamp01((agora - inicioSubida[i]) / tempoSubida);
                        th[i] = Mathf.Lerp(deSubida[i], alvoSubida[i], 1f - (1f - p) * (1f - p));
                        vel[i] = 0f;
                        if (p >= 1f) alvoSubida[i] = 0f;
                        continue;
                    }
                    float dir = i + 1 < th.Length ? th[i + 1] : th[i];
                    float viz = th[i - 1] + dir - 2f * th[i];
                    float acc = -(gravidade / comp[i]) * Mathf.Sin(th[i]) - amortecimento * vel[i] + acoplamento * viz;
                    vel[i] += acc * h;
                    th[i] += vel[i] * h;
                }
                th[0] = 0f; vel[0] = 0f;
            }
        }

        float Angulo(float u)
        {
            float f = Mathf.Clamp01(u) * (th.Length - 1);
            int i = Mathf.Min((int)f, th.Length - 2);
            return Mathf.Lerp(th[i], th[i + 1], f - i);
        }

        void Animar()
        {
            for (int i = 0; i < baseV.Length; i++)
            {
                float u = param[i].x, v = param[i].y;
                float d = (1f - v) * altura;                                          // distância abaixo da haste
                // o tecido curva: perto da haste gira pouco, embaixo gira o ângulo todo
                float a = Angulo(u) * (0.45f + 0.55f * (1f - v));
                // deslocamento = (pendurado girado) - (pendurado reto); a forma do Blender (dobras) é preservada
                Vector3 desl = baixo * (d * Mathf.Cos(a) - d) + frente * (d * Mathf.Sin(a));
                vert[i] = baseV[i] + desl;
            }
            malha.vertices = vert;
            malha.RecalculateNormals();
            malha.RecalculateBounds();
        }

        void OnDestroy()
        {
            if (malha != null) Destroy(malha);
        }
    }
}
