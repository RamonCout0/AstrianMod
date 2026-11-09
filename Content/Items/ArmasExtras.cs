using System;
using AscensaoAstral.Common;
using AscensaoAstral.Content.Bosses.Silvanna;
using AscensaoAstral.Content.Projectiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AscensaoAstral.Content.Items
{
    // ═══════════════ INVOCADOR (as duas lutas) + ROGUE (só com o Calamity) ═══════════════

    /// <summary>Base dos cajados de lacaio: põe o buff e cria o lacaio no cursor.</summary>
    public abstract class CajadoDeLacaio : ModItem
    {
        protected abstract int Lacaio { get; }
        protected abstract int BuffDoLacaio { get; }

        public override void SetStaticDefaults()
        {
            ItemID.Sets.GamepadWholeScreenUseRange[Type] = true;
            ItemID.Sets.LockOnIgnoresCollision[Type] = true;
        }

        protected void Base(int dano, int raridade, int valorOuro)
        {
            Item.width = 40; Item.height = 40;
            Item.DamageType = DamageClass.Summon;
            Item.damage = dano;
            Item.mana = 10;
            Item.knockBack = 3f;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = Item.useAnimation = 36;
            Item.noMelee = true;
            Item.buffType = BuffDoLacaio;
            Item.shoot = Lacaio;
            Item.UseSound = SoundID.Item44;
            Item.rare = raridade;
            Item.value = Item.sellPrice(gold: valorOuro);
            Item.damage = Equilibrio.Dano(Type, Item.damage);
        }

        public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
            => position = Main.MouseWorld;

        public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
        {
            player.AddBuff(Item.buffType, 2);
            Projectile p = Projectile.NewProjectileDirect(source, position, Vector2.Zero, type, damage, knockback, player.whoAmI);
            p.originalDamage = Item.damage;
            return false;
        }
    }

    public abstract class BuffDeLacaio : ModBuff
    {
        protected abstract int Lacaio { get; }

        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;
            Main.buffNoTimeDisplay[Type] = true;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            if (player.ownedProjectileCounts[Lacaio] > 0) player.buffTime[buffIndex] = 18000;
            else { player.DelBuff(buffIndex); buffIndex--; }
        }
    }

    /// <summary>Base dos lacaios: confere o buff, acha o alvo (respeitando a mira do jogador).</summary>
    public abstract class Lacaio : ModProjectile
    {
        public override string Texture => "AscensaoAstral/Assets/Vazio";
        protected abstract int BuffDoLacaio { get; }

        public override void SetStaticDefaults()
        {
            Main.projPet[Type] = true;
            ProjectileID.Sets.MinionTargettingFeature[Type] = true;
            ProjectileID.Sets.MinionSacrificable[Type] = true;
            ProjectileID.Sets.CultistIsResistantTo[Type] = true;
        }

        public override void SetDefaults()
        {
            Projectile.width = 36; Projectile.height = 36;
            Projectile.tileCollide = false;
            Projectile.friendly = true;
            Projectile.minion = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.minionSlots = 1f;
            Projectile.penetrate = -1;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = 15;
        }

        protected Player Dono => Main.player[Projectile.owner];

        protected bool Vivo()
        {
            if (Dono.dead || !Dono.active) { Dono.ClearBuff(BuffDoLacaio); return false; }
            if (Dono.HasBuff(BuffDoLacaio)) Projectile.timeLeft = 2;
            return true;
        }

        protected NPC AcharAlvo(float alcance)
        {
            if (Dono.HasMinionAttackTargetNPC)
            {
                NPC n = Main.npc[Dono.MinionAttackTargetNPC];
                if (n.CanBeChasedBy() && n.Distance(Projectile.Center) < alcance * 1.5f) return n;
            }
            NPC melhor = null;
            float d = alcance;
            foreach (NPC n in Main.ActiveNPCs)
                if (n.CanBeChasedBy() && n.Distance(Projectile.Center) < d) { d = n.Distance(Projectile.Center); melhor = n; }
            return melhor;
        }

        protected Vector2 PosicaoDeDescanso(float altura) => Dono.Center + new Vector2(-Dono.direction * (44f + Projectile.minionPos * 36f), -altura);
    }

    // ─── Silvanna: Cajado dos Cavaleiros do Céu ───

    public class CajadoDosCavaleiros : CajadoDeLacaio, IArmaComReferencia
    {
        public int ReferenciaVanilla => ItemID.SpiderStaff;
        public string ReferenciaCalamity => "";
        public float Fator => 1.05f; // a lança atravessa 3 inimigos, mas tem recarga
        public bool SemTempo => true;
        public override string Texture => "Terraria/Images/Item_" + ItemID.FrostStaff;
        protected override int Lacaio => ModContent.ProjectileType<CavaleiroDoCeu>();
        protected override int BuffDoLacaio => ModContent.BuffType<BuffCavaleiroDoCeu>();

        public override void SetDefaults()
        {
            Base(30, ItemRarityID.Pink, 5);
            Item.color = new Color(170, 220, 255);
        }
    }

    public class BuffCavaleiroDoCeu : BuffDeLacaio
    {
        public override string Texture => "Terraria/Images/Buff_" + BuffID.FlinxMinion;
        protected override int Lacaio => ModContent.ProjectileType<CavaleiroDoCeu>();
    }

    /// <summary>Um cavaleiro de gelo flutua atrás de você e arremessa lanças nos inimigos (os cavaleiros do céu da luta).</summary>
    public class CavaleiroDoCeu : Lacaio
    {
        protected override int BuffDoLacaio => ModContent.BuffType<BuffCavaleiroDoCeu>();
        private ref float Recarga => ref Projectile.ai[0];
        private Vector2 mira;

        public override void SetDefaults()
        {
            base.SetDefaults();
            Projectile.friendly = false; // ele não encosta: arremessa
        }

        public override bool? CanDamage() => false;

        public override void AI()
        {
            if (!Vivo()) return;
            Vector2 casa = PosicaoDeDescanso(70f) + new Vector2(0f, MathF.Sin(Main.GlobalTimeWrappedHourly * 2f + Projectile.minionPos) * 6f);
            if (Projectile.Distance(casa) > 2000f) Projectile.Center = casa;
            Projectile.velocity = (casa - Projectile.Center) * 0.1f;
            NPC alvo = AcharAlvo(900f);
            mira = alvo?.Center ?? Projectile.Center + new Vector2(Dono.direction * 100f, 0f);
            Projectile.spriteDirection = mira.X > Projectile.Center.X ? 1 : -1;
            if (Recarga > 0f) Recarga--;
            if (alvo is not null && Recarga <= 0f)
            {
                Recarga = 50f;
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = 0.2f, Volume = 0.6f }, Projectile.Center);
                if (Projectile.owner == Main.myPlayer)
                {
                    Vector2 v = (alvo.Center + alvo.velocity * 10f - Projectile.Center).SafeNormalize(Vector2.UnitX) * 18f;
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, v, ModContent.ProjectileType<LancaAmiga>(), Projectile.damage, Projectile.knockBack, Projectile.owner);
                }
            }
            Lighting.AddLight(Projectile.Center, 0.25f, 0.45f, 0.65f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D tex = ModContent.Request<Texture2D>("AscensaoAstral/Assets/Sheets/VerdadeiraForma").Value;
            Desenho.Brilho(Projectile.Center, new Color(120, 200, 255), 0.6f);
            Main.EntitySpriteDraw(tex, Projectile.Center - Main.screenPosition, null, new Color(190, 230, 255) * 0.9f, 0f, tex.Size() / 2f, 0.42f,
                Projectile.spriteDirection > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None);
            if (Recarga > 20f || Recarga == 0f) // a lança na mão, apontada
            {
                Vector2 dir = (mira - Projectile.Center).SafeNormalize(Vector2.UnitX);
                Desenho.Lanca(Projectile.Center - dir * 16f, Projectile.Center + dir * 38f, 10f, new Color(150, 220, 255));
            }
            return false;
        }
    }

    public class LancaAmiga : LancaVoadora
    {
        public override void SetDefaults()
        {
            base.SetDefaults();
            Projectile.hostile = false;
            Projectile.friendly = true;
            Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 3;
            Projectile.tileCollide = true;
            Projectile.timeLeft = 90;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => target.AddBuff(BuffID.Frostburn2, 120);
    }

    // ─── Astharoth: Cajado do Dragão do Éter ───

    public class CajadoDoDragao : CajadoDeLacaio, IArmaComReferencia
    {
        public int ReferenciaVanilla => ItemID.XenoStaff;
        public string ReferenciaCalamity => "InfectedRemote";
        public float Fator => 1f;
        public bool SemTempo => true;
        public override string Texture => "Terraria/Images/Item_" + ItemID.StardustDragonStaff;
        protected override int Lacaio => ModContent.ProjectileType<DragaoLacaio>();
        protected override int BuffDoLacaio => ModContent.BuffType<BuffDragaoLacaio>();

        public override void SetDefaults()
        {
            Base(60, ItemRarityID.Yellow, 10);
            Item.color = new Color(215, 170, 255);
        }
    }

    public class BuffDragaoLacaio : BuffDeLacaio
    {
        public override string Texture => "Terraria/Images/Buff_" + BuffID.StardustDragonMinion;
        protected override int Lacaio => ModContent.ProjectileType<DragaoLacaio>();
    }

    /// <summary>Um dragãozinho do Éter que dá botes nos inimigos e volta ondulando.</summary>
    public class DragaoLacaio : Lacaio
    {
        protected override int BuffDoLacaio => ModContent.BuffType<BuffDragaoLacaio>();
        private readonly Vector2[] corpo = new Vector2[6];

        public override void AI()
        {
            if (!Vivo()) return;
            NPC alvo = AcharAlvo(1000f);
            if (alvo is not null)
            {
                Vector2 dir = (alvo.Center - Projectile.Center).SafeNormalize(Vector2.UnitY);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, dir * 17f, 0.07f);
            }
            else
            {
                Vector2 casa = PosicaoDeDescanso(60f) + (Main.GlobalTimeWrappedHourly * 2f + Projectile.minionPos).ToRotationVector2() * 30f;
                if (Projectile.Distance(casa) > 2000f) Projectile.Center = casa;
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, (casa - Projectile.Center) * 0.08f, 0.15f);
            }
            if (Projectile.velocity.Length() > 0.5f) Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            DragaoMini.Seguir(corpo, Projectile.Center, 15f);
            Lighting.AddLight(Projectile.Center, 0.45f, 0.25f, 0.7f);
            if (Main.rand.NextBool(4))
                Dust.NewDustPerfect(Projectile.Center, DustID.PurpleTorch, -Projectile.velocity * 0.1f, 0, default, 1f).noGravity = true;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) => projHitbox.Intersects(targetHitbox);

        public override bool PreDraw(ref Color lightColor)
        {
            DragaoMini.Desenhar(corpo, Projectile.Center, Projectile.rotation, 0.6f, 1f);
            return false;
        }
    }

    // ─── Rogue (a classe do Calamity): só existe se o Calamity estiver instalado ───

    public static class Rogue
    {
        private static DamageClass classe;
        public static DamageClass Classe
        {
            get
            {
                if (classe is null && !(ModLoader.HasMod("CalamityMod") && ModContent.TryFind("CalamityMod", "RogueDamageClass", out classe)))
                    classe = DamageClass.Throwing;
                return classe;
            }
        }
    }

    public abstract class ArmaRogue : ModItem
    {
        public override bool IsLoadingEnabled(Mod mod) => ModLoader.HasMod("CalamityMod");
    }

    /// <summary>Pingente de Arremesso (Rogue): crava onde bater e estilhaça em lascas.</summary>
    public class PingenteDeArremesso : ArmaRogue, IArmaComReferencia
    {
        public int ReferenciaVanilla => ItemID.ShadowFlameKnife;
        public string ReferenciaCalamity => "Icebreaker";
        public float Fator => 0.8f;
        public override string Texture => "Terraria/Images/Item_" + ItemID.FrostDaggerfish;

        public override void SetDefaults()
        {
            Item.width = 30; Item.height = 30;
            Item.DamageType = Rogue.Classe;
            Item.damage = 45;
            Item.knockBack = 4f;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = Item.useAnimation = 22;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<PingenteArremessado>();
            Item.shootSpeed = 13f;
            Item.UseSound = SoundID.Item1;
            Item.rare = ItemRarityID.Pink;
            Item.value = Item.sellPrice(gold: 5);
            Item.color = new Color(190, 230, 255);
            Item.damage = Equilibrio.Dano(Type, Item.damage);
        }
    }

    public class PingenteArremessado : ModProjectile
    {
        public override string Texture => "AscensaoAstral/Assets/Vazio";
        private ref float Cravado => ref Projectile.ai[0];

        public override bool IsLoadingEnabled(Mod mod) => ModLoader.HasMod("CalamityMod");

        public override void SetDefaults()
        {
            Projectile.width = 16; Projectile.height = 16;
            Projectile.friendly = true;
            Projectile.DamageType = Rogue.Classe;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
        }

        public override void AI()
        {
            if (Cravado > 0f)
            {
                Projectile.velocity = Vector2.Zero;
                if (++Cravado > 30f) Projectile.Kill();
                return;
            }
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.25f, 16f);
            Projectile.rotation = Projectile.velocity.ToRotation();
        }

        public override bool OnTileCollide(Vector2 oldVelocity)
        {
            Cravado = 1f;
            Projectile.tileCollide = false;
            Projectile.friendly = false;
            SoundEngine.PlaySound(SoundID.Item50 with { Pitch = 0.4f, Volume = 0.6f }, Projectile.Center);
            return false;
        }

        public override void OnKill(int timeLeft)
        {
            SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.6f }, Projectile.Center);
            if (Projectile.owner != Main.myPlayer) return;
            for (int k = 0; k < 4; k++)
            {
                float ang = -MathHelper.PiOver2 + MathHelper.Lerp(-1f, 1f, k / 3f);
                int i = Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center - new Vector2(0f, 6f), ang.ToRotationVector2() * Main.rand.NextFloat(4f, 7f),
                    ModContent.ProjectileType<LascaAmiga>(), (int)(Projectile.damage * 0.35f), 1f, Projectile.owner);
                Main.projectile[i].DamageType = Rogue.Classe;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Vector2 dir = Projectile.rotation.ToRotationVector2();
            Desenho.Ponta(Projectile.Center - dir * 16f, Projectile.Center + dir * 16f, 14f, new Color(160, 225, 255), Color.White, 1f + Cravado / 30f, Desenho.Estilo.Gelo);
            return false;
        }
    }

    /// <summary>Adaga do Errante (Rogue): onde ela acerta, abre uma fenda que solta uma meia-lua de éter.</summary>
    public class AdagaDoErrante : ArmaRogue, IArmaComReferencia
    {
        public int ReferenciaVanilla => ItemID.ShadowFlameKnife;
        public string ReferenciaCalamity => "FuelCellBundle";
        public float Fator => 0.85f;
        public override string Texture => "Terraria/Images/Item_" + ItemID.MagicDagger;

        public override void SetDefaults()
        {
            Item.width = 24; Item.height = 24;
            Item.DamageType = Rogue.Classe;
            Item.damage = 80;
            Item.knockBack = 3f;
            Item.useStyle = ItemUseStyleID.Swing;
            Item.useTime = Item.useAnimation = 16;
            Item.noUseGraphic = true;
            Item.noMelee = true;
            Item.autoReuse = true;
            Item.shoot = ModContent.ProjectileType<AdagaArremessada>();
            Item.shootSpeed = 17f;
            Item.UseSound = SoundID.Item1;
            Item.rare = ItemRarityID.Yellow;
            Item.value = Item.sellPrice(gold: 10);
            Item.color = new Color(215, 170, 255);
            Item.damage = Equilibrio.Dano(Type, Item.damage);
        }
    }

    public class AdagaArremessada : ModProjectile
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.MagicDagger;

        public override bool IsLoadingEnabled(Mod mod) => ModLoader.HasMod("CalamityMod");

        public override void SetDefaults()
        {
            Projectile.width = 14; Projectile.height = 14;
            Projectile.friendly = true;
            Projectile.DamageType = Rogue.Classe;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 120;
        }

        public override void AI()
        {
            if (Projectile.timeLeft < 100) Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.3f, 16f);
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver4;
            if (Main.rand.NextBool(3))
                Dust.NewDustPerfect(Projectile.Center, DustID.PurpleTorch, Vector2.Zero, 0, default, 1f).noGravity = true;
        }

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (Projectile.owner != Main.myPlayer) return;
            int i = Projectile.NewProjectile(Projectile.GetSource_FromThis(), target.Center - Projectile.velocity.SafeNormalize(Vector2.UnitX) * 60f,
                Projectile.velocity.SafeNormalize(Vector2.UnitX) * 16f, ModContent.ProjectileType<FendaDoErrante>(), (int)(Projectile.damage * 0.6f), 3f, Projectile.owner);
            Main.projectile[i].DamageType = Rogue.Classe;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D t = ModContent.Request<Texture2D>(Texture).Value;
            Main.EntitySpriteDraw(t, Projectile.Center - Main.screenPosition, null, new Color(215, 170, 255), Projectile.rotation, t.Size() / 2f, 1f, 0);
            return false;
        }
    }
}
