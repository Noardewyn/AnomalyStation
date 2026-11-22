using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tg;
using Anomaly.Protocol;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Server.Tests.Server;

// Test fixtures of the port's own (rules 15): a human whose damage, healing and status procs record the call and do nothing
// else, so that "each dev command reaches its tg proc" is checked on the call itself. The real human's own procs call some of
// them too (Initialize looks up every bodypart): the spy records and stubs only while Recording is on, which Boot sets after
// the human is made, and its Life does nothing, so that SSmobs' ticks add no calls of their own.

/// <summary>A bodypart whose bleed stacks proc records its call.</summary>
[DmPath("/obj/item/bodypart/test_m2_p6_spy")]
public class obj_item_bodypart_test_m2_p6_spy : obj_item_bodypart
{
    public readonly List<string> Calls = new();

    public override void adjustBleedStacks(object adjust_by = null, float? minimum = null) => Calls.Add("adjustBleedStacks " + Dm.str(adjust_by));
}

/// <summary>A human whose dev-command procs record their calls.</summary>
[DmPath("/mob/living/carbon/human/test_m2_p6_spy")]
public class mob_living_carbon_human_test_m2_p6_spy : mob_living_carbon_human
{
    public readonly List<string> Calls = new();
    public float Brute = 7, Burn = 6, Tox = 5, Oxy = 4;
    public obj_item_bodypart_test_m2_p6_spy Part;

    /// <summary>On: the procs below record and stub; off (while the human is made): they are the real human's.</summary>
    public bool Recording;

    // The spy stands still in time: no breath, no organs, no status effects add calls between two dev commands.
    public override object Life(object seconds_per_tick = null, params object[] __extra) => null;

    public override async DmTask<object> apply_damage(float? damage = null, string damagetype = null, object def_zone = null, float? blocked = null, float? forced = null, float? spread_damage = null, float? wound_bonus = null, float? exposed_wound_bonus = null, float? sharpness = null, object attack_direction = null, object attacking_item = null, float? wound_clothing = null)
    {
        if (!Recording) return await base.apply_damage(damage, damagetype, def_zone, blocked, forced, spread_damage, wound_bonus, exposed_wound_bonus, sharpness, attack_direction, attacking_item, wound_clothing);
        Calls.Add($"apply_damage {damage} {damagetype} {Dm.str(def_zone)}");
        return damage;
    }

    public override object get_brute_loss() => Recording ? Brute : base.get_brute_loss();
    public override object get_fire_loss() => Recording ? Burn : base.get_fire_loss();
    public override object get_tox_loss() => Recording ? Tox : base.get_tox_loss();
    public override object get_oxy_loss() => Recording ? Oxy : base.get_oxy_loss();

    public override async DmTask<object> adjust_brute_loss(object amount = null, float? updating_health = null, float? forced = null, float? required_bodytype = null)
    {
        if (!Recording) return await base.adjust_brute_loss(amount, updating_health, forced, required_bodytype);
        Calls.Add("adjust_brute_loss " + Dm.str(amount));
        return amount;
    }

    public override async DmTask<object> adjust_fire_loss(object amount = null, float? updating_health = null, float? forced = null, float? required_bodytype = null)
    {
        if (!Recording) return await base.adjust_fire_loss(amount, updating_health, forced, required_bodytype);
        Calls.Add("adjust_fire_loss " + Dm.str(amount));
        return amount;
    }

    public override async DmTask<object> adjust_tox_loss(object amount = null, float? updating_health = null, float? forced = null, float? required_biotype = null)
    {
        if (!Recording) return await base.adjust_tox_loss(amount, updating_health, forced, required_biotype);
        Calls.Add("adjust_tox_loss " + Dm.str(amount));
        return amount;
    }

    public override async DmTask<object> adjust_oxy_loss(object amount = null, float? updating_health = null, float? forced = null, float? required_biotype = null)
    {
        if (!Recording) return await base.adjust_oxy_loss(amount, updating_health, forced, required_biotype);
        Calls.Add("adjust_oxy_loss " + Dm.str(amount));
        return amount;
    }

    public override async DmTask fully_heal(float? heal_flags = null)
    {
        if (!Recording)
        {
            await base.fully_heal(heal_flags);
            return;
        }
        Calls.Add("fully_heal " + heal_flags);
    }

    public override async DmTask<object> death(object gibbed = null, params object[] __extra)
    {
        if (!Recording) return await base.death(gibbed, __extra);
        Calls.Add("death");
        return null;
    }

    public override async DmTask<object> revive(float? full_heal_flags = null, float? excess_healing = null, float? force_grab_ghost = null)
    {
        if (!Recording) return await base.revive(full_heal_flags, excess_healing, force_grab_ghost);
        Calls.Add("revive " + full_heal_flags);
        return null;
    }

    public override async DmTask<object> Knockdown(object amount = null, float? daze_amount = null, float? ignore_canstun = null)
    {
        if (!Recording) return await base.Knockdown(amount, daze_amount, ignore_canstun);
        Calls.Add("Knockdown " + Dm.str(amount));
        return null;
    }

    public override async DmTask<object> Stun(object amount = null, float? ignore_canstun = null)
    {
        if (!Recording) return await base.Stun(amount, ignore_canstun);
        Calls.Add("Stun " + Dm.str(amount));
        return null;
    }

    public override object set_blood_volume(object amount = null, float? minimum = null, float? maximum = null, object cached_blood_volume = null)
    {
        if (!Recording) return base.set_blood_volume(amount, minimum, maximum, cached_blood_volume);
        Calls.Add("set_blood_volume " + Dm.str(amount));
        return amount;
    }

    public override object get_bodypart(string zone = null, float? include_stumps = null)
    {
        if (!Recording) return base.get_bodypart(zone, include_stumps);
        Calls.Add("get_bodypart " + zone);
        return zone == "chest" ? Part : null;
    }
}

/// <summary>
/// The dev commands (scope 2k, D73, Q9): their syntax (DevCommandLine) and that each one calls exactly the tg proc the table
/// names (DevCommands, on a recording human), from the console and, through the whole host, from a client.
/// </summary>
public class DevCommandTests
{
    // ---- the syntax ----

    [Theory]
    [InlineData("damage Alice brute 20", DevVerb.Damage, "Alice", DevDamageType.Brute, 20f, null)]
    [InlineData("DAMAGE me Oxy 7.5 Chest", DevVerb.Damage, "me", DevDamageType.Oxy, 7.5f, "chest")]
    [InlineData("damage #12 fire 1 l_arm", DevVerb.Damage, "#12", DevDamageType.Burn, 1f, "l_arm")]
    [InlineData("damage 12 toxin 1000 head", DevVerb.Damage, "12", DevDamageType.Tox, 1000f, "head")]
    public void DamageParses(string line, DevVerb verb, string who, DevDamageType type, float amount, string zone)
    {
        Assert.True(DevCommandLine.TryParse(line, out DevCommand c, out string error), error);
        Assert.Equal((verb, who, type, amount, zone), (c.Verb, c.Who, c.Type.Value, c.Number, c.Zone));
    }

    [Theory]
    [InlineData("heal me", DevVerb.Heal, null)]
    [InlineData("heal me tox", DevVerb.Heal, DevDamageType.Tox)]
    [InlineData("fullheal me", DevVerb.FullHeal, null)]
    [InlineData("kill #4", DevVerb.Kill, null)]
    [InlineData("revive Bob", DevVerb.Revive, null)]
    [InlineData("analyze me", DevVerb.Analyze, null)]
    [InlineData("godmode me", DevVerb.GodMode, null)]
    [InlineData("fly me", DevVerb.Fly, null)]
    [InlineData("noclip Alice", DevVerb.NoClip, null)]
    [InlineData("GOD #12", DevVerb.God, null)]
    public void TheTargetVerbsParse(string line, DevVerb verb, DevDamageType? type)
    {
        Assert.True(DevCommandLine.TryParse(line, out DevCommand c, out string error), error);
        Assert.Equal((verb, type), (c.Verb, c.Type));
        Assert.NotNull(c.Who);
    }

    // M10 slice G5 (D273): tg's admin Call Shuttle and Cancel Shuttle
    [Theory]
    [InlineData("shuttle call", "call")]
    [InlineData("SHUTTLE Call NoRecall", "call norecall")]
    [InlineData("shuttle recall", "recall")]
    public void TheShuttleVerbParsesItsMode(string line, string mode)
    {
        Assert.True(DevCommandLine.TryParse(line, out DevCommand c, out string error), error);
        Assert.Equal((DevVerb.Shuttle, mode), (c.Verb, c.Mode));
        Assert.False(DevCommandLine.TryParse("shuttle", out _, out _));
        Assert.False(DevCommandLine.TryParse("shuttle fly", out _, out _));
    }

    // M10 slice J: tg's admin Reboot World (the regular restart, with a delay)
    [Theory]
    [InlineData("reboot", 1f)]
    [InlineData("REBOOT 30", 30f)]
    public void TheRebootVerbParsesItsDelay(string line, float seconds)
    {
        Assert.True(DevCommandLine.TryParse(line, out DevCommand c, out string error), error);
        Assert.Equal((DevVerb.Reboot, seconds), (c.Verb, c.Number));
        Assert.False(DevCommandLine.TryParse("reboot 0", out _, out _));
        Assert.False(DevCommandLine.TryParse("reboot now please", out _, out _));
    }

    [Fact]
    public void TheOtherVerbsParseTheirArguments()
    {
        Assert.True(DevCommandLine.TryParse("knockdown me 2.5", out DevCommand k, out _));
        Assert.Equal((DevVerb.Knockdown, 2.5f), (k.Verb, k.Number));
        Assert.True(DevCommandLine.TryParse("stun Bob 3", out DevCommand st, out _));
        Assert.Equal((DevVerb.Stun, 3f), (st.Verb, st.Number));
        Assert.True(DevCommandLine.TryParse("bleed me chest 4", out DevCommand b, out _));
        Assert.Equal((DevVerb.Bleed, "chest", 4f), (b.Verb, b.Zone, b.Number));
        Assert.True(DevCommandLine.TryParse("blood me 300", out DevCommand bl, out _));
        Assert.Equal((DevVerb.Blood, 300f), (bl.Verb, bl.Number));
        // M8 slice E (D195): set_heartattack and losebreath
        Assert.True(DevCommandLine.TryParse("heart Bob STOP", out DevCommand he, out _));
        Assert.Equal((DevVerb.Heart, "Bob", "stop"), (he.Verb, he.Who, he.Mode));
        Assert.True(DevCommandLine.TryParse("breath me 20", out DevCommand lb, out _));
        Assert.Equal((DevVerb.Breath, 20f), (lb.Verb, lb.Number));
        // M6 slice 4: tg's dismember() and cause_wound_of_type_and_severity
        Assert.True(DevCommandLine.TryParse("dismember #12 L_ARM", out DevCommand dm, out _));
        Assert.Equal((DevVerb.Dismember, "#12", "l_arm"), (dm.Verb, dm.Who, dm.Zone));
        Assert.True(DevCommandLine.TryParse("wound me r_leg Slash critical", out DevCommand wo, out _));
        Assert.Equal((DevVerb.Wound, "r_leg", Defines.WOUND_SLASH, (float)Defines.WOUND_SEVERITY_CRITICAL), (wo.Verb, wo.Zone, wo.Mode, wo.Number));
        // M6 slice 5: tg's admin Gib, with or without the brain
        Assert.True(DevCommandLine.TryParse("gib #12", out DevCommand gi, out _));
        Assert.Equal((DevVerb.Gib, "#12", (string)null), (gi.Verb, gi.Who, gi.Mode));
        Assert.True(DevCommandLine.TryParse("gib me Brain", out DevCommand gb, out _));
        Assert.Equal((DevVerb.Gib, "brain"), (gb.Verb, gb.Mode));
        Assert.True(DevCommandLine.TryParse("speedscale 0.75", out DevCommand sp, out _));
        Assert.Equal((DevVerb.SpeedScale, 0.75f), (sp.Verb, sp.Number));
        Assert.True(DevCommandLine.TryParse("speed me 2", out DevCommand own, out _));
        Assert.Equal((DevVerb.Speed, "me", 2f), (own.Verb, own.Who, own.Number));
        Assert.True(DevCommandLine.TryParse("projspeed 3", out DevCommand ps, out _));
        Assert.Equal((DevVerb.ProjectileSpeed, 3f), (ps.Verb, ps.Number));
        Assert.True(DevCommandLine.TryParse("autorespawn on 15", out DevCommand ar, out _));
        Assert.Equal((DevVerb.AutoRespawn, "on", 15f), (ar.Verb, ar.Mode, ar.Number));
        Assert.True(DevCommandLine.TryParse("autorespawn off", out DevCommand aro, out _));
        Assert.Equal((DevVerb.AutoRespawn, "off", -1f), (aro.Verb, aro.Mode, aro.Number)); // no delay: it stays
        Assert.True(DevCommandLine.TryParse("possess #12", out DevCommand po, out _));
        Assert.Equal((DevVerb.Possess, "#12", (string)null), (po.Verb, po.Who, po.Mode));
        Assert.True(DevCommandLine.TryParse("possess Bob force", out DevCommand pf, out _));
        Assert.Equal((DevVerb.Possess, "Bob", "force"), (pf.Verb, pf.Who, pf.Mode));
        Assert.True(DevCommandLine.TryParse("unpossess", out DevCommand up, out _));
        Assert.Equal(DevVerb.Unpossess, up.Verb);
        Assert.True(DevCommandLine.TryParse("givecontrol #12 Bob force delete", out DevCommand gc, out _));
        Assert.Equal((DevVerb.GiveControl, "#12", "Bob", 1, "force"), (gc.Verb, gc.Who, gc.Mode, gc.Count, gc.Zone));
        Assert.True(DevCommandLine.TryParse("givecontrol #12 Bob", out DevCommand gc2, out _));
        Assert.Equal((0, (string)null), (gc2.Count, gc2.Zone));
        Assert.True(DevCommandLine.TryParse("gas air", out DevCommand a, out _));
        Assert.Equal((DevVerb.Gas, Defines.OPENTURF_DEFAULT_ATMOS, (int?)null), (a.Verb, a.Mix, a.X));
        Assert.True(DevCommandLine.TryParse("gas vacuum 5 6", out DevCommand v, out _));
        Assert.Equal((Defines.AIRLESS_ATMOS, 5, 6, 1), (v.Mix, v.X.Value, v.Y.Value, v.Z.Value));
        Assert.True(DevCommandLine.TryParse("gas o2=22;n2=82;TEMP=293.15 5 6 1", out DevCommand g, out _));
        Assert.Equal("o2=22;n2=82;TEMP=293.15", g.Mix);
        Assert.True(DevCommandLine.TryParse("spawn /mob/living/carbon/human 7 8", out DevCommand sw, out _));
        Assert.Equal((DevVerb.Spawn, 7, 8, "/mob/living/carbon/human", 1, (byte)0), (sw.Verb, sw.X.Value, sw.Y.Value, sw.TypePath, sw.Count, sw.Dir));
        // the owner's dev menu (2026-10-13): tg's spawn panel's amount and dir, delete, air
        Assert.True(DevCommandLine.TryParse("spawn /obj/item/wrench", out DevCommand sw2, out _));
        Assert.Equal(("/obj/item/wrench", (int?)null, 1), (sw2.TypePath, sw2.X, sw2.Count));
        Assert.True(DevCommandLine.TryParse("spawn /obj/item/wrench 3 4 1 2 0", out DevCommand sw4, out _));
        Assert.Equal((2, (byte)0), (sw4.Count, sw4.Dir)); // the dev menu's "the type's own" dir
        Assert.True(DevCommandLine.TryParse("spawn /turf/closed/wall 3 4 1 5 8", out DevCommand sw3, out _));
        Assert.Equal(("/turf/closed/wall", 3, 4, 1, 5, (byte)8), (sw3.TypePath, sw3.X.Value, sw3.Y.Value, sw3.Z.Value, sw3.Count, sw3.Dir));
        Assert.True(DevCommandLine.TryParse("delete #42", out DevCommand de, out _));
        Assert.Equal((DevVerb.Delete, "#42", (int?)null), (de.Verb, de.Who, de.X));
        // the owner's rotate tool (2026-10-15, DV56): a quarter clockwise by default, counter-clockwise, or a way
        Assert.True(DevCommandLine.TryParse("rotate #42", out DevCommand ro, out _));
        Assert.Equal((DevVerb.Rotate, "#42", "cw"), (ro.Verb, ro.Who, ro.Mode));
        Assert.True(DevCommandLine.TryParse("rot #42 CCW", out DevCommand ro2, out _));
        Assert.Equal("ccw", ro2.Mode);
        Assert.True(DevCommandLine.TryParse("rotate #42 east", out DevCommand ro3, out _));
        Assert.Equal("east", ro3.Mode);
        Assert.False(DevCommandLine.TryParse("rotate #42 up", out _, out _));
        Assert.False(DevCommandLine.TryParse("rotate", out _, out _));
        // the owner's mount tool (2026-10-30, DV56): a side of a wall
        Assert.True(DevCommandLine.TryParse("mount #42 3 4 2 EAST", out DevCommand mo, out _));
        Assert.Equal((DevVerb.Mount, "#42", 3, 4, 2, "east"), (mo.Verb, mo.Who, mo.X.Value, mo.Y.Value, mo.Z.Value, mo.Mode));
        Assert.True(DevCommandLine.TryParse("mount #42 3 4 north", out DevCommand mo2, out _));
        Assert.Null(mo2.Z);
        Assert.False(DevCommandLine.TryParse("mount #42 3 4 up", out _, out _));
        Assert.False(DevCommandLine.TryParse("mount #42 north", out _, out _));
        Assert.True(DevCommandLine.TryParse("del 3 4", out DevCommand de2, out _));
        Assert.Equal((DevVerb.Delete, 3, 4, 1), (de2.Verb, de2.X.Value, de2.Y.Value, de2.Z.Value));
        Assert.True(DevCommandLine.TryParse("air add Plasma -2.5 3 4", out DevCommand ai, out _));
        Assert.Equal((DevVerb.Air, "add", "plasma", -2.5f, 3, 4, 1, 0), (ai.Verb, ai.Mode, ai.Mix, ai.Number, ai.X.Value, ai.Y.Value, ai.Z.Value, ai.Radius));
        Assert.True(DevCommandLine.TryParse("air remove co2 3 4 1 7", out DevCommand ai2, out _));
        Assert.Equal(("remove", "co2", 7), (ai2.Mode, ai2.Mix, ai2.Radius));
        Assert.True(DevCommandLine.TryParse("air temp 500 3 4 1 2", out DevCommand ai3, out _));
        Assert.Equal(("temp", 500f, 2), (ai3.Mode, ai3.Number, ai3.Radius));
        Assert.True(DevCommandLine.TryParse("air set vacuum 3 4", out DevCommand ai4, out _));
        Assert.Equal(("set", Defines.AIRLESS_ATMOS), (ai4.Mode, ai4.Mix));
        // M4b slice 1: breach and patch take WHO (the turf WHO faces) or a map position
        Assert.True(DevCommandLine.TryParse("breach me", out DevCommand br, out _));
        Assert.Equal((DevVerb.Breach, "me", (int?)null), (br.Verb, br.Who, br.X));
        Assert.True(DevCommandLine.TryParse("BREACH 3 4", out DevCommand br2, out _));
        Assert.Equal((DevVerb.Breach, (string)null, 3, 4, 1), (br2.Verb, br2.Who, br2.X.Value, br2.Y.Value, br2.Z.Value));
        Assert.True(DevCommandLine.TryParse("patch 3 4 1", out DevCommand pa, out _));
        Assert.Equal((DevVerb.Patch, 3, 4), (pa.Verb, pa.X.Value, pa.Y.Value));
        // M4b slice 2: valve takes WHO or a map position like breach; airmode takes WHO and a mode's short name
        Assert.True(DevCommandLine.TryParse("valve me", out DevCommand va, out _));
        Assert.Equal((DevVerb.Valve, "me"), (va.Verb, va.Who));
        Assert.True(DevCommandLine.TryParse("valve 3 4", out DevCommand va2, out _));
        Assert.Equal((DevVerb.Valve, 3, 4, 1), (va2.Verb, va2.X.Value, va2.Y.Value, va2.Z.Value));
        Assert.True(DevCommandLine.TryParse("airmode me Siphon", out DevCommand am, out _));
        Assert.Equal((DevVerb.AirMode, "me", "siphon"), (am.Verb, am.Who, am.Mode));
        // M4b slice 3: ignite takes WHO or a map position like breach
        Assert.True(DevCommandLine.TryParse("ignite me", out DevCommand ig, out _));
        Assert.Equal((DevVerb.Ignite, "me"), (ig.Verb, ig.Who));
        Assert.True(DevCommandLine.TryParse("ignite 3 4 1", out DevCommand ig2, out _));
        Assert.Equal((DevVerb.Ignite, 3, 4, 1), (ig2.Verb, ig2.X.Value, ig2.Y.Value, ig2.Z.Value));
        // M5x slice 1: explode X Y [Z] DEV HEAVY LIGHT [FLASH FLAME]: the count of numbers tells whether Z and FLASH FLAME are given
        Assert.True(DevCommandLine.TryParse("explode 47 155 0 1 3", out DevCommand ex, out _));
        Assert.Equal((DevVerb.Explode, 47, 155, 1), (ex.Verb, ex.X.Value, ex.Y.Value, ex.Z.Value));
        Assert.Equal(new[] { 0f, 1f, 3f }, ex.Ranges[..3]);
        Assert.True(float.IsNaN(ex.Ranges[3]) && float.IsNaN(ex.Ranges[4]), "tg's defaults for flash and flame");
        Assert.True(DevCommandLine.TryParse("explode 47 155 2 0 1 3", out DevCommand ex2, out _));
        Assert.Equal((2, 0f, 1f, 3f), (ex2.Z.Value, ex2.Ranges[0], ex2.Ranges[1], ex2.Ranges[2]));
        Assert.True(DevCommandLine.TryParse("explode 47 155 1 2 3 4 5", out DevCommand ex3, out _));
        Assert.Equal((1, 1f, 2f, 3f, 4f, 5f), (ex3.Z.Value, ex3.Ranges[0], ex3.Ranges[1], ex3.Ranges[2], ex3.Ranges[3], ex3.Ranges[4]));
        Assert.True(DevCommandLine.TryParse("explode 47 155 1 0 1 3 0 2", out DevCommand ex4, out _));
        Assert.Equal((1, 0f, 2f), (ex4.Z.Value, ex4.Ranges[3], ex4.Ranges[4]));
    }

    [Fact]
    public void TheStatusEffectVerbsParse()
    {
        Assert.True(DevCommandLine.TryParse("effects", out DevCommand e0, out _));
        Assert.Equal((DevVerb.Effects, "me"), (e0.Verb, e0.Who));
        Assert.True(DevCommandLine.TryParse("effects #12", out DevCommand e1, out _));
        Assert.Equal((DevVerb.Effects, "#12"), (e1.Verb, e1.Who));
        Assert.True(DevCommandLine.TryParse("effect me add incapacitating/stun 5", out DevCommand a, out _));
        Assert.Equal((DevVerb.Effect, "me", "add", "incapacitating/stun", 5f), (a.Verb, a.Who, a.Mode, a.TypePath, a.Number));
        Assert.True(DevCommandLine.TryParse("EFFECT Bob ADD /datum/status_effect/staggered", out DevCommand a2, out _));
        Assert.Equal(("Bob", "add", "/datum/status_effect/staggered"), (a2.Who, a2.Mode, a2.TypePath));
        Assert.True(float.IsNaN(a2.Number)); // no N: tg's defaults
        Assert.True(DevCommandLine.TryParse("effect #4 remove stun", out DevCommand r, out _));
        Assert.Equal(("#4", "remove", "stun"), (r.Who, r.Mode, r.TypePath));
        Assert.True(DevCommandLine.TryParse("effect types", out DevCommand t, out _));
        Assert.Equal((DevVerb.Effect, "types", (string)null), (t.Verb, t.Mode, t.Who));
        Assert.Contains("effect WHO add TYPE [N]", DevCommandLine.Help);
    }

    [Theory]
    [InlineData("", "empty line")]
    [InlineData("effects me you", "usage: effects")]
    [InlineData("effect me", "usage: effect")]
    [InlineData("effect me add", "usage: effect")]
    [InlineData("effect me jump stun", "usage: effect")]
    [InlineData("effect me remove stun 5", "usage: effect")]
    [InlineData("effect me add stun 5 6", "usage: effect")]
    [InlineData("effect me add st*n", "is not a status effect type or id")]
    [InlineData("effect me add stun 0", "N (seconds or stacks) must be above 0")]
    [InlineData("effect me add stun 601", "at most 600")]
    [InlineData("levitate me", "unknown dev command 'levitate'")]
    [InlineData("damage", "usage: damage")]
    [InlineData("damage me brute", "usage: damage")]
    [InlineData("damage me brute 5 chest extra", "usage: damage")]
    [InlineData("damage me blunt 5", "not a damage type")]
    [InlineData("damage me brute 0", "damage must be above 0")]
    [InlineData("damage me brute -5", "damage must be above 0")]
    [InlineData("damage me brute 1001", "at most 1000")]
    [InlineData("damage me brute NaN", "damage must be above 0")]
    [InlineData("damage me brute 5 tail", "not a body zone")]
    [InlineData("heal me stamina", "not a damage type")]
    [InlineData("kill", "usage: kill")]
    [InlineData("kill me too", "usage: kill")]
    [InlineData("knockdown me 0", "seconds must be above 0")]
    [InlineData("stun me 61", "at most 60")]
    [InlineData("bleed me chest 0", "stacks must be")]
    [InlineData("bleed me chest 31", "stacks must be")]
    [InlineData("bleed me neck 3", "not a body zone")]
    [InlineData("dismember me", "usage: dismember")]
    [InlineData("dismember me tail", "not a body zone")]
    [InlineData("wound me chest slash", "usage: wound")]
    [InlineData("wound me chest bite severe", "not a wound type")]
    [InlineData("wound me chest burn loss", "not a wound severity")]
    [InlineData("gib", "usage: gib")]
    [InlineData("gib me head", "usage: gib")]
    [InlineData("blood me -1", "millilitres must be")]
    [InlineData("blood me 2001", "millilitres must be")]
    [InlineData("heart me", "usage: heart")]
    [InlineData("heart me pause", "usage: heart")]
    [InlineData("breath me", "usage: breath")]
    [InlineData("breath me 101", "N must be")]
    [InlineData("breath me -1", "N must be")]
    [InlineData("gas", "usage: gas")]
    [InlineData("gas air 5", "usage: gas")]
    [InlineData("gas air 0 5", "not a map position")]
    [InlineData("gas air 5 999", "not a map position")]
    [InlineData("gas air 5 5 99", "z level")]
    [InlineData("gas o2=22;n2=82 TEMP", "usage: gas")]
    [InlineData("gas o2=22/n2=82", "may hold only letters")]
    [InlineData("spawn /datum/gas/plasma", "is not an /obj, /mob or /turf type path")]
    [InlineData("spawn /obj/item/../x", "is not an /obj, /mob or /turf type path")]
    [InlineData("spawn /mob/living/carbon/human 5", "usage: spawn")]
    [InlineData("spawn /obj/item 5 5 1 101", "the amount is 1 to 100")]
    [InlineData("spawn /obj/item 5 5 1 0", "the amount is 1 to 100")]
    [InlineData("spawn /obj/item 5 5 1 1 3", "the dir is 0 (the type's own) or one of")]
    [InlineData("spawn /obj/item 5 5 1 1 2 x", "usage: spawn")]
    [InlineData("delete", "usage: delete")]
    [InlineData("delete 1 2 3 4", "usage: delete")]
    [InlineData("air", "usage: air")]
    [InlineData("air fill plasma 5 5", "is not an air operation")]
    [InlineData("air add plasma 5 5", "usage: air")]
    [InlineData("air add plasma 0 5 5", "the moles are from")]
    [InlineData("air add plas-ma 10 5 5", "is not a gas id")]
    [InlineData("air add plasma 100001 5 5", "the moles are from")]
    [InlineData("air temp 1 5 5", "the temperature is 2.7 to")]
    [InlineData("air remove plasma 5 5 1 8", "the radius is 0 to 7")]
    [InlineData("air set o2=1/n2=2 5 5", "may hold only letters")]
    [InlineData("speedscale 0", "the scale must be above 0")]
    [InlineData("speedscale 4.5", "the scale must be above 0")]
    [InlineData("speedscale fast", "the scale must be above 0")]
    [InlineData("speed me", "usage: speed WHO F")]
    [InlineData("speed me 0", "the speed must be above 0")]
    [InlineData("speed me 4.5", "the speed must be above 0")]
    [InlineData("projspeed 0", "the scale must be above 0")]
    [InlineData("projspeed 9", "the scale must be above 0")]
    [InlineData("autorespawn", "usage: autorespawn on|off [SECONDS]")]
    [InlineData("autorespawn maybe", "usage: autorespawn on|off [SECONDS]")]
    [InlineData("autorespawn on 601", "the delay is 0 to 600 seconds")]
    [InlineData("autorespawn on -1", "the delay is 0 to 600 seconds")]
    [InlineData("possess", "usage: possess WHO [force]")]
    [InlineData("possess Bob now", "usage: possess WHO [force]")]
    [InlineData("unpossess Bob", "usage: unpossess")]
    [InlineData("givecontrol #12", "usage: givecontrol WHO PLAYER [delete] [force]")]
    [InlineData("givecontrol #12 Bob delete delete", "usage: givecontrol WHO PLAYER [delete] [force]")]
    [InlineData("breach", "usage: breach WHO")]
    [InlineData("breach 3 4 1 2", "usage: breach WHO")]
    [InlineData("patch 0 4", "not a map position")]
    [InlineData("valve", "usage: valve WHO")]
    [InlineData("airmode me", "usage: airmode WHO MODE")]
    [InlineData("airmode me flood", "is not an air alarm mode")]
    [InlineData("ignite", "usage: ignite WHO")]
    [InlineData("explode 3 4 1 2", "usage: explode X Y [Z] DEV HEAVY LIGHT")]
    [InlineData("explode 3 4 1 2 3 4 5 6 7", "usage: explode X Y [Z] DEV HEAVY LIGHT")]
    [InlineData("explode 3 4 1 2 big", "a range is a number from 0 to 50")]
    [InlineData("explode 3 4 1 2 60", "a range is a number from 0 to 50")]
    public void BadLinesAreRefusedWithAReason(string line, string reason)
    {
        Assert.False(DevCommandLine.TryParse(line, out DevCommand c, out string error));
        Assert.Null(c);
        Assert.Contains(reason, error);
    }

    [Fact]
    public void LongLinesControlCharactersAndLongMixesAreRefused()
    {
        Assert.False(DevCommandLine.TryParse("godmode " + new string('a', Limits.MaxDevCommandBytes), out _, out string tooLong));
        Assert.Contains("longer than", tooLong);
        Assert.False(DevCommandLine.TryParse("godmode me\u0001", out _, out string control));
        Assert.Contains("control characters", control);
        Assert.False(DevCommandLine.TryParse("gas " + new string('a', DevCommandLine.MaxMixChars + 1), out _, out string longMix));
        Assert.Contains("longer than", longMix);
        Assert.False(DevCommandLine.TryParse(null, out _, out _));
        Assert.True(DevCommandLine.IsDevVerb("Damage"));
        Assert.False(DevCommandLine.IsDevVerb("kick"));
        Assert.False(DevCommandLine.IsDevVerb(null));
        Assert.Contains("damage WHO", DevCommandLine.Help);
    }

    // ---- DevCommands: each one reaches its tg proc ----

    private static (World World, mob_living_carbon_human_test_m2_p6_spy Spy, TestServer Server) Boot()
    {
        var server = TestServer.Start();
        mob_living_carbon_human_test_m2_p6_spy spy;
        using (server.World.Enter())
        {
            spy = Dm.@new<mob_living_carbon_human_test_m2_p6_spy>(locate(5, 5, 1));
            spy.Part = Dm.@new<obj_item_bodypart_test_m2_p6_spy>();
            spy.Recording = true;
        }
        return (server.World, spy, server);
    }

    [Fact]
    public void DamageCallsApplyDamageWithAZoneAndTheTypesAdjustWithout()
    {
        var (world, spy, server) = Boot();
        using (server)
        using (world.Enter())
        {
            string reply = DevCommands.Damage(spy, DevDamageType.Burn, 12.5f, "chest");
            Assert.Equal("apply_damage 12.5 burn chest", Assert.Single(spy.Calls));
            Assert.Contains("12.5 burn damage to the chest", reply);
            spy.Calls.Clear();
            DevCommands.Damage(spy, DevDamageType.Brute, 20f);
            DevCommands.Damage(spy, DevDamageType.Burn, 21f);
            DevCommands.Damage(spy, DevDamageType.Tox, 22f);
            DevCommands.Damage(spy, DevDamageType.Oxy, 23f);
            // Without a zone it is the type's adjust_*_loss (pinned: brute through take_overall_damage would spread over the parts).
            Assert.Equal(new[] { "adjust_brute_loss 20", "adjust_fire_loss 21", "adjust_tox_loss 22", "adjust_oxy_loss 23" }, spy.Calls);
        }
    }

    [Fact]
    public void HealCallsTheAdjustProcsWithTheNegativeOfTheLoss()
    {
        var (world, spy, server) = Boot();
        using (server)
        using (world.Enter())
        {
            DevCommands.Heal(spy, DevDamageType.Tox);
            Assert.Equal("adjust_tox_loss -5", Assert.Single(spy.Calls));
            spy.Calls.Clear();
            DevCommands.Heal(spy);
            Assert.Equal(new[] { "adjust_brute_loss -7", "adjust_fire_loss -6", "adjust_tox_loss -5", "adjust_oxy_loss -4" }, spy.Calls);
        }
    }

    [Fact]
    public void FullHealKillReviveKnockdownStunBloodAndBleedReachTheirProcs()
    {
        var (world, spy, server) = Boot();
        using (server)
        using (world.Enter())
        {
            DevCommands.FullHeal(spy);
            DevCommands.Kill(spy);
            DevCommands.Revive(spy);
            DevCommands.Knockdown(spy, 2f);
            DevCommands.Stun(spy, 1.5f);
            DevCommands.Blood(spy, 300f);
            Assert.Equal(new[]
            {
                "fully_heal " + Defines.ADMIN_HEAL_ALL, // tg's admin heal (2026-10-15: HEAL_ALL left the nutrition and the cuffs)
                "death",
                "revive " + Defines.ADMIN_HEAL_ALL, // tg's admin rejuvenate
                "Knockdown 20",                      // 2 SECONDS in deciseconds
                "Stun 15",
                "set_blood_volume 300",
            }, spy.Calls);
            spy.Calls.Clear();
            string reply = DevCommands.Bleed(spy, "chest", 3f);
            Assert.Equal("get_bodypart chest", Assert.Single(spy.Calls));
            Assert.Equal("adjustBleedStacks 3", Assert.Single(spy.Part.Calls));
            Assert.Contains("chest bleed stacks adjusted by 3", reply);
            Assert.Contains("has no l_arm", DevCommands.Bleed(spy, "l_arm", 3f));
        }
    }

    [Fact]
    public void GodModeTogglesTgsTrait()
    {
        var (world, spy, server) = Boot();
        using (server)
        using (world.Enter())
        {
            Assert.Contains("godmode on", DevCommands.GodMode(spy));
            Assert.True(GlobalProcs.HAS_TRAIT_FROM(spy, Defines.TRAIT_GODMODE, Defines.ADMIN_TRAIT));
            // 2026-10-15: tg's immunity traits beside it, and the ignored slowdowns
            foreach (string trait in new[] { Defines.TRAIT_NO_STAGGER, Defines.TRAIT_NO_SIDE_KICK, Defines.TRAIT_NOFIRE })
                Assert.True(GlobalProcs.HAS_TRAIT_FROM(spy, trait, DevCommands.GodmodeSource), trait);
            Assert.True(GlobalProcs.HAS_TRAIT_FROM(spy, Defines.TRAIT_IGNORESLOWDOWN, Defines.ADMIN_TRAIT));
            Assert.Contains("godmode off", DevCommands.GodMode(spy));
            Assert.False(GlobalProcs.HAS_TRAIT(spy, Defines.TRAIT_GODMODE));
            foreach (string trait in new[] { Defines.TRAIT_NO_STAGGER, Defines.TRAIT_NO_SIDE_KICK, Defines.TRAIT_NOFIRE, Defines.TRAIT_IGNORESLOWDOWN })
                Assert.False(GlobalProcs.HAS_TRAIT(spy, trait), trait);
        }
    }

    [Fact]
    public void FlyTogglesTgsFlyingWithNegatedGravityAndIgnoredSlowdowns()
    {
        var (world, spy, server) = Boot();
        using (server)
        using (world.Enter())
        {
            Assert.Contains("fly on", DevCommands.Fly(spy));
            Assert.True(GlobalProcs.HAS_TRAIT_FROM(spy, Defines.TRAIT_MOVE_FLYING, Defines.ADMIN_TRAIT));
            Assert.True(GlobalProcs.HAS_TRAIT_FROM(spy, Defines.TRAIT_NEGATES_GRAVITY, DevCommands.FlySource));
            Assert.True(GlobalProcs.HAS_TRAIT_FROM(spy, Defines.TRAIT_IGNORESLOWDOWN, Defines.ADMIN_TRAIT));
            DevCommands.GodMode(spy);
            Assert.Contains("fly off", DevCommands.Fly(spy));
            Assert.False(GlobalProcs.HAS_TRAIT(spy, Defines.TRAIT_MOVE_FLYING));
            Assert.False(GlobalProcs.HAS_TRAIT(spy, Defines.TRAIT_NEGATES_GRAVITY));
            Assert.True(GlobalProcs.HAS_TRAIT(spy, Defines.TRAIT_IGNORESLOWDOWN)); // the godmode's, still on
            DevCommands.GodMode(spy);
            Assert.False(GlobalProcs.HAS_TRAIT(spy, Defines.TRAIT_IGNORESLOWDOWN));
        }
    }

    [Fact]
    public void SpawnHumanMakesAnAdminSpawnedHumanOnTheTurf()
    {
        using var server = TestServer.Start();
        using (server.World.Enter())
        {
            mob_living_carbon_human human = DevCommands.SpawnHuman(locate(6, 6, 1));
            Assert.Same(locate(6, 6, 1), human.loc);
            Assert.True(Dm.truthy(Dm.bitand(human.flags_1, Defines.ADMIN_SPAWNED_1)));
            Assert.Null(human.client);
        }
    }

    [M2Fact(M2Needs.Locomotion)]
    public void SpeedScaleSetsTheRealSeamsScale()
    {
        using var server = TestServer.Start();
        using (server.World.Enter())
        {
            Assert.Contains("0.6", DevCommands.SpeedScale(0.6f));
            Assert.Equal(0.6f, LocomotionSeam.SpeedScale(server.World));
        }
    }

    [Fact]
    public void GasReplacesTheAirOfOneTurfOrOfEveryOpenTurfFromTgsParseGasString()
    {
        using var server = TestServer.Start();
        using (server.World.Enter())
        {
            string reply = DevCommands.Gas(Defines.AIRLESS_ATMOS, 4, 4, 1);
            Assert.Contains("1 open turf(s)", reply);
            datum_gas_mixture vacuum = ((turf_open)locate(4, 4, 1)).air;
            Assert.NotNull(vacuum);
            Assert.Equal(0f, Dm.num(vacuum.total_moles()));
            string all = DevCommands.Gas(Defines.OPENTURF_DEFAULT_ATMOS);
            Assert.Contains("open turf(s) now hold", all);
            Assert.True(Dm.num(((turf_open)locate(4, 4, 1)).air.total_moles()) > 0);
            Assert.Contains("is not an open turf", DevCommands.Gas(Defines.AIRLESS_ATMOS, 1, 1, 1)); // a wall of the rim
        }
    }

    [Fact]
    public void BreachOpensATurfToSpaceAndPatchClosesIt()
    {
        // M4b slice 1: the test room's floor (5, 5): its air-tight objects removed, then tg's ChangeTurf to space and back to plating
        using var server = TestServer.Start();
        using (server.World.Enter())
        {
            Type floor = locate(5, 5, 1).GetType();
            obj_structure_window_fulltile window = Dm.@new<obj_structure_window_fulltile>(locate(5, 5, 1));
            string reply = DevCommands.Breach(locate(5, 5, 1));
            Assert.Contains("(5,5,1) is space now (1 air-tight object(s) removed)", reply);
            Assert.True(QDELETED(window));
            Assert.IsType<turf_open_space>(locate(5, 5, 1));
            Assert.Contains("is already space", DevCommands.Breach(locate(5, 5, 1)));
            // the owner (2026-10-15): patch puts back what the breach removed, the turf and its window
            Assert.Contains("again (1 object(s) put back)", DevCommands.Patch(locate(5, 5, 1)));
            Assert.Equal(floor, locate(5, 5, 1).GetType());
            Assert.NotNull(Dm.locate<obj_structure_window_fulltile>(locate(5, 5, 1)));
            Assert.Contains("is not space", DevCommands.Patch(locate(5, 5, 1)));

            // a space turf no breach made becomes plating
            Waiting.Detach(locate(6, 5, 1).ChangeTurf(typeof(turf_open_space)));
            Assert.Contains("(6,5,1) is plating now", DevCommands.Patch(locate(6, 5, 1)));
            Assert.IsType<turf_open_floor_plating>(locate(6, 5, 1));
        }
    }

    [Fact]
    public void ValveTogglesTheFacedCanistersValve()
    {
        // M4b slice 2: tg's toggle_valve on the canister of a turf, as the canister window's button
        using var server = TestServer.Start();
        using (server.World.Enter())
        {
            var can = Dm.@new<obj_machinery_portable_atmospherics_canister_nitrogen>(locate(5, 5, 1));
            Assert.Contains("opened", DevCommands.Valve(locate(5, 5, 1), null));
            Assert.Equal(1f, can.valve_open);
            Assert.Contains("closed", DevCommands.Valve(locate(5, 5, 1), null));
            Assert.Equal(0f, can.valve_open);
            Assert.Contains("no canister at (6,6,1)", DevCommands.Valve(locate(6, 6, 1), null));
        }
    }

    [Fact]
    public void AirModeSelectsAModeOnTheAirAlarmOfTheMobsArea()
    {
        // M4b slice 2: tg's select_mode on the air alarm of the mob's area, as the alarm window's mode list
        using var server = TestServer.Start();
        using (server.World.Enter())
        {
            mob_living_carbon_human human = DevCommands.SpawnHuman(locate(6, 6, 1));
            Assert.Contains("no air alarm in", DevCommands.AirMode(human, "siphon"));
            var alarm = Dm.@new<obj_machinery_airalarm>(locate(5, 5, 1));
            Assert.Contains("set to", DevCommands.AirMode(human, "siphon"));
            Assert.IsType<datum_air_alarm_mode_siphon>(alarm.selected_mode);
            Assert.Contains("set to", DevCommands.AirMode(human, "off"));
            Assert.IsType<datum_air_alarm_mode_off>(alarm.selected_mode);
        }
    }

    [Fact]
    public void AreaPowerParsesAndSetsTheAreasChannelAsItsApcDoes()
    {
        Assert.True(DevCommandLine.TryParse("areapower 5 6 light off", out DevCommand a, out _));
        Assert.Equal((DevVerb.AreaPower, 5, 6, 1, "light off"), (a.Verb, a.X.Value, a.Y.Value, a.Z.Value, a.Mode));
        Assert.True(DevCommandLine.TryParse("AreaPower 5 6 2 ALL on", out DevCommand b, out _));
        Assert.Equal((2, "all on"), (b.Z.Value, b.Mode));
        Assert.False(DevCommandLine.TryParse("areapower 5 6 lights off", out _, out string badChannel));
        Assert.Contains("not a power channel", badChannel);
        Assert.False(DevCommandLine.TryParse("areapower 5 6 light maybe", out _, out _));
        Assert.False(DevCommandLine.TryParse("areapower 5 light off", out _, out _));

        using var server = TestServer.Start();
        using (server.World.Enter())
        {
            // the test room needs no power (/area/misc/testroom): there is no APC to switch
            Assert.Contains("needs no power", DevCommands.AreaPower(locate(6, 6, 1), "light", false));
            area room = (area)get_area(locate(6, 6, 1));
            room.requires_power = Dm.TRUE;
            Assert.Contains("light off", DevCommands.AreaPower(locate(6, 6, 1), "light", false));
            Assert.False(Dm.truthy(room.power_light));
            Assert.True(Dm.truthy(room.power_equip));
            Assert.Contains("equip off", DevCommands.AreaPower(locate(6, 6, 1), "equip", false));
            Assert.False(Dm.truthy(room.power_equip));
            Assert.Contains("light on", DevCommands.AreaPower(locate(6, 6, 1), "all", true));
            Assert.True(Dm.truthy(room.power_light) && Dm.truthy(room.power_equip) && Dm.truthy(room.power_environ));
        }
    }

    [Fact]
    public void EmpParsesAndPulsesTheTurfAsTgsAdminEmPulseDoes()
    {
        // emp X Y [Z] HEAVY LIGHT: 4 or 5 numbers (adminfun.dm:29-42)
        Assert.True(DevCommandLine.TryParse("emp 5 6 2 4", out DevCommand a, out _));
        Assert.Equal((DevVerb.Emp, 5, 6, 1), (a.Verb, a.X.Value, a.Y.Value, a.Z.Value));
        Assert.Equal(new[] { 2f, 4f }, a.Ranges);
        Assert.True(DevCommandLine.TryParse("EMP 5 6 2 3 7", out DevCommand b, out _));
        Assert.Equal(2, b.Z.Value);
        Assert.Equal(new[] { 3f, 7f }, b.Ranges);
        Assert.False(DevCommandLine.TryParse("emp 5 6 2", out _, out string usage));
        Assert.Contains("usage: emp X Y [Z] HEAVY LIGHT", usage);
        Assert.False(DevCommandLine.TryParse("emp 5 6 -1 4", out _, out _));
        Assert.False(DevCommandLine.TryParse("emp 5 6 2 999", out _, out _));

        using var server = TestServer.Start();
        using (server.World.Enter())
        {
            Assert.Contains("both ranges are 0", DevCommands.Emp(locate(6, 6, 1), 0, 0));
            Assert.DoesNotContain(Dm.each<obj_effect_temp_visual_emp_pulse>(locate(6, 6, 1)), _ => true);
            var cell = Dm.@new<obj_item_stock_parts_power_store_cell>(locate(6, 7, 1));
            cell.maxcharge = 100000;
            cell.charge = 100000;
            Assert.Contains("heavy 2, light 4", DevCommands.Emp(locate(6, 6, 1), 2, 4));
            Assert.Equal(100000f - Defines.STANDARD_CELL_CHARGE, cell.charge); // one tile away: within the heavy range
            Assert.Single(Dm.each<obj_effect_temp_visual_emp_pulse>(locate(6, 6, 1)));
        }
    }

    [Fact]
    public void IrradiateParsesAndQueuesTgsRadiationPulse()
    {
        // irradiate X Y [Z] INTENSITY [THRESHOLD]: with 4 numbers a last one under 1 is the threshold
        Assert.True(DevCommandLine.TryParse("irradiate 5 6 3", out DevCommand a, out _));
        Assert.Equal((DevVerb.Irradiate, 5, 6, 1), (a.Verb, a.X.Value, a.Y.Value, a.Z.Value));
        Assert.Equal(new[] { 3f, Defines.RAD_MEDIUM_INSULATION }, a.Ranges);
        Assert.True(DevCommandLine.TryParse("irradiate 5 6 3 0.5", out DevCommand b, out _));
        Assert.Equal(1, b.Z.Value);
        Assert.Equal(new[] { 3f, 0.5f }, b.Ranges);
        Assert.True(DevCommandLine.TryParse("irradiate 5 6 2 4", out DevCommand c, out _));
        Assert.Equal(2, c.Z.Value);
        Assert.Equal(new[] { 4f, Defines.RAD_MEDIUM_INSULATION }, c.Ranges);
        Assert.True(DevCommandLine.TryParse("IRRADIATE 5 6 2 4 0.2", out DevCommand d, out _));
        Assert.Equal(2, d.Z.Value);
        Assert.Equal(new[] { 4f, 0.2f }, d.Ranges);
        Assert.False(DevCommandLine.TryParse("irradiate 5 6", out _, out string usage));
        Assert.Contains("usage: irradiate X Y [Z] INTENSITY [THRESHOLD]", usage);
        Assert.False(DevCommandLine.TryParse("irradiate 5 6 2.5", out _, out _));
        Assert.False(DevCommandLine.TryParse("irradiate 5 6 999", out _, out _));
        Assert.False(DevCommandLine.TryParse("irradiate 5 6 2 4 1", out _, out _));
        // the verb keeps its number, after recharge (the wire may carry it): appended verbs follow it
        Assert.Equal(DevVerb.Irradiate + 1, DevVerb.Zap);
        Assert.Equal(DevVerb.Recharge + 1, DevVerb.Irradiate);

        using var server = TestServer.Start();
        using (server.World.Enter())
        {
            Assert.NotNull(GlobalProcs.SSradiation);
            Assert.Contains("range 3, threshold 0.7", DevCommands.Irradiate(locate(6, 6, 1), 3, Defines.RAD_MEDIUM_INSULATION));
            var pulse = Assert.Single(Dm.each<datum_radiation_pulse_information>(GlobalProcs.SSradiation.processing));
            Assert.Equal((3f, Defines.RAD_MEDIUM_INSULATION, (float)Defines.DEFAULT_RADIATION_CHANCE), (Dm.num(pulse.max_range), Dm.num(pulse.threshold), Dm.num(pulse.chance)));
            Assert.Same(locate(6, 6, 1), pulse.source_ref.resolve());
        }
    }

    [Fact]
    public void RechargeParsesAndFillsTheSmesAtAPlaceOrEverywhere()
    {
        Assert.True(DevCommandLine.TryParse("recharge 5 6", out DevCommand a, out _));
        Assert.Equal((DevVerb.Recharge, 5, 6, 1, null), (a.Verb, a.X.Value, a.Y.Value, a.Z.Value, a.Mode));
        Assert.True(DevCommandLine.TryParse("RECHARGE 5 6 2", out DevCommand b, out _));
        Assert.Equal(2, b.Z.Value);
        Assert.True(DevCommandLine.TryParse("recharge all", out DevCommand c, out _));
        Assert.Equal((DevVerb.Recharge, "all"), (c.Verb, c.Mode));
        Assert.False(DevCommandLine.TryParse("recharge", out _, out string usage));
        Assert.Contains("usage: recharge X Y [Z] | recharge all", usage);
        Assert.False(DevCommandLine.TryParse("recharge some", out _, out _));
        Assert.False(DevCommandLine.TryParse("recharge 5", out _, out _));

        // the verb keeps its number, after emp (the wire may carry it)
        Assert.Equal(DevVerb.Emp + 1, DevVerb.Recharge);
        // the verb keeps its number (the wire may carry it): appended verbs follow it
        Assert.Equal(DevVerb.Recharge + 1, DevVerb.Irradiate);

        using var server = TestServer.Start();
        using (server.World.Enter())
        {
            Assert.Contains("no power storage unit", DevCommands.Recharge(null)); // the test room has none
            Assert.Contains("no power storage unit at (6,6,1)", DevCommands.Recharge(locate(6, 6, 1)));
        }
    }

    [Fact]
    public void SmCrystalParsesAndSetsOrReportsTheCrystal()
    {
        // smcrystal X Y [Z] power N | damage N | status
        Assert.True(DevCommandLine.TryParse("smcrystal 5 6 power 1500", out DevCommand a, out _));
        Assert.Equal((DevVerb.SmCrystal, 5, 6, 1, "power", 1500f), (a.Verb, a.X.Value, a.Y.Value, a.Z.Value, a.Mode, a.Number));
        Assert.True(DevCommandLine.TryParse("smcrystal 5 6 2 damage 40", out DevCommand b, out _));
        Assert.Equal((2, "damage", 40f), (b.Z.Value, b.Mode, b.Number));
        Assert.True(DevCommandLine.TryParse("smcrystal 5 6 status", out DevCommand c, out _));
        Assert.False(DevCommandLine.TryParse("smcrystal 5 6 power", out _, out _));
        Assert.Equal("status", c.Mode);
        Assert.False(DevCommandLine.TryParse("smcrystal 5 6", out _, out string usage));
        Assert.Contains("usage: smcrystal", usage);
        Assert.False(DevCommandLine.TryParse("smcrystal 5 6 damage 101", out _, out _));
        Assert.False(DevCommandLine.TryParse("smcrystal 5 6 status 3", out _, out _));
        // the verbs after it are appended (the wire may carry their numbers)
        Assert.Equal(DevVerb.SmCrystal + 1, DevVerb.SmDelam);

        using var server = TestServer.Start();
        using (server.World.Enter())
        {
            Assert.Contains("no supermatter crystal", DevCommands.SmCrystal(locate(6, 6, 1), "status", 0));
            var sm = Dm.@new<obj_machinery_power_supermatter_crystal>(locate(6, 6, 1));
            Assert.Contains("1500 MeV", DevCommands.SmCrystal(locate(6, 6, 1), "power", 1500));
            Assert.Equal(1500f, sm.internal_energy);
            Assert.Contains("integrity 60%", DevCommands.SmCrystal(locate(6, 6, 1), "damage", 40));
            Assert.Equal(40f, Dm.num(sm.damage));
        }
    }

    [Fact]
    public void SmDelamParsesAndDelaminatesTheCrystalWithTheNamedStrategy()
    {
        // smdelam X Y [Z] [cascade|singularity|tesla|explosive]
        Assert.True(DevCommandLine.TryParse("smdelam 5 6", out DevCommand a, out _));
        Assert.Equal((DevVerb.SmDelam, 5, 6, 1, (string)null), (a.Verb, a.X.Value, a.Y.Value, a.Z.Value, a.Mode));
        Assert.True(DevCommandLine.TryParse("smdelam 5 6 2 singularity", out DevCommand b, out _));
        Assert.Equal((2, "singularity"), (b.Z.Value, b.Mode));
        Assert.True(DevCommandLine.TryParse("smdelam 5 6 tesla", out DevCommand c, out _));
        Assert.Equal((1, "tesla"), (c.Z.Value, c.Mode));
        Assert.False(DevCommandLine.TryParse("smdelam 5", out _, out string usage));
        Assert.Contains("usage: smdelam", usage);
        Assert.False(DevCommandLine.TryParse("smdelam 5 6 1 2", out _, out _));
        Assert.False(DevCommandLine.TryParse("smdelam 5 6 1 boom", out _, out _));
        // verbs are appended, never reordered (the wire may carry their numbers): SmDelam follows SmCrystal
        Assert.Equal(DevVerb.SmCrystal + 1, DevVerb.SmDelam);

        using var server = TestServer.Start();
        using (server.World.Enter())
        {
            Assert.Contains("no supermatter crystal", DevCommands.SmDelam(locate(6, 6, 1), null));
            var sm = Dm.@new<obj_machinery_power_supermatter_crystal_shard>(locate(6, 6, 1));
            Assert.Contains("delaminates (singularity)", DevCommands.SmDelam(locate(6, 6, 1), "singularity"));
            Assert.True(QDELETED(sm), "the singularity eats the crystal (delamination_effects.dm:104)");
            Assert.NotNull(Dm.locate<obj_singularity>(locate(6, 6, 1)));
        }
    }

    [Fact]
    public void ZapParsesAndBoltsFromTheTurfAsTgsTeslaZap()
    {
        // zap X Y [Z] POWER: 3 or 4 numbers (energy_ball.dm:204)
        Assert.True(DevCommandLine.TryParse("zap 5 6 1000000", out DevCommand a, out _));
        Assert.Equal((DevVerb.Zap, 5, 6, 1, 1000000f), (a.Verb, a.X.Value, a.Y.Value, a.Z.Value, a.Number));
        Assert.True(DevCommandLine.TryParse("ZAP 5 6 2 4e6", out DevCommand b, out _));
        Assert.Equal((2, 4e6f), (b.Z.Value, b.Number));
        Assert.False(DevCommandLine.TryParse("zap 5 6", out _, out string usage));
        Assert.Contains("usage: zap X Y [Z] POWER", usage);
        Assert.False(DevCommandLine.TryParse("zap 5 6 0", out _, out _));
        Assert.False(DevCommandLine.TryParse("zap 5 6 1 1e11", out _, out _));
        // the verbs after it are appended (the wire may carry their numbers)
        Assert.Equal(DevVerb.Zap + 1, DevVerb.SmCrystal);

        using var server = TestServer.Start();
        using (server.World.Enter())
        {
            Assert.Contains("below tg's cutoff", DevCommands.Zap(locate(6, 6, 1), 1000));
            var rod = Dm.@new<obj_machinery_power_energy_accumulator_grounding_rod_anchored>(locate(8, 6, 1));
            Assert.Contains("1 bolt: grounding rod", DevCommands.Zap(locate(6, 6, 1), 1e6f));
            Assert.Equal(1e6f, rod.stored_energy);
        }
    }

    [Fact]
    public void DropTakesOffWhatIsWornInASlotAndGravityTurnsTheGeneratorOffAndOn()
    {
        // the owner (2026-10-15): drop WHO SLOT (tg's dropItemToGround) and gravity WHO on|off (the generator's enable/disable)
        Assert.True(DevCommandLine.TryParse("drop me back", out DevCommand d, out _));
        Assert.Equal((DevVerb.Drop, "back"), (d.Verb, d.Mode));
        Assert.False(DevCommandLine.TryParse("drop me tail", out _, out string bad));
        Assert.Contains("not a slot", bad);
        Assert.True(DevCommandLine.TryParse("gravity me off", out DevCommand g, out _));
        Assert.Equal((DevVerb.Gravity, "off"), (g.Verb, g.Mode));
        Assert.True(DevCommandLine.TryParse("gravity me off now", out DevCommand gn, out _));
        Assert.Equal("off now", gn.Mode);
        Assert.False(DevCommandLine.TryParse("gravity me maybe", out _, out _));

        using var server = TestServer.Start();
        using (server.World.Enter())
        {
            mob_living_carbon_human human = DevCommands.SpawnHuman(locate(6, 6, 1));
            var bag = human.get_item_by_slot(Defines.ITEM_SLOT_BACK) as obj_item;
            if (bag == null)
            {
                bag = Dm.@new<obj_item_storage_backpack>(locate(6, 6, 1));
                Waiting.NoSleep(human.equip_to_slot_or_del(bag, Defines.ITEM_SLOT_BACK));
            }
            Assert.Same(bag, human.get_item_by_slot(Defines.ITEM_SLOT_BACK));
            Assert.Contains("dropped to the floor", DevCommands.Drop(human, "back"));
            Assert.Same(locate(6, 6, 1), bag.loc);
            Assert.Contains("nothing in back", DevCommands.Drop(human, "back"));

            Assert.Contains("no gravity generator", DevCommands.Gravity(human, false));
            var gen = Dm.@new<obj_machinery_gravity_generator_main>(locate(10, 10, 1));
            Assert.Contains("breaker off", DevCommands.Gravity(human, false)); // tg's pace: discharging
            Assert.False(Dm.truthy(gen.breaker));
            Assert.Contains("is off now", DevCommands.Gravity(human, false, now: true));
            Assert.False(Dm.truthy(gen.on));
            gen.process(); // the breaker stays off: no charging back up (the owner's bug: it came back in a second)
            Assert.False(Dm.truthy(gen.on));
            Assert.Contains("is on now", DevCommands.Gravity(human, true, now: true));
            Assert.True(Dm.truthy(gen.on));
        }
    }

    // ---- through the host ----

    private static string Reply(TestServer server, string line)
    {
        server.Host.EnqueueConsoleLine(line);
        int before = server.Out.ToString().Length;
        server.Tick(2);
        return server.Out.ToString().Substring(before);
    }

    [Fact]
    public void TheConsoleRunsADevCommandOnAMobByIdAndAnswersOnTheConsole()
    {
        var (world, spy, server) = Boot();
        using (server)
        {
            string reply = Reply(server, $"damage #{spy.dm_id} brute 9 chest");
            Assert.Equal("apply_damage 9 brute chest", Assert.Single(spy.Calls));
            Assert.Contains("9 brute damage to the chest", reply);
            // Recorded as a console command, in the replay log, with its text.
            Assert.Contains(server.Host.Log.Commands, c => c.Kind == CommandKind.Console && c.Text == $"damage #{spy.dm_id} brute 9 chest");

            spy.Calls.Clear();
            Reply(server, $"kill #{spy.dm_id}");
            Reply(server, $"knockdown #{spy.dm_id} 2");
            Assert.Equal(new[] { "death", "Knockdown 20" }, spy.Calls);
        }
    }

    [Fact]
    public void TheConsoleSaysWhyACommandDidNotRun()
    {
        using var server = TestServer.Start();
        Assert.Contains("console: damage must be above 0", Reply(server, "damage me brute 0"));
        Assert.Contains("the console has no mob", Reply(server, "kill me"));
        Assert.Contains("no player or mob 'Nobody'", Reply(server, "kill Nobody"));
        Assert.Contains("no player or mob '#1'", Reply(server, "fullheal #1")); // an id that is no movable
        Reply(server, "spawn /obj/item 11 11");
        long itemId;
        using (server.World.Enter())
            itemId = Dm.each<atom_movable>(locate(11, 11, 1).contents).Single().dm_id;
        Assert.Contains("is not a living mob", Reply(server, $"fullheal #{itemId}"));
        Assert.Contains("Dev commands", Reply(server, "dev-help"));
        Assert.DoesNotContain(server.Host.Log.Commands, c => c.Kind == CommandKind.Console && c.Text.StartsWith("damage", StringComparison.Ordinal));
        Assert.Empty(server.Err.ToString());
    }

    [Fact]
    public void ADevSpawnMakesARegisteredDummyHumanAndSoDoesTheConsoleSpawn()
    {
        using var server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        alice.Motion(0, 0);
        alice.Action(ActionKind.DevCommand, text: "spawn /mob/living/carbon/human 9 9");
        server.Tick(2);
        mob_living_carbon_human dummy = null;
        using (server.World.Enter())
            dummy = Dm.each<mob_living_carbon_human>(locate(9, 9, 1).contents).Single();
        Assert.True(server.Locomotion.IsRegistered(dummy)); // solid to the players' capsules (D105)
        Assert.Contains("spawned a human", System.Net.WebUtility.HtmlDecode(FakeCodec.DecodeServerText(alice.Messages(MessageId.ServerText).Last()).Html));

        Reply(server, "spawn /mob/living/carbon/human 10 10");
        mob_living_carbon_human second = null;
        using (server.World.Enter())
            second = Dm.each<mob_living_carbon_human>(locate(10, 10, 1).contents).Single();
        Assert.True(server.Locomotion.IsRegistered(second));
        // A thing that is no mob is not a capsule.
        Reply(server, "spawn /obj/item 11 11");
        using (server.World.Enter())
            Assert.All(Dm.each<atom_movable>(locate(11, 11, 1).contents), m => Assert.False(m is mob_living l && server.Locomotion.IsRegistered(l)));
    }

    [Fact]
    public void APlayerAddsListsAndRemovesStatusEffectsThroughTheHost()
    {
        using var server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        alice.Motion(0, 0);
        string Last()
        {
            server.Tick(2);
            return System.Net.WebUtility.HtmlDecode(FakeCodec.DecodeServerText(alice.Messages(MessageId.ServerText).Last()).Html);
        }
        var human = (mob_living)server.MobOf("Alice");
        alice.Action(ActionKind.DevCommand, text: "effect me add incapacitating/knockdown 5");
        Assert.Contains("incapacitating/knockdown applied (knockdown: 5 s left)", Last());
        using (server.World.Enter())
            Assert.True(Dm.truthy(human.IsKnockdown()));
        alice.Action(ActionKind.DevCommand, text: "effects me");
        server.Tick(2);
        List<string> lines = alice.Messages(MessageId.ServerText).Select(m => System.Net.WebUtility.HtmlDecode(FakeCodec.DecodeServerText(m).Html)).ToList();
        Assert.Contains(lines, l => l.Contains("status effect(s)", StringComparison.Ordinal));
        Assert.Contains(lines, l => l.Contains("incapacitating/knockdown", StringComparison.Ordinal) && l.Contains("s left", StringComparison.Ordinal));
        alice.Action(ActionKind.DevCommand, text: "effect me remove knockdown");
        Assert.Contains("incapacitating/knockdown removed", Last());
        using (server.World.Enter())
            Assert.False(Dm.truthy(human.IsKnockdown()));
        alice.Action(ActionKind.DevCommand, text: "effect me add teleporting");
        Assert.Contains("no status effect the port has live", Last());
        alice.Action(ActionKind.DevCommand, text: "effect types");
        Assert.Contains("incapacitating/stun", Last());
        // the console names a mob by id
        string reply = Reply(server, $"effect #{human.dm_id} add staggered 3");
        Assert.Contains("staggered applied (staggered: 3 s left)", reply);
        Assert.Contains("staggered", Reply(server, $"effects #{human.dm_id}"));
        Assert.Empty(server.Err.ToString());
    }

    [M2Fact(M2Needs.Medical)]
    public void TheRealProcsRunWhenTheLivingCoreHasLanded()
    {
        using var server = TestServer.Start();
        FakeRemote alice = server.Join("Alice");
        alice.Motion(0, 0);
        // just after one of SSmobs' own fires, so her next Life (which breathes the oxygen loss away) is 2 s off
        datum_controller_subsystem_mobs mobs;
        using (server.World.Enter())
            mobs = Dm.each<datum_controller_subsystem_mobs>(Master.subsystems).Single();
        float before = mobs.times_fired;
        for (int i = 0; i < 60 && mobs.times_fired == before; i++)
            server.Tick();
        alice.Action(ActionKind.DevCommand, text: "damage me oxy 30");
        server.Tick(2);
        mob_living human = (mob_living)server.MobOf("Alice");
        Assert.Equal(30f, Dm.num(human.get_oxy_loss()));
        alice.Action(ActionKind.DevCommand, text: "fullheal me");
        server.Tick(2);
        Assert.Equal(0f, Dm.num(human.get_oxy_loss()));
        Assert.Empty(server.Err.ToString());
    }
}
