const fs=require('fs');
const file=process.argv[2]||'.recovery/merged/Assets/Scenes/PrototypeLayout.unity';
const s=fs.readFileSync(file,'utf8');
const blocks=s.split(/(?=--- !u!\d+ &\d+\r?\n)/);
const ids=new Map(),duplicates=[];
for(const b of blocks){const id=b.match(/^--- !u!\d+ &(\d+)/)?.[1];if(!id)continue;if(ids.has(id))duplicates.push(id);ids.set(id,b);}
const broken=[];
for(const [id,b] of ids){
  if(!/^--- !u!(4|224) &/.test(b))continue;
  const parent=b.match(/m_Father: \{fileID: (\d+)\}/)?.[1];
  if(parent&&parent!=='0'&&ids.has(parent)&&!ids.get(parent).includes(`- {fileID: ${id}}`))broken.push(`parent ${id}->${parent}`);
  for(const child of b.matchAll(/^  - \{fileID: (\d+)\}$/gm))if(ids.has(child[1])&&!ids.get(child[1]).includes(`m_Father: {fileID: ${id}}`))broken.push(`child ${id}->${child[1]}`);
}
console.log(file,'objects',ids.size,'duplicates',duplicates.length,'broken links',broken.length);
console.log('duplicate ids',duplicates.slice(0,80).join(','));
console.log('broken',broken.slice(0,30).join(','));
if(duplicates.length||broken.length)process.exitCode=1;
