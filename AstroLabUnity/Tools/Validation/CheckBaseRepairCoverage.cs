using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using AstroLab.Nicholas;
using AstroLab.Nicholas.Core;
using AstroLab.Gameplay;

void Check(bool ok,string message) { if(!ok)throw new Exception(message); }
var mission=MissionController.Instance;
var baseModel=UnityEngine.Object.FindAnyObjectByType<BaseConsertada>();
var player=mission.PlayerTransform;
var camera=Camera.main;
var interaction=UnityEngine.Object.FindAnyObjectByType<MissionPlayerInteraction>();
var cc=player.GetComponent<CharacterController>();
var oldPosition=player.position; var oldRotation=player.rotation; var oldCamera=camera.transform.localRotation;
var runtimeErrors=new System.Collections.Generic.List<string>();
Application.LogCallback onLog=(message,stack,kind)=> { if(kind==LogType.Error||kind==LogType.Assert||kind==LogType.Exception)runtimeErrors.Add(message); };
Application.logMessageReceived+=onLog;
void Move(Vector3 p) { bool active=cc && cc.enabled; if(cc)cc.enabled=false; player.position=p; if(cc)cc.enabled=active; Physics.SyncTransforms(); }
try
{
    var map=MissionPieceBindings.CreateMap();
    Check(map.Values.Sum(ids=>ids.Length)==PecasDaBase.Todas.Length,"Peças sem manutenção");
    Check(map.Values.SelectMany(ids=>ids).Distinct().Count()==PecasDaBase.Todas.Length,"Peça cobrada em grupos diferentes");
    mission.NewGame();
    var door=baseModel.baseLunar.GetComponentsInChildren<Transform>(true).Single(t=>t.name=="Porta_SuporteVida_TRAVADA");
    Check(door.GetComponent<MissionEquipmentTarget>()?.Equipment==EquipmentId.LifeSupport,"Porta sem alvo de manutenção");
    Move(new Vector3(-9,1.98f,0));
    camera.transform.LookAt(door.GetComponent<Renderer>().bounds.center);
    Physics.SyncTransforms();
    interaction.GetType().GetMethod("Update",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(interaction,null);
    Check(interaction.AimedEquipment && interaction.AimedEquipment.transform==door,"Não é possível mirar na porta pelo corredor");
    Check(mission.Repair(interaction.AimedEquipment.Equipment).Success,"Reparo pela porta falhou");
    Check(mission.Engine.State.SpareParts==9,"Porta cobrou custo adicional ao suporte de vida");
    Check(!door.gameObject.activeSelf,"Porta quebrada continua ativa");
    var repairedDoor=baseModel.ObjetoConsertado("Porta_SuporteVida");
    Check(repairedDoor && repairedDoor.activeInHierarchy,"Porta restaurada ausente");
    var sliding=repairedDoor.GetComponent<RestoredSlidingDoor>();
    Check(sliding,"Abertura automática da porta ausente");
    await Task.Delay(500);
    Check(sliding.IsOpen,"Porta restaurada não abriu ao se aproximar");
    Physics.SyncTransforms();
    cc.Move(Vector3.left*5f);
    Check(player.position.x < -12.5f,"O collider da porta ainda bloqueia o corredor: "+player.position);
    Move(new Vector3(-6,1.98f,0)); await Task.Delay(500);
    Check(!sliding.IsOpen,"Porta não fechou ao afastar");
    mission.NewGame();
    Check(door.gameObject.activeSelf && !repairedDoor.activeSelf,"Reinício não restaurou o estado da porta");

    mission.Engine.StartEva(); Check(mission.Repair(EquipmentId.SolarPanels).Success,"Usina não reparou"); mission.Engine.FinishEva();
    Check(mission.Repair(EquipmentId.LifeSupport).Success,"Suporte de vida não reparou");
    Check(mission.Repair(EquipmentId.Greenhouse).Success,"Estufa não reparou");
    mission.Power(EquipmentId.LifeSupport,true); mission.Power(EquipmentId.Greenhouse,true); mission.AdvanceTurn();
    Check(mission.Repair(EquipmentId.RadiationShelter).Success,"Proteção da base não reparou");
    Check(mission.Repair(EquipmentId.WaterRecycler).Success,"Reciclador não reparou");
    Check(baseModel.TotalConsertadas==PecasDaBase.Todas.Length,"Restauração incompleta: "+baseModel.TotalConsertadas+"/"+PecasDaBase.Todas.Length);
    var missing=PecasDaBase.Todas.Where(p=>!baseModel.EstaConsertada(p.id)||!baseModel.ObjetoConsertado(p.id).activeInHierarchy).Select(p=>p.id).ToArray();
    Check(missing.Length==0,"Modelos ainda quebrados: "+string.Join(",",missing));
    foreach(var part in PecasDaBase.Todas)
    foreach(var name in part.quebrados)
    {
        var broken=baseModel.baseLunar.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==name);
        Check(!broken || !broken.gameObject.activeInHierarchy,"Peça quebrada permaneceu visível: "+name);
    }
    Check(mission.Engine.State.SpareParts==0,"Orçamento da missão foi alterado");
    mission.AdvanceTurn(); Check(mission.AdvanceTurn().Success,"Missão completa não venceu");
    var cloud=UnityEngine.Object.FindAnyObjectByType<CloudCatalogClient>();
    for(int i=0;i<30 && !cloud.LoadedFromCloud;i++) await Task.Delay(200);
    Check(cloud.LoadedFromCloud,"Catálogo não foi lido da nuvem: "+cloud.Status);
    Check(runtimeErrors.Count==0,"Erros durante os reparos: "+string.Join("; ",runtimeErrors));
    Debug.Log("[AstroLab][Validation] PASS: 47 peças restauradas, porta acessível e transitável, custos preservados, vitória e catálogo da nuvem.");
    return new {cloudRead=true,totalPieces=47,doorTarget=true,doorRepair=true,doorOpens=true,walkedThroughDoor=true,doorCloses=true,allBrokenMeshesHidden=true,originalBudget=true,victory=true};
}
finally
{
    Application.logMessageReceived-=onLog;
    mission.NewGame(); Move(oldPosition); player.rotation=oldRotation; camera.transform.localRotation=oldCamera;
}
