using System;
using AscensaoAstral.Common.Raid;
using Luminance.Assets;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AscensaoAstral.Content.Projectiles
{
    /// <summary>Cores/estilos dos perigos.</summary>
    public enum TipoPerigo { Corte = 0, Gelo = 1, Solar = 2, Atordoar = 3, Eter = 4 }

    internal static class Desenho
    {
        public static Color Cor(TipoPerigo t) => t switch
        {
            TipoPerigo.Gelo => new Color(120, 210, 255),
            TipoPerigo.Solar => new Color(255, 210, 90),
            TipoPerigo.Atordoar => new Color(190, 110, 255),
            TipoPerigo.Eter => new Color(150, 90, 255),
            _ => new Color(255, 60, 50)
        };

        /// <summary>Faixa reta com o shader do laser (ou o padrão, se ainda não compilou).</summary>
        public static void Faixa(Vector2 a, Vector2 b, float largura, Color cor, Color nucleo)
        {
            ManagedShader shader = null;
            if (ShaderManager.TryGetShader("AscensaoAstral.LaserEscarlateShader", out ManagedShader s))
            {
                shader = s;
                shader.TrySetParameter("corBorda", cor.ToVector3());
                shader.TrySetParameter("corNucleo", nucleo.ToVector3());
                shader.SetTexture(MiscTexturesRegistry.WavyBlotchNoise.Value, 1, SamplerState.LinearWrap);
            }

            Vector2[] pontos = new Vector2[8];
            for (int i = 0; i < pontos.Length; i++)
                pontos[i] = Vector2.Lerp(a, b, i / (float)(pontos.Length - 1));
            PrimitiveRenderer.RenderTrail(pontos, new PrimitiveSettings(_ => largura, _ => Color.White, Smoothen: false, Shader: shader), 8);
        }

        /// <summary>
        /// Retângulo de aviso em coordenadas do mundo. Com <paramref name="progresso"/> (0 → 1 até o golpe) usa o
        /// AvisoShader: borda acesa, listras correndo e um preenchimento que avança. Sem ele (ou sem shader), o simples.
        /// </summary>
        public static void RetanguloAviso(Rectangle r, Color cor, float pulso, float progresso)
        {
            if (!ShaderManager.TryGetShader("AscensaoAstral.AvisoShader", out ManagedShader shader))
            {
                RetanguloAviso(r, cor, pulso);
                return;
            }
            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            shader.TrySetParameter("progresso", MathHelper.Clamp(progresso, 0f, 1f));
            shader.TrySetParameter("cor", cor.ToVector3());
            shader.TrySetParameter("tamanho", new Vector2(r.Width, r.Height));
            shader.Apply();
            sb.Draw(ModContent.Request<Texture2D>("AscensaoAstral/Assets/Branco").Value,
                new Rectangle(r.X - (int)Main.screenPosition.X, r.Y - (int)Main.screenPosition.Y, r.Width, r.Height), Color.White);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>Retângulo de aviso simples (translúcido, piscando) em coordenadas do mundo.</summary>
        public static void RetanguloAviso(Rectangle r, Color cor, float pulso)
        {
            Texture2D px = TextureAssets.MagicPixel.Value;
            Rectangle tela = new(r.X - (int)Main.screenPosition.X, r.Y - (int)Main.screenPosition.Y, r.Width, r.Height);
            Rectangle um = new(0, 0, 1, 1);
            Main.spriteBatch.Draw(px, tela, um, cor * (0.12f + 0.12f * pulso));
            int b = 2;
            Main.spriteBatch.Draw(px, new Rectangle(tela.X, tela.Y, tela.Width, b), um, cor * 0.8f);
            Main.spriteBatch.Draw(px, new Rectangle(tela.X, tela.Bottom - b, tela.Width, b), um, cor * 0.8f);
            Main.spriteBatch.Draw(px, new Rectangle(tela.X, tela.Y, b, tela.Height), um, cor * 0.8f);
            Main.spriteBatch.Draw(px, new Rectangle(tela.Right - b, tela.Y, b, tela.Height), um, cor * 0.8f);
        }

        public static void Brilho(Vector2 mundo, Color cor, float escala)
        {
            Texture2D bloom = MiscTexturesRegistry.BloomCircleSmall.Value;
            Main.spriteBatch.Draw(bloom, mundo - Main.screenPosition, null, cor with { A = 0 }, 0f, bloom.Size() / 2f, escala, 0, 0f);
        }
    }

    /// <summary>
    /// O "_spawn_hazard" universal: um retângulo que primeiro AVISA (translúcido) e depois ATIVA (sólido, machuca).
    ///   ai[0] = largura, ai[1] = altura, ai[2] = aviso (ticks) * 10 + tipo
    ///   velocidade opcional (o retângulo anda)
    /// Use RetanguloPerigo.Criar(...) em vez de chamar NewProjectile na mão.
    /// </summary>
    public class RetanguloPerigo : ModProjectile
    {
        public override string Texture => "AscensaoAstral/Assets/Vazio";
        private const int Ativo = 24;

        private int Aviso => (int)Projectile.ai[2] / 10;
        private TipoPerigo Tipo => (TipoPerigo)((int)Projectile.ai[2] % 10);
        private int Tempo => Aviso + Ativo - Projectile.timeLeft;
        private bool Ligado => Tempo >= Aviso;

        public static void Criar(IEntitySourceLike fonte, Vector2 centro, Vector2 tamanho, int aviso, TipoPerigo tipo, int dano, Vector2 velocidade = default)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;
            Projectile.NewProjectile(fonte.Fonte, centro, velocidade, ModContent.ProjectileType<RetanguloPerigo>(), dano, 0f, Main.myPlayer,
                tamanho.X, tamanho.Y, aviso * 10 + (int)tipo);
        }

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3000;

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 2;
        }

        public override void OnSpawn(Terraria.DataStructures.IEntitySource source) => Projectile.timeLeft = Aviso + Ativo;

        private Rectangle Area => new((int)(Projectile.Center.X - Projectile.ai[0] / 2f), (int)(Projectile.Center.Y - Projectile.ai[1] / 2f), (int)Projectile.ai[0], (int)Projectile.ai[1]);

        public override void AI()
        {
            if (Tempo == Aviso)
            {
                SoundEngine.PlaySound((Tipo == TipoPerigo.Solar ? SoundID.Item122 : SoundID.Item71) with { Volume = 0.55f }, Projectile.Center);
                ScreenShakeSystem.StartShakeAtPoint(Projectile.Center, 3f);
            }
            if (Ligado)
            {
                Lighting.AddLight(Projectile.Center, Desenho.Cor(Tipo).ToVector3() * 0.8f);
                Rectangle a = Area;
                for (int i = 0; i < 3; i++)
                {
                    Vector2 p = new(Main.rand.NextFloat(a.Left, a.Right), Main.rand.NextFloat(a.Top, a.Bottom));
                    int pó = Tipo switch { TipoPerigo.Gelo => DustID.IceTorch, TipoPerigo.Solar => DustID.SolarFlare, TipoPerigo.Corte => DustID.RedTorch, _ => DustID.PurpleTorch };
                    Dust.NewDustPerfect(p, pó, Main.rand.NextVector2Circular(3f, 3f), 0, default, 1.3f).noGravity = true;
                }
            }
        }

        public override bool CanHitPlayer(Player target) => Ligado;
        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) => Area.Intersects(targetHitbox);

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            if (Tipo == TipoPerigo.Atordoar)
                target.AddBuff(BuffID.Frozen, 60);
            if (Tipo == TipoPerigo.Gelo)
                target.AddBuff(BuffID.Chilled, 180);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Rectangle r = Area;
            Color cor = Desenho.Cor(Tipo);
            if (!Ligado)
            {
                Desenho.RetanguloAviso(r, cor, 0.5f + 0.5f * MathF.Sin(Tempo * 0.5f), Tempo / (float)Math.Max(1, Aviso));
                return false;
            }

            // A faixa segue o lado mais comprido do retângulo
            bool horizontal = r.Width >= r.Height;
            Vector2 a = horizontal ? new Vector2(r.Left, r.Center.Y) : new Vector2(r.Center.X, r.Top);
            Vector2 b = horizontal ? new Vector2(r.Right, r.Center.Y) : new Vector2(r.Center.X, r.Bottom);
            float largura = (horizontal ? r.Height : r.Width) * 1.4f * Utils.GetLerpValue(Aviso + Ativo, Aviso + Ativo - 6, Tempo, true);
            Desenho.Faixa(a, b, largura, cor, Color.White);
            return false;
        }
    }

    /// <summary>Atalho para passar a "fonte" do NPC sem repetir NPC.GetSource_FromAI() em todo lugar.</summary>
    public readonly struct IEntitySourceLike
    {
        public readonly Terraria.DataStructures.IEntitySource Fonte;
        public IEntitySourceLike(Terraria.DataStructures.IEntitySource f) => Fonte = f;
        public static implicit operator IEntitySourceLike(NPC npc) => new(npc.GetSource_FromAI());
    }

    /// <summary>Rajada (bola de energia) com rastro. ai[0]: 0 gelo, 1 éter, 2 fogo. ai[1] > 0: persegue o jogador mais perto no começo.</summary>
    public class Rajada : ModProjectile
    {
        public override string Texture => "AscensaoAstral/Assets/Vazio";

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 10;
            ProjectileID.Sets.TrailingMode[Type] = 0;
        }

        public override void SetDefaults()
        {
            Projectile.width = 18;
            Projectile.height = 18;
            Projectile.hostile = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 300;
        }

        private Color Cor => Projectile.ai[0] switch { 1 => new Color(160, 100, 255), 2 => new Color(255, 120, 40), _ => new Color(120, 210, 255) };

        public override void AI()
        {
            int idade = 300 - Projectile.timeLeft;
            if (Projectile.ai[1] > 0f && idade < 60)
            {
                Player alvo = Aereo.MaisPerto(Projectile.Center);
                if (alvo is not null)
                {
                    float vel = Projectile.velocity.Length();
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, Projectile.DirectionTo(alvo.Center) * vel, 0.04f * Projectile.ai[1]);
                }
            }
            if (Projectile.velocity.Length() < 14f)
                Projectile.velocity *= 1.015f;
            Lighting.AddLight(Projectile.Center, Cor.ToVector3() * 0.6f);
            if (Main.rand.NextBool(3))
                Dust.NewDustPerfect(Projectile.Center, Projectile.ai[0] switch { 1 => DustID.PurpleTorch, 2 => DustID.Torch, _ => DustID.IceTorch }, -Projectile.velocity * 0.15f, 0, default, 1.1f).noGravity = true;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            if (Projectile.ai[0] == 0)
                target.AddBuff(BuffID.Chilled, 120);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Vector2[] pontos = new Vector2[Projectile.oldPos.Length];
            for (int i = 0; i < pontos.Length; i++)
                pontos[i] = Projectile.oldPos[i] == Vector2.Zero ? Projectile.Center : Projectile.oldPos[i] + Projectile.Size / 2f;
            Color c = Cor;
            PrimitiveRenderer.RenderTrail(pontos, new PrimitiveSettings(t => 16f * (1f - t), t => c * (1f - t) * 0.8f), 16);
            Desenho.Brilho(Projectile.Center, c, 0.45f);
            Desenho.Brilho(Projectile.Center, Color.White, 0.18f);
            return false;
        }
    }

    /// <summary>A alma de quem caiu. Um aliado parado em cima dela revive o dono (veja RaidJogador).</summary>
    public class AlmaDoJogador : ModProjectile
    {
        public override string Texture => "AscensaoAstral/Assets/Vazio";

        public override void SetDefaults()
        {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60;
        }

        private Player Dono => Main.player[(int)Projectile.ai[0]];

        public override void AI()
        {
            Player dono = Dono;
            RaidJogador rj = dono.GetModPlayer<RaidJogador>();
            bool viva = dono.active && dono.dead && RaidBoss.Ativo is not null;
            if (dono.whoAmI == Main.myPlayer)
                viva &= rj.TemAlma;

            if (!viva)
            {
                Projectile.Kill();
                return;
            }

            Projectile.timeLeft = 60;
            Projectile.velocity = Vector2.Zero;

            // Progresso visual (calculado por cada cliente com as mesmas regras)
            bool alguemParado = false;
            foreach (Player p in Main.ActivePlayers)
                if (p.whoAmI != dono.whoAmI && !p.dead && p.Distance(Projectile.Center) < 56f && p.velocity.Length() < 1f)
                    alguemParado = true;
            Projectile.localAI[0] = alguemParado ? Math.Min(RaidJogador.DuracaoRevive, Projectile.localAI[0] + 1) : Math.Max(0, Projectile.localAI[0] - 2);

            Lighting.AddLight(Projectile.Center, 0.4f, 0.7f, 1f);
            if (Main.rand.NextBool(4))
                Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(14f, 14f), DustID.BlueFairy, -Vector2.UnitY, 120, default, 0.9f).noGravity = true;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            float flutua = MathF.Sin(Main.GlobalTimeWrappedHourly * 3f) * 5f;
            Vector2 c = Projectile.Center + new Vector2(0f, flutua);
            Desenho.Brilho(c, new Color(110, 180, 255), 0.9f);
            Desenho.Brilho(c, Color.White, 0.3f);

            // Anel de progresso do revive
            float progresso = Projectile.localAI[0] / RaidJogador.DuracaoRevive;
            if (progresso > 0f)
            {
                int pontos = (int)(40 * progresso);
                for (int i = 0; i < pontos; i++)
                {
                    float ang = -MathHelper.PiOver2 + i / 40f * MathHelper.TwoPi;
                    Desenho.Brilho(c + ang.ToRotationVector2() * 34f, new Color(255, 230, 140), 0.08f);
                }
            }

            Utils.DrawBorderString(Main.spriteBatch, Dono.name, c - Main.screenPosition + new Vector2(0f, -48f), new Color(180, 220, 255), 0.75f, 0.5f, 0.5f);
            return false;
        }
    }
}
