using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using AstroLab.Gameplay;
using AstroLab.Nicholas;
using AstroLab.Nicholas.Core;
using AstroLab.Asteroids;
void Check(bool value,string text){if(!value)throw new Exception(text);}
var world=LunarWorld.Instance;var mission=MissionController.Instance;var p=mission.PlayerTransform;
var motor=p.GetComponent<JogadorFPS>();var radial=p.GetComponent<RadialCharacterMotor>();var cc=p.GetComponent<CharacterController>();
var terminal=UnityEngine.Object.FindAnyObjectByType<AsteroidTerminal>();var dashboard=UnityEngine.Object.FindAnyObjectByType<BaseResourceDashboard>();
var safety=UnityEngine.Object.FindAnyObjectByType<BaseInteriorSafety>();
terminal.Close();terminal.enabled=false;dashboard.Close();Time.timeScale=1;
void Pose(Vector3 position,Vector3 forward,Vector3 up)
{motor.enabled=false;cc.enabled=false;p.SetPositionAndRotation(position,Quaternion.LookRotation(forward,up));motor.enabled=true;Physics.SyncTransforms();}
var errors=new System.Collections.Generic.List<string>();
Application.LogCallback onLog=(text,stack,type)=>{if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(text);};Application.logMessageReceived+=onLog;
try
{
    Check(world&&radial,"Terreno ou motor radial ausente");
    Check(world.GetComponentsInChildren<MeshCollider>().Count(c=>c.name.StartsWith("Terreno_"))==6,"Globo não tem seis setores de colisão");
    var roof=GameObject.Find("BaseLunar").GetComponentsInChildren<Renderer>(true).Single(r=>r.name=="Abrigo_Cobertura_Regolito");
    var greenhouse=UnityEngine.Object.FindAnyObjectByType<BaseConsertada>().ObjetoConsertado("Estufa_Vidros").GetComponent<Renderer>();
    Check(roof.bounds.max.z<greenhouse.bounds.min.z-.2f,"Regolito ainda invade a estufa");
    Pose(new Vector3(6,1.95f,4),Vector3.right,Vector3.up);await Task.Delay(300);
    radial.Move(Vector3.right*7);Check(p.position.x<10,"Personagem atravessa parede íntegra do hub: "+p.position);
    foreach(var n in new[]{Vector3.right,Vector3.down,Vector3.forward})
    {
        var tangent=Vector3.ProjectOnPlane(Vector3.forward,n);if(tangent.sqrMagnitude<.1f)tangent=Vector3.right;
        Pose(world.Point(n,1.2f),tangent,n);await Task.Delay(900);
        Check(radial.IsGrounded,"Contato com o solo falhou no hemisfério "+n);
        Check(Vector3.Dot(p.up,world.UpAt(p.position))>.99f,"Cápsula não acompanha a gravidade");
    }
    Pose(world.Point(Vector3.right,1.1f),Vector3.forward,Vector3.right);await Task.Delay(500);
    float start=(p.position-world.Center).magnitude;typeof(JogadorFPS).GetMethod("ExecutarPulo",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(motor,null);
    await Task.Delay(350);Check((p.position-world.Center).magnitude>start+.35f,"Pulo: start="+start+" now="+(p.position-world.Center).magnitude+" scale="+Time.timeScale+" fps="+motor.enabled+" kb="+Keyboard.current.name+" k="+Keyboard.current.spaceKey.isPressed+" v="+typeof(JogadorFPS).GetField("velY",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(motor)+" ground="+radial.IsGrounded+" ast="+terminal.IsOpen);
    mission.NewGame();Pose(new Vector3(-17,1.95f,0),Vector3.forward,Vector3.up);safety.Synchronize();
    mission.Engine.StartEva();mission.Repair(EquipmentId.SolarPanels);mission.Engine.FinishEva();
    mission.Repair(EquipmentId.LifeSupport);mission.Repair(EquipmentId.Greenhouse);
    mission.Power(EquipmentId.LifeSupport,true);mission.Power(EquipmentId.Greenhouse,true);mission.AdvanceTurn();
    int time=mission.Engine.State.RemainingActionTime,power=mission.Engine.PowerUsed;
    mission.Recharge();
    Check(mission.Engine.State.SuitOxygen==100&&mission.Engine.State.SuitBattery==100,"Ação de recarga não abastece o traje");
    Check(mission.Engine.State.RemainingActionTime==time-1,"Recarga não custa exatamente uma ação");
    Check(mission.Engine.PowerUsed==power&&mission.Engine.IsPowered(EquipmentId.Greenhouse),"Recarga desligou a produção");
    mission.Recharge();
    Check(mission.Engine.State.RemainingActionTime==time-1,"Traje cheio cobrou outra ação");
    Check(BaseKnowledgeCatalog.Find(EquipmentId.SuitCharger).repair.Contains("[C]"),"Catálogo remoto voltou a mostrar instrução antiga");
    terminal.enabled=true;
    Check(terminal&&terminal.Client.Records.Count>=10&&terminal.Station,"Observatório ou acervo ausente");
    for(int i=0;i<60&&terminal.AlertCount==0;i++)await Task.Delay(100);
    Check(terminal.AlertCount>0&&terminal.Station.SkyTarget.gameObject.activeInHierarchy,"Nenhum alerta ou asteroide visível");
    terminal.Open();await Task.Delay(350);Check(terminal.IsOpen&&Time.timeScale==0,"Observatório não abre/pausa");
    Check(terminal.Station.Preview&&terminal.Station.Preview.IsCreated(),"Câmera sem imagem");
    var previous=RenderTexture.active;RenderTexture.active=terminal.Station.Preview;
    var texture=new Texture2D(terminal.Station.Preview.width,terminal.Station.Preview.height,TextureFormat.RGB24,false);
    texture.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0);texture.Apply();
    Color centre=texture.GetPixel(texture.width/2,texture.height/2),corner=texture.GetPixel(0,0);
    RenderTexture.active=previous;UnityEngine.Object.Destroy(texture);
    Check(Vector3.Distance(new Vector3(centre.r,centre.g,centre.b),new Vector3(corner.r,corner.g,corner.b))>.06f,"Preview é uma cor uniforme");
    terminal.Close();Check(Time.timeScale>0,"Fechar observatório não retomou missão");
    Check(errors.Count==0,string.Join(";",errors));
    return "PASS: seis setores físicos; paredes sólidas; abrigo separado da estufa; chão e pulo em hemisférios; ação de recarga abastece uma vez com produção ligada; instruções atualizadas; alertas e câmera reais; pausa preservada. NASA: "+terminal.Client.Records.Count+" alvos.";
}
finally
{
    if(terminal&&terminal.IsOpen)terminal.Close();
    terminal.enabled=true;
    Application.logMessageReceived-=onLog;mission.NewGame();
}





