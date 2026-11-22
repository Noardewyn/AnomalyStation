// Translated from tgstation (snapshot 1d99bd7f): code/datums/mutations/tongue_spike.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3, D327): the tongue spike and the chem spike: the tongue shot out as an embedding spike that morphs back,
// and the chem spike's link that sends the shooter's reagents into whoever it is stuck in. The var blocks are generated (D111).

// tg: code/datums/mutations/tongue_spike.dm:12
public partial class datum_action_cooldown_spell_tongue_spike : datum_action_cooldown_spell
{
    // tg: code/datums/mutations/tongue_spike.dm:24
    public override async DmTask<object> is_valid_target(atom cast_on = null)
    {
        await DmTask.CompletedTask;
        return iscarbon(cast_on) ? TRUE : FALSE;
    }

    // tg: code/datums/mutations/tongue_spike.dm:27
    public override async DmTask<object> cast(atom target = null)
    {
        mob_living_carbon cast_on = target as mob_living_carbon;
        object dot = await base.cast(target);
        if (HAS_TRAIT(cast_on, TRAIT_NODISMEMBER))
        {
            to_chat(cast_on, span_notice(I18n.T("You concentrate really hard, but nothing happens.")));
            return dot;
        }

        obj_item_organ_tongue to_fire = Dm.locate_type(typeof(obj_item_organ_tongue), cast_on.organs) as obj_item_organ_tongue;
        if (to_fire == null)
        {
            to_chat(cast_on, span_notice(I18n.T("You don't have a tongue to shoot!")));
            return dot;
        }

        await to_fire.Remove(cast_on, special: TRUE);
        obj_item_hardened_spike spike = Dm.@new(spike_path, get_turf(cast_on), cast_on) as obj_item_hardened_spike;
        await to_fire.forceMove(spike);
        await spike.throw_at(get_edge_target_turf(cast_on, cast_on.dir) as atom, 14, 4, cast_on);
        return dot;
    }
}

// tg: code/datums/mutations/tongue_spike.dm:43
public partial class obj_item_hardened_spike : obj_item
{
    // tg: code/datums/mutations/tongue_spike.dm:61
    [DmParams("mapload", "source")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        mob_living_carbon source = Dm.arg(__extra, 0) as mob_living_carbon;
        object dot = base.Initialize(mapload, __extra);
        fired_by_ref = WEAKREF(source) as datum_weakref;
        _addtimer(Dm.@new<datum_callback>(this, nameof(check_morph)), 5 * SECONDS, file: "code/datums/mutations/tongue_spike.dm", line: 64);
        return dot;
    }

    // tg: code/datums/mutations/tongue_spike.dm:66
    public virtual async DmTask check_morph()
    {
        // Failed to embed, morph back
        if (embed_data?.owner == null)
            await morph_back();
    }

    // tg: code/datums/mutations/tongue_spike.dm:71
    public virtual async DmTask morph_back()
    {
        visible_message(span_warning(I18n.T("[] cracks and twists, changing shape!", this)));
        foreach (obj tongue in Dm.each_cast<obj>(contents))
            await tongue.forceMove(get_turf(this));
        qdel(this);
    }
}

// tg: code/datums/mutations/tongue_spike.dm:77
public partial class datum_embedding_tongue_spike : datum_embedding
{
    // tg: code/datums/mutations/tongue_spike.dm:84
    public override async DmTask<object> stop_embedding()
    {
        object dot = await base.stop_embedding();
        obj_item_hardened_spike tongue_spike = parent as obj_item_hardened_spike;
        if (!QDELETED(tongue_spike)) // This can cause a qdel loop
            await tongue_spike.morph_back();
        return dot;
    }
}

// tg: code/datums/mutations/tongue_spike.dm:118
public partial class datum_embedding_tongue_spike_chem : datum_embedding_tongue_spike
{
    // tg: code/datums/mutations/tongue_spike.dm:122
    public override async DmTask on_successful_embed(mob_living_carbon victim = null, obj_item_bodypart target_limb = null)
    {
        obj_item_hardened_spike_chem tongue_spike = parent as obj_item_hardened_spike_chem;
        mob_living_carbon fired_by = tongue_spike.fired_by_ref?.resolve() as mob_living_carbon;
        if (fired_by == null)
            return;

        datum_action_send_chems chem_action = await Dm.new_async(typeof(datum_action_send_chems), tongue_spike) as datum_action_send_chems;
        chem_action.transferred_ref = WEAKREF(victim) as datum_weakref;
        await chem_action.Grant(fired_by);

        to_chat(fired_by, span_notice(I18n.T("Link established! Use the \"Transfer Chemicals\" ability to send your chemicals to the linked target!")));
    }

    // tg: code/datums/mutations/tongue_spike.dm:135
    public override async DmTask<object> stop_embedding()
    {
        object dot = await base.stop_embedding();
        obj_item_hardened_spike_chem tongue_spike = parent as obj_item_hardened_spike_chem;
        mob_living_carbon fired_by = tongue_spike.fired_by_ref?.resolve() as mob_living_carbon;
        if (fired_by == null)
            return dot;

        to_chat(fired_by, span_warning(I18n.T("Link lost!")));
        datum_action_send_chems chem_action = Dm.locate_type(typeof(datum_action_send_chems), fired_by.actions) as datum_action_send_chems;
        qdel(chem_action);
        return dot;
    }
}

// tg: code/datums/mutations/tongue_spike.dm:146
public partial class datum_action_send_chems : datum_action
{
    // tg: code/datums/mutations/tongue_spike.dm:157
    [DmParams("Target")]
    public override async DmTask New(params object[] __extra)
    {
        await base.New(__extra);
        if (target is not obj_item_hardened_spike_chem)
            qdel(this);
    }

    // tg: code/datums/mutations/tongue_spike.dm:162
    public override async DmTask<object> Trigger(mob clicker = null, object trigger_flags = null, params object[] __extra)
    {
        object dot = await base.Trigger(clicker, trigger_flags, __extra);
        if (!Dm.truthy(dot))
            return FALSE;
        if (!ishuman(owner) || owner.reagents == null)
            return FALSE;
        mob_living_carbon_human transferer = owner as mob_living_carbon_human;
        mob_living_carbon_human transferred = transferred_ref?.resolve() as mob_living_carbon_human;
        if (!ishuman(transferred))
            return FALSE;

        to_chat(transferred, span_warning(I18n.T("You feel a tiny prick!")));
        datum_reagents reagents = transferer.reagents as datum_reagents;
        await reagents.trans_to(transferred, reagents.total_volume, transferred_by: transferer);

        obj_item_hardened_spike_chem chem_spike = target as obj_item_hardened_spike_chem;

        // This is where it would deal damage, if it transfers chems it removes itself so no damage
        mob_living_carbon spike_owner = (chem_spike.get_embed() as datum_embedding)?.owner;
        // Message first because it'll shift back into a tongue right after moving
        if (spike_owner != null)
            spike_owner.visible_message(span_notice(I18n.T("[] falls out of []!", chem_spike, spike_owner)));
        await chem_spike.forceMove(get_turf(chem_spike));
        return TRUE;
    }
}
