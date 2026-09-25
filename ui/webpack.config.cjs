const path=require('path');
module.exports={mode:'production',entry:'./src/index.jsx',context:__dirname,
externalsType:'window',externals:{react:'React','cs2/api':'cs2/api','cs2/ui':'cs2/ui','cs2/l10n':'cs2/l10n','cs2/modding':'cs2/modding'},
module:{rules:[{test:/\.jsx$/,use:{loader:require.resolve('swc-loader'),options:{jsc:{parser:{syntax:'ecmascript',jsx:true},transform:{react:{runtime:'classic'}}}}}}]},
output:{path:path.resolve(__dirname,'../local/SuperTrafficSpawnBlock'),filename:'SuperTrafficSpawnBlock.mjs',library:{type:'module'}},experiments:{outputModule:true}};


