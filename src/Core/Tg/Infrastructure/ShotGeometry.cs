using System.Linq;
using System.IO;
using System.Text;
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// M7x slice A (D177, docs/ai/specs/weapons.md section 2): a shot hits where it lands. A shot aimed by a player's camera (DV94) is a 3D
// line: its 2D start and heading are tg's (aim_projectile, process_movement), its height at a map point is the eye's height plus a
// slope times the distance from the start. This file holds the port's own part: the line's vars on the projectile and on the
// effects a shot leaves (replicated as NetField.Shot), tg's spread as a 3D cone (DV96), and the crossing test of the line against a
// carbon's drawn body parts (DV95), which uses the committed body-part mask table (Generated/BodyMasks.bin, tools/Anomaly.Tools.Icons
// --masks) because the server reads no icon at run time (D104).
//
// The picture is placed as the client draws a mob (godot/scripts/Render/Billboard.cs, Shared/Icons/BillboardMath.cs): a billboard
// facing the viewer, drawn at WorldScale.MobScale (0.8) of the tile scale with its lowest opaque row on the floor, its frame chosen by
// the viewer's bearing (BillboardMath.ChooseDir, ChooseLyingDir); a lying body is the same billboard with tg's turned picture. The
// client's functions are linked into no core project (the client references this one), so the few lines are mirrored here and the
// server tests compare them (Client/ShotGeometryMirrorTests) and the zones the two find (Integration/AimedShotGameplayTests, risk R1).

public partial class obj_projectile
{
    /// <summary>DEVIATION(DV94): the shot was aimed by a player's camera (its click params held <c>anomaly-aim</c>): the 3D crossing applies.</summary>
    public bool dm_aimed;

    /// <summary>The shot's line is set (<see cref="dm_origin_x"/> and on): every projectile once aimed or fired.</summary>
    public bool dm_shot;

    /// <summary>The line's start, absolute tile pixels east (a tile's centre at 32 times its x).</summary>
    public float dm_origin_x;

    /// <summary>The line's start, absolute tile pixels north.</summary>
    public float dm_origin_y;

    /// <summary>The height at the start above the level's floor, tile pixels (32 to 2 m).</summary>
    public float dm_origin_h;

    /// <summary>Height pixels per horizontal pixel (the tangent of the pitch).</summary>
    public float dm_slope;

    /// <summary>While <see cref="ShotGeometry.HitCeiling"/> runs the hit chain on the cell's turf: the shot hit the cell's own ceiling, not the turf.</summary>
    public bool dm_hit_ceiling;
}

public partial class turf_open
{
    /// <summary>DEVIATION(DV577): the shots' dents on the cell's floor and on its top (<see cref="ShotGeometry.DentSurface"/>), as a wall's <c>dent_decals</c>.</summary>
    public DmList dm_dent_decals;
}

public partial class obj_effect
{
    /// <summary>Protocol version 34: the effect was left by a shot (a tracer, a muzzle, an impact) and is drawn at its line's height.</summary>
    public bool dm_shot;

    /// <summary>The line's start, absolute tile pixels east.</summary>
    public float dm_origin_x;

    /// <summary>The line's start, absolute tile pixels north.</summary>
    public float dm_origin_y;

    /// <summary>The height at the start, tile pixels.</summary>
    public float dm_origin_h;

    /// <summary>Height pixels per horizontal pixel (0 for an impact).</summary>
    public float dm_slope;

    /// <summary>The shot's heading, degrees clockwise from north.</summary>
    public float dm_shot_angle;
}

/// <summary>The 3D geometry of an aimed shot (D177).</summary>
public static class ShotGeometry
{
    /// <summary>Tile pixels per tile (tg's <c>ICON_SIZE_ALL</c>); heights use the same pixels, 32 to the 2 m tile.</summary>
    public const float PixelsPerTile = 32f;

    /// <summary>DEVIATION(DV97): the ceiling's height in tile pixels: the world's level height (<see cref="LevelHeight"/>, the server's wall height; 32 for 2 m).</summary>
    public static float CeilingPixels => LevelHeight.Pixels(World.CurrentOrNull);

    /// <summary>The height an unaimed shot is drawn at: the middle of a mob's picture (0.8 m, 12.8 px).</summary>
    public const float UnaimedHeightPixels = 12.8f;

    /// <summary>The client's <c>WorldScale.MobScale</c>: a mob's picture is drawn at 0.8 of the tile scale.</summary>
    public const float MobScale = 0.8f;

    /// <summary>The standing eye (D157: 1.3 m), tile pixels; the server's stand-in when a client's eye is refused (DV94).</summary>
    public const float StandingEyePixels = 20.8f;

    /// <summary>The lying eye (0.4 m), tile pixels.</summary>
    public const float LyingEyePixels = 6.4f;

    /// <summary>The highest standing eye a client may report: <c>WorldScale.MaxEyeHeight</c>, 1.9 m.</summary>
    public const float MaxStandingEyePixels = 30.4f;

    /// <summary>The highest lying eye a client may report: 0.8 m.</summary>
    public const float MaxLyingEyePixels = 12.8f;

    /// <summary>How far a client's eye may be from the mob's own position, tile pixels (0.75 tile: a sprint's lead at the round trip, risk R4).</summary>
    public const float MaxEyeOffsetPixels = 24f;

    // ---- the line ----

    /// <summary>The horizontal unit vector of a heading in degrees clockwise from north (x east, y north).</summary>
    public static (double X, double Y) Heading(double degrees)
    {
        double a = degrees * Math.PI / 180.0;
        return (Math.Sin(a), Math.Cos(a));
    }

    /// <summary>The projectile's point now, absolute tile pixels (its turf's centre plus its pixel offsets).</summary>
    public static (double X, double Y) PointOf(obj_projectile p) => (Dm.num(p.x) * PixelsPerTile + p.pixel_x, Dm.num(p.y) * PixelsPerTile + p.pixel_y);

    /// <summary>The shot's height at the absolute point (<paramref name="x"/>, <paramref name="y"/>), tile pixels.</summary>
    public static double HeightAt(obj_projectile p, double x, double y)
    {
        double dx = x - p.dm_origin_x, dy = y - p.dm_origin_y;
        return p.dm_origin_h + p.dm_slope * Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// DEVIATION(DV95): a wall dent's pixel_z for an aimed shot, the height its line has at its point now, from the middle of the wall's
    /// face picture at the bottom of the wall (the client draws a dent's picture there, at the tile scale, on a wall as high as the level).
    /// </summary>
    public static float DentHeight(obj_projectile p)
    {
        (double x, double y) = PointOf(p);
        return (float)Math.Clamp(Math.Round(HeightAt(p, x, y) - PixelsPerTile / 2.0), -PixelsPerTile / 2.0, CeilingPixels - PixelsPerTile / 2.0);
    }

    /// <summary>
    /// DEVIATION(DV214): the side of <paramref name="wall"/> the shot hit (the one toward the projectile's point, which is still in the turf it came
    /// from) and the dent's x along that face, to the right of one looking at it (tg's impact x on a north or south face, its y on an east or west one).
    /// </summary>
    public static (byte Face, float Along) DentFace(obj_projectile p, turf wall, float impactX, float impactY)
    {
        (double x, double y) = PointOf(p);
        double dx = x - Dm.num(wall.x) * PixelsPerTile, dy = y - Dm.num(wall.y) * PixelsPerTile;
        if (Math.Abs(dx) > Math.Abs(dy))
            return dx > 0 ? ((byte)Dm.EAST, impactY) : ((byte)Dm.WEST, -impactY);
        return dy > 0 ? ((byte)Dm.NORTH, -impactX) : ((byte)Dm.SOUTH, impactX);
    }

    /// <summary>
    /// DEVIATION(DV97): how much farther (horizontal pixels) the shot flies from its point now before its line reaches the floor or the
    /// ceiling; infinity for a level line.
    /// </summary>
    public static double DistanceToFloorOrCeiling(obj_projectile p)
    {
        if (p.dm_slope == 0f)
            return double.PositiveInfinity;
        (double x, double y) = PointOf(p);
        double dx = x - p.dm_origin_x, dy = y - p.dm_origin_y;
        double travelled = Math.Sqrt(dx * dx + dy * dy);
        double exit = ((p.dm_slope < 0f ? 0.0 : CeilingPixels) - p.dm_origin_h) / p.dm_slope;
        return Math.Max(0.0, exit - travelled);
    }

    /// <summary>
    /// DEVIATION(DV394): an aimed shot whose line reaches the floor over open space (tg's zPassOut down, a catwalk holds it) goes on into the level
    /// under it, and one that reaches the top of a cell with no ceiling under the open space of the level over it goes on up into it (D325: the 3D
    /// client aims through the levels it shows; tg's projectiles keep their level). Its line is the same, its heights one level higher or lower,
    /// counted from the new level's floor. False when nothing lets it through: the shot hits the floor or the ceiling as before.
    /// </summary>
    public static async DmTask<bool> PassLevel(obj_projectile p)
    {
        if (p.loc is not turf here || p.dm_slope == 0f)
            return false;
        bool down = p.dm_slope < 0f;
        if (!down && CeilingSystem.Of(World.Current).At((int)Dm.num(here.x), (int)Dm.num(here.y), (int)Dm.num(here.z)).Exists)
            return false;
        object dir = down ? DOWN : UP;
        if ((down ? GET_TURF_BELOW(here) : GET_TURF_ABOVE(here)) is not turf next)
            return false;
        // DEVIATION(DV525): a ladder's hole is a floor tg only draws see-through (ladders.dm make_base_transparent); the 3D client cuts the hatch's
        // opening out of it (DV397), and a shot through that opening goes on, down or up
        if (!InHatch(p, down ? here : next) && (Dm.num(here.zPassOut(dir)) == 0 || Dm.num(next.zPassIn(dir)) == 0))
            return false;
        p.free_hitscan_forceMove = TRUE;
        await p.forceMove(next);
        if (QDELETED(p) || p.loc != next)
            return false;
        p.dm_origin_h += down ? CeilingPixels : -CeilingPixels;
        return true;
    }

    /// <summary>What closes the top of a cell for a shot that reached it (<see cref="Overhead"/>).</summary>
    public enum Top
    {
        /// <summary>Nothing: the cell is open to the sky (no ceiling of its own, no level over it).</summary>
        None,

        /// <summary>The cell's own ceiling (D295: the ceiling system's floor block).</summary>
        Ceiling,

        /// <summary>The floor of the level over the cell (D325: the turf above is the cell's boundary).</summary>
        FloorAbove,

        /// <summary>The ceilings are not set up (a test world): the shot hits the cell's turf, as before them.</summary>
        Unknown,
    }

    /// <summary>
    /// DEVIATION(DV97): what a shot that reached the top of <paramref name="here"/> (and did not pass into the level over it, <see cref="PassLevel"/>)
    /// hits there: the floor of the level above (<paramref name="above"/>; open space or space over the cell lets it fly on), else the cell's own ceiling.
    /// </summary>
    public static Top Overhead(turf here, out turf above)
    {
        above = here == null ? null : GET_TURF_ABOVE(here) as turf;
        if (above != null && !isspaceturf(above) && above is not turf_open_openspace)
            return Top.FloorAbove;
        above = null;
        if (here == null || CeilingSystem.Of(World.Current) is not { Initialized: true } ceilings)
            return Top.Unknown;
        return ceilings.At((int)Dm.num(here.x), (int)Dm.num(here.y), (int)Dm.num(here.z)).Exists ? Top.Ceiling : Top.None;
    }

    /// <summary>
    /// DEVIATION(DV97): a shot hits its cell's own ceiling, which is no atom: tg's hit chain on the cell's turf (<c>on_hit</c>: the hit's sound and
    /// impact effect, at the ceiling's height) with <see cref="obj_projectile.dm_hit_ceiling"/> set, so what a hit does to the surface it hit lands on
    /// the ceiling, not on the floor under it.
    /// </summary>
    public static async DmTask HitCeiling(obj_projectile p)
    {
        if (p.loc is not turf cell)
            return;
        p.dm_hit_ceiling = true;
        try
        {
            await p.on_hit(cell);
        }
        finally
        {
            p.dm_hit_ceiling = false;
        }
    }

    /// <summary>
    /// DEVIATION(DV577): an aimed shot's dent on the floor or the ceiling it hit, as tg's on a wall (<c>add_dent</c>, walls.dm:309: effects.dmi's
    /// bullet_hole at <c>BULLET_HOLE_LAYER</c>, at most <c>MAX_DENT_DECALS</c>): an overlay of the turf of the cell whose floor (<c>dir</c> DOWN) or
    /// top (<c>dir</c> UP: its own ceiling, or the floor of the level over it hit from under it) the shot hit, where its line met it (the
    /// projectile's pixel offsets in that cell). tg's floors take no dents.
    /// </summary>
    public static void DentSurface(obj_projectile p, turf target)
    {
        if (p.loc is not turf cell)
            return;
        bool top = p.dm_hit_ceiling || Dm.num(target.z) > Dm.num(cell.z);
        turf_open holder = (top ? cell : target) as turf_open;
        if (holder == null || Dm.num(LAZYLEN(holder.dm_dent_decals)) >= MAX_DENT_DECALS)
            return;
        mutable_appearance decal = mutable_appearance(Dm.resource("icons/effects/effects.dmi"), "bullet_hole", BULLET_HOLE_LAYER) as mutable_appearance;
        decal.pixel_w = (float)Math.Round(Math.Clamp(Dm.num(p.pixel_x), -PixelsPerTile / 2, PixelsPerTile / 2));
        decal.pixel_z = (float)Math.Round(Math.Clamp(Dm.num(p.pixel_y), -PixelsPerTile / 2, PixelsPerTile / 2));
        decal.dir = top ? UP : DOWN;
        if (Dm.truthy(LAZYLEN(holder.dm_dent_decals)))
        {
            holder.cut_overlay(holder.dm_dent_decals);
            Dm.append(ref holder.dm_dent_decals, decal);
        }
        else
            holder.dm_dent_decals = Dm.list(decal);
        holder.add_overlay(holder.dm_dent_decals);
    }

    /// <summary>DV577: the dents on the top of the cell of <paramref name="cell"/> go with the ceiling or the floor over it that changed.</summary>
    public static void ClearTopDents(turf cell)
    {
        if (cell is not turf_open open || !Dm.truthy(LAZYLEN(open.dm_dent_decals)))
            return;
        DmList kept = Dm.list();
        foreach (mutable_appearance dent in Dm.each<mutable_appearance>(open.dm_dent_decals))
            if (Dm.num(dent.dir) != UP)
                Dm.append(ref kept, dent);
        if (kept.len == open.dm_dent_decals.len)
            return;
        open.cut_overlay(open.dm_dent_decals);
        open.dm_dent_decals = kept.len > 0 ? kept : null;
        if (kept.len > 0)
            open.add_overlay(kept);
    }

    // ---- the stairs' flight (DV526) ----

    /// <summary>
    /// DV526: how far up the stairs' flight a point is, 0 at its low edge to 1 at its high edge: <paramref name="east"/>, <paramref name="north"/> are
    /// tiles from the tile's centre, <paramref name="dir"/> BYOND's dir the stairs go up by (the client's <c>StairRamp.Progress</c>); NaN for no ramp.
    /// </summary>
    public static double FlightProgress(int dir, double east, double north)
    {
        double along = dir switch { 1 => north, 2 => -north, 4 => east, 8 => -east, _ => double.NaN };
        return double.IsNaN(along) ? double.NaN : Math.Clamp(along + 0.5, 0.0, 1.0);
    }

    /// <summary>
    /// DEVIATION(DV526): the height (tile pixels over the floor) of the stairs' flight at the absolute point (<paramref name="x"/>, <paramref name="y"/>)
    /// over <paramref name="t"/>: the 3D client draws tg's stairs under a level as a flight rising that level's height over their tile (DV370) and lifts
    /// what stands on it so; false where it draws none (no stairs, a diagonal, no level above: the flat picture).
    /// </summary>
    public static bool TryFlight(turf t, double x, double y, out double height, out int dir)
    {
        height = 0;
        dir = 0;
        if (t == null || GET_TURF_ABOVE(t) is not turf)
            return false;
        foreach (obj_structure_stairs stairs in Dm.each<obj_structure_stairs>(t))
        {
            double progress = FlightProgress((int)Dm.num(stairs.dir), x / PixelsPerTile - Dm.num(t.x), y / PixelsPerTile - Dm.num(t.y));
            if (double.IsNaN(progress))
                continue;
            dir = (int)Dm.num(stairs.dir);
            height = progress * CeilingPixels;
            return true;
        }
        return false;
    }

    /// <summary>
    /// DV526: how much farther (horizontal pixels) the shot flies in its turf before its line meets the stairs' flight there (0 when it is under it
    /// already: the flight is solid to the floor); infinity for none. The flight rises one level over the tile, a pixel a pixel along its dir.
    /// </summary>
    public static double DistanceToFlight(obj_projectile p)
    {
        if (p.loc is not turf here)
            return double.PositiveInfinity;
        (double x, double y) = PointOf(p);
        if (!TryFlight(here, x, y, out double floor, out int dir))
            return double.PositiveInfinity;
        double h = HeightAt(p, x, y);
        if (h <= floor)
            return 0;
        (double ux, double uy) = Heading(Dm.num(p.angle));
        double closing = (dir switch { 1 => uy, 2 => -uy, 4 => ux, _ => -ux }) - p.dm_slope;
        return closing > 1e-9 ? (h - floor) / closing : double.PositiveInfinity;
    }

    /// <summary>
    /// DV526: an aimed shot whose eye is over the top of its level (a climber high on the stairs' flight) starts on the level above, its heights
    /// counted from that floor, when the way up is open there (the open space over the stairs); else its eye is the top of its level.
    /// </summary>
    public static async DmTask StartOnLevelAbove(obj_projectile p)
    {
        if (p.dm_origin_h <= CeilingPixels)
            return;
        if (p.loc is turf here && !CeilingSystem.Of(World.Current).At((int)Dm.num(here.x), (int)Dm.num(here.y), (int)Dm.num(here.z)).Exists
            && Dm.num(here.zPassOut(UP)) != 0 && GET_TURF_ABOVE(here) is turf above && Dm.num(above.zPassIn(UP)) != 0)
        {
            p.starting = above;
            await p.forceMove(above);
            if (p.loc == above)
            {
                p.dm_origin_h -= CeilingPixels;
                return;
            }
        }
        p.dm_origin_h = CeilingPixels;
    }

    /// <summary>
    /// DV525: a ladder hatch's opening in its tile, in shares of half the tile from its centre, west to east and south to north (the client's
    /// <c>WorldGeometry.HatchWest</c> and on: 1.2 x 0.9 m at the tile's north side).
    /// </summary>
    public const float HatchWest = -0.6f, HatchEast = 0.6f, HatchSouth = 0.05f, HatchNorth = 0.95f;

    /// <summary>DV525: the shot's point now is over the opening of a ladder's hole cut out of <paramref name="floor"/>.</summary>
    public static bool InHatch(obj_projectile p, turf floor)
    {
        if (!Dm.each<obj_effect_abstract_ladder_hole>(floor).Any())
            return false;
        (double x, double y) = PointOf(p);
        double half = PixelsPerTile / 2;
        double e = (x - Dm.num(floor.x) * PixelsPerTile) / half, n = (y - Dm.num(floor.y) * PixelsPerTile) / half;
        return e >= HatchWest && e <= HatchEast && n >= HatchSouth && n <= HatchNorth;
    }

    /// <summary>
    /// DEVIATION(DV414): an aimed bullet whose line reached the floor or a ceiling bounces off it as tg's bounce off a wall: the same number of
    /// bounces left, chance and decay (impact's ricochet block), the same leeway (a bullet bounces only at a grazing angle, here its pitch),
    /// and a floor or a ceiling is hard and not shiny (bullets and bombs bounce, energy shots do not). The line starts again at the point with
    /// its slope turned over. False when it does not bounce: no floor there (space, open space) or no ceiling, or tg's rolls say no.
    /// </summary>
    public static bool Bounce(obj_projectile p)
    {
        if (p.loc is not turf here || p.dm_slope == 0f || p.ricochets >= p.ricochets_max || !Dm.truthy(Dm.@in(p.armor_flag, Dm.list(BOMB, BULLET))))
            return false;
        if (p.dm_slope < 0f ? isspaceturf(here) || here is turf_open_openspace : Overhead(here, out _) == Top.None)
            return false;
        if (Dm.truthy(p.ricochet_incidence_leeway) && Math.Atan(Math.Abs(p.dm_slope)) * 180.0 / Math.PI > p.ricochet_incidence_leeway)
            return false;
        if (!Dm.truthy(p.check_ricochet(here)))
            return false;
        Rebase(p);
        // the bounce is seen: the bullet's own hit effect at the point (its sparks; the first tracer ends there)
        if (p.impact_effect_type != null && !Dm.truthy(p.hitscan))
            MarkImpact(p, Dm.@new(p.impact_effect_type, here, p.pixel_x, p.pixel_y) as atom);
        p.dm_slope = -p.dm_slope;
        // tg: impact()'s ricochet block (projectile.dm:487)
        p.on_ricochet(here);
        p.impacted = Dm.list();
        p.ignore_source_check = TRUE;
        p.maximum_range = max(0, Dm.sub(p.maximum_range, p.reflect_range_decrease));
        p.ricochet_chance *= p.ricochet_decay_chance;
        p.damage = Dm.num(p.damage) * p.ricochet_decay_damage;
        p.stamina *= p.ricochet_decay_damage;
        p.range = Dm.num_or_null(p.maximum_range);
        return true;
    }

    /// <summary>Starts the line at the projectile's point and height now, keeping its slope (a ricochet or a reflection turns it level, D177).</summary>
    public static void Rebase(obj_projectile p)
    {
        (double x, double y) = PointOf(p);
        float h = (float)Math.Clamp(HeightAt(p, x, y), 0.0, CeilingPixels);
        p.dm_origin_x = (float)x;
        p.dm_origin_y = (float)y;
        p.dm_origin_h = h;
    }

    /// <summary>DEVIATION(DV96): the share of tg's homing offsets (<c>homing_inaccuracy_min</c> to <c>_max</c>, pixels) a 3D shot keeps.</summary>
    public const float HomingOffsetScale = 0.3f;

    /// <summary>
    /// DEVIATION(DV96): a homing shot (tg's <c>process_homing</c>, which has just turned its heading) homes in 3D: its line starts again at its
    /// point and height now, so the line the hits are found on (<see cref="Cross"/>) runs along its new heading, and its pitch turns toward
    /// <paramref name="target"/>'s middle (a mob's, <see cref="UnaimedHeightPixels"/> over its floor) by at most <paramref name="turnDegrees"/>,
    /// the turn tg allows its heading. Without the pitch a shot the cone threw up or down flew over or under the man it homed on.
    /// </summary>
    public static void Home(obj_projectile p, atom target, double turnDegrees)
    {
        if (!p.dm_shot || target == null || get_turf(target) is not turf at)
            return;
        Rebase(p);
        (double x, double y) = PointOf(p);
        (double tx, double ty) = target is mob_living living ? (CentreOf(living, p).X, CentreOf(living, p).Y) : (Dm.num(at.x) * PixelsPerTile, Dm.num(at.y) * PixelsPerTile);
        double distance = Math.Sqrt((tx - x) * (tx - x) + (ty - y) * (ty - y));
        if (distance < 1)
            return;
        double rise = UnaimedHeightPixels + (Dm.num(at.z) - Dm.num(p.z)) * CeilingPixels - p.dm_origin_h;
        double pitch = Math.Atan(p.dm_slope) * 180 / Math.PI, wanted = Math.Atan2(rise, distance) * 180 / Math.PI;
        p.dm_slope = SlopeOf(pitch + Math.Clamp(wanted - pitch, -turnDegrees, turnDegrees));
    }

    /// <summary>An unaimed shot's line: level at <see cref="UnaimedHeightPixels"/> from its point now (tg's 2D shot, drawn at a mob's middle).</summary>
    public static void SetUnaimed(obj_projectile p)
    {
        (double x, double y) = PointOf(p);
        p.dm_shot = true;
        p.dm_origin_x = (float)x;
        p.dm_origin_y = (float)y;
        p.dm_origin_h = UnaimedHeightPixels;
        p.dm_slope = 0f;
    }

    /// <summary>
    /// DEVIATION(DV96): tg's deviation <paramref name="deviation"/> (degrees, the sum of the gun's spread rolls) as a 3D cone: for an aimed
    /// shot the world RNG draws one more value, <c>phi = rand(0, 359)</c>, after tg's own rolls, and the aim turns by |d| about it
    /// (yaw by |d| cos phi, pitch by |d| sin phi), |d| times the server's share of it (<see cref="ShotSpread"/>). No draw for a deviation of 0.
    /// </summary>
    public static (double Yaw, double Pitch) Scatter(double yawDegrees, double pitchDegrees, double deviation)
    {
        if (deviation == 0)
            return (yawDegrees, pitchDegrees);
        double phi = Dm.rand(0, 359) * Math.PI / 180.0;
        double d = Math.Abs(deviation) * ShotSpread.ScaleOf(World.CurrentOrNull); // the server's share of tg's cone
        return (yawDegrees + d * Math.Cos(phi), Math.Clamp(pitchDegrees + d * Math.Sin(phi), -89.0, 89.0));
    }

    /// <summary>The slope (height pixels per horizontal pixel) of a pitch in degrees.</summary>
    public static float SlopeOf(double pitchDegrees) => (float)Math.Tan(Math.Clamp(pitchDegrees, -89.0, 89.0) * Math.PI / 180.0);

    // ---- the effects ----

    /// <summary>A tracer or a muzzle flash of <paramref name="p"/>: the shot's own line.</summary>
    public static void CopyLine(obj_projectile p, atom effect)
    {
        if (effect is not obj_effect fx || !p.dm_shot)
            return;
        fx.dm_shot = true;
        fx.dm_origin_x = p.dm_origin_x;
        fx.dm_origin_y = p.dm_origin_y;
        fx.dm_origin_h = p.dm_origin_h;
        fx.dm_slope = p.dm_slope;
        fx.dm_shot_angle = Dm.num(p.angle);
    }

    /// <summary>How far to the side of a mech's heading its arms are, tile pixels (DV361: a mech fills its tile).</summary>
    public const float MechArmSidePixels = 10f;

    /// <summary>
    /// DEVIATION(DV361): a mech's firing effect carries its shot's line, started <see cref="MechArmSidePixels"/> to the side of the heading the
    /// arm that fired is on (the right arm's to its right): the others' clients draw its flash and the bolt from that arm. The shot's own line
    /// stays at the pilot's eye (DV94).
    /// </summary>
    public static void MarkArm(obj_projectile p, atom effect, bool right)
    {
        CopyLine(p, effect);
        if (effect is not obj_effect fx || !fx.dm_shot)
            return;
        double a = fx.dm_shot_angle * Math.PI / 180.0, side = right ? MechArmSidePixels : -MechArmSidePixels;
        fx.dm_origin_x += (float)(Math.Cos(a) * side); // the heading's right is (cos a, -sin a): clockwise from north
        fx.dm_origin_y -= (float)(Math.Sin(a) * side);
    }

    /// <summary>
    /// An impact of <paramref name="p"/>: level at the shot's height over the effect's own point (its turf and pixel offsets), between the floor
    /// and the ceiling. DEVIATION(DV399): on a closed turf the point is where the shot's line meets the turf's side (<see cref="EnterTile"/>).
    /// </summary>
    public static void MarkImpact(obj_projectile p, atom effect)
    {
        if (effect is not obj_effect fx || !p.dm_shot || effect.loc is not turf)
            return;
        double x = Dm.num(effect.x) * PixelsPerTile + effect.pixel_x, y = Dm.num(effect.y) * PixelsPerTile + effect.pixel_y;
        if (effect.loc is turf_closed && EnterTile(p.dm_origin_x, p.dm_origin_y, Dm.num(p.angle), Dm.num(effect.x), Dm.num(effect.y)) is { } side)
            (x, y) = side;
        // an effect on the level over the shot's (the floor above it, hit from under it) is on that level's floor, where the line met it (tg's impact
        // point is a few pixels farther on, which a rising line puts over that floor)
        bool above = effect.loc is turf on && p.loc is turf from && Dm.num(on.z) > Dm.num(from.z);
        fx.dm_shot = true;
        fx.dm_origin_x = (float)x;
        fx.dm_origin_y = (float)y;
        fx.dm_origin_h = above ? 0f : (float)Math.Clamp(HeightAt(p, x, y), 0.0, CeilingPixels);
        fx.dm_slope = 0f;
        fx.dm_shot_angle = Dm.num(p.angle);
    }

    /// <summary>
    /// Where the line from the absolute point (<paramref name="originX"/>, <paramref name="originY"/>) along the heading <paramref name="degrees"/>
    /// enters the tile (<paramref name="tileX"/>, <paramref name="tileY"/>), half a pixel before its side; null when the line starts in the tile
    /// or passes it by.
    /// </summary>
    public static (double X, double Y)? EnterTile(double originX, double originY, double degrees, double tileX, double tileY)
    {
        (double hx, double hy) = Heading(degrees);
        double half = PixelsPerTile / 2, enter = double.NegativeInfinity, exit = double.PositiveInfinity;
        foreach ((double o, double h, double c) in new[] { (originX, hx, tileX * PixelsPerTile), (originY, hy, tileY * PixelsPerTile) })
        {
            if (Math.Abs(h) < 1e-9)
            {
                if (Math.Abs(o - c) > half)
                    return null;
                continue;
            }
            double a = (c - half - o) / h, b = (c + half - o) / h;
            enter = Math.Max(enter, Math.Min(a, b));
            exit = Math.Min(exit, Math.Max(a, b));
        }
        if (enter <= 0 || enter > exit)
            return null;
        return (originX + hx * (enter - 0.5), originY + hy * (enter - 0.5));
    }

    /// <summary>How long the hit body part stays lit, deciseconds.</summary>
    public const float HitFlashDeciseconds = 3f;

    /// <summary>
    /// DEVIATION(DV400): a shot's hit on a living mob is shown on the mob, for everyone who sees it: its picture jolts 2 pixels to a side and
    /// back (an animation beside whatever runs on it; fixed steps, so the world's RNG is not drawn from as tg's <c>Shake</c> would), and a
    /// carbon's hit body part (<paramref name="zone"/>, tg's <c>check_hit_limb_zone_name</c>) is lit for <see cref="HitFlashDeciseconds"/>:
    /// the part's own cached images as one overlay over the picture, bright red.
    /// </summary>
    public static void ShowHit(mob_living victim, object zone)
    {
        if (victim == null || QDELETED(victim))
            return;
        Dm.animate(victim, Dm.named("pixel_x", 2), Dm.named("time", 0.5f), Dm.named("flags", (int)ANIMATION_PARALLEL | (int)ANIMATION_RELATIVE));
        Dm.animate(Dm.named("pixel_x", -4), Dm.named("time", 1), Dm.named("flags", ANIMATION_RELATIVE));
        Dm.animate(Dm.named("pixel_x", 2), Dm.named("time", 0.5f), Dm.named("flags", ANIMATION_RELATIVE));
        if (victim is not mob_living_carbon carbon || !Dm.truthy(zone) || !Dm.truthy(carbon.icon_render_keys?[zone]))
            return;
        if (mob_living_carbon.limb_icon_cache[carbon.icon_render_keys[zone]] is not DmList parts || parts.len == 0)
            return;
        mutable_appearance flash = mutable_appearance() as mutable_appearance;
        flash.appearance_flags = (int)KEEP_TOGETHER | (int)RESET_COLOR;
        // every drawn pixel of the part becomes the same bright red, most of its alpha kept: seen on any clothes
        flash.color = Dm.list(
            0f, 0f, 0f, 0f,
            0f, 0f, 0f, 0f,
            0f, 0f, 0f, 0f,
            0f, 0f, 0f, 0.85f,
            1f, 0.25f, 0.2f, 0f);
        Dm.combine(ref flash.overlays, parts);
        victim.add_overlay(flash);
        _addtimer(Dm.@new<datum_callback>(victim, nameof(atom.cut_overlay), flash), HitFlashDeciseconds, file: "anomaly/ShotGeometry.ShowHit", line: 0);
    }

    // ---- the crossing (DV95) ----

    /// <summary>What the crossing test says of a target.</summary>
    public enum Crossing
    {
        /// <summary>Not a carbon with body-part images: tg's 2D rule applies.</summary>
        NotApplicable,

        /// <summary>The line passes beside the drawn body.</summary>
        Miss,

        /// <summary>The line crosses a body part (<see cref="Cross"/>'s zone).</summary>
        Hit,
    }

    /// <summary>
    /// DEVIATION(DV95): where the shot's 3D line crosses <paramref name="target"/>'s picture: a carbon's body part (the zone, tg's
    /// <c>BODY_ZONE_*</c>, the hands counting as their arms), or a miss beside it. The picture stands at the mob's centre (its continuous
    /// position, else its tile's centre, plus its pixel offsets at the mob scale) facing back along the shot's heading, so the shooter
    /// sees it so; a crossing behind the line's start misses.
    /// </summary>
    public static Crossing Cross(obj_projectile p, mob_living target, out string zone)
    {
        zone = null;
        if (target is not mob_living_carbon carbon || carbon.overlays_standing?[BODYPARTS_LAYER] is not DmList parts || parts.Count == 0)
            return Crossing.NotApplicable;
        (double cx, double cy, byte mobYaw) = CentreOf(target, p);
        (double ux, double uy) = Heading(Dm.num(p.angle));
        double along = (cx - p.dm_origin_x) * ux + (cy - p.dm_origin_y) * uy;
        if (along < 0)
            return Crossing.Miss;
        double px = p.dm_origin_x + ux * along, py = p.dm_origin_y + uy * along;
        double lateral = (px - cx) * uy - (py - cy) * ux; // along the viewer's right, (uy, -ux)
        double height = p.dm_origin_h + p.dm_slope * along;
        // DV526: a mob on the stairs' flight is drawn lifted along it
        turf standing = ShotRewind.TryPointFor(p, target, out _, out _, out int seenZ) ? locate((float)Math.Floor(cx / PixelsPerTile + 0.5), (float)Math.Floor(cy / PixelsPerTile + 0.5), seenZ) : target.loc as turf;
        if (TryFlight(standing, cx, cy, out double lift, out _))
            height -= lift;
        bool lying = target.body_position == LYING_DOWN;
        byte dir = lying ? LyingFrame(target, mobYaw, cx, cy, cx - ux, cy - uy) : ChooseDir(mobYaw, cx, cy, cx - ux, cy - uy);
        zone = ZoneAtPoint(Layers(parts, dir), PlacementOf(target), lateral / MobScale, height / MobScale);
        return zone == null ? Crossing.Miss : Crossing.Hit;
    }

    /// <summary>
    /// DEVIATION(DV95): the zone at a point of the carbon's drawn picture in frame <paramref name="dir"/>, <paramref name="right"/> icon pixels right of
    /// its centre and <paramref name="up"/> over the floor (the mob scale undone), null for none. Package T, slice T2: the body is placed as the client
    /// draws it (<c>AppearanceCompositor.TryRootPlacement</c>: the root's transform about the icon's middle, then its <c>pixel_w</c> and
    /// <c>pixel_z</c>; a giant's picture reaches past the icon square), its lowest drawn body row on the floor (the client's billboard).
    /// </summary>
    public static string ZoneAtPoint(mob_living_carbon carbon, byte dir, double right, double up)
        => carbon.overlays_standing?[BODYPARTS_LAYER] is DmList parts ? ZoneAtPoint(Layers(parts, dir), PlacementOf(carbon), right, up) : null;

    private static string ZoneAtPoint(List<Layer> layers, Affine? placement, double right, double up)
    {
        int trim = TrimRows(layers, placement);
        int col = (int)Math.Floor(right + 16), row = (int)Math.Floor(up + trim);
        int sx = col, sy = row;
        if (placement.HasValue && !Unturn(placement.Value, col, row, out sx, out sy))
            return null;
        return ZoneAt(layers, sx, sy);
    }

    /// <summary>
    /// DEVIATION(DV95): of the mobs an aimed shot's line crosses on one turf (select_target's list), the nearest along the shot's heading;
    /// the shot's <c>def_zone</c> becomes the zone it crosses on that one (tg picks one at random).
    /// </summary>
    public static mob_living Nearest(obj_projectile p, DmList crossed)
    {
        (double ux, double uy) = Heading(Dm.num(p.angle));
        mob_living nearest = null;
        double best = double.PositiveInfinity;
        foreach (mob_living living in Dm.each<mob_living>(crossed))
        {
            (double cx, double cy, _) = CentreOf(living, p);
            double along = (cx - p.dm_origin_x) * ux + (cy - p.dm_origin_y) * uy;
            if (along < best)
                (best, nearest) = (along, living);
        }
        if (nearest != null && Cross(p, nearest, out string zone) == Crossing.Hit)
            p.def_zone = zone;
        return nearest;
    }

    /// <summary>The zone under tg's picture pixel (<paramref name="iconX"/>, <paramref name="iconY"/>, 1 to 32) of the carbon's untransformed frame <paramref name="dir"/>, null for none (the server's grid of R1).</summary>
    public static string ZoneAt(mob_living_carbon carbon, byte dir, int iconX, int iconY)
        => carbon.overlays_standing?[BODYPARTS_LAYER] is DmList parts ? ZoneAt(Layers(parts, dir), iconX - 1, iconY - 1) : null;

    // The mob's centre in absolute tile pixels and its facing (the client's billboard frame reads the high byte of the capsule's yaw).
    private static (double X, double Y, byte Yaw) CentreOf(mob_living target, obj_projectile p)
    {
        double ox = target.pixel_x * MobScale, oy = target.pixel_y * MobScale; // the root's pixel offsets: a ground offset at the mob scale
        if (World.Current is World world && LocomotionSeam.TryGetState(world, target.dm_id, out Anomaly.Core.Motion.MotionState state, out _) && target.loc is turf)
        {
            // DV527: a rewound shot's point of the mob then
            if (ShotRewind.TryPointFor(p, target, out double seenX, out double seenY, out _))
                return (seenX + ox, seenY + oy, (byte)(state.Yaw >> 8));
            return (state.X / 8.0 + ox, state.Y / 8.0 + oy, (byte)(state.Yaw >> 8));
        }
        return (Dm.num(target.x) * PixelsPerTile + ox, Dm.num(target.y) * PixelsPerTile + oy, YawOfDir((byte)Dm.num(target.dir)));
    }

    private static bool IsLimp(mob_living target) => Dm.num(target.stat) != 0; // the replicator's limp posture bit: stat 0 is conscious

    /// <summary>
    /// The frame a lying <paramref name="target"/> shows a viewer at (<paramref name="viewerX"/>, <paramref name="viewerY"/>), from its own state as the
    /// replicator sends it in the posture byte: limp, lying buckled to an object (slice T4c) and the side it was turned to (DV588).
    /// </summary>
    public static byte LyingFrame(mob_living target, byte yaw, double mobX, double mobY, double viewerX, double viewerY)
        => ChooseLyingDir(yaw, mobX, mobY, viewerX, viewerY, IsLimp(target), target.dm_lies_buckled, target.dm_turned_face_up);

    // One body-part image: its zone, its mask in the frame (rows from the bottom), its pixel offsets and its layer order.
    private readonly record struct Layer(string Zone, uint[] Rows, int OffsetX, int OffsetY);

    private static List<Layer> Layers(DmList parts, byte dir)
    {
        var found = new List<(float Layer, int Order, Layer Item)>();
        int order = 0;
        foreach (object item in Dm.each(parts))
        {
            order++;
            if (item is not image img || img.icon_state is not string state)
                continue;
            string zone = ZoneOfState(state);
            if (zone == null)
                continue;
            uint[] rows = BodyMasks.Find(FileOf(img.icon), state, dir) ?? BodyMasks.Find(BodyMasks.HumanFile, "human_" + StateZoneSuffix(state), dir);
            if (rows == null)
                continue;
            found.Add((img.layer, order, new Layer(zone, Cut(img.icon, rows, dir), (int)Math.Round(img.pixel_x), (int)Math.Round(img.pixel_y))));
        }
        // BYOND draws a higher layer over a lower one and, on one layer, the later overlay over the earlier: the topmost first
        found.Sort((a, b) => a.Layer != b.Layer ? b.Layer.CompareTo(a.Layer) : b.Order.CompareTo(a.Order));
        return found.Select(f => f.Item).ToList();
    }

    // Package T, slice T2: a made icon's mask with its Blends by a mask in the table applied (ICON_MULTIPLY keeps the pixels both have): tg's handle_masking
    // cuts each leg into the part over the other leg (leg_masks.dmi right_leg/left_leg, on BODYPARTS_LAYER) and the rest (the _lower states, BODYPARTS_LOW_LAYER).
    private static uint[] Cut(object picture, uint[] rows, byte dir)
    {
        if (picture is not icon made)
            return rows;
        foreach ((int function, object with, int x, int y) in made.dm_ops)
        {
            if (function != (int)ICON_MULTIPLY || x != 1 || y != 1 || with is not icon mask || BodyMasks.Find(FileOf(mask.dm_file), mask.dm_state, dir) is not uint[] cut)
                continue;
            var kept = new uint[32];
            for (int row = 0; row < 32; row++)
                kept[row] = rows[row] & cut[row];
            rows = kept;
        }
        return rows;
    }

    private static string ZoneAt(List<Layer> layers, int x, int y)
    {
        foreach (Layer layer in layers)
        {
            int lx = x - layer.OffsetX, ly = y - layer.OffsetY;
            if (lx >= 0 && lx < 32 && ly >= 0 && ly < 32 && (layer.Rows[ly] & (1u << lx)) != 0)
                return layer.Zone;
        }
        return null;
    }

    // The row of the lowest drawn body pixel of the placed picture (below the icon square's bottom when negative): the client stands the picture's lowest
    // opaque row on the floor. A placed picture reaches at most a square past the icon square (a giant's 1.25 plus its lift, a raised body's pixel_z).
    private static int TrimRows(List<Layer> layers, Affine? placement)
    {
        int from = placement.HasValue ? -32 : 0, to = placement.HasValue ? 64 : 32;
        for (int y = from; y < to; y++)
            for (int x = from; x < to; x++)
            {
                int sx = x, sy = y;
                if (placement.HasValue && !Unturn(placement.Value, x, y, out sx, out sy))
                    continue;
                if (ZoneAt(layers, sx, sy) != null)
                    return y;
            }
        return 0;
    }

    // ---- the client's mirrored functions (Shared/Icons/BillboardMath.cs, Shared/Play/BodyZoneAim.cs) ----

    private static ReadOnlySpan<byte> FrontClockwise => [2, 6, 4, 5, 1, 9, 8, 10]; // S, SE, E, NE, N, NW, W, SW

    /// <summary>The client's <c>BillboardMath.ChooseDir</c> for a four-direction icon (tg's body parts).</summary>
    public static byte ChooseDir(byte yaw, double mobX, double mobY, double viewerX, double viewerY)
    {
        double dx = viewerX - mobX, dy = viewerY - mobY;
        if (dx * dx + dy * dy < 1e-9)
            return 2;
        double relative = Bearing(dx, dy) - yaw * 360.0 / 256.0;
        relative = ((relative % 360.0) + 360.0) % 360.0;
        return FrontClockwise[((int)Math.Floor((relative + 45.0) / 90.0) % 4) * 2];
    }

    /// <summary>
    /// The client's <c>BillboardMath.ChooseLyingDir</c>: a body turned over shows the side it was turned to (DV588, <paramref name="turnedFaceUp"/>), one
    /// lying buckled to an object its front (slice T4c), from every side; otherwise a conscious body lies face down (its back, north), a limp one by the
    /// viewer's side.
    /// </summary>
    public static byte ChooseLyingDir(byte yaw, double mobX, double mobY, double viewerX, double viewerY, bool limp, bool buckled = false, bool? turnedFaceUp = null)
    {
        if (turnedFaceUp is bool up)
            return up ? (byte)2 : (byte)1;
        if (buckled)
            return 2;
        if (!limp)
            return 1;
        double dx = viewerX - mobX, dy = viewerY - mobY;
        if (dx * dx + dy * dy < 1e-9)
            return 2;
        double relative = Bearing(dx, dy) - yaw * 360.0 / 256.0;
        return Math.Cos(relative * Math.PI / 180.0) >= -1e-9 ? (byte)2 : (byte)1;
    }

    /// <summary>The client's <c>BillboardMath.YawOfDir</c>.</summary>
    public static byte YawOfDir(byte dir)
    {
        int dx = ((dir & 4) != 0 ? 1 : 0) - ((dir & 8) != 0 ? 1 : 0);
        int dy = ((dir & 1) != 0 ? 1 : 0) - ((dir & 2) != 0 ? 1 : 0);
        if (dx == 0 && dy == 0)
            return 128;
        return (byte)(int)Math.Round(Bearing(dx, dy) * 256.0 / 360.0);
    }

    private static double Bearing(double dx, double dy)
    {
        double bearing = Math.Atan2(dx, dy) * 180.0 / Math.PI;
        return bearing < 0 ? bearing + 360.0 : bearing;
    }

    /// <summary>The client's <c>BodyZoneAim.ZoneOfState</c> as tg's zone: <c>human_head_m</c> the head, a hand its arm.</summary>
    public static string ZoneOfState(string state)
    {
        string suffix = StateZoneSuffix(state);
        return suffix switch
        {
            "head" => BODY_ZONE_HEAD,
            "chest" => BODY_ZONE_CHEST,
            "l_arm" or "l_hand" => BODY_ZONE_L_ARM,
            "r_arm" or "r_hand" => BODY_ZONE_R_ARM,
            "l_leg" => BODY_ZONE_L_LEG,
            "r_leg" => BODY_ZONE_R_LEG,
            _ => null,
        };
    }

    private static string StateZoneSuffix(string state)
    {
        if (string.IsNullOrEmpty(state))
            return null;
        if (state.EndsWith("_m", StringComparison.Ordinal) || state.EndsWith("_f", StringComparison.Ordinal))
            state = state.Substring(0, state.Length - 2);
        foreach (string zone in new[] { "head", "chest", "l_arm", "l_hand", "r_arm", "r_hand", "l_leg", "r_leg" })
            if (state.EndsWith("_" + zone, StringComparison.Ordinal))
                return zone;
        return null;
    }

    // BYOND's matrix(a, b, c, d, e, f): x' = a x + b y + c, y' = d x + e y + f, turned about the icon square's middle (the client's compositor).
    private readonly record struct Affine(double A, double B, double C, double D, double E, double F);

    // The root's placement in its picture (the client's AppearanceCompositor.TryRootPlacement): its transform, then its pixel_w and pixel_z (a body raised by a
    // stool or a table, moved by a handrail); null for none.
    private static Affine? PlacementOf(atom root)
    {
        matrix m = root.transform as matrix;
        var placement = m == null ? new Affine(1, 0, 0, 0, 1, 0) : new Affine(m.a, m.b, m.c, m.d, m.e, m.f);
        placement = placement with { C = placement.C + root.pixel_w, F = placement.F + root.pixel_z };
        return placement == new Affine(1, 0, 0, 0, 1, 0) ? null : placement;
    }

    // The source pixel under the placed picture's pixel (x, y from 0): BodyZoneAim.ZoneAt's undo of the root's placement.
    private static bool Unturn(Affine t, int x, int y, out int sx, out int sy)
    {
        sx = sy = 0;
        double det = t.A * t.E - t.B * t.D;
        if (Math.Abs(det) < 1e-9)
            return false;
        // the point is the centre of the pixel, relative to the square's middle; the transform is undone and moved back
        double px = x + 0.5 - 16 - t.C, py = y + 0.5 - 16 - t.F;
        double qx = (t.E * px - t.B * py) / det, qy = (-t.D * px + t.A * py) / det;
        sx = (int)Math.Floor(qx + 16);
        sy = (int)Math.Floor(qy + 16);
        return sx >= 0 && sx < 32 && sy >= 0 && sy < 32;
    }

    private static string FileOf(object icon) => icon switch
    {
        DmResource resource => resource.Path,
        icon made => FileOf(made.dm_file),
        string path => path,
        _ => null,
    };
}

/// <summary>
/// The committed body-part mask table (<c>Generated/BodyMasks.bin</c>, made by <c>tools/Anomaly.Tools.Icons --masks</c>): the opaque pixels of
/// tg's body-part icon states per direction, 32 rows of 32 bits (row 0 the bottom, bit 0 icon-x 1).
/// </summary>
public static class BodyMasks
{
    /// <summary>The human body parts' file, the fallback for a state the table lacks (the mask of the same zone).</summary>
    public const string HumanFile = "icons/mob/human/bodyparts_greyscale.dmi";

    private static readonly Lazy<Dictionary<(string File, string State), Dictionary<byte, uint[]>>> Table = new(Load);

    /// <summary>The number of states in the table.</summary>
    public static int Count => Table.Value.Count;

    /// <summary>The mask of <paramref name="state"/> of <paramref name="file"/> in direction <paramref name="dir"/> (a diagonal shows its east or west side, an absent one south), null when the table lacks the state.</summary>
    public static uint[] Find(string file, string state, byte dir)
    {
        if (file == null || state == null || !Table.Value.TryGetValue((file, state), out Dictionary<byte, uint[]> dirs))
            return null;
        if (dirs.TryGetValue(dir, out uint[] rows))
            return rows;
        if ((dir & 12) != 0 && dirs.TryGetValue((byte)(dir & 12), out rows))
            return rows;
        return dirs.TryGetValue(2, out rows) ? rows : dirs.Values.First();
    }

    private static Dictionary<(string, string), Dictionary<byte, uint[]>> Load()
    {
        var table = new Dictionary<(string, string), Dictionary<byte, uint[]>>();
        using Stream stream = typeof(BodyMasks).Assembly.GetManifestResourceStream("Anomaly.Core.Tg.BodyMasks.bin")
            ?? throw new InvalidOperationException("the body-part mask table is not embedded (Generated/BodyMasks.bin)");
        using var r = new BinaryReader(stream);
        if (Encoding.ASCII.GetString(r.ReadBytes(8)) != "ANOMBM1\n")
            throw new InvalidDataException("not a body-part mask table");
        int count = r.ReadUInt16();
        for (int i = 0; i < count; i++)
        {
            string file = Encoding.UTF8.GetString(r.ReadBytes(r.ReadByte()));
            string state = Encoding.UTF8.GetString(r.ReadBytes(r.ReadByte()));
            var dirs = new Dictionary<byte, uint[]>();
            for (int d = r.ReadByte(); d > 0; d--)
            {
                byte dir = r.ReadByte();
                var rows = new uint[32];
                for (int y = 0; y < 32; y++)
                    rows[y] = r.ReadUInt32();
                dirs[dir] = rows;
            }
            table[(file, state)] = dirs;
        }
        return table;
    }
}
