"""Audit original DMI atlases, map vendor states, and create nearest-neighbor references."""
from pathlib import Path
from PIL import Image, ImageDraw
import json,re,hashlib,collections

ROOT=Path(__file__).resolve().parents[2]
TG=ROOT.parent/'tgstation'
OUT=ROOT/'godot/assets/models/station_props'
OUT.mkdir(parents=True,exist_ok=True)
ART=ROOT/'docs/ai/art'
SOURCES={
 'structures':('icons/obj/structures',['rack']),
 'solar':('icons/obj/machines/solar',['sp_base','solar_panel_glass','solar_panel_glass_edge','tracker','tracker_base']),
 'floor':('icons/obj/machines/floor',['holopad0','holopad1','holopad_open','holopad1_mask']),
 'vending':('icons/obj/machines/vending',[]),
 'disposal':('icons/obj/pipes_n_cables/disposal',['disposal','dispover-ready','dispover-handle','dispover-full']),
 'plumbing':('icons/obj/watercloset',['shower','sink','sink_alt','toilet00','toilet10','toilet01','toilet11','toilet-water','mirror','urinal']),
 'chemical_tanks':('icons/obj/medical/chemical_tanks',['water','fuel']),
 'map_objects':('icons/map_icons/objects',['/obj/machinery/portable_atmospherics/canister','/obj/machinery/atmospherics/components/tank']),
 'canister':('icons/obj/pipes_n_cables/canisters',['base','lights','stripe','outline','window-base','can-open','can-connector']),
 'stationary':('icons/obj/pipes_n_cables/stationary_canisters',['canister-0','canister-3','canister-15','canister-255']),
 'stationary_misc':('icons/obj/pipes_n_cables/stationary_canisters_misc',['window-fg','window-bg','knob','frame']),
 'bed':('icons/obj/bed',['bed']),
 'medical_bed':('icons/obj/medical/medical_bed',['emerg_down','emerg_up','brakes_down']),
 'hydroponics':('icons/obj/service/hydroponics/equipment',['hydrotray3','soil']),
 'suit_storage':('icons/obj/machines/suit_storage',['classic','classic_open','classic_suit','classic_helm','classic_storage','classic_lights_closed','classic_lights_open','classic_panel']),
 'engine':('icons/obj/machines/engine/other',['portgen0_0','portgen0_1','portgen1_0','portgen1_1']),
 'robot':('icons/mob/silicon/robots',['robot_old']),
}
parser=(ROOT/'docs/ai/research/10-scripts/dm_types.py').read_text(encoding='utf-8').split('for dp,dn,fn in os.walk')[0]
ns={};exec(parser,ns)
ns['KEEPVARS'].update(['light_mask','panel_type','parent_type'])
for path in sorted((TG/'code/modules/vending').rglob('*.dm')):ns['parse_file'](str(path))
types=ns['types']
def resolve(path,var):
 while path:
  value=types.get(path,{}).get('vars',{}).get(var)
  if value:return value.strip('"\'')
  path=path.rsplit('/',1)[0]
 return None
maptext=(TG/'_maps/map_files/MetaStation/MetaStation.dmm').read_text()
mapped=collections.Counter(re.findall(r'/obj/machinery/vending(?:/[\w]+)*',maptext))
vendors={}
for path,count in sorted(mapped.items()):
 state=resolve(path,'icon_state')
 if state:vendors.setdefault(state,[]).append({'type':path,'dictionary_occurrences':count,'light_mask':resolve(path,'light_mask'),'panel_type':resolve(path,'panel_type')})
# Include all declared machine appearances as well, since the table doesn't enumerate its 39 states.
all_vendor_states=sorted({resolve(path,'icon_state') for path in types if path.startswith('/obj/machinery/vending')} - {None})
vendmeta=json.loads((ROOT/'godot/assets/tg/icons/obj/machines/vending.json').read_text())
vendnames={s['name'] for s in vendmeta['states']}
selected=sorted(set(vendors)&vendnames)
SOURCES['vending']=(SOURCES['vending'][0],selected)
report={'sources':{},'mapped_vending':vendors,'mapped_vendor_states':selected,
        'all_declared_vendor_states':all_vendor_states,'source_map':'_maps/map_files/MetaStation/MetaStation.dmm',
        'count_note':'Dictionary occurrence counts are provenance, not tile instance counts.'}
contact=[]
for key,(rel,wanted) in SOURCES.items():
 png=ROOT/'godot/assets/tg'/(rel+'.png');meta=json.loads(png.with_suffix('.json').read_text())
 dmi=TG/(rel+'.dmi');original=Image.open(dmi);image=Image.open(png).convert('RGBA')
 assert image.tobytes()==original.convert('RGBA').tobytes(),rel
 original_states={};first=0
 for chunk in original.info['Description'].split('state = ')[1:]:
  name=re.match(r'"([^"]*)"',chunk)[1]
  dirs=int(re.search(r'dirs = (\d+)',chunk)[1]) if 'dirs = ' in chunk else 1
  frames=int(re.search(r'frames = (\d+)',chunk)[1]) if 'frames = ' in chunk else 1
  original_states.setdefault(name,[]).append((dirs,frames,first));first+=dirs*frames
 for state in meta['states']:
  assert (state['dirs'],state['frames'],state['first']) in original_states[state['name']],(rel,state['name'],original_states[state['name']],(state['dirs'],state['frames'],state['first']))
  cols=meta['sheetWidth']//meta['width']
  if state['frames']>0:
   order=[state['first']+f*state['dirs']+d for d in range(state['dirs']) for f in range(state['frames'])]
   assert state['rects']==[[(i%cols)*meta['width'],(i//cols)*meta['height'],meta['width'],meta['height']] for i in order],(rel,state['name'],state['first'],state['rects'][:3])
 states={s['name']:s for s in meta['states']}
 report['sources'][key]={'png':str(png.relative_to(ROOT)).replace('\\','/'),'dmi':rel+'.dmi',
  'png_sha256':hashlib.sha256(png.read_bytes()).hexdigest(),'dmi_sha256':hashlib.sha256(dmi.read_bytes()).hexdigest(),
  'tile_px':[meta['width'],meta['height']],'sheet_px':list(image.size),'pixels_equal_dmi':True,
  'states':meta['states'],'selected_states':[s for s in wanted if s in states],
  'missing_requested_states':[s for s in wanted if s not in states]}
 for state in wanted:
  if state not in states:continue
  s=states[state]
  if key=='vending':continue
  contact.append((key,state,image,s['rects'][::s['frames']][:min(s['dirs'],4)]))
def sheet(items,path,columns=4):
 cw,ch=260,180
 result=Image.new('RGB',(cw*columns,ch*((len(items)+columns-1)//columns)),(39,45,54));draw=ImageDraw.Draw(result)
 for i,(key,state,im,rects) in enumerate(items):
  ox=(i%columns)*cw;oy=(i//columns)*ch
  draw.text((ox+5,oy+5),key+': '+state[:29],fill='white')
  for n,r in enumerate(rects):
   size=128 if len(rects)==1 else 60
   sprite=im.crop((r[0],r[1],r[0]+r[2],r[1]+r[3])).resize((size,size),Image.Resampling.NEAREST)
   result.paste(sprite,(ox+5+n*63,oy+30),sprite)
 result.save(path)
sheet(contact,ART/'station_props_source_reference.png')
vi=Image.open(ROOT/'godot/assets/tg/icons/obj/machines/vending.png').convert('RGBA')
sheet([('vendor',s,vi,[next(x for x in vendmeta['states'] if x['name']==s)['rects'][0]]) for s in selected],ART/'vending_source_reference.png',6)
(OUT/'source_audit.json').write_text(json.dumps(report,indent=2)+'\n',encoding='utf-8')
# Resolve the real portable-canister GAGS source, including additive and multiply shaders.
ns['parse_file'](str(TG/'code/modules/atmospherics/machinery/portable/canister.dm'))
can_types=sorted(set(re.findall(r'/obj/machinery/portable_atmospherics/canister(?:/[\w]+)*',maptext)))
configs={'canister':'canister_default.json','canister/base':'canister_base.json',
 'canister/post_effects':'canister_post_effects.json','canister/stripe':'canister_stripe.json',
 'canister/double_stripe':'canister_double_stripe.json','canister/hazard':'canister_hazard.json'}
cans=report['sources']['canister'];can_image=Image.open(ROOT/cans['png']).convert('RGBA')
can_states={s['name']:s for s in cans['states']}
def layer(state,tint=None):
 r=can_states[state]['rects'][0];out=can_image.crop((r[0],r[1],r[0]+32,r[1]+32))
 if tint:
  color=tuple(int(tint[i:i+2],16) for i in (1,3,5))
  out.putdata([tuple(round(p[i]*color[i]/255) for i in range(3))+(p[3],) for p in out.get_flattened_data()])
 return out
def render_config(config,colors,state=''):
 definition=json.loads((TG/'code/datums/greyscale/json_configs'/configs[config]).read_text())
 return render_entries(definition[state],colors)
def render_entries(entries,colors):
 out=Image.new('RGBA',(32,32))
 for entry in entries:
  if isinstance(entry,list):child=render_entries(entry,colors);mode='overlay'
  else:
   chosen=[colors[i-1] for i in entry.get('color_ids',[]) ]
   if entry['type']=='reference':child=render_config(entry['reference_type'].split('/datum/greyscale_config/')[1],chosen)
   else:child=layer(entry['icon_state'],chosen[0] if chosen else None)
   mode=entry.get('blend_mode','overlay')
  if mode=='overlay':out=Image.alpha_composite(out,child)
  elif mode in ('add','multiply','subtract'):
   pixels=[]
   for dest,src in zip(out.get_flattened_data(),child.get_flattened_data()):
    alpha=src[3]/255
    if mode=='add':rgb=[min(255,round(dest[i]+src[i]*alpha)) for i in range(3)]
    elif mode=='subtract':rgb=[max(0,round(dest[i]-src[i]*alpha)) for i in range(3)]
    else:rgb=[round(dest[i]*(1-alpha+alpha*src[i]/255)) for i in range(3)]
    pixels.append(tuple(rgb)+(max(dest[3],src[3]) if mode=='add' else dest[3],))
   out.putdata(pixels)
  else:raise ValueError(mode)
 return out
variants=[]
base_type='/obj/machinery/portable_atmospherics/canister'
for tp in [base_type]+[t for t in can_types if t!=base_type]:
 colors=re.findall(r'#[0-9a-fA-F]{6}',resolve(tp,'greyscale_colors') or '#6b6b80')
 config=(resolve(tp,'greyscale_config') or '/datum/greyscale_config/canister').split('/datum/greyscale_config/')[1]
 variants.append({'name':'generic' if tp==base_type else tp.rsplit('/',1)[1],'type':tp,'config':config,'colors':colors})
derived=Image.new('RGBA',(32,32*len(variants)))
for i,v in enumerate(variants):
 tile=render_config(v['config'],v['colors']);derived.paste(tile,(0,32*i));v['rect']=[0,32*i,32,32]
textures=OUT/'textures';textures.mkdir(exist_ok=True)
derived.save(textures/'portable_canister_gags.png')
(OUT/'canister_gags_variants.json').write_text(json.dumps({'variants':variants,'source_configs':configs,'recipe':'Original GAGS states; byte-domain tint, overlay, add and multiply. No resampling.'},indent=2)+'\n')
# glTF has one base-color RGBA sampler: pack the unmodified base RGB together with
# the original light-mask alpha, since the two original tiles have different UV offsets.
vendor_states={s['name']:s for s in vendmeta['states']}
light_tiles=[]
for state in selected:
 mask=vendors[state][0]['light_mask']
 if mask not in vendor_states:continue
 def crop_vendor(name):
  r=vendor_states[name]['rects'][0]
  return vi.crop((r[0],r[1],r[0]+32,r[1]+32))
 base=crop_vendor(state);mask_image=crop_vendor(mask)
 base.putalpha(mask_image.getchannel('A'))
 light_tiles.append((state,mask,base))
light_atlas=Image.new('RGBA',(32,32*len(light_tiles)))
light_manifest=[]
for i,(state,mask,tile) in enumerate(light_tiles):
 light_atlas.paste(tile,(0,32*i));light_manifest.append({'name':state,'mask':mask,'rect':[0,32*i,32,32]})
light_atlas.save(textures/'vending_light_layers.png')
(OUT/'vending_light_layers.json').write_text(json.dumps({'variants':light_manifest,'recipe':'Original base RGB + original light-mask alpha; first frame; no resampling.'},indent=2)+'\n')
print('Mapped vending states',len(selected),selected)
print('All declared states',len(all_vendor_states))
print('Missing',[(k,v['missing_requested_states']) for k,v in report['sources'].items() if v['missing_requested_states']])
print('Tiles',[(k,v['tile_px']) for k,v in report['sources'].items()])
