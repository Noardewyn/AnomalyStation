// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/dejavu.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps MO2 (D333): the file whole, for the chrono suit's MOD rewinder (modules_timeline.dm): after its interval the parent is put back
// where it stood, with the toxin, oxygen, stamina and brain damage it had (a carbon also gets its limbs back as they were, a simple or
// basic creature its brute damage, an object its integrity), as many times as it was given rewinds. The wizard's temporal ward kind
// (/datum/component/dejavu/wizard) comes with it; the sepia slime's rewind camera that gives the plain kind stays xenobiology's. The
// var blocks are generated (D111).

/**
 * A component to reset the parent to its previous state after some time passes
 */
// tg: code/datums/components/dejavu.dm:4
public partial class datum_component_dejavu : datum_component
{
    // tg: code/datums/components/dejavu.dm:40
    [DmParams("rewinds", "interval", "add_component")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object rewinds = Dm.arg(__extra, 0) ?? 1;
        object interval = Dm.arg(__extra, 1) ?? 10 * SECONDS;
        object add_component = Dm.arg(__extra, 2) ?? FALSE;
        if (!isatom(parent))
            return COMPONENT_INCOMPATIBLE;

        starting_turf = get_turf(parent);
        rewinds_remaining = rewinds;
        rewind_interval = interval;
        repeating_component = add_component;

        if (isliving(parent))
        {
            mob_living L = parent as mob_living;
            tox_loss = Dm.num(L.get_tox_loss());
            oxy_loss = Dm.num(L.get_oxy_loss());
            stamina_loss = Dm.num(L.get_stamina_loss());
            brain_loss = Dm.num(L.get_organ_loss(ORGAN_SLOT_BRAIN));
            rewind_type = nameof(rewind_living);
        }

        if (iscarbon(parent))
        {
            mob_living_carbon C = parent as mob_living_carbon;
            saved_bodyparts = C.save_bodyparts() as DmList;
            rewind_type = nameof(rewind_carbon);
        }
        else if (isanimal_or_basicmob(parent))
        {
            mob_living animal = parent as mob_living;
            brute_loss = animal.bruteloss;
            rewind_type = nameof(rewind_animal);
        }
        else if (isobj(parent))
        {
            obj O = parent as obj;
            integrity = O.get_integrity();
            rewind_type = nameof(rewind_obj);
        }

        _addtimer(Dm.@new<datum_callback>(this, rewind_type), Dm.num(rewind_interval), file: "code/datums/components/dejavu.dm", line: 72);
        return null;
    }

    // tg: code/datums/components/dejavu.dm:74
    public override object Destroy(float? force = null)
    {
        starting_turf = null;
        saved_bodyparts = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/dejavu.dm:79
    public virtual async DmTask rewind()
    {
        to_chat(parent, span_notice(rewind_message));

        //comes after healing so new limbs comically drop to the floor
        if (starting_turf != null)
        {
            if (!Dm.truthy(check_teleport_valid(parent as atom, starting_turf)))
                to_chat(parent, span_warning(I18n.T("For some reason, your head aches and fills with mental fog when you try to think of where you were... It feels like you're now going against some dull, unstoppable universal force.")));
            else
            {
                atom_movable master = parent as atom_movable;
                await master.forceMove(starting_turf);
            }
        }

        rewinds_remaining = Dm.num(rewinds_remaining) - 1;
        if (Dm.truthy(rewinds_remaining) || Dm.num(rewinds_remaining) < 0)
            _addtimer(Dm.@new<datum_callback>(this, rewind_type), Dm.num(rewind_interval), file: "code/datums/components/dejavu.dm", line: 92);
        else
        {
            to_chat(parent, span_notice(no_rewinds_message));
            qdel(this);
        }
    }

    // tg: code/datums/components/dejavu.dm:97
    public virtual async DmTask rewind_living()
    {
        if (Dm.eq(rewinds_remaining, 1) && Dm.truthy(repeating_component) && !iscarbon(parent) && !isanimal_or_basicmob(parent))
            await parent.AddComponent(type, 1, rewind_interval, TRUE);

        mob_living master = parent as mob_living;
        await master.set_tox_loss(tox_loss);
        await master.set_oxy_loss(oxy_loss);
        await master.set_stamina_loss(stamina_loss);
        await master.set_organ_loss(ORGAN_SLOT_BRAIN, brain_loss);
        await rewind();
    }

    // tg: code/datums/components/dejavu.dm:108
    public virtual async DmTask rewind_carbon()
    {
        if (Dm.eq(rewinds_remaining, 1) && Dm.truthy(repeating_component))
            await parent.AddComponent(type, 1, rewind_interval, TRUE);

        if (saved_bodyparts != null)
        {
            mob_living_carbon master = parent as mob_living_carbon;
            await master.apply_saved_bodyparts(saved_bodyparts);
        }
        await rewind_living();
    }

    // tg: code/datums/components/dejavu.dm:117
    public virtual async DmTask rewind_animal()
    {
        if (Dm.eq(rewinds_remaining, 1) && Dm.truthy(repeating_component))
            await parent.AddComponent(type, 1, rewind_interval, TRUE);

        mob_living master = parent as mob_living;
        master.bruteloss = Dm.num(brute_loss);
        await master.updatehealth();
        await rewind_living();
    }

    // tg: code/datums/components/dejavu.dm:126
    public virtual async DmTask rewind_obj()
    {
        if (Dm.eq(rewinds_remaining, 1) && Dm.truthy(repeating_component))
            await parent.AddComponent(type, 1, rewind_interval, TRUE);

        obj master = parent as obj;
        master.update_integrity(integrity);
        await rewind();
    }
}

///differently themed dejavu for modsuits.
// tg: code/datums/components/dejavu.dm:135
public partial class datum_component_dejavu_timeline : datum_component_dejavu
{
    // tg: code/datums/components/dejavu.dm:139
    public override async DmTask rewind()
    {
        playsound(get_turf(parent), Dm.resource("sound/items/modsuit/rewinder.ogg"), 50, TRUE);
        await base.rewind();
    }
}

// tg: code/datums/components/dejavu.dm:143
public partial class datum_component_dejavu_wizard : datum_component_dejavu
{
    // tg: code/datums/components/dejavu.dm:146
    public override async DmTask rewind()
    {
        playsound(get_turf(parent), Dm.resource("sound/items/modsuit/rewinder.ogg"), 50, TRUE);
        await base.rewind();
    }
}
