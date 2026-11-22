// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/mop.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The base mop (gaps package C3: the cleanbot's and a janitor's) but the fantasy bonuses (the enchanting's fantasy component); the advanced
// mop with its condenser (gaps H4, the janitor).

// tg: code/game/objects/items/mop.dm:1
public partial class obj_item_mop : obj_item
{
    // tg: code/game/objects/items/mop.dm:24
    private static DmList clean_blacklist => TgState.Current.obj_item_mop__clean_blacklist ??= typecacheof(Dm.list(typeof(obj_item_reagent_containers_cup_bucket), typeof(obj_structure_mop_bucket), typeof(obj_structure_sink))) as DmList;

    // tg: code/game/objects/items/mop.dm:38
    [DmParams("mapload")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_cleaner), mopspeed, Dm.named("pre_clean_callback", Dm.@new<datum_callback>(this, nameof(should_clean))), Dm.named("on_cleaned_callback", Dm.@new<datum_callback>(this, nameof(apply_reagents)))));
        Waiting.NoSleep(AddComponent(typeof(datum_component_walking_aid)));
        create_reagents(max_reagent_volume);
        Dm.append(ref GLOB.janitor_devices, this);
        return dot;
    }

    // tg: code/game/objects/items/mop.dm:45
    public override object Destroy(float? force = null)
    {
        Dm.remove(ref GLOB.janitor_devices, this);
        return base.Destroy(force);
    }

    ///Checks whether or not we should clean.
    // tg: code/game/objects/items/mop.dm:50
    public virtual object should_clean(datum cleaning_source = null, atom atom_to_clean = null, mob_living cleaner = null)
    {
        if (Dm.truthy(clean_blacklist[atom_to_clean.type]))
            return Dm.bitor(CLEAN_BLOCKED, CLEAN_DONT_BLOCK_INTERACTION);
        datum_reagents our_reagents = reagents as datum_reagents;
        if (Dm.num(our_reagents.total_volume) < 0.1f)
        {
            cleaner.balloon_alert(cleaner, "mop is dry!");
            return CLEAN_BLOCKED;
        }
        if (Dm.truthy(our_reagents.has_reagent(amount: 1, chemical_flags: REAGENT_CLEANS)))
            return CLEAN_ALLOWED;
        return Dm.bitor(CLEAN_ALLOWED, Dm.bitor(CLEAN_NO_XP, CLEAN_NO_WASH));
    }

    /**
     * Applies reagents to the cleaned floor and removes them from the mop.
     *
     * Arguments
     * * cleaning_source the source of the cleaning
     * * cleaned_turf the turf that is being cleaned
     * * cleaner the mob that is doing the cleaning
     */
    // tg: code/game/objects/items/mop.dm:68
    public virtual void apply_reagents(datum cleaning_source = null, turf cleaned_turf = null, mob_living cleaner = null, object clean_succeeded = null)
    {
        if (!Dm.truthy(clean_succeeded))
            return;
        datum_reagents our_reagents = reagents as datum_reagents;
        Waiting.NoSleep(our_reagents.expose(cleaned_turf, TOUCH, 10)); //Needed for proper floor wetting.
        object val2remove = 1;
        if (Dm.truthy(cleaner?.mind))
            val2remove = round(Dm.num(cleaner.mind.get_skill_modifier(typeof(datum_skill_cleaning), SKILL_SPEED_MODIFIER)), 0.1f);
        Waiting.NoSleep(our_reagents.remove_all(val2remove)); //reaction() doesn't use up the reagents
    }
}

// rules 10.6: the static vars above, made on first use
public sealed partial class TgState
{
    internal DmList obj_item_mop__clean_blacklist;
}

// tg: code/game/objects/items/mop.dm:77
public partial class obj_item_mop_advanced : obj_item_mop
{
    // tg: code/game/objects/items/mop.dm:95
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        START_PROCESSING(SSobj, this);
        return dot;
    }

    // tg: code/game/objects/items/mop.dm:99
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        refill_enabled = Dm.truthy(refill_enabled) ? FALSE : TRUE;
        if (Dm.truthy(refill_enabled))
            START_PROCESSING(SSobj, this);
        else
            STOP_PROCESSING(SSobj, this);
        user.balloon_alert(user, Dm.text("condenser switch []", Dm.truthy(refill_enabled) ? "on" : "off"));
        playsound(user, Dm.resource("sound/machines/click.ogg"), 30, TRUE);
        await DmTask.CompletedTask;
        return null;
    }

    // tg: code/game/objects/items/mop.dm:108
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        var holder = (datum_reagents)reagents;
        float amadd = min(Dm.num(max_reagent_volume) - holder.total_volume, refill_rate * Dm.num(seconds_per_tick));
        if (amadd > 0)
            Waiting.NoSleep(holder.add_reagent(refill_reagent, amadd));
        return null;
    }

    // tg: code/game/objects/items/mop.dm:113
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        DmList dot = await base.examine(user, __extra) as DmList;
        Dm.append(ref dot, span_notice(I18n.T("The condenser switch is set to <b>[]</b>.", Dm.truthy(refill_enabled) ? "ON" : "OFF")));
        return dot;
    }

    // tg: code/game/objects/items/mop.dm:117
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        return base.Destroy(force);
    }
}

