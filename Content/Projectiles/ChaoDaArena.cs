using System.Collections.Generic;
using AscensaoAstral.Common.Raid;
using AscensaoAstral.Content.Bosses.Astharoth;
using AscensaoAstral.Content.Bosses.Silvanna;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AscensaoAstral.Content.Projectiles
{
    /// <summary>
    /// O chão da arena é chão de verdade: projéteis que caem/voam até ele se espatifam ali, em vez de
    /// atravessar e ficar brilhando embaixo da terra (o que a vitrine mostrou em quase todo ataque).
    /// </summary>
    public class ChaoDaArena : GlobalProjectile
    {
        private static HashSet<int> tipos;

        public override void SetStaticDefaults()
        {
            tipos = new()
            {
                ModContent.ProjectileType<QuedaEstelar>(), ModContent.ProjectileType<Rajada>(), ModContent.ProjectileType<LaminaGuiada>(),
                ModContent.ProjectileType<PenaDeLuz>(), ModContent.ProjectileType<LancaVoadora>(), ModContent.ProjectileType<CloneDeGelo>(),
            };
        }

        public override void Unload() => tipos = null;

        public override bool AppliesToEntity(Projectile entity, bool lateInstantiation) => lateInstantiation && entity.ModProjectile?.Mod == Mod;

        public override void PostAI(Projectile projectile)
        {
            if (tipos is null || !tipos.Contains(projectile.type) || RaidBoss.Ativo is not RaidBoss chefe) return;
            if (projectile.velocity.Y <= 0f || projectile.Center.Y < chefe.ArenaPiso - 2f) return;

            Vector2 impacto = new(projectile.Center.X, chefe.ArenaPiso - 2f);
            if (!Main.dedServ)
            {
                int poeira = projectile.type == ModContent.ProjectileType<PenaDeLuz>() || projectile.type == ModContent.ProjectileType<Rajada>() ? DustID.GoldFlame
                    : projectile.type == ModContent.ProjectileType<LaminaGuiada>() ? DustID.PurpleTorch : DustID.IceTorch;
                for (int i = 0; i < 10; i++)
                {
                    Dust d = Dust.NewDustPerfect(impacto, poeira, new Vector2(Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-5f, -1f)), 0, default, 1.4f);
                    d.noGravity = Main.rand.NextBool();
                }
                if (Main.rand.NextBool(3))
                    SoundEngine.PlaySound(SoundID.Item27 with { Volume = 0.35f, Pitch = 0.3f, MaxInstances = 3 }, impacto);
            }
            projectile.Kill();
        }
    }
}
