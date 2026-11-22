// Translated from tgstation (snapshot 1d99bd7f): code/game/objects/items/AI_modules/_AI_modules.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file: the AI upload's default lawset spawner (gaps G3, D291) and the AI modules, the law modules and the core and
// full lawset modules (silicons SI1, D326).

// tg: code/game/objects/items/AI_modules/_AI_modules.dm:5
public partial class obj_item_ai_module : obj_item
{
    ///defined truthy result for `handle_unique_ai()`, which makes initialize return INITIALIZE_HINT_QDEL
    // tg: code/game/objects/items/AI_modules/_AI_modules.dm:2  #define SHOULD_QDEL_MODULE (#undef at :271)
    internal const float SHOULD_QDEL_MODULE = 1;

    /// Called before being installed into a law rack. Return FALSE to block installation.
    // tg: code/game/objects/items/AI_modules/_AI_modules.dm:22
    public virtual object can_install_to_rack(mob_living user = null, obj_machinery_ai_law_rack rack = null)
    {
        return TRUE;
    }

    /// Called right before the module is added to the rack by a living mob, allowing special handling
    // tg: code/game/objects/items/AI_modules/_AI_modules.dm:26
    public virtual void pre_user_install_to_rack(mob_living user = null, obj_machinery_ai_law_rack rack = null)
    {
        return;
    }

    /// Called right before the module is removed from the rack by a living mob, allowing special handling
    // tg: code/game/objects/items/AI_modules/_AI_modules.dm:30
    public virtual async DmTask pre_user_uninstall_from_rack(mob_living user = null, obj_machinery_ai_law_rack rack = null)
    {
        return;
    }

    /// Logs the installation of this module to the law change log and silicon log.
    // tg: code/game/objects/items/AI_modules/_AI_modules.dm:34
    public virtual void log_install(mob_living user = null, obj_machinery_ai_law_rack rack = null)
    {
        log_silicon(Dm.text("[] has installed [] into [] ([])", key_name(user), this, rack, rack.log_status()));
    }

    /// Logs the uninstallation of this module to the law change log and silicon log.
    // tg: code/game/objects/items/AI_modules/_AI_modules.dm:38
    public virtual void log_uninstall(mob_living user = null, obj_machinery_ai_law_rack rack = null)
    {
        log_silicon(Dm.text("[] has removed [] from [] ([])", key_name(user), this, rack, rack.log_status()));
    }

    /// Called after a module is installed into a law rack.
    // tg: code/game/objects/items/AI_modules/_AI_modules.dm:42
    public virtual void on_rack_install(obj_machinery_ai_law_rack rack = null)
    {
        return;
    }

    /// Called when the law rack this is installed to has been linked to an AI.
    /// Also called when installed to a rack that already has an AI linked to it.
    // tg: code/game/objects/items/AI_modules/_AI_modules.dm:47
    public virtual async DmTask silicon_linked_to_installed(mob_living_silicon lawed = null)
    {
        return;
    }

    /// Called after a module is uninstalled from a law rack.
    // tg: code/game/objects/items/AI_modules/_AI_modules.dm:51
    public virtual void on_rack_uninstall(obj_machinery_ai_law_rack rack = null)
    {
        return;
    }

    /// Called with the law rack this is installed to is unlinked from an AI.
    /// Also called when uninstalled from a rack that has an AI linked to it.
    // tg: code/game/objects/items/AI_modules/_AI_modules.dm:56
    public virtual void silicon_unlinked_from_installed(mob_living_silicon lawed = null)
    {
        return;
    }
}

/// When slotted into an AI law rack, adds laws! The bread and butter of AI modules.
// tg: code/game/objects/items/AI_modules/_AI_modules.dm:60
public partial class obj_item_ai_module_law : obj_item_ai_module
{
    // tg: code/game/objects/items/AI_modules/_AI_modules.dm:73
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (Dm.truthy(mapload) && HAS_TRAIT(SSstation, STATION_TRAIT_UNIQUE_AI) && Dm.truthy(is_station_level(z)) && Dm.eq(handle_unique_ai(), SHOULD_QDEL_MODULE))
            return INITIALIZE_HINT_QDEL;
        return dot;
    }

    /// Logs the installation of this module to the law change log and silicon log.
    // tg: code/game/objects/items/AI_modules/_AI_modules.dm:79
    public override void log_install(mob_living user = null, obj_machinery_ai_law_rack rack = null)
    {
        base.log_install(user, rack);
        foreach ((object law, object is_ioned) in Dm.pairs(laws))
            log_law_change(user, Dm.text("added law to [] ([], text: [], ioned: [])", rack, rack.log_status(), law, is_ioned));
    }

    /// Logs the uninstallation of this module to the law change log and silicon log.
    // tg: code/game/objects/items/AI_modules/_AI_modules.dm:85
    public override void log_uninstall(mob_living user = null, obj_machinery_ai_law_rack rack = null)
    {
        base.log_uninstall(user, rack);
        foreach ((object law, object is_ioned) in Dm.pairs(laws))
            log_law_change(user, Dm.text("removed law from [] ([], text: [], ioned: [])", rack, rack.log_status(), law, is_ioned));
    }

    // tg: code/game/objects/items/AI_modules/_AI_modules.dm:90
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        object dot = await base.examine(user, __extra);
        if (Dm.truthy(ioned))
            Dm.append(ref dot, I18n.T("This module has been damaged and should be repaired with a [].", EXAMINE_HINT("multitool")));

        object examine_laws = display_laws();
        if (Dm.truthy(examine_laws))
            Dm.append(ref dot, Dm.text("<br>[]", examine_laws));
        return dot;
    }

    // tg: code/game/objects/items/AI_modules/_AI_modules.dm:99
    public override async DmTask<object> multitool_act(mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(ioned))
            return NONE;
        balloon_alert(user, I18n.T("repairing ion damage..."));
        if (!Dm.truthy(await tool.use_tool(ismachinery(loc) ? loc : this, user, 4 * SECONDS, volume: 25, extra_checks: Dm.@new<datum_callback>(this, nameof(multitool_cb), loc, user, tool))))
            return ITEM_INTERACT_BLOCKING;
        balloon_alert(user, I18n.T("module repaired"));
        await set_ioned(FALSE);
        set_laws(saved_laws);
        saved_laws = null;
        return ITEM_INTERACT_SUCCESS;
    }

    /// Helper to set the laws list and automatically update any racks we're installed in.
    // tg: code/game/objects/items/AI_modules/_AI_modules.dm:111
    public virtual void set_laws(DmList new_laws = null)
    {
        laws = new_laws;
        update_rack_laws();
    }

    /// Removes a law by text from the module and updates any racks we're installed in.
    // tg: code/game/objects/items/AI_modules/_AI_modules.dm:116
    public virtual void remove_law(object law_text = null)
    {
        Dm.remove(laws, law_text);
        update_rack_laws();
    }

    /// Adds a law to the module and updates any racks we're installed in.
    // tg: code/game/objects/items/AI_modules/_AI_modules.dm:121
    public virtual void add_law(object law_text = null)
    {
        laws[law_text] = FALSE;
        update_rack_laws();
    }

    /// Adds a law to the module at a specific index and updates any racks we're installed in.
    // tg: code/game/objects/items/AI_modules/_AI_modules.dm:126
    public virtual void add_law_to_index(object law_text = null, object index = null)
    {
        if (isnum(index) && Dm.le(index, Dm.length(laws)))
        {
            Dm.remove(laws, law_text);
            laws.Insert(Dm.num(index), law_text);
        }
        laws[law_text] = FALSE;
        update_rack_laws();
    }

    /// Update any racks we're installed in
    // tg: code/game/objects/items/AI_modules/_AI_modules.dm:134
    public virtual void update_rack_laws()
    {
        obj_machinery_ai_law_rack_base rack_parent = (loc as obj_machinery_ai_law_rack)?.get_parent_rack() as obj_machinery_ai_law_rack_base;
        rack_parent?.update_lawset();
    }

    // tg: code/game/objects/items/AI_modules/_AI_modules.dm:138
    public virtual object multitool_cb(atom start_loc = null, mob_living user = null, obj_item tool = null)
    {
        if (!Dm.truthy(ioned))
            return FALSE;
        if (loc != start_loc)
            return FALSE;
        return TRUE;
    }

    /// Updates the "ioned" stat of the module
    // tg: code/game/objects/items/AI_modules/_AI_modules.dm:147
    public virtual async DmTask set_ioned(object new_ioned = null)
    {
        if (!Dm.truthy(ioned))
            save_laws();
        float old_ioned = ioned;
        ioned = Dm.num(new_ioned);
        if (old_ioned == ioned)
            return;
        update_appearance();

        if (Dm.truthy(ioned))
            await add_atom_colour(COLOR_RED_GRAY, TEMPORARY_COLOUR_PRIORITY);
        else
            await remove_atom_colour(TEMPORARY_COLOUR_PRIORITY, COLOR_RED_GRAY);
    }

    /// Saves whatever laws are currently programmed into the module, so they can be restored later.
    // tg: code/game/objects/items/AI_modules/_AI_modules.dm:162
    public virtual void save_laws()
    {
        saved_laws = laws.Copy();
    }

    // tg: code/game/objects/items/AI_modules/_AI_modules.dm:165
    public override async DmTask<object> attack_self(mob user = null, object modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_self(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;

        object displayed = display_laws();
        if (Dm.truthy(displayed))
        {
            to_chat(user, boxed_message(displayed));
            dot = TRUE;
        }
        if (!Dm.truthy(ioned) && Dm.truthy(user.is_holding(this)))
            dot = await configure(user);
        return dot;
    }

    /// Allows users to configure aspects of the module, if applicable.
    // tg: code/game/objects/items/AI_modules/_AI_modules.dm:178
    public virtual async DmTask<object> configure(mob user = null)
    {
        return FALSE;
    }

    /// Returns a text display of the laws for the module.
    // tg: code/game/objects/items/AI_modules/_AI_modules.dm:182
    public virtual object display_laws()
    {
        object assembled_laws = "";

        foreach (object law in Dm.each(laws))
            Dm.append(ref assembled_laws, Dm.text("\"[]\"<br>", law));

        if (Dm.truthy(assembled_laws))
            return Dm.text("<b>Programmed Law[]:</b><br>[]", (Dm.length(laws) > 1) ? "s" : "", assembled_laws);

        return null;
    }

    ///what this module should do if it is mapload spawning on a unique AI station trait round.
    // tg: code/game/objects/items/AI_modules/_AI_modules.dm:195
    public virtual object handle_unique_ai()
    {
        return SHOULD_QDEL_MODULE; //instead of the roundstart bid to un-unique the AI, there will be a research requirement for it.
    }

    // tg: code/game/objects/items/AI_modules/_AI_modules.dm:198
    public virtual void apply_to_combined_lawset(datum_ai_laws combined_lawset = null)
    {
        return;
    }
}

// tg: code/game/objects/items/AI_modules/_AI_modules.dm:201
public partial class obj_item_ai_module_law_core : obj_item_ai_module_law
{
    // tg: code/game/objects/items/AI_modules/_AI_modules.dm:205
    public override void apply_to_combined_lawset(datum_ai_laws combined_lawset = null)
    {
        foreach ((object law_text, object is_ioned) in Dm.pairs(laws))
            combined_lawset.add_inherent_law(law_text, ioned: Dm.num(is_ioned));
    }

    // tg: code/game/objects/items/AI_modules/_AI_modules.dm:209
    public override async DmTask pre_user_uninstall_from_rack(mob_living user = null, obj_machinery_ai_law_rack rack = null)
    {
        obj_machinery_ai_law_rack_base parent_rack = rack.get_parent_rack() as obj_machinery_ai_law_rack_base;
        if (parent_rack == null || rack != parent_rack)
            return; // if we are a child, no stun

        foreach (mob_living bot in Dm.each<mob_living>(assoc_to_values(parent_rack.linked_mobs)))
        {
            // removing core laws temporarily stuns the silicon to let people swap cores without immediately getting blasted
            if (!Dm.truthy(await parent_rack.rack_stun(bot)))
                continue;
            to_chat(bot, span_userdanger(I18n.T("Core module removed. Recalculating directives...")));
        }
    }
}

// tg: code/game/objects/items/AI_modules/_AI_modules.dm:220
public partial class obj_item_ai_module_law_core_full : obj_item_ai_module_law_core
{
    // tg: code/game/objects/items/AI_modules/_AI_modules.dm:223
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        if (!Dm.truthy(law_id))
            return dot;
        object lawtype = lawid_to_type(law_id);
        if (!Dm.truthy(lawtype))
            return dot;
        datum_ai_laws core_laws = Dm.@new((Type)lawtype) as datum_ai_laws;
        laws = core_laws.inherent;
        return dot;
    }

    // tg: code/game/objects/items/AI_modules/_AI_modules.dm:233
    public override object handle_unique_ai()
    {
        object default_laws = get_round_default_lawset();
        if (Dm.eq(law_id, Dm.initial((Type)default_laws, (datum_ai_laws p) => p.id)))
            return null;
        return SHOULD_QDEL_MODULE;
    }
}

// tg: code/game/objects/items/AI_modules/_AI_modules.dm:239
public partial class obj_effect_spawner_round_default_module : obj_effect_spawner
{
    // tg: code/game/objects/items/AI_modules/_AI_modules.dm:245
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        object default_laws = get_round_default_lawset();
        //try to spawn a law board, since they may have special functionality (asimov setting subjects)
        foreach (object potential_lawboard in Dm.each(subtypesof(typeof(obj_item_ai_module_law_core_full))))
        {
            if (!Dm.eq(Dm.initial((Type)potential_lawboard, (obj_item_ai_module_law_core_full p) => p.law_id), Dm.initial((Type)default_laws, (datum_ai_laws p) => p.id)))
                continue;
            Dm.@new((Type)potential_lawboard, loc);
            return dot;
        }
        //spawn the fallback instead
        Dm.@new<obj_item_ai_module_law_core_round_default_fallback>(loc);
        return dot;
    }
}

///When the default lawset spawner cannot find a module object to spawn, it will spawn this, and this sets itself to the round default.
///This is so /datum/lawsets can be picked even if they have no module for themselves.
// tg: code/game/objects/items/AI_modules/_AI_modules.dm:259
public partial class obj_item_ai_module_law_core_round_default_fallback : obj_item_ai_module_law_core
{
    // tg: code/game/objects/items/AI_modules/_AI_modules.dm:261
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        object default_laws = get_round_default_lawset();
        datum_ai_laws default_laws__obj = Dm.@new((Type)default_laws) as datum_ai_laws;
        name = Dm.text("'[]' Core AI Module", default_laws__obj.name);
        laws = default_laws__obj.inherent;
        return dot;
    }

    // tg: code/game/objects/items/AI_modules/_AI_modules.dm:268
    public override object handle_unique_ai()
    {
        return null;
    }
}
