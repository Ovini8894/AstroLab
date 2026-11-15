using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using Unity.Pipeline.Scripting;
using AstroLab.Capacete;

public static class CheckScanner
{
    static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
    public static async Task<Dictionary<string,object>> Run()
    {
        var s=UnityEngine.Object.FindAnyObjectByType<ScannerPrimeiraPessoa>();var h=s.visor;
        bool auto=s.lerMouseAutomaticamente,movement=h.controleMovimento.enabled;float timeScale=Time.timeScale;
        var player=h.controleMovimento.transform;var pos=player.position;var rot=player.rotation;var cameraRot=s.cameraMira.transform.localRotation;
        var cc=player.GetComponent<CharacterController>();bool ccEnabled=cc.enabled;
        var a=GameObject.CreatePrimitive(PrimitiveType.Cube);a.name="ArtefatoSemCadastro";
        var b=GameObject.CreatePrimitive(PrimitiveType.Cube);b.name="RochaDeTeste";
        var results=new Dictionary<string,object>();
        try
        {
            s.lerMouseAutomaticamente=false;h.FecharDescoberta();h.controleMovimento.enabled=false;cc.enabled=false;
            player.position=new Vector3(0,40,0);player.rotation=Quaternion.identity;s.cameraMira.transform.localRotation=Quaternion.identity;
            a.transform.position=s.cameraMira.transform.position+Vector3.forward*4;b.transform.position=s.cameraMira.transform.position+Vector3.right*4;
            Physics.SyncTransforms();s.AtualizarVarredura(false,0);
            int scans=s.ScansConcluidos;
            s.AtualizarVarredura(true,2.99f);Check(!h.FichaAberta && s.ScansConcluidos==scans,"Scan terminou antes de 3 segundos");results["naoConcluiEm2_99s"]=true;
            Check(s.audioScanner.isPlaying && s.audioScanner.loop,"Som de varredura não começou");
            s.AtualizarVarredura(true,.01f);Check(h.FichaAberta && s.ScansConcluidos==scans+1,"Scan não terminou em 3 segundos");
            Check(h.descobertaDescricao.text.Contains("não possui uma descrição específica"),"Objeto sem cadastro não recebeu identificação genérica");results["concluiEm3sEAbreDescricao"]=true;
            h.FecharDescoberta();s.AtualizarVarredura(true,5);Check(s.ScansConcluidos==scans+1,"Botão mantido repetiu conclusão");results["umaConclusaoPorPressao"]=true;
            s.AtualizarVarredura(false,0);s.AtualizarVarredura(true,1.5f);Check(s.Progresso>.49f,"Progresso não avançou");s.AtualizarVarredura(false,0);Check(s.Progresso==0,"Soltar não cancelou progresso");results["soltarCancela"]=true;
            s.AtualizarVarredura(true,1.7f);s.cameraMira.transform.LookAt(b.transform.position);s.AtualizarVarredura(true,.1f);Check(s.Progresso<.04f && s.AlvoAtual==b,"Trocar de objeto carregou progresso");results["trocarAlvoReinicia"]=true;
            s.cameraMira.transform.rotation=Quaternion.LookRotation(Vector3.up);s.AtualizarVarredura(true,.2f);Check(s.Progresso==0 && s.AlvoAtual==null,"Perder o alvo não cancelou");results["perderMiraCancela"]=true;
            s.cameraMira.transform.LookAt(b.transform.position);s.AtualizarVarredura(false,0);s.AtualizarVarredura(true,3);Check(h.FichaAberta && h.descobertaTitulo.text=="Rocha lunar","Scan de outro tipo de objeto falhou");results["outroTipoDeObjeto"]=true;
            h.FecharDescoberta();s.AtualizarVarredura(false,0);s.AtualizarVarredura(true,1);Time.timeScale=0;s.AtualizarVarredura(true,2);Check(s.Progresso==0,"Pausa não interrompeu scan");Time.timeScale=timeScale;results["pausaCancela"]=true;
            var rig=GameObject.Find("MaosEScanner").transform;var initial=rig.localPosition;
            await Task.Delay(150);Check(Vector3.Distance(initial,rig.localPosition)>0.00001f,"Mãos estão congeladas");results["respiracaoDasMaos"]=true;
            Check(h.scannerAtivo,"Inspeção instantânea não foi desativada");results["semAtalhoInstantaneo"]=true;
            Debug.Log("[SCANNER_TESTE] PASS: 2.99s incompleto, 3.00s completo, cancelamentos, tipos distintos, áudio e movimento.");
            return results;
        }
        finally
        {
            Time.timeScale=timeScale;h.FecharDescoberta();s.AtualizarVarredura(false,0);
            UnityEngine.Object.Destroy(a);UnityEngine.Object.Destroy(b);
            player.SetPositionAndRotation(pos,rot);s.cameraMira.transform.localRotation=cameraRot;cc.enabled=ccEnabled;h.controleMovimento.enabled=movement;s.lerMouseAutomaticamente=auto;
        }
    }
}
