// Translated from tgstation (snapshot 1d99bd7f): code/game/machinery/ecto_sniffer.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the ectoscopic sniffer. A ghost's click (once per fifteen seconds per ghost, while it is powered, switched on and
// its sensor wire whole) or a pulse of its one wire makes it beep, flick and report a random level of paranormal activity; the
// ghost is told when its residue decays. A hand toggles it, a wrench anchors it, the panel opens with a screwdriver. Its powered,
// unpowered and open states. The var block is generated (D111).

// tg: code/game/machinery/ecto_sniffer.dm:1
public partial class obj_machinery_ecto_sniffer : obj_machinery
{
    // tg: code/game/machinery/ecto_sniffer.dm:18
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        set_wires(Dm.@new<datum_wires_ecto_sniffer>(this));
        return dot;
    }

    // tg: code/game/machinery/ecto_sniffer.dm:22
    public override async DmTask<object> attack_ghost(mob_dead_observer user = null)
    {
        object dot = await base.attack_ghost(user);
        if (!Dm.truthy(is_operational) || !Dm.truthy(on) || !Dm.truthy(sensor_enabled))
            return dot;

        foreach (object spirit_key in Dm.each(ectoplasmic_residues))
        {
            if (Dm.eq(spirit_key, user.ckey))
                return dot;
        }
        await activate(user);
        return dot;
    }

    // tg: code/game/machinery/ecto_sniffer.dm:32
    public virtual async DmTask activate(mob activator = null)
    {
        if (!Dm.truthy(await use_energy(active_power_usage, force: FALSE)))
            return;
        flick("ecto_sniffer_flick", this);
        playsound(loc, "sound/machines/ectoscope_beep.ogg", 75);
        await say(I18n.T("Reporting [] levels of paranormal activity!", pick(world.file2list("strings/spook_levels.txt") as DmList)));
        if (Dm.truthy(activator?.ckey))
        {
            Dm.append(ref ectoplasmic_residues, activator.ckey);
            _addtimer(Dm.@new<datum_callback>(this, nameof(clear_residue), activator.ckey), 15 * SECONDS, file: "code/game/machinery/ecto_sniffer.dm", line: 40);
        }
    }

    // tg: code/game/machinery/ecto_sniffer.dm:42
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        add_fingerprint(user);
        on = Dm.not(on);
        balloon_alert(user, I18n.T("sniffer turned []", Dm.truthy(on) ? "on" : "off"));
        update_appearance();
        return dot;
    }

    // tg: code/game/machinery/ecto_sniffer.dm:49
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        if (Dm.truthy(panel_open))
            icon_state = Dm.text("[]_open", base_icon_state);
        else
            icon_state = Dm.text("[][]", base_icon_state, (Dm.truthy(is_operational) && Dm.truthy(on)) ? null : "-p");
        return dot;
    }

    // tg: code/game/machinery/ecto_sniffer.dm:56
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        object dot = await base.update_overlays(__extra);
        // DEVIATION(DV12): if(is_operational && on) . += emissive_appearance(icon, "[base_icon_state]-light-mask", src, alpha = src.alpha),
        // the glow on the emissive plane, is not created
        return dot;
    }

    // tg: code/game/machinery/ecto_sniffer.dm:61
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        tool.play_tool_sound(this, 15);
        await set_anchored(Dm.not(anchored));
        balloon_alert(user, I18n.T("sniffer []", Dm.truthy(anchored) ? "anchored" : "unanchored"));
        return ITEM_INTERACT_SUCCESS;
    }

    // tg: code/game/machinery/ecto_sniffer.dm:67
    public override async DmTask<object> screwdriver_act(mob_living user = null, obj_item tool = null)
    {
        await DmTask.CompletedTask;
        return default_deconstruction_screwdriver(user, tool);
    }

    // tg: code/game/machinery/ecto_sniffer.dm:70
    public override async DmTask<object> crowbar_act(mob_living user = null, obj_item tool = null)
    {
        return await default_deconstruction_crowbar(user, tool);
    }

    // tg: code/game/machinery/ecto_sniffer.dm:73
    public override object Destroy(float? force = null)
    {
        ectoplasmic_residues = null;
        return base.Destroy(force);
    }

    // tg: code/game/machinery/ecto_sniffer.dm:77
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        Dm.append(ref dot, span_notice(I18n.T("Any active ghost can leave a layer of ectoplasm on the ectoscopic sniffer, causing a small, audible blip, indicating they wish to enter the world as a positronic personality.")));
        return dot;
    }

    ///Removes the ghost from the ectoplasmic_residues list and lets them know they are free to activate the sniffer again.
    // tg: code/game/machinery/ecto_sniffer.dm:83
    public virtual void clear_residue(object ghost_ckey = null)
    {
        Dm.remove(ref ectoplasmic_residues, ghost_ckey);
        mob ghost = get_mob_by_ckey(ghost_ckey) as mob;
        if (ghost == null || isliving(ghost))
            return;
        to_chat(ghost, Dm.text("[] <span class='nicegreen'>[]</span>", FOLLOW_LINK(ghost, this), I18n.T("The coating of ectoplasmic residue you left on []'s sensors has decayed.", this)));
    }
}
