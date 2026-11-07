using System;
using AscensaoAstral.Common.Raid;
using AscensaoAstral.Content.Projectiles;
using Luminance.Assets;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AscensaoAstral.Content.Bosses.Silvanna
{
    // ═══════════════ O GELO DA BRUXA: tudo aqui é FÍSICO (cai, quica, rola, gruda no chão e estilhaça) ═══════════════
    // É o que separa a Silvanna do Astharoth: ela mexe no terreno; ele corta o espaço.

    /// <summary>Lasca de gelo: voa em arco com gravidade e se espatifa no chão.</summary>
    public class LascaDeGelo : ModProjectile
    {
        public override string Texture => "AscensaoAstral/Assets/Vazio";

        public override void SetDefaults()
        {
            Projectile.width = 14; Projectile.height = 14;
            Projectile.hostile = true; Projectile.tileCollide = false; Projectile.penetrate = 1; Projectile.timeLeft = 180;
        }

        public override void AI()
        {
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 0.32f, 14f);
            Projectile.rotation = Projectile.velocity.ToRotation();
            if (Main.rand.NextBool(4))
                Dust.NewDustPerfect(Projectile.Center, DustID.IceTorch, -Projectile.velocity * 0.1f, 0, default, 0.9f).noGravity = true;
            Lighting.AddLight(Projectile.Center, 0.25f, 0.45f, 0.7f);
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info) => target.AddBuff(BuffID.Chilled, 90);

        public override bool PreDraw(ref Color lightColor)
        {
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitY);
            Desenho.Ponta(Projectile.Center - dir * 14f, Projectile.Center + dir * 12f, 12f, new Color(160, 225, 255), Color.White, 1f, Desenho.Estilo.Gelo);
            return false;
        }

        /// <summary>Leque de lascas para cima (quique do chapéu, pingente estilhaçando...).</summary>
        public static void Leque(NPC dono, Vector2 de, int n, float abertura, float velMin, float velMax, int dano)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            for (int k = 0; k < n; k++)
            {
                float ang = -MathHelper.PiOver2 + MathHelper.Lerp(-abertura, abertura, n == 1 ? 0.5f : k / (float)(n - 1));
                Projectile.NewProjectile(dono.GetSource_FromAI(), de, ang.ToRotationVector2() * Main.rand.NextFloat(velMin, velMax), ModContent.ProjectileType<LascaDeGelo>(), dano, 0f, Main.myPlayer);
            }
        }
    }

    /// <summary>
    /// Pingente gigante: se forma lá no alto rachando (cai neve e lasquinhas — esse é o aviso), despenca,
    /// CRAVA no chão e fica ali um tempo; depois estilhaça em lascas. ai[0] = altura do chão, ai[1] = tempo pendurado.
    /// </summary>
    public class PingenteGigante : ModProjectile
    {
        public override string Texture => "AscensaoAstral/Assets/Vazio";
        private const float Comprimento = 170f;
        private const int Cravado = 55;
        private ref float Idade => ref Projectile.localAI[0];
        private ref float TempoCravado => ref Projectile.localAI[1];
        private bool Caindo => Idade >= Projectile.ai[1] && TempoCravado == 0f;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;

        public override void SetDefaults()
        {
            Projectile.width = 44; Projectile.height = 150;
            Projectile.hostile = true; Projectile.tileCollide = false; Projectile.penetrate = -1; Projectile.timeLeft = 400;
        }

        public override void AI()
        {
            Idade++;
            if (TempoCravado > 0f)
            {
                Projectile.velocity = Vector2.Zero;
                if (++TempoCravado >= Cravado)
                {
                    SoundEngine.PlaySound(SoundID.Item27 with { Pitch = -0.2f }, Projectile.Center);
                    if (Main.netMode != NetmodeID.MultiplayerClient && RaidBoss.Ativo is RaidBoss b)
                        LascaDeGelo.Leque(b.NPC, Projectile.Top + new Vector2(0f, 30f), 6, 1.2f, 5f, 8.5f, Projectile.damage / 2 + 6);
                    for (int i = 0; i < 20; i++)
                        Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(20f, 60f), DustID.Ice, Main.rand.NextVector2Circular(4f, 4f), 0, default, 1.3f);
                    Projectile.Kill();
                }
                return;
            }
            if (!Caindo)
            {
                // pendurado: treme e solta neve (o aviso é o próprio pingente rachando)
                Projectile.velocity = Vector2.Zero;
                if (Main.rand.NextBool(3))
                    Dust.NewDustPerfect(Projectile.Bottom + new Vector2(Main.rand.NextFloat(-14f, 14f), 0f), DustID.Snow, new Vector2(0f, Main.rand.NextFloat(1f, 3f)), 0, default, 1f);
                return;
            }
            if (Idade == Projectile.ai[1]) SoundEngine.PlaySound(SoundID.Item30 with { Pitch = -0.5f, Volume = 0.8f }, Projectile.Center);
            Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + 1.1f, 26f);
            if (Projectile.Bottom.Y >= Projectile.ai[0])
            {
                Projectile.Bottom = new Vector2(Projectile.Center.X, Projectile.ai[0] + 36f); // a ponta entra no chão
                Projectile.velocity = Vector2.Zero;
                TempoCravado = 1f;
                ScreenShakeSystem.StartShakeAtPoint(Projectile.Center, 5f);
                SoundEngine.PlaySound(SoundID.Item70 with { Pitch = 0.3f, Volume = 0.8f }, Projectile.Center);
                for (int i = 0; i < 14; i++)
                    Dust.NewDustPerfect(new Vector2(Projectile.Center.X, Projectile.ai[0]), DustID.Snow, new Vector2(Main.rand.NextFloat(-5f, 5f), -Main.rand.NextFloat(1f, 4f)), 0, default, 1.4f);
            }
            Lighting.AddLight(Projectile.Center, 0.35f, 0.6f, 0.9f);
        }

        public override bool CanHitPlayer(Player target) => Caindo;
        public override void OnHitPlayer(Player target, Player.HurtInfo info) => target.AddBuff(BuffID.Chilled, 180);

        public override bool PreDraw(ref Color lightColor)
        {
            Color c = new(150, 220, 255);
            Vector2 topo = Projectile.Top, ponta = Projectile.Top + new Vector2(0f, Comprimento);
            if (!Caindo && TempoCravado == 0f)
            {
                // tremendo lá em cima + um fiozinho de geada no chão onde vai cair
                float k = Idade / Math.Max(1f, Projectile.ai[1]);
                Vector2 treme = new(MathF.Sin(Idade * 1.7f) * 2f * k, 0f);
                topo += treme; ponta += treme;
                Vector2 chao = new(Projectile.Center.X, Projectile.ai[0]);
                Main.spriteBatch.Draw(MiscTexturesRegistry.BloomCircleSmall.Value, chao - Main.screenPosition, null, new Color(150, 220, 255, 0) * (0.15f + 0.45f * k), 0f,
                    MiscTexturesRegistry.BloomCircleSmall.Value.Size() / 2f, new Vector2(0.9f, 0.18f), 0, 0f);
            }
            float racha = TempoCravado > 0f ? TempoCravado / Cravado : 0f;
            Desenho.Ponta(topo, ponta, 52f, c, Color.White, 1f + racha * 0.8f, Desenho.Estilo.Gelo);
            if (racha > 0.5f)
                Desenho.Clarao(Projectile.Center, c, (racha - 0.5f) * 0.8f, Idade * 0.1f);
            return false;
        }
    }

    /// <summary>
    /// Bola de neve da Avalanche: desce rolando pelo terreno de verdade, cresce enquanto rola. Pule por cima.
    /// ai[0] = direção (±1).
    /// </summary>
    public class BolaDeNeve : ModProjectile
    {
        public override string Texture => "AscensaoAstral/Assets/Vazio";
        private ref float Idade => ref Projectile.localAI[0];
        private float Raio => MathHelper.Lerp(22f, 60f, Utils.GetLerpValue(0f, 110f, Idade, true));

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;

        public override void SetDefaults()
        {
            Projectile.width = 40; Projectile.height = 40;
            Projectile.hostile = true; Projectile.tileCollide = false; Projectile.penetrate = -1; Projectile.timeLeft = 300;
        }

        public override void AI()
        {
            Idade++;
            float dir = Projectile.ai[0];
            Projectile.velocity.X = dir * MathHelper.Lerp(6f, 10.5f, Utils.GetLerpValue(0f, 80f, Idade, true));
            float chao = RaidBoss.Ativo is RaidBoss b ? b.Chao(Projectile.Center.X + Projectile.velocity.X) : Projectile.Center.Y + Raio;
            float alvoY = chao - Raio;
            // segue o terreno: sobe a ladeira na hora, desce caindo
            Projectile.velocity.Y = alvoY < Projectile.Center.Y ? alvoY - Projectile.Center.Y : Math.Min(Projectile.velocity.Y + 0.6f, alvoY - Projectile.Center.Y);
            Projectile.rotation += Projectile.velocity.X / Raio;
            if (Main.rand.NextBool())
                Dust.NewDustPerfect(new Vector2(Projectile.Center.X - dir * Raio * 0.6f, chao - 4f), DustID.Snow, new Vector2(-dir * Main.rand.NextFloat(1f, 3f), -Main.rand.NextFloat(1f, 3f)), 0, default, 1.3f);
            if ((int)Idade % 20 == 0)
                ScreenShakeSystem.StartShakeAtPoint(Projectile.Center, 1.5f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
            => Vector2.Distance(Projectile.Center, Vector2.Clamp(Projectile.Center, targetHitbox.TopLeft(), targetHitbox.BottomRight())) < Raio * 0.9f;

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            target.AddBuff(BuffID.Chilled, 150);
            target.velocity = new Vector2(Projectile.ai[0] * 9f, -6f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Texture2D circulo = ModContent.Request<Texture2D>("AscensaoAstral/Assets/Circulo").Value;
            Texture2D bloom = MiscTexturesRegistry.BloomCircleSmall.Value;
            Vector2 p = Projectile.Center - Main.screenPosition;
            float r = Raio;
            Color luz = Lighting.GetColor(Projectile.Center.ToTileCoordinates());
            Color neve = Color.Lerp(new Color(200, 220, 240), Color.White, 0.5f).MultiplyRGB(Color.Lerp(luz, Color.White, 0.55f));
            // sombra no chão
            Main.spriteBatch.Draw(bloom, p + new Vector2(0f, r * 0.95f), null, Color.Black * 0.35f, 0f, bloom.Size() / 2f, new Vector2(r / 40f, r / 160f), 0, 0f);
            Main.spriteBatch.Draw(circulo, p, null, neve, Projectile.rotation, circulo.Size() / 2f, r * 2f / circulo.Width, 0, 0f);
            // manchas girando (dá para ver que está rolando) + luz por cima
            for (int i = 0; i < 4; i++)
            {
                Vector2 m = (Projectile.rotation + i * MathHelper.PiOver2).ToRotationVector2() * r * 0.5f;
                Main.spriteBatch.Draw(circulo, p + m, null, new Color(170, 195, 225) * 0.5f, 0f, circulo.Size() / 2f, r * 0.45f / circulo.Width, 0, 0f);
            }
            Main.spriteBatch.Draw(bloom, p + new Vector2(-r * 0.3f, -r * 0.35f), null, new Color(255, 255, 255, 0) * 0.35f, 0f, bloom.Size() / 2f, r / 60f, 0, 0f);
            return false;
        }
    }
}
