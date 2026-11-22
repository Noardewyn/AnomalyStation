// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/grenades/plastic.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M5x slice 4 (D149, D150): C4. Left in docs/ai/specs/live-partial.txt: the fantasy bonuses (M10), the suicide and its shout
// (M10). The X-4 (/obj/item/grenade/c4/x4) is a var-only subtype and lives with it. The var block is generated.

// tg: code/game/objects/items/grenades/plastic.dm:1
[DmPath("/obj/item/grenade/c4")]
public partial class obj_item_grenade_c4 : obj_item_grenade
{
    // tg: code/game/objects/items/grenades/plastic.dm:47
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_empprotection), EMP_PROTECT_WIRES));
        plastic_overlay = mutable_appearance(icon, Dm.text("[]2", inhand_icon_state), HIGH_OBJ_LAYER) as mutable_appearance;
        set_wires(Dm.@new<datum_wires_explosive_c4>(this));
        return dot;
    }

    // tg: code/game/objects/items/grenades/plastic.dm:53
    public override object Destroy(float? force = null)
    {
        target = null;
        return base.Destroy(force);
    }

    // tg: code/game/objects/items/grenades/plastic.dm:57
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        to_chat(user, span_notice(I18n.T("The wire panel can be accessed without a screwdriver.")));
        return TRUE;
    }

    // tg: code/game/objects/items/grenades/plastic.dm:61
    public override async DmTask<object> attackby(obj_item item = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        if (Dm.truthy(is_wire_tool(item)))
        {
            // tg: :63  wires.interact(user): the wires window, where cutting or pulsing any wire sets the charge off (wire
            // hacking, M7, D150)
            Dm.NotTranslated("code/game/objects/items/grenades/plastic.dm:63", "attackby: wires.interact(user), C4's wire panel (wire hacking, M7, D150)");
            return null;
        }
        else
            return await base.attackby(item, user, modifiers, attack_modifiers);
    }

    // tg: code/game/objects/items/grenades/plastic.dm:67
    public override async DmTask<object> detonate(mob_living lanced_by = null)
    {
        if (QDELETED(this))
            return FALSE;
        if (Dm.truthy(dud_flags))
        {
            active = FALSE;
            update_appearance();
            return FALSE;
        }

        object dot = await base.detonate(lanced_by);
        turf location = null;
        object target_density = null;
        if (target != null)
        {
            if (!QDELETED(target))
            {
                location = get_turf(target);
                target_density = target.density; // We're about to blow target up, so need to save this value for later
                target.cut_overlay(plastic_overlay);
                if (!ismob(target) || Dm.truthy(full_damage_on_mobs))
                {
                    float x = location.x, y = location.y, z = location.z;
                    await EX_ACT(target, EXPLODE_HEAVY, target);
                    // TURF-IDENTITY (object-model.md 7.1): a wall the charge was stuck to is replaced by its own heavy ex_act
                    // (dismantle_wall); tg's location, a turf reference, then names the new turf of the cell
                    if (location.x == 0 && location.y == 0 && location.z == 0)
                        location = locate(x, y, z);
                }
            }
        }
        else
            location = get_turf(this);
        if (location != null)
        {
            if (Dm.truthy(directional) && Dm.truthy(target_density))
            {
                object angle = dir2angle(aim_dir);
                await explosion(location, devastation_range: Dm.num(boom_sizes[1]), heavy_impact_range: Dm.num(boom_sizes[2]), light_impact_range: Dm.num(boom_sizes[3]), explosion_cause: this, explosion_direction: Dm.num(angle), explosion_arc: directional_arc);
            }
            else
                await explosion(location, devastation_range: Dm.num(boom_sizes[1]), heavy_impact_range: Dm.num(boom_sizes[2]), light_impact_range: Dm.num(boom_sizes[3]), explosion_cause: this);
        }
        qdel(this);
        return dot;
    }

    //assembly stuff
    // tg: code/game/objects/items/grenades/plastic.dm:96
    public override object receive_signal(datum_signal signal = null)
    {
        // (receive_signal cannot sleep, waiting-model.md 5.4: explosion() may, so the detonation runs on its own)
        Waiting.Detach(detonate());
        return null;
    }

    // tg: code/game/objects/items/grenades/plastic.dm:99
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object newtime = await tgui_input_number(user, "Please set the timer", "C4 Timer", minimum_timer, maximum_timer, minimum_timer);
        if (!Dm.truthy(newtime) || QDELETED(user) || QDELETED(this) || !Dm.truthy(await ((mob)World.Current.usr).can_perform_action(this, FORBID_TELEKINESIS_REACH)))
            return null;
        det_time = Dm.num(newtime);
        to_chat(user, I18n.T("Timer set for [] seconds.", det_time));
        return null;
    }

    /// DV255: the side of its target's tile the charge was stuck on (a BYOND dir), 0 for a click with no aim.
    private int stuck_face;

    // tg: code/game/objects/items/grenades/plastic.dm:106
    public override async DmTask<object> interact_with_atom(atom interacting_with = null, mob_living user = null, DmList modifiers = null)
    {
        // Here lies C4 ghosts. We hardly knew ye
        if (istype(interacting_with, typeof(mob_dead))) // isdead
            return NONE;
        aim_dir = get_dir(user, interacting_with);
        // DEVIATION(DV255): the face the planter's crosshair went in by, where the 3D client draws the charge
        stuck_face = PointGeometry.FaceAimedAt(modifiers, interacting_with);
        return Dm.truthy(await plant_c4(interacting_with, user)) ? ITEM_INTERACT_SUCCESS : ITEM_INTERACT_BLOCKING;
    }

    // tg: code/game/objects/items/grenades/plastic.dm:113
    public virtual async DmTask<object> plant_c4(atom bomb_target = null, mob_living user = null)
    {
        if (bomb_target != user && HAS_TRAIT(user, TRAIT_PACIFISM) && isliving(bomb_target))
        {
            to_chat(user, span_warning(I18n.T("You don't want to harm other living beings!")));
            return FALSE;
        }

        to_chat(user, span_notice(I18n.T("You start planting []. The timer is set to []...", this, det_time)));

        if (!Dm.truthy(await do_after(user, 3 * SECONDS, target: bomb_target)))
            return FALSE;
        if (!Dm.truthy(await user.temporarilyRemoveItemFromInventory(this)))
            return FALSE;
        target = bomb_target;
        active = TRUE;

        message_admins(Dm.text("[] planted [] on [] at [] with [] second fuse", ADMIN_LOOKUPFLW(user), name, target.name, ADMIN_VERBOSEJMP(target), det_time));
        user.log_message(Dm.text("planted [] on [] with a [] second fuse.", name, target.name, det_time), LOG_ATTACK);
        // tg blends the charge's icon over the target's (icon(...).Blend(..., ICON_OVERLAY)) into one picture; the port draws the same
        // picture as the target's appearance with the charge as its overlay, which the client composes as it does every overlay
        var bomb_target_image = (mutable_appearance)mutable_appearance(bomb_target.icon, Dm.str(bomb_target.icon_state));
        Dm.append(ref bomb_target_image.overlays, mutable_appearance(icon, Dm.str(icon_state)));
        notify_ghosts(
            I18n.T("[] has planted \\a [] on [] with a [] second fuse!", user.real_name, this, target, det_time),
            source: bomb_target,
            header: I18n.T("Explosive Planted"),
            alert_overlay: bomb_target_image,
            notify_flags: NOTIFY_CATEGORY_NOFLASH);

        await moveToNullspace(); //Yep

        if (isitem(bomb_target)) //your crappy throwing star can't fly so good with a giant brick of c4 on it.
        {
            obj_item thrown_weapon = (obj_item)bomb_target;
            thrown_weapon.throw_speed = max(1, (thrown_weapon.throw_speed - 3));
            thrown_weapon.throw_range = max(1, (thrown_weapon.throw_range - 3));
            if (thrown_weapon.get_embed() is datum_embedding thrown_embed)
                thrown_embed.embed_chance = 0;
        }
        else if (isliving(bomb_target))
            plastic_overlay.layer = FLOAT_LAYER;
        else if (isopenturf(bomb_target)) //Render them above open turf layer if theyre open turf
            plastic_overlay.layer = ABOVE_OPEN_TURF_LAYER;

        // DEVIATION(DV63): the charge's overlay faces the planter (the side of a wall or a door it is stuck on, which the 3D client draws it on);
        // its one-direction state looks the same either way
        if (stuck_face != 0)
            plastic_overlay.dir = stuck_face;
        else if (Dm.truthy(aim_dir))
            plastic_overlay.dir = REVERSE_DIR(aim_dir);
        target.add_overlay(plastic_overlay);
        to_chat(user, span_notice(I18n.T("You plant the bomb. Timer counting down from [].", det_time)));
        _addtimer(Dm.@new<datum_callback>(this, nameof(detonate)), det_time * 10, file: "code/game/objects/items/grenades/plastic.dm", line: 154);
        return TRUE;
    }
}
