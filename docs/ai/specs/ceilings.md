# Independent ceilings

First iteration 2026-10-24 (DV183); rebuilt as floor blocks 2026-10-30 (D295). Protocol 58 requires matching client/server builds.

## Boundary model

`CeilingSystem` owns the upper boundary at `(x, y, z)` independently of replaceable turfs. Replacing or destroying a floor does not recreate or delete its ceiling. A boundary is one of tg's floor blocks (D295): a floor turf type (`/turf/open/floor/...`: plating, a floor tile's turf, glass floors, the reinforced floor) or, without a turf, the lattice or catwalk it was built from; plus tg's broken state (a 1-based pick from the turf's `broken_states()`) and the derived structural support. It is simulation state, included in the world hash; replication only reads it.

The boundary's opposite side is currently exterior vacuum and starlight. There is no traversable upper level. This is the future seam for sharing one boundary with the floor of the room above: replace the exterior endpoint with an adjacent atmospheric volume, preserve one authoritative boundary, and add separate upper/lower finishes. Do not create a second independently sealed floor at the same interface. Actual level traversal, vertical collision, stacked-room rendering and atmosphere between floors are future work.

Initial map conversion gives station floors and walls a plating ceiling. Space, openspace, `/area/space` and `/area/station/solars` do not receive one. Solar maintenance rooms keep their ceiling. This decision uses map types and areas, not gas pressure: an airless interior still has a roof. Map patches override individual defaults, including explicit removals. Rules run only at world initialization; newly constructed floors do not implicitly create roofs.

## Construction

Aim up at a boundary and left-click with the item in the active hand. With anything but a gun in the active hand (and throw mode off), a look up through a hole aims at the absent ceiling's plane, so rods or a floor tile build it there; Alt does the same with any hand. The recipes are tg's floor recipes, with tg's costs, waits and messages (`_open.dm` build_with_rods/build_with_floor_tiles, `plating.dm`, `floor.dm`, `reinforced_floor.dm`, `lattice.dm`). The server checks adjacency, incapacitation, the held item and the target revision before and after a timed action; moving away cancels it, and a second builder cannot complete against the first builder's changed surface. Anything else (cables, pipes, other items) is refused: floor things are not placed on a ceiling.

| Current boundary | Item/tool | Result | Cost or refund |
| --- | --- | --- | --- |
| None | Rods | Lattice (needs support, below) | 1 rod |
| Lattice | Rods | Catwalk | 1 rod |
| Lattice/catwalk, or none beside a floor block | Iron floor tile | Plating | 1 tile |
| Lattice / catwalk | Wirecutters | None | 1 / 2 rods |
| Plating | Any floor tile | That tile's floor (iron, dark, white, glass, reinforced glass, wood, ...) | 1 tile |
| Plating | Rods (3 s) | Reinforced floor | 2 rods |
| Plating | Plastitanium (1.5 s) | Insulated reinforced floor | 1 sheet |
| Broken plating | Welder | Fixed plating | Tool's normal checks |
| A floor tile's floor | Crowbar | Plating | The tile back (none from a broken floor) |
| Reinforced floor | Wrench (3 s) | Plating | Its rods back |

Plating cannot be taken apart by hand, as in tg (an RCD or an explosion); a broken plating takes no tile until welded. Plasteel's reinforced plating (a sequence of tool states) and RCD integration are deferred.

### Under a level above

A cell under a level above has no ceiling of its own (D325): the turf of that level is over it, and it is built and broken on that level. From below only the tile under that turf's plating is worked on (DV507; the owner, 2026-11-17: a floor between two levels has two sides, and a tile goes on the side looked at). The click is the same ceiling click, with the same checks.

| Seen from below | Item/tool | Result | Cost or refund |
| --- | --- | --- | --- |
| Plating (under a floor that is not glass, or under a wall) | Any floor tile but glass | That tile laid under the plating; the floor on top stays as it is | 1 tile |
| A laid tile | Crowbar | The bare plating | The tile back, into the room |
| A laid reinforced floor (a map's) | Wrench (3 s) | The bare plating | Its rods back |

Everything else is refused with a message: rods, plastitanium, a welder or a crowbar on that plating (it is the floor of the level above, reinforced and mended from there), a glass tile (glass takes the place of the plating, so it is laid from the level above), and anything used where the turf above is open space or glass. The laid tile is no part of the support, air or damage rules. When the turf over it loses its plating (it is taken away or made glass) the tile is not shown, and it is shown again once plating is back there. The hint names what is seen from below: `Ceiling: plating`, the laid tile's name, or the glass floor's.

Lattices connect through cardinal neighbors. Any connected component touching a capital wall or support column is supported, without a span limit. Iron sheets offer a four-sheet support-column recipe using the existing full-cell wall construction/destruction behavior. Losing the final anchor leaves the component intact and sealed but marks it unsupported; new lattices cannot extend it. Floor recipes, repair and dismantling remain possible. Restoring a wall restores support.

## Atmosphere, light, damage and pictures

A missing boundary, lattice or catwalk exposes the underlying open turf to vacuum once per atmospheric cycle through LINDA's existing immutable-space gas-sharing code. Surrounding turfs are activated so ordinary pressure equalization and pressure movement draw air, loose items and characters toward the breach. The outlet reports upward airflow for presentation but does not move an actor upward, change its level or delete it (DV179). Any floor turf closes the outlet.

Opaque floors block exterior starlight. Glass floors, lattices, catwalks and holes admit it without changing the floor's horizontal opacity or collision. Existing edge-space starlight stays bounded.

Explosions use tg's floor odds on the ceiling (`floor.dm:48`, `reinforced_floor.dm:55`), once per blast with the turf below: devastation removes the block; a heavy blast leaves a lattice, removes it, or scrapes one layer (an 80 % chance, else breaks the tile), sometimes dropping an iron sheet into the room; a light blast breaks the tile half the time. A directed blast counts as light. The ceiling has no contents to shield it, and the floor's fire stays with the floor below. A lattice or catwalk alone is an obj with tg's 50 integrity: a blast deals tg's obj damage (`obj_defense.dm:10`; nothing against a bomb), the damage is kept between hits, and at none left it falls apart, its rods dropping into the room below. Anything held that builds nothing there and does not have NOBLUDGEON hits it as tg's `attack_atom`/`attacked_by` do (force times demolition_mod, through its melee armour of 50, with the item's attack cooldown); a ceiling click obeys ClickOn's cooldown. Upward aimed projectiles stop at the ceiling and run their hit effects; like tg's floors, the block takes no bullet damage. Shots into absent ceilings leave the simulated plane. Acid, fire, singularities, collapse and falling debris are deferred.

The server draws each block with tg's own picture (`CeilingSystem.Appearance`): the type's appearance, tg's bitmask smoothing among the ceiling neighbours (glass floors, carpets, lattices; a lattice does not smooth with walls yet), and the broken overlay from `damaged_dmi`. The picture is interned in the public appearance table and sent with the cell; the client draws it the floors' way, glass with the transparent material. Unsupported components show orange edges. Existing ceiling visibility options still apply.

## Debug editing and persistence

The dev menu's spawn tool aimed at a ceiling, or up through a hole in one, puts the chosen floor turf (Spawn, Turfs), a lattice or a catwalk (Objects) there; the delete tool removes a ceiling on a click and scrapes one layer off on Shift + click. Commands are server-authoritative and require the existing dev-command permission:

```
ceiling none|lattice|catwalk|scrape|break|fix|/turf/open/floor/... X Y Z [WIDTH HEIGHT]
ceiling /turf/open/floor/glass 60 90 1 3 2
ceiling scrape 60 90 1
ceiling save
```

Debug edits bypass the recipes and the support rule. They affect the round only: what is over a tile of a map is set in the map editor (the Tile panel's Ceiling, the `ceiling` var of the tile's turf: DV507, docs/ai/specs/map-editor.md 5.12). Until 2026-11-17 a `ceiling save` wrote the round's edits into the map's patch file under `--map-edit`; the patch files are gone (D337), and none held a ceiling.

## Networking and verification

MapGrid includes all existing boundaries; Delta carries the turf type, lattice, broken pick, support, the picture's public-table index and the lattice's damage (version 58), and explicit removal records. A neighbour's change resends a cell whose smoothed picture changed. Ceiling targets are a distinct target kind using cell coordinates. Existing turf clicks retain their meaning. Wire validation rejects a non-floor turf, a lattice under a floor block, a broken empty cell, private or out-of-table pictures, duplicate coordinates, out-of-map join records and truncated data.

Tests cover independent floor replacement, solar defaults, support loss/restoration and diagonal rejection, cancellation and competing builders, the floor recipe lifecycle (lattice, catwalk, plating, tiles, crowbar, reinforced floor, refused cables and plating removal), broken plating, lattice damage kept between blasts and melee hits, light-blast sealing, explosion removal, smoothing and broken pictures, pressure sealing and horizontal breach flow, actor retention, wire validation, rendering plans, two connected clients, late join, gameplay clicks, patch persistence, and a tile laid under the plating of the level above and pried off again through the wire.
