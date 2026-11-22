# Service and kitchen

18 base models. Each has a GLB and editable Blender file.

Use the GLB in Godot. Keep original texture sampling at nearest. Inspect named child meshes and anchors before wiring animations to gameplay; mutually exclusive state clips must not play together. Dynamic contents are deliberately separate.

Source atlas metadata: `source_states.json`. Source audit and specific limitations are recorded below. Atlas verification checks RGBA identity and glTF sampler settings; geometry checks sample exported animation poses against floor and ceiling. They do not certify gameplay completeness.

| Model | Triangles including hidden state meshes | Coverage |
|---|---:|---|
| [photocopier.glb](photocopier.glb) | 116 | Source photocopier1 operation frames/timing, photocopier2 maintenance source retained. Separate input/output anchors; documents and books are runtime attachments. |
| [library_scanner.glb](library_scanner.glb) | 130 | Source bigscanner1 operation frames/timing, bigscanner2 maintenance source retained. Separate input/output anchors; documents and books are runtime attachments. |
| [bookbinder.glb](bookbinder.glb) | 90 | Source binder1 operation frames/timing, binder2 maintenance source retained. Separate input/output anchors; documents and books are runtime attachments. |
| [pda_painter.glb](pda_painter.glb) | 72 | Department variants share original pdapainter. Separate lid, power/broken panel states and PDA attachment; device paint is runtime data. |
| [photobooth.glb](photobooth.glb) | 134 | Hollow booth with seat, original flat curtain shown by closed/open state clips. Security uses same geometry. Occupant/photo items separate; curtain deliberately stays 2D. |
| [biogenerator.glb](biogenerator.glb) | 158 | Seven exclusive original biomass fill levels, screen/work/container overlays, separate hatch. Organic contents and reagent containers remain runtime objects. |
| [seed_extractor.glb](seed_extractor.glb) | 36 | Main scanner and side seed outlet, separate source maintenance cover. No source processing animation; product objects are separate. |
| [washing_machine.glb](washing_machine.glb) | 410 | Hollow cabinet, rotating drum and hinged circular door. Empty/full/bloody source content layers. Clothes are runtime attachments; running blood effect remains source metadata. |
| [coffee_machine_impressa.glb](coffee_machine_impressa.glb) | 72 | Tabletop Impressa with actual pot cavity and dispenser nozzle. Pot, cups, sugar and bean attachments are dynamic; their original source overlays retained, not baked into body. |
| [kitchen_oven.glb](kitchen_oven.glb) | 180 | Hollow oven, tray and bottom-hinged framed glass door. Source on/off lamp. Range adds two burners; oven has a tall backguard. Food and flame visuals are runtime attachments. |
| [kitchen_range.glb](kitchen_range.glb) | 224 | Hollow oven, tray and bottom-hinged framed glass door. Source on/off lamp. Range adds two burners; oven has a tall backguard. Food and flame visuals are runtime attachments. |
| [griddle.glb](griddle.glb) | 144 | One shared griddle for the three source control variants; original flat cooking surface remains clear for dynamically placed food. Food stand subtype is not present in this audit. |
| [deep_fryer.glb](deep_fryer.glb) | 216 | Two independently modelled baskets with handles and lowering/raising clips; off/on controls. Source start/stop interpreted as physical basket movement. Grease overlay and oil bubbles remain source metadata for runtime. |
| [food_processor.glb](food_processor.glb) | 36 | Same geometry layout for food/slime variants; original palette atlas and separate side maintenance cover. No invented rotating exterior; processing uses contents and sound in source. |
| [slime_processor.glb](slime_processor.glb) | 36 | Same geometry layout for food/slime variants; original palette atlas and separate side maintenance cover. No invented rotating exterior; processing uses contents and sound in source. |
| [gibber.glb](gibber.glb) | 134 | Open feed with opposing rotating jaws, source empty/loaded/active indicators. Body contents and blood tint/decal are runtime-owned; no body or gore baked into geometry. |
| [food_cart.glb](food_cart.glb) | 244 | Wheeled serving cart with original parasol silhouette and top compartments. Food and icecream selections supplied at runtime; no duplicate copies per menu item. |
| [icecream_cart.glb](icecream_cart.glb) | 244 | Wheeled serving cart with original parasol silhouette and top compartments. Food and icecream selections supplied at runtime; no duplicate copies per menu item. |
