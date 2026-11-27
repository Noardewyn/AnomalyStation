// Translated from tgstation (snapshot 1d99bd7f): code/modules/research/xenobiology/crossbreeding/stabilized.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the stabilized extracts, which give whoever carries them (in hand, in a pocket or a bag, in a MOD's storage) the
// passive effect of their colour (the effects are in _status_effects.dm); the gold one's familiar can be moved, changed, freed of
// its mind or renamed, the rainbow one keeps a regenerative extract for its owner's crit. The var blocks are generated (D111).

/*
Stabilized extracts:
	Provides a passive buff to the holder.
*/

//To add: Create an effect in crossbreeding/_status_effects.dm with the name "/datum/status_effect/stabilized/[color]"
//Status effect will automatically be applied while held, and lost on drop.

// tg: code/modules/research/xenobiology/crossbreeding/stabilized.dm:10
public partial class obj_item_slimecross_stabilized : obj_item_slimecross
{
    // tg: code/modules/research/xenobiology/crossbreeding/stabilized.dm:16
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        START_PROCESSING(SSobj, this);
        return dot;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/stabilized.dm:20
    public override object Destroy(float? force = null)
    {
        STOP_PROCESSING(SSobj, this);
        qdel(linked_effect);
        return base.Destroy(force);
    }

    /// Returns the mob that is currently holding us if we are either in their inventory or a backpack analogue.
    /// Returns null if it's in an invalid location, so that we can check explicitly for null later.
    // tg: code/modules/research/xenobiology/crossbreeding/stabilized.dm:27
    public virtual object get_held_mob()
    {
        if (loc == null)
            return null;
        if (isliving(loc))
            return loc;
        // Snowflake check for modsuit backpacks, which should be valid but are 3 rather than 2 steps from the owner
        if (loc is obj_item_mod_module_storage mod_backpack)
        {
            mob_living modsuit_wearer = mod_backpack.mod?.wearer as mob_living;
            return modsuit_wearer != null ? modsuit_wearer : null;
        }
        atom nested_loc = loc.loc;
        if (isliving(nested_loc))
            return nested_loc;
        return null;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/stabilized.dm:42
    public override object process(object seconds_per_tick = null, params object[] __extra)
    {
        mob_living holder = get_held_mob() as mob_living;
        if (holder == null)
            return null;
        Type effectpath = typeof(datum_status_effect_stabilized);
        DmList effects = TgState.Current.obj_item_slimecross_stabilized__effects ??= subtypesof(typeof(datum_status_effect_stabilized));
        foreach (Type effect in Dm.each_cast<Type>(effects))
        {
            if (!Dm.eq(Dm.initial(effect, (datum_status_effect_stabilized s) => s.colour), colour))
                continue;
            effectpath = effect;
            break;
        }
        if (Dm.truthy(holder.has_status_effect(effectpath)))
            return null;
        Waiting.NoSleep(holder.apply_status_effect(effectpath, this));
        return PROCESS_KILL;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/stabilized.dm:124
public partial class obj_item_slimecross_stabilized_gold : obj_item_slimecross_stabilized
{
    // tg: code/modules/research/xenobiology/crossbreeding/stabilized.dm:130
    public virtual void generate_mobtype()
    {
        DmList mob_spawn_pets = TgState.Current.obj_item_slimecross_stabilized_gold__mob_spawn_pets ??= Dm.list();
        if (!Dm.truthy(Dm.length(mob_spawn_pets)))
        {
            foreach (Type animal in Dm.each_cast<Type>(subtypesof(typeof(mob_living_simple_animal))))
            {
                if (Dm.eq(Dm.initial(animal, (mob_living_simple_animal a) => a.gold_core_spawnable), FRIENDLY_SPAWN))
                    Dm.append(ref mob_spawn_pets, animal);
            }
            foreach (Type basicanimal in Dm.each_cast<Type>(subtypesof(typeof(mob_living_basic))))
            {
                if (Dm.eq(Dm.initial(basicanimal, (mob_living_basic a) => a.gold_core_spawnable), FRIENDLY_SPAWN))
                    Dm.append(ref mob_spawn_pets, basicanimal);
            }
        }
        mob_type = pick(mob_spawn_pets) as Type;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/stabilized.dm:141
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        generate_mobtype();
        return dot;
    }

    // tg: code/modules/research/xenobiology/crossbreeding/stabilized.dm:145
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object choice = await tgui_input_list(user, I18n.T("Which do you want to reset?"), I18n.T("Familiar Adjustment"), sort_list(Dm.list("Familiar Location", "Familiar Species", "Familiar Sentience", "Familiar Name")) as DmList);
        if (choice == null)
            return null;
        if (!Dm.truthy(await user.can_perform_action(this)))
            return null;
        if (isliving(user))
        {
            mob_living L = (mob_living)user;
            if (Dm.truthy(L.has_status_effect(typeof(datum_status_effect_stabilized_gold))))
                L.remove_status_effect(typeof(datum_status_effect_stabilized_gold));
        }
        if (Dm.eq(choice, "Familiar Location"))
        {
            to_chat(user, span_notice(I18n.T("You prod [], and it shudders slightly.", this)));
            START_PROCESSING(SSobj, this);
        }
        if (Dm.eq(choice, "Familiar Species"))
        {
            to_chat(user, span_notice(I18n.T("You squeeze [], and a shape seems to shift around inside.", this)));
            generate_mobtype();
            START_PROCESSING(SSobj, this);
        }
        if (Dm.eq(choice, "Familiar Sentience"))
        {
            to_chat(user, span_notice(I18n.T("You poke [], and it lets out a glowing pulse.", this)));
            saved_mind = null;
            START_PROCESSING(SSobj, this);
        }
        if (Dm.eq(choice, "Familiar Name"))
        {
            object newname = await sanitize_name(await tgui_input_text(user, I18n.T("Would you like to change the name of []", mob_name), I18n.T("Name change"), mob_name, MAX_NAME_LEN));
            if (Dm.truthy(newname))
                mob_name = Dm.str(newname);
            to_chat(user, span_notice(I18n.T("You speak softly into [], and it shakes slightly in response.", this)));
            START_PROCESSING(SSobj, this);
        }
        return null;
    }
}

// tg: code/modules/research/xenobiology/crossbreeding/stabilized.dm:190
public partial class obj_item_slimecross_stabilized_rainbow : obj_item_slimecross_stabilized
{
    // tg: code/modules/research/xenobiology/crossbreeding/stabilized.dm:195
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!(tool is obj_item_slimecross_regenerative))
            return NONE;

        if (regencore != null)
        {
            to_chat(user, span_warning(I18n.T("[] already has a regenerative crossbreed stored in it!", this)));
            return ITEM_INTERACT_BLOCKING;
        }

        to_chat(user, span_notice(I18n.T("You place [] in [], prepping the extract for automatic application!", tool, this)));
        regencore = (obj_item_slimecross_regenerative)tool;
        await tool.forceMove(this);
        return ITEM_INTERACT_SUCCESS;
    }
}

public sealed partial class TgState
{
    // tg: code/modules/research/xenobiology/crossbreeding/stabilized.dm:48  process's var/static/list/effects (rules 10.6)
    internal DmList obj_item_slimecross_stabilized__effects;
    // tg: code/modules/research/xenobiology/crossbreeding/stabilized.dm:131  generate_mobtype's var/static/list/mob_spawn_pets
    internal DmList obj_item_slimecross_stabilized_gold__mob_spawn_pets;
}
