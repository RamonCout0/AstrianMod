using System;
using AscensaoAstral.Content.Projectiles;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace AscensaoAstral.Common
{
    /// <summary>
    /// A ULTIMATE DA FUSÃO (a "mioge" de cada classe). O Eco Estelar fundido carrega uma barra conforme você causa dano;
    /// cheia, aperte a tecla da ultimate (padrão: F — dá para trocar em Controles) e ela é liberada.
    ///
    ///   Guerreiro  Lâmina do Vazio     — uma meia-lua gigante que atravessa a tela
    ///   Mago       Chuva de Astra      — estrelas caem em volta do cursor
    ///   Atirador   Disparo Estelar     — um feixe que atravessa tudo na direção do cursor
    ///   Invocador  Coro dos Ecos       — ecos de luz giram em volta de você e atiram nos inimigos
    ///
    /// Pensado para crescer: a futura árvore de habilidades pode mexer em CargaPorDano, DanoBase e na própria ultimate.
    /// </summary>
    public class UltimateSistema : ModSystem
    {
        public static ModKeybind Tecla;
        public override void Load() => Tecla = KeybindLoader.RegisterKeybind(Mod, "UltimateDaFusao", "F");
        public override void Unload() => Tecla = null;
    }

    public class UltimateJogador : ModPlayer
    {
        public float Carga;                 // 0..100
        public const float CargaMax = 100f;
        public float CargaPorDano = 1f / 45f; // 45 de dano = 1% da barra (a árvore de habilidades pode mexer aqui)
        public int DanoBase = 260;
        public int TempoBrilho;

        private AstralJogador Astral => Player.GetModPlayer<AstralJogador>();
        public bool Pronta => Carga >= CargaMax;

        public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone) => Carregar(damageDone);
        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (proj.ModProjectile is not UltimateBase) Carregar(damageDone); // a própria ultimate não recarrega
        }

        private void Carregar(int dano)
        {
            if (!Astral.Fundido || Astral.Classe == ClasseAstral.Nenhuma) return;
            bool estava = Pronta;
            Carga = Math.Min(CargaMax, Carga + dano * CargaPorDano);
            if (!estava && Pronta && Player.whoAmI == Main.myPlayer)
            {
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.5f, Volume = 0.7f });
                CombatText.NewText(Player.Hitbox, new Color(255, 225, 140), "ULTIMATE PRONTA!", true);
            }
        }

        public override void ProcessTriggers(TriggersSet triggersSet)
        {
            if (UltimateSistema.Tecla?.JustPressed != true || !Pronta || Player.dead || !Astral.Fundido) return;
            Carga = 0f;
            TempoBrilho = 40;
            Soltar();
        }

        private void Soltar()
        {
            IEntitySource fonte = Player.GetSource_FromThis("Ultimate");
            Vector2 mira = Main.MouseWorld;
            Vector2 dir = (mira - Player.Center).SafeNormalize(Vector2.UnitX * Player.direction);
            Hud.Clarao(0.35f);
            ScreenShakeSystem.StartShake(10f);
            SoundEngine.PlaySound(SoundID.Item163 with { Pitch = 0.2f }, Player.Center);

            switch (Astral.Classe)
            {
                case ClasseAstral.Melee:
                {
                    int dano = (int)Player.GetTotalDamage(DamageClass.Melee).ApplyTo(DanoBase * 2);
                    Projectile.NewProjectile(fonte, Player.Center, dir * 16f, ModContent.ProjectileType<LaminaDoVazio>(), dano, 8f, Player.whoAmI);
                    Player.velocity = dir * 14f; // investida junto
                    Hud.Banner("LÂMINA DO VAZIO", new Color(255, 120, 120));
                    break;
                }
                case ClasseAstral.Mago:
                {
                    int dano = (int)Player.GetTotalDamage(DamageClass.Magic).ApplyTo(DanoBase);
                    for (int i = 0; i < 14; i++)
                    {
                        Vector2 alvo = mira + new Vector2(Main.rand.NextFloat(-260f, 260f), Main.rand.NextFloat(-60f, 60f));
                        Vector2 de = alvo + new Vector2(Main.rand.NextFloat(-200f, 200f), -900f - i * 40f);
                        Projectile.NewProjectile(fonte, de, (alvo - de).SafeNormalize(Vector2.UnitY) * 26f, ModContent.ProjectileType<EstrelaDeAstra>(), dano, 4f, Player.whoAmI, alvo.Y);
                    }
                    Hud.Banner("CHUVA DE ASTRA", new Color(150, 170, 255));
                    break;
                }
                case ClasseAstral.Atirador:
                {
                    int dano = (int)Player.GetTotalDamage(DamageClass.Ranged).ApplyTo(DanoBase / 4);
                    Projectile.NewProjectile(fonte, Player.Center, dir, ModContent.ProjectileType<DisparoEstelar>(), dano, 2f, Player.whoAmI);
                    Hud.Banner("DISPARO ESTELAR", new Color(255, 225, 140));
                    break;
                }
                case ClasseAstral.Invocador:
                {
                    int dano = (int)Player.GetTotalDamage(DamageClass.Summon).ApplyTo(DanoBase / 3);
                    for (int i = 0; i < 5; i++)
                        Projectile.NewProjectile(fonte, Player.Center, Vector2.Zero, ModContent.ProjectileType<CoroDosEcos>(), dano, 2f, Player.whoAmI, i / 5f * MathHelper.TwoPi);
                    Hud.Banner("CORO DOS ECOS", new Color(140, 255, 200));
                    break;
                }
            }
        }

        public override void PostUpdate()
        {
            if (TempoBrilho > 0) TempoBrilho--;
            if (Pronta && Main.rand.NextBool(4))
                Dust.NewDustPerfect(Player.Center + Main.rand.NextVector2Circular(18f, 26f), DustID.GoldFlame, -Vector2.UnitY, 0, default, 1f).noGravity = true;
        }
    }

    /// <summary>A barra da ultimate, embaixo da vida.</summary>
    public class UltimateHud : ModSystem
    {
        public override void PostDrawInterface(SpriteBatch sb)
        {
            Player p = Main.LocalPlayer;
            if (p.dead || Main.gameMenu || !p.GetModPlayer<AstralJogador>().Fundido) return;
            UltimateJogador u = p.GetModPlayer<UltimateJogador>();
            Texture2D px = TextureAssets.MagicPixel.Value;
            Rectangle um = new(0, 0, 1, 1);
            int w = 180, h = 10, x = Main.screenWidth - w - 40, y = 92;
            float k = u.Carga / UltimateJogador.CargaMax;
            float pulso = u.Pronta ? 0.6f + 0.4f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f) : 1f;
            sb.Draw(px, new Rectangle(x - 2, y - 2, w + 4, h + 4), um, Color.Black * 0.6f);
            sb.Draw(px, new Rectangle(x, y, (int)(w * k), h), um, Color.Lerp(new Color(120, 90, 220), new Color(255, 215, 120), k) * pulso);
            string tecla = UltimateSistema.Tecla?.GetAssignedKeys() is { Count: > 0 } teclas ? teclas[0] : "?";
            string texto = u.Pronta ? $"ULTIMATE [{tecla}]" : $"Fusão {(int)(k * 100)}%";
            Utils.DrawBorderString(sb, texto, new Vector2(x + w / 2f, y - 16f), u.Pronta ? new Color(255, 230, 150) : Color.White, 0.75f, 0.5f, 0f);
        }
    }

    // ═══════════════ PROJÉTEIS DAS ULTIMATES ═══════════════
    public abstract class UltimateBase : ModProjectile
    {
        public override string Texture => "AscensaoAstral/Assets/Vazio";
        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3000;
        }
    }

    /// <summary>Guerreiro: meia-lua de luz vermelha que atravessa tudo e cresce.</summary>
    public class LaminaDoVazio : UltimateBase
    {
        public override void SetDefaults()
        {
            Projectile.width = 160; Projectile.height = 160;
            Projectile.friendly = true; Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1; Projectile.tileCollide = false; Projectile.timeLeft = 60;
            Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = 8;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.scale = 1f + (60 - Projectile.timeLeft) / 60f * 0.8f;
            Lighting.AddLight(Projectile.Center, 1.2f, 0.4f, 0.5f);
            if (Main.rand.NextBool()) Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(60f, 60f), DustID.RedTorch, Projectile.velocity * 0.2f, 0, default, 1.8f).noGravity = true;
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) => Vector2.Distance(projHitbox.Center.ToVector2(), targetHitbox.Center.ToVector2()) < 110f * Projectile.scale;

        public override bool PreDraw(ref Color lightColor)
        {
            float a = Math.Min(1f, Projectile.timeLeft / 15f);
            for (int i = -10; i <= 10; i++)
            {
                float ang = Projectile.rotation + i * 0.13f;
                float r = 100f * Projectile.scale * (1f - Math.Abs(i) * 0.02f);
                Vector2 p = Projectile.Center + ang.ToRotationVector2() * r - Projectile.velocity * 2f;
                Desenho.Brilho(p, new Color(255, 80, 90) * a, 0.5f * Projectile.scale * (1f - Math.Abs(i) / 12f));
                Desenho.Brilho(p, Color.White * a, 0.18f * Projectile.scale * (1f - Math.Abs(i) / 12f));
            }
            return false;
        }
    }

    /// <summary>Mago: estrela que cai do céu e explode onde marcou (ai[0] = Y do alvo).</summary>
    public class EstrelaDeAstra : UltimateBase
    {
        public override void SetDefaults()
        {
            Projectile.width = 30; Projectile.height = 30;
            Projectile.friendly = true; Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1; Projectile.tileCollide = false; Projectile.timeLeft = 180;
            Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = 20;
        }

        public override void AI()
        {
            Lighting.AddLight(Projectile.Center, 0.6f, 0.7f, 1.3f);
            if (Projectile.Center.Y >= Projectile.ai[0])
            {
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.5f, Pitch = 0.4f }, Projectile.Center);
                for (int i = 0; i < 16; i++)
                    Dust.NewDustPerfect(Projectile.Center, DustID.BlueFairy, Main.rand.NextVector2Circular(7f, 7f), 0, default, 1.5f).noGravity = true;
                Projectile.Resize(140, 140);
                Projectile.timeLeft = Math.Min(Projectile.timeLeft, 3);
                Projectile.velocity = Vector2.Zero;
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Vector2[] pts = new Vector2[Projectile.oldPos.Length];
            for (int i = 0; i < pts.Length; i++) pts[i] = Projectile.oldPos[i] == Vector2.Zero ? Projectile.Center : Projectile.oldPos[i] + Projectile.Size / 2f;
            PrimitiveRenderer.RenderTrail(pts, new PrimitiveSettings(t => 26f * (1f - t), t => Color.Lerp(Color.White, new Color(110, 130, 255), t) * (1f - t)), 20);
            Desenho.Brilho(Projectile.Center, new Color(150, 170, 255), 0.8f);
            Desenho.Brilho(Projectile.Center, Color.White, 0.3f);
            return false;
        }
    }

    /// <summary>Atirador: feixe dourado que sai de você na direção do cursor (segue o cursor devagar).</summary>
    public class DisparoEstelar : UltimateBase
    {
        private const float Comprimento = 1800f;

        public override void SetDefaults()
        {
            Projectile.width = 20; Projectile.height = 20;
            Projectile.friendly = true; Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1; Projectile.tileCollide = false; Projectile.timeLeft = 90;
            Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = 6;
        }

        public override void AI()
        {
            Player dono = Main.player[Projectile.owner];
            Projectile.Center = dono.Center;
            if (Projectile.owner == Main.myPlayer)
            {
                Vector2 alvo = (Main.MouseWorld - dono.Center).SafeNormalize(Projectile.velocity);
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, alvo, 0.06f).SafeNormalize(Vector2.UnitX);
                Projectile.netUpdate = true;
            }
            ScreenShakeSystem.SetUniversalRumble(1.5f, MathHelper.TwoPi, null, 0.2f);
            Lighting.AddLight(Projectile.Center + Projectile.velocity * 200f, 1.4f, 1.2f, 0.6f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, Projectile.Center + Projectile.velocity * Comprimento, 50f, ref _);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            float a = Math.Min(1f, Projectile.timeLeft / 12f) * Math.Min(1f, (90 - Projectile.timeLeft) / 6f);
            Desenho.Faixa(Projectile.Center, Projectile.Center + Projectile.velocity * Comprimento, 60f * a, new Color(255, 200, 90), Color.White);
            Desenho.Brilho(Projectile.Center, new Color(255, 220, 140), 1.4f * a);
            return false;
        }
    }

    /// <summary>Invocador: eco de luz que gira em volta de você e atira no inimigo mais perto. ai[0] = ângulo inicial.</summary>
    public class CoroDosEcos : UltimateBase
    {
        public override void SetDefaults()
        {
            Projectile.width = 24; Projectile.height = 24;
            Projectile.friendly = false; Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1; Projectile.tileCollide = false; Projectile.timeLeft = 60 * 8;
        }

        public override void AI()
        {
            Player dono = Main.player[Projectile.owner];
            float ang = Projectile.ai[0] + Main.GlobalTimeWrappedHourly * 2.2f;
            Projectile.Center = dono.Center + ang.ToRotationVector2() * 90f;
            Lighting.AddLight(Projectile.Center, 0.4f, 1f, 0.7f);
            if (Projectile.owner != Main.myPlayer || Projectile.timeLeft % 20 != 0) return;
            NPC alvo = null; float dist = 900f;
            foreach (NPC n in Main.ActiveNPCs)
                if (n.CanBeChasedBy() && n.Distance(Projectile.Center) < dist) { dist = n.Distance(Projectile.Center); alvo = n; }
            if (alvo is not null)
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Projectile.DirectionTo(alvo.Center) * 18f, ModContent.ProjectileType<TiroDoEco>(), Projectile.damage, 1f, Projectile.owner);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Desenho.Brilho(Projectile.Center, new Color(120, 255, 190), 0.7f);
            Desenho.Brilho(Projectile.Center, Color.White, 0.22f);
            return false;
        }
    }

    public class TiroDoEco : UltimateBase
    {
        public override void SetDefaults()
        {
            Projectile.width = 14; Projectile.height = 14;
            Projectile.friendly = true; Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1; Projectile.tileCollide = false; Projectile.timeLeft = 90;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Vector2[] pts = new Vector2[Projectile.oldPos.Length];
            for (int i = 0; i < pts.Length; i++) pts[i] = Projectile.oldPos[i] == Vector2.Zero ? Projectile.Center : Projectile.oldPos[i] + Projectile.Size / 2f;
            PrimitiveRenderer.RenderTrail(pts, new PrimitiveSettings(t => 12f * (1f - t), t => new Color(120, 255, 190) * (1f - t)), 12);
            Desenho.Brilho(Projectile.Center, Color.White, 0.2f);
            return false;
        }
    }
}
