const fs=require('fs'),vm=require('vm'),assert=require('assert');
const root=require('path').resolve(__dirname,'../ui/node_modules');
const React=require(root+'/react'),swc=require(root+'/@swc/core');
const values={destinationVisible:true,source:'10:2',destinations:'20:1,30:1',sinks:'20:1|Sink%20A,30:1|Sink%20B',spawnRate:5};
const calls=[],native={InfoSectionFoldout:()=>null,Checkbox:()=>null,RadioToggle:()=>null,Tooltip:()=>null};
const api={bindValue:(_,key)=>key,useValue:key=>values[key],trigger:(...args)=>calls.push(args)};
const translations={'SuperTrafficSpawnBlock.Destination':'차량 목적지','SuperTrafficSpawnBlock.Outside':'도시 외부','SuperTrafficSpawnBlock.Rate':'차량 생성 배율','SuperTrafficSpawnBlock.LoadWarning':'시스템에 큰 부하를 줄 수 있습니다'};
const code=swc.transformSync(fs.readFileSync(__dirname+'/../ui/src/index.jsx','utf8'),{jsc:{parser:{syntax:'ecmascript',jsx:true},transform:{react:{runtime:'classic'}}},module:{type:'commonjs'}}).code;
const context={exports:{},require:name=>name==='react'?React:name==='cs2/ui'?{Tooltip:native.Tooltip}:name==='cs2/l10n'?{useLocalization:()=>({translate:(key,fallback)=>translations[key]||fallback})}:api};vm.runInNewContext(code,context);
let render;const prior=()=>null;
context.exports.default({get:(path,key)=>key==='classes'?{header:'native-header',content:'native-content'}:native[key],extend:(path,key,fn)=>{const map=fn({prior});assert.equal(map.prior,prior);render=map['SuperTrafficSpawnBlock.DestinationSection'];}});
function nodes(e){return e&&typeof e==='object'?[e,...React.Children.toArray(e.props?.children).flatMap(nodes)]:[];}
let all=nodes(render()),boxes=all.filter(e=>e.type===native.Checkbox),radios=all.filter(e=>e.type===native.RadioToggle);
assert.equal(boxes.length,3);assert.deepEqual(boxes.map(b=>b.props.checked),[false,true,true]);boxes[1].props.onChange(false);assert.deepEqual(calls.pop(),['SuperTrafficSpawnBlock','toggleDestination','10:2','20:1',false]);boxes[0].props.onChange(true);assert.deepEqual(calls.pop(),['SuperTrafficSpawnBlock','toggleDestination','10:2','outside',true]);
assert.equal(radios.length,6);assert.equal(radios.filter(r=>r.props.checked).length,1);assert(radios[2].props.checked);radios[5].props.onChange(true);assert.deepEqual(calls.pop(),['SuperTrafficSpawnBlock','setSpawnRate','10:2',50]);radios[2].props.onChange(false);assert.equal(calls.length,0);
assert.equal(all.filter(e=>e.type===native.InfoSectionFoldout).length,2);assert(!all.some(e=>e.type==='button'));const css=all.find(e=>e.type==='style').props.children;assert(css.includes('padding:12rem 20rem'));assert(css.includes('margin:0;padding:0'));
const warnings=all.filter(e=>e.type===native.Tooltip);assert.equal(warnings.length,4);
assert.deepEqual(warnings.map(w=>nodes(w).filter(n=>n.type==='img').length),[1,1,2,3]);
assert(warnings.every(w=>w.props.tooltip==='시스템에 큰 부하를 줄 수 있습니다'));
assert(nodes(warnings[0]).filter(n=>n.type==='img').every(n=>n.props.src.endsWith('warning-yellow.svg')));
assert(warnings.slice(1).every(w=>nodes(w).filter(n=>n.type==='img').every(n=>n.props.src.endsWith('warning-red.svg'))));
values.source='11:1';values.spawnRate=2;values.destinations='outside';all=nodes(render());assert(all.filter(e=>e.type===native.RadioToggle)[1].props.checked);assert(all.filter(e=>e.type===native.Checkbox)[0].props.checked);values.destinationVisible=false;assert.equal(render(),null);
console.log('PASS: native multi-checkbox/radio controls, independent source payloads, one selected rate, shared padding and no status/dropdown.');

