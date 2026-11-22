import os, json, collections, pickle
from dmm_parse import summarize, ROOT
res=[]
for dp,dn,fn in os.walk(os.path.join(ROOT,'_maps')):
    for f in fn:
        if f.endswith('.dmm'):
            p=os.path.join(dp,f)
            try:
                s=summarize(p)
                res.append(s)
            except Exception as e:
                res.append({'path':os.path.relpath(p,ROOT),'err':str(e)})
pickle.dump(res,open('dmm_all.pkl','wb'))
print(len(res), sum(1 for r in res if 'err' in r))
