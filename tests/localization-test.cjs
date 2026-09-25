const fs=require('fs'),path=require('path'),assert=require('assert');
const root=path.resolve(__dirname,'..');
const en=JSON.parse(fs.readFileSync(path.join(root,'lang/en-US.json'),'utf8'));
const ko=JSON.parse(fs.readFileSync(path.join(root,'lang/ko-KR.json'),'utf8'));
assert.deepStrictEqual(Object.keys(en).sort(),Object.keys(ko).sort());
for(const dict of [en,ko]) for(const value of Object.values(dict)) assert(typeof value==='string'&&value.trim());
const ui=fs.readFileSync(path.join(root,'ui/src/index.jsx'),'utf8');
for(const match of ui.matchAll(/translate\(group\+'([^']+)'/g)) assert(en['SuperTrafficSpawnBlock'+match[1]]);
const prefix='SuperTrafficSpawnBlock.SuperTrafficSpawnBlock.Mod';
for(const name of ['Status','AllowHeavyVehicles']) for(const kind of ['OPTION','OPTION_DESCRIPTION'])
 assert(en[`Options.${kind}[${prefix}.Settings.${name}]`]);
for(const key of ['Waiting','Ready','Inactive']) assert(en['SuperTrafficSpawnBlock.Status.'+key]);
console.log(`PASS: ${Object.keys(en).length} matching locale keys, option IDs, UI coverage and status translations.`);
