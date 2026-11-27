// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/lavaland/mining_loot/megafauna/demonic_frost_miner.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file. The var blocks are generated.

// Resurrection crystal

// tg: code/modules/mining/lavaland/mining_loot/megafauna/demonic_frost_miner.dm:3
public partial class obj_item_resurrection_crystal : obj_item
{
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/demonic_frost_miner.dm:9
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        if (!iscarbon(user))
        {
            to_chat(user, span_notice(I18n.T("A dark presence stops you from absorbing the crystal.")));
            return null;
        }
        await forceMove(user);
        to_chat(user, span_notice(I18n.T("You feel a bit safer... but a demonic presence lurks in the back of your head...")));
        RegisterSignal(user, COMSIG_LIVING_DEATH, nameof(resurrect));
        return null;
    }

    /// Resurrects the target when they die by moving them and dusting a clone in their place, one life for another
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/demonic_frost_miner.dm:18
    [SignalHandler]
    public virtual void resurrect(mob_living_carbon user = null, object gibbed = null)
    {
        if (Dm.truthy(gibbed))
        {
            to_chat(user, span_notice(I18n.T("This power cannot be used if your entire mortal body is disintegrated...")));
            return;
        }
        user.visible_message(span_notice(I18n.T("You see []'s soul dragged out of their body!", user)), span_notice(I18n.T("You feel your soul dragged away to a fresh body!")));
        System.Type typepath = user.type;
        mob_living_carbon clone = Dm.@new(typepath, user.loc) as mob_living_carbon;
        clone.real_name = user.real_name;
        Waiting.Detach(user.dna.copy_dna(clone.dna, Dm.bitor(COPY_DNA_SE, COPY_DNA_SPECIES))); // INVOKE_ASYNC
        Waiting.NoSleep(clone.updateappearance(mutcolor_update: 1));
        turf T = (find_maintenance_spawn(atmos_sensitive: TRUE, require_darkness: TRUE) ?? find_safe_turf()) as turf;
        Waiting.NoSleep(user.forceMove(T));
        Waiting.NoSleep(user.revive(ADMIN_HEAL_ALL));
        user.set_species(typeof(datum_species_shadow)); // INVOKE_ASYNC; set_species never sleeps here
        to_chat(user, span_notice(I18n.T("You blink and find yourself in []... feeling a bit darker.", get_area_name(T))));
        Waiting.NoSleep(clone.dust());
        qdel(this);
    }
}

// Cursed boots

// tg: code/modules/mining/lavaland/mining_loot/megafauna/demonic_frost_miner.dm:40
public partial class obj_item_clothing_shoes_winterboots_ice_boots_ice_trail : obj_item_clothing_shoes_winterboots_ice_boots
{
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/demonic_frost_miner.dm:47
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        RegisterSignal(this, COMSIG_SHOES_STEP_ACTION, nameof(on_step));
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/demonic_frost_miner.dm:51
    public override async DmTask equipped__proc(mob user = null, object slot = null, float? initial = null)
    {
        await base.equipped__proc(user, slot, initial);
        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_FEET)))
            ADD_TRAIT(this, TRAIT_NODROP, CURSED_ITEM_TRAIT(type));
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/demonic_frost_miner.dm:56
    public override async DmTask<object> dropped(mob user = null, float? silent = null, params object[] __extra)
    {
        object dot = await base.dropped(user, silent, __extra);
        // Could have been blown off in an explosion from the previous owner
        REMOVE_TRAIT(this, TRAIT_NODROP, CURSED_ITEM_TRAIT(type));
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/demonic_frost_miner.dm:61
    public override async DmTask<object> ui_action_click(mob user = null, object actiontype = null)
    {
        await DmTask.CompletedTask;
        on = Dm.truthy(on) ? FALSE : TRUE;
        to_chat(user, span_notice(Dm.truthy(on) ? I18n.T("You activate [].", this) : I18n.T("You deactivate [].", this)));
        return null;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/demonic_frost_miner.dm:65
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(Dm.truthy(on) ? I18n.T("The shoes are enabled.") : I18n.T("The shoes are disabled.")));
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/demonic_frost_miner.dm:69
    [SignalHandler]
    public virtual void on_step()
    {
        turf T = get_turf(loc);
        if (!Dm.truthy(on) || isclosedturf(T) || istype(T, change_turf))
            return;
        System.Type reset_turf = T.type;
        Waiting.NoSleep(T.ChangeTurf(change_turf, flags: CHANGETURF_INHERIT_AIR));
        T = (turf)turf.dm_identity(T); // TURF-IDENTITY: tg's T names the cell, the ice from here on
        _addtimer(Dm.@new<datum_callback>(T, nameof(turf.ChangeTurf), reset_turf, null, CHANGETURF_INHERIT_AIR), duration, Dm.bitor(TIMER_OVERRIDE, TIMER_UNIQUE), file: "code/modules/mining/lavaland/mining_loot/megafauna/demonic_frost_miner.dm", line: 77);
    }
}

// Demonic jackhammer

// tg: code/modules/mining/lavaland/mining_loot/megafauna/demonic_frost_miner.dm:81
public partial class obj_item_pickaxe_drill_jackhammer_demonic : obj_item_pickaxe_drill_jackhammer
{
    // tg: code/modules/mining/lavaland/mining_loot/megafauna/demonic_frost_miner.dm:86
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_knockback), 4, TRUE, FALSE));
        Waiting.NoSleep(AddElement(typeof(datum_element_lifesteal), 5));
        return dot;
    }

    // tg: code/modules/mining/lavaland/mining_loot/megafauna/demonic_frost_miner.dm:91
    public override async DmTask<object> use_tool(atom target = null, mob_living user = null, object delay = null, float? amount = null, float? volume = null, datum_callback extra_checks = null, float? timed_action_flags = null)
    {
        amount ??= 0;
        volume ??= 0;
        turf T = get_turf(target);
        mineral_scan_pulse(T, Dm.num(world.view) + 1, this);
        return await base.use_tool(target, user, delay, amount, volume, extra_checks, timed_action_flags);
    }
}
