import json, collections, os
d=json.load(open('dmi_all.json'))
tot_states=sum(len(o['states']) for o in d)
print('files',len(d),'states',tot_states)
# frames
tot_frames_img=0
anim=0; multi_dir=collections.Counter(); framecount=collections.Counter()
state_names_unique=set()
for o in d:
    for s in o['states']:
        multi_dir[s['dirs']]+=1
        framecount[s['frames']]+=1
        if s['frames']>1: anim+=1
        tot_frames_img+=s['dirs']*s['frames']
        state_names_unique.add(s['name'])
print('total image cells (dirs*frames)',tot_frames_img)
print('animated states (frames>1)',anim)
print('dirs dist',sorted(multi_dir.items()))
print('frames top',sorted(framecount.items())[:15], 'max', max(framecount))
print('unique state names globally',len(state_names_unique))
# cell sizes
sz=collections.Counter()
szstates=collections.Counter()
for o in d:
    sz[(o['cw'],o['ch'])]+=1
    szstates[(o['cw'],o['ch'])]+=len(o['states'])
print('cell sizes (files)',sz.most_common(25))
print('cell sizes (states)',szstates.most_common(25))
# non-32 files
big=[o for o in d if (o['cw'],o['ch'])!=(32,32)]
print('non-32x32 files',len(big),'states',sum(len(o['states']) for o in big))
# top dirs by file count and state count
byd=collections.defaultdict(lambda:[0,0,0])
for o in d:
    parts=o['path'].split('/')
    key='/'.join(parts[:2]) if parts[0]=='icons' else '/'.join(parts[:3]) 
    byd[key][0]+=1; byd[key][1]+=len(o['states']); byd[key][2]+=o['size']
print('--- by dir (files, states, bytes)')
for k,v in sorted(byd.items(), key=lambda kv:-kv[1][1])[:40]:
    print(k,v)
# top-level root groupings
root=collections.defaultdict(lambda:[0,0,0])
for o in d:
    r=o['path'].split('/')[0]
    root[r][0]+=1; root[r][1]+=len(o['states']); root[r][2]+=o['size']
print('--- by root',dict(root))
# top 15 files by state count
print('--- top files by states')
for o in sorted(d,key=lambda o:-len(o['states']))[:20]:
    print(o['path'],len(o['states']),o['cw'],o['ch'])
