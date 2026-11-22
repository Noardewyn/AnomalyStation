// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/tools/janitorial/broom.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (silicons SI2, D326: the janitor cyborg's push broom upgrade and the rolling positronic sphere's sweep). The var
// block is generated (D111).

// tg: code/game/objects/items/tools/janitorial/broom.dm:4
public partial class obj_item_pushbroom : obj_item
{
    // tg: code/game/objects/items/tools/janitorial/broom.dm:24
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_jousting), Dm.named("damage_boost_per_tile", 1)));
        Waiting.NoSleep(AddComponent(typeof(datum_component_two_handed),
            Dm.named("force_unwielded", 8),
            Dm.named("force_wielded", 12),
            Dm.named("icon_wielded", Dm.text("[]1", base_icon_state)),
            Dm.named("wield_callback", Dm.@new<datum_callback>(this, nameof(on_wield))),
            Dm.named("unwield_callback", Dm.@new<datum_callback>(this, nameof(on_unwield)))
        ));
        Waiting.NoSleep(AddComponent(typeof(datum_component_walking_aid)));
        return dot;
    }

    // tg: code/game/objects/items/tools/janitorial/broom.dm:36
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        icon_state = Dm.text("[]0", base_icon_state);
        return await base.update_icon_state(__extra);
    }

    /**
     * Handles registering the sweep proc when the broom is wielded
     *
     * Arguments:
     * * source - The source of the on_wield proc call
     * * user - The user which is wielding the broom
     */
    // tg: code/game/objects/items/tools/janitorial/broom.dm:47
    public virtual void on_wield(obj_item source = null, mob user = null)
    {
        to_chat(user, span_notice(I18n.T("You brace the [] against the ground in a firm sweeping stance.", this)));
        RegisterSignal(user, COMSIG_MOVABLE_PRE_MOVE, nameof(sweep));
    }

    /**
     * Handles unregistering the sweep proc when the broom is unwielded
     *
     * Arguments:
     * * source - The source of the on_unwield proc call
     * * user - The user which is unwielding the broom
     */
    // tg: code/game/objects/items/tools/janitorial/broom.dm:58
    public virtual void on_unwield(obj_item source = null, mob user = null)
    {
        UnregisterSignal(user, COMSIG_MOVABLE_PRE_MOVE);
    }

    // tg: code/game/objects/items/tools/janitorial/broom.dm:61
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        sweep(user, interacting_with);
        return NONE; // I guess
    }

    /**
     * Attempts to push up to BROOM_PUSH_LIMIT atoms from a given location the user's faced direction
     *
     * Arguments:
     * * user - The user of the pushbroom
     * * A - The atom which is located at the location to push atoms from
     */
    // tg: code/game/objects/items/tools/janitorial/broom.dm:72
    [SignalHandler]
    public virtual void sweep(mob user = null, atom atom = null)
    {
        Waiting.NoSleep(do_sweep(this, user, atom, user.dir));
    }
}

// tg: code/game/objects/items/tools/janitorial/broom.dm:85
public static partial class GlobalProcs
{
    /// Max number of atoms a broom can sweep at once
    private const int BROOM_PUSH_LIMIT = 20;

    /**
    * Sweep objects in the direction we're facing towards our direction
    * Arguments
    * * broomer - The object being used for brooming
    * * user - The person who is brooming
    * * target - The object or tile that's target of a broom click or being moved into
    * * sweep_dir - The directions in which we sweep objects
    */
    // tg: code/game/objects/items/tools/janitorial/broom.dm:85
    public static async DmTask do_sweep(obj broomer = null, mob user = null, atom target = null, object sweep_dir = null)
    {
        turf current_item_loc = isturf(target) ? target as turf : target.loc as turf;
        if (!isturf(current_item_loc))
            return;
        turf new_item_loc = get_step(current_item_loc, Dm.num(sweep_dir)) as turf;

        DmList items_to_sweep = Dm.list();
        int i = 1;
        foreach (obj_item garbage in Dm.each<obj_item>(current_item_loc.contents))
        {
            if (Dm.truthy(garbage.anchored))
                continue;
            Dm.append(ref items_to_sweep, garbage);
            i++;
            if (i > BROOM_PUSH_LIMIT)
                break;
        }

        if (Dm.truthy(new_item_loc?._listen_lookup?[COMSIG_TURF_RECEIVE_SWEEPED_ITEMS]))
            new_item_loc._SendSignal(COMSIG_TURF_RECEIVE_SWEEPED_ITEMS, Dm.list(new_item_loc, broomer, user, items_to_sweep));

        if (!Dm.truthy(length(items_to_sweep)))
            return;

        foreach (obj_item garbage in Dm.each<obj_item>(items_to_sweep))
            await garbage.Move(new_item_loc, sweep_dir);

        playsound(current_item_loc, Dm.resource("sound/items/weapons/thudswoosh.ogg"), 30, TRUE, -1);
    }
}
