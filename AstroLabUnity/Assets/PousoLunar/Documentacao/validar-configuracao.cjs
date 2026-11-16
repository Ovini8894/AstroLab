// Validação estática; não representa um teste de importação ou Play Mode.
const fs=require('fs'),path=require('path'),cp=require('child_process'),assert=require('assert');
const root='Assets/PousoLunar/';
const original=f=>cp.execFileSync('git',['show','HEAD:AstroLabUnity/'+f],{encoding:'utf8'}).replace(/\r\n/g,'\n');
const blocks=s=>s.replace(/\r\n/g,'\n').split(/(?=^--- !u!)/m);
function validate(file) {
  const current=fs.readFileSync(file,'utf8'), entries=blocks(current);
  const identifiers=entries.flatMap(b=>{const m=b.match(/^--- !u!\d+ &(-?\d+)/);return m?[m[1]]:[];});
  const ids=new Set(identifiers); assert.equal(ids.size,identifiers.length,'IDs duplicados');
  for(const m of current.matchAll(/\{fileID: (-?\d+)\}/g))assert(m[1]==='0'||ids.has(m[1]),'Referência local ausente: '+m[1]);
  assert(!current.includes('PREVIEW_ExistingBlenderRocket_RemoveForFinalFBX'),'Preview presente');
  assert(!current.includes('guid: 8a28544c5f45f774fbe265e7a2143a56'),'Foguete antigo ainda referenciado');
  const before=blocks(original(file));
  for(const marker of ['AstroLab.PousoLunar.LunarLandingController\n','m_Name: LandingPoint\n','m_Name: Approach\n','m_Name: Deceleration\n','m_Name: VerticalDescent\n']) {
    const prior=before.find(b=>b.includes(marker)); assert(prior,'Marcador original ausente: '+marker);
    assert(entries.includes(prior),'Bloco preservado alterado: '+marker);
    if(marker.startsWith('m_Name:')) {const transform=prior.match(/component: \{fileID: (\d+)\}/)[1];const priorTransform=before.find(b=>b.startsWith('--- !u!4 &'+transform+'\n'));assert(entries.includes(priorTransform),'Waypoint movido');}
  }
  assert(!current.includes('sourceModel: {fileID: 100100000,'),'sourceModel aponta para Prefab incompatível com GameObject');
  const identityFile=root+'Documentacao/identidade-modelo-unity.json';
  if(fs.existsSync(identityFile)) {
    const identity=JSON.parse(fs.readFileSync(identityFile,'utf8').replace(/^\uFEFF/,''));
    assert(identity.type==='UnityEngine.GameObject'&&typeof identity.localId==='string'&&identity.localId!=='100100000','Identidade importada incorreta');
    assert(current.includes(`sourceModel: {fileID: ${identity.localId}, guid: ${identity.guid}, type: 3}`),'Referência do modelo não corresponde ao GameObject importado');
  } else assert(current.includes('sourceModel: {fileID: 0}')||/sourceModel: \{fileID: -?\d+, guid: 614bef39780f3af439767de6f4943616, type: 3\}/.test(current),'Modelo final não configurado');
  console.log('PASS grafo local, preview removido, controlador/waypoints preservados e referência FBX compatível: '+file);
}
validate(root+'Cenas/PousoLunar.unity'); validate(root+'Prefabs/RocketLandingRoot.prefab');
const baselineFile=root+'Documentacao/preservacao-apresentacao.json';
if(fs.existsSync(baselineFile)) {
  const crypto=require('crypto');
  const baseline=JSON.parse(fs.readFileSync(baselineFile,'utf8'));
  for(const [file,hash] of Object.entries(baseline.files))
    assert.equal(crypto.createHash('sha256').update(fs.readFileSync(root+file)).digest('hex'),hash,'Animação/propulsão/API alterada: '+file);
  console.log('PASS hashes de animação, propulsão e APIs públicas iguais ao início desta melhoria.');
}
const landing=fs.readFileSync(root+'Cenas/PousoLunar.unity','utf8');
assert(/SceneRoots:\r?\n  m_ObjectHideFlags: 0\r?\n  m_Roots:/.test(landing),'Raízes da cena inválidas');
assert(landing.includes('sourceVisualModel: {fileID: 919132149155446097, guid: a564f47d55b9d42f8a7b4b43e84134c3, type: 3}'),'Base visual não configurada');
for(const file of ['Cenas/PousoLunar.unity','Prefabs/RocketLandingRoot.prefab']) {
  const camera=blocks(fs.readFileSync(root+file,'utf8')).find(b=>b.includes('AstroLab.PousoLunar.LunarLandingCamera\n'));
  assert.equal([...camera.matchAll(/followTarget: 1/g)].length,4,'Acompanhamento ausente');
  for(const m of camera.matchAll(/elevation: ([\d.]+)/g))assert(+m[1]>=20 && +m[1]<=40,'Elevação fora da faixa');
}
console.log('PASS ambiente referenciado, SceneRoots e quatro planos elevados com acompanhamento.');
const guids=new Set();
function walk(dir){for(const d of fs.readdirSync(dir,{withFileTypes:true})){const p=path.join(dir,d.name);if(d.isDirectory())walk(p);else if(p.endsWith('.meta')){const m=fs.readFileSync(p,'utf8').match(/^guid: (\w+)/m);if(m)guids.add(m[1]);}}}
walk('Assets');
const baselineGuids=new Set([...original(root+'Cenas/PousoLunar.unity').matchAll(/guid: ([0-9a-f]{32})/g),...original(root+'Prefabs/RocketLandingRoot.prefab').matchAll(/guid: ([0-9a-f]{32})/g)].map(m=>m[1]));
for(const f of [root+'Cenas/PousoLunar.unity',root+'Prefabs/RocketLandingRoot.prefab',root+'Prefabs/LunarBaseVisualEnvironment.prefab',...fs.readdirSync(root+'Materiais').filter(x=>x.endsWith('.mat')).map(x=>root+'Materiais/'+x)])
  for(const m of fs.readFileSync(f,'utf8').matchAll(/guid: ([0-9a-f]{32})/g))assert(baselineGuids.has(m[1])||guids.has(m[1]),'GUID novo ausente: '+m[1]);
const changed=cp.execFileSync('git',['diff','--name-only'],{encoding:'utf8'}).trim().split('\n').filter(Boolean);
assert(changed.every(p=>p.startsWith('AstroLabUnity/Assets/PousoLunar/')),'Alteração fora do escopo');
console.log('PASS novos GUIDs de assets e escopo; referências antigas mantidas. IDs internos de assets importados exigem Unity; não verificados aqui.');
