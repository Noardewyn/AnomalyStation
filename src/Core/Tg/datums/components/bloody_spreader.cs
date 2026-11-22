// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/bloody_spreader.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps package H2, D333): the meat's blood on whoever handles it (the meat food and the meat material). The var block
// is generated (D111); Initialize's static hand_signals is a TgState field (rules 10.6).

// tg: code/datums/components/bloody_spreader.dm:1
public partial class datum_component_bloody_spreader : datum_component
{
    // tg: code/datums/components/bloody_spreader.dm:14  Initialize's var/static/list/hand_signals (rules 10.6)
    private static DmList Initialize__hand_signals => TgState.Current.datum_component_bloody_spreader__Initialize__hand_signals ??= Dm.list(
        COMSIG_ATOM_HULK_ATTACK,
        COMSIG_ITEM_ATTACK_SELF,
        COMSIG_ITEM_ATTACK_SELF_SECONDARY,
        COMSIG_ITEM_DROPPED
    );

    // tg: code/datums/components/bloody_spreader.dm:10
    [DmParams("blood_left", "blood_dna", "diseases")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object blood_left = Dm.arg(__extra, 0) ?? INFINITY;
        DmList blood_dna = Dm.arg(__extra, 1) as DmList ?? Dm.list(Dm.kv(((datum_blood_type)get_blood_type(typeof(datum_blood_type_meat))).dna_string, get_blood_type(typeof(datum_blood_type_meat))));
        DmList diseases = Dm.arg(__extra, 2) as DmList;
        if (!isatom(parent))
            return COMPONENT_INCOMPATIBLE;

        DmList hand_signals = Initialize__hand_signals;
        RegisterSignals(parent, hand_signals, nameof(spread_to_hands));

        if (isstructure(parent))
        {
            RegisterSignal(parent, COMSIG_ATOM_ATTACK_HAND, nameof(spread_to_hands));
            RegisterSignal(parent, COMSIG_ATOM_ATTACK_HAND_SECONDARY, nameof(spread_to_hands));
        }

        RegisterSignal(parent, COMSIG_ATOM_ATTACKBY, nameof(spread_to_atom));
        RegisterSignal(parent, COMSIG_ITEM_ATTACK_ATOM, nameof(spread_to_atom));
        RegisterSignal(parent, COMSIG_ITEM_ATTACK, nameof(spread_to_atom));
        RegisterSignal(parent, COMSIG_ITEM_HIT_REACT, nameof(spread_to_atom));
        RegisterSignal(parent, COMSIG_ATOM_ENTERED, nameof(spread_to_atom));
        RegisterSignal(parent, COMSIG_MOVABLE_BUMP, nameof(spread_to_atom));
        RegisterSignal(parent, COMSIG_MOVABLE_IMPACT, nameof(spread_to_atom));

        RegisterSignal(parent, COMSIG_ITEM_EQUIPPED, nameof(spread_on_equip));
        RegisterSignal(parent, COMSIG_ATOM_STORED_ITEM, nameof(spread_on_stored));

        if (isclothing(parent))
            await parent.AddComponent(typeof(datum_component_bloodysoles));

        this.blood_left = blood_left;
        this.blood_dna = blood_dna;
        this.diseases = diseases;
        return null;
    }

    // tg: code/datums/components/bloody_spreader.dm:44
    [DmParams("new_comp", "i_am_original", "blood_left", "blood_dna", "diseases")]
    public override async DmTask InheritComponent(datum_component C = null, object i_am_original = null, params object[] __extra)
    {
        object blood_left = Dm.arg(__extra, 0) ?? 0;
        DmList blood_dna = Dm.arg(__extra, 1) as DmList;
        DmList diseases = Dm.arg(__extra, 2) as DmList;
        Dm.combine(ref this.blood_dna, blood_dna);
        Dm.combine(ref this.diseases, diseases);
        if (Dm.num(this.blood_left) >= INFINITY)
            return;
        this.blood_left = Dm.add(this.blood_left, blood_left);
    }

    /// Apply blood to the mob who interacted with us
    // tg: code/datums/components/bloody_spreader.dm:52
    [SignalHandler]
    public virtual void spread_to_hands(atom parent = null, mob bloody_fool = null)
    {
        if (ishuman(bloody_fool))
        {
            mob_living_carbon_human as_human = (mob_living_carbon_human)bloody_fool;
            Waiting.NoSleep(as_human.add_blood_DNA_to_items(blood_dna, ITEM_SLOT_GLOVES));
            blood_left = Dm.num(blood_left) - 3;
        }
        else
        {
            Waiting.NoSleep(bloody_fool.add_blood_DNA(blood_dna, diseases));
            blood_left = Dm.num(blood_left) - 1;
        }

        if (Dm.num(blood_left) <= 0)
            qdel(this);
    }

    /// Apply blood to the atom that interacted with us, ignoring that they may be human
    // tg: code/datums/components/bloody_spreader.dm:67
    [SignalHandler]
    public virtual void spread_to_atom(atom parent = null, atom bloody_fool = null)
    {
        Waiting.NoSleep(bloody_fool.add_blood_DNA(blood_dna, diseases));
        blood_left = Dm.num(blood_left) - 1;

        if (Dm.num(blood_left) <= 0)
            qdel(this);
    }

    /// Apply blood to both the item put into us, and the mob who put it there
    // tg: code/datums/components/bloody_spreader.dm:77
    [SignalHandler]
    public virtual void spread_on_stored(atom parent = null, obj_item stored = null, mob user = null)
    {
        spread_to_atom(parent, stored);
        if (Dm.num(blood_left) > 0)
            spread_to_hands(parent, user);
    }

    /// Apply blood to the slots that our item covers when equipped
    // tg: code/datums/components/bloody_spreader.dm:85
    [SignalHandler]
    public virtual void spread_on_equip(obj_item parent = null, mob equipper = null, object slot = null)
    {
        if (!ishuman(equipper))
        {
            Waiting.NoSleep(equipper.add_blood_DNA(blood_dna, diseases));
            blood_left = Dm.num(blood_left) - 1;
            if (Dm.num(blood_left) <= 0)
                qdel(this);
            return;
        }

        mob_living_carbon_human as_human = (mob_living_carbon_human)equipper;

        float blood_slots = ITEM_SLOT_GLOVES;
        float blood_lost = 3;
        if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_LPOCKET | ITEM_SLOT_RPOCKET | ITEM_SLOT_ID)))
        {
            Dm.combine(ref blood_slots, ITEM_SLOT_ICLOTHING);
            blood_lost += 1;
        }
        else if (Dm.truthy(Dm.bitand(slot, ITEM_SLOT_SUITSTORE | ITEM_SLOT_BELT | ITEM_SLOT_BACK | ITEM_SLOT_NECK)))
        {
            Dm.combine(ref blood_slots, ITEM_SLOT_ICLOTHING | ITEM_SLOT_OCLOTHING); // Will only actually bloody one of these, thus blood_lost is increased by one instead of two
            blood_lost += 1;
        }

        if (!Dm.truthy(Dm.bitand(slot, ITEM_SLOT_HANDS)))
        {
            DmList equipment = as_human.get_equipped_items() as DmList;
            foreach (obj_item equipped in Dm.each_cast<obj_item>(equipment))
            {
                object item_slot = as_human.get_slot_by_item(equipped);
                if (Dm.truthy(Dm.bitand(item_slot, equipped.slot_flags)) && Dm.truthy(Dm.bitand(parent.body_parts_covered, equipped.body_parts_covered)))
                {
                    if (!Dm.truthy(Dm.bitand(blood_slots, item_slot)))
                        blood_lost += 1;
                    Dm.combine(ref blood_slots, item_slot);
                }
            }
        }

        if (blood_slots != ITEM_SLOT_GLOVES)
            blood_lost -= 1;

        Waiting.NoSleep(as_human.add_blood_DNA_to_items(blood_dna, blood_slots));
        blood_left = Dm.num(blood_left) - blood_lost;

        if (Dm.num(blood_left) <= 0)
            qdel(this);
    }
}

public sealed partial class TgState
{
    // tg: code/datums/components/bloody_spreader.dm:14  /datum/component/bloody_spreader/Initialize's var/static/list/hand_signals
    internal DmList datum_component_bloody_spreader__Initialize__hand_signals;
}
