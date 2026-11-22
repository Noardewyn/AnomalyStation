import os, re, json, collections, sys
ROOT='/Users/alexey/Desktop/projects/SpaceStation3D/tgstation'
KEEPVARS={'icon','icon_state','name','desc','slot_flags','worn_icon','worn_icon_state','greyscale_config','greyscale_config_worn','greyscale_colors','inhand_icon_state','lefthand_file','righthand_file','w_class','supports_variations_flags','smoothing_flags','smoothing_groups','canSmoothWith','base_icon_state','icon_living','icon_dead','icon_gib','density','anchored','layer','plane','bound_width','bound_height','pixel_x','pixel_y','dir','alpha','color','mouse_opacity'}
types=collections.defaultdict(lambda:{'vars':{}, 'file':None, 'abstract':False})
def strip_comments(text):
    out=[];i=0;n=len(text);instr=None;depth=0
    while i<n:
        c=text[i]
        if depth>0:
            if text.startswith('/*',i): depth+=1;i+=2;continue
            if text.startswith('*/',i): depth-=1;i+=2;continue
            if c=='\n': out.append('\n')
            i+=1;continue
        if instr:
            if c=='\\' and instr!="'": out.append(text[i:i+2]);i+=2;continue
            if c==instr: instr=None
            elif c=='\n' and instr=='"' : pass
            out.append(c);i+=1;continue
        if c in '"\'': instr=c;out.append(c);i+=1;continue
        if text.startswith('//',i):
            while i<n and text[i]!='\n': i+=1
            continue
        if text.startswith('/*',i): depth=1;i+=2;continue
        out.append(c);i+=1
    return ''.join(out)
def balance(s):
    # paren/brace balance outside strings
    b=0;instr=None;i=0
    while i<len(s):
        c=s[i]
        if instr:
            if c=='\\': i+=2;continue
            if c==instr: instr=None
        elif c in '"\'': instr=c
        elif c in '([{': b+=1
        elif c in ')]}': b-=1
        i+=1
    return b
SKIPKW=('var/','var ','proc/','verb/','proc ','verb ','static/','global/','const/','tmp/','/var','/proc','/verb','/static','/global','/const','/tmp')
def parse_file(path):
    try:
        text=open(path,encoding='utf-8',errors='replace').read()
    except: return
    text=strip_comments(text)
    stack=[] # (indent, path)
    skip_indent=None
    cont=0
    for raw in text.split('\n'):
        if not raw.strip(): continue
        if raw.lstrip().startswith('#'): continue
        if cont>0:
            cont+=balance(raw)
            if cont<0: cont=0
            continue
        indent=len(raw)-len(raw.lstrip('\t'))
        # treat leading spaces as tabs (4)
        line=raw.strip()
        if indent==0 and raw.startswith(' '):
            indent=(len(raw)-len(raw.lstrip(' ')))//4
        if skip_indent is not None:
            if indent>skip_indent: 
                continue
            skip_indent=None
        while stack and stack[-1][0]>=indent: stack.pop()
        parent=stack[-1][1] if stack else ''
        b=balance(line)
        # var assignment or declaration
        m=re.match(r'^(/?[A-Za-z_][\w/]*?)\s*=\s*(.*)$',line)
        is_proc = ('(' in line.split('=')[0]) if '=' in line else ('(' in line)
        if is_proc and not ('=' in line and '(' not in line.split('=')[0]):
            # proc or override def
            skip_indent=indent
            if b>0: cont=b
            continue
        if m:
            lhs,rhs=m.group(1),m.group(2)
            comps=[c for c in lhs.split('/') if c]
            if comps and comps[0] in ('var','static','global','const','tmp'):
                if b>0: cont=b
                continue
            # absolute path var assign: /obj/item/foo/icon_state = ...
            if lhs.startswith('/') and len(comps)>1:
                var=comps[-1]; tpath='/'+'/'.join(comps[:-1])
                if 'var' in comps[:-1]: 
                    if b>0: cont=b
                    continue
                if var in KEEPVARS:
                    types[tpath]['vars'][var]=rhs.strip()
                    types[tpath]['file']=types[tpath]['file'] or path
                else:
                    types[tpath]
                    types[tpath]['file']=types[tpath]['file'] or path
                if b>0: cont=b
                continue
            var=comps[-1] if comps else lhs
            if parent:
                if var in KEEPVARS:
                    types[parent]['vars'][var]=rhs.strip()
                if var=='abstract_type': types[parent]['abstract']=True
            if b>0: cont=b
            continue
        # type decl
        if '=' in line: 
            if b>0: cont=b
            continue
        if any(line.startswith(k) for k in ('var/','proc/','verb/','static/','global/','const/','tmp/','if ','if(','else','for ','for(','while','return','set ','switch','do','spawn')):
            if b>0: cont=b
            continue
        comps=[c for c in line.split('/') if c]
        if not comps or not re.match(r'^[A-Za-z_][\w]*$',comps[0]): 
            continue
        if any(c in ('var','proc','verb','static','global','const','tmp') for c in comps):
            if b>0: cont=b
            continue
        if not all(re.match(r'^[\w]+$',c) for c in comps): continue
        if line.startswith('/'): path='/'+'/'.join(comps)
        else: path=(parent+'/'+'/'.join(comps))
        if not path.startswith('/'): continue
        stack.append((indent,path))
        types[path]['file']=types[path]['file'] or path
        types[path]['file']=path if False else types[path]['file']
        if b>0: cont=b
for dp,dn,fn in os.walk(os.path.join(ROOT,'code')):
    for f in fn:
        if f.endswith('.dm'): 
            p=os.path.join(dp,f); 
            before=set(types.keys())
            parse_file(p)
            for k in types:
                if types[k]['file'] is None or (k not in before and types[k]['file']!=p): types[k]['file']=p
# fix: set file properly
out={k:{'vars':v['vars'],'file':os.path.relpath(v['file'],ROOT) if v['file'] and v['file'].startswith('/Users') else v['file'],'abstract':v['abstract']} for k,v in types.items()}
json.dump(out,open('dm_types.json','w'))
print(len(out))
