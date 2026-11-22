// Translated from tgstation (snapshot 1d99bd7f): code/datums/components/pellet_cloud.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7x slice D: the pellet cloud of the directed kind (an ammo casing with several pellets: the shotgun shells), whole. Each pellet is a
// projectile of its own, thrown by the casing's throw_proj with the click's params and its own spread, so an aimed shot (DV94) gives every
// pellet its own 3D cone (DV96) and its own crossing test (DV95). The circular kind (a grenade, a landmine, a supply pod: create_blast_pellets,
// handle_martyrs, pew and the grenade signals) came with the syndicate campaign G2's frag grenades (D316). The var block is generated (D111).

// the following defines are used for [/datum/component/pellet_cloud/var/list/wound_info_by_part] to store the damage, wound_bonus, and bw_bonus for each bodypart hit
// tg: code/datums/components/pellet_cloud.dm:1-3  #define CLOUD_POSITION_DAMAGE 1 / CLOUD_POSITION_W_BONUS 2 / CLOUD_POSITION_BW_BONUS 3 (file-local)

// tg: code/datums/components/pellet_cloud.dm:26
public partial class datum_component_pellet_cloud : datum_component
{
    private const int CLOUD_POSITION_DAMAGE = 1;
    private const int CLOUD_POSITION_W_BONUS = 2;
    private const int CLOUD_POSITION_BW_BONUS = 3;

    // tg: code/datums/components/pellet_cloud.dm:57
    [DmParams("projectile_type", "magnitude")]
    public override async DmTask<object> Initialize(params object[] __extra)
    {
        object projectile_type = Dm.arg(__extra, 0) ?? typeof(obj_item_shrapnel);
        object magnitude = Dm.arg(__extra, 1) ?? 5;
        object dot = await base.Initialize(Dm.forward(__extra, projectile_type, magnitude));
        // isammocasing, isgrenade, islandmine, issupplypod (is_helpers.dm:297 and below)
        bool is_casing = parent is obj_item_ammo_casing;
        if (!is_casing && !(parent is obj_item_grenade) && !(parent is obj_effect_mine) && !(parent is obj_structure_closet_supplypod))
            return COMPONENT_INCOMPATIBLE;

        if (Dm.num(magnitude) < 1)
        {
            stack_trace(Dm.text("Invalid magnitude [] < 1 on pellet_cloud, parent: []", magnitude, parent), "code/datums/components/pellet_cloud.dm", 62);
            magnitude = 1;
        }

        this.projectile_type = projectile_type as Type;

        if (is_casing)
            num_pellets = magnitude;
        else
            radius = Dm.num(magnitude);
        return dot;
    }

    // tg: code/datums/components/pellet_cloud.dm:72
    public override object Destroy(float? force = null)
    {
        purple_hearts = null;
        pellets = null;
        targets_hit = null;
        wound_info_by_part = null;
        bodies = null;
        return base.Destroy(force);
    }

    // tg: code/datums/components/pellet_cloud.dm:80
    public override async DmTask RegisterWithParent()
    {
        RegisterSignal(parent, COMSIG_PREQDELETED, nameof(nullspace_parent));
        if (parent is obj_item_ammo_casing)
        {
            RegisterSignal(parent, COMSIG_FIRE_CASING, nameof(create_casing_pellets));
        }
        else if (parent is obj_item_grenade)
        {
            RegisterSignal(parent, COMSIG_GRENADE_ARMED, nameof(grenade_armed));
            RegisterSignal(parent, COMSIG_GRENADE_DETONATE, nameof(create_blast_pellets));
        }
        else if (parent is obj_effect_mine)
        {
            RegisterSignal(parent, COMSIG_MINE_TRIGGERED, nameof(create_blast_pellets));
        }
        else if (parent is obj_structure_closet_supplypod)
        {
            RegisterSignal(parent, COMSIG_SUPPLYPOD_LANDED, nameof(create_blast_pellets));
        }
    }

    // tg: code/datums/components/pellet_cloud.dm:93
    public override async DmTask UnregisterFromParent(params object[] __extra)
    {
        UnregisterSignal(parent, Dm.list(COMSIG_PREQDELETED, COMSIG_FIRE_CASING, COMSIG_GRENADE_DETONATE, COMSIG_GRENADE_ARMED, COMSIG_MOVABLE_MOVED, COMSIG_MINE_TRIGGERED, COMSIG_ITEM_DROPPED));
    }

    /**
     * create_casing_pellets() is for directed pellet clouds for ammo casings that have multiple pellets (buckshot and scatter lasers for instance)
     *
     * Honestly this is mostly just a rehash of [/obj/item/ammo_casing/proc/fire_casing] for pellet counts > 1, except this lets us tamper with the pellets and hook onto them for tracking purposes.
     * The arguments really don't matter, while this proc is triggered by COMSIG_FIRE_CASING, it's just a big mess of the state vars we need for doing the stuff over here.
     */
    // tg: code/datums/components/pellet_cloud.dm:104
    [SignalHandler]
    public virtual void create_casing_pellets(obj_item_ammo_casing shell = null, atom target = null, mob_living user = null, atom fired_from = null, object randomspread = null, object spread = null, object zone_override = null, object @params = null, object distro = null, obj_projectile proj = null)
    {
        shooter = user;
        turf target_loc = get_turf(target);
        if (!Dm.truthy(zone_override))
            zone_override = shooter.zone_selected;

        // things like mouth executions and gunpoints can multiply the damage and wounds of projectiles, so this makes sure those effects are applied to each pellet instead of just one
        float original_damage = Dm.num(shell.loaded_projectile.damage);
        float original_stamina = Dm.num(shell.loaded_projectile.stamina);
        float original_speed = Dm.num(shell.loaded_projectile.speed);
        float original_wounds_bonus = Dm.num(shell.loaded_projectile.wound_bonus);
        float original_bare_wounds_bonus = Dm.num(shell.loaded_projectile.exposed_wound_bonus);
        object original_ignored_faction = shell.loaded_projectile.get_faction();
        object original_ignored_allies = shell.loaded_projectile.allies;

        for (float i = 1; i <= Dm.num(num_pellets); i++)
        {
            Waiting.NoSleep(shell.ready_proj(target, user, SUPPRESSED_VERY, zone_override as string, fired_from));
            if (Dm.truthy(distro))
            {
                if (Dm.truthy(randomspread))
                    spread = round((rand() - 0.5f) * Dm.num(distro));
                else //Smart spread
                    spread = round((i / Dm.num(num_pellets) - 0.5f) * Dm.num(distro));
            }

            RegisterSignal(shell.loaded_projectile, COMSIG_PROJECTILE_SELF_ON_HIT, nameof(pellet_hit));
            RegisterSignals(shell.loaded_projectile, Dm.list(COMSIG_PROJECTILE_RANGE_OUT, COMSIG_QDELETING), nameof(pellet_range));
            shell.loaded_projectile.damage = original_damage;
            shell.loaded_projectile.stamina = original_stamina;
            shell.loaded_projectile.speed = original_speed;
            shell.loaded_projectile.wound_bonus = original_wounds_bonus;
            shell.loaded_projectile.exposed_wound_bonus = original_bare_wounds_bonus;
            shell.loaded_projectile.set_faction(original_ignored_faction);
            shell.loaded_projectile.set_allies(original_ignored_allies);
            Dm.append(ref pellets, shell.loaded_projectile);
            turf current_loc = get_turf(fired_from);
            if (target_loc == null || current_loc == null || shell.loaded_projectile == null)
                return;
            Waiting.Detach(shell.throw_proj(target, target_loc, shooter, @params, spread, fired_from));

            if (i != Dm.num(num_pellets))
                shell.newshot();
        }
    }

    ///One of our pellets hit something, record what it was and check if we're done (terminated == num_pellets)
    // tg: code/datums/components/pellet_cloud.dm:254
    [SignalHandler]
    public virtual void pellet_hit(obj_projectile proj = null, atom_movable firer = null, atom target = null, object Angle = null, object hit_zone = null)
    {
        Dm.remove(ref pellets, proj);
        terminated = Dm.num(terminated) + 1;
        hits = Dm.num(hits) + 1;
        obj_item_bodypart hit_part = null;
        bool damage = true;
        if (iscarbon(target) && Dm.truthy(hit_zone))
        {
            mob_living_carbon hit_carbon = target as mob_living_carbon;
            hit_part = hit_carbon.get_bodypart(hit_zone as string) as obj_item_bodypart;
            if (hit_part != null)
            {
                target = hit_part;
                if (proj.wound_bonus != CANT_WOUND) // handle wounding
                {
                    // unfortunately, due to how pellet clouds handle finalizing only after every pellet is accounted for, that also means there might be a short delay in dealing wounds if one pellet goes wide
                    // while buckshot may reach a target or miss it all in one tick, we also have to account for possible ricochets that may take a bit longer to hit the target
                    if (wound_info_by_part[hit_part] == null)
                        wound_info_by_part[hit_part] = Dm.list(0, 0, 0);
                    DmList part_info = wound_info_by_part[hit_part] as DmList;
                    part_info[CLOUD_POSITION_DAMAGE] = Dm.num(part_info[CLOUD_POSITION_DAMAGE]) + proj.damage; // these account for decay
                    part_info[CLOUD_POSITION_W_BONUS] = Dm.num(part_info[CLOUD_POSITION_W_BONUS]) + proj.wound_bonus;
                    part_info[CLOUD_POSITION_BW_BONUS] = Dm.num(part_info[CLOUD_POSITION_BW_BONUS]) + proj.exposed_wound_bonus;
                    proj.wound_bonus = CANT_WOUND; // actual wounding will be handled aggregate
                }
            }
        }
        else if (isobj(target))
        {
            obj hit_object = target as obj;
            if (Dm.num(hit_object.damage_deflection) > proj.damage || proj.damage == 0)
                damage = false;
        }

        // LAZYADDASSOC(targets_hit[target], "hits", 1) and LAZYSET(targets_hit[target], "damage", damage)
        DmList target_info = targets_hit[target] as DmList;
        if (target_info == null)
            targets_hit[target] = target_info = Dm.list();
        target_info["hits"] = Dm.num(target_info["hits"]) + 1;
        target_info["damage"] = damage ? TRUE : FALSE;
        if (Dm.eq(target_info["hits"], 1))
            RegisterSignal(target, COMSIG_QDELETING, nameof(on_target_qdel), @override: TRUE);
        UnregisterSignal(proj, Dm.list(COMSIG_QDELETING, COMSIG_PROJECTILE_RANGE_OUT, COMSIG_PROJECTILE_SELF_ON_HIT));
        if (Dm.eq(terminated, num_pellets))
            Waiting.NoSleep(finalize());
    }

    ///One of our pellets disappeared due to hitting their max range (or just somehow got qdel'd), remove it from our list and check if we're done (terminated == num_pellets)
    // tg: code/datums/components/pellet_cloud.dm:290
    [SignalHandler]
    public virtual void pellet_range(obj_projectile proj = null)
    {
        Dm.remove(ref pellets, proj);
        terminated = Dm.num(terminated) + 1;
        UnregisterSignal(proj, Dm.list(COMSIG_QDELETING, COMSIG_PROJECTILE_RANGE_OUT, COMSIG_PROJECTILE_SELF_ON_HIT));
        if (Dm.eq(terminated, num_pellets))
            Waiting.NoSleep(finalize());
    }

    ///All of our pellets are accounted for, time to go target by target and tell them how many things they got hit by.
    // tg: code/datums/components/pellet_cloud.dm:314
    public virtual async DmTask finalize()
    {
        Type proj_type = projectile_type;
        string proj_name = Dm.initial(proj_type, (obj_projectile p) => p.name);

        foreach (atom target_atom in Dm.each<atom>(targets_hit))
        {
            atom target = target_atom;
            DmList target_info = targets_hit[target] as DmList;
            float num_hits = Dm.num(target_info["hits"]);
            bool damage = Dm.truthy(target_info["damage"]);
            UnregisterSignal(target, COMSIG_QDELETING);
            obj_item_bodypart hit_part = null;
            if (isbodypart(target))
            {
                hit_part = target as obj_item_bodypart;
                if (hit_part.owner == null) //only bother doing the thing if it was a limb just laying on the ground lol.
                    hit_part = null; //so the visible_message later on doesn't generate extra text.
                else
                {
                    target = hit_part.owner;
                    if (Dm.truthy(wound_info_by_part[hit_part]) && (Dm.eq(Dm.initial(proj_type, (obj_projectile p) => p.damage_type), BRUTE) || Dm.eq(Dm.initial(proj_type, (obj_projectile p) => p.damage_type), BURN))) // so a cloud of disablers that deal stamina don't inadvertently end up causing burn wounds)
                    {
                        DmList info = wound_info_by_part[hit_part] as DmList;
                        float damage_dealt = Dm.num(info[CLOUD_POSITION_DAMAGE]);
                        float w_bonus = Dm.num(info[CLOUD_POSITION_W_BONUS]);
                        float bw_bonus = Dm.num(info[CLOUD_POSITION_BW_BONUS]);
                        string wounding_type = Dm.eq(Dm.initial(proj_type, (obj_projectile p) => p.damage_type), BRUTE) ? WOUND_BLUNT : WOUND_BURN; // sharpness is handled in the wound rolling
                        float sharpness = Dm.num(Dm.initial(proj_type, (obj_projectile p) => p.sharpness));

                        if (wounding_type == WOUND_BLUNT && sharpness != 0)
                        {
                            if (Dm.truthy(Dm.bitand(sharpness, SHARP_EDGED)))
                                wounding_type = WOUND_SLASH;
                            else if (Dm.truthy(Dm.bitand(sharpness, SHARP_POINTY)))
                                wounding_type = WOUND_PIERCE;
                        }

                        Dm.remove(ref wound_info_by_part, hit_part);

                        // technically this only checks armor worn the moment that all the pellets resolve rather than as each one hits you,
                        // but this isn't important enough to warrant all the extra loops of mostly redundant armor checks
                        mob_living_carbon hit_carbon = target as mob_living_carbon;
                        float armor_factor = Dm.num(hit_carbon.getarmor(hit_part, Dm.initial(proj_type, (obj_projectile p) => p.armor_flag)));
                        armor_factor = min(ARMOR_MAX_BLOCK, armor_factor); //cap damage reduction at 90%
                        if (armor_factor > 0)
                        {
                            if (Dm.truthy(Dm.initial(proj_type, (obj_projectile p) => p.weak_against_armour)) && armor_factor >= 0)
                                armor_factor *= ARMOR_WEAKENED_MULTIPLIER;
                            damage_dealt *= max(0, 1 - armor_factor * 0.01f);
                        }

                        await hit_part.painless_wound_roll(wounding_type, damage_dealt, w_bonus, bw_bonus, sharpness);
                    }
                }
            }

            string limb_hit_text = "";
            if (hit_part != null)
                limb_hit_text = Dm.text(" in the []", hit_part.plaintext_zone);

            if (num_hits > 1)
            {
                target.visible_message(span_danger(Dm.text("[] is hit by [] [][][][]!", target, num_hits, proj_name, plural_s(proj_name), limb_hit_text, damage ? "" : ", without leaving a mark")), null, null, COMBAT_MESSAGE_RANGE, target);
                to_chat(target, span_userdanger(Dm.text("You're hit by [] []s[]!", num_hits, proj_name, limb_hit_text)));
            }
            else
            {
                target.visible_message(span_danger(Dm.text("[] is hit by a [][][]!", target, proj_name, limb_hit_text, damage ? "" : ", without leaving a mark")), null, null, COMBAT_MESSAGE_RANGE, target);
                to_chat(target, span_userdanger(Dm.text("You're hit by a [][]!", proj_name, limb_hit_text)));
            }
        }

        foreach (mob_living martyr in Dm.each<mob_living>(purple_hearts))
        {
            if (martyr.stat == DEAD && martyr.client != null)
                Dm.Deferred("code/datums/components/pellet_cloud.dm:407", "finalize: client.give_award(/datum/award/achievement/misc/lookoutsir), the grenade martyr's achievement (achievements, M10+)");
        }
        UnregisterSignal(parent, COMSIG_PREQDELETED);
        if (Dm.truthy(queued_delete))
            qdel(parent);
        qdel(this);
    }

    // tg: code/datums/components/pellet_cloud.dm:154
    [SignalHandler]
    public virtual void create_blast_pellets(obj O = null, mob_living triggerer = null)
    {
        atom A = (atom)parent;

        if (parent is obj_item_grenade) // handle_martyrs can reduce the radius and thus the number of pellets we produce if someone dives on top of a frag grenade
        {
            Waiting.Detach(handle_martyrs(triggerer)); // INVOKE_ASYNC(src, PROC_REF(handle_martyrs), triggerer): note that we can modify radius in this proc
        }
        else if (parent is obj_effect_mine)
        {
            obj_effect_mine_shrapnel triggered_mine = parent as obj_effect_mine_shrapnel;
            if (triggered_mine == null) // a runtime in tg: every mine is cast to the shrapnel mine to read shred_triggerer
                return;
            if (Dm.truthy(triggered_mine.shred_triggerer) && triggerer != null) // free shrapnel for the idiot who stepped on it if we're a mine that shreds the triggerer
            {
                pellet_delta += Dm.num(radius); // so they don't count against the later total
                for (float i = 1; i <= Dm.num(radius); i++)
                    Waiting.Detach(pew(triggerer, TRUE)); // INVOKE_ASYNC(src, PROC_REF(pew), triggerer, TRUE)
            }
        }

        if (Dm.num(radius) < 1)
            return;

        DmList all_the_turfs_were_gonna_lacerate = RANGE_TURFS(radius, A).Copy();
        all_the_turfs_were_gonna_lacerate.Remove(RANGE_TURFS(Dm.num(radius) - 1, A));
        num_pellets = all_the_turfs_were_gonna_lacerate.len + pellet_delta;

        foreach (object T in Dm.each(all_the_turfs_were_gonna_lacerate))
        {
            turf shootat_turf = (turf)T;
            Waiting.Detach(pew(shootat_turf)); // INVOKE_ASYNC(src, PROC_REF(pew), shootat_turf)
        }
    }

    /**
     * handle_martyrs() is used for grenades that shoot shrapnel to check if anyone threw themselves/were thrown on top of the grenade, thus absorbing a good chunk of the shrapnel
     *
     * Between the time the grenade is armed and the actual detonation, we set var/list/bodies to the list of mobs currently on the new tile, as if the grenade landed on top of them, tracking if any of them move off the tile and removing them from the "under" list
     * Once the grenade detonates, handle_martyrs() is called and gets all the new mobs on the tile, and add the ones not in var/list/bodies to var/list/martyrs
     * We then iterate through the martyrs and reduce the shrapnel magnitude for each mob on top of it, shredding each of them with some of the shrapnel they helped absorb. This can snuff out all of the shrapnel if there's enough bodies
     *
     * Note we track anyone who's alive and client'd when they get shredded in var/list/purple_hearts, for achievement checking later
     */
    // tg: code/datums/components/pellet_cloud.dm:187
    public virtual async DmTask handle_martyrs(mob_living punishable_triggerer = null)
    {
        float magnitude_absorbed = 0;
        DmList martyrs = Dm.list();

        float self_harm_radius_mult = 3;

        if (punishable_triggerer != null && prob(60))
        {
            to_chat(punishable_triggerer, span_userdanger(I18n.T("Your plan to whack someone with a grenade on a stick backfires on you, literally!")));
            self_harm_radius_mult = 1; // we'll still give the guy who got hit some extra shredding, but not 3*radius
            pellet_delta += Dm.num(radius);
            for (float i = 1; i <= Dm.num(radius); i++)
                await pew(punishable_triggerer); // thought you could be tricky and lance someone with no ill effects!!
        }

        foreach (mob_living body in Dm.each<mob_living>(get_turf(parent as atom)))
        {
            if (body == shooter)
            {
                pellet_delta += Dm.num(radius) * self_harm_radius_mult;
                for (float i = 1; i <= Dm.num(radius) * self_harm_radius_mult; i++)
                    await pew(body); // free shrapnel if it goes off in your hand, and it doesn't even count towards the absorbed. fun!
            }
            else if (!Dm.@in(body, bodies))
            {
                martyrs.Add(body); // promoted from a corpse to a hero
            }
        }

        foreach (object M in Dm.each(martyrs))
        {
            mob_living martyr = (mob_living)M;
            float r = Dm.num(radius);
            if (r > 4)
            {
                martyr.visible_message(Dm.text("<b>[]</b>", span_danger(I18n.T("[] heroically covers \\the [] with [] body, absorbing a load of the shrapnel!", martyr, parent, martyr.p_their()))), span_userdanger(I18n.T("You heroically cover \\the [] with your body, absorbing a load of the shrapnel!", parent)));
                magnitude_absorbed += round(r * 0.5f);
            }
            else if (r >= 2)
            {
                martyr.visible_message(Dm.text("<b>[]</b>", span_danger(I18n.T("[] heroically covers \\the [] with [] body, absorbing some of the shrapnel!", martyr, parent, martyr.p_their()))), span_userdanger(I18n.T("You heroically cover \\the [] with your body, absorbing some of the shrapnel!", parent)));
                magnitude_absorbed += 2;
            }
            else
            {
                martyr.visible_message(Dm.text("<b>[]</b>", span_danger(I18n.T("[] heroically covers \\the [] with [] body, snuffing out the shrapnel!", martyr, parent, martyr.p_their()))), span_userdanger(I18n.T("You heroically cover \\the [] with your body, snuffing out the shrapnel!", parent)));
                magnitude_absorbed = r;
            }

            float pellets_absorbed = MathF.Pow(r, 2) - MathF.Pow(r - magnitude_absorbed - 1, 2);
            radius = r - magnitude_absorbed;
            pellet_delta -= round(pellets_absorbed * 0.5f);

            if (martyr.stat != DEAD && martyr.client != null)
            {
                LAZYADD(ref purple_hearts, martyr);
                RegisterSignal(martyr, COMSIG_QDELETING, nameof(on_target_qdel), @override: TRUE);
            }

            for (float i = 1; i <= round(pellets_absorbed * 0.5f); i++)
                await pew(martyr);

            if (Dm.num(radius) < 1)
                break;
        }
    }

    // tg: code/datums/components/pellet_cloud.dm:280
    public virtual async DmTask pew(atom target = null, object landmine_victim = null)
    {
        obj_projectile pellet = Dm.@new(projectile_type, get_turf(parent as atom)) as obj_projectile;

        //Shooting Code:
        pellet.spread = 0;
        pellet.original = target;
        pellet.fired_from = parent;
        pellet.firer = parent as atom_movable; // don't hit ourself that would be really annoying
        pellet.impacted = Dm.list(Dm.kv(WEAKREF(parent), TRUE)); // don't hit the target we hit already with the flak
        pellet.suppressed = SUPPRESSED_VERY; // set the projectiles to make no message so we can do our own aggregate message
        await pellet.aim_projectile(target, parent as atom);
        RegisterSignal(pellet, COMSIG_PROJECTILE_SELF_ON_HIT, nameof(pellet_hit));
        RegisterSignals(pellet, Dm.list(COMSIG_PROJECTILE_RANGE_OUT, COMSIG_QDELETING), nameof(pellet_range));
        pellets.Add(pellet);
        await pellet.fire();
        if (Dm.truthy(landmine_victim))
            await pellet.impact(target);
    }

    /// Look alive, we're armed! Now we start watching to see if anyone's covering us
    // tg: code/datums/components/pellet_cloud.dm:362
    [SignalHandler]
    public virtual void grenade_armed(obj_item nade = null)
    {
        if (ismob(nade.loc))
            shooter = nade.loc as mob_living;
        LAZYINITLIST(ref bodies);
        RegisterSignal(parent, COMSIG_ITEM_DROPPED, nameof(grenade_dropped));
        RegisterSignal(parent, COMSIG_MOVABLE_MOVED, nameof(grenade_moved));
        Waiting.NoSleep(AddComponent(typeof(datum_component_connect_loc_behalf), parent, grenade_armed__loc_connections));
    }

    // tg: code/datums/components/pellet_cloud.dm:370  grenade_armed's var/static/list/loc_connections (a proc-level static, rules 10.6)
    private static DmList grenade_armed__loc_connections => TgState.Current.datum_component_pellet_cloud__grenade_armed__loc_connections ??= Dm.list(
        Dm.kv(COMSIG_ATOM_EXITED, nameof(grenade_uncrossed)));

    /// Someone dropped the grenade, so set them to the shooter in case they're on top of it when it goes off
    // tg: code/datums/components/pellet_cloud.dm:376
    [SignalHandler]
    public virtual void grenade_dropped(obj_item nade = null, mob_living slick_willy = null)
    {
        shooter = slick_willy;
        grenade_moved();
    }

    /// Our grenade has moved, reset var/list/bodies so we're "on top" of any mobs currently on the tile
    // tg: code/datums/components/pellet_cloud.dm:383
    [SignalHandler]
    public virtual void grenade_moved()
    {
        LAZYCLEARLIST(bodies);
        foreach (mob_living L in Dm.each<mob_living>(get_turf(parent as atom)))
        {
            RegisterSignal(L, COMSIG_QDELETING, nameof(on_target_qdel), @override: TRUE);
            bodies.Add(L);
        }
    }

    /// Someone who was originally "under" the grenade has moved off the tile and is now eligible for being a martyr and "covering" it
    // tg: code/datums/components/pellet_cloud.dm:392
    [SignalHandler]
    public virtual void grenade_uncrossed(datum source = null, atom_movable gone = null, object direction = null)
    {
        bodies?.Remove(gone);
    }

    /// Our grenade or landmine or caseless shell or whatever tried deleting itself, so we intervene and nullspace it until we're done here
    // tg: code/datums/components/pellet_cloud.dm:395
    [SignalHandler]
    public virtual object nullspace_parent()
    {
        atom_movable AM = parent as atom_movable;
        Waiting.NoSleep(AM.moveToNullspace());
        queued_delete = TRUE;
        return TRUE;
    }

    /// Someone who was originally "under" the grenade has moved off the tile and is now eligible for being a martyr and "covering" it
    // tg: code/datums/components/pellet_cloud.dm:404
    [SignalHandler]
    public virtual void on_target_qdel(atom target = null)
    {
        UnregisterSignal(target, COMSIG_QDELETING);
        Dm.remove(ref targets_hit, target);
        if (bodies != null)
            Dm.remove(ref bodies, target);
        if (purple_hearts != null)
            Dm.remove(ref purple_hearts, target);
    }
}

public sealed partial class TgState
{
    // tg: code/datums/components/pellet_cloud.dm:370
    internal DmList datum_component_pellet_cloud__grenade_armed__loc_connections;
}
