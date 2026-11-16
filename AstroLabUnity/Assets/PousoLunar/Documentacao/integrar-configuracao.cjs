// Migração restrita e idempotente. Execute da raiz AstroLabUnity; não toca no FBX/importador.
const fs = require('fs');
const path = require('path');
const crypto = require('crypto');
const root = path.resolve('Assets/PousoLunar');
if (!fs.existsSync(path.join(root, 'Scripts/LunarLandingController.cs'))) throw Error('Execute da raiz AstroLabUnity.');
const put = (p,s) => { const out=path.resolve(root,p); if(!out.startsWith(root+path.sep))throw Error('Fora do módulo'); fs.mkdirSync(path.dirname(out),{recursive:true}); fs.writeFileSync(out,s); };
function guid(p, importer='DefaultImporter') {
  const file=path.join(root,p+'.meta');
  if(!fs.existsSync(file)) put(p+'.meta',`fileFormatVersion: 2\nguid: ${crypto.randomBytes(16).toString('hex')}\n${fs.statSync(path.join(root,p)).isDirectory()?'folderAsset: yes\n':''}${importer}:\n  externalObjects: {}\n${importer==='NativeFormatImporter'?'  mainObjectFileID: 2100000\n':''}  userData: \n  assetBundleName: \n  assetBundleVariant: \n`);
  return fs.readFileSync(file,'utf8').match(/guid: (\w+)/)[1];
}
const types=['LunarLandingSpacecraft','LunarLandingMainEngine','LunarLandingRCS','LunarLandingDust'];
const scriptGuids=types.map(t=>guid(`Scripts/${t}.cs`,'MonoImporter'));
fs.mkdirSync(path.join(root,'Shaders'),{recursive:true});
fs.mkdirSync(path.join(root,'Materiais'),{recursive:true});
guid('Shaders'); guid('Materiais');
const shader=guid('Shaders/LunarLandingParticle.shader','ShaderImporter');
function material(name,blend,brightness) {
  put(`Materiais/${name}.mat`,`%YAML 1.1\n%TAG !u! tag:unity3d.com,2011:\n--- !u!21 &2100000\nMaterial:\n  serializedVersion: 8\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_Name: ${name}\n  m_Shader: {fileID: 4800000, guid: ${shader}, type: 3}\n  m_ValidKeywords: []\n  m_InvalidKeywords: []\n  m_LightmapFlags: 4\n  m_EnableInstancingVariants: 1\n  m_DoubleSidedGI: 0\n  m_CustomRenderQueue: 3000\n  stringTagMap: {}\n  disabledShaderPasses: []\n  m_SavedProperties:\n    serializedVersion: 3\n    m_TexEnvs: []\n    m_Ints: []\n    m_Floats:\n    - _DstBlend: ${blend}\n    m_Colors:\n    - _BaseColor: {r: ${brightness}, g: ${brightness}, b: ${brightness}, a: 1}\n  m_BuildTextureStacks: []\n`);
  return guid(`Materiais/${name}.mat`,'NativeFormatImporter');
}
const mats=[material('M_VacuumPlume',1,2),material('M_RCSGas',10,1),material('M_LunarDust',10,1)];
function ref(id){return `{fileID: ${id}}`;}
function modelReference() {
  const file=path.join(root,'Documentacao/identidade-modelo-unity.json');
  if(!fs.existsSync(file)) {
    // Uma referência GameObject já salva pelo Unity é evidência válida; não adivinhar IDs de subassets.
    const scene=fs.readFileSync(path.join(root,'Cenas/PousoLunar.unity'),'utf8');
    const stored=scene.match(/sourceModel: \{fileID: (-?\d+), guid: 614bef39780f3af439767de6f4943616, type: 3\}/);
    if(stored&&stored[1]!=='100100000'&&stored[1]!=='0') return stored[0].slice('sourceModel: '.length);
    throw Error('Referência GameObject ainda não obtida do Unity. Execute Inspecionar referência importada do modelo.');
  }
  const identity=JSON.parse(fs.readFileSync(file,'utf8').replace(/^\uFEFF/,''));
  if(identity.type!=='UnityEngine.GameObject'||identity.guid!=='614bef39780f3af439767de6f4943616'||typeof identity.localId!=='string'||!/^[-]?\d+$/.test(identity.localId)||identity.localId==='100100000'||identity.localId==='0')
    throw Error('Identidade importada do GameObject inválida; execute Inspecionar referência importada do modelo no Unity.');
  return `{fileID: ${identity.localId}, guid: ${identity.guid}, type: 3}`;
}
function migrate(file) {
  let original=fs.readFileSync(path.join(root,file),'utf8').replace(/\r\n/g,'\n');
  let blocks=original.split(/(?=^--- !u!)/m);
  const block=(text)=>blocks.find(b=>b.includes(text));
  const id=(b)=>b.match(/^--- !u!\d+ &(-?\d+)/)[1];
  const controller=block('AstroLab.PousoLunar.LunarLandingController\n');
  const controllerId=id(controller),rootId=controller.match(/m_GameObject: \{fileID: (\d+)\}/)[1];
  const rootIndex=blocks.findIndex(b=>b.startsWith(`--- !u!1 &${rootId}\n`));
  // Remove somente o subgrafo do preview, preservando todos os demais IDs e waypoints.
  const preview=blocks.find(b=>b.includes('m_Name: PREVIEW_ExistingBlenderRocket_RemoveForFinalFBX\n'));
  if(preview) {
    const removed=new Set([id(preview)]);
    let change=true;
    while(change) { change=false; for(const b of blocks) {
      if(!b.startsWith('---'))continue;
      const bid=id(b), go=b.match(/m_GameObject: \{fileID: (\d+)\}/), father=b.match(/m_Father: \{fileID: (\d+)\}/);
      if(!removed.has(bid) && (go&&removed.has(go[1]) || father&&removed.has(father[1]))) {removed.add(bid);change=true;}
      if(removed.has(bid)) {
        if(go&&!removed.has(go[1])) {removed.add(go[1]);change=true;}
        for(const m of b.matchAll(/- component: \{fileID: (\d+)\}/g)) if(!removed.has(m[1])){removed.add(m[1]);change=true;}
      }
    }}
    blocks=blocks.filter(b=>!b.startsWith('---')||!removed.has(id(b))).map(b=>b.replace(/^  - \{fileID: (\d+)\}\n/gm,(s,n)=>removed.has(n)?'':s).replace(/m_Children:\n(?=  m_Father)/g,'m_Children: []\n'));
  }
  const ids=[790000001,790000002,790000003,790000004];
  if(blocks.some(b=>b.includes('AstroLab.PousoLunar.LunarLandingSpacecraft\n'))) { console.log('Já configurado: '+file);return; }
  const rootBlock=blocks.findIndex(b=>b.startsWith(`--- !u!1 &${rootId}\n`));
  blocks[rootBlock]=blocks[rootBlock].replace('  m_Layer:',ids.map(n=>`  - component: ${ref(n)}\n`).join('')+'  m_Layer:');
  const effectIndex=blocks.findIndex(b=>b.includes('AstroLab.PousoLunar.LunarLandingEffects\n'));
  blocks[effectIndex]+= `  mainEngine: ${ref(ids[1])}\n  rcs: ${ref(ids[2])}\n  lunarDust: ${ref(ids[3])}\n`;
  const fields=[
    `  controller: ${ref(controllerId)}\n  sourceModel: ${modelReference()}\n  plumeMaterial: {fileID: 2100000, guid: ${mats[0]}, type: 2}\n  gasMaterial: {fileID: 2100000, guid: ${mats[1]}, type: 2}\n  dustMaterial: {fileID: 2100000, guid: ${mats[2]}, type: 2}\n  modelHeight: 8\n  visualOffset: {x: 0, y: 0, z: 0}\n  visualEuler: {x: 0, y: 0, z: 0}\n  mainEngine: ${ref(ids[1])}\n  rcs: ${ref(ids[2])}\n  lunarDust: ${ref(ids[3])}\n`,
    `  controller: ${ref(controllerId)}\n  nozzles: []\n  responseSpeed: 2.5\n  plumeLength: 3\n  plumeWidth: 0.65\n  coreEmissionRate: 150\n  outerEmissionRate: 90\n  glowEmissionRate: 35\n  flickerAmount: 0.035\n  previewIntensity: 0.65\n  previewDuration: 3\n`,
    `  controller: ${ref(controllerId)}\n  localCenterOfMass: {x: 0, y: 4, z: 0}\n  thrusters: []\n  burstDuration: 0.11\n  repeatInterval: 0.28\n  accelerationDeadZone: 0.12\n  fullThrustAcceleration: 4\n  stabilizationIntensity: 0.35\n  manualThrusterIndex: 0\n  manualIntensity: 0.7\n`,
    `  controller: ${ref(controllerId)}\n  mainEngine: ${ref(ids[1])}\n  grains: {fileID: 0}\n  touchdownGrains: {fileID: 0}\n  interactionHeight: 5\n  particlesPerSecond: 180\n  outwardVelocity: 4\n  lifetime: 0.8\n  upwardSpread: 0.12\n  touchdownCount: 65\n`
  ];
  for(let i=0;i<ids.length;i++) blocks.push(`--- !u!114 &${ids[i]}\nMonoBehaviour:\n  m_ObjectHideFlags: 0\n  m_CorrespondingSourceObject: {fileID: 0}\n  m_PrefabInstance: {fileID: 0}\n  m_PrefabAsset: {fileID: 0}\n  m_GameObject: ${ref(rootId)}\n  m_Enabled: 1\n  m_EditorHideFlags: 0\n  m_Script: {fileID: 11500000, guid: ${scriptGuids[i]}, type: 3}\n  m_Name: \n  m_EditorClassIdentifier: Assembly-CSharp::AstroLab.PousoLunar.${types[i]}\n${fields[i]}`);
  put(file,blocks.map(b=>/AstroLab.PousoLunar.LunarLanding(Spacecraft|MainEngine|RCS|Dust)\n/.test(b)?b.replace(/ +$/gm,''):b).join(''));
  console.log('Configurado: '+file);
}
migrate('Cenas/PousoLunar.unity');
migrate('Prefabs/RocketLandingRoot.prefab');
// Metas completos, inclusive documentação e scripts que Unity ainda não pôde importar.
function metas(dir) { for(const e of fs.readdirSync(dir,{withFileTypes:true})) {
  if(e.name.startsWith('.')||e.name.endsWith('.meta')||e.name.endsWith('~')||e.name.endsWith('.log'))continue;
  const absolute=path.join(dir,e.name), relative=path.relative(root,absolute).replaceAll('\\','/');
  const importer=e.name.endsWith('.cs')?'MonoImporter':'DefaultImporter';
  guid(relative,importer);
  if(e.isDirectory())metas(absolute);
}}
metas(root);
