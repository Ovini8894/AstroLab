using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using AstroLab.Nicholas;
using AstroLab.Nicholas.Core;
using AstroLab.Gameplay;

void Check(bool ok,string text){if(!ok)throw new Exception(text);}
var mission=MissionController.Instance;var player=mission.PlayerTransform;var camera=Camera.main;
var model=UnityEngine.Object.FindAnyObjectByType<BaseConsertada>();
var safety=UnityEngine.Object.FindAnyObjectByType<BaseInteriorSafety>();
var interaction=UnityEngine.Object.FindAnyObjectByType<MissionPlayerInteraction>();
var cc=player.GetComponent<CharacterController>();
var oldPosition=player.position;var oldRotation=player.rotation;var oldCamera=camera.transform.localRotation;
var originalMotor=player.GetComponent<JogadorFPS>();bool originalMotorEnabled=originalMotor.enabled;
var testTerminal=UnityEngine.Object.FindAnyObjectByType<AstroLab.Asteroids.AsteroidTerminal>();bool terminalEnabled=testTerminal.enabled;testTerminal.Close();testTerminal.enabled=false;
UnityEngine.Object.FindAnyObjectByType<BaseResourceDashboard>().Close();Time.timeScale=1;
var update=typeof(MissionPlayerInteraction).GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic);
var errors=new System.Collections.Generic.List<string>();
Application.LogCallback onLog=(text,stack,type)=>{if(type==LogType.Error||type==LogType.Exception||type==LogType.Assert)errors.Add(text);};
Application.logMessageReceived+=onLog;
void Move(Vector3 pos){if(cc)cc.enabled=false;player.position=pos;if(cc)cc.enabled=true;Physics.SyncTransforms();}
bool Aim(EquipmentId id)
{
    originalMotor.enabled=false;
    foreach(var target in UnityEngine.Object.FindObjectsByType<MissionEquipmentTarget>().Where(t=>t.isActiveAndEnabled&&t.Equipment==id))
    {
        var renderer=target.GetComponent<Renderer>();if(!renderer)continue;
        var center=renderer.bounds.center;
        foreach(var direction in new[]{Vector3.forward,Vector3.back,Vector3.left,Vector3.right})
        {
            var eye=center+direction*2f;eye.y=Mathf.Max(1.6f,center.y);
            Vector3 candidate=eye-(camera.transform.position-player.position);
            if(safety.Contains(candidate)==MissionRuleSet.Find(id).NeedsEva)continue;
            Move(candidate);camera.transform.LookAt(center);
            update.Invoke(interaction,null);
            if(interaction.AimedEquipment&&interaction.AimedEquipment.Equipment==id)return true;
        }
    }
    return false;
}
try
{
    mission.NewGame();Move(new Vector3(-15,1.98f,0));
    Check(safety&&safety.Contains(player.position),"Suporte de vida não está no volume interno");
    for(float x=-17;x<=19;x+=.1f)Check(safety.Contains(new Vector3(x,1.98f,0)),"Descontinuidade de EVA no corredor: "+x);
    for(float z=0;z<=17;z+=.1f)Check(safety.Contains(new Vector3(0,1.98f,z)),"Descontinuidade no acesso à eclusa: "+z);
    Check(!safety.Contains(new Vector3(0,1.98f,18))&&!safety.Contains(new Vector3(0,1.98f,27))&&!safety.Contains(new Vector3(30,1.98f,0)),"Exterior foi tratado como habitat");
    Check(mission.Engine.StartEva().Success,"Preparação do erro EVA falhou");
    safety.Synchronize();Check(!mission.Engine.State.IsOutside,"EVA permanece ativa dentro da base");
    foreach(var part in PecasDaBase.Todas.Where(MissionPieceBindings.IsArchitecture))
    {
        Check(model.EstaConsertada(part.id)&&model.ObjetoConsertado(part.id).activeInHierarchy,"Estrutura começa quebrada: "+part.id);
        var target=model.ObjetoConsertado(part.id).GetComponent<MissionEquipmentTarget>();
        Check(!target||!target.enabled,"Parede ainda oferece reparo: "+part.id);
    }
    Check(Aim(EquipmentId.LifeSupport),"Não é possível mirar no suporte de vida");
    safety.Synchronize();update.Invoke(interaction,null);
    Check(interaction.CurrentPrompt.Contains("[R]"),"Reparo disponível não oferece R");
    var dashboard=UnityEngine.Object.FindAnyObjectByType<BaseResourceDashboard>();
    dashboard.Open();
    var repairButton=(UnityEngine.UI.Button)typeof(BaseResourceDashboard).GetField("repairButton",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(dashboard);
    Check(repairButton.interactable,"Botão de reparo não funciona com o collider do modelo");
    dashboard.Close();
    // Exercise the shared action invoked by R, including the real raycast and engine validation.
    interaction.RepairAimed();
    Check(mission.Engine.IsRepaired(EquipmentId.LifeSupport),"Ação compartilhada de reparo não restaurou o suporte de vida");
    Check(mission.Engine.State.SpareParts==9&&mission.Engine.State.RemainingActionTime==4,"Custo incorreto no reparo R");
    Check(Aim(EquipmentId.LifeSupport),"Equipamento saudável não pode ser inspecionado");update.Invoke(interaction,null);
    Check(!interaction.CurrentPrompt.Contains("[R]"),"R oferecido em equipamento já consertado");
    Check(!interaction.RepairAimed().Success&&mission.Engine.State.SpareParts==9,"Reparo duplicado cobrou peças");
    Check(Aim(EquipmentId.WaterRecycler),"Reciclador não pode ser alcançado");
    mission.Engine.StartEva();update.Invoke(interaction,null);
    Check(!interaction.CurrentPrompt.Contains("[R]")&&interaction.CurrentPrompt.Contains("dentro"),"Prompt não explica bloqueio de EVA");
    safety.Synchronize();Check(!mission.Engine.State.IsOutside,"Entrada física não liberou o reparo");
    var door=model.ObjetoConsertado("Porta_SuporteVida");
    Move(new Vector3(-9.5f,1.98f,0));await Task.Delay(600);Physics.SyncTransforms();
    Check(door.GetComponent<RestoredSlidingDoor>().IsOpen,"Porta íntegra não abriu");
    var radial=player.GetComponent<RadialCharacterMotor>();
    if(radial)radial.Move(Vector3.left*4.5f);else cc.Move(Vector3.left*4.5f);Check(player.position.x<-12.5f,"Porta bloqueia passagem física");
    safety.Synchronize();Check(!mission.Engine.State.IsOutside,"Travessia interna ativou EVA");
    Move(new Vector3(-6,1.98f,0));await Task.Delay(600);Check(!door.GetComponent<RestoredSlidingDoor>().IsOpen,"Porta não fecha");
    mission.NewGame();Move(mission.OuterAirlock.position+Vector3.up*1.2f);safety.Synchronize();
    Check(mission.Engine.State.IsOutside,"Saída física não inicia EVA");
    Check(mission.Repair(EquipmentId.SolarPanels).Success,"Reparo externo não funciona");
    Move(new Vector3(0,1.98f,0));safety.Synchronize();
    Check(mission.Repair(EquipmentId.LifeSupport).Success,"Reparo interno não funciona após retornar");
    Check(mission.Repair(EquipmentId.Greenhouse).Success,"Estufa não reparou");
    mission.Power(EquipmentId.LifeSupport,true);mission.Power(EquipmentId.Greenhouse,true);mission.AdvanceTurn();
    Check(mission.Repair(EquipmentId.RadiationShelter).Success,"Abrigo não reparou");
    Check(mission.Repair(EquipmentId.WaterRecycler).Success,"Reciclador não reparou");
    Check(model.TotalConsertadas==47&&mission.Engine.State.SpareParts==0,"Modelos ou orçamento inconsistentes");
    mission.AdvanceTurn();Check(mission.AdvanceTurn().Success,"Vitória não funciona após as mudanças");
    var artifacts=UnityEngine.Object.FindObjectsByType<MissionHistoricalTarget>();
    Check(artifacts.Length==5&&artifacts.All(a=>a.transform.Find("ReplicaDetalhada")&&a.GetComponent<Collider>()),"Réplicas ou colliders ausentes");
    Check(artifacts.All(a=>a.GetComponentsInChildren<Renderer>().Length>=7),"Réplicas continuam sem detalhes");
    Check(errors.Count==0,string.Join(";",errors));
    return "PASS: estrutura íntegra, ação de reparo, custo único, bloqueio explicado, EVA física, porta transitável, 47 peças, vitória e 5 réplicas detalhadas. Erros: 0.";
}
finally
{
    testTerminal.enabled=terminalEnabled;originalMotor.enabled=originalMotorEnabled;
    Application.logMessageReceived-=onLog;
    mission.NewGame();Move(oldPosition);player.rotation=oldRotation;camera.transform.localRotation=oldCamera;
}



