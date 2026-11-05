import json, glob, os
import sys; base=sys.argv[1] if len(sys.argv)>1 else '../../Assets/AutoloadedEffects'
ordem=['Shaders/NuvensShader.fx','Filters/BuracoNegroFilter.fx','Shaders/LaserEscarlateShader.fx','Filters/VeuFilter.fx','Filters/GeloFilter.fx','Filters/OndaFilter.fx']
sh={os.path.basename(p)[:-3]: open(os.path.join(base,p),encoding='utf-8').read() for p in ordem}
t=open('bancada.template.html',encoding='utf-8').read()
t=t.replace('/*RUIDOS*/{}', open('ruidos.json').read()).replace('/*SHADERS*/{}', json.dumps(sh, ensure_ascii=False))
open('bancada.html','w',encoding='utf-8').write(t)
# página de teste local (com skeleton)
open('teste.html','w',encoding='utf-8').write('<!doctype html><html><head><meta charset="utf-8"></head><body>'+t+'</body></html>')
print(len(t))
