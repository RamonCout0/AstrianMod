using Luminance.Assets;
using System;
using AscensaoAstral.Content.Projectiles;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AscensaoAstral.Content.Bosses.Astharoth
{
    /// <summary>Pilar de luz do Julgamento: risco no céu → coluna de luz larga. ai[0] = topo, ai[1] = chão, ai[2] = aviso.</summary>
    public class PilarSolar : ModProjectile
    {
        public override string Texture => "AscensaoAstral/Assets/Vazio";
        private const int Ativo = 22;
        private const float Largura = 110f;
        private int Idade => (int)Projectile.ai[2] + Ativo - Projectile.timeLeft;
        private bool Ligado => Idade >= Projectile.ai[2];

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3000;
        public override void SetDefaults()
        {
            Projectile.width = 16; Projectile.height = 16;
            Projectile.hostile = true; Projectile.tileCollide = false; Projectile.penetrate = -1; Projectile.timeLeft = 2;
        }
        public override void OnSpawn(IEntitySource source) => Projectile.timeLeft = (int)Projectile.ai[2] + Ativo;

        private Rectangle Area => new((int)(Projectile.Center.X - Largura / 2f), (int)Projectile.ai[0], (int)Largura, (int)(Projectile.ai[1] - Projectile.ai[0]));

        public override void AI()
        {
            if (Idade == (int)Projectile.ai[2])
            {
                ScreenShakeSystem.StartShakeAtPoint(new Vector2(Projectile.Center.X, Projectile.ai[1]), 5f);
                for (int i = 0; i < 20; i++)
                    Dust.NewDustPerfect(new Vector2(Projectile.Center.X + Main.rand.NextFloat(-50f, 50f), Projectile.ai[1]), DustID.GoldFlame, new Vector2(Main.rand.NextFloat(-4f, 4f), -Main.rand.NextFloat(4f, 12f)), 0, default, 1.8f).noGravity = true;
            }
            if (Ligado) Lighting.AddLight(new Vector2(Projectile.Center.X, Projectile.ai[1] - 100f), 1.4f, 1.2f, 0.6f);
        }

        public override bool CanHitPlayer(Player target) => Ligado;
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) => Area.Intersects(targetHitbox);

        public override bool PreDraw(ref Color lightColor)
        {
            Color c = new(255, 210, 120);
            if (!Ligado)
            {
                Desenho.RetanguloAviso(Area, c, 0.5f, Idade / Projectile.ai[2]);
                return false;
            }
            float a = Utils.GetLerpValue(Ativo + Projectile.ai[2], Projectile.ai[2] + Ativo - 6, Idade, true) * Utils.GetLerpValue(Projectile.ai[2], Projectile.ai[2] + 3, Idade, true);
            float bate = Utils.GetLerpValue(Projectile.ai[2] + 6, Projectile.ai[2], Idade, true);
            Vector2 topo = new(Projectile.Center.X, Projectile.ai[0]), chao = new(Projectile.Center.X, Projectile.ai[1]);
            Desenho.Faixa(topo, chao, Largura * (1.1f + 0.5f * bate) * a, c, Color.White, 1f + bate, 0f);
            // onde bate no chão: anel de luz achatado + clarão
            Desenho.Brilho(chao, c, 2.5f * a);
            Desenho.Clarao(chao, c, (0.8f + bate) * a);
            Main.spriteBatch.Draw(MiscTexturesRegistry.BloomCircleSmall.Value, chao - Main.screenPosition, null, (c with { A = 0 }) * a * 0.6f, 0f,
                MiscTexturesRegistry.BloomCircleSmall.Value.Size() / 2f, new Vector2(Largura / 40f * (1f + bate), 0.35f), 0, 0f);
            return false;
        }
    }

    /// <summary>Pena de luz: voa fazendo curva (ai[0] = quanto vira por tick).</summary>
    public class PenaDeLuz : ModProjectile
    {
        public override string Texture => "AscensaoAstral/Assets/Vazio";
        public override void SetStaticDefaults() { ProjectileID.Sets.TrailCacheLength[Type] = 10; ProjectileID.Sets.TrailingMode[Type] = 0; }
        public override void SetDefaults()
        {
            Projectile.width = 16; Projectile.height = 16;
            Projectile.hostile = true; Projectile.tileCollide = false; Projectile.penetrate = 1; Projectile.timeLeft = 220;
        }
        public override void AI()
        {
            Projectile.velocity = Projectile.velocity.RotatedBy(Projectile.ai[0]);
            if (Projectile.velocity.Length() < 12f) Projectile.velocity *= 1.01f;
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, 0.8f, 0.7f, 0.3f);
        }
        public override bool PreDraw(ref Color lightColor)
        {
            Vector2[] pts = new Vector2[Projectile.oldPos.Length];
            for (int i = 0; i < pts.Length; i++) pts[i] = Projectile.oldPos[i] == Vector2.Zero ? Projectile.Center : Projectile.oldPos[i] + Projectile.Size / 2f;
            PrimitiveRenderer.RenderTrail(pts, new PrimitiveSettings(t => 14f * (1f - t), t => (Color.Lerp(Color.White, new Color(255, 200, 100), t) with { A = 0 }) * (1f - t)), 12);
            // a pena: ponta de luz para a frente
            Vector2 frente = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Desenho.Ponta(Projectile.Center - frente * 6f, Projectile.Center + frente * 26f, 12f, new Color(255, 210, 120), Color.White);
            Desenho.Brilho(Projectile.Center, new Color(255, 220, 140), 0.3f);
            return false;
        }
    }

    /// <summary>Risco de luz da Cruz: linha fina (aviso) de ai[0..1]=fim até explodir depois de ai[2] ticks.</summary>
    public class RastroDeLuz : ModProjectile
    {
        public override string Texture => "AscensaoAstral/Assets/Vazio";
        private const int Ativo = 26;
        private int Idade => (int)Projectile.ai[2] + Ativo - Projectile.timeLeft;
        private bool Ligado => Idade >= Projectile.ai[2];
        private Vector2 Fim => new(Projectile.ai[0], Projectile.ai[1]);

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 4000;
        public override void SetDefaults()
        {
            Projectile.width = 16; Projectile.height = 16;
            Projectile.hostile = true; Projectile.tileCollide = false; Projectile.penetrate = -1; Projectile.timeLeft = 2;
        }
        public override void OnSpawn(IEntitySource source) => Projectile.timeLeft = (int)Projectile.ai[2] + Ativo;

        public override void AI()
        {
            if (Idade == (int)Projectile.ai[2])
            {
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 0.6f, Pitch = 0.3f }, (Projectile.Center + Fim) / 2f);
                ScreenShakeSystem.StartShakeAtPoint(Projectile.Center, 6f);
            }
        }

        public override bool CanHitPlayer(Player target) => Ligado;
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, Fim, 60f, ref _);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Color c = new(255, 215, 130);
            if (!Ligado)
            {
                float k = Idade / Projectile.ai[2];
                Desenho.Risco(Projectile.Center, Fim, 6f + k * 10f, c * (0.35f + k * 0.5f));
                return false;
            }
            float a = Utils.GetLerpValue(Projectile.ai[2] + Ativo, Projectile.ai[2] + Ativo - 8, Idade, true);
            float bateu = Utils.GetLerpValue(Projectile.ai[2] + 6, Projectile.ai[2], Idade, true);
            Desenho.Faixa(Projectile.Center, Fim, 80f * a * (1f + 0.4f * bateu), c, Color.White, 1f + bateu, 0.8f);
            Desenho.Clarao((Projectile.Center + Fim) / 2f, c, 1.2f * a * (0.5f + bateu));
            return false;
        }
    }
}
