using AscensaoAstral.Common;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AscensaoAstral.Content.Tiles
{
    /// <summary>Minério Astrânia. Minerar o PRIMEIRO dispara o impacto (cratera + Eco Estelar).</summary>
    public class AstraniaTile : ModTile
    {
        public override void SetStaticDefaults()
        {
            TileID.Sets.Ore[Type] = true;
            Main.tileSolid[Type] = true;
            Main.tileBlockLight[Type] = true;
            Main.tileShine[Type] = 900;
            Main.tileShine2[Type] = true;
            Main.tileSpelunker[Type] = true;
            Main.tileOreFinderPriority[Type] = 720;
            Main.tileLighted[Type] = true;

            AddMapEntry(new Color(120, 190, 255), CreateMapEntryName());
            DustType = DustID.BlueCrystalShard;
            HitSound = SoundID.Tink;
            MineResist = 2.5f;
            MinPick = 100; // picareta de Cobalto/Paládio
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
        {
            r = 0.05f; g = 0.15f; b = 0.3f;
        }

        public override void KillTile(int i, int j, ref bool fail, ref bool effectOnly, ref bool noItem)
        {
            if (!fail && !effectOnly && AstralMundo.AstraniaLiberada && !AstralMundo.ImpactoAconteceu)
                AstralMundo.DispararImpacto(i, j);
        }
    }

    /// <summary>Chão do Éter. Indestrutível enquanto existir (o mod remove sozinho no fim da raid).</summary>
    public class PonteEterea : ModTile
    {
        public override void SetStaticDefaults()
        {
            Main.tileSolid[Type] = true;
            Main.tileBlockLight[Type] = false;
            Main.tileLighted[Type] = true;
            AddMapEntry(new Color(150, 90, 230), CreateMapEntryName());
            DustType = DustID.PurpleTorch;
            MinPick = int.MaxValue;
        }

        public override void ModifyLight(int i, int j, ref float r, ref float g, ref float b)
        {
            r = 0.35f; g = 0.2f; b = 0.6f;
        }

        public override bool CanKillTile(int i, int j, ref bool blockDamaged) => false;
        public override bool CanExplode(int i, int j) => false;
        public override bool CanReplace(int i, int j, int tileTypeBeingPlaced) => false;
    }
}
