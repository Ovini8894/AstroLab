using UnityEngine;

namespace AstroLab.Centro
{
    // Lima (MPFB, Humanoid): liga o NpcInstrutor ao Animator, pisca a cada 3–6 s, mexe a boca enquanto fala
    // e vira a cabeça para o jogador (LookAt depois da animação, com limite de ângulo).
    [RequireComponent(typeof(Animator))]
    public class LimaAnimador : MonoBehaviour
    {
        public NpcInstrutor npc;
        public float velocidadeDoClipe = 1.6f;    // m/s em que o ciclo Walk foi animado (passada 0,8 m, 1 s)
        public float limiteYaw = 60f, limitePitch = 25f;

        static readonly int pAndando = Animator.StringToHash("Andando");
        static readonly int pFalando = Animator.StringToHash("Falando");
        static readonly int pVel = Animator.StringToHash("VelAnda");
        static readonly int pApontar = Animator.StringToHash("Apontar");
        static readonly int pAcenar = Animator.StringToHash("Acenar");

        Animator anim;
        SkinnedMeshRenderer pele;
        int iPiscar = -1, iBoca = -1;
        Transform cabeca;
        Quaternion rotOlhar = Quaternion.identity;
        float proxPiscada, tPiscada = -1f, boca;

        void Awake()
        {
            anim = GetComponent<Animator>();
            if (!npc) npc = GetComponentInParent<NpcInstrutor>();
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (r.sharedMesh == null) continue;
                int a = r.sharedMesh.GetBlendShapeIndex("Piscar"), b = r.sharedMesh.GetBlendShapeIndex("Boca");
                if (a >= 0 || b >= 0) { pele = r; iPiscar = a; iBoca = b; break; }
            }
            if (anim.isHuman) cabeca = anim.GetBoneTransform(HumanBodyBones.Head);
            if (cabeca == null) foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == "head") { cabeca = t; break; }
            proxPiscada = Time.time + Random.Range(1f, 3f);
        }

        public void Apontar() => anim.SetTrigger(pApontar);
        public void Acenar() => anim.SetTrigger(pAcenar);

        void Update()
        {
            float dt = Time.deltaTime;
            if (npc)
            {
                anim.SetBool(pAndando, npc.AndandoAgora);
                anim.SetBool(pFalando, npc.Falando && !npc.AndandoAgora);
                anim.SetFloat(pVel, npc.velocidade / Mathf.Max(0.1f, velocidadeDoClipe));   // passada casa com o deslocamento: sem escorregar
            }
            if (!pele) return;
            // piscar: fecha e abre em 0,16 s, a cada 3–6 s (às vezes duas vezes seguidas)
            if (iPiscar >= 0)
            {
                if (tPiscada < 0f && Time.time >= proxPiscada) tPiscada = 0f;
                float w = 0f;
                if (tPiscada >= 0f)
                {
                    tPiscada += dt;
                    w = Mathf.Clamp01(1f - Mathf.Abs(tPiscada / 0.08f - 1f));
                    if (tPiscada >= 0.16f)
                    {
                        tPiscada = -1f;
                        proxPiscada = Time.time + (Random.value < 0.15f ? 0.25f : Random.Range(3f, 6f));
                    }
                }
                pele.SetBlendShapeWeight(iPiscar, w * 100f);
            }
            // boca: abre e fecha em ritmo de fala enquanto o diálogo está aberto
            if (iBoca >= 0)
            {
                bool fala = npc && npc.Falando;
                float alvo = fala ? Mathf.Clamp01(0.55f + 0.45f * Mathf.Sin(Time.time * 13f) * Mathf.Sin(Time.time * 4.7f + 1f)) : 0f;
                boca = Mathf.MoveTowards(boca, alvo, dt * 9f);
                pele.SetBlendShapeWeight(iBoca, boca * 100f);
            }
        }

        void LateUpdate()
        {
            if (!cabeca || !npc) return;
            var alvo = npc.AlvoOlhar;
            var dir = (alvo - cabeca.position).normalized;
            var local = transform.InverseTransformDirection(dir);
            float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -limiteYaw, limiteYaw);
            float pitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg, -limitePitch, limitePitch);
            var delta = Quaternion.Euler(pitch, yaw, 0f);
            rotOlhar = Quaternion.Slerp(rotOlhar, delta, 1f - Mathf.Exp(-6f * Time.deltaTime));
            // aplica a rotação no espaço do corpo, por cima da pose animada
            var corpo = transform.rotation;
            cabeca.rotation = corpo * rotOlhar * Quaternion.Inverse(corpo) * cabeca.rotation;
        }
    }
}
