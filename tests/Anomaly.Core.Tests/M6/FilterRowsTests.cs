using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using Anomaly.Core.Tests.M4bAudio;
using Anomaly.Core.Tg;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tests.M6;

/// <summary>
/// M6 slice 7b (D156): the tg rows that waited only for filters, translated now that the client draws them: an atom's colour filter
/// on an overlay and its children (atom_color.dm:122), tg's filter procs run on a mutable appearance (datum.dm:334 as an /image), a
/// human's height filters (human_update_icons.dm:804), the blood stain cut to a colour-filtered item (decals/blood.dm:43), a damaged
/// brain's grey (brain_item.dm:391), the canister's gas window (canister.dm:379) and a coloured chair's armrest (colors.dm:250).
/// </summary>
public class FilterRowsTests
{
    private const int Second = 20;

    private static float[] Grey => new[] { 0.5f, 0, 0, 0, 0.5f, 0, 0, 0, 0.5f };

    private static IEnumerable<dm_filter> Filters(object holder) => Dm.each<dm_filter>(holder switch
    {
        image i => i.filters as DmList,
        atom a => a.filters as DmList,
        _ => null,
    } ?? Dm.list());

    [Fact]
    public void An_atoms_colour_matrix_becomes_a_colour_filter_on_its_overlay_and_the_overlays_children()
    {
        var r = new AudioRoom(8, 8, seed: 711);
        r.In(() =>
        {
            var thing = Dm.@new<obj_item>(Builtins.locate(3, 3, 1));
            thing.color = Dm.list(0.5f, 0, 0, 0, 0.5f, 0, 0, 0, 0.5f);
            var overlay = Dm.@new<mutable_appearance>();
            var child = Dm.@new<mutable_appearance>();
            Dm.append(ref overlay.overlays, child);
            Assert.Same(overlay, thing.color_atom_overlay(overlay));
            Assert.Null(overlay.color);
            dm_filter tint = Assert.Single(Filters(overlay));
            Assert.Equal("color", tint.type);
            Assert.Equal(ATOM_PRIORITY_COLOR_FILTER, tint.name);
            Assert.Single(Filters(((DmList)overlay.overlays)[1f] as image));

            // a plain colour is the overlay's colour, no filter
            thing.color = "#ff0000";
            var plain = Dm.@new<mutable_appearance>();
            thing.color_atom_overlay(plain);
            Assert.Equal("#ff0000", plain.color);
            Assert.Empty(Filters(plain));
        });
    }

    [Fact]
    public void Tgs_filter_procs_run_on_a_mutable_appearance_in_priority_order()
    {
        var r = new AudioRoom(8, 8, seed: 712);
        r.In(() =>
        {
            var look = Dm.@new<mutable_appearance>();
            look.add_filter("second", 2, outline_filter(size: 1) as DmList);
            look.add_filter("first", 1, gauss_blur_filter(size: 2) as DmList);
            Assert.Equal(new[] { "blur", "outline" }, Filters(look).Select(f => f.type));
            Assert.Equal("outline", ((dm_filter)look.get_filter("second")).type);
            look.remove_filter("first");
            Assert.Equal(new[] { "outline" }, Filters(look).Select(f => f.type));
            look.clear_filters();
            Assert.Null(look.filters);
        });
    }

    [Fact]
    public void A_tall_humans_body_is_lengthened_by_tgs_displacement_filters()
    {
        var r = new AudioRoom(8, 8, seed: 713);
        var h = r.Dummy(4, 4);
        r.Step(Second);
        r.In(() =>
        {
            h.mob_height = HUMAN_HEIGHT_TALLEST;
            var look = Dm.@new<mutable_appearance>();
            var worn = Dm.@new<mutable_appearance>();
            Dm.append(ref look.overlays, worn);
            h.apply_height_filter(look);
            List<dm_filter> made = Filters(look).ToList();
            Assert.Equal(new[] { "Lenghten_Torso", "Lenghten_Arms", "Lenghten_Legs" }, made.Select(f => f.name));
            Assert.All(made, f => Assert.Equal("displace", f.type));
            Assert.Equal(new[] { "Cut3", "Cut5", "Cut4" }, made.Select(f => ((icon)f.Get("icon")).dm_state));
            Assert.Equal(3, Filters(((DmList)look.overlays)[1f] as image).Count()); // a child without KEEP_TOGETHER gets them too

            h.mob_height = HUMAN_HEIGHT_SHORT;
            h.apply_height_filter(look);
            Assert.Equal(new[] { "Cut_Legs" }, Filters(look).Select(f => f.name)); // the old ones are removed first
        });
    }

    [Fact]
    public void A_blood_stain_on_a_colour_filtered_item_is_cut_to_the_items_render_target()
    {
        var r = new AudioRoom(8, 8, seed: 714);
        r.In(() =>
        {
            var thing = Dm.@new<obj_item>(Builtins.locate(3, 3, 1));
            var element = Dm.@new<datum_element_decal_blood>();
            element.uses_filter = Dm.TRUE;
            Assert.True(Dm.truthy(element.generate_appearance(_color: "#aa0000", source: thing)));
            Assert.Equal(Dm.text("blood_target_[]", REF(thing)), thing.render_target);
            dm_filter cutout = Assert.Single(Filters(element.pic));
            Assert.Equal("alpha", cutout.type);
            Assert.Equal("blood_cutout", cutout.name);
            Assert.Equal(thing.render_target, cutout.Get("render_source"));
            Assert.True(Dm.truthy(Dm.bitand(element.pic.appearance_flags, Dm.KEEP_APART)));
        });
    }

    [Fact]
    public void A_damaged_brain_turns_grey_by_a_colour_filter_and_a_healed_one_loses_it()
    {
        var r = new AudioRoom(8, 8, seed: 715);
        r.In(() =>
        {
            var brain = Dm.@new<obj_item_organ_brain>(Builtins.locate(3, 3, 1));
            brain.damage = brain.maxHealth;
            brain.update_brain_color(Dm.FALSE);
            dm_filter grey = Assert.Single(Filters(brain));
            Assert.Equal("color", grey.type);
            Assert.Equal("brain_damage_color_filter", grey.name);
            Assert.Equal("#7f7f7f", grey.Get("color")); // fully damaged: the gradient's end

            brain.damage = 0;
            brain.update_brain_color(Dm.FALSE);
            Assert.Empty(Filters(brain));
        });
    }

    [Fact]
    public void A_canisters_gas_window_masks_its_gas_pictures_with_tgs_alpha_filter()
    {
        var r = new AudioRoom(8, 8, seed: 716);
        r.In(() =>
        {
            var canister = Dm.@new<obj_machinery_portable_atmospherics_canister_plasma>(Builtins.locate(3, 3, 1));
            canister.update_window();
            Assert.NotNull(canister.window);
            Assert.Equal("window-base", canister.window.icon_state);
            var visuals = Dm.each<image>((DmList)canister.window.overlays).ToList();
            Assert.NotEmpty(visuals); // plasma shows
            Assert.All(visuals, v =>
            {
                dm_filter mask = Assert.Single(Filters(v));
                Assert.Equal("alpha", mask.type);
                Assert.Equal("window-base", ((icon)mask.Get("icon")).dm_state);
            });
        });
    }

    [Fact]
    public void A_coloured_chairs_armrest_takes_the_chairs_colour_filter_recursively()
    {
        var r = new AudioRoom(8, 8, seed: 717);
        r.In(() =>
        {
            DmList tint = color_matrix_filter(Dm.list(Grey.Cast<object>().ToArray())) as DmList;
            var armrest = Dm.@new<mutable_appearance>();
            Dm.append(ref armrest.overlays, Dm.@new<mutable_appearance>());
            mutable_appearance made = filter_appearance_recursive(armrest, tint);
            Assert.NotSame(armrest, made);
            Assert.Equal("color", Assert.Single(Filters(made)).type);
            Assert.Equal("color", Assert.Single(Filters(((DmList)made.overlays)[1f] as image)).type);
        });
    }
}
