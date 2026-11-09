using System;
using AscensaoAstral.Common.Raid;
using AscensaoAstral.Content.Projectiles;
using Luminance.Assets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.Drawing;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ObjectData;
using Terraria.Enums;

namespace AscensaoAstral.Content.Items
{
    // ═══════════════ COLECIONÁVEIS DOS CHEFES (máscara 1/7, troféu 10%, relíquia no Master, pet 25% no Master) ═══════════════
    // Arte provisória: tudo usa o próprio Terraria (tingido) ou é desenhado em código. Troque pelos seus sprites
    // apagando a linha "Texture =>" e colocando um PNG com o nome da classe na mesma pasta.

    // ─── Máscaras (visual) ───

    /// <summary>Chapéu de Armotheus: a máscara da Silvanna (por enquanto, a aparência do Chapéu de Mago).</summary>
    public class ChapeuDeArmotheus : ModItem
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.WizardHat;

        public override void SetDefaults()
        {
            Item.CloneDefaults(ItemID.WizardHat); // copia o visual na cabeça (headSlot)
            Item.vanity = true;
            Item.defense = 0;
            Item.rare = ItemRarityID.Blue;
            Item.value = Item.sellPrice(gold: 1);
            Item.color = new Color(170, 210, 255);
        }

        public override void UpdateEquip(Player player) { } // só visual
    }

    /// <summary>Elmo do Errante: a máscara do Astharoth (por enquanto, a aparência do Grande Elmo do Escudeiro).</summary>
    public class ElmoDoErrante : ModItem
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.SquireGreatHelm;

        public override void SetDefaults()
        {
            Item.CloneDefaults(ItemID.SquireGreatHelm);
            Item.vanity = true;
            Item.defense = 0;
            Item.rare = ItemRarityID.Blue;
            Item.value = Item.sellPrice(gold: 1);
            Item.color = new Color(210, 170, 255);
        }

        public override void UpdateEquip(Player player) { }
    }

    // ─── Troféus (quadro 3×3 na parede, desenhado em código com o ícone do chefe) ───

    public class TrofeuDosChefes : ModTile
    {
        public override string Texture => "AscensaoAstral/Assets/Branco";

        public override void SetStaticDefaults()
        {
            Main.tileFrameImportant[Type] = true;
            Main.tileLavaDeath[Type] = true;
            TileID.Sets.FramesOnKillWall[Type] = true;
            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3Wall);
            TileObjectData.newTile.StyleHorizontal = true;
            TileObjectData.addTile(Type);
            AddMapEntry(new Color(120, 85, 60), Language.GetText("MapObject.Trophy"));
            DustType = DustID.WoodFurniture;
        }

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
        {
            Tile t = Main.tile[i, j];
            if (t.TileFrameX % 54 != 0 || t.TileFrameY != 0) return false; // desenha só uma vez, no canto de cima
            int estilo = t.TileFrameX / 54;
            Vector2 zero = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 p = new Vector2(i * 16, j * 16) - Main.screenPosition + zero;
            Color luz = Lighting.GetColor(i + 1, j + 1);
            Texture2D px = TextureAssets.MagicPixel.Value;
            Rectangle um = new(0, 0, 1, 1);
            Color moldura = new Color(150, 110, 60).MultiplyRGB(luz), fundo = (estilo == 0 ? new Color(40, 70, 110) : new Color(60, 35, 95)).MultiplyRGB(luz);
            spriteBatch.Draw(px, new Rectangle((int)p.X, (int)p.Y, 48, 48), um, moldura);
            spriteBatch.Draw(px, new Rectangle((int)p.X + 4, (int)p.Y + 4, 40, 40), um, fundo);
            spriteBatch.Draw(px, new Rectangle((int)p.X + 2, (int)p.Y + 2, 44, 2), um, new Color(220, 180, 100).MultiplyRGB(luz));
            Texture2D icone = ModContent.Request<Texture2D>(estilo == 0 ? "AscensaoAstral/Content/Bosses/Silvanna/RaidSilvanna_Head_Boss" : "AscensaoAstral/Content/Bosses/Astharoth/Astharoth_Head_Boss").Value;
            float esc = 30f / Math.Max(icone.Width, icone.Height);
            spriteBatch.Draw(icone, p + new Vector2(24f), null, luz, 0f, icone.Size() / 2f, esc, 0, 0f);
            return false;
        }
    }

    public abstract class ItemDeTrofeu : ModItem
    {
        protected abstract int Estilo { get; }
        protected abstract int Visual { get; }
        public override string Texture => "Terraria/Images/Item_" + Visual;

        public override void SetDefaults()
        {
            Item.DefaultToPlaceableTile(ModContent.TileType<TrofeuDosChefes>(), Estilo);
            Item.width = 32; Item.height = 32;
            Item.rare = ItemRarityID.Blue;
            Item.value = Item.buyPrice(gold: 1);
        }
    }

    public class TrofeuDaSilvanna : ItemDeTrofeu { protected override int Estilo => 0; protected override int Visual => ItemID.QueenSlimeTrophy; }
    public class TrofeuDoAstharoth : ItemDeTrofeu { protected override int Estilo => 1; protected override int Visual => ItemID.GolemTrophy; }

    // ─── Relíquias (Master; e Revengeance com o Calamity): pedestal + o chefe flutuando, dourado ───

    public class RelicariaDosChefes : ModTile
    {
        public override string Texture => "AscensaoAstral/Assets/Branco";

        public override void SetStaticDefaults()
        {
            Main.tileShine[Type] = 400;
            Main.tileFrameImportant[Type] = true;
            TileID.Sets.InteractibleByNPCs[Type] = true;
            TileObjectData.newTile.CopyFrom(TileObjectData.Style3x4);
            TileObjectData.newTile.LavaDeath = false;
            TileObjectData.newTile.DrawYOffset = 2;
            TileObjectData.newTile.Direction = TileObjectDirection.PlaceLeft;
            TileObjectData.newTile.StyleHorizontal = false;
            TileObjectData.newTile.StyleWrapLimitVisualOverride = 2;
            TileObjectData.newTile.StyleMultiplier = 2;
            TileObjectData.newTile.StyleWrapLimit = 2;
            TileObjectData.newTile.styleLineSkipVisualOverride = 0;
            TileObjectData.newAlternate.CopyFrom(TileObjectData.newTile);
            TileObjectData.newAlternate.Direction = TileObjectDirection.PlaceRight;
            TileObjectData.addAlternate(1);
            TileObjectData.addTile(Type);
            AddMapEntry(new Color(233, 207, 94), Language.GetText("MapObject.Relic"));
        }

        public override bool CreateDust(int i, int j, ref int type) => false;

        public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
        {
            Tile t = Main.tile[i, j];
            if (t.TileFrameX % 54 == 0 && t.TileFrameY % 72 == 0)
                Main.instance.TilesRenderer.AddSpecialPoint(i, j, TileDrawing.TileCounterType.CustomNonSolid);
            return false; // tudo é desenhado em SpecialDraw (o relicário flutua, precisa de redesenho a cada quadro)
        }

        public override void SpecialDraw(int i, int j, SpriteBatch spriteBatch)
        {
            Tile t = Main.tile[i, j];
            int estilo = t.TileFrameY / 72;
            bool virado = t.TileFrameX >= 54;
            Vector2 zero = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
            Vector2 canto = new Vector2(i * 16, j * 16) - Main.screenPosition + zero;
            Color luz = Lighting.GetColor(i + 1, j + 3);
            Texture2D px = TextureAssets.MagicPixel.Value;
            Rectangle um = new(0, 0, 1, 1);

            // pedestal de pedra com borda dourada
            Color pedra = new Color(95, 90, 110).MultiplyRGB(luz), ouro = new Color(230, 190, 90).MultiplyRGB(luz);
            spriteBatch.Draw(px, new Rectangle((int)canto.X + 4, (int)canto.Y + 40, 40, 24), um, pedra);
            spriteBatch.Draw(px, new Rectangle((int)canto.X, (int)canto.Y + 60, 48, 6), um, pedra);
            spriteBatch.Draw(px, new Rectangle((int)canto.X + 4, (int)canto.Y + 40, 40, 3), um, ouro);
            spriteBatch.Draw(px, new Rectangle((int)canto.X, (int)canto.Y + 60, 48, 2), um, ouro);

            // o chefe em miniatura, dourado, flutuando (como as relíquias do Master)
            float t0 = Main.GlobalTimeWrappedHourly;
            Vector2 centro = canto + new Vector2(24f, 18f + MathF.Sin(t0 * MathHelper.TwoPi / 5f) * 4f);
            Texture2D tex = ModContent.Request<Texture2D>(estilo == 0 ? "AscensaoAstral/Assets/Sheets/Bruxa" : "AscensaoAstral/Content/Bosses/Astharoth/Astharoth").Value;
            float esc = 46f / Math.Max(tex.Width, tex.Height);
            SpriteEffects flip = virado ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            Color dourado = new(255, 225, 140);
            float brilho = 0.5f + 0.5f * MathF.Sin(t0 * 2f);
            for (int k = 0; k < 4; k++) // contorno brilhante
                spriteBatch.Draw(tex, centro + (k * MathHelper.PiOver2 + t0).ToRotationVector2() * 2f, null, dourado with { A = 0 } * (0.3f + 0.2f * brilho), 0f, tex.Size() / 2f, esc, flip, 0f);
            spriteBatch.Draw(tex, centro, null, dourado.MultiplyRGB(Color.Lerp(luz, Color.White, 0.5f)), 0f, tex.Size() / 2f, esc, flip, 0f);
        }
    }

    public abstract class ItemDeRelicario : ModItem
    {
        protected abstract int Estilo { get; }
        protected abstract int Visual { get; }
        public override string Texture => "Terraria/Images/Item_" + Visual;

        public override void SetDefaults()
        {
            Item.DefaultToPlaceableTile(ModContent.TileType<RelicariaDosChefes>(), Estilo);
            Item.width = 30; Item.height = 40;
            Item.rare = ItemRarityID.Master;
            Item.master = true;
            Item.value = Item.buyPrice(gold: 5);
        }
    }

    public class RelicariaDaSilvanna : ItemDeRelicario { protected override int Estilo => 0; protected override int Visual => ItemID.QueenSlimeMasterTrophy; }
    public class RelicariaDoAstharoth : ItemDeRelicario { protected override int Estilo => 1; protected override int Visual => ItemID.GolemMasterTrophy; }

    // ─── Pets (Master, 25%) ───

    public abstract class ItemDePet : ModItem
    {
        protected abstract int Visual { get; }
        protected abstract Color Tinta { get; }
        protected abstract int Pet { get; }
        protected abstract int Buff { get; }
        public override string Texture => "Terraria/Images/Item_" + Visual;

        public override void SetDefaults()
        {
            Item.DefaultToVanitypet(Pet, Buff);
            Item.width = 28; Item.height = 28;
            Item.rare = ItemRarityID.Master;
            Item.master = true;
            Item.value = Item.sellPrice(gold: 5);
            Item.color = Tinta;
        }

        public override bool? UseItem(Player player)
        {
            if (player.whoAmI == Main.myPlayer) player.AddBuff(Item.buffType, 3600);
            return true;
        }
    }

    public class CartolaDeArmotheus : ItemDePet
    {
        protected override int Visual => ItemID.MagicHat;
        protected override Color Tinta => new(170, 215, 255);
        protected override int Pet => ModContent.ProjectileType<MiniArmotheus>();
        protected override int Buff => ModContent.BuffType<BuffMiniArmotheus>();
    }

    public class OvoDoEter : ItemDePet
    {
        protected override int Visual => ItemID.ShadowOrb;
        protected override Color Tinta => new(210, 160, 255);
        protected override int Pet => ModContent.ProjectileType<MiniDragao>();
        protected override int Buff => ModContent.BuffType<BuffMiniDragao>();
    }

    public abstract class BuffDePet : ModBuff
    {
        protected abstract int Pet { get; }
        public override void SetStaticDefaults()
        {
            Main.buffNoTimeDisplay[Type] = true;
            Main.vanityPet[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            bool _ = false;
            player.BuffHandle_SpawnPetIfNeededAndSetTime(buffIndex, ref _, Pet);
        }
    }

    public class BuffMiniArmotheus : BuffDePet
    {
        public override string Texture => "Terraria/Images/Buff_" + BuffID.Wisp;
        protected override int Pet => ModContent.ProjectileType<MiniArmotheus>();
    }

    public class BuffMiniDragao : BuffDePet
    {
        public override string Texture => "Terraria/Images/Buff_" + BuffID.ShadowOrb;
        protected override int Pet => ModContent.ProjectileType<MiniDragao>();
    }

    /// <summary>Base dos pets: segue o dono flutuando, some quando o buff acaba.</summary>
    public abstract class PetFlutuante : ModProjectile
    {
        public override string Texture => "AscensaoAstral/Assets/Vazio";
        protected abstract int Buff { get; }

        public override void SetStaticDefaults()
        {
            Main.projPet[Type] = true;
            ProjectileID.Sets.CharacterPreviewAnimations[Type] = ProjectileID.Sets.SimpleLoop(0, 1);
        }

        public override void SetDefaults()
        {
            Projectile.width = 30; Projectile.height = 30;
            Projectile.friendly = false;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.netImportant = true;
            Projectile.timeLeft = 2;
        }

        public override void AI()
        {
            Player dono = Main.player[Projectile.owner];
            if (dono.dead || !dono.active || !dono.HasBuff(Buff)) { Projectile.Kill(); return; }
            Projectile.timeLeft = 2;
            Vector2 alvo = dono.Center + new Vector2(-dono.direction * 48f, -46f + MathF.Sin(Main.GlobalTimeWrappedHourly * 2.5f) * 6f);
            float dist = Vector2.Distance(Projectile.Center, alvo);
            if (dist > 1600f) Projectile.Center = alvo;
            Projectile.velocity = (alvo - Projectile.Center) * 0.08f;
            if (Projectile.velocity.Length() > 18f) Projectile.velocity = Vector2.Normalize(Projectile.velocity) * 18f;
            Projectile.spriteDirection = dono.direction;
            Mexer(dono);
        }

        protected virtual void Mexer(Player dono) { }
    }

    /// <summary>Mini Armotheus: o chapéu em miniatura, quicando no ar atrás de você (o Ricochete, só que fofo).</summary>
    public class MiniArmotheus : PetFlutuante
    {
        protected override int Buff => ModContent.BuffType<BuffMiniArmotheus>();

        protected override void Mexer(Player dono)
        {
            Projectile.rotation = Projectile.velocity.X * 0.04f;
            Lighting.AddLight(Projectile.Center, 0.2f, 0.35f, 0.5f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = ModContent.Request<Texture2D>("AscensaoAstral/Assets/Sheets/Idle").Value;
            int q = (int)(Main.GlobalTimeWrappedHourly * 8f) % 6;
            float quique = Math.Abs(MathF.Sin(Main.GlobalTimeWrappedHourly * 4f)) * 6f;
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition - new Vector2(0f, quique), new Rectangle(q * 100, 0, 100, 100),
                Color.Lerp(lightColor, Color.White, 0.5f), Projectile.rotation, new Vector2(50f), 0.42f, Projectile.spriteDirection > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
            return false;
        }
    }

    /// <summary>Mini Dragão do Éter: o dragão do Astharoth em miniatura, ondulando atrás de você.</summary>
    public class MiniDragao : PetFlutuante
    {
        protected override int Buff => ModContent.BuffType<BuffMiniDragao>();
        private readonly Vector2[] corpo = new Vector2[5];

        protected override void Mexer(Player dono)
        {
            Projectile.rotation = Projectile.velocity.Length() > 0.5f ? Projectile.velocity.ToRotation() + MathHelper.PiOver2 : Projectile.rotation;
            DragaoMini.Seguir(corpo, Projectile.Center, 14f);
            Lighting.AddLight(Projectile.Center, 0.4f, 0.25f, 0.6f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            DragaoMini.Desenhar(corpo, Projectile.Center, Projectile.rotation, 0.5f, 1f);
            return false;
        }
    }

    /// <summary>Desenha um dragão do Éter pequeno com os sprites do dragão do Cultista (tingidos), como o chefe faz.</summary>
    public static class DragaoMini
    {
        public static void Seguir(Vector2[] corpo, Vector2 cabeca, float espaco)
        {
            Vector2 frente = cabeca;
            for (int i = 0; i < corpo.Length; i++)
            {
                if (corpo[i] == Vector2.Zero) corpo[i] = frente;
                Vector2 d = corpo[i] - frente;
                if (d.Length() > espaco) corpo[i] = frente + Vector2.Normalize(d) * espaco;
                frente = corpo[i];
            }
        }

        private static Texture2D Tex(int tipo, out Rectangle quadro)
        {
            Main.instance.LoadNPC(tipo);
            Texture2D t = TextureAssets.Npc[tipo].Value;
            quadro = new Rectangle(0, 0, t.Width, t.Height / Math.Max(1, Main.npcFrameCount[tipo]));
            return t;
        }

        public static void Desenhar(Vector2[] corpo, Vector2 cabeca, float rotCabeca, float escala, float opacidade)
        {
            Color tinta = new Color(235, 215, 255) * opacidade, brilho = new Color(170, 110, 255, 0) * 0.5f * opacidade;
            for (int i = corpo.Length - 1; i >= 0; i--)
            {
                if (corpo[i] == Vector2.Zero) continue;
                Vector2 frente = i == 0 ? cabeca : corpo[i - 1];
                float rot = (frente - corpo[i]).ToRotation() + MathHelper.PiOver2;
                Texture2D t = Tex(i == corpo.Length - 1 ? NPCID.CultistDragonTail : NPCID.CultistDragonBody2, out Rectangle q);
                Main.EntitySpriteDraw(t, corpo[i] - Main.screenPosition, q, brilho, rot, q.Size() / 2f, escala * 1.15f, 0);
                Main.EntitySpriteDraw(t, corpo[i] - Main.screenPosition, q, tinta, rot, q.Size() / 2f, escala, 0);
            }
            Texture2D cab = Tex(NPCID.CultistDragonHead, out Rectangle qc);
            Main.EntitySpriteDraw(cab, cabeca - Main.screenPosition, qc, brilho, rotCabeca, qc.Size() / 2f, escala * 1.15f, 0);
            Main.EntitySpriteDraw(cab, cabeca - Main.screenPosition, qc, tinta, rotCabeca, qc.Size() / 2f, escala, 0);
        }
    }

    /// <summary>Condição de drop: relíquia também no Revengeance do Calamity (é o padrão dele).</summary>
    public class NoRevengeanceSemMaster : IItemDropRuleCondition
    {
        public bool CanDrop(DropAttemptInfo info) => !Main.masterMode && Dificuldade.CalRevengeance;
        public bool CanShowItemDropInUI() => Common.Equilibrio.ComCalamity;
        public string GetConditionDescription() => "No Revengeance";
    }
}
