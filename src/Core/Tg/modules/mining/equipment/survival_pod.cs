// Translated from tgstation (snapshot 1d99bd7f): code/modules/mining/equipment/survival_pod.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using System.Linq;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The bluespace shelter capsules: used, a capsule shakes for five seconds, then loads its shelter template centred on itself (SSmapping's
// shelter templates, the converted _maps/templates/shelter_*.dmm) if the ground allows it, throwing back whoever stood too close, with
// tg's phase-in sound and a puff of smoke. The pod's furniture: the pod computer (a GPS), the survival storage unit and the forgery.
// The pod's sleeper waits for the sleeper itself, which is not ported (its overlay is translated with it).

/*****************************Survival Pod********************************/

//Survival Capsule
// tg: code/modules/mining/equipment/survival_pod.dm:12
public partial class obj_item_survivalcapsule : obj_item
{
    // tg: code/modules/mining/equipment/survival_pod.dm:27
    public virtual void get_template()
    {
        if (template != null)
            return;
        template = SSmapping.shelter_templates[template_id] as datum_map_template_shelter;
        if (template == null)
        {
            // WARNING(MSG) (__HELPERS/logging/_logging.dm:2), expanded inline (rules 11.1 (d), (f): src, usr, __FILE__, __LINE__)
            warning(Dm.text("[] in [] at line [] src: [] usr: [].", Dm.text("Shelter template ([]) not found!", template_id), "code/modules/mining/equipment/survival_pod.dm", 32, this, usr));
            qdel(this);
        }
    }

    // tg: code/modules/mining/equipment/survival_pod.dm:35
    public override object Destroy(float? force = null)
    {
        template = null; // without this, capsules would be one use. per round.
        return base.Destroy(force);
    }

    // tg: code/modules/mining/equipment/survival_pod.dm:39
    public override async DmTask<object> examine(mob user = null, params object[] __extra)
    {
        var dot = (DmList)await base.examine(user, __extra);
        get_template();
        Dm.append(ref dot, I18n.T("This capsule has the [] stored.", template.name));
        Dm.append(ref dot, template.description);
        return dot;
    }

    // tg: code/modules/mining/equipment/survival_pod.dm:45
    public override async DmTask<object> interact(mob user = null, params object[] __extra)
    {
        object dot = await base.interact(user, __extra);
        if (Dm.truthy(dot))
            return dot;

        //Can't grab when capsule is New() because templates aren't loaded then
        get_template();
        if (Dm.truthy(used))
            return FALSE;

        loc.visible_message(span_warning(I18n.T("[] begins to shake. Stand back!", this)));
        used = TRUE;
        _addtimer(Dm.@new<datum_callback>(this, nameof(expand), user), 5 * SECONDS, file: "code/modules/mining/equipment/survival_pod.dm", line: 57);
        if (iscarbon(user))
        {
            var carbon = (mob_living_carbon)user;
            carbon.throw_mode_on(THROW_MODE_TOGGLE);
        }
        return TRUE;
    }

    /// Expands the capsule into a full shelter, placing the template at the item's location (NOT triggerer's location)
    // tg: code/modules/mining/equipment/survival_pod.dm:64
    public virtual async DmTask expand(mob triggerer = null)
    {
        if (QDELETED(this))
            return;

        turf deploy_location = get_turf(this);
        object status = template.check_deploy(deploy_location, this, Dm.num(get_ignore_flags()));
        if (!Dm.eq(status, SHELTER_DEPLOY_ALLOWED))
        {
            await fail_feedback(status);
            used = FALSE;
            return;
        }

        // DEVIATION(DV612): the throws fly DV416's arc, slower than tg's quickstart steps: the shelter loads while they are still on its footprint
        if (Dm.truthy(yeet_back))
            await yote_nearby(deploy_location);
        await template.load(deploy_location, centered: TRUE);
        trigger_admin_alert(triggerer, deploy_location);
        playsound(this, Dm.resource("sound/effects/phasein.ogg"), 100, TRUE);
        Dm.@new<obj_effect_particle_effect_fluid_smoke>(get_turf(this));
        qdel(this);
    }

    /// Returns a bitfield used to ignore some checks in template.check_deploy()
    // tg: code/modules/mining/equipment/survival_pod.dm:84
    public virtual object get_ignore_flags()
    {
        return NONE;
    }

    ///Returns a message including the reason why it couldn't be deployed
    // tg: code/modules/mining/equipment/survival_pod.dm:88
    public virtual async DmTask fail_feedback(object status = null)
    {
        switch (Dm.str(status))
        {
            case SHELTER_DEPLOY_BAD_AREA:
                loc.visible_message(span_warning(I18n.T("[] will not function in this area.", this)));
                break;
            case SHELTER_DEPLOY_BAD_TURFS:
            case SHELTER_DEPLOY_ANCHORED_OBJECTS:
            case SHELTER_DEPLOY_OUTSIDE_MAP:
            case SHELTER_DEPLOY_BANNED_OBJECTS:
                loc.visible_message(span_warning(I18n.T("[] doesn't have room to deploy! You need to clear a []x[] area!", this, template.width, template.height)));
                break;
        }
        await DmTask.CompletedTask;
    }

    /// Throws any mobs near the deployed location away from the item / shelter
    /// Does some math to make closer mobs get thrown further
    // tg: code/modules/mining/equipment/survival_pod.dm:97
    public virtual async DmTask yote_nearby(turf deploy_location = null)
    {
        float width = template.width;
        float height = template.height;
        float base_x_throw_distance = Dm.ceil(width / 2);
        float base_y_throw_distance = Dm.ceil(height / 2);
        foreach (mob_living did_not_stand_back in Dm.each<mob_living>(range(loc, Dm.text("[]x[]", width, height))))
        {
            float dir_to_center = get_dir(deploy_location, did_not_stand_back);
            if (!Dm.truthy(dir_to_center))
                dir_to_center = Dm.num(pick(GLOB.alldirs));
            // Aiming to throw the target just enough to get them out of the range of the shelter
            // IE: Stronger if they're closer, weaker if they're further away
            float throw_dist = 0;
            float x_component = Math.Abs(did_not_stand_back.x - deploy_location.x);
            float y_component = Math.Abs(did_not_stand_back.y - deploy_location.y);
            if (Dm.truthy(ISDIAGONALDIR(dir_to_center)))
                throw_dist = Dm.ceil(MathF.Sqrt(base_x_throw_distance * base_x_throw_distance + base_y_throw_distance * base_y_throw_distance) - MathF.Sqrt(x_component * x_component + y_component * y_component));
            else if (Dm.truthy(Dm.bitand(dir_to_center, Dm.bitor(NORTH, SOUTH))))
                throw_dist = base_y_throw_distance - y_component + 1;
            else if (Dm.truthy(Dm.bitand(dir_to_center, Dm.bitor(EAST, WEST))))
                throw_dist = base_x_throw_distance - x_component + 1;

            await did_not_stand_back.Paralyze(3 * SECONDS);
            await did_not_stand_back.Knockdown(6 * SECONDS);
            await did_not_stand_back.throw_at(
                target: get_edge_target_turf(did_not_stand_back, dir_to_center) as atom,
                range: throw_dist,
                speed: 3,
                force: MOVE_FORCE_VERY_STRONG);
        }
    }

    /// Logs if the capsule was triggered, by default only if it happened on non-lavaland
    // tg: code/modules/mining/equipment/survival_pod.dm:126
    public virtual void trigger_admin_alert(mob triggerer = null, turf trigger_loc = null)
    {
        //only report capsules away from the mining/lavaland level
        if (Dm.truthy(is_mining_level(trigger_loc.z)))
            return;

        message_admins(Dm.text("[] activated a bluespace capsule away from the mining level! []", ADMIN_LOOKUPFLW(triggerer), ADMIN_VERBOSEJMP(trigger_loc)));
        log_admin(Dm.text("[] activated a bluespace capsule away from the mining level at []", key_name(triggerer), AREACOORD(trigger_loc)));
    }
}

//Sleeper
// tg: code/modules/mining/equipment/survival_pod.dm:191
public partial class obj_machinery_sleeper_survival_pod : obj_machinery_sleeper
{
    // tg: code/modules/mining/equipment/survival_pod.dm:196
    public override async DmTask<object> update_overlays(params object[] __extra)
    {
        var dot = (DmList)await base.update_overlays(__extra);
        if (!Dm.truthy(state_open))
            Dm.append(ref dot, "sleeper_cover");
        return dot;
    }
}

//Computer
// tg: code/modules/mining/equipment/survival_pod.dm:207
public partial class obj_item_gps_computer : obj_item_gps
{
    // tg: code/modules/mining/equipment/survival_pod.dm:215
    public override async DmTask<object> wrench_act(mob_living user = null, obj_item tool = null)
    {
        obj_item I = tool;
        await base.wrench_act(user, tool);

        user.visible_message(span_warning(I18n.T("[] disassembles [].", user, this)),
            span_notice(I18n.T("You start to disassemble []...", this)), span_hear(I18n.T("You hear clanking and banging noises.")));
        if (Dm.truthy(await I.use_tool(this, user, 20, volume: 50)))
        {
            Dm.@new<obj_item_gps>(loc);
            qdel(this);
        }
        return TRUE;
    }

    // tg: code/modules/mining/equipment/survival_pod.dm:225
    public override async DmTask<object> attack_hand(mob user = null, DmList modifiers = null, params object[] __extra)
    {
        object dot = await base.attack_hand(user, modifiers, __extra);
        if (Dm.truthy(dot))
            return dot;
        await attack_self(user);
        return dot;
    }
}

//Survival Storage Unit
// tg: code/modules/mining/equipment/survival_pod.dm:241
public partial class obj_machinery_smartfridge_survival_pod : obj_machinery_smartfridge
{
    // tg: code/modules/mining/equipment/survival_pod.dm:253
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        Waiting.NoSleep(AddElement(typeof(datum_element_update_icon_blocker)));
        Waiting.NoSleep(AddElement(typeof(datum_element_tool_blocker), TOOL_WELDER, TOOL_ACT_PRIMARY));
        Waiting.NoSleep(AddElement(typeof(datum_element_tool_blocker), TOOL_SCREWDRIVER, TOOL_ACT_PRIMARY));
        Waiting.NoSleep(AddElement(typeof(datum_element_tool_blocker), TOOL_WRENCH, TOOL_ACT_PRIMARY));
        Waiting.NoSleep(AddElement(typeof(datum_element_tool_blocker), TOOL_CROWBAR, TOOL_ACT_PRIMARY));
        return base.Initialize(mapload, __extra);
    }

    // tg: code/modules/mining/equipment/survival_pod.dm:273
    public override object accept_check(obj_item weapon = null)
    {
        return isitem(weapon) ? TRUE : FALSE;
    }
}

// tg: code/modules/mining/equipment/survival_pod.dm:261
public partial class obj_machinery_smartfridge_survival_pod_preloaded : obj_machinery_smartfridge_survival_pod
{
    // tg: code/modules/mining/equipment/survival_pod.dm:261
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        for (int i = 1; i <= 5; i++)
        {
            var W = Dm.@new<obj_item_food_donkpocket_warm>(this);
            Waiting.NoSleep(load(W));
        }
        if (prob(50))
        {
            var D = Dm.@new<obj_item_storage_dice>(this);
            Waiting.NoSleep(load(D));
        }
        else
        {
            var G = Dm.@new<obj_item_instrument_guitar>(this);
            Waiting.NoSleep(load(G));
        }
        return dot;
    }
}

// tg: code/modules/mining/equipment/survival_pod.dm:285
public partial class obj_item_fakeartefact : obj_item
{
    // tg: code/modules/mining/equipment/survival_pod.dm:289  var/static/possible = list(...) (rules 10.6: a constant list of paths, read only)
    private static readonly System.Collections.Immutable.ImmutableArray<Type> possible =
    [
        typeof(obj_item_ship_in_a_bottle),
        typeof(obj_item_gun_energy_pulse),
        typeof(obj_item_book_granter_martial_carp),
        typeof(obj_item_melee_supermatter_sword),
        typeof(obj_item_shield_changeling),
        typeof(obj_item_lava_staff),
        typeof(obj_item_energy_katana),
        typeof(obj_item_hierophant_club),
        typeof(obj_item_his_grace),
        typeof(obj_item_gun_energy_minigun),
        typeof(obj_item_gun_ballistic_automatic_l6_saw),
        typeof(obj_item_gun_magic_staff_chaos),
        typeof(obj_item_gun_magic_staff_spellblade),
        typeof(obj_item_gun_magic_wand_death),
        typeof(obj_item_gun_magic_wand_fireball),
        typeof(obj_item_stack_telecrystal_twenty),
        typeof(obj_item_nuke_core),
        typeof(obj_item_banhammer),
    ];

    // tg: code/modules/mining/equipment/survival_pod.dm:310
    public override object Initialize(object mapload = null, params object[] __extra)
    {
        object dot = base.Initialize(mapload, __extra);
        var I = (Type)pick(Dm.list(possible.Cast<object>().ToArray()));
        name = Dm.str(Dm.initial(I, (obj_item p) => p.name));
        icon = Dm.initial(I, (obj_item p) => p.icon);
        desc = Dm.str(Dm.initial(I, (obj_item p) => p.desc));
        icon_state = Dm.str(Dm.initial(I, (obj_item p) => p.icon_state));
        inhand_icon_state = Dm.str(Dm.initial(I, (obj_item p) => p.inhand_icon_state));
        lefthand_file = Dm.initial(I, (obj_item p) => p.lefthand_file);
        righthand_file = Dm.initial(I, (obj_item p) => p.righthand_file);
        cut_overlays(); //to get rid of the big blue x
        return dot;
    }
}
