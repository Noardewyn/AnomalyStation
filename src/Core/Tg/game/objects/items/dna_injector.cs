// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/dna_injector.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file but vv_edit_var (the admin var editor) (syndicate G4a, D316: the clumsiness injector and the injectors of the
// syndi-kits; gaps P3: a stored DNA's temporary transformation, which the DNA console's makeup injectors carry).
// The injectors themselves are the generated var blocks (D111).

// tg: code/game/objects/items/dna_injector.dm:1
public partial class obj_item_dnainjector : obj_item
{
    // tg: code/game/objects/items/dna_injector.dm:27
    [DmParams("mapload", "stored_dna", "damage_coeff")]
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        datum_dna new_stored_dna = Dm.arg(__extra, 0) as datum_dna;
        object new_damage_coeff = Dm.arg(__extra, 1) ?? 1;
        object dot = base.Initialize(mapload, __extra);
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_updates_onmob)));
        if (Dm.truthy(used))
            update_appearance();
        stored_dna = new_stored_dna;
        damage_coeff = Dm.num(new_damage_coeff);
        return dot;
    }

    // tg: code/game/objects/items/dna_injector.dm:35
    public override object Destroy(float? force = null)
    {
        if (stored_dna?.holder != null)
        {
            stack_trace("DNA injector got owned DNA somehow", "code/game/objects/items/dna_injector.dm", 37);
            stored_dna = null;
        }
        else
            QDEL_NULL(ref stored_dna);
        return base.Destroy(force);
    }

    // vv_edit_var (:43): the admin var editor (not ported)

    // tg: code/game/objects/items/dna_injector.dm:48
    public override async DmTask<object> update_icon_state(params object[] __extra)
    {
        object dot = await base.update_icon_state(__extra);
        string state = Dm.text("[][]", Dm.initial(this, (obj_item_dnainjector p) => p.icon_state), Dm.truthy(used) ? "0" : null);
        inhand_icon_state = state;
        icon_state = state;
        return dot;
    }

    // tg: code/game/objects/items/dna_injector.dm:52
    public override object update_desc(float? updates = null)
    {
        object dot = base.update_desc(updates);
        desc = Dm.text("[][]", Dm.initial(this, (obj_item_dnainjector p) => p.desc), Dm.truthy(used) ? "This one is used up." : null);
        return dot;
    }

    // tg: code/game/objects/items/dna_injector.dm:56
    public override async DmTask<object> attack_paw(mob user = null, DmList modifiers = null)
    {
        return await attack_hand(user, modifiers);
    }

    // tg: code/game/objects/items/dna_injector.dm:59
    public virtual async DmTask<object> inject(mob_living_carbon target = null, mob user = null)
    {
        if (!Dm.truthy(target.can_mutate()))
            return FALSE;
        if (target.stat == DEAD) //prevents dead people from having their DNA changed
        {
            to_chat(user, span_notice(I18n.T("You can't modify []'s DNA while [] dead.", target, target.p_theyre())));
            return FALSE;
        }
        foreach (object removed_mutation in Dm.each(remove_mutations))
            await target.dna.remove_mutation(removed_mutation, GLOB.standard_mutation_sources);

        foreach (System.Type added_mutation in Dm.each_cast<System.Type>(add_mutations))
        {
            if (Dm.truthy(Dm.initial(added_mutation, (datum_mutation p) => p.warn_admins_on_inject)) && target != user && !HAS_TRAIT(target, TRAIT_LESSER_HUMANOID))
                message_admins(Dm.text("[] injected [] with [] containing [].", ADMIN_LOOKUPFLW(user), key_name_admin(target), this, Dm.initial(added_mutation, (datum_mutation p) => p.name)));
            if (Dm.truthy(target.dna.mutation_in_sequence(added_mutation)))
            {
                await target.dna.activate_mutation(added_mutation);
                if (duration != INFINITY)
                    _addtimer(Dm.@new<datum_callback>(target.dna, nameof(datum_dna.remove_mutation), added_mutation, MUTATION_SOURCE_ACTIVATED), Dm.num(duration), file: "code/game/objects/items/dna_injector.dm", line: 74);
            }
            else
            {
                await target.dna.add_mutation(added_mutation, MUTATION_SOURCE_MUTATOR);
                if (duration != INFINITY)
                    _addtimer(Dm.@new<datum_callback>(target.dna, nameof(datum_dna.remove_mutation), added_mutation, MUTATION_SOURCE_MUTATOR), Dm.num(duration), file: "code/game/objects/items/dna_injector.dm", line: 78);
            }
        }

        if (stored_dna != null)
            await target.apply_status_effect(typeof(datum_status_effect_temporary_transformation_dna_injector), duration, stored_dna);
        return TRUE;
    }

    // tg: code/game/objects/items/dna_injector.dm:84
    public override async DmTask<object> attack__proc(mob_living target_mob = null, mob_living user = null, DmList modifiers = null, object attack_modifiers = null, params object[] __extra)
    {
        mob target = target_mob;
        if (!ISADVANCEDTOOLUSER(user))
        {
            to_chat(user, span_warning(I18n.T("You don't have the dexterity to do this!")));
            return null;
        }
        if (Dm.truthy(used))
        {
            to_chat(user, span_warning(I18n.T("This injector is used up!")));
            return null;
        }
        if (target is mob_living_carbon_human humantarget)
        {
            if (!Dm.truthy(humantarget.try_inject(user, injection_flags: INJECT_TRY_SHOW_ERROR_MESSAGE)))
                return null;
        }
        log_combat(user, target, "attempted to inject", this);

        if (target != user)
        {
            target.visible_message(span_danger(I18n.T("[] is trying to inject [] with []!", user, target, this)),
                span_userdanger(I18n.T("[] is trying to inject you with []!", user, this)));
            if (!Dm.truthy(await do_after(user, 3 * SECONDS, target)) || Dm.truthy(used))
                return null;
            target.visible_message(span_danger(I18n.T("[] injects [] with the syringe with []!", user, target, this)),
                span_userdanger(I18n.T("[] injects you with the syringe with []!", user, this)));
        }
        else
            to_chat(user, span_notice(I18n.T("You inject yourself with [].", this)));

        log_combat(user, target, "injected", this);

        if (!Dm.truthy(await inject(target as mob_living_carbon, user))) //Now we actually do the heavy lifting.
        {
            to_chat(user, span_notice(I18n.T("It appears that [] does not have compatible DNA.", target)));
            return null;
        }

        used = TRUE;
        update_appearance();
        return null;
    }
}

// tg: code/game/objects/items/dna_injector.dm:131
public partial class obj_item_dnainjector_activator : obj_item_dnainjector
{
    // tg: code/game/objects/items/dna_injector.dm:138
    public override async DmTask<object> inject(mob_living_carbon target = null, mob user = null)
    {
        if (!Dm.truthy(target.can_mutate()))
            return FALSE;
        foreach (object mutation__it in Dm.each(add_mutations))
        {
            object mutation = mutation__it;
            datum_mutation added_mutation = mutation as datum_mutation;
            if (istype(added_mutation, typeof(datum_mutation)))
                mutation = added_mutation.type;
            if (!Dm.truthy(await target.dna.activate_mutation(mutation__it)))
            {
                if (Dm.truthy(force_mutate))
                    await target.dna.add_mutation(mutation__it, MUTATION_SOURCE_MUTATOR);
            }
            else if (Dm.truthy(research) && target.client != null)
                filled = TRUE;
            foreach (datum_disease_advance disease in Dm.each<datum_disease_advance>(target.diseases))
            {
                foreach (datum_symptom symp in Dm.each<datum_symptom>(disease.symptoms))
                {
                    if (symp.type == typeof(datum_symptom_genetic_mutation) || symp.type == typeof(datum_symptom_viralevolution))
                        crispr_charge = TRUE;
                }
            }
            log_combat(user, target, Dm.text("[]", !Dm.truthy(force_mutate) ? "failed to inject" : "injected"), Dm.text("[] ([])[]", this, mutation, Dm.truthy(crispr_charge) ? " with CRISPR charge" : ""));
        }
        return TRUE;
    }
}
