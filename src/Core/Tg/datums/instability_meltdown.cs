// Translated from tgstation (snapshot 1d99bd7f): code/datums/instability_meltdown.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3, D327): the meltdowns of a body whose DNA lost all stability. The effects that need a mechanic the port has
// not got yet are recorded leaves (not-translated section 165): the corgi and crab transformations, the paraplegic's
// wheelchair (its trauma came with gaps B1; the decloning and gastrolosis diseases came with gaps VI1), the slime mutation toxin, the skeleton species and the
// staff of animation's mimic (the petrified statue came with gaps ST5). The var blocks are
// generated (D111).

/// A possible genetic meltdown that occurs when someone exceeds 100 genetic instability
// tg: code/datums/instability_meltdown.dm:2
public partial class datum_instability_meltdown : datum
{
    /// Code that runs when this meltdown is picked
    // tg: code/datums/instability_meltdown.dm:11
    public virtual async DmTask meltdown(mob_living_carbon_human victim = null)
    {
        await DmTask.CompletedTask;
    }
}

// Nonfatal meltdowns

/// Turns you into a monkey
// tg: code/datums/instability_meltdown.dm:17
public partial class datum_instability_meltdown_monkey : datum_instability_meltdown
{
    // tg: code/datums/instability_meltdown.dm:19
    public override async DmTask meltdown(mob_living_carbon_human victim = null)
    {
        await DmTask.CompletedTask;
        victim.monkeyize();
    }
}

/// Gives you brain trauma that makes your legs disfunctional and gifts you a wheelchair
// tg: code/datums/instability_meltdown.dm:23
public partial class datum_instability_meltdown_paraplegic : datum_instability_meltdown
{
    // tg: code/datums/instability_meltdown.dm:25
    public override async DmTask meltdown(mob_living_carbon_human victim = null)
    {
        await victim.gain_trauma(typeof(datum_brain_trauma_severe_paralysis_paraplegic));
        // new /obj/vehicle/ridden/wheelchair(get_turf(victim)): the wheelchair waits for the ridden vehicles (vehicles; inert)
        Dm.Deferred("code/datums/instability_meltdown.dm:27", "paraplegic meltdown: the wheelchair (the ridden vehicles)");
        to_chat(victim, span_warning(I18n.T("My flesh turned into a wheelchair and I can't feel my legs.")));
    }
}

/// Turns you into a corgi
// tg: code/datums/instability_meltdown.dm:31
public partial class datum_instability_meltdown_corgi : datum_instability_meltdown
{
    // tg: code/datums/instability_meltdown.dm:33
    public override async DmTask meltdown(mob_living_carbon_human victim = null)
    {
        await DmTask.CompletedTask;
        // victim.corgize()
        Dm.Deferred("code/datums/instability_meltdown.dm:34", "corgi meltdown: corgize (the transformation procs)");
    }
}

/// Does nothing
// tg: code/datums/instability_meltdown.dm:37
public partial class datum_instability_meltdown_alright : datum_instability_meltdown
{
    // tg: code/datums/instability_meltdown.dm:39
    public override async DmTask meltdown(mob_living_carbon_human victim = null)
    {
        await DmTask.CompletedTask;
        to_chat(victim, span_notice(I18n.T("Oh, I actually feel quite alright!")));
    }
}

/// Gives you the same text as above but now when you're hit you take 200 times more damage
// tg: code/datums/instability_meltdown.dm:43
public partial class datum_instability_meltdown_not_alright : datum_instability_meltdown
{
    // tg: code/datums/instability_meltdown.dm:45
    public override async DmTask meltdown(mob_living_carbon_human victim = null)
    {
        await DmTask.CompletedTask;
        to_chat(victim, span_notice(I18n.T("Oh, I actually feel quite alright!")));
        victim.damage_resistance -= 20000;
        victim.log_message(Dm.text("has received x200 damage multiplier from [] genetic meltdown", type));
    }
}

/// Turns you into a slime
// tg: code/datums/instability_meltdown.dm:51
public partial class datum_instability_meltdown_slime : datum_instability_meltdown
{
    // tg: code/datums/instability_meltdown.dm:53
    public override async DmTask meltdown(mob_living_carbon_human victim = null)
    {
        await DmTask.CompletedTask;
        to_chat(victim, span_notice(I18n.T("Oh, I actually feel quite alright!")));
        // victim.reagents.add_reagent(/datum/reagent/aslimetoxin, 10)
        Dm.Deferred("code/datums/instability_meltdown.dm:55", "slime meltdown: the advanced mutation toxin (the slime people)");
    }
}

/// Makes you phase through walls into a random direction
// tg: code/datums/instability_meltdown.dm:58
public partial class datum_instability_meltdown_yeet : datum_instability_meltdown
{
    // tg: code/datums/instability_meltdown.dm:60
    public override async DmTask meltdown(mob_living_carbon_human victim = null)
    {
        await victim.apply_status_effect(typeof(datum_status_effect_go_away));
    }
}

/// Makes you take cell damage and gibs you after some time
// tg: code/datums/instability_meltdown.dm:64
public partial class datum_instability_meltdown_decloning : datum_instability_meltdown
{
    // tg: code/datums/instability_meltdown.dm:66
    public override async DmTask meltdown(mob_living_carbon_human victim = null)
    {
        to_chat(this, span_notice(I18n.T("Oh, I actually feel quite alright!"))); // tg sends it to the meltdown itself, so nobody reads it
        await victim.ForceContractDisease(Dm.@new<datum_disease_decloning>()); // slow acting, non-viral GBS
    }
}

/// Makes you vomit up a random organ
// tg: code/datums/instability_meltdown.dm:71
public partial class datum_instability_meltdown_organ_vomit : datum_instability_meltdown
{
    // tg: code/datums/instability_meltdown.dm:73
    public override async DmTask meltdown(mob_living_carbon_human victim = null)
    {
        DmList elligible_organs = Dm.list();
        foreach (obj_item_organ organ in Dm.each_cast<obj_item_organ>(victim.organs)) //make sure we dont get an implant or cavity item
        {
            if (!Dm.truthy(Dm.bitand(organ.organ_flags, ORGAN_EXTERNAL)))
                Dm.append(ref elligible_organs, organ);
        }
        await victim.vomit(VOMIT_CATEGORY_DEFAULT, lost_nutrition: 10);
        if (!Dm.truthy(length(elligible_organs)))
            return;
        obj_item_organ picked_organ = pick(elligible_organs) as obj_item_organ;
        await picked_organ.Remove(null); // tg passes the meltdown datum as the organ's owner (src), not a carbon: the removal from victim's organs does not happen
        victim.visible_message(span_danger(I18n.T("[] vomits up [] []!", victim, p_their(), picked_organ.name)), span_danger(I18n.T("You vomit up your []", picked_organ.name))); //no "vomit up your heart"
        await picked_organ.forceMove(victim.drop_location() as atom);
        if (prob(20))
        {
            // picked_organ.animate_atom_living(): the staff of animation's mimic (magic.dm:187, not ported)
            Dm.Deferred("code/datums/instability_meltdown.dm:87", "organ_vomit meltdown: animate_atom_living (the staff of animation's mimics)");
        }
    }
}

/// Turns you into a snail
// tg: code/datums/instability_meltdown.dm:89
public partial class datum_instability_meltdown_snail : datum_instability_meltdown
{
    // tg: code/datums/instability_meltdown.dm:92
    public override async DmTask meltdown(mob_living_carbon_human victim = null)
    {
        to_chat(victim, span_notice(I18n.T("Oh, I actually feel quite alright!")));
        await victim.ForceContractDisease(Dm.@new<datum_disease_gastrolosis>());
    }
}

/// Turns you into the ultimate lifeform
// tg: code/datums/instability_meltdown.dm:97
public partial class datum_instability_meltdown_crab : datum_instability_meltdown
{
    // tg: code/datums/instability_meltdown.dm:99
    public override async DmTask meltdown(mob_living_carbon_human victim = null)
    {
        await DmTask.CompletedTask;
        to_chat(victim, span_notice(I18n.T("Your DNA mutates into the ultimate biological form!")));
        // victim.crabize()
        Dm.Deferred("code/datums/instability_meltdown.dm:101", "crab meltdown: crabize (the transformation procs)");
    }
}

// Fatal meltdowns

/// Instantly gibs you
// tg: code/datums/instability_meltdown.dm:110
public partial class datum_instability_meltdown_fatal_gib : datum_instability_meltdown_fatal
{
    // tg: code/datums/instability_meltdown.dm:112
    public override async DmTask meltdown(mob_living_carbon_human victim = null)
    {
        victim.investigate_log("has been gibbed by DNA instability.", INVESTIGATE_DEATHS);
        await victim.gib(DROP_ALL_REMAINS);
    }
}

/// Dusts you
// tg: code/datums/instability_meltdown.dm:117
public partial class datum_instability_meltdown_fatal_dust : datum_instability_meltdown_fatal
{
    // tg: code/datums/instability_meltdown.dm:119
    public override async DmTask meltdown(mob_living_carbon_human victim = null)
    {
        victim.investigate_log("has been dusted by DNA instability.", INVESTIGATE_DEATHS);
        await victim.dust();
    }
}

/// Turns you into a statue
// tg: code/datums/instability_meltdown.dm:124
public partial class datum_instability_meltdown_fatal_petrify : datum_instability_meltdown_fatal
{
    // tg: code/datums/instability_meltdown.dm:126
    public override async DmTask meltdown(mob_living_carbon_human victim = null)
    {
        victim.investigate_log("has been transformed into a statue by DNA instability.", INVESTIGATE_DEATHS);
        await victim.death();
        await victim.petrify(INFINITY, FALSE); // statue_timer = INFINITY, save_brain = FALSE (the petrified statue since gaps ST5)
        victim.ghostize(FALSE);
    }
}

/// Either dismembers you, or if unable to, gibs you
// tg: code/datums/instability_meltdown.dm:133
public partial class datum_instability_meltdown_fatal_dismember : datum_instability_meltdown_fatal
{
    // tg: code/datums/instability_meltdown.dm:135
    public override async DmTask meltdown(mob_living_carbon_human victim = null)
    {
        obj_item_bodypart part = victim.get_bodypart(Dm.str(pick(BODY_ZONE_CHEST, BODY_ZONE_HEAD))) as obj_item_bodypart;
        if (part != null)
        {
            await part.dismember();
            return;
        }
        victim.investigate_log("has been gibbed by DNA instability.", INVESTIGATE_DEATHS);
        await victim.gib(DROP_ALL_REMAINS);
    }
}

/// Turns you into a skeleton, with a high chance of killing you soon after
// tg: code/datums/instability_meltdown.dm:144
public partial class datum_instability_meltdown_fatal_skeletonize : datum_instability_meltdown_fatal
{
    // tg: code/datums/instability_meltdown.dm:146
    public override async DmTask meltdown(mob_living_carbon_human victim = null)
    {
        victim.visible_message(span_warning(I18n.T("[]'s skin melts off!", victim)), span_boldwarning(I18n.T("Your skin melts off!")));
        await victim.spawn_gibs();
        // victim.set_species(/datum/species/skeleton)
        Dm.Deferred("code/datums/instability_meltdown.dm:149", "skeletonize meltdown: the skeleton species");
        if (prob(90))
            _addtimer(Dm.@new<datum_callback>(victim, nameof(victim.death)), 3 * SECONDS, file: "code/datums/instability_meltdown.dm", line: 151);
    }
}

/// Makes you look up and melts out your eyes
// tg: code/datums/instability_meltdown.dm:154
public partial class datum_instability_meltdown_fatal_ceiling : datum_instability_meltdown_fatal
{
    // tg: code/datums/instability_meltdown.dm:156
    public override async DmTask meltdown(mob_living_carbon_human victim = null)
    {
        await DmTask.CompletedTask;
        to_chat(victim, span_phobia(I18n.T("LOOK UP!")));
        _addtimer(Dm.@new<datum_callback>(victim, nameof(victim.something_horrible_mindmelt)), 3 * SECONDS, file: "code/datums/instability_meltdown.dm", line: 158);
    }
}

/// Slowly turns you into a psyker
// tg: code/datums/instability_meltdown.dm:161
public partial class datum_instability_meltdown_fatal_psyker : datum_instability_meltdown_fatal
{
    // tg: code/datums/instability_meltdown.dm:163
    public override async DmTask meltdown(mob_living_carbon_human victim = null)
    {
        await victim.slow_psykerize();
    }
}
