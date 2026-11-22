// Translated from tgstation (snapshot 1d99bd7f): code/_globalvars/lists/ambience.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (W4.0 contracts: /area/Initialize reads GLOB.ambience_assoc, areas.dm:198).

public partial class datum_controller_global_vars
{
    // tg: code/_globalvars/lists/ambience.dm:1  GLOBAL_LIST_INIT(generic_ambience, list(...))
    public DmList generic_ambience;

    // tg: code/_globalvars/lists/ambience.dm:1  (GLOBAL_MANAGED(generic_ambience, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalgeneric_ambience()
    {
        generic_ambience = Dm.list(
            Dm.resource("sound/ambience/general/ambigen1.ogg"),
            Dm.resource("sound/ambience/general/ambigen2.ogg"),
            Dm.resource("sound/ambience/general/ambigen3.ogg"),
            Dm.resource("sound/ambience/general/ambigen4.ogg"),
            Dm.resource("sound/ambience/general/ambigen5.ogg"),
            Dm.resource("sound/ambience/general/ambigen6.ogg"),
            Dm.resource("sound/ambience/general/ambigen7.ogg"),
            Dm.resource("sound/ambience/general/ambigen8.ogg"),
            Dm.resource("sound/ambience/general/ambigen9.ogg"),
            Dm.resource("sound/ambience/general/ambigen10.ogg"),
            Dm.resource("sound/ambience/general/ambigen11.ogg"),
            Dm.resource("sound/ambience/general/ambigen13.ogg"),
            Dm.resource("sound/ambience/general/ambigen14.ogg"));
        Dm.append(ref gvars_datum_init_order, "generic_ambience");
    }

    // tg: code/_globalvars/lists/ambience.dm:17  GLOBAL_LIST_INIT(holy_ambience, list(...))
    public DmList holy_ambience;

    // tg: code/_globalvars/lists/ambience.dm:17  (GLOBAL_MANAGED(holy_ambience, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalholy_ambience()
    {
        holy_ambience = Dm.list(
            Dm.resource("sound/ambience/holy/ambicha1.ogg"),
            Dm.resource("sound/ambience/holy/ambicha2.ogg"),
            Dm.resource("sound/ambience/holy/ambicha3.ogg"),
            Dm.resource("sound/ambience/holy/ambicha4.ogg"),
            Dm.resource("sound/ambience/holy/ambiholy.ogg"),
            Dm.resource("sound/ambience/holy/ambiholy2.ogg"),
            Dm.resource("sound/ambience/holy/ambiholy3.ogg"));
        Dm.append(ref gvars_datum_init_order, "holy_ambience");
    }

    // tg: code/_globalvars/lists/ambience.dm:27  GLOBAL_LIST_INIT(danger_ambience, list(...))
    public DmList danger_ambience;

    // tg: code/_globalvars/lists/ambience.dm:27  (GLOBAL_MANAGED(danger_ambience, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobaldanger_ambience()
    {
        danger_ambience = Dm.list(
            Dm.resource("sound/ambience/misc/ambidanger.ogg"),
            Dm.resource("sound/ambience/misc/ambidanger2.ogg"));
        Dm.append(ref gvars_datum_init_order, "danger_ambience");
    }

    // tg: code/_globalvars/lists/ambience.dm:32  GLOBAL_LIST_INIT(ruins_ambience, list(...))
    public DmList ruins_ambience;

    // tg: code/_globalvars/lists/ambience.dm:32  (GLOBAL_MANAGED(ruins_ambience, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalruins_ambience()
    {
        ruins_ambience = Dm.list(
            Dm.resource("sound/ambience/lavaland/ambicave.ogg"),
            Dm.resource("sound/ambience/misc/ambidanger.ogg"),
            Dm.resource("sound/ambience/misc/ambidanger2.ogg"),
            Dm.resource("sound/ambience/maintenance/ambimaint1.ogg"),
            Dm.resource("sound/ambience/ruin/ambimine.ogg"),
            Dm.resource("sound/ambience/misc/ambimystery.ogg"),
            Dm.resource("sound/ambience/ruin/ambiruin.ogg"),
            Dm.resource("sound/ambience/ruin/ambiruin2.ogg"),
            Dm.resource("sound/ambience/ruin/ambiruin3.ogg"),
            Dm.resource("sound/ambience/ruin/ambiruin4.ogg"),
            Dm.resource("sound/ambience/ruin/ambiruin5.ogg"),
            Dm.resource("sound/ambience/ruin/ambiruin6.ogg"),
            Dm.resource("sound/ambience/ruin/ambiruin7.ogg"),
            Dm.resource("sound/ambience/engineering/ambitech3.ogg"));
        Dm.append(ref gvars_datum_init_order, "ruins_ambience");
    }

    // tg: code/_globalvars/lists/ambience.dm:49  GLOBAL_LIST_INIT(engi_ambience, list(...))
    public DmList engi_ambience;

    // tg: code/_globalvars/lists/ambience.dm:49  (GLOBAL_MANAGED(engi_ambience, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalengi_ambience()
    {
        engi_ambience = Dm.list(
            Dm.resource("sound/ambience/engineering/ambiatmos.ogg"),
            Dm.resource("sound/ambience/engineering/ambiatmos2.ogg"),
            Dm.resource("sound/ambience/engineering/ambisin1.ogg"),
            Dm.resource("sound/ambience/engineering/ambisin2.ogg"),
            Dm.resource("sound/ambience/engineering/ambisin3.ogg"),
            Dm.resource("sound/ambience/engineering/ambisin4.ogg"),
            Dm.resource("sound/ambience/engineering/ambitech.ogg"),
            Dm.resource("sound/ambience/engineering/ambitech2.ogg"),
            Dm.resource("sound/ambience/engineering/ambitech3.ogg"));
        Dm.append(ref gvars_datum_init_order, "engi_ambience");
    }

    // tg: code/_globalvars/lists/ambience.dm:61  GLOBAL_LIST_INIT(mining_ambience, list(...))
    public DmList mining_ambience;

    // tg: code/_globalvars/lists/ambience.dm:61  (GLOBAL_MANAGED(mining_ambience, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmining_ambience()
    {
        mining_ambience = Dm.list(
            Dm.resource("sound/ambience/lavaland/ambicave.ogg"),
            Dm.resource("sound/ambience/misc/ambidanger.ogg"),
            Dm.resource("sound/ambience/misc/ambidanger2.ogg"),
            Dm.resource("sound/ambience/lavaland/ambilava1.ogg"),
            Dm.resource("sound/ambience/lavaland/ambilava2.ogg"),
            Dm.resource("sound/ambience/lavaland/ambilava3.ogg"),
            Dm.resource("sound/ambience/maintenance/ambimaint1.ogg"),
            Dm.resource("sound/ambience/ruin/ambimine.ogg"),
            Dm.resource("sound/ambience/ruin/ambiruin.ogg"),
            Dm.resource("sound/ambience/ruin/ambiruin2.ogg"),
            Dm.resource("sound/ambience/ruin/ambiruin3.ogg"),
            Dm.resource("sound/ambience/ruin/ambiruin4.ogg"),
            Dm.resource("sound/ambience/ruin/ambiruin5.ogg"),
            Dm.resource("sound/ambience/ruin/ambiruin6.ogg"),
            Dm.resource("sound/ambience/ruin/ambiruin7.ogg"));
        Dm.append(ref gvars_datum_init_order, "mining_ambience");
    }

    // tg: code/_globalvars/lists/ambience.dm:79  GLOBAL_LIST_INIT(icemoon_ambience, list(...))
    public DmList icemoon_ambience;

    // tg: code/_globalvars/lists/ambience.dm:79  (GLOBAL_MANAGED(icemoon_ambience, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalicemoon_ambience()
    {
        icemoon_ambience = Dm.list(
            Dm.resource("sound/ambience/icemoon/ambiicetheme.ogg"),
            Dm.resource("sound/ambience/icemoon/ambiicemelody1.ogg"),
            Dm.resource("sound/ambience/icemoon/ambiicemelody2.ogg"),
            Dm.resource("sound/ambience/icemoon/ambiicemelody3.ogg"),
            Dm.resource("sound/ambience/icemoon/ambiicemelody4.ogg"),
            Dm.resource("sound/ambience/icemoon/ambiicesting1.ogg"),
            Dm.resource("sound/ambience/icemoon/ambiicesting2.ogg"),
            Dm.resource("sound/ambience/icemoon/ambiicesting3.ogg"),
            Dm.resource("sound/ambience/icemoon/ambiicesting4.ogg"),
            Dm.resource("sound/ambience/icemoon/ambiicesting5.ogg"));
        Dm.append(ref gvars_datum_init_order, "icemoon_ambience");
    }

    // tg: code/_globalvars/lists/ambience.dm:92  GLOBAL_LIST_INIT(medical_ambience, list(...))
    public DmList medical_ambience;

    // tg: code/_globalvars/lists/ambience.dm:92  (GLOBAL_MANAGED(medical_ambience, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmedical_ambience()
    {
        medical_ambience = Dm.list(
            Dm.resource("sound/ambience/medical/ambinice.ogg"));
        Dm.append(ref gvars_datum_init_order, "medical_ambience");
    }

    // tg: code/_globalvars/lists/ambience.dm:96  GLOBAL_LIST_INIT(virology_ambience, list(...))
    public DmList virology_ambience;

    // tg: code/_globalvars/lists/ambience.dm:96  (GLOBAL_MANAGED(virology_ambience, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalvirology_ambience()
    {
        virology_ambience = Dm.list(
            Dm.resource("sound/ambience/medical/ambiviro.ogg"),
            Dm.resource("sound/ambience/medical/ambiviro1.ogg"),
            Dm.resource("sound/ambience/medical/ambiviro2.ogg"));
        Dm.append(ref gvars_datum_init_order, "virology_ambience");
    }

    // tg: code/_globalvars/lists/ambience.dm:102  GLOBAL_LIST_INIT(spooky_ambience, list(...))
    public DmList spooky_ambience;

    // tg: code/_globalvars/lists/ambience.dm:102  (GLOBAL_MANAGED(spooky_ambience, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalspooky_ambience()
    {
        spooky_ambience = Dm.list(
            Dm.resource("sound/ambience/medical/ambimo1.ogg"),
            Dm.resource("sound/ambience/medical/ambimo2.ogg"),
            Dm.resource("sound/ambience/misc/ambimystery.ogg"),
            Dm.resource("sound/ambience/misc/ambiodd.ogg"),
            Dm.resource("sound/ambience/ruin/ambiruin6.ogg"),
            Dm.resource("sound/ambience/ruin/ambiruin7.ogg"));
        Dm.append(ref gvars_datum_init_order, "spooky_ambience");
    }

    // tg: code/_globalvars/lists/ambience.dm:111  GLOBAL_LIST_INIT(space_ambience, list(...))
    public DmList space_ambience;

    // tg: code/_globalvars/lists/ambience.dm:111  (GLOBAL_MANAGED(space_ambience, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalspace_ambience()
    {
        space_ambience = Dm.list(
            Dm.resource("sound/ambience/engineering/ambiatmos.ogg"),
            Dm.resource("sound/ambience/space/ambispace.ogg"),
            Dm.resource("sound/ambience/space/ambispace2.ogg"),
            Dm.resource("sound/ambience/space/ambispace3.ogg"),
            Dm.resource("sound/ambience/space/ambispace4.ogg"),
            Dm.resource("sound/ambience/space/ambispace5.ogg"),
            Dm.resource("sound/ambience/space/ambispace6.ogg"));
        // DV574: tg's last entry, sound/music/lobby_music/title2.ogg (a Robocop theme remix), has no licence and is not shipped (D344)
        Dm.append(ref gvars_datum_init_order, "space_ambience");
    }

    // tg: code/_globalvars/lists/ambience.dm:122  GLOBAL_LIST_INIT(maint_ambience, list(...))
    public DmList maint_ambience;

    // tg: code/_globalvars/lists/ambience.dm:122  (GLOBAL_MANAGED(maint_ambience, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalmaint_ambience()
    {
        maint_ambience = Dm.list(
            Dm.resource("sound/ambience/maintenance/ambimaint1.ogg"),
            Dm.resource("sound/ambience/maintenance/ambimaint2.ogg"),
            Dm.resource("sound/ambience/maintenance/ambimaint3.ogg"),
            Dm.resource("sound/ambience/maintenance/ambimaint4.ogg"),
            Dm.resource("sound/ambience/maintenance/ambimaint5.ogg"),
            Dm.resource("sound/ambience/maintenance/ambimaint6.ogg"),
            Dm.resource("sound/ambience/maintenance/ambimaint7.ogg"),
            Dm.resource("sound/ambience/maintenance/ambimaint8.ogg"),
            Dm.resource("sound/ambience/maintenance/ambimaint9.ogg"),
            Dm.resource("sound/ambience/maintenance/ambimaint10.ogg"),
            Dm.resource("sound/ambience/maintenance/ambimaint11.ogg"),
            Dm.resource("sound/ambience/maintenance/ambimaint12.ogg"),
            Dm.resource("sound/ambience/engineering/ambitech2.ogg"),
            Dm.resource("sound/mobs/non-humanoids/hiss/lowHiss1.ogg"),
            Dm.resource("sound/mobs/non-humanoids/hiss/lowHiss2.ogg"),
            Dm.resource("sound/mobs/non-humanoids/hiss/lowHiss3.ogg"),
            Dm.resource("sound/mobs/non-humanoids/hiss/lowHiss4.ogg"),
            Dm.resource("sound/ambience/maintenance/maintambience.ogg"));
        Dm.append(ref gvars_datum_init_order, "maint_ambience");
    }

    // tg: code/_globalvars/lists/ambience.dm:143  GLOBAL_LIST_INIT(away_ambience, list(...))
    public DmList away_ambience;

    // tg: code/_globalvars/lists/ambience.dm:143  (GLOBAL_MANAGED(away_ambience, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalaway_ambience()
    {
        away_ambience = Dm.list(
            Dm.resource("sound/ambience/engineering/ambiatmos.ogg"),
            Dm.resource("sound/ambience/engineering/ambiatmos2.ogg"),
            Dm.resource("sound/ambience/misc/ambidanger.ogg"),
            Dm.resource("sound/ambience/misc/ambidanger2.ogg"),
            Dm.resource("sound/ambience/maintenance/ambimaint.ogg"),
            Dm.resource("sound/ambience/misc/ambiodd.ogg"),
            Dm.resource("sound/ambience/ruin/ambiruin.ogg"),
            Dm.resource("sound/ambience/ruin/ambiruin2.ogg"),
            Dm.resource("sound/ambience/ruin/ambiruin3.ogg"),
            Dm.resource("sound/ambience/ruin/ambiruin4.ogg"),
            Dm.resource("sound/ambience/ruin/ambiruin5.ogg"),
            Dm.resource("sound/ambience/ruin/ambiruin6.ogg"),
            Dm.resource("sound/ambience/ruin/ambiruin7.ogg"),
            Dm.resource("sound/ambience/engineering/ambitech.ogg"),
            Dm.resource("sound/ambience/engineering/ambitech2.ogg"));
        Dm.append(ref gvars_datum_init_order, "away_ambience");
    }

    // tg: code/_globalvars/lists/ambience.dm:161  GLOBAL_LIST_INIT(reebe_ambience, list(...))
    public DmList reebe_ambience;

    // tg: code/_globalvars/lists/ambience.dm:161  (GLOBAL_MANAGED(reebe_ambience, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalreebe_ambience()
    {
        reebe_ambience = Dm.list(
            Dm.resource("sound/ambience/misc/ambireebe1.ogg"),
            Dm.resource("sound/ambience/misc/ambireebe2.ogg"),
            Dm.resource("sound/ambience/misc/ambireebe3.ogg"));
        Dm.append(ref gvars_datum_init_order, "reebe_ambience");
    }

    // tg: code/_globalvars/lists/ambience.dm:167  GLOBAL_LIST_INIT(creepy_ambience, list(...))
    public DmList creepy_ambience;

    // tg: code/_globalvars/lists/ambience.dm:167  (GLOBAL_MANAGED(creepy_ambience, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalcreepy_ambience()
    {
        creepy_ambience = Dm.list(
            Dm.resource("sound/effects/ghost.ogg"),
            Dm.resource("sound/effects/ghost2.ogg"),
            Dm.resource("sound/effects/heart_beat.ogg"),
            Dm.resource("sound/effects/screech.ogg"),
            Dm.resource("sound/effects/hallucinations/behind_you1.ogg"),
            Dm.resource("sound/effects/hallucinations/behind_you2.ogg"),
            Dm.resource("sound/effects/hallucinations/far_noise.ogg"),
            Dm.resource("sound/effects/hallucinations/growl1.ogg"),
            Dm.resource("sound/effects/hallucinations/growl2.ogg"),
            Dm.resource("sound/effects/hallucinations/growl3.ogg"),
            Dm.resource("sound/effects/hallucinations/i_see_you1.ogg"),
            Dm.resource("sound/effects/hallucinations/i_see_you2.ogg"),
            Dm.resource("sound/effects/hallucinations/im_here1.ogg"),
            Dm.resource("sound/effects/hallucinations/im_here2.ogg"),
            Dm.resource("sound/effects/hallucinations/look_up1.ogg"),
            Dm.resource("sound/effects/hallucinations/look_up2.ogg"),
            Dm.resource("sound/effects/hallucinations/over_here1.ogg"),
            Dm.resource("sound/effects/hallucinations/over_here2.ogg"),
            Dm.resource("sound/effects/hallucinations/over_here3.ogg"),
            Dm.resource("sound/effects/hallucinations/turn_around1.ogg"),
            Dm.resource("sound/effects/hallucinations/turn_around2.ogg"),
            Dm.resource("sound/effects/hallucinations/veryfar_noise.ogg"),
            Dm.resource("sound/effects/hallucinations/wail.ogg"));
        Dm.append(ref gvars_datum_init_order, "creepy_ambience");
    }

    // tg: code/_globalvars/lists/ambience.dm:193  GLOBAL_LIST_INIT(ambience_assoc, list(...))
    public DmList ambience_assoc;

    // tg: code/_globalvars/lists/ambience.dm:193  (GLOBAL_MANAGED(ambience_assoc, list(...)), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalambience_assoc()
    {
        ambience_assoc = Dm.list(
            Dm.kv(AMBIENCE_AWAY, GLOB.away_ambience),
            Dm.kv(AMBIENCE_CREEPY, GLOB.creepy_ambience),
            Dm.kv(AMBIENCE_DANGER, GLOB.danger_ambience),
            Dm.kv(AMBIENCE_ENGI, GLOB.engi_ambience),
            Dm.kv(AMBIENCE_GENERIC, GLOB.generic_ambience),
            Dm.kv(AMBIENCE_HOLY, GLOB.holy_ambience),
            Dm.kv(AMBIENCE_MAINT, GLOB.maint_ambience),
            Dm.kv(AMBIENCE_MEDICAL, GLOB.medical_ambience),
            Dm.kv(AMBIENCE_VIROLOGY, GLOB.virology_ambience),
            Dm.kv(AMBIENCE_MINING, GLOB.mining_ambience),
            Dm.kv(AMBIENCE_ICEMOON, GLOB.icemoon_ambience),
            Dm.kv(AMBIENCE_REEBE, GLOB.reebe_ambience),
            Dm.kv(AMBIENCE_RUINS, GLOB.ruins_ambience),
            Dm.kv(AMBIENCE_SPACE, GLOB.space_ambience),
            Dm.kv(AMBIENCE_SPOOKY, GLOB.spooky_ambience));
        Dm.append(ref gvars_datum_init_order, "ambience_assoc");
    }
}
