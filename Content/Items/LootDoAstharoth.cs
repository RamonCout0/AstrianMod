using System;
using AscensaoAstral.Common;
using AscensaoAstral.Common.Raid;
using AscensaoAstral.Content.Bosses.Astharoth;
using AscensaoAstral.Content.Projectiles;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace AscensaoAstral.Content.Items
{
    // ═══════════════ LOOT DO ASTHAROTH (pós-Golem; com o Calamity, no nível do Plaguebringer Goliath) ═══════════════
    // As armas dele cortam o espaço: a fenda que solta uma meia-lua, a flecha que vira asa, o pilar do Julgamento.

    /// <summary>Material: um caco do Éter. Cai do Astharoth.</summary>
    public class FragmentoDoEter : ModItem
    {
        // Arte provisória: o próprio sprite do Terraria (FragmentNebula), tingido — como o Calamity antigo fazia
        public override string Texture => Arte.Item(this, ItemID.FragmentNebula);

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 25;
            ItemID.Sets.ItemNoGravity[Type] = true;
        }

        public override void SetDefaults()
        {
            Item.color = Arte.Tinta(this, new Color(200, 150, 255));
            Item.width = 20; Item.height = 20;
            Item.maxStack = Item.CommonMaxStack;
            Item.value = Item.sellPrice(gold: 1);
            Item.rare = ItemRarityID.Yellow;
        }

        public override void PostUpdate() => Lighting.AddLight(Item.Center, 0.5f, 0.3f, 0.8f);
    }

    /// <summary>Lâmina do Errante (corpo a corpo): cada golpe abre uma fenda à frente, e dela sai uma meia-lua de éter.</summary>
    public class LaminaDoErrante : ModItem, IArmaComReferencia
    {
        // Arte provisória: o próprio sprite do Terraria (NightsEdge), tingido — como o Calamity antigo fazia
        public override string Texture => Arte.Item(this, ItemID.NightsEdge);

        public int ReferenciaVanilla => ItemID.PossessedHatchet;
        public string ReferenciaCalamity => "Virulence";
        public float Fator => 0.85f; // golpe + meia-lua

        public override void SetDefaults()
        {
            Item.color = Arte.Tinta(this, new Color(215, 170, 255));
            Item.width = 52; Item.height = 52;
            Item.DamageType = DamageClass.Melee;
            Item.damage = 95;
            Item.knockBack = 6f;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = Item.useAnimation = 22;
            Item.autoReuse = true;
            Item.useTurn = false;
            Item.shoot = ModContent.ProjectileType<FendaDoErrante>();
            Item.shootSpeed = 16f;
            Item.UseSound = SoundID.Item71;
            Item.rare = ItemRarityID.Yellow;
            Item.value = Item.sellPrice(gold: 10);
            Item.damage = Equilibrio.Dano(Type, Item.damage);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            Vector2 dir = velocity.SafeNormalize(Vector2.UnitX * player.direction);
            Projectile.NewProjectile(source, player.Center + dir * 90f, velocity, type, (int)(damage * 0.7f), knockback, player.whoAmI);
            return false;
        }

        public override void MeleeEffects(Player player, Rectangle hitbox)
        {
            if (Main.rand.NextBool(2))
                Dust.NewDustPerfect(hitbox.Center.ToVector2() + Main.rand.NextVector2Circular(hitbox.Width / 2f, hitbox.Height / 2f), DustID.PurpleTorch, Vector2.Zero, 0, default, 1.3f).noGravity = true;
        }
    }

    /// <summary>A fenda que a Lâmina abre: fica um instante aberta e solta a meia-lua na direção do golpe.</summary>
    public class FendaDoErrante : ModProjectile
    {
        public override string Texture => "AscensaoAstral/Assets/Vazio";
        private const int Abre = 12;

        public override void SetDefaults()
        {
            Projectile.width = 20; Projectile.height = 20;
            Projectile.friendly = false; // a fenda não machuca: a meia-lua, sim
            Projectile.DamageType = DamageClass.Melee;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Abre;
        }

        public override void AI()
        {
            Projectile.Center -= Projectile.velocity; // parada no lugar (a velocidade é a da meia-lua)
            Projectile.rotation = Projectile.velocity.ToRotation();
        }

        public override void OnKill(int timeLeft)
        {
            SoundEngine.PlaySound(SoundID.Item60 with { Pitch = 0.3f, Volume = 0.7f }, Projectile.Center);
            if (Projectile.owner == Main.myPlayer)
            {
                int i = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Projectile.velocity, ModContent.ProjectileType<CorteAmigo>(), Projectile.damage, Projectile.knockBack, Projectile.owner);
                Main.projectile[i].DamageType = Projectile.DamageType; // a Adaga (Rogue) também abre fendas
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            float k = 1f - Projectile.timeLeft / (float)Abre;
            Vector2 lado = Projectile.velocity.SafeNormalize(Vector2.UnitX).RotatedBy(MathHelper.PiOver2);
            Desenho.Golpe(Desenho.Estilo.Eter, Projectile.Center - lado * 70f * (0.4f + 0.6f * k), Projectile.Center + lado * 70f * (0.4f + 0.6f * k), 4f + 16f * k, new Color(190, 120, 255), 1f + k * 0.5f, 0.6f + 0.4f * k);
            return false;
        }
    }

    public class CorteAmigo : CorteDoPasso
    {
        public override void SetDefaults()
        {
            base.SetDefaults();
            Projectile.hostile = false;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
            Projectile.timeLeft = 36;
            Projectile.width = 50; Projectile.height = 150;
        }
    }

    /// <summary>Arco do Arcanjo (à distância): toda flecha vira uma flecha de luz que se abre num leque de penas.</summary>
    public class ArcoDoArcanjo : ModItem, IArmaComReferencia
    {
        // Arte provisória: o próprio sprite do Terraria (DaedalusStormbow), tingido — como o Calamity antigo fazia
        public override string Texture => Arte.Item(this, ItemID.DaedalusStormbow);

        public int ReferenciaVanilla => ItemID.PulseBow; // (o Stynger tem dano baixo por tiro: o dano dele está na explosão)
        public string ReferenciaCalamity => "Malevolence";
        public float Fator => 0.7f; // flecha + 3 penas

        public override void SetDefaults()
        {
            Item.color = Arte.Tinta(this, new Color(255, 230, 170));
            Item.width = 24; Item.height = 54;
            Item.DamageType = DamageClass.Ranged;
            Item.damage = 60;
            Item.knockBack = 3f;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = Item.useAnimation = 22;
            Item.noMelee = true;
            Item.autoReuse = true;
            Item.useAmmo = AmmoID.Arrow;
            Item.shoot = ProjectileID.WoodenArrowFriendly;
            Item.shootSpeed = 14f;
            Item.UseSound = SoundID.Item5;
            Item.rare = ItemRarityID.Yellow;
            Item.value = Item.sellPrice(gold: 10);
            Item.damage = Equilibrio.Dano(Type, Item.damage);
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
            => type = ModContent.ProjectileType<FlechaDeLuz>();
    }

    /// <summary>Flecha de luz: voa reta e, depois de um tempo, se abre em três penas que fazem curva.</summary>
    public class FlechaDeLuz : ModProjectile
    {
        public override string Texture => "AscensaoAstral/Assets/Vazio";
        private const int Abre = 18;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 0;
        }

        public override void SetDefaults()
        {
            Projectile.width = 12; Projectile.height = 12;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.arrow = true;
            Projectile.penetrate = 1;
            Projectile.timeLeft = Abre;
            Projectile.extraUpdates = 1;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, 1f, 0.85f, 0.4f);
        }

        public override void OnKill(int timeLeft)
        {
            if (Projectile.owner != Main.myPlayer) return;
            for (int k = -1; k <= 1; k++)
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Projectile.velocity.RotatedBy(k * 0.28f) * 0.8f,
                    ModContent.ProjectileType<PenaAmiga>(), (int)(Projectile.damage * 0.4f), 2f, Projectile.owner, -k * 0.012f);
            for (int i = 0; i < 10; i++)
                Dust.NewDustPerfect(Projectile.Center, DustID.GoldFlame, Main.rand.NextVector2Circular(4f, 4f), 0, default, 1.3f).noGravity = true;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Vector2[] pts = new Vector2[Projectile.oldPos.Length];
            for (int i = 0; i < pts.Length; i++) pts[i] = Projectile.oldPos[i] == Vector2.Zero ? Projectile.Center : Projectile.oldPos[i] + Projectile.Size / 2f;
            PrimitiveRenderer.RenderTrail(pts, new PrimitiveSettings(t => 12f * (1f - t), t => (Color.Lerp(Color.White, new Color(255, 200, 100), t) with { A = 0 }) * (1f - t)), 12);
            Vector2 frente = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Desenho.Ponta(Projectile.Center - frente * 10f, Projectile.Center + frente * 22f, 12f, new Color(255, 215, 130), Color.White, 1f, Desenho.Estilo.Solar);
            return false;
        }
    }

    public class PenaAmiga : PenaDeLuz
    {
        public override void SetDefaults()
        {
            base.SetDefaults();
            Projectile.hostile = false;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.tileCollide = true;
            Projectile.timeLeft = 90;
        }
    }

    /// <summary>Tomo do Julgamento (mágico): um pilar de luz cai do céu em cima do cursor.</summary>
    public class TomoDoJulgamento : ModItem, IArmaComReferencia
    {
        // Arte provisória: o próprio sprite do Terraria (SpellTome), tingido — como o Calamity antigo fazia
        public override string Texture => Arte.Item(this, ItemID.SpellTome);

        public int ReferenciaVanilla => ItemID.HeatRay;
        public string ReferenciaCalamity => "PlagueStaff";
        public float Fator => 0.8f; // acerta todo mundo na coluna

        public override void SetDefaults()
        {
            Item.color = Arte.Tinta(this, new Color(255, 225, 150));
            Item.width = 28; Item.height = 30;
            Item.DamageType = DamageClass.Magic;
            Item.damage = 110;
            Item.mana = 18;
            Item.knockBack = 4f;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.useTime = Item.useAnimation = 34;
            Item.noMelee = true;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<PilarAmigo>();
            Item.shootSpeed = 1f;
            Item.UseSound = SoundID.Item122 with { Pitch = 0.4f, Volume = 0.6f };
            Item.rare = ItemRarityID.Yellow;
            Item.value = Item.sellPrice(gold: 10);
            Item.damage = Equilibrio.Dano(Type, Item.damage);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            Vector2 mira = Main.MouseWorld;
            float chao = RaidBoss.ChaoEm(mira.X, mira.Y - 60f, 50, mira.Y + 300f);
            Projectile.NewProjectile(source, new Vector2(mira.X, chao - 800f), Vector2.Zero, type, damage, knockback, player.whoAmI, chao - 800f, chao, 16);
            return false;
        }
    }

    public class PilarAmigo : PilarSolar
    {
        public override void SetDefaults()
        {
            base.SetDefaults();
            Projectile.hostile = false;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool? CanHitNPC(NPC target) => Ligado ? null : false;
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => target.AddBuff(BuffID.Daybreak, 120);
    }

    /// <summary>Eco do Éter (Expert): ao levar dano, você escorrega para o Éter por um instante e o eco explode.</summary>
    public class EcoDoEter : ModItem
    {
        // Arte provisória: o próprio sprite do Terraria (CelestialStone), tingido — como o Calamity antigo fazia
        public override string Texture => Arte.Item(this, ItemID.CelestialStone);

        public override void SetDefaults()
        {
            Item.color = Arte.Tinta(this, new Color(190, 140, 255));
            Item.width = 28; Item.height = 28;
            Item.accessory = true;
            Item.expert = true;
            Item.rare = ItemRarityID.Expert;
            Item.value = Item.sellPrice(gold: 8);
        }

        public override void UpdateAccessory(Player player, bool hideVisual) => player.GetModPlayer<AcessoriosDosChefes>().Eco = true;
    }

    /// <summary>O instante no Éter do Eco do Éter: intangível e mais rápido.</summary>
    public class PassoNoEter : ModBuff
    {
        public override string Texture => Arte.Buff(this, BuffID.ShadowDodge);

        public override void SetStaticDefaults() => Main.buffNoSave[Type] = true;

        public override void Update(Player player, ref int buffIndex)
        {
            player.moveSpeed += 0.3f;
            player.immune = true;
            player.immuneNoBlink = true;
            if (Main.rand.NextBool(2))
                Dust.NewDustPerfect(player.Center + Main.rand.NextVector2Circular(14f, 22f), DustID.PurpleTorch, -player.velocity * 0.2f, 0, default, 1.2f).noGravity = true;
        }
    }

    /// <summary>Bolsa do Tesouro do Astharoth (Expert).</summary>
    public class BolsaDoAstharoth : ModItem
    {
        // Arte provisória: o próprio sprite do Terraria (GolemBossBag), tingido — como o Calamity antigo fazia
        public override string Texture => Arte.Item(this, ItemID.GolemBossBag);

        public override void SetStaticDefaults()
        {
            ItemID.Sets.BossBag[Type] = true;
            Item.ResearchUnlockCount = 3;
        }

        public override void SetDefaults()
        {
            Item.color = Arte.Tinta(this, new Color(200, 150, 255));
            Item.width = 32; Item.height = 32;
            Item.maxStack = Item.CommonMaxStack;
            Item.consumable = true;
            Item.rare = ItemRarityID.Expert;
            Item.expert = true;
        }

        public override bool CanRightClick() => true;

        public override void ModifyItemLoot(ItemLoot itemLoot)
        {
            itemLoot.Add(LootDosChefes.ArmasDoAstharoth(1));
            itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<EcoDoEter>()));
            itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<FragmentoDoEter>(), 1, 22, 28));
            itemLoot.Add(ItemDropRule.CoinsBasedOnNPCValue(ModContent.NPCType<Astharoth>()));
        }
    }
}

namespace AscensaoAstral.Content.Items
{
    /// <summary>
    /// Dentro da dimensão da raid, depois que o chefe cai, o loot vem até você: a raid fecha poucos segundos
    /// depois da vitória e ninguém deve perder a bolsa por estar longe dela.
    /// </summary>
    public class LootVemAteVoce : ModPlayer
    {
        public override void PostUpdate()
        {
            if (Player.whoAmI != Main.myPlayer || !Raids.DentroDeUmaRaid || RaidBoss.Ativo is not null) return;
            foreach (Item it in Main.ActiveItems)
            {
                float d = Vector2.Distance(it.Center, Player.Center);
                if (d > 4000f || d < 8f) continue;
                it.velocity = Vector2.Zero;
                it.Center = Vector2.Lerp(it.Center, Player.Center, Math.Min(1f, 22f / d + 0.08f));
            }
        }
    }
}
