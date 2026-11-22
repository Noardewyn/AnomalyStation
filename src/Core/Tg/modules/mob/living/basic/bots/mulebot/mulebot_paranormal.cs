// Translated from tgstation (snapshot 1d99bd7f): code/modules/mob/living/basic/bots/mulebot/mulebot_paranormal.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package C3).

// tg: code/modules/mob/living/basic/bots/mulebot/mulebot_paranormal.dm:1
public partial class mob_living_basic_bot_mulebot_paranormal : mob_living_basic_bot_mulebot
{
    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_paranormal.dm:9
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        DmList dot = await base.update_overlays(__extra) as DmList;
        if (!isobserver(load))
            return dot;
        mutable_appearance ghost_overlay = mutable_appearance(Dm.resource("icons/mob/simple/mob.dmi"), "ghost", layer + 0.01f) as mutable_appearance; //use a generic ghost icon, otherwise you can metagame who's dead if they have a custom ghost set
        ghost_overlay.pixel_y = 12;
        Dm.append(ref dot, ghost_overlay);
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_paranormal.dm:17
    public override object get_load_name() //Don't reveal the name of ghosts so we can't metagame who died and all that.
    {
        object dot = base.get_load_name();
        if (Dm.truthy(dot) && isobserver(load))
            return "Unknown";
        return dot;
    }

    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_paranormal.dm:22
    public override async DmTask load__proc(atom_movable atom_to_load = null)
    {
        atom_movable movable_atom = atom_to_load;
        if (load != null || Dm.truthy(movable_atom.anchored))
            return;

        if (!isturf(movable_atom.loc)) //To prevent the loading from stuff from someone's inventory or screen icons.
            return;

        if (isobserver(movable_atom))
        {
            visible_message(span_warning("A ghostly figure appears on [src]!".Replace("[src]", Dm.text("[]", this))));
            await movable_atom.forceMove(this);
            RegisterSignal(movable_atom, COMSIG_MOVABLE_MOVED, nameof(ghost_moved));
        }
        else if (!Dm.truthy((wires as datum_wires).is_cut(WIRE_LOADCHECK)))
        {
            buzz(MULEBOT_MOOD_SIGH);
            return; // if not hacked, only allow ghosts to be loaded
        }
        else if (movable_atom is obj object_to_load)
        {
            if (Dm.truthy(object_to_load.has_buckled_mobs()) || Dm.locate<mob>(movable_atom) != null) //can't load non crates objects with mobs buckled to it or inside it.
            {
                buzz(MULEBOT_MOOD_SIGH);
                return;
            }

            if (movable_atom is obj_structure_closet_crate crate)
                await crate.close(); //make sure it's closed

            await movable_atom.forceMove(this);
        }
        else if (isliving(movable_atom) && !Dm.truthy(await load_mob(movable_atom as mob_living)))
            return;

        load = movable_atom;
        update_bot_mode(new_mode: BOT_IDLE);
        update_appearance();
    }

    ///Handles the ghosts moving out from the mule
    // tg: code/modules/mob/living/basic/bots/mulebot/mulebot_paranormal.dm:55
    [SignalHandler]
    public virtual void ghost_moved()
    {
        visible_message(span_notice("The ghostly figure vanishes..."));
        UnregisterSignal(load, COMSIG_MOVABLE_MOVED);
        Waiting.NoSleep(unload());
    }
}
