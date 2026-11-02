const fs=require('fs');
const path=require('path');
const cp=require('child_process');
const merged=[
  'Assets/Scenes/PrototypeLayout.unity',
  'Assets/Scripts/Gameplay/ActionResolver.cs',
  'Assets/Scripts/Gameplay/DayCycle.cs',
  'Assets/Scripts/Interaction/WorkerAssignment.cs',
  'Packages/manifest.json',
  'Packages/packages-lock.json'
];
const added=[
  'Assets/Editor/CommunityCameraEditor.cs',
  'Assets/Editor/CommunityCameraEditor.cs.meta',
  'Assets/Editor/CommunityCameraPlaySave.cs',
  'Assets/Editor/CommunityCameraPlaySave.cs.meta',
  'Assets/Scripts/Interaction/CommunityCamera.cs',
  'Assets/Scripts/Interaction/CommunityCamera.cs.meta'
];
for(const file of merged)fs.copyFileSync(path.join('.recovery/merged',file),file);
for(const file of added){
  fs.mkdirSync(path.dirname(file),{recursive:true});
  fs.writeFileSync(file,cp.execFileSync('git',['show','c268f25:'+file]));
}
console.log('Restored',merged.length,'merged files and',added.length,'missing files');
