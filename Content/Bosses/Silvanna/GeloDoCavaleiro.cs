using System;
using AscensaoAstral.Common.Raid;
using AscensaoAstral.Content.Projectiles;
using Luminance.Assets;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AscensaoAstral.Content.Bosses.Silvanna
{
    /// <summary>
    /// O FRIO: quanto mais tempo fora do abrigo na Nevasca, mais o jogador congela (0 → 1).
    /// Congelando: anda mais devagar, voa menos e toma mais dano. No máximo, fica preso no gelo por um instante.
    /// </summary>
    public class FrioJogador : ModPlayer
    {
        public float Congelamento;
        /// <summary>Ligado pela Silvanna a cada tick em que este jogador está exposto à nevasca.</summary>
        public bool Exposto;
        public bool NoGelo; // pisando num rastro de gelo (escorrega e fica lento)

        public override void ResetEffects()
        {
            if (Exposto) Congelamento = Math.Min(1f, Congelamento + 1f / 540f); // 9 s até congelar de vez
            else Congelamento = Math.Max(0f, Congelamento - 1f / 150f);
            Exposto = false;
        }

        public override void PostUpdateRunSpeeds()
        {
            float lento = 1f - Congelamento * 0.55f;
            Player.maxRunSpeed *= lento;
            Player.accRunSpeed *= lento;
            Player.runAcceleration *= lento;
            if (NoGelo)
            {
                Player.maxRunSpeed *= 0.7f;
                Player.runSlowdown *= 0.15f; // escorrega
            }
            NoGelo = false;
        }

        public override void PostUpdateEquips()
        {
            Player.endurance -= Congelamento * 0.35f;     // toma até 35% a mais de dano
            Player.wingTimeMax = (int)(Player.wingTimeMax * (1f - Congelamento * 0.6f));
        }

        public override void PostUpdate()
        {
            if (Main.netMode == NetmodeID.Server || Player.whoAmI != Main.myPlayer) return;
            if (Congelamento >= 1f && !Player.HasBuff(BuffID.Frozen))
            {
                Player.AddBuff(BuffID.Frozen, 50);
                CombatText.NewText(Player.Hitbox, new Color(170, 230, 255), "CONGELADO!", true);
                Congelamento = 0.75f;
            }
            if (Congelamento > 0.2f && Main.rand.NextFloat() < Congelamento * 0.5f)
                Dust.NewDustPerfect(Player.Center + Main.rand.NextVector2Circular(14f, 22f), DustID.IceTorch, -Vector2.UnitY * 0.6f, 0, default, 0.9f).noGravity = true;
        }
    }

    // ═══════════════ ATAQUES DO CAVALEIRO DE GELO ═══════════════

    /// <summary>Espinho de gelo que brota do chão. ai[0] = aviso (ticks), ai[1] = altura. Nasce numa onda em direção ao jogador.</summary>
    public class EspinhoDeGelo : ModProjectile
    {
        public override string Texture => "AscensaoAstral/Assets/Vazio";
        private const int Ativo = 40;
        private int Idade => (int)Projectile.ai[0] + Ativo - Projectile.timeLeft;
        private bool Subiu => Idade >= Projectile.ai[0];

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 1200;

        public override void SetDefaults()
        {
            Projectile.width = 34; Projectile.height = 34;
            Projectile.hostile = true; Projectile.tileCollide = false; Projectile.penetrate = -1; Projectile.timeLeft = 2;
        }

        public override void OnSpawn(IEntitySource source) => Projectile.timeLeft = (int)Projectile.ai[0] + Ativo;

        private float AlturaAgora => Projectile.ai[1] * (Subiu ? Utils.GetLerpValue(0, 6, Idade - Projectile.ai[0], true) * Utils.GetLerpValue(0, 10, Projectile.timeLeft, true) : 0f);

        public override void AI()
        {
            if (Idade == (int)Projectile.ai[0])
            {
                SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.45f, Pitch = 0.2f }, Projectile.Center);
                for (int i = 0; i < 6; i++)
                    Dust.NewDustPerfect(Projectile.Center, DustID.IceTorch, new Vector2(Main.rand.NextFloat(-3f, 3f), -Main.rand.NextFloat(3f, 8f)), 0, default, 1.4f).noGravity = true;
            }
        }

        public override bool CanHitPlayer(Player target) => Subiu && Projectile.timeLeft > 8;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            float h = AlturaAgora;
            return new Rectangle((int)Projectile.Center.X - 14, (int)(Projectile.Center.Y - h), 28, (int)h).Intersects(targetHitbox);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            target.AddBuff(BuffID.Chilled, 180);
            target.velocity.Y = -9f;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Color c = new(150, 220, 255);
            if (!Subiu)
            {
                float k = Idade / Projectile.ai[0];
                Desenho.Brilho(Projectile.Center, c, 0.25f + k * 0.4f);
                Desenho.RetanguloAviso(new Rectangle((int)Projectile.Center.X - 14, (int)(Projectile.Center.Y - Projectile.ai[1]), 28, (int)Projectile.ai[1]), c, k * 0.6f, k);
                return false;
            }
            float h = AlturaAgora;
            // espinho: faixa que afina no topo + brilho na ponta
            // espinho de cristal: ponta principal + duas lascas menores do lado
            Vector2 baseE = Projectile.Center;
            Desenho.Ponta(baseE, baseE - new Vector2(0f, h), 30f, c, Color.White, 1f, Desenho.Estilo.Gelo);
            Desenho.Ponta(baseE + new Vector2(-10f, 0f), baseE + new Vector2(-22f, -h * 0.45f), 14f, c, Color.White, 1f, Desenho.Estilo.Gelo);
            Desenho.Ponta(baseE + new Vector2(10f, 0f), baseE + new Vector2(24f, -h * 0.35f), 12f, c, Color.White, 1f, Desenho.Estilo.Gelo);
            Desenho.Brilho(baseE, c, 0.5f);
            return false;
        }
    }

    /// <summary>
    /// Anel de gelo que se expande a partir do cavaleiro, com UMA brecha. Passe pela brecha.
    /// ai[0] = ângulo da brecha, ai[1] = velocidade de giro da brecha, ai[2] = raio máximo.
    /// </summary>
    public class AnelDeGelo : ModProjectile
    {
        public override string Texture => "AscensaoAstral/Assets/Vazio";
        private const float Brecha = 0.55f, Espessura = 26f;
        private int Idade => 160 - Projectile.timeLeft;
        private float Raio => MathHelper.Lerp(40f, Projectile.ai[2], Utils.GetLerpValue(20, 160, Idade, true));
        private float AnguloBrecha => Projectile.ai[0] + Projectile.ai[1] * Idade;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 4000;

        public override void SetDefaults()
        {
            Projectile.width = 16; Projectile.height = 16;
            Projectile.hostile = true; Projectile.tileCollide = false; Projectile.penetrate = -1; Projectile.timeLeft = 160;
        }

        public override bool CanHitPlayer(Player target) => Idade > 20;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Vector2 d = targetHitbox.Center.ToVector2() - Projectile.Center;
            float dist = d.Length();
            if (Math.Abs(dist - Raio) > Espessura + 12f) return false;
            float diferenca = MathHelper.WrapAngle(d.ToRotation() - AnguloBrecha);
            return Math.Abs(diferenca) > Brecha;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) => target.AddBuff(BuffID.Chilled, 150);

        public override bool PreDraw(ref Color lightColor)
        {
            Color c = new(150, 220, 255);
            float alfa = Idade < 20 ? Idade / 20f * 0.5f : 1f;
            int pontos = 90;
            for (int i = 0; i < pontos; i++)
            {
                float ang = i / (float)pontos * MathHelper.TwoPi;
                if (Math.Abs(MathHelper.WrapAngle(ang - AnguloBrecha)) < Brecha) continue;
                Vector2 p = Projectile.Center + ang.ToRotationVector2() * Raio;
                Desenho.Brilho(p, c * alfa, 0.22f);
                if (i % 3 == 0) Desenho.Brilho(p, Color.White * alfa, 0.07f);
            }
            // a brecha brilha dourado para ser fácil de achar
            Desenho.Brilho(Projectile.Center + AnguloBrecha.ToRotationVector2() * Raio, new Color(255, 220, 140) * alfa, 0.6f);
            return false;
        }
    }

    /// <summary>
    /// Prisão de gelo: um círculo marca onde você está; depois de um tempo um cristal fecha ali.
    /// Quem ainda estiver dentro fica congelado. ai[0] = aviso.
    /// </summary>
    public class PrisaoDeGelo : ModProjectile
    {
        public override string Texture => "AscensaoAstral/Assets/Vazio";
        private const float Raio = 70f;
        private int Idade => (int)Projectile.ai[0] + 30 - Projectile.timeLeft;
        private bool Fechou => Idade >= Projectile.ai[0];

        public override void SetDefaults()
        {
            Projectile.width = 16; Projectile.height = 16;
            Projectile.hostile = false; Projectile.tileCollide = false; Projectile.penetrate = -1; Projectile.timeLeft = 2;
        }

        public override void OnSpawn(IEntitySource source) => Projectile.timeLeft = (int)Projectile.ai[0] + 30;

        public override void AI()
        {
            if (Idade != (int)Projectile.ai[0]) return;
            SoundEngine.PlaySound(SoundID.Item30 with { Pitch = -0.5f }, Projectile.Center);
            ScreenShakeSystem.StartShakeAtPoint(Projectile.Center, 5f);
            for (int i = 0; i < 24; i++)
                Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(Raio, Raio), DustID.IceTorch, Main.rand.NextVector2Circular(4f, 4f), 0, default, 1.6f).noGravity = true;
            if (Main.netMode != NetmodeID.Server)
            {
                Player eu = Main.LocalPlayer;
                if (!eu.dead && eu.Distance(Projectile.Center) < Raio)
                {
                    eu.AddBuff(BuffID.Frozen, 90);
                    eu.Hurt(PlayerDeathReason.ByProjectile(eu.whoAmI, Projectile.whoAmI), 40, 0);
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Color c = new(150, 220, 255);
            if (!Fechou)
            {
                float k = Idade / Projectile.ai[0];
                for (int i = 0; i < 40; i++)
                {
                    float ang = i / 40f * MathHelper.TwoPi + k * 2f;
                    Desenho.Brilho(Projectile.Center + ang.ToRotationVector2() * Raio * (1.3f - k * 0.3f), c * (0.3f + k * 0.7f), 0.1f);
                }
                Desenho.Brilho(Projectile.Center, c * k * 0.5f, 1.2f);
                return false;
            }
            // o cristal: losango de luz que some
            float s = Projectile.timeLeft / 30f;
            for (int i = 0; i < 6; i++)
            {
                float ang = i / 6f * MathHelper.TwoPi + MathHelper.PiOver2;
                Desenho.Ponta(Projectile.Center, Projectile.Center + ang.ToRotationVector2() * Raio * (1.1f + 0.15f * (i % 2)), 26f * s, c, Color.White, 1f, Desenho.Estilo.Gelo);
            }
            Desenho.Brilho(Projectile.Center, Color.White * s, 1.2f);
            return false;
        }
    }

    /// <summary>
    /// Sopro glacial: um cone de vento gelado que sai do cavaleiro e varre de um lado para o outro.
    /// ai[0] = ângulo inicial, ai[1] = quanto gira no total, ai[2] = NPC dono. Fique ATRÁS dele.
    /// </summary>
    public class SoproGlacial : ModProjectile
    {
        public override string Texture => "AscensaoAstral/Assets/Vazio";
        public const int Aviso = 50, Duracao = 110;
        private const float Alcance = 1000f, Abertura = 0.36f;
        private int Idade => Aviso + Duracao - Projectile.timeLeft;
        private bool Soprando => Idade >= Aviso;
        private float Angulo => Projectile.ai[0] + Projectile.ai[1] * Utils.GetLerpValue(Aviso, Aviso + Duracao, Idade, true);

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3000;

        public override void SetDefaults()
        {
            Projectile.width = 16; Projectile.height = 16;
            Projectile.hostile = true; Projectile.tileCollide = false; Projectile.penetrate = -1; Projectile.timeLeft = Aviso + Duracao;
        }

        public override void AI()
        {
            NPC dono = Main.npc[(int)Projectile.ai[2]];
            if (dono.active) Projectile.Center = dono.Center;
            if (Idade == Aviso) SoundEngine.PlaySound(SoundID.DD2_BetsyFlameBreath with { Pitch = 0.4f }, Projectile.Center);
            if (Soprando && Main.netMode != NetmodeID.Server)
                for (int i = 0; i < 4; i++)
                {
                    float a = Angulo + Main.rand.NextFloat(-Abertura, Abertura);
                    Dust.NewDustPerfect(Projectile.Center, DustID.IceTorch, a.ToRotationVector2() * Main.rand.NextFloat(10f, 22f), 0, default, 1.8f).noGravity = true;
                }
        }

        public override bool CanHitPlayer(Player target) => Soprando;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Vector2 d = targetHitbox.Center.ToVector2() - Projectile.Center;
            return d.Length() < Alcance && Math.Abs(MathHelper.WrapAngle(d.ToRotation() - Angulo)) < Abertura;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            target.AddBuff(BuffID.Chilled, 240);
            target.GetModPlayer<FrioJogador>().Congelamento = Math.Min(1f, target.GetModPlayer<FrioJogador>().Congelamento + 0.25f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D bloom = MiscTexturesRegistry.BloomCircleSmall.Value;
            Color c = new Color(150, 220, 255) with { A = 0 };
            float alfa = Soprando ? 1f : Idade / (float)Aviso * 0.4f;
            float pulso = 0.8f + 0.2f * MathF.Sin(Idade * 0.6f);
            // o cone: vários "sopros" de bloom esticados ao longo do raio
            for (int i = 1; i <= 14; i++)
            {
                float k = i / 14f;
                Vector2 p = Projectile.Center + Angulo.ToRotationVector2() * Alcance * k - Main.screenPosition;
                float largura = Alcance * k * MathF.Tan(Abertura) * 2f / bloom.Width;
                Main.spriteBatch.Draw(bloom, p, null, c * alfa * 0.35f * pulso * (1.1f - k * 0.5f), Angulo, bloom.Size() / 2f, new Vector2(0.6f, largura), 0, 0f);
            }
            if (!Soprando) // linhas das bordas do cone (o aviso)
                for (int s = -1; s <= 1; s += 2)
                {
                    Vector2 fim = Projectile.Center + (Angulo + s * Abertura).ToRotationVector2() * Alcance;
                    Desenho.Faixa(Projectile.Center, fim, 3f, new Color(150, 220, 255) * 0.6f, Color.White);
                }
            return false;
        }
    }

    /// <summary>Rastro de gelo no chão (deixado pelo Deslize Glacial): quem pisa escorrega e fica lento.</summary>
    public class RastroDeGelo : ModProjectile
    {
        public override string Texture => "AscensaoAstral/Assets/Vazio";

        public override void SetDefaults()
        {
            Projectile.width = 70; Projectile.height = 18;
            Projectile.hostile = false; Projectile.tileCollide = false; Projectile.penetrate = -1; Projectile.timeLeft = 300;
        }

        public override void AI()
        {
            if (Main.netMode == NetmodeID.Server) return;
            Player eu = Main.LocalPlayer;
            if (!eu.dead && eu.Hitbox.Intersects(new Rectangle((int)Projectile.position.X, (int)Projectile.position.Y - 10, Projectile.width, Projectile.height + 20)))
            {
                eu.GetModPlayer<FrioJogador>().NoGelo = true;
                eu.AddBuff(BuffID.Chilled, 30);
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            float a = Math.Min(1f, Projectile.timeLeft / 40f);
            Desenho.Golpe(Desenho.Estilo.Gelo, new Vector2(Projectile.position.X, Projectile.Center.Y), new Vector2(Projectile.position.X + Projectile.width, Projectile.Center.Y), 16f, new Color(150, 220, 255), 1f, a);
            return false;
        }
    }
}
