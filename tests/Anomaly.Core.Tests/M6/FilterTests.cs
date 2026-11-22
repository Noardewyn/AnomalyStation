using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tests.Tools;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M6;

/// <summary>
/// M6 slice 7 (D155): BYOND's filters in the shim (filter(), the filters list, animate of a filter) and tg's filter procs
/// (datum.dm:334-528, __HELPERS/filters.dm), with the callers that M6 needs: the glove of a one-handed human masked where the hand
/// is gone (human_update_icons.dm:199), the split skull's displacement (cranial_fissure.dm:53).
/// </summary>
public class FilterTests
{
    private const int Second = 20;

    [Fact]
    public void Filter_makes_a_filter_of_a_known_type_from_named_arguments()
    {
        dm_filter outline = Dm.filter(Dm.named("type", "outline"), Dm.named("size", 2), Dm.named("color", "#FF0000"));
        Assert.Equal("outline", outline.type);
        Assert.Equal(2, outline.Get("size"));
        Assert.Equal("#FF0000", outline.Get("color"));
        Assert.Null(outline.name);

        dm_filter alpha = Dm.filter(Dm.arglist(Dm.list(Dm.kv("type", "alpha"), Dm.kv("name", "mask"), Dm.kv("x", 1))));
        Assert.Equal("alpha", alpha.type);
        Assert.Equal("mask", alpha.name);

        Assert.Throws<DmRuntimeException>(() => Dm.filter(Dm.named("type", "sparkles")));
        Assert.Throws<DmRuntimeException>(() => Dm.filter("outline"));
    }

    [Fact]
    public void A_filters_list_finds_and_removes_a_filter_by_its_name()
    {
        dm_filter a = Dm.filter(Dm.named("type", "outline"), Dm.named("name", "a"));
        dm_filter b = Dm.filter(Dm.named("type", "blur"), Dm.named("name", "b"));
        DmList filters = Dm.filter_list(Dm.list(a, b));
        Assert.True(filters.IsFilters);
        // BYOND: "the atom gets a copy of this filter" (M6 slice 7b): the list's filters are copies, found by name
        var attached = (dm_filter)filters["b"];
        Assert.NotSame(b, attached);
        Assert.Equal("blur", attached.type);
        Assert.Null(filters["c"]);
        object current = filters;
        Dm.remove(ref current, "a");
        Assert.Equal(1, ((DmList)current).Count);
        Assert.Same(attached, ((DmList)current)[1f]);
        Assert.Null(Dm.filter_list(Dm.list()));
        // OpenDream: a list of filter parameters is a filter, and an item that is neither a filter nor such a list is skipped
        DmList made = Dm.filter_list(Dm.list(Dm.kv("type", "outline"), Dm.kv("size", 2)));
        Assert.Equal("outline", ((dm_filter)made[1f]).type);
        Assert.Equal(2f, Dm.num(((dm_filter)made[1f]).Get("size")));
        Assert.Null(Dm.filter_list(Dm.list("outline")));
        Assert.Null(Dm.filter_list(Dm.list(Dm.kv("type", "sparkle"))));
        Assert.Throws<DmRuntimeException>(() => Dm.filter_list("outline"));
    }

    [Fact]
    public void Tg_add_filter_keeps_the_filters_in_priority_order_and_modify_transition_and_remove_change_them()
    {
        var r = new AudioRoom(8, 8, seed: 701);
        r.In(() =>
        {
            var thing = Dm.@new<obj_item>(Builtins.locate(3, 3, 1));
            thing.add_filter("outline", 2, outline_filter(size: 1, color: "#FF0000") as DmList);
            thing.add_filter("mask", 1, alpha_mask_filter(x: 1, render_source: "*mask") as DmList);
            var filters = (DmList)thing.filters;
            Assert.Equal(2, filters.Count);
            Assert.Equal("alpha", ((dm_filter)filters[1f]).type); // priority 1 first
            Assert.Equal("outline", ((dm_filter)filters[2f]).type);
            Assert.Equal("outline", ((dm_filter)thing.get_filter("outline")).type);
            Assert.False(((dm_filter)thing.get_filter("outline")).Has("priority"));

            thing.modify_filter("outline", Dm.list(Dm.kv("size", 2)));
            Assert.Equal(2, ((dm_filter)thing.get_filter("outline")).Get("size"));
            Assert.Equal("#FF0000", ((dm_filter)thing.get_filter("outline")).Get("color"));

            // transition_filter animates the filter (its vars take the step's values at once) and keeps the new parameters
            thing.transition_filter("outline", Dm.list(Dm.kv("size", 3)), 10);
            Assert.Equal(3, ((dm_filter)thing.get_filter("outline")).Get("size"));

            // the same name replaces the filter
            thing.add_filter("outline", 3, outline_filter(size: 4) as DmList);
            Assert.Equal(2, ((DmList)thing.filters).Count);
            Assert.Equal(4, ((dm_filter)thing.get_filter("outline")).Get("size"));

            thing.remove_filter("mask");
            Assert.Single(Dm.each<dm_filter>((DmList)thing.filters));
            thing.clear_filters();
            Assert.Null(thing.filters);
            Assert.Null(thing.get_filter("outline"));
        });
    }

    [Fact]
    public void A_glove_on_the_hand_that_is_left_is_masked_where_the_lost_hand_was()
    {
        var r = new AudioRoom(8, 8, seed: 702);
        var h = r.Dummy(4, 4);
        r.Step(Second);
        r.In(() =>
        {
            var gloves = Dm.@new<obj_item_clothing_gloves_color_black>(h.loc);
            Assert.True(Dm.truthy(Waiting.NoSleep(h.equip_to_slot_if_possible(gloves, ITEM_SLOT_GLOVES))));
            Assert.Null(((mutable_appearance)h.overlays_standing[GLOVES_LAYER]).filters); // two hands: no mask
            Waiting.NoSleep(((obj_item_bodypart)h.get_bodypart(BODY_ZONE_R_ARM)).dismember());
        });
        r.Step(1);
        r.In(() =>
        {
            Assert.NotNull(h.gloves);
            var overlay = (mutable_appearance)h.overlays_standing[GLOVES_LAYER];
            string expected = Dm.text("*MissGlove [] R", REF(h));
            dm_filter mask = Assert.Single(Dm.each<dm_filter>((DmList)overlay.filters));
            Assert.Equal("alpha", mask.type);
            Assert.Equal(expected, mask.Get("render_source"));
            Assert.Equal(Dm.MASK_INVERSE, Dm.num(mask.Get("flags")));
            // the mask's picture is an overlay of the glove with that render target: tg's effects.dmi "missingr"
            IDmAppearance target = null;
            foreach (object o in Dm.each(overlay.overlays))
                if (o is IDmAppearance look && Dm.eq(look.render_target, expected)) target = look;
            Assert.NotNull(target);
            Assert.Equal("missingr", target.icon_state);
            Assert.Equal("icons/effects/effects.dmi", Dm.text("[]", target.icon));
        });
    }

    [Fact]
    public void A_cranial_fissure_displaces_the_victims_picture_until_it_is_mended()
    {
        var r = new AudioRoom(8, 8, seed: 703);
        if (TgClone.Root != null) // tg's strings/ files (the scars' texts of a removed wound), as the server mounts them
            TgStrings.Mount(DmWorldState.Of(r.World).Files, TgClone.Root);
        var victim = r.Dummy(4, 4);
        r.Step(Second);
        datum_wound_cranial_fissure fissure = r.In(() =>
        {
            var made = (datum_wound_cranial_fissure)Waiting.NoSleep(Dm.new_async(typeof(datum_wound_cranial_fissure)));
            Waiting.NoSleep(made.apply_wound((obj_item_bodypart)victim.get_bodypart(BODY_ZONE_HEAD)));
            return made;
        });
        r.In(() =>
        {
            var displace = (dm_filter)victim.get_filter("cranial_fissure_displacement");
            Assert.NotNull(displace);
            Assert.Equal("displace", displace.type);
            Assert.Equal(3, Dm.num(displace.Get("size")));
            var map = Assert.IsType<icon>(displace.Get("icon"));
            Assert.Equal("displacement", map.dm_state);
            Waiting.NoSleep(fissure.remove_wound());
            Assert.Null(victim.get_filter("cranial_fissure_displacement"));
        });
    }

    [Fact]
    public void A_damaged_jumpsuit_shows_tgs_damage_picture_blended_from_its_own_icon()
    {
        var r = new AudioRoom(8, 8, seed: 704);
        r.In(() =>
        {
            var suit = Dm.@new<obj_item_clothing_under_rank_civilian_janitor>(Builtins.locate(3, 3, 1));
            var other = Dm.@new<obj_item_clothing_under_rank_civilian_janitor>(Builtins.locate(4, 3, 1));
            Assert.DoesNotContain(Dm.each<icon>(suit.overlays), _ => true);
            foreach (obj_item_clothing c in new obj_item_clothing[] { suit, other })
            {
                Waiting.NoSleep(c.update_clothes_damaged_state(CLOTHING_DAMAGED));
                c.update_appearance();
            }
            // clothing.dm:519: icon(icon, icon_state, , 1) blended white (ICON_ADD), then by item_damage.dmi's "itemdamaged" (ICON_MULTIPLY)
            icon damaged = null;
            foreach (object o in Dm.each(suit.overlays))
                if (o is IDmAppearance look && look.icon is icon made) damaged = made;
            Assert.NotNull(damaged);
            Assert.Equal(suit.icon_state, damaged.dm_state);
            Assert.Equal(1, damaged.dm_frame);
            Assert.Equal(2, damaged.dm_ops.Count);
            Assert.Equal(((int)Dm.ICON_ADD, (object)"#fff"), (damaged.dm_ops[0].Function, damaged.dm_ops[0].With));
            Assert.Equal((int)Dm.ICON_MULTIPLY, damaged.dm_ops[1].Function);
            Assert.Equal("itemdamaged", ((icon)damaged.dm_ops[1].With).dm_state);
            string resource = damaged.dm_resource();
            Assert.StartsWith("iconops:", resource, StringComparison.Ordinal);
            Assert.Contains("itemdamaged", Uri.UnescapeDataString(Uri.UnescapeDataString(resource)));
        });
    }

    [Fact]
    public void Damaged_GAGS_shoes_key_their_damage_picture_by_the_GAGS_name()
    {
        var r = new AudioRoom(8, 8, seed: 705);
        r.In(() =>
        {
            var shoes = Dm.@new<obj_item_clothing_shoes_sneakers_black>(Builtins.locate(3, 3, 1));
            Assert.StartsWith("gags:", Assert.IsType<string>(shoes.icon), StringComparison.Ordinal);
            Waiting.NoSleep(shoes.update_clothes_damaged_state(CLOTHING_DAMAGED));
            shoes.update_appearance(); // the GAGS text has no REF (DV19): its name keys the cache
            icon damaged = Dm.each(shoes.overlays).OfType<IDmAppearance>().Select(o => o.icon).OfType<icon>().Single();
            Assert.Equal(shoes.icon, damaged.dm_file);
            Assert.StartsWith("iconops:" + ((string)shoes.icon).Replace("|", "%7C") + "|", damaged.dm_resource(), StringComparison.Ordinal); // a part escapes its |
        });
        r.AssertNoRuntimes();
    }
}
