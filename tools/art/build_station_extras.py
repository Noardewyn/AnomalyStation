"""Source-specific remaining station models. Run in Blender through MCP."""
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
common=(ROOT/'tools/art/build_computers_construction.py').read_text().split('# The medical laptop')[0]
common=common.replace('machines/computers_construction','station_props/station_extras').replace('station_art_computers','station_art_extras')
exec(compile(common,'extras_helpers','exec'))
source_bindings={}

def colour(key,st,target=(.30,.32,.34)):
    im=next(n.image for n in materials[key].node_tree.nodes if n.type=='TEX_IMAGE');W,H=im.size;p=list(im.pixels);r=meta[key]['states'][st]['rects'][0]
    choices=[]
    for y in range(r[3]):
        for x in range(r[2]):
            off=((H-1-r[1]-y)*W+r[0]+x)*4;c=p[off:off+4]
            if c[3]>.99:choices.append((sum((c[i]-target[i])**2 for i in range(3)),x,y))
    _,x,y=min(choices);return (x+.5,y+.5,0,0)

def box(b,bounds,rect=None):
    q=colour(b.key,b.state);b.box(bounds,rect or q,q,q,q)

def post(b,a,c,width,rect=None):beams(b,a,c,width,rect or colour(b.key,b.state))

def disk(b,z,r,height,rect=None,xy=(0,0),n=12):profile(b,[(z,r),(z+height,r)],rect or colour(b.key,b.state),center=(*xy,0),n=n)

def ring(b,z,outer,inner,height,rect=None,xy=(0,0),n=12):
    q=rect or colour(b.key,b.state);base=len(b.v)
    for zz,rr in [(z,outer),(z+height,outer),(z+height,inner),(z,inner)]:
        b.v.extend([(xy[0]+rr*math.cos(j*math.tau/n),xy[1]+rr*math.sin(j*math.tau/n),zz) for j in range(n)])
    for row in range(4):
        for j in range(n):
            b.f.append([base+row*n+j,base+row*n+(j+1)%n,base+((row+1)%4)*n+(j+1)%n,base+((row+1)%4)*n+j]);b.uv.append(uvrect(b.key,b.state,q))

def ellipsoid(b,center,radii,rect,n=10):
    # Closed lathed six-ring volume. End radii remain positive to avoid degenerate poles.
    first=len(b.v);profile(b,[(-1,.03),(-.8,.6),(-.35,.94),(.35,.94),(.8,.6),(1,.03)],rect,n=n)
    for i in range(first,len(b.v)):
        v=b.v[i];b.v[i]=tuple(center[k]+v[k]*radii[k] for k in range(3))

def panel_hinge(key,st,name,bounds,rect,pivot):
    ob=solid(key,st,name,bounds,rect,colour(key,st),origin=pivot)
    motion('maintenance_open',ob,'rotation_euler',[(0,0,0),(-math.pi/2,0,0)],.5)
    motion('maintenance_close',ob,'rotation_euler',[(-math.pi/2,0,0),(0,0,0)],.5)
    return ob

def source_panel(asset,key,states,name,bounds,crop,default=0):
    ob=front(key,states[default],name,*bounds,crop)
    screen_binding(asset,ob,key,states[default],crop,states)
    return ob

def record(name,types,notes):
    root['source_types']=';'.join(types);source_bindings[name]=types
    finish(name,notes)

start('industrial_suit_storage');key='suit';st='industrial';b=Builder(key,st);q=colour(key,st)
box(b,(-.78,.78,-.49,.46,0,.14),(2,29,28,2))
box(b,(-.57,.57,.37,.46,.14,1.75),(7,7,18,22))
for x,rx in [(-.78,2),(.57,27)]:
    box(b,(x,x+.21,-.49,.46,.14,1.91),(rx,1,3,28))
box(b,(-.57,.57,-.49,.46,1.73,1.91),(8,1,17,4));b.finish('Industrial_Cabinet')
back=front(key,'industrial_open','Cabinet_Interior',-.55,.55,.365,.16,1.72,(7,7,18,22))
door=solid(key,st,'Retracting_Shutter',(-.56,.56,-.51,-.44,.15,1.72),(7,7,18,22),q,origin=(0,-.44,1.72))
motion('open',door,'scale',[(1,1,1),(1,1,.035)],.6);motion('close',door,'scale',[(1,1,.035),(1,1,1)],.6)
panel_hinge(key,'industrial_panel','Service_Hatch',(-.72,-.58,-.515,-.49,.15,.43),(1,24,4,6),(-.65,-.49,.43))
source_panel('industrial_suit_storage',key,['industrial_ready','industrial_uv','industrial_locked','industrial_uvstrong'],'Status_Lamp',(-.16,.16,-.514,1.48,1.57),(13,7,6,3))
for name,xyz in [('Suit',(0,.20,.68)),('Helmet',(0,.20,1.40)),('Storage',(0,.20,.22))]:anchor(name,xyz)
record('industrial_suit_storage',['/obj/machinery/suit_storage_unit/industrial'],'Orange reinforced side posts, deep cabinet and separately retracting shutter. Original industrial interior, status UV variants, hatch and content sockets. Suit/helmet/items remain separate gameplay content. UV and sterilisation duration are runtime controlled.')

start('drone_shell_dispenser');key='drone';st='off';b=Builder(key,st)
box(b,(-.76,.76,-.48,.48,0,.18),(3,28,26,3))
for x,rx in [(-.76,3),(.52,25)]:box(b,(x,x+.24,-.48,.48,.18,.97),(rx,16,4,12))
box(b,(-.52,.52,.36,.48,.18,1.05),(10,18,12,10))
box(b,(-.76,.76,-.48,.48,.97,1.61),(3,3,26,11));b.finish('Dispenser_Housing')
solid(key,st,'Output_Tray',(-.48,.48,-.53,.35,.18,.32),(10,25,12,5),colour(key,st))
source_panel('drone_shell_dispenser',key,['off','on','make','recharge'],'Production_Display',(-.48,.48,-.484,.76,.96),(10,16,14,4))
anchor('Drone_Output',(0,-.55,.33))
panel_hinge(key,st,'Material_Hatch',(-.35,.35,-.488,-.47,1.12,1.44),(9,6,11,5),(0,-.47,1.44))
record('drone_shell_dispenser',['/obj/machinery/drone_dispenser'],'Correct drone_dispenser atlas (not the incorrect fax fallback in old inventory). Low production housing and real recessed output tray. off/on/make/recharge bindings retain all original frames and timing; output shells and 3s manufacture/3min recharge handled by game.')

start('weapon_scanner_gate');key='gate';st='scangate';b=Builder(key,st)
for x,rx in [(-.98,1),(.82,28)]:box(b,(x,x+.16,-.15,.15,0,1.80),(rx,6,3,24))
box(b,(-.98,.98,-.19,.19,1.79,1.98),(1,2,30,4));b.finish('Scanner_Arch')
panel_hinge(key,'scangate_open','Service_Hatch',(-.18,.18,-.214,-.19,1.79,1.94),(12,4,7,3),(0,-.19,1.94))
scan=source_panel('weapon_scanner_gate',key,['passive','scanning','alarm'],'Scan_Field',(-.81,.81,-.005,.1,1.78),(3,6,26,24));scan.scale=(0,0,0)
state('idle',scan,False);state('scanning',scan,True);state('alarm',scan,True);anchor('Scan_Volume',(0,0,.9))
record('weapon_scanner_gate',['/obj/machinery/scanner_gate/preset_guns'],'Open 1.64m passage, finished arch, separate service hatch. Transparent scan plane is intentionally not a solid wall. Set scanning/alarm UV state plus visibility clip; detection is runtime.')

start('training_bomb');key='bomb';st='training-bomb-inactive';b=Builder(key,st);q=colour(key,st)
box(b,(-.34,.34,-.28,.28,.04,.72),(11,12,9,9))
for x in [-.47,.33]:
    for z in [.18,.30,.42,.54]:box(b,(x,x+.14,-.17,.17,z,z+.05),(6,13,3,1))
for x in [-.24,-.08,.08,.24]:box(b,(x-.025,x+.025,-.15,.15,.72,.91),(12,7,1,4))
b.finish('Training_Device')
source_panel('training_bomb',key,['training-bomb-inactive','training-bomb-active','training-bomb-inactive-wires','training-bomb-active-wires'],'Training_Display',(-.22,.22,-.284,.21,.59),(12,13,7,7))
anchor('Wire_Panel',(0,-.29,.4))
record('training_bomb',['/obj/machinery/syndicatebomb/training'],'Small source training device with radiating fins and replaceable inactive/active/wires artwork. Gameplay timer and reset are external; no invented explosion animation.')

start('gulag_teleporter');key='chair';st='implantchair_open';b=Builder(key,st)
box(b,(-.72,.72,-.57,.57,0,.14),(3,29,27,2))
box(b,(-.21,.66,.35,.57,.14,1.88),(17,4,12,24))
box(b,(-.29,.72,-.43,.57,1.80,1.94),(12,0,19,3))
box(b,(-.14,.58,-.39,.36,.36,.48),(17,23,11,3))
box(b,(-.14,.58,.25,.36,.48,1.49),(18,9,9,14))
for x in [-.22,.58]:box(b,(x,x+.08,-.35,.35,.48,.79),(12,15,2,9))
box(b,(-.76,-.29,-.52,.17,.14,.75),(1,23,11,7))
for y in [.32,.45]:post(b,(-.62,y,.75),(-.62,y,1.76),.045)
post(b,(-.62,.39,1.76),(.15,.39,1.76),.055);b.finish('Teleporter_Chair_And_Frame')
source_panel('gulag_teleporter',key,['implantchair','implantchair_unpowered'],'Control_Screen',(-.69,-.35,-.524,.42,.69),(2,23,9,6))
# Rounded transparent enclosure, separate from chair and fixed rear wall.
b=Builder(key,'implantchair');ring(b,0,.48,.455,1.56,(18.5,12.5,0,0),n=12)
door=b.finish('Transparent_Canopy',origin=(0,0,0));door.location=(.20,0,.16);alpha(door,.25)
motion('open',door,'scale',[(1,1,1),(1,1,.035)],.6);motion('close',door,'scale',[(1,1,.035),(1,1,1)],.6)
anchor('Occupant',(.20,0,.48));panel_hinge(key,'implantchair_maintenance','Service_Hatch',(-.67,-.37,-.54,-.52,.16,.36),(2,27,8,3),(-.52,-.52,.36))
record('gulag_teleporter',['/obj/machinery/gulag_teleporter'],'Vertical implant-chair enclosure with actual seat, control pedestal and connected overhead frame; separate telescoping transparent canopy. Original powered/unpowered controls, maintenance hatch and Occupant socket; prisoner and teleport effects remain dynamic.')

start('gulag_item_reclaimer');key='wall';st='gulag_off';b=Builder(key,st)
box(b,(-.53,.53,0,.24,.42,1.30),(5,3,22,18));box(b,(-.49,.49,-.12,.24,.18,.42),(6,21,20,7));b.finish('Wall_Terminal')
source_panel('gulag_item_reclaimer',key,['gulag_off','gulag_on'],'Reclaimer_Screen',(-.44,.44,-.004,.62,1.15),(7,8,18,11))
anchor('Wall_Mount',(0,.24,.7));anchor('Item_Output',(-.27,-.16,.28))
record('gulag_item_reclaimer',['/obj/machinery/gulag_item_reclaimer'],'Wall terminal with projecting item slot and keyboard ledge. Off/on screen binding; reclaimed items attach at output, not baked.')

start('monkey_recycler');key='kitchen';st='grinder';b=Builder(key,st)
box(b,(-.63,.63,-.47,.47,.08,.80),(7,18,20,12));box(b,(-.63,.63,.30,.47,.8,1.31),(7,3,20,5))
for x,rx in [(-.63,7),(.48,25)]:box(b,(x,x+.15,-.47,.47,.8,1.31),(rx,5,2,9))
box(b,(-.48,.48,-.47,-.32,.8,.92),(9,14,16,2));b.finish('Recycler_Housing')
for i,x in enumerate([-.18,.18]):
    b=Builder(key,st);profile(b,[(-.30,.14),(.27,.14)],(12,7,7,5),'Y',(x,0,1.02),n=8)
    jaws=b.finish('Grinding_Roller_'+str(i),(x,0,1.02));motion('working',jaws,'rotation_euler',[(0,0,0),(0,math.tau*(-1 if i else 1),0)],1)
panel_hinge(key,'grinder_open','Maintenance_Hatch',(-.22,.22,-.49,-.47,.25,.62),(14,20,6,7),(0,-.47,.62))
anchor('Input',(0,-.2,1.12));anchor('Product_Output',(0,-.51,.23))
record('monkey_recycler',['/obj/machinery/monkey_recycler'],'Open recessed grinding hopper, rollers and service hatch. Source grinder_monkey decoration and input/output anchors; input monkeys/cubes are gameplay content. Roller cycle is a suggested mechanical loop.')

start('basketball_hoop');key='hoop';st='hoop';b=Builder(key,st);q=colour(key,st,(.15,.15,.15))
box(b,(-.23,.23,.24,.65,0,.10),q);post(b,(0,.46,.08),(0,.46,1.65),.07,q)
box(b,(-.58,.58,.34,.43,1.24,1.94),(3,21,25,17));post(b,(0,.35,1.48),(0,-.05,1.48),.04,q);b.finish('Stand_And_Backboard')
b=Builder(key,st);ring(b,1.47,.29,.255,.04,colour(key,st,(.65,.06,.02)),xy=(0,-.24));b.finish('Hoop_Rim')
b=Builder(key,st);net=colour(key,st,(.65,.65,.65))
for j in range(8):
    a=j*math.tau/8;c=(j+1)*math.tau/8
    post(b,(.265*math.cos(a),-.24+.265*math.sin(a),1.47),(.18*math.cos(c),-.24+.18*math.sin(c),1.12),.012,net)
    post(b,(.265*math.cos(c),-.24+.265*math.sin(c),1.47),(.18*math.cos(a),-.24+.18*math.sin(a),1.12),.012,net)
ring(b,1.11,.19,.175,.018,net,xy=(0,-.24),n=8);b.finish('Open_Net');anchor('Ball_Target',(0,-.24,1.5));anchor('Score_Text',(0,.32,1.91))
record('basketball_hoop',['/obj/structure/hoop'],'Source 32x64 standing hoop, adapted below the station 2m ceiling. Real circular rim and open low-poly net. Score is runtime text; basketball separate.')

start('skeleton_model');key='mannequin';st='mannequin_skeleton_male';b=Builder(key,st);bone=colour(key,st,(.72,.72,.45))
for x in [-.13,.13]:
    post(b,(x,-.01,.08),(x,0,.49),.055,bone);post(b,(x,0,.49),(x*.65,0,.89),.075,bone)
    box(b,(x-.045,x+.045,-.19,.04,0,.07),bone)
post(b,(0,0,.82),(0,0,1.41),.045,bone)
for z,w in [(1.08,.17),(1.17,.20),(1.26,.22),(1.34,.19)]:
    for sign in [-1,1]:
        post(b,(0,.025,z),(sign*w,-.01,z+.015),.025,bone);post(b,(sign*w,-.01,z+.015),(sign*w*.75,-.13,z-.035),.025,bone)
for sign in [-1,1]:
    post(b,(0,0,.89),(sign*.16,-.02,.85),.05,bone);post(b,(sign*.16,-.02,.85),(sign*.09,-.08,.75),.05,bone)
    post(b,(0,0,1.38),(sign*.25,0,1.38),.045,bone)
    post(b,(sign*.25,0,1.38),(sign*.32,-.015,1.10),.05,bone);post(b,(sign*.32,-.015,1.10),(sign*.33,-.08,.87),.045,bone)
    box(b,(sign*.33-.035,sign*.33+.035,-.12,-.05,.78,.90),bone)
ellipsoid(b,(0,0,1.60),(.14,.12,.19),bone,n=8);box(b,(-.09,.09,-.11,.07,1.41,1.47),bone);b.finish('Skeleton')
front(key,st,'Skull_Face',-.105,.105,-.116,1.52,1.70,(13,3,6,5));anchor('Headwear',(0,0,1.79));anchor('Clothing',(0,0,1.2))
record('skeleton_model',['/obj/structure/mannequin/skeleton'],'Actual skeletal mannequin atlas, not inherited wooden placeholder. Rounded skull, ribs, pelvis and jointed limb bones. Eyepatch/Hawaiian shirt are source starting equipment and remain independent attachments.')

start('quartermaster_statue');key='statue';st='qm';b=Builder(key,st);gold=colour(key,st,(.65,.52,.03))
disk(b,0,.60,.13,gold,n=8);disk(b,.13,.47,.12,gold,n=8)
for x in [-.19,.19]:box(b,(x-.11,x+.11,-.14,.16,.25,.77),(11 if x<0 else 17,22,4,7))
box(b,(-.29,.29,-.17,.20,.74,1.35),(10,12,12,11))
for sign in [-1,1]:post(b,(sign*.24,0,1.28),(sign*.41,-.01,.94),.14,gold)
box(b,(.36,.65,-.16,.10,.61,1.0),(21,20,4,8))
ellipsoid(b,(0,0,1.51),(.20,.17,.22),(11,5,10,7),n=8)
box(b,(-.25,.25,-.19,.20,1.64,1.72),(10,3,12,3));box(b,(-.17,.17,-.13,.14,1.71,1.82),gold);b.finish('Gold_Statue')
record('quartermaster_statue',['/obj/structure/statue/gold/qm'],'Golden clothed quartermaster with hat, briefcase and stepped octagonal pedestal. Palette and face/coat UVs come from qm sprite; static statue.')

start('snowman');key='statue';st='snowman';b=Builder(key,st);snow=colour(key,st,(.85,.85,.85))
ellipsoid(b,(0,0,.43),(.53,.46,.43),snow,n=10);ellipsoid(b,(0,0,.93),(.39,.35,.33),snow,n=10);ellipsoid(b,(0,0,1.36),(.27,.25,.28),snow,n=10);b.finish('Snow_Body')
b=Builder(key,st);dark=colour(key,st,(.03,.03,.03))
for x in [-.085,.085]:ellipsoid(b,(x,-.229,1.43),(.027,.025,.027),dark,n=6)
for z,y in [(.93,-.34),(1.09,-.29)]:ellipsoid(b,(0,y,z),(.028,.025,.028),dark,n=6)
profile(b,[(.22,.05),(.43,.008)],colour(key,st,(.8,.22,.02)),'Y',(0,0,1.33),n=6);b.finish('Coal_And_Carrot')
record('snowman',['/obj/structure/statue/snow/snowman'],'Three rounded low-poly snow volumes, coal and projecting carrot, palette sampled from original snowman sprite. No melting behaviour baked into asset.')

start('cloning_pod_exhibit');key='cloning';st='pod_0';b=Builder(key,st)
disk(b,0,.62,.18,(6,26,20,4));disk(b,.18,.46,.22,(9,24,14,3));ring(b,.4,.45,.39,.06,(9,23,14,2));b.finish('Exhibit_Base')
b=Builder(key,st);profile(b,[(.44,.41),(1.55,.41),(1.75,.33),(1.84,.18)],(9,3,13,21),n=12);capsule=b.finish('Exhibit_Glass');alpha(capsule,.35)
b=Builder(key,st);post(b,(.34,.20,.4),(.34,.20,1.58),.06);post(b,(-.34,.20,.4),(-.34,.20,1.58),.06);b.finish('Rear_Supports')
record('cloning_pod_exhibit',['/obj/structure/showcase/machinery/cloning_pod'],'Static retired cloning exhibit, not a functional cloning machine. Rounded translucent capsule and low radial pedestal; pod_0 original atlas.')

start('damaged_cryo_exhibit');key='sleeper';st='sleeper-open';b=Builder(key,st)
box(b,(-.55,.55,-.91,.91,0,.25),(7,27,18,4));box(b,(-.48,.48,-.84,.84,.25,.36),(8,22,16,5))
box(b,(-.55,.55,.66,.91,.25,.86),(7,3,18,6))
for x,rx in [(-.55,6),(.45,24)]:box(b,(x,x+.10,-.91,.66,.25,.64),(rx,15,2,13))
b.finish('Open_Sleeper_Base')
solid(key,st,'Recessed_Mattress',(-.44,.44,-.78,.65,.36,.43),(10,18,12,8),colour(key,st,(.09,.1,.12)))
lid=solid(key,st,'Raised_Glass_Lid',(-.48,.48,-.02,.04,0,1.35),(10,7,12,10),colour(key,st,(.1,.5,.6)))
lid.location=(0,.63,.70);lid.rotation_euler.x=math.radians(48);alpha(lid,.32)
anchor('Exhibit_Plaque',(0,-.92,.15))
record('damaged_cryo_exhibit',['/obj/structure/showcase/machinery/oldpod'],'Horizontal old sleeper exhibit, with its source permanently raised glass lid, deep mattress cavity and finished side walls. Static museum prop; no functional treatment loop.')

start('fermenting_barrel');key='structures';st='barrel_open';b=Builder(key,st)
# Continuous lathed outside + inner bore, capped bottom, no invisible open underside.
profile(b,[(0,.37),(.12,.43),(.55,.48),(1.0,.42),(1.12,.37),(1.12,.32),(.16,.35)],(8,10,16,18),n=12);b.finish('Hollow_Barrel')
b=Builder(key,'barrel');iron=colour(key,'barrel',(.35,.35,.32))
for z,r in [(.2,.45),(.85,.46)]:ring(b,z,r+.015,r-.015,.07,iron)
b.finish('Metal_Hoops')
b=Builder(key,'barrel');disk(b,1.12,.37,.04,(9,6,14,6));lid=b.finish('Removable_Lid')
motion('open',lid,'location',[(0,0,0),(0,.4,.15)],.4);motion('close',lid,'location',[(0,.4,.15),(0,0,0)],.4)
anchor('Contents',(0,0,.2));anchor('Label',(0,-.485,.6))
record('fermenting_barrel',['/obj/structure/fermenting_barrel'],'12-sided bulging wooden barrel with real hollow interior, two hoops and removable lid. Labels, fruit and liquid level remain independent content.')

start('meat_spike');key='spike';st='spikeframe';b=Builder(key,st);q=colour(key,st)
for x in [-.65,.65]:
    post(b,(x,0,.07),(x,0,1.84),.065,q);post(b,(x,-.39,.04),(x,.39,.04),.065,q)
post(b,(-.65,0,1.84),(.65,0,1.84),.08,q);post(b,(-.65,0,.08),(.65,0,.08),.045,q);b.finish('Spike_Frame')
b=Builder(key,'spike')
for x in [-.37,-.12,.12,.37]:
    post(b,(x,0,1.80),(x,-.15,1.59),.035,q)
    profile(b,[(.12,.035),(.24,.006)],q,'Y',(x,0,1.59),n=6)
spikes=b.finish('Installed_Spikes');state('frame',spikes,False);state('assembled',spikes,True)
anchor('Buckle_Contents',(0,-.22,.95))
record('meat_spike',['/obj/structure/kitchenspike','/obj/structure/kitchenspike_frame'],'Shared open frame for all three placements. frame/assembled clips toggle actual metal hooks. Attached animals and bloody corpse variants are content, not permanently baked sprites.')

start('ripley_wreckage');key='mecha';st='ripley-broken';b=Builder(key,st);gold=colour(key,st,(.62,.43,.05));dark=colour(key,st,(.07,.08,.07))
for x in [-.39,.39]:
    box(b,(x-.22,x+.22,-.55,.27,0,.15),(8 if x<0 else 19,27,5,3))
    post(b,(x,0,.15),(x*.75,.15,.56),.22,gold)
box(b,(-.47,.47,-.12,.49,.43,1.10),(9,12,14,10));box(b,(-.42,.42,.30,.56,1.08,1.52),(10,4,12,6))
for sign in [-1,1]:post(b,(sign*.42,.22,1.17),(sign*.68,0,.72),.20,gold)
post(b,(-.68,0,.72),(-.80,-.38,.22),.15,dark);post(b,(.68,0,.72),(.80,-.47,.16),.15,gold)
for x in [-.39,.27]:box(b,(x,x+.12,-.18,.31,.94,1.48),gold)
b.finish('Collapsed_Ripley')
front(key,st,'Broken_Cockpit',-.27,.27,-.125,.74,1.24,(12,11,8,9));anchor('Salvage',(0,0,.6))
record('ripley_wreckage',['/obj/structure/mecha_wreckage/ripley'],'Collapsed yellow loader with splayed feet, hanging arms and open/dark cockpit. Uses current ripley-broken, not obsolete ripley-broken-old; static salvage prop.')

# Remaining large devices from the audit; underfloor beacons remain excluded.
start('tachyon_doppler_array');key='research';st='tdoppler';b=Builder(key,st)
box(b,(-.81,.81,-.46,.46,0,.61),(2,22,28,9));box(b,(-.73,-.04,-.30,.24,.61,1.34),(5,4,12,15))
post(b,(.43,.15,.61),(.43,.15,1.36),.09);b.finish('Doppler_Base')
b=Builder(key,st);ellipsoid(b,(.43,.15,1.55),(.29,.26,.26),(20,2,9,8),n=8);b.finish('Directional_Sensor')
source_panel('tachyon_doppler_array',key,['tdoppler','tdoppler_screen-off','tdoppler_screen-broken'],'Monitor',(-.67,-.10,-.304,.89,1.25),(5,5,11,8))
panel_hinge(key,'tdoppler_open','Service_Hatch',(-.68,-.12,-.48,-.46,.15,.47),(4,24,11,6),(-.4,-.46,.47))
anchor('Detection_Direction',(.43,-.5,1.55))
record('tachyon_doppler_array',['/obj/machinery/doppler_array'],'Directional sensor and monitor stand beside each other on a common base, reconstructed from four directional sprites. Power/broken display variants and maintenance hatch. Explosion measurement is runtime.')

start('ectoscopic_sniffer');key='research';st='ecto_sniffer';b=Builder(key,st)
box(b,(-.32,.32,-.25,.25,0,.21),(11,12,11,6));profile(b,[(.21,.12),(.40,.14),(.68,.14),(.83,.07)],(14,2,5,10),n=8);b.finish('Sniffer_Body')
source_panel('ectoscopic_sniffer',key,['ecto_sniffer','ecto_sniffer-p','ecto_sniffer_flick'],'Sensor_Window',(-.075,.075,-.143,.35,.68),(15,4,3,7))
panel_hinge(key,'ecto_sniffer_open','Service_Hatch',(-.16,.16,-.27,-.25,.04,.17),(13,14,6,3),(0,-.25,.17))
record('ectoscopic_sniffer',['/obj/machinery/ecto_sniffer'],'Small upright detector on a low base; glass sensor window keeps original working/off/measurement frames. No enlargement to a human-sized cabinet.')

start('exploration_scanner');key='exploration';st='scanner_off';b=Builder(key,st)
box(b,(-.70,.70,-.58,.58,0,.30),(5,27,23,4));box(b,(-.66,.66,-.52,.52,.30,.78),(5,16,23,11));post(b,(0,0,.78),(0,0,1.11),.17);b.finish('Scanner_Pedestal')
b=Builder(key,st)
# Thick shallow concave dish with its own rear cap, tilted above the pedestal.
profile(b,[(0,.08),(.08,.49),(.20,.57),(.25,.57),(.15,.43),(.10,.06)],colour(key,st,(.55,.55,.63)),n=12)
post(b,(-.37,-.25,.21),(.37,.25,.21),.045,colour(key,st,(.08,.08,.11)))
dish=b.finish('Dish');dish.location=(0,0,1.10);dish.rotation_euler.x=math.radians(35)
source_panel('exploration_scanner',key,['scanner_off','scanner_ready','scanner_on'],'Status_Lamp',(.42,.54,-.524,.37,.46),(24,26,2,2))
panel_hinge(key,'scanner_open','Service_Hatch',(-.38,.38,-.54,-.52,.12,.34),(9,28,13,3),(0,-.52,.34))
anchor('Scan_Direction',(0,-.5,1.4))
record('exploration_scanner',['/obj/machinery/exoscanner'],'Tilted concave 12-sided dish on equipment base, not a flat circular sprite. Original off/ready/on bindings and maintenance panel; interfered/readiness mechanics remain runtime.')

start('fishing_portal_generator');key='fishing';st='portal';b=Builder(key,st)
profile(b,[(0,.69),(.10,.82),(.35,.82),(.43,.73),(.43,.62),(.20,.62)],(2,18,28,11),n=12)
for i,v in enumerate(b.v):b.v[i]=(v[0],v[1]*.70,v[2])
b.finish('Fishing_Portal_Rim')
surface=face(key,'portal_ocean','Destination_Surface',[(-.59,-.40,.205),(.59,-.40,.205),(.59,.40,.205),(-.59,.40,.205)],(5,21,22,5))
screen_binding('fishing_portal_generator',surface,key,'portal_ocean',(5,21,22,5),[s for s in meta[key]['states'] if s.startswith('portal_') and s not in ['portal_open','portal_on','portal_emissive','portal_rift']])
state('off',surface,False);state('on',surface,True)
panel_hinge(key,'portal_open','Service_Hatch',(-.19,.19,-.58,-.56,.07,.23),(14,26,5,5),(0,-.56,.23));anchor('Fishing_Target',(0,0,.43))
record('fishing_portal_generator',['/obj/machinery/fishing_portal_generator'],'Low oval basin-shaped device with finished inner rim. Separate original destination surface supports water/lava/other source variants via UV bindings; off hides surface. Fishing content is runtime.')

start('broadcast_camera');key='camera';st='camera';b=Builder(key,st);wood=colour(key,st,(.3,.10,.03));metal=colour(key,st)
for a in [0,math.tau/3,2*math.tau/3]:post(b,(.37*math.cos(a),.37*math.sin(a),0),(0,0,1.04),.045,wood)
disk(b,1.01,.13,.12,metal,n=8);b.finish('Tripod')
b=Builder(key,st);box(b,(-.20,.20,-.18,.35,1.12,1.47),(12,8,8,9));profile(b,[(.18,.12),(.36,.12),(.40,.09)],colour(key,st,(.07,.07,.07)),'Y',(0,0,1.29),n=8);box(b,(-.17,.17,.02,.31,1.47,1.54),metal);b.finish('Camera_Head')
source_panel('broadcast_camera',key,['camera_on','camera_off'],'Recording_Lamp',(.12,.18,-.184,1.38,1.44),(17,10,1,1));anchor('Broadcast_Target',(0,-2,1.3))
record('broadcast_camera',['/obj/machinery/greenscreen_camera'],'Directional captain-caster on a connected wooden tripod. Low-poly lens projects forward; on/off lamp binds original sprites. Video feed is runtime content.')

start('restaurant_portal');key='portal';st='portal';b=Builder(key,st)
for x,rx in [(-.78,4),(.64,25)]:box(b,(x,x+.14,-.21,.21,0,1.93),(rx,2,2,29))
box(b,(-.78,.78,-.21,.21,1.55,1.98),(5,1,22,6));box(b,(-.78,.78,-.21,.21,0,.1),(5,30,22,2));b.finish('Portal_Frame')
surface=front(key,st,'Portal_Surface',-.63,.63,0,.11,1.54,(6,8,19,22));alpha(surface,.65)
b=Builder(key,'portal_door')
for i in range(8):box(b,(-.62,.62,-.03,.04,.14+i*.17,.20+i*.17),(8,9+i*2,16,1))
shutter=b.finish('Closed_Venue_Shutter');state('venue_open',shutter,False);state('venue_closed',shutter,True)
panel_hinge(key,'portal-open','Service_Hatch',(.78,.89,-.21,-.19,.74,1.05),(28,16,3,5),(.83,-.19,1.05))
anchor('Passage',(0,0,.1))
record('restaurant_portal',['/obj/machinery/restaurant_portal/bar','/obj/machinery/restaurant_portal/restaurant'],'Shared bar/restaurant robot gate with source blue portal and slatted closed-venue overlay as separate geometry. Venue opening toggles shutter; robot routing and access remain runtime.')

start('television_exhibit');key='tv';st='television';b=Builder(key,st)
box(b,(-.57,.57,-.31,.34,.15,1.03),(4,6,24,20))
for x in [-.43,.36]:box(b,(x,x+.07,-.17,.20,0,.15),(6,26,2,3))
post(b,(.27,.10,1.03),(.38,.10,1.30),.025);post(b,(.38,.10,1.30),(.15,.10,1.32),.025);b.finish('Wooden_TV')
source_panel('television_exhibit',key,['television','television_broken'],'TV_Screen',(-.43,.18,-.314,.38,.87),(7,13,13,11))
record('television_exhibit',['/obj/structure/showcase/machinery/tv'],'Small vintage wooden television with side knobs, legs and antenna. Original screen/broken-screen binding; moving newsfeed is optional runtime content.')

(OUT/'models.json').write_text(json.dumps(report,indent=2))
(OUT/'animation_clips.json').write_text(json.dumps(states_report,indent=2))
(OUT/'atlas_bindings.json').write_text(json.dumps(bindings,indent=2))
(OUT/'source_types.json').write_text(json.dumps(source_bindings,indent=2))
preview=bpy.data.scenes.new('Station_Extras_Review');bpy.context.window.scene=preview
for index,(name,(_,objects)) in enumerate(assets.items()):
    for source in objects:
        ob=source.copy();ob.animation_data_clear();ob.parent=None;preview.collection.objects.link(ob);ob.location=source.location+Vector(((index%6)*3,(index//6)*3.2,0))
world=bpy.data.worlds.new('Extras_World');world.use_nodes=True;preview.world=world
next(n for n in world.node_tree.nodes if n.type=='BACKGROUND').inputs['Strength'].default_value=.65
target=Vector((7.5,4.8,.8))
for loc,power in [((1,-5,12),3000),((14,8,12),3000)]:
    ld=bpy.data.lights.new('Review_Area','AREA');ld.energy=power;ld.size=10;ob=bpy.data.objects.new('Review_Area',ld);preview.collection.objects.link(ob);ob.location=loc;ob.rotation_euler=(target-ob.location).to_track_quat('-Z','Y').to_euler()
cd=bpy.data.cameras.new('Review_Camera');cd.type='ORTHO';cd.ortho_scale=22
cam=bpy.data.objects.new('Review_Camera',cd);preview.collection.objects.link(cam);cam.location=target+Vector((6,-16,13));cam.rotation_euler=(target-cam.location).to_track_quat('-Z','Y').to_euler();preview.camera=cam
preview.render.engine='CYCLES';preview.cycles.samples=24;preview.render.resolution_x=2100;preview.render.resolution_y=1450;preview.render.resolution_percentage=100
bpy.data.libraries.write(str(OUT/'station_extras_review.blend'),{preview},path_remap='RELATIVE',fake_user=True,compress=True)
bpy.app.driver_namespace['station_extras_build']=globals().copy()
preview.render.filepath=str(REVIEW/'preview.png');bpy.ops.render.render(write_still=True)
result={'models':{k:v['triangles'] for k,v in report.items()},'preview':str(REVIEW/'preview.png')}
