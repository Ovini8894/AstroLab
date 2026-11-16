using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEngine;
using AstroLab.Asteroids;
var terminal=UnityEngine.Object.FindAnyObjectByType<AsteroidTerminal>();
const BindingFlags fields=BindingFlags.Instance|BindingFlags.NonPublic;
string path=(string)typeof(AsteroidTerminal).GetProperty("SavePath",fields).GetValue(terminal);
bool existed=File.Exists(path);byte[] oldFile=existed?File.ReadAllBytes(path):null;
string memory=JsonUtility.ToJson(terminal.Progress);
int initial=terminal.Progress.Count;
try
{
    terminal.Open();await Task.Delay(200);
    var record=terminal.Client.Records.First(r=>AsteroidScience.CanAnalyse(r)&&r.MinMetres>1&&!terminal.Progress.Contains(r.Id));
    typeof(AsteroidTerminal).GetField("selected",fields).SetValue(terminal,record);
    terminal.BeginAnalysis();
    var slider=(UnityEngine.UI.Slider)typeof(AsteroidTerminal).GetField("slider",fields).GetValue(terminal);
    var confirm=(UnityEngine.UI.Button)typeof(AsteroidTerminal).GetField("estimateButton",fields).GetValue(terminal);
    if(slider.value!=0||confirm.interactable||terminal.CurrentAnalysis.Question!=null)throw new Exception("Estimativa começou pronta");
    terminal.ConfirmSizeEstimate();
    if(terminal.Answer(0)||terminal.Progress.Count!=initial)throw new Exception("Permitiu pular a estimativa");
    var facts=(RectTransform)typeof(AsteroidTerminal).GetField("facts",fields).GetValue(terminal);
    var data=facts.GetComponentsInChildren<TMPro.TMP_Text>().First(t=>t.name=="Dados");
    if(!data.text.Contains("Oculto"))throw new Exception("Faixa de tamanho revelada antes do palpite");
    slider.value=(float)(record.MinMetres*.1/terminal.CurrentAnalysis.ScaleMaximum);terminal.ConfirmSizeEstimate();
    if(terminal.CurrentAnalysis.SizeAccepted||terminal.Progress.Count!=initial)throw new Exception("Palpite fora da faixa foi aceito");
    slider.value=(float)(terminal.CurrentAnalysis.RepresentativeMetres/terminal.CurrentAnalysis.ScaleMaximum);terminal.ConfirmSizeEstimate();
    if(!terminal.CurrentAnalysis.SizeAccepted)throw new Exception("Palpite visual na faixa NASA não foi aceito");
    var question=terminal.CurrentAnalysis.Question;
    int wrong=(question.CorrectIndex+1)%3;
    if(terminal.Answer(wrong)||terminal.Progress.Count!=initial)throw new Exception("Resposta errada alterou a coleção");
    if(!terminal.Answer(question.CorrectIndex)||terminal.Progress.Count!=initial+1)throw new Exception("Análise correta não foi registrada");
    if(terminal.Answer(question.CorrectIndex))throw new Exception("Resposta repetida reabriu análise encerrada");
    if(!File.Exists(path))throw new Exception("Análise não foi salva");
    terminal.BeginAnalysis();
    slider=(UnityEngine.UI.Slider)typeof(AsteroidTerminal).GetField("slider",fields).GetValue(terminal);
    slider.value=(float)(terminal.CurrentAnalysis.RepresentativeMetres/terminal.CurrentAnalysis.ScaleMaximum);terminal.ConfirmSizeEstimate();
    if(terminal.CurrentAnalysis.Question.Kind==question.Kind)throw new Exception("Revisão repetiu o mesmo tipo de questão");
    terminal.Answer(terminal.CurrentAnalysis.Question.CorrectIndex);
    if(terminal.Progress.Count!=initial+1)throw new Exception("Alvo repetido duplicou a descoberta");
    return "PASS: slider inicia neutro, confirmar exige mover, faixa oculta antes do palpite, sem pular etapa, erro permite tentar novamente, tamanho válido revela dados, três alternativas, questão varia ao rever, análise salva uma vez e conquista não duplica. Progresso do usuário restaurado.";
}
finally
{
    terminal.Close();
    typeof(AsteroidTerminal).GetProperty("Progress").SetValue(terminal,JsonUtility.FromJson<AsteroidProgress>(memory));
    if(existed)File.WriteAllBytes(path,oldFile);else if(File.Exists(path))File.Delete(path);
}
