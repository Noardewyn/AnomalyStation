// Translated from tgstation (snapshot 1d99bd7f): code/modules/cargo/bounty.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// Gaps P4a (D327): the whole file, the bounty base the bank accounts' civilian bounty slot holds; the bounty kinds and the consoles
// that hand them out are gaps P4c. The var blocks are generated.

// The list of all current global, shared crew bounties, does not contain any personal bounties however.
// tg: code/modules/cargo/bounty.dm:2 (interleaved types, rules 1.9)
public partial class datum_controller_global_vars : datum_controller
{
    // tg: code/modules/cargo/bounty.dm:2  GLOBAL_LIST_EMPTY(shared_crew_bounties)
    public DmList shared_crew_bounties;

    // tg: code/modules/cargo/bounty.dm:2  (GLOBAL_MANAGED(shared_crew_bounties, ...), __DEFINES/__globals.dm:5)
    public virtual void InitGlobalshared_crew_bounties()
    {
        shared_crew_bounties = Dm.list();
        Dm.append(ref gvars_datum_init_order, "shared_crew_bounties");
    }
}

// tg: code/modules/cargo/bounty.dm:4
public partial class datum_bounty : datum
{
    /// Can this bounty be claimed right now?
    // tg: code/modules/cargo/bounty.dm:24
    public virtual object can_claim()
    {
        return FALSE;
    }

    /// If an item in question can satisfy the bounty.
    // tg: code/modules/cargo/bounty.dm:28
    public virtual object applies_to(obj shipped = null)
    {
        return FALSE;
    }

    /// When calculating the contribution breakdown of an object shipped, how much does that object increase that account's cut?
    // tg: code/modules/cargo/bounty.dm:32
    public virtual object contribution_amount(obj shipped = null)
    {
        return 1;
    }

    /// Called when an object is sent on the bounty pad.
    // tg: code/modules/cargo/bounty.dm:36
    public virtual async DmTask<object> ship(obj shipped = null)
    {
        return null;
    }

    /// Formats the text for what is required to complete the bounty, for display purposes.
    // tg: code/modules/cargo/bounty.dm:40
    public virtual object print_required()
    {
        return "";
    }

    /// Returns the adjusted reward for this bounty, taking into account any global modifiers.
    // tg: code/modules/cargo/bounty.dm:44
    public virtual object get_bounty_reward()
    {
        float high_priority_mult = Dm.truthy(high_priority) ? 1.5f : 1;
        return Dm.num(reward) * SSeconomy.bounty_modifier * high_priority_mult;
    }

    /// Called when this bounty is selected by the passed ID card
    // tg: code/modules/cargo/bounty.dm:49
    public virtual void on_selected(obj_item_card_id id_card = null)
    {
        return;
    }

    /// Called when this bounty is successfully claimed by the passed ID card
    // tg: code/modules/cargo/bounty.dm:53
    public virtual void on_claimed(obj_item_card_id id_card = null)
    {
        return;
    }

    /// Called when this bounty is reset from the passed ID card, either from successful claim or from being replaced by another bounty
    // tg: code/modules/cargo/bounty.dm:57
    public virtual void on_reset(obj_item_card_id id_card = null)
    {
        return;
    }

    /// Proc that returns the current quantity of this bounty.
    // tg: code/modules/cargo/bounty.dm:61
    public virtual object get_total()
    {
        return null;
    }

    /// Proc that returns the current maximum quantity of this bounty.
    // tg: code/modules/cargo/bounty.dm:65
    public virtual object get_max()
    {
        return null;
    }

    /// If the user can actually get this bounty as a selection.
    // tg: code/modules/cargo/bounty.dm:69
    public virtual object can_get()
    {
        return TRUE;
    }
}

// tg: code/modules/cargo/bounty.dm:72 (global procs; interleaved types, rules 1.9)
public static partial class GlobalProcs
{
    /** Returns a new bounty of random type.
     *
     * * Category determines what specific catagory of bounty should be chosen.
     */
    // tg: code/modules/cargo/bounty.dm:76
    public static datum_bounty random_bounty(object category = null)
    {
        category ??= 0;
        object bounty_num = null;
        object chosen_type = null;
        bool bounty_succeeded = false;
        datum_bounty bounty_ref = null;
        while (!bounty_succeeded)
        {
            if (Dm.truthy(category) && !Dm.eq(category, CIV_JOB_RANDOM))
                bounty_num = category;
            else
                bounty_num = rand(1, MAXIMUM_BOUNTY_JOBS);
            switch (Dm.num(bounty_num))
            {
                case CIV_JOB_BASIC:
                    chosen_type = pick(subtypesof(typeof(datum_bounty_item_assistant)));
                    break;
                case CIV_JOB_ROBO:
                    chosen_type = pick(subtypesof(typeof(datum_bounty_item_mech)));
                    break;
                case CIV_JOB_CHEF:
                    chosen_type = pick(Dm.add(subtypesof(typeof(datum_bounty_item_chef)), subtypesof(typeof(datum_bounty_reagent_chef))));
                    break;
                case CIV_JOB_SEC:
                    if (prob(75))
                        chosen_type = typeof(datum_bounty_patrol);
                    else
                        chosen_type = typeof(datum_bounty_item_contraband);
                    break;
                case CIV_JOB_DRINK:
                    if (prob(50))
                        chosen_type = typeof(datum_bounty_reagent_simple_drink);
                    else
                        chosen_type = typeof(datum_bounty_reagent_complex_drink);
                    break;
                case CIV_JOB_CHEM:
                    if (prob(50))
                        chosen_type = typeof(datum_bounty_reagent_chemical_simple);
                    else
                        chosen_type = typeof(datum_bounty_reagent_chemical_complex);
                    break;
                case CIV_JOB_MED_VIRO:
                    if (prob(75))
                        chosen_type = pick(subtypesof(typeof(datum_bounty_item_medical)));
                    else
                        chosen_type = pick(subtypesof(typeof(datum_bounty_virus)));
                    break;
                case CIV_JOB_SCI:
                    if (prob(50))
                        chosen_type = pick(subtypesof(typeof(datum_bounty_item_science)));
                    else
                        chosen_type = pick(subtypesof(typeof(datum_bounty_item_slime)));
                    break;
                case CIV_JOB_ENG:
                    chosen_type = pick(subtypesof(typeof(datum_bounty_item_engineering)));
                    break;
                case CIV_JOB_MINE:
                    chosen_type = pick(subtypesof(typeof(datum_bounty_item_mining)));
                    break;
                case CIV_JOB_MED:
                    chosen_type = pick(subtypesof(typeof(datum_bounty_item_medical)));
                    break;
                case CIV_JOB_GROW:
                    chosen_type = pick(subtypesof(typeof(datum_bounty_item_botany)));
                    break;
                case CIV_JOB_ATMOS:
                    chosen_type = pick(subtypesof(typeof(datum_bounty_item_atmospherics)));
                    break;
                case CIV_JOB_BITRUN:
                    chosen_type = pick(subtypesof(typeof(datum_bounty_item_bitrunning)));
                    break;
            }
            bounty_ref = Dm.@new(chosen_type as Type) as datum_bounty;
            if (Dm.truthy(bounty_ref.can_get()))
                bounty_succeeded = true;
            else
                qdel(bounty_ref);
        }
        return bounty_ref;
    }
}
