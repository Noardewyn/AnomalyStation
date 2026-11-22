// Translated from tgstation (snapshot 1d99bd7f): code/datums/dna/dna_block.dm
// Original copyright and license: tgstation, AGPL-3.0 (code). See ../tgstation/LICENSE.
using Anomaly.Core.DmShim;
using Anomaly.Core.Kernel;
using static Anomaly.Core.DmShim.Dm;
using static Anomaly.Core.Tg.Builtins;
using static Anomaly.Core.Tg.Defines;
using static Anomaly.Core.Tg.GlobalProcs;

namespace Anomaly.Core.Tg;

// The whole file (gaps P3, D327): a DNA block, one feature's place in the unique identity or unique features hash. The var blocks
// are generated (D111).

/// A singleton for handling block-unique functions called by the DNA datum.
///
/// You don't need to add a DNA block for every feature.
/// Only add a new one if you want this feature to be changed via genetics.
// tg: code/datums/dna/dna_block.dm:9
public partial class datum_dna_block : datum
{
    /// Returns the unique block created from target. To be used for external calls.
    ///
    /// Does extra checks to make sure target is valid before calling the internal
    /// `create_unique_block`, don't override this.
    // tg: code/datums/dna/dna_block.dm:17
    [ShouldNotOverride]
    public virtual object unique_block(mob_living_carbon_human target = null)
    {
        if (!ishuman(target))
            throw CRASH("Non-human mobs shouldn't have DNA", "code/datums/dna/dna_block.dm", 20);
        return create_unique_block(target);
    }

    /// Actually creates the unique block from the inputted target.
    /// Not used outside of the type, see `unique_block` instead.
    ///
    /// Children should always override this.
    // tg: code/datums/dna/dna_block.dm:27
    [DmProtected]
    public virtual object create_unique_block(mob_living_carbon_human target = null)
    {
        return null;
    }

    /// The position of this block's string in its hash type
    // tg: code/datums/dna/dna_block.dm:32
    public virtual object position_in_hash()
    {
        return null;
    }

    /// Takes in the old hash and a string value to change this block to inside the hash.
    ///
    /// Returns a new hash with block's value updated
    // tg: code/datums/dna/dna_block.dm:38
    public virtual object modified_hash(object old_hash = null, object value = null)
    {
        object block_pos = position_in_hash();
        if (block_pos == null)
            return old_hash;
        string preceding_blocks = copytext(old_hash, 1, Dm.num(block_pos));
        string succeeding_blocks = copytext(old_hash, Dm.num(block_pos) + block_length);
        return preceding_blocks + Dm.str(value) + succeeding_blocks;
    }

    /// Gets the block string from the hash inserted
    // tg: code/datums/dna/dna_block.dm:47
    public virtual object get_block(object from_hash = null)
    {
        if (from_hash == null)
            throw CRASH("Null hash provided for getting dna block string", "code/datums/dna/dna_block.dm", 49);
        object block_pos = position_in_hash();
        return copytext(from_hash, Dm.num(block_pos), Dm.num(block_pos) + block_length);
    }

    /// Applies the DNA effects/appearance that this block's string encodes
    // tg: code/datums/dna/dna_block.dm:54
    public virtual async DmTask apply_to_mob(mob_living_carbon_human target = null, object dna_hash = null)
    {
        await DmTask.CompletedTask;
    }
}

/// Blocks for unique identities (skin tones, hair style, and gender)
// tg: code/datums/dna/dna_block.dm:58
public partial class datum_dna_block_identity : datum_dna_block
{
    // tg: code/datums/dna/dna_block.dm:61
    public override object position_in_hash()
    {
        return GLOB.total_ui_len_by_block[GetType()];
    }
}

/// Blocks for unique features (mutant color, mutant bodyparts)
// tg: code/datums/dna/dna_block.dm:65
public partial class datum_dna_block_feature : datum_dna_block
{
    // tg: code/datums/dna/dna_block.dm:70
    public override object position_in_hash()
    {
        return GLOB.total_uf_len_by_block[GetType()];
    }
}
