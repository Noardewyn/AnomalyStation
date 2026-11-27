// Translated from tgstation (snapshot 1d99bd7f): code/modules/holodeck/items.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but the holographic skateboard (its pick_up_board overrides the skateboards' own, which are not translated): the
// holographic energy swords, the holographic deck of cards (syndicate G3d, D316), the dodgeball, the ready declaration device, the
// holodeck's conveyor and the papers. The var blocks are generated (D111).

// tg: code/modules/holodeck/items.dm:10
public partial class obj_item_melee_energy_sword_holographic : obj_item_melee_energy_sword
{
    // tg: code/modules/holodeck/items.dm:24
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(sword_color_icon))
            sword_color_icon = Dm.str(pick("red", "blue", "green", "purple"));
        return dot;
    }
}

// tg: code/modules/holodeck/items.dm:36
public partial class obj_item_toy_cards_deck_syndicate_holographic : obj_item_toy_cards_deck_syndicate
{
    // tg: code/modules/holodeck/items.dm:38
    [DmParams("mapload", "holodeck")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        holodeck = Dm.arg(__extra, 0) as obj_machinery_computer_holodeck;
        RegisterSignal(this, COMSIG_QDELETING, nameof(handle_card_delete));
        return base.Initialize(mapload, __extra);
    }

    // tg: code/modules/holodeck/items.dm:43
    public virtual void handle_card_delete(datum source = null)
    {
        //if any REAL cards have been inserted into the deck they are moved outside before destroying it
        foreach (obj_item_toy_singlecard card in Dm.each<obj_item_toy_singlecard>(card_atoms))
        {
            if (Dm.truthy(Dm.bitand(Dm.num(card.flags_1), HOLOGRAM_1)))
                continue;
            card_atoms.Remove(card);
            Waiting.NoSleep(card.forceMove(drop_location() as atom));
        }
    }
}

// tg: code/modules/holodeck/items.dm:53
public partial class obj_item_toy_dodgeball : obj_item_toy
{
    // tg: code/modules/holodeck/items.dm:61
    public override object throw_impact(atom hit_atom = null, datum_thrownthing throwingdatum = null, params object[] __extra)
    {
        base.throw_impact(hit_atom, throwingdatum, __extra);
        if (ishuman(hit_atom))
        {
            var M = (mob_living_carbon)hit_atom;
            playsound(this, Dm.resource("sound/items/dodgeball.ogg"), 50, TRUE);
            Waiting.NoSleep(M.apply_damage(10, STAMINA));
            if (prob(5))
            {
                Waiting.NoSleep(M.Paralyze(60));
                visible_message(span_danger(I18n.T("[] is knocked right off [] feet!", M, M.p_their())));
            }
        }
        return null;
    }
}

//
// Machines
//

// tg: code/modules/holodeck/items.dm:76
public partial class obj_machinery_readybutton : obj_machinery
{
    // tg: code/modules/holodeck/items.dm:89
    public override DmTask<object> attack_ai(mob user = null)
    {
        to_chat(user, span_warning(I18n.T("The station AI is not to interact with these devices!")));
        return DmTask<object>.FromResult(null);
    }

    // tg: code/modules/holodeck/items.dm:93
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        to_chat(user, span_warning(I18n.T("You are too primitive to use this device!")));
        return null;
    }

    // tg: code/modules/holodeck/items.dm:97
    public override DmTask<object> attackby(obj_item attacking_item = null, mob user = null, DmList modifiers = null, DmList attack_modifiers = null)
    {
        to_chat(user, span_warning(I18n.T("The device is a solid button, there's nothing you can do with it!")));
        return DmTask<object>.FromResult(null);
    }

    // tg: code/modules/holodeck/items.dm:100
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        if (Dm.truthy(Dm.bitand(machine_stat, Dm.bitor(NOPOWER, BROKEN))))
        {
            to_chat(user, span_warning(I18n.T("This device is not powered!")));
            return dot;
        }

        currentarea = get_area(this) as area;
        if (currentarea == null)
        {
            qdel(this);
            return dot;
        }

        if (Dm.truthy(eventstarted))
        {
            to_chat(usr, span_warning(I18n.T("The event has already begun!")));
            return dot;
        }

        ready = Dm.num(!Dm.truthy(ready));

        update_appearance();

        float numbuttons = 0;
        float numready = 0;
        foreach (DmList zlevel_turfs in Dm.each<DmList>(currentarea.get_zlevel_turf_lists()))
        {
            foreach (turf area_turf in Dm.each<turf>(zlevel_turfs))
            {
                foreach (obj_machinery_readybutton button in Dm.each<obj_machinery_readybutton>(area_turf))
                {
                    numbuttons++;
                    if (Dm.truthy(button.ready))
                        numready++;
                }
            }
        }

        if (numbuttons == numready)
            begin_event();
        return dot;
    }

    // tg: code/modules/holodeck/items.dm:130
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        if (Dm.truthy(ready) && Dm.truthy(is_operational))
        {
            Dm.append(ref dot, mutable_appearance(icon, "auth_on"));
            // DEVIATION(DV12): . += emissive_appearance(icon, "auth_on", src, alpha = src.alpha), the glow on the emissive plane, is not created
        }
        return dot;
    }

    // tg: code/modules/holodeck/items.dm:137
    public virtual void begin_event()
    {
        eventstarted = TRUE;

        foreach (DmList zlevel_turfs in Dm.each<DmList>(currentarea.get_zlevel_turf_lists()))
        {
            foreach (turf area_turf in Dm.each<turf>(zlevel_turfs))
            {
                foreach (obj_structure_window barrier in Dm.each<obj_structure_window>(area_turf))
                {
                    if (Dm.truthy(Dm.bitand(barrier.flags_1, HOLOGRAM_1))) // Just in case: only holo-windows
                        qdel(barrier);
                }

                foreach (mob contestant in Dm.each<mob>(area_turf))
                    to_chat(contestant, span_userdanger(I18n.T("FIGHT!")));
            }
        }
    }
}

// tg: code/modules/holodeck/items.dm:151
public partial class obj_machinery_conveyor_holodeck : obj_machinery_conveyor
{
    // tg: code/modules/holodeck/items.dm:153
    public override async DmTask<object> item_interaction(mob_living user = null, obj_item tool = null, DmList modifiers = null, params object[] __extra)
    {
        if (!Dm.truthy(await user.transferItemToLoc(tool, drop_location())))
            return await base.item_interaction(user, tool, modifiers, __extra);

        return ITEM_INTERACT_SUCCESS;
    }
}
