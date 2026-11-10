using System;
using System.Collections.Generic;
using AscensaoAstral.Common.Raid;
using AscensaoAstral.Content.Projectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace AscensaoAstral.Common
{
    /// <summary>
    /// CONTRA-ATAQUE (o counter do Lost Ark). Uma tecla própria (padrão: Q — dá para trocar em Controles), igual para
    /// todas as classes: um avanço curto com um golpe à frente. Só funciona com um chefe de raid na luta.
    /// Se o golpe acertar o chefe enquanto ele está AZUL (janela aberta), o golpe dele é cancelado e ele fica atordoado.
    /// </summary>
    public class ContraAtaqueSistema : ModSystem
    {
        public static ModKeybind Tecla;
        public override void Load() => Tecla = KeybindLoader.RegisterKeybind(Mod, "ContraAtaque", "Q");
        public override void Unload() => Tecla = null;
    }

    public class ContraAtaqueJogador : ModPlayer
    {
        // ─── Variáveis do counter ───
        public const int Recarga = 240;          // 4 s entre um contra-ataque e outro
        public const float Avanco = 11f;         // velocidade do avanço
        public const int DanoMinimo = 20;

        public int recarga;

        public override void PostUpdate()
        {
            if (recarga <= 0) return;
            recarga--;
            // pronto de novo: uma faísca azul para avisar (só durante a raid)
            if (recarga == 0 && Player.whoAmI == Main.myPlayer && RaidBoss.Ativo is not null)
            {
                SoundEngine.PlaySound(SoundID.MaxMana with { Volume = 0.5f }, Player.Center);
                for (int i = 0; i < 10; i++)
                    Dust.NewDustPerfect(Player.Center + Main.rand.NextVector2Circular(16f, 24f), DustID.IceTorch, -Vector2.UnitY * 2f, 0, default, 1.2f).noGravity = true;
            }
        }

        public override void ProcessTriggers(TriggersSet triggersSet)
        {
            if (ContraAtaqueSistema.Tecla?.JustPressed != true || recarga > 0 || Player.dead || Player.CCed) return;
            if (RaidBoss.Ativo is null) return; // só nas lutas de raid

            recarga = Recarga;
            int dir = Main.MouseWorld.X >= Player.Center.X ? 1 : -1;
            Player.ChangeDir(dir);
            Player.velocity.X = dir * Avanco;
            int dano = Math.Max(DanoMinimo, Player.GetWeaponDamage(Player.HeldItem));
            Projectile.NewProjectile(Player.GetSource_FromThis(), Player.Center, Vector2.Zero, ModContent.ProjectileType<GolpeDeContra>(), dano, 6f, Player.whoAmI, dir);
            SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.3f }, Player.Center);
        }
    }
}

namespace AscensaoAstral.Content.Projectiles
{
    /// <summary>O golpe do Contra-ataque: uma meia-lua azul à frente do jogador. ai[0] = direção (1 ou -1).</summary>
    public class GolpeDeContra : ModProjectile
    {
        private const int Duracao = 14;
        public override string Texture => "AscensaoAstral/Assets/Vazio";

        public override void SetDefaults()
        {
            Projectile.width = 120; Projectile.height = 100;
            Projectile.friendly = true; Projectile.hostile = false;
            Projectile.DamageType = DamageClass.Generic;
            Projectile.penetrate = -1;
            Projectile.tileCollide = false;
            Projectile.ignoreWater = true;
            Projectile.timeLeft = Duracao;
            Projectile.usesLocalNPCImmunity = true;
            Projectile.localNPCHitCooldown = -1; // acerta cada inimigo uma vez
        }

        public override void AI()
        {
            Player dono = Main.player[Projectile.owner];
            if (!dono.active || dono.dead) { Projectile.Kill(); return; }
            Projectile.Center = dono.Center + new Vector2(Projectile.ai[0] * 55f, 0f);
            Lighting.AddLight(Projectile.Center, 0.3f, 0.6f, 1f);

            // O counter vale por ENCOSTAR na área certa com a janela aberta (o chefe ou a cabeça do dragão,
            // que não toma dano). Quem decide é o servidor.
            if (Projectile.owner != Main.myPlayer || Projectile.localAI[0] != 0f) return;
            if (Common.Raid.RaidBoss.Ativo is not Common.Raid.RaidBoss chefe || !chefe.JanelaAberta || !chefe.AreaDeCounter.Intersects(Projectile.Hitbox)) return;
            Projectile.localAI[0] = 1f;
            if (Main.netMode == NetmodeID.SinglePlayer)
                chefe.ReceberCounter(Projectile.owner);
            else
            {
                ModPacket p = Mod.GetPacket();
                p.Write((byte)3);
                p.Write(chefe.NPC.whoAmI);
                p.Send();
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            float t = 1f - Projectile.timeLeft / (float)Duracao;        // 0 → 1
            float dir = Projectile.ai[0];
            Player dono = Main.player[Projectile.owner];
            // meia-lua: pontos de luz num arco que varre de cima para baixo
            for (int i = 0; i <= 10; i++)
            {
                float a = MathHelper.Lerp(-1.1f, 1.1f, i / 10f);
                if (i / 10f > t * 1.4f) break;
                Vector2 p = dono.Center + new Vector2(dir * MathF.Cos(a) * 60f, MathF.Sin(a) * 60f);
                Desenho.Brilho(p, new Color(110, 200, 255) * (1f - t), 0.55f);
            }
            return false;
        }
    }
}
