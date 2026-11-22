// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/vermin/cockroach/cockroach.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C4).

// tg: code/modules/mob/living/basic/vermin/cockroach/cockroach.dm:2
public partial class mob_living_basic_cockroach : mob_living_basic
{
    // tg: code/modules/mob/living/basic/vermin/cockroach/cockroach.dm:44
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        turf our_turf = get_turf(this) as turf;
        if (Dm.truthy(SSmapping.level_trait(our_turf.z, ZTRAIT_SNOWSTORM)) || Dm.truthy(SSmapping.level_trait(our_turf.z, ZTRAIT_ICE_RUINS)) || Dm.truthy(SSmapping.level_trait(our_turf.z, ZTRAIT_ICE_RUINS_UNDERGROUND)))
        {
            name = Dm.text("ice-[]", name);
            real_name = name;
            desc = Dm.text("[]<br>This one seems to have a blue tint and has adapted to the cold.", desc);
            minimum_survivable_temperature = 140; // 40kelvin below icebox temp
            Waiting.NoSleep(add_atom_colour("#66ccff", FIXED_COLOUR_PRIORITY));
        }

        object dot = base.Initialize(mapload, __extra);

        Waiting.NoSleep(AddElement(typeof(datum_element_death_drops), typeof(obj_effect_decal_cleanable_insectguts)));
        Waiting.NoSleep(AddElement(typeof(datum_element_swabable), cockroach_cell_line, CELL_VIRUS_TABLE_GENERIC_MOB, 1f, 7f));
        Waiting.NoSleep(AddElement(typeof(datum_element_can_be_held)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_squashable), 50f, 1f, Dm.bitor(Dm.bitor(SQUASHED_SHOULD_BE_GIBBED, SQUASHED_ALWAYS_IF_DEAD), SQUASHED_DONT_SQUASH_IN_CONTENTS)));
        ADD_TRAIT(this, TRAIT_VENTCRAWLER_ALWAYS, INNATE_TRAIT);
        ADD_TRAIT(this, TRAIT_NUKEIMMUNE, INNATE_TRAIT);
        ADD_TRAIT(this, TRAIT_RADIMMUNE, INNATE_TRAIT);
        if (minion_path != null)
        {
            set_pet_commands();
            Waiting.NoSleep(AddElement(typeof(datum_element_regal_rat_minion), minion_path, "hiss", minion_commands));
        }
        return dot;
    }

    /// Set our pet commands
    // tg: code/modules/mob/living/basic/vermin/cockroach/cockroach.dm:77
    public virtual void set_pet_commands()
    {
        minion_commands = GLOB.regal_rat_minion_commands;
    }

    // tg: code/modules/mob/living/basic/vermin/cockroach/cockroach.dm:80
    public override object ex_act(object severity = null, object target = null, params object[] __extra) //Explosions are a terrible way to handle a cockroach.
    {
        return FALSE;
    }

    // tg: code/modules/mob/living/basic/vermin/cockroach/cockroach.dm:83
    public override object get_gibs_type(float? drop_bitflags = null)
    {
        return null;
    }
}

// tg: code/modules/mob/living/basic/vermin/cockroach/cockroach.dm:94
public partial class mob_living_basic_cockroach_bloodroach : mob_living_basic_cockroach
{
    // tg: code/modules/mob/living/basic/vermin/cockroach/cockroach.dm:102
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_death_drops), typeof(obj_effect_decal_cleanable_blood_gibs_old)));
        return dot;
    }

    // tg: code/modules/mob/living/basic/vermin/cockroach/cockroach.dm:106
    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        if (HAS_TRAIT(this, TRAIT_BUGKILLER_DEATH))
            return await base.death(gibbed, __extra);
        foreach (turf messy_turf in Dm.each<turf>(view(this, 2)))
        {
            Dm.@new<obj_effect_decal_cleanable_blood>(messy_turf);
            foreach (mob_living mob_in_turf in Dm.each<mob_living>(messy_turf))
            {
                mob_in_turf.visible_message(span_danger(Dm.text("[] is splattered with blood!", mob_in_turf)), span_userdanger("You're splattered with blood!"));
                await mob_in_turf.add_blood_DNA(Dm.list(Dm.kv("Non-human DNA", random_human_blood_type())));
                await mob_in_turf.add_mood_event("splattered_with_blood", typeof(datum_mood_event_splattered_with_blood));
                playsound(mob_in_turf, Dm.resource("sound/effects/splat.ogg"), 50, TRUE, extrarange: SILENCED_SOUND_EXTRARANGE);
            }
        }
        return await base.death(gibbed, __extra);
    }
}

// tg: code/modules/mob/living/basic/vermin/cockroach/cockroach.dm:120
public partial class mob_living_basic_cockroach_hauberoach : mob_living_basic_cockroach
{
    // tg: code/modules/mob/living/basic/vermin/cockroach/cockroach.dm:138
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_caltrop), 10f, 15f, null, null, Dm.bitor(CALTROP_BYPASS_SHOES, CALTROP_SILENT)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_squashable), 100f, 1f, Dm.bitor(Dm.bitor(SQUASHED_SHOULD_BE_GIBBED, SQUASHED_ALWAYS_IF_DEAD), SQUASHED_DONT_SQUASH_IN_CONTENTS), nameof(on_squish)));
        return dot;
    }

    ///Proc called when a cockroach is squished. Checks if the crossing mob is immune to the spike. If they are, it dies. If not, we just got stepped on.
    // tg: code/modules/mob/living/basic/vermin/cockroach/cockroach.dm:149
    public virtual object on_squish(mob_living cockroach = null, atom_movable crossing = null)
    {
        mob_living living_target = crossing as mob_living;
        if (living_target == null)
            return FALSE; //We failed to run the invoke. Might be because we're a structure. Let the squashable element handle it then!
        if (!HAS_TRAIT(living_target, TRAIT_PIERCEIMMUNE))
        {
            living_target.visible_message(span_danger(Dm.text("[] steps onto []'s spike!", living_target, cockroach)), span_userdanger(Dm.text("You step onto []'s spike!", cockroach)));
            return TRUE;
        }
        living_target.visible_message(span_notice(Dm.text("[] squashes [], not even noticing its spike.", living_target, cockroach)), span_notice(Dm.text("You squashed [], not even noticing its spike.", cockroach)));
        return FALSE;
    }
}

// tg: code/modules/mob/living/basic/vermin/cockroach/cockroach.dm:173
public partial class mob_living_basic_cockroach_glockroach : mob_living_basic_cockroach
{
    // tg: code/modules/mob/living/basic/vermin/cockroach/cockroach.dm:191
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_ranged_attacks), typeof(obj_item_ammo_casing_glockroach), null, null, burst_shots, null, ranged_cooldown));
        if (ranged_cooldown <= 1 * SECONDS)
            Waiting.NoSleep(AddComponent(typeof(datum_component_ranged_mob_full_auto)));
        return dot;
    }

    // tg: code/modules/mob/living/basic/vermin/cockroach/cockroach.dm:201
    public override void set_pet_commands()
    {
        minion_commands = TgState.Current.mob_living_basic_cockroach_glockroach__glockroach_commands ??= Dm.list(typeof(datum_pet_command_idle), typeof(datum_pet_command_free), typeof(datum_pet_command_protect_owner_glockroach), typeof(datum_pet_command_follow), typeof(datum_pet_command_attack_glockroach));
    }
}

public sealed partial class TgState
{
    internal DmList mob_living_basic_cockroach_glockroach__glockroach_commands;
}
