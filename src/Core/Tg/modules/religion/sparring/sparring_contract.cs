// Translated from tgstation (snapshot 1d99bd7f): code/modules/religion/sparring/sparring_contract.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the sparring contract and its SparringContract window (the terms, the two signatures, the fight). The var block is
// generated (D111).

// tg: code/modules/religion/sparring/sparring_contract.dm:1
public partial class obj_item_sparring_contract : obj_item
{
    // tg: code/modules/religion/sparring/sparring_contract.dm:19
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        name = I18n.T("[]'s sparring contract", GLOB.deity);
        return dot;
    }

    // tg: code/modules/religion/sparring/sparring_contract.dm:23
    public override object Destroy(float? force = null)
    {
        signed_by.Cut();
        datum_religion_sect_spar sect = GLOB.religious_sect as datum_religion_sect_spar;
        if (sect != null)
            sect.existing_contract = null;
        return base.Destroy(force);
    }

    // tg: code/modules/religion/sparring/sparring_contract.dm:29
    public override async DmTask<object> ui_interact(mob user = null, datum_tgui ui = null, params object[] __extra)
    {
        ui = await SStgui.try_update_ui(user, this, ui) as datum_tgui;
        if (ui == null)
        {
            ui = Dm.@new<datum_tgui>(user, this, "SparringContract", name);
            await ui.open();
        }
        return null;
    }

    // tg: code/modules/religion/sparring/sparring_contract.dm:35
    public override async DmTask<object> ui_static_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();
        area arena = GLOB.areas_by_type[arena_condition] as area;
        data["set_weapon"] = weapons_condition;
        data["set_area"] = arena?.name;
        data["set_stakes"] = stakes_condition;
        data["possible_areas"] = get_possible_areas();
        data["stakes_holy_match"] = STAKES_HOLY_MATCH;

        return data;
    }

    // tg: code/modules/religion/sparring/sparring_contract.dm:46
    public override async DmTask<object> ui_data(mob user = null)
    {
        await DmTask.CompletedTask;
        DmList data = Dm.list();
        area arena = GLOB.areas_by_type[arena_condition] as area;
        mob_living_carbon_human left_partner = null;
        if (Dm.truthy(signed_by[1]))
            left_partner = ((datum_weakref)signed_by[1]).resolve() as mob_living_carbon_human;
        mob_living_carbon_human right_partner = null;
        if (Dm.truthy(signed_by[2]))
            right_partner = ((datum_weakref)signed_by[2]).resolve() as mob_living_carbon_human;
        // (partner in arena.contents): BYOND's area contents list the movables standing directly on its turfs, the port's list
        // only the turfs (Builtin/area.builtin.cs), so the check is a partner on a turf of the arena
        data["in_area"] = Dm.num((left_partner != null && right_partner != null && arena != null)
            && (left_partner.loc is turf && get_area(left_partner) == arena) && (right_partner.loc is turf && get_area(right_partner) == arena));
        data["no_chaplains"] = Dm.num(!Dm.truthy(left_partner?.mind?.holy_role) && !Dm.truthy(right_partner?.mind?.holy_role));
        data["left_sign"] = left_partner != null ? left_partner.real_name : "none";
        data["right_sign"] = right_partner != null ? right_partner.real_name : "none";
        return data;
    }

    // tg: code/modules/religion/sparring/sparring_contract.dm:61
    public virtual object get_possible_areas()
    {
        DmList area_names = Dm.list();
        datum_religion_sect_spar sect = GLOB.religious_sect as datum_religion_sect_spar;
        foreach (object key in Dm.each(sect.arenas))
            Dm.append(ref area_names, key);
        return area_names;
    }

    // tg: code/modules/religion/sparring/sparring_contract.dm:68
    public override async DmTask<object> ui_act(object action = null, DmList @params = null, datum_tgui ui = null, datum_ui_state state = null)
    {
        object dot = null;
        dot = await base.ui_act(action, @params, ui, state);
        if (Dm.truthy(dot))
            return dot;

        mob user = usr;

        if (!ishuman(user))
        {
            to_chat(user, span_warning(I18n.T("This contract refuses to be signed by a lesser creature such as yourself.")));
            return dot;
        }

        datum_religion_sect_spar sect = GLOB.religious_sect as datum_religion_sect_spar;

        DmList resolved_opponents = Dm.list();
        foreach (datum_weakref resolve_me in Dm.each_cast<datum_weakref>(sect.past_opponents))
        {
            object resolved = resolve_me.resolve();
            if (resolved != null)
                Dm.append(ref resolved_opponents, resolved);
        }

        if ((Dm.@in(user, resolved_opponents)) && Dm.eq(@params["stakes"], STAKES_HOLY_MATCH))
            to_chat(user, span_warning(I18n.T("This contract refuses to allow a holy match with a previous holy match loser. Pick a different stake!")));

        //any updating of the terms should update the UI to display new terms
        dot = TRUE;

        mob_living_carbon_human left_partner = null;
        if (Dm.truthy(signed_by[1]))
            left_partner = ((datum_weakref)signed_by[1]).resolve() as mob_living_carbon_human;
        mob_living_carbon_human right_partner = null;
        if (Dm.truthy(signed_by[2]))
            right_partner = ((datum_weakref)signed_by[2]).resolve() as mob_living_carbon_human;

        object sw__1 = action;
        if (Dm.eq(sw__1, "clear"))
            signed_by = Dm.list(null, null);//remove weakrefs
        else if (Dm.eq(sw__1, "fight"))
        {
            if (left_partner == null || right_partner == null || left_partner.mind == null || right_partner.mind == null)
                return dot;
            if (HAS_TRAIT(left_partner, TRAIT_SPARRING) || HAS_TRAIT(right_partner, TRAIT_SPARRING))
            {
                to_chat(user, span_warning(I18n.T("One participant is already sparring!")));
                return dot;
            }
            mob_living_carbon_human chaplain = Dm.truthy(left_partner.mind.holy_role) ? left_partner : right_partner;
            mob_living_carbon_human opponent = Dm.truthy(left_partner.mind.holy_role) ? right_partner : left_partner;
            Dm.@new<datum_sparring_match>(weapons_condition, GLOB.areas_by_type[arena_condition], stakes_condition, chaplain, opponent);
            qdel(this);
        }
        else if (Dm.eq(sw__1, "sign"))
        {
            if (user == left_partner || user == right_partner)
            {
                to_chat(user, span_warning(I18n.T("You've already signed one side of the contract.")));
                return dot;
            }
            area arena_condition_area = GLOB.areas_by_type[arena_condition] as area;
            object arena_condition_name = format_text(arena_condition_area.name);
            //setting/checking for terms changed
            float terms_changed = FALSE;
            if (!Dm.eq(@params["weapon"], weapons_condition))
            {
                if (!Dm.truthy(@params["weapon"]))
                    return dot; //they hit f5 to clear data then submitted
                terms_changed = TRUE;
                weapons_condition = Dm.num(@params["weapon"]);
            }
            if (!Dm.eq(@params["area"], arena_condition_name))
            {
                if (!Dm.truthy(@params["area"]))
                    return dot; //they hit f5 to clear data then submitted
                terms_changed = TRUE;
                Type new_area_condition = sect.arenas[@params["area"]] as Type;
                arena_condition = new_area_condition;
            }
            if (!Dm.eq(@params["stakes"], stakes_condition))
            {
                if (!Dm.truthy(@params["stakes"]))
                    return dot; //they hit f5 to clear data then submitted
                terms_changed = TRUE;
                stakes_condition = Dm.num(@params["stakes"]);
            }
            //if you change the terms you have to get the other person to sign again.
            if (Dm.truthy(terms_changed) && (left_partner != null || right_partner != null))
            {
                signed_by = Dm.list(null, null);//remove weakrefs
                to_chat(user, span_warning(I18n.T("You will need to get your sparring partner to sign again under these new terms you've set.")));
            }
            //fluff and signing
            object user_ref = WEAKREF(user);
            if (Dm.eq(@params["sign_position"], CONTRACT_LEFT_FIELD))
                signed_by[1] = user_ref;
            else
                signed_by[2] = user_ref;
        }
        return dot;
    }
}
