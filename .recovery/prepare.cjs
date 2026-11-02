const fs=require('fs');
const path=require('path');
const cp=require('child_process');
const root='.recovery';
const base='34a9bee';
const ours='f40fdec';
const theirs='c268f25';
const git=(args)=>cp.execFileSync('git',args,{maxBuffer:30*1024*1024});
fs.mkdirSync(root,{recursive:true});
fs.writeFileSync(path.join(root,'camera-stash.patch'),git(['diff','--binary',base,theirs]));
const changed=git(['diff','--name-only',base,theirs]).toString().trim().split(/\r?\n/);
for(const file of changed){
  if(!/\.(cs|unity|json)$/.test(file))continue;
  const put=(kind,bytes)=>{const p=path.join(root,kind,file);fs.mkdirSync(path.dirname(p),{recursive:true});fs.writeFileSync(p,bytes);return p;};
  const get=(rev)=>{try{return git(['show',rev+':'+file])}catch{return Buffer.from('')}};
  const a=put('current',get(ours)),b=put('base',get(base)),c=put('stash',get(theirs));
  const merged=cp.spawnSync('git',['merge-file','-p',a,b,c],{maxBuffer:30*1024*1024});
  put('merged',merged.stdout);
  console.log(merged.status===0?'OK      ':'CONFLICT',file);
}
