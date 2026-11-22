# Vending machine geometry classification

67 machine appearances in the supplied atlas, grouped into 21 construction families. The source atlas is 800 × 800, tiles are 32 × 32; all 287 states retain source frame/direction metadata in the adjacent JSON.

Same family plus exact source silhouette, width and recess layout. Geometry and UV data are linked; per-state material selects atlas tile. Different silhouettes/layouts keep separate meshes.

Upper horizontal cap and edge bands are mapped to physical roof and stepped side edges. The front begins below the roof band. Side and rear service textures are derived separately from source colours and trim, never a repeated full frontal sprite.

| Family | Reusable appearances | Geometry |
|---|---|---|
| wardrobe | atmosdrobe, bardrobe, cargodrobe, centdrobe, chapdrobe, chefdrobe, chemdrobe, clothes, coroner_drobe, curadrobe, detdrobe, engidrobe, genedrobe, hydrobe, janidrobe, lawdrobe, medidrobe, robodrobe, scidrobe, secdrobe, virodrobe | Shared tall display and full-width delivery slot |
| snack | snack, snackblue, snackorange, snackgreen, snackteal, sustenance, sustenance_labor, nutri, seeds | Shared product window, right keypad and lower dispenser |
| branded_drink | Cola_Machine, cola_black, red_cola, space_up, pwr_game, shamblers_juice | Shared solid graphic front, side controls and recessed delivery slot |
| drink_display | soda, starkist | Large inset graphic display, centered delivery slot |
| magic | MagiVend, RunicVendor | Same display and delivery geometry; distinct magical artwork |
| wide_medical | med, drug | Wide upright cabinet, tall left display and lower-right dispenser |
| wide_service | cart, sec | Wide cabinet with left service display, right keypad and small dispenser |
| wide_electronics | robotics, modularpc | Wide horizontal display with broad lower dispenser; widths differ by source |
| wide_food | snackdonk, syndi, nt-donk, liberationstation | Wide solid advertising panel, right keypad and broad dispenser |
| compact_product | cigs, generic, parts, games | Compact centered product display; exact recess sizes selected per sprite |
| industrial | tool, engivend | Tall industrial body, lower panel and asymmetric control recess |
| custom | custom, greed | Wide custom product display and broad lower tray |
| coffee | coffee | Coffee panel above a centered cup recess |
| dinnerware | dinnerware | Tall open-looking shelf display and right controls |
| engi | engi | Wide industrial panel with lower horizontal item display |
| sovietsoda | sovietsoda | Separate protruding upper sign and lower dispensing cavity |
| boozeomat | boozeomat | Extra-wide cabinet: bottle window, separate keypad and delivery recess |
| hotdog | hotdog-vendor | Protruding central dispenser between deep side casings |
| wall_medical | wallmed | Shallow wall-mounted compact dispenser, not a shortened floor cabinet |
| theater | theater | Large costume display, small delivery opening |
| cytopro | cytopro | Narrow portrait display, side controls and lower delivery opening |

67 declared appearance states, not 287 independent machines. Off/broken/deny/animation frames are state variants; frame metadata is preserved, no animations are added.
