# Station 3D asset rules

These are persistent user requirements for modelling and revising station assets.

- Use Blender MCP for all modelling and model revisions. If it is unavailable,
  stop model work and report that the connection must be restored; do not substitute
  background Blender, CLI or another modelling interface.
- Current scope is models only. Review geometry, UVs, materials and poses in Blender.
  Do not launch Godot, perform in-game checks or change game integration/catalog
  bindings unless the user separately requests that work.
- Study the original game code, icon states, overlays and every available directional
  sprite before modelling. Most SS13 sprites use an elevated oblique view, roughly
  45 degrees. Do not interpret the complete sprite as a flat front or top elevation.
- Preserve original sprite sheets unchanged. Use UV regions from the appropriate
  surface and direction, nearest filtering and low-poly geometry.
- Review fronts, backs, both sides and top separately. Use the original rear/side
  sprite when available. Never stretch unrelated front details or atlas fragments
  across backs and sides. Where the source provides no useful surface detail, construct restrained panel borders,
  seams, fasteners and pixel-scale material variation using suitable original-atlas regions.
  A plain single-color fill is not an acceptable finished side or rear surface.
  Avoid noisy, scrambled or stretched sprite fragments.
- Preserve the recognizable silhouette and relative proportions before adding small
  bevels or detail. Interpret depth consistently across sprite directions. Use shared
  geometry for texture-only variants; do not duplicate models for cardinal rotations.
- Door seams follow pixel boundaries and the actual dark separation contours, not
  diagonals through stepped pixels. Keep sharp corners where the sprite is sharp.
  Glass has finished side faces/thickness and appropriate transparency; no accidental
  view into unfinished geometry. Original front/back decals must remain possible.
- Modular furniture must align exactly on the grid with compatible edge UVs. Include
  legs in the required modules, provide plain inserts and appropriate corner pieces,
  and avoid duplicate rotated modules. Inspect assembled straight and corner layouts.
- Respect the station grid, modular joins, single-floor height, gameplay states and
  animation clearance. Keep moving pieces separate, with pivots at real joints.
- Parts must visibly connect: legs to supporting frames, barrels to mounts, safety
  restraints to upper seat hinges. Check more than one viewing direction and moving
  poses, not just the overview render.
- Avoid unintended holes, missing caps, degenerate faces, flipped normals and torn
  geometry. Intended windows, recesses and openings require finished interior walls
  and thickness. Transparent effect/decal planes are explicit exceptions.
- **No z-fighting:** do not leave overlapping visible coplanar faces at joins. Remove
  redundant contact faces or construct a shared continuous surface. Do not blanket
  offset or merge moving parts. Decals and state overlays need a deliberate small
  surface offset, and mutually exclusive state meshes must not be visible together.
- Before delivery check closure/normals, inter-part contact, coplanar overlap, UVs,
  original texture identity, nearest sampling, and relevant animation poses. Numeric
  checks do not replace visual review. Report unreviewed scope honestly.
- Curtains may remain 2D. Do not add underfloor pipes, wires, vents, scrubbers or
  sensors unless the user explicitly changes that scope.
- Transit tubes and their stations/capsules are explicitly requested exceptions:
  use a genuinely round low-poly cross-section, not a stepped square pixel outline.

Current correction pass: `tools/art/repair_perspective_assets.py` is the authoritative
override for eight revised models. Run it after older batch builders if rebuilding
those batches; otherwise the old builders will restore their earlier shapes.

- For elevated orthographic machinery sprites, separate the top-plane region from
  the front-wall region before constructing depth; do not turn the upper region
  into a vertical monitor. Generic machine_frame (stock_parts box_0..2) is a box
  chassis with a horizontal roof opening, distinct from computer_frame. Its generic
  shape does not need to duplicate every machine that can be built from it.
- Every model must receive deliberate side and rear texture treatment, not only selected assets. Use directional source artwork where available; otherwise add restrained panel borders, seams and visible pixel-scale material variation from the original atlas. Do not leave sides or backs as a plain single-color fill. Check consistent pixel scale and avoid stretched strips, noisy patches and unrelated front details.
- A separate rear-view inspection is mandatory for every created or revised model before final delivery. A front or overview render alone is not sufficient. Verify rear geometry, connections, UVs, pixel scale and material treatment in Blender, alongside the front, both sides and top.
- Tram walls are textured cubes in the game. Do not create or revise tram wall/window wall models further; leave the existing authored wall models in place for the user to remove later. Continue with other tram equipment, rails, base and required states only.
