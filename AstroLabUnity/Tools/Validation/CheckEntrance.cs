using System;
using System.Threading.Tasks;
using UnityEngine;
using AstroLab.Gameplay;
using AstroLab.Nicholas;
void Check(bool x,string s){if(!x)throw new Exception(s);}
var m=MissionController.Instance;var p=m.PlayerTransform;var fps=p.GetComponent<JogadorFPS>();var motor=p.GetComponent<RadialCharacterMotor>();
var terminal=UnityEngine.Object.FindAnyObjectByType<AstroLab.Asteroids.AsteroidTerminal>();terminal.Close();
var overlay=UnityEngine.Object.FindAnyObjectByType<MissionOverlay>();overlay.enabled=false;Time.timeScale=1;
m.NewGame();fps.enabled=false;p.SetPositionAndRotation(new Vector3(0,.95f,25),Quaternion.LookRotation(Vector3.back));Physics.SyncTransforms();
try
{
    var safety=UnityEngine.Object.FindAnyObjectByType<BaseInteriorSafety>();safety.Synchronize();Check(m.Engine.State.IsOutside,"Rampa descoberta considerada interior");
    for(int i=0;i<280;i++){motor.Move(new Vector3(0,-.04f,-.06f));await Task.Delay(16);}
    Check(p.position.z<12,"Entrada bloqueada em "+p.position);
    Check(!m.Engine.State.IsOutside,"Chegada ao interior manteve EVA");
    Check(p.position.y>1.7,"Personagem caiu pelo piso");
    p.rotation=Quaternion.identity;
    for(int i=0;i<280;i++){motor.Move(new Vector3(0,-.04f,.06f));await Task.Delay(16);}
    Check(p.position.z>23.9f,"Saída bloqueada em "+p.position);Check(m.Engine.State.IsOutside,"Saída não iniciou EVA");
    return "PASS: terreno → rampa → porta automática → piso da eclusa → corredor; retorno ao terreno sem teleporte; estado interior/EVA correto nos dois sentidos. Posição final: "+p.position;
}
finally{overlay.enabled=true;fps.enabled=true;m.NewGame();}

