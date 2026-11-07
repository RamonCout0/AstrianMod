using System;
using AscensaoAstral.Common;
using AscensaoAstral.Common.Raid;
using AscensaoAstral.Content.Bosses.Silvanna;
using AscensaoAstral.Content.Projectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace AscensaoAstral.Content.Items
{
    // ═══════════════ LOOT DA SILVANNA (começo do Hardmode; com o Calamity, no nível do Cryogen) ═══════════════
    // Cada arma é um ataque dela nas suas mãos: a geada que vira espinho, o pingente que crava e estilhaça,
    // a bola de neve que rola pelo terreno.

    /// <summary>Material: gelo que não derrete. Cai da Silvanna (receitas e melhorias).</summary>
    public class GeloEterno : ModItem
    {
        public override void SetStaticDefaults() => Item.ResearchUnlockCount = 25;

        public override void SetDefaults()
        {
            Item.width = 20; Item.height = 20;
            Item.maxStack = Item.CommonMaxStack;
            Item.value = Item.sellPrice(silver: 40);
            Item.rare = ItemRarityID.Pink;
        }

        public override void PostUpdate() => Lighting.AddLight(Item.Center, 0.25f, 0.45f, 0.65f);
    }

    /// <summary>Lança de Armotheus (corpo a corpo): a estocada deixa geada sob o inimigo, e a geada vira espinho.</summary>
    public class LancaDeArmotheus : ModItem, IArmaComReferencia
    {
        public int ReferenciaVanilla => ItemID.AdamantiteGlaive;
        public string ReferenciaCalamity => "Avalanche";
        public float Fator => 0.85f; // o espinho é dano a mais

        public override void SetDefaults()
        {
            Item.width = 40; Item.height = 40;
            Item.DamageType = DamageClass.Melee;
            Item.damage = 52;
            Item.knockBack = 6f;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = Item.useAnimation = 24;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<EstocadaDeArmotheus>();
            Item.shootSpeed = 1f;
            Item.UseSound = SoundID.Item1;
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.sellPrice(gold: 5);
            Item.damage = Equilibrio.Dano(Type, Item.damage);
        }

        public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] < 1;
    }

    /// <summary>A estocada: sai do jogador e volta (desenhada com a lança de luz do cavaleiro).</summary>
    public class EstocadaDeArmotheus : ModProjectile
    {
        public override string Texture => "AscensaoAstral/Assets/Vazio";
        private const float Alcance = 150f;

        public override void SetDefaults()
        {
            Projectile.width = 26; Projectile.height = 26;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ownerHitCheck = true;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; // cada estocada acerta cada inimigo uma vez
            Projectile.timeLeft = 2;
        }

        private Player Dono => Main.player[Projectile.owner];
        private Vector2 Direcao => Projectile.velocity.SafeNormalize(Vector2.UnitX * Dono.direction);
        private float Progresso => 1f - Projectile.timeLeft / Math.Max(1f, Projectile.localAI[1]);
        private Vector2 Ponta => Dono.RotatedRelativePoint(Dono.MountedCenter) + Direcao * (30f + Alcance * MathF.Sin(Progresso * MathHelper.Pi));

        public override void AI()
        {
            Player p = Dono;
            if (Projectile.localAI[0] == 0f)
            {
                Projectile.localAI[0] = 1f;
                Projectile.localAI[1] = Projectile.timeLeft = Math.Max(2, p.itemAnimationMax);
            }
            if (!p.active || p.dead || p.noItems || p.CCed) { Projectile.Kill(); return; }
            p.heldProj = Projectile.whoAmI;
            p.ChangeDir(Direcao.X >= 0f ? 1 : -1);
            p.itemRotation = MathF.Atan2(Direcao.Y * p.direction, Direcao.X * p.direction);
            Projectile.Center = Ponta;
            Projectile.rotation = Direcao.ToRotation();
            if (Main.rand.NextBool(3))
                Dust.NewDustPerfect(Projectile.Center, DustID.IceTorch, Direcao * 2f, 0, default, 1.1f).noGravity = true;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Dono.MountedCenter, Ponta, 22f, ref _);
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            target.AddBuff(BuffID.Frostburn2, 180);
            // uma geada por estocada: no chão embaixo do inimigo, vira espinho logo depois
            if (Projectile.localAI[2] == 0f && Projectile.owner == Main.myPlayer)
            {
                Projectile.localAI[2] = 1f;
                float chao = RaidBoss.ChaoEm(target.Center.X, target.Bottom.Y - 24f, 20, target.Bottom.Y);
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), new Vector2(target.Center.X, chao), Vector2.Zero, ModContent.ProjectileType<EspinhoAmigo>(),
                    (int)(Projectile.damage * 0.6f), 4f, Projectile.owner, 16, 110f);
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Desenho.Lanca(Ponta - Direcao * 120f, Ponta + Direcao * 20f, 20f, new Color(150, 220, 255), 1.1f);
            return false;
        }
    }

    /// <summary>O espinho de gelo do chefe, do lado do jogador.</summary>
    public class EspinhoAmigo : EspinhoDeGelo
    {
        public override void SetDefaults()
        {
            base.SetDefaults();
            Projectile.hostile = false;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Melee;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override bool? CanHitNPC(NPC target) => Subiu && Projectile.timeLeft > 8 ? null : false;
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => target.AddBuff(BuffID.Frostburn2, 180);
    }

    /// <summary>Cajado do Pingente (mágico): um pingente gigante se forma sobre o cursor, despenca, crava e estilhaça.</summary>
    public class CajadoDoPingente : ModItem, IArmaComReferencia
    {
        public int ReferenciaVanilla => ItemID.FrostStaff;
        public string ReferenciaCalamity => "SnowstormStaff";
        public float Fator => 0.8f; // acerta em área + as lascas

        public override void SetStaticDefaults() => Item.staff[Type] = true;

        public override void SetDefaults()
        {
            Item.width = 40; Item.height = 40;
            Item.DamageType = DamageClass.Magic;
            Item.damage = 60;
            Item.mana = 14;
            Item.knockBack = 5f;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = Item.useAnimation = 30;
            Item.noMelee = true;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<PingenteAmigo>();
            Item.shootSpeed = 1f;
            Item.UseSound = SoundID.Item28;
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.sellPrice(gold: 5);
            Item.damage = Equilibrio.Dano(Type, Item.damage);
        }

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            Vector2 mira = Main.MouseWorld;
            float chao = RaidBoss.ChaoEm(mira.X, mira.Y - 40f, 50, mira.Y + 300f);
            Projectile.NewProjectile(source, new Vector2(mira.X, Math.Min(mira.Y, chao) - 420f), Vector2.Zero, type, damage, knockback, player.whoAmI, chao, 12);
            return false;
        }
    }

    public class PingenteAmigo : PingenteGigante
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

        public override bool? CanHitNPC(NPC target) => Caindo ? null : false;
        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => target.AddBuff(BuffID.Frostburn2, 240);

        protected override void Estilhacar()
        {
            if (Projectile.owner != Main.myPlayer) return;
            for (int k = 0; k < 5; k++)
            {
                float ang = -MathHelper.PiOver2 + MathHelper.Lerp(-1.1f, 1.1f, k / 4f);
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Top + new Vector2(0f, 30f), ang.ToRotationVector2() * Main.rand.NextFloat(5f, 8f),
                    ModContent.ProjectileType<LascaAmiga>(), (int)(Projectile.damage * 0.4f), 2f, Projectile.owner);
            }
        }
    }

    public class LascaAmiga : LascaDeGelo
    {
        public override void SetDefaults()
        {
            base.SetDefaults();
            Projectile.hostile = false;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Magic;
            Projectile.tileCollide = true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => target.AddBuff(BuffID.Frostburn2, 120);
    }

    /// <summary>Lançador de Avalanche (à distância): dispara bolas de neve que rolam pelo terreno e crescem. Usa bolas de neve.</summary>
    public class LancadorDeAvalanche : ModItem, IArmaComReferencia
    {
        public int ReferenciaVanilla => ItemID.AdamantiteRepeater;
        public string ReferenciaCalamity => "HoarfrostBow";
        public float Fator => 0.75f; // atravessa tudo no caminho

        public override void SetDefaults()
        {
            Item.width = 50; Item.height = 24;
            Item.DamageType = DamageClass.Ranged;
            Item.damage = 48;
            Item.knockBack = 7f;
            Item.useStyle = ItemUseStyleID.Shoot;
            Item.useTime = Item.useAnimation = 32;
            Item.noMelee = true;
            Item.autoReuse = true;
            Item.useAmmo = AmmoID.Snowball;
            Item.shoot = ModContent.ProjectileType<BolaAmiga>();
            Item.shootSpeed = 8f;
            Item.UseSound = SoundID.Item11;
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.sellPrice(gold: 5);
            Item.damage = Equilibrio.Dano(Type, Item.damage);
        }

        public override Vector2? HoldoutOffset() => new Vector2(-6f, 0f);

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
            => type = ModContent.ProjectileType<BolaAmiga>();

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            Projectile.NewProjectile(source, position, velocity, type, damage, knockback, player.whoAmI, velocity.X >= 0f ? 1f : -1f);
            return false;
        }
    }

    public class BolaAmiga : BolaDeNeve
    {
        public override void SetDefaults()
        {
            base.SetDefaults();
            Projectile.hostile = false;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Ranged;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 20;
            Projectile.timeLeft = 150;
        }

        protected override float ChaoAqui(float x) => RaidBoss.ChaoEm(x, Projectile.Center.Y - 100f, 25, Projectile.Center.Y + Raio + 40f);

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => target.AddBuff(BuffID.Frostburn2, 180);
    }

    /// <summary>Coração de Armotheus (Expert): imune ao frio; ao levar dano, um escudo de gelo te protege por alguns segundos.</summary>
    public class CoracaoDeArmotheus : ModItem
    {
        public override void SetDefaults()
        {
            Item.width = 28; Item.height = 28;
            Item.accessory = true;
            Item.expert = true;
            Item.rare = ItemRarityID.Expert;
            Item.value = Item.sellPrice(gold: 4);
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            player.buffImmune[BuffID.Chilled] = true;
            player.buffImmune[BuffID.Frozen] = true;
            player.buffImmune[BuffID.Frostburn] = true;
            player.buffImmune[BuffID.Frostburn2] = true;
            player.GetModPlayer<AcessoriosDosChefes>().Coracao = true;
        }
    }

    /// <summary>Escudo de gelo do Coração de Armotheus: defesa e redução de dano por pouco tempo.</summary>
    public class EscudoDeGelo : ModBuff
    {
        public override void SetStaticDefaults() => Main.buffNoSave[Type] = true;

        public override void Update(Player player, ref int buffIndex)
        {
            player.statDefense += 12;
            player.endurance += 0.15f;
            Lighting.AddLight(player.Center, 0.3f, 0.6f, 0.9f);
            if (Main.rand.NextBool(4))
                Dust.NewDustPerfect(player.Center + Main.rand.NextVector2CircularEdge(26f, 34f), DustID.IceTorch, Vector2.Zero, 0, default, 1.1f).noGravity = true;
        }
    }

    /// <summary>Bolsa do Tesouro da Silvanna (Expert): uma arma, o Coração de Armotheus e Gelo Eterno.</summary>
    public class BolsaDaSilvanna : ModItem
    {
        public override void SetStaticDefaults()
        {
            ItemID.Sets.BossBag[Type] = true;
            Item.ResearchUnlockCount = 3;
        }

        public override void SetDefaults()
        {
            Item.width = 32; Item.height = 32;
            Item.maxStack = Item.CommonMaxStack;
            Item.consumable = true;
            Item.rare = ItemRarityID.Expert;
            Item.expert = true;
        }

        public override bool CanRightClick() => true;

        public override void ModifyItemLoot(ItemLoot itemLoot)
        {
            itemLoot.Add(LootDosChefes.ArmasDaSilvanna(1));
            itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<CoracaoDeArmotheus>()));
            itemLoot.Add(ItemDropRule.Common(ModContent.ItemType<GeloEterno>(), 1, 20, 25));
            itemLoot.Add(ItemDropRule.CoinsBasedOnNPCValue(ModContent.NPCType<RaidSilvanna>()));
        }
    }

    /// <summary>Regras de drop dos dois chefes, no padrão do Cryogen/Golem: uma arma garantida por luta.</summary>
    public static class LootDosChefes
    {
        public static IItemDropRule ArmasDaSilvanna(int chance) => ItemDropRule.OneFromOptionsNotScalingWithLuck(chance,
            ModContent.ItemType<LancaDeArmotheus>(), ModContent.ItemType<CajadoDoPingente>(), ModContent.ItemType<LancadorDeAvalanche>());

        public static IItemDropRule ArmasDoAstharoth(int chance) => ItemDropRule.OneFromOptionsNotScalingWithLuck(chance,
            ModContent.ItemType<LaminaDoErrante>(), ModContent.ItemType<ArcoDoArcanjo>(), ModContent.ItemType<TomoDoJulgamento>());

        public static void Silvanna(NPCLoot npcLoot)
        {
            npcLoot.Add(ItemDropRule.BossBag(ModContent.ItemType<BolsaDaSilvanna>()));
            LeadingConditionRule classico = new(new Conditions.NotExpert());
            classico.OnSuccess(ArmasDaSilvanna(1));
            classico.OnSuccess(ItemDropRule.Common(ModContent.ItemType<GeloEterno>(), 1, 8, 15));
            npcLoot.Add(classico);
        }

        public static void Astharoth(NPCLoot npcLoot)
        {
            npcLoot.Add(ItemDropRule.BossBag(ModContent.ItemType<BolsaDoAstharoth>()));
            LeadingConditionRule classico = new(new Conditions.NotExpert());
            classico.OnSuccess(ArmasDoAstharoth(1));
            classico.OnSuccess(ItemDropRule.Common(ModContent.ItemType<FragmentoDoEter>(), 1, 10, 18));
            npcLoot.Add(classico);
        }
    }

    /// <summary>Efeitos dos acessórios de Expert dos dois chefes.</summary>
    public class AcessoriosDosChefes : ModPlayer
    {
        public bool Coracao, Eco;
        private int recargaCoracao, recargaEco;

        public override void ResetEffects()
        {
            Coracao = Eco = false;
            if (recargaCoracao > 0) recargaCoracao--;
            if (recargaEco > 0) recargaEco--;
        }

        public override void OnHurt(Player.HurtInfo info)
        {
            if (Coracao && recargaCoracao == 0)
            {
                recargaCoracao = 60 * 25;
                Player.AddBuff(ModContent.BuffType<EscudoDeGelo>(), 60 * 5);
                SoundEngine.PlaySound(SoundID.Item30 with { Pitch = 0.2f }, Player.Center);
            }
            if (Eco && recargaEco == 0)
            {
                // ECO DO ÉTER: você escorrega para o Éter por um instante (intangível) e o eco explode onde você estava
                recargaEco = 60 * 20;
                Player.immune = true;
                Player.immuneTime = Math.Max(Player.immuneTime, 90);
                Player.AddBuff(ModContent.BuffType<PassoNoEter>(), 90);
                SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.4f }, Player.Center);
                if (Player.whoAmI == Main.myPlayer)
                {
                    int dano = (int)Player.GetTotalDamage(DamageClass.Generic).ApplyTo(120);
                    foreach (NPC n in Main.ActiveNPCs)
                        if (!n.friendly && n.CanBeChasedBy() && n.Distance(Player.Center) < 260f)
                            Player.ApplyDamageToNPC(n, dano, 6f, n.Center.X > Player.Center.X ? 1 : -1);
                }
                for (int i = 0; i < 30; i++)
                    Dust.NewDustPerfect(Player.Center, DustID.PurpleTorch, Main.rand.NextVector2Circular(9f, 9f), 0, default, 1.8f).noGravity = true;
            }
        }
    }
}
