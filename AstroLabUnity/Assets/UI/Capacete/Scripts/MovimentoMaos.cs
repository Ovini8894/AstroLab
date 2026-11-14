using UnityEngine;

namespace AstroLab.Capacete
{
    [DefaultExecutionOrder(120)]
    public class MovimentoMaos : MonoBehaviour
    {
        public ScannerPrimeiraPessoa scanner;
        public Transform jogador;
        public float amplitudeRespiracao=.0035f;
        public float amplitudePasso=.008f;
        Vector3 posicaoBase,ultimaPosicao;
        Quaternion rotacaoBase;
        float fase,velocidadeSuave,poseScanner;

        void Awake(){posicaoBase=transform.localPosition;rotacaoBase=transform.localRotation;ultimaPosicao=jogador?jogador.position:Vector3.zero;}
        void LateUpdate()
        {
            float dt=Time.deltaTime;if(dt<=0)return;
            float velocidade=jogador?Vector3.ProjectOnPlane(jogador.position-ultimaPosicao,Vector3.up).magnitude/dt:0;
            if(jogador)ultimaPosicao=jogador.position;
            velocidadeSuave=Mathf.Lerp(velocidadeSuave,Mathf.Clamp01(velocidade/5.5f),1-Mathf.Exp(-8*dt));
            fase+=dt*Mathf.Lerp(1.8f,11f,velocidadeSuave);
            poseScanner=Mathf.Lerp(poseScanner,scanner && scanner.Escaneando?1f:0f,1-Mathf.Exp(-9*dt));
            float respirar=Mathf.Sin(Time.time*1.8f)*amplitudeRespiracao;
            transform.localPosition=posicaoBase+new Vector3(Mathf.Sin(fase*.5f)*amplitudePasso*velocidadeSuave,respirar+Mathf.Sin(fase)*amplitudePasso*velocidadeSuave+poseScanner*.028f,poseScanner*.018f);
            transform.localRotation=rotacaoBase*Quaternion.Euler(-poseScanner*3f,Mathf.Sin(fase*.5f)*velocidadeSuave,Mathf.Sin(Time.time*1.2f)*.35f+Mathf.Sin(fase*.5f)*velocidadeSuave*1.4f);
        }
    }
}
