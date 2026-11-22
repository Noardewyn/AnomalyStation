// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/carbon/human/death.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// PARTIAL: only GLOB.dead_players_during_shift and the human's death translated (M2 P2, scope.md 2e "human": a human dies
// of its damage), and the gib animation (M6 slice 5); gaps K1: the changeling's Drain (an absorbed body husked, its DNA ruined, its
// blood drained). Dust, the death logs' readout and skeletons are M6, M8 and M10.

public partial class datum_controller_global_vars
{
    // tg: code/modules/mob/living/carbon/human/death.dm:1  GLOBAL_LIST_EMPTY(dead_players_during_shift)
    public DmList dead_players_during_shift;

    // tg: code/modules/mob/living/carbon/human/death.dm:1  (GLOBAL_MANAGED(dead_players_during_shift, list()), __DEFINES/_globals.dm)
    public virtual void InitGlobaldead_players_during_shift()
    {
        dead_players_during_shift = Dm.list();
        Dm.append(ref gvars_datum_init_order, "dead_players_during_shift");
    }
}

// tg: code/modules/mob/living/carbon/human/death.dm:5
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/modules/mob/living/carbon/human/death.dm:5  (the M7 supermatter slice: the crystal dusts)
    public override void spawn_dust(float? just_ash = null)
    {
        if (Dm.truthy(just_ash))
        {
            base.spawn_dust(just_ash);
            return;
        }

        Type bone_type = typeof(obj_effect_decal_remains_human);
        if (isplasmaman(this))
            bone_type = typeof(obj_effect_decal_remains_plasma);

        // tg's /obj/effect/decal/remains/human local also holds the plasma remains, its sibling: object (rules 3.4)
        object bones = Dm.@new(bone_type, loc);
        ((atom)bones).pixel_z = -6;
        ((atom)bones).pixel_w = rand(-1, 1);
    }
    // tg: code/modules/mob/living/carbon/human/death.dm:2  (M6 slice 5, gibbing)
    public override void gib_animation()
    {
        Dm.@new<obj_effect_temp_visual_gib_animation>(loc, ((datum_species)dna.species).gib_anim);
    }

    // tg: code/modules/mob/living/carbon/human/death.dm:18
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        object dot = null;
        if (stat == DEAD)
            return dot;
        stop_sound_channel(CHANNEL_HEARTBEAT); // tg: code/modules/mob/living/carbon/human/death.dm:20
        obj_item_organ_heart human_heart = get_organ_slot(ORGAN_SLOT_HEART) as obj_item_organ_heart;
        if (human_heart != null)
            human_heart.beat = BEAT_NONE;
        human_heart?.Stop();

        force_say(Dm.list(""), immediate: TRUE);

        dot = await base.death(gibbed, __extra);

        if (client != null && !HAS_TRAIT(this, TRAIT_SUICIDED) && !Dm.@in(client, GLOB.dead_players_during_shift))
            Dm.append(ref GLOB.dead_players_during_shift, client);

        // :33-41  if(SSticker.HasRoundStarted()) { SSblackbox.ReportDeath(src); log_message(...); if(key) investigate_log(...) }
        // GAP(lead): SSticker's HasRoundStarted, SSblackbox, /atom/proc/log_message and investigate_log are not declared; the
        // death's blackbox entry and logs are admin tooling (M10).
        Dm.Deferred("code/modules/mob/living/carbon/human/death.dm:33", "death: the blackbox entry and the death logs (admins, M10+)");
        to_chat(this, span_warning(I18n.T("You have died. Barring complete bodyloss, you can in most cases be revived by other players. If you do not wish to be brought back, use the \"Do Not Resuscitate\" button at the bottom of your screen.")));
        // :44-45  if(SSlag_switch.measures[DISABLE_DEAD_KEYLOOP] && !client?.holder) to_chat(src, ...ghost movement disabled...)
        // GAP(lead): SSlag_switch is not declared; ghosts are M10.
        Dm.Deferred("code/modules/mob/living/carbon/human/death.dm:44", "death: the lag switch notice about ghost movement (ghosts, M10+)");
        return dot;
    }
}



// tg: code/modules/mob/living/carbon/human/death.dm:46
public partial class mob_living_carbon_human : mob_living_carbon
{
    // tg: code/modules/mob/living/carbon/human/death.dm:46
    public virtual object reagents_readout()
    {
        Dm.NotTranslated("code/modules/mob/living/carbon/human/death.dm:46", "reagents_readout: not in M2 (M6 gib/dust, M10+ logs)");
        return default;
    }

    // tg: code/modules/mob/living/carbon/human/death.dm:59
    public virtual object makeSkeleton()
    {
        Dm.NotTranslated("code/modules/mob/living/carbon/human/death.dm:59", "makeSkeleton: not in M2 (M6 gib/dust, M10+ logs)");
        return default;
    }
}

// tg: code/modules/mob/living/carbon/human/death.dm:66
public partial class mob_living_carbon : mob_living
{
    // tg: code/modules/mob/living/carbon/human/death.dm:66
    public virtual object Drain()
    {
        become_husk(CHANGELING_DRAIN);
        ADD_TRAIT(this, TRAIT_BADDNA, CHANGELING_DRAIN);
        set_blood_volume(0);
        return TRUE;
    }
}
