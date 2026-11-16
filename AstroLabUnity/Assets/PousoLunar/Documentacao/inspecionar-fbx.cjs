// Read-only FBX 7.x binary inspection. node inspecionar-fbx.cjs path/to/model.fbx
const fs = require('fs'), z = require('zlib');
const b = fs.readFileSync(process.argv[2]); let p = 23;
const version = b.readUInt32LE(p); p += 4;
function uint() { const n = version >= 7500 ? Number(b.readBigUInt64LE(p)) : b.readUInt32LE(p); p += version >= 7500 ? 8 : 4; return n; }
function prop() {
  const type = String.fromCharCode(b[p++]);
  const size = {Y:2,C:1,I:4,F:4,D:8,L:8}[type];
  if (size) { let v; if(type==='Y')v=b.readInt16LE(p);if(type==='C')v=b[p];if(type==='I')v=b.readInt32LE(p);if(type==='F')v=b.readFloatLE(p);if(type==='D')v=b.readDoubleLE(p);if(type==='L')v=String(b.readBigInt64LE(p));p+=size;return v; }
  if(type==='S'||type==='R'){const n=b.readUInt32LE(p);p+=4;const v=type==='S'?b.toString('utf8',p,p+n):'<binary>';p+=n;return v;}
  const n=b.readUInt32LE(p), encoding=b.readUInt32LE(p+4), len=b.readUInt32LE(p+8);p+=12;let a=b.subarray(p,p+len);p+=len;if(encoding)a=z.inflateSync(a); const out=[];
  const stride={f:4,d:8,l:8,i:4,b:1,c:1}[type];
  for(let i=0;i<n;i++) out.push(type==='d'?a.readDoubleLE(i*stride):type==='f'?a.readFloatLE(i*stride):type==='l'?String(a.readBigInt64LE(i*stride)):type==='i'?a.readInt32LE(i*stride):a[i]);return out;
}
function node(){const end=uint(),count=uint(),len=uint(),n=b[p++];if(!end)return null;const name=b.toString('utf8',p,p+n);p+=n;const props=Array.from({length:count},prop),children=[];while(p<end){const child=node();if(!child)break;children.push(child);}p=end;return {name,props,children};}
const roots=[];while(p<b.length-32){const n=node();if(!n)break;roots.push(n);}
function get(n,name){return n.children.find(c=>c.name===name);}
const objects=roots.find(n=>n.name==='Objects');
const out={version,settings:roots.find(n=>n.name==='GlobalSettings'),connections:roots.find(n=>n.name==='Connections'),objects:[]};
const selected=process.argv[4]?objects.children.filter(n=>String(n.props[1]).includes(process.argv[4])).map(n=>n.props[0]):[];
if(process.argv[4])for(const c of out.connections.children)if(selected.includes(c.props[2]))selected.push(c.props[1]);
for(const n of objects.children){if(process.argv[4]&&!selected.includes(n.props[0]))continue;const item={type:n.name,id:n.props[0],name:n.props[1],kind:n.props[2]};if(n.name==='Geometry'){const v=get(n,'Vertices')?.props[0];if(v){let min=[Infinity,Infinity,Infinity],max=[-Infinity,-Infinity,-Infinity];for(let i=0;i<v.length;i++) {const a=i%3;min[a]=Math.min(min[a],v[i]);max[a]=Math.max(max[a],v[i]);}Object.assign(item,{vertices:v.length/3,min,max});if(process.argv[4])item.positions=v;}}else if(n.name==='Model'||n.name==='Material'||n.name==='Texture'||n.name==='Video')item.children=n.children;out.objects.push(item);}
if(process.argv[4])out.connections=out.connections.children.filter(n=>n.props.some(p=>out.objects.some(o=>o.id===p)));
if (process.argv[3]) fs.writeFileSync(process.argv[3], JSON.stringify(out,null,2), 'utf8');
else process.stdout.write(JSON.stringify(out,null,2));
