import React from 'react';
import {bindValue,useValue,trigger} from 'cs2/api';
import {useLocalization} from 'cs2/l10n';
import {Tooltip} from 'cs2/ui';
const group='SuperTrafficSpawnBlock';
const visible=bindValue(group,'destinationVisible',false),source=bindValue(group,'source',''),destinations=bindValue(group,'destinations','outside'),sinks=bindValue(group,'sinks',''),rate=bindValue(group,'spawnRate',2);
let Foldout,Checkbox,Radio,headerClass,contentClass;
const rowStyle={display:'flex',alignItems:'center',minHeight:'32rem',paddingTop:'4rem',paddingBottom:'4rem'};
function Destination(){
 const show=useValue(visible),src=useValue(source),selected=useValue(destinations).split(','),list=useValue(sinks),factor=useValue(rate);
 const {translate}=useLocalization();
 if(!show)return null;
 const choices=[{id:'outside',label:translate(group+'.Outside','Outside city')},...list.split(',').filter(Boolean).map(value=>{const [id,label]=value.split('|');return {id,label:decodeURIComponent(label)};})];
 const choose=(id,value)=>trigger(group,'toggleDestination',src,id,value);
 const setRate=value=>trigger(group,'setSpawnRate',src,value);
 return <>
  <style>{`.stsb-section .${headerClass}{padding:12rem 20rem;}.stsb-section .${contentClass}{padding:0 20rem 12rem;margin:0;}.stsb-section{margin:0;padding:0;}`}</style>
  <Foldout key={src+'dest'} className="stsb-section" header={translate(group+'.Destination','Traffic destinations')} initialExpanded={true}>
   <div style={{maxHeight:'240rem',overflowY:'auto'}}>{choices.map(c=><div key={c.id} style={rowStyle} onClick={()=>choose(c.id,!selected.includes(c.id))}>
    <Checkbox checked={selected.includes(c.id)} onChange={value=>choose(c.id,value)}/>
    <span style={{marginLeft:'10rem',flex:'1 1 auto',minWidth:0,whiteSpace:'normal',overflowWrap:'break-word'}}>{c.label}</span>
   </div>)}</div>
  </Foldout>
  <Foldout key={src+'rate'} className="stsb-section" header={translate(group+'.Rate','Spawn rate')} initialExpanded={true}>
   <div role="radiogroup" style={{display:'flex',flexWrap:'wrap'}}>{[1,2,5,10,20,50].map(value=><div key={value} style={{...rowStyle,width:'33.333%'}} onClick={()=>setRate(value)}>
    <Radio checked={factor===value} onChange={checked=>{if(checked)setRate(value);}}/>
    <span style={{marginLeft:'10rem'}}>{value}x</span>
    {value>=5&&<Tooltip tooltip={translate(group+'.LoadWarning','May place a heavy load on your system')}>
     <span aria-label={translate(group+'.LoadWarning','May place a heavy load on your system')} style={{display:'flex',position:'relative',flexShrink:0,marginLeft:'6rem',width:(24+(value===50?2:value===20?1:0)*8)+'rem',height:'24rem'}}>
      {Array.from({length:value===50?3:value===20?2:1},(_,index)=><img key={index} alt="" src={'coui://supertrafficspawnblock/warning-'+(value===5?'yellow':'red')+'.svg'} style={{position:'absolute',left:(index*8)+'rem',top:0,width:'24rem',height:'24rem',zIndex:index}}/>)}
     </span>
    </Tooltip>}
   </div>)}</div>
  </Foldout>
 </>;
}
export default registry=>{
 Foldout=registry.get('game-ui/game/components/selected-info-panel/shared-components/info-section/info-section-foldout.tsx','InfoSectionFoldout');
 Checkbox=registry.get('game-ui/common/input/toggle/checkbox/checkbox.tsx','Checkbox');
 Radio=registry.get('game-ui/common/input/toggle/radio-toggle/radio-toggle.tsx','RadioToggle');
 headerClass=registry.get('game-ui/common/foldout/themes/info-section-foldout.module.scss','classes').header.split(' ')[0];
 contentClass=registry.get('game-ui/game/components/selected-info-panel/shared-components/info-section/info-section.module.scss','classes').content.split(' ')[0];
 registry.extend('game-ui/game/components/selected-info-panel/selected-info-sections/selected-info-sections.tsx','selectedInfoSectionComponents',sections=>({...sections,['SuperTrafficSpawnBlock.DestinationSection']:Destination}));
};
export const hasCSS=false;


