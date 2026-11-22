import os, sys, struct, zlib, re, json, collections
ROOT='/Users/alexey/Desktop/projects/SpaceStation3D/tgstation'
def read_dmi(path):
    with open(path,'rb') as f:
        data=f.read()
    assert data[:8]==b'\x89PNG\r\n\x1a\n', path
    pos=8
    w=h=None
    desc=None
    while pos<len(data):
        ln,=struct.unpack('>I',data[pos:pos+4]); typ=data[pos+4:pos+8]
        body=data[pos+8:pos+8+ln]
        if typ==b'IHDR':
            w,h=struct.unpack('>II',body[:8])
        elif typ==b'zTXt':
            k,rest=body.split(b'\0',1)
            if k==b'Description':
                desc=zlib.decompress(rest[1:]).decode('utf-8','replace')
        elif typ==b'tEXt':
            k,rest=body.split(b'\0',1)
            if k==b'Description':
                desc=rest.decode('utf-8','replace')
        elif typ==b'iTXt':
            k,rest=body.split(b'\0',1)
            if k==b'Description':
                comp=rest[0]; rest=rest[2:]
                _,rest=rest.split(b'\0',1); _,rest=rest.split(b'\0',1)
                desc=(zlib.decompress(rest) if comp else rest).decode('utf-8','replace')
        pos+=12+ln
    return w,h,desc
def parse_desc(desc):
    # returns (version,width,height, states list)
    lines=desc.split('\n')
    width=height=32
    states=[]
    cur=None
    for ln in lines:
        ln=ln.strip()
        if not ln or ln.startswith('#'): continue
        if ln.startswith('version'): continue
        m=re.match(r'(\w+)\s*=\s*(.*)',ln)
        if not m: continue
        k,v=m.group(1),m.group(2)
        if k=='width' and cur is None: width=int(v)
        elif k=='height' and cur is None: height=int(v)
        elif k=='state':
            cur={'name':v.strip().strip('"'),'dirs':1,'frames':1,'delay':None,'loop':0,'rewind':0,'movement':0,'hotspot':None}
            states.append(cur)
        elif cur is not None:
            if k=='dirs': cur['dirs']=int(v)
            elif k=='frames': cur['frames']=int(v)
            elif k=='delay': cur['delay']=v
            elif k=='loop': cur['loop']=int(v)
            elif k=='rewind': cur['rewind']=int(v)
            elif k=='movement': cur['movement']=int(v)
    return width,height,states
if __name__=='__main__':
    out=[]
    for dp,dn,fn in os.walk(ROOT):
        if '/.git' in dp: continue
        for f in fn:
            if f.endswith('.dmi'):
                p=os.path.join(dp,f)
                try:
                    w,h,desc=read_dmi(p)
                    if desc is None:
                        out.append({'path':os.path.relpath(p,ROOT),'err':'nodesc','pw':w,'ph':h,'size':os.path.getsize(p)}); continue
                    cw,ch,states=parse_desc(desc)
                    out.append({'path':os.path.relpath(p,ROOT),'pw':w,'ph':h,'cw':cw,'ch':ch,'size':os.path.getsize(p),'states':states})
                except Exception as e:
                    out.append({'path':os.path.relpath(p,ROOT),'err':str(e)})
    json.dump(out,open('dmi_all.json','w'))
    print(len(out), sum(1 for o in out if 'err' in o))
