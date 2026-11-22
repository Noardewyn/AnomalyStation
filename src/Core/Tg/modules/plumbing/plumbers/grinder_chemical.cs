// Translated from tgstation (snapshot 1d99bd7f): code/modules/plumbing/plumbers/grinder_chemical.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Engineering E15 (plumbing): the whole file. The var block is generated (D111).

// tg: code/modules/plumbing/plumbers/grinder_chemical.dm:1
public partial class obj_machinery_plumbing_grinder_chemical : obj_machinery_plumbing
{
    // tg: code/modules/plumbing/plumbers/grinder_chemical.dm:13
    [DmParams("mapload", "layer")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object layer = Dm.arg(__extra, 0);
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddComponent(typeof(datum_component_plumbing_simple_supply), layer));
        Waiting.NoSleep(AddElement(typeof(datum_element_connect_loc), Initialize__loc_connections));
        return dot;
    }

    // tg: code/modules/plumbing/plumbers/grinder_chemical.dm:16  Initialize's var/static/list/loc_connections (a proc-level static, rules 10.6)
    private static ref DmList Initialize__loc_connections => ref TgState.Current.obj_machinery_plumbing_grinder_chemical__Initialize__loc_connections;

    // tg: code/modules/plumbing/plumbers/grinder_chemical.dm:21
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);

        Dm.append(ref dot, span_notice(I18n.T("Use empty hand to change operation mode. Currently []", Dm.truthy(grinding) ? "Grinding" : "Juicing")));
        return dot;
    }

    /**
     * Check if the user can interact with the grinder
     * Arguments
     *
     * * mob/user - the player we are checking for
     */
    // tg: code/modules/plumbing/plumbers/grinder_chemical.dm:32
    public virtual async DmTask<object> check_interactable(mob user = null)
    {
        return await can_interact(user);
    }

    // tg: code/modules/plumbing/plumbers/grinder_chemical.dm:37
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        mob_living living_user = user as mob_living;
        if (Dm.truthy(living_user.combat_mode) || !Dm.truthy(await user.can_perform_action(this, Dm.bitor(ALLOW_SILICON_REACH, FORBID_TELEKINESIS_REACH))))
            return FALSE;

        DmList options = Dm.list();

        options["grind"] = attack_hand__radial_grind;

        options["juice"] = attack_hand__radial_juice;

        object choice = await show_radial_menu(
            user,
            this,
            options,
            custom_check: Dm.@new<datum_callback>(this, nameof(check_interactable), user));
        if (!Dm.truthy(choice))
            return FALSE;

        grinding = Dm.eq(choice, "grind") ? TRUE : FALSE;
        return TRUE;
    }

    // tg: code/modules/plumbing/plumbers/grinder_chemical.dm:43, :46  attack_hand's var/static radial_grind and radial_juice (rules 10.6)
    private static ref object attack_hand__radial_grind => ref TgState.Current.obj_machinery_plumbing_grinder_chemical__attack_hand__radial_grind;
    private static ref object attack_hand__radial_juice => ref TgState.Current.obj_machinery_plumbing_grinder_chemical__attack_hand__radial_juice;

    // tg: code/modules/plumbing/plumbers/grinder_chemical.dm:61
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = NONE;
        if (Dm.truthy(user.combat_mode) || !Dm.truthy(await user.can_perform_action(this, Dm.bitor(ALLOW_SILICON_REACH, FORBID_TELEKINESIS_REACH))))
            return ITEM_INTERACT_SKIP_TO_ATTACK;

        if (istype(tool, typeof(obj_item_construction_plumbing)))
            return await tool.interact_with_atom(this, user, modifiers);
        else if (istype(tool, typeof(obj_item_plunger)))
            return dot;
        else if (istype(tool, typeof(obj_item_storage_bag)))
        {
            if (!Dm.truthy(anchored))
            {
                to_chat(user, span_warning(I18n.T("Anchor first to start [].", Dm.truthy(grinding) ? "grind" : "juice")));
                return ITEM_INTERACT_BLOCKING;
            }

            to_chat(user, span_notice(I18n.T("You dump items from [] into the grinder.", tool)));
            foreach (obj_item obj_item in Dm.each<obj_item>(tool.contents))
                await blend(obj_item);
            return ITEM_INTERACT_SUCCESS;
        }
        else if (!Dm.truthy(tool.tool_behaviour))
        {
            string action = Dm.text("[]", Dm.truthy(grinding) ? "grind" : "juice");
            if (!Dm.truthy(anchored))
            {
                to_chat(user, span_warning(I18n.T("Anchor first to star [].", action)));
                return ITEM_INTERACT_BLOCKING;
            }

            to_chat(user, span_notice(I18n.T("You attempt to [] [].", action, tool)));
            await blend(tool);
            return ITEM_INTERACT_SUCCESS;
        }
        return dot;
    }

    // tg: code/modules/plumbing/plumbers/grinder_chemical.dm:89
    public override async DmTask<object> CanAllowThrough(atom_movable mover = null, object border_dir = null)
    {
        object dot = await base.CanAllowThrough(mover, border_dir);
        if (!Dm.truthy(anchored))
            return dot;
        if (!istype(mover, typeof(obj_item)))
            return FALSE;
        return TRUE;
    }

    // tg: code/modules/plumbing/plumbers/grinder_chemical.dm:97
    [SignalHandler]
    public virtual void on_entered(datum source = null, atom_movable AM = null)
    {
        if (!isitem(AM))
            return;

        Waiting.Detach(blend(AM as obj_item));
    }

    // tg: code/modules/plumbing/plumbers/grinder_chemical.dm:106
    public override async DmTask<object> blended(obj_item blended_item = null, object grinded = null)
    {
        //don't delete slime extracts
        if (blended_item is obj_item_slime_extract slime)
        {
            //so you can't regrind them for extra stuff
            slime.can_grind = FALSE;

            await blended_item.forceMove(drop_location() as atom);

            return TRUE;
        }

        return await base.blended(blended_item, grinded);
    }

    /**
     * Grinds/Juices the atom
     * Arguments
     * * [AM][atom] - the atom to grind or juice
     */
    // tg: code/modules/plumbing/plumbers/grinder_chemical.dm:123
    public virtual async DmTask blend(obj_item item_to_blend = null)
    {
        if (!Dm.truthy(is_operational) || !Dm.truthy(anchored))
            return;
        datum_reagents holder = (datum_reagents)reagents;
        if (Dm.truthy(holder.holder_full()))
            return;

        if (Dm.truthy(Dm.bitand(item_to_blend.item_flags, ABSTRACT)) || Dm.truthy(Dm.bitand(item_to_blend.flags_1, HOLOGRAM_1)))
            return;

        if (!Dm.truthy(item_to_blend.blend_requirements(this)))
            return;

        if (!Dm.truthy(grinding))
            await item_to_blend.juice(holder, usr, this);
        else if (Dm.truthy(length(await item_to_blend.grind_results())) || Dm.truthy(((datum_reagents)item_to_blend.reagents)?.total_volume))
            await item_to_blend.grind(holder, usr, this);

        await use_energy(active_power_usage);
    }
}

public sealed partial class TgState
{
    // tg: code/modules/plumbing/plumbers/grinder_chemical.dm:16
    internal DmList obj_machinery_plumbing_grinder_chemical__Initialize__loc_connections = Dm.list(
        Dm.kv(COMSIG_ATOM_ENTERED, nameof(obj_machinery_plumbing_grinder_chemical.on_entered)));

    // tg: code/modules/plumbing/plumbers/grinder_chemical.dm:43
    internal object obj_machinery_plumbing_grinder_chemical__attack_hand__radial_grind = image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_grind");

    // tg: code/modules/plumbing/plumbers/grinder_chemical.dm:46
    internal object obj_machinery_plumbing_grinder_chemical__attack_hand__radial_juice = image(icon: Dm.resource("icons/hud/radial.dmi"), icon_state: "radial_juice");
}
