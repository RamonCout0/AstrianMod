using System;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AscensaoAstral.Content.Projectiles
{
    /// <summary>
    /// ATAQUES CONTRA QUEM VOA. Como no Hardmode todo mundo tem asas, "ficar longe do chão" não pode ser uma resposta:
    /// estes perigos cobrem o ar inteiro — perseguem, giram pela arena toda ou caem do teto em toda a largura.
    /// </summary>
    internal static class Aereo
    {
        public static Color Cor(float tipo) => tipo switch { 1 => new Color(170, 110, 255), 2 => new Color(255, 140, 60), _ => new Color(130, 215, 255) };

        public static Player MaisPerto(Vector2 de)
        {
            Player melhor = null;
            float dist = 3000f;
            foreach (Player p in Main.ActivePlayers)
            {
                if (p.dead) continue;
                float d = p.Distance(de);
                if (d < dist) { dist = d; melhor = p; }
            }
            return melhor;
        }
    }

    /// <summary>
    /// Lâmina que PERSEGUE o jogador por um tempo e depois segue reto. ai[0] = cor (0 gelo, 1 éter, 2 fogo),
    /// ai[1] = atraso antes de começar a perseguir. Some depois de 4 s.
    /// </summary>
    public class LaminaGuiada : ModProjectile
    {
        public override string Texture => "AscensaoAstral/Assets/Vazio";
        private const int TempoPerseguindo = 75;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
            ProjectileID.Sets.TrailingMode[Type] = 2;
        }

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.hostile = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = 1;
            Projectile.timeLeft = 240;
        }

        public override void AI()
        {
            int idade = 240 - Projectile.timeLeft;
            float atraso = Projectile.ai[1];

            if (idade < atraso)
                Projectile.velocity *= 0.93f; // freia e "mira"
            else if (idade < atraso + TempoPerseguindo)
            {
                Player alvo = Aereo.MaisPerto(Projectile.Center);
                if (alvo is not null)
                {
                    Vector2 desejada = Projectile.DirectionTo(alvo.Center) * 13f;
                    Projectile.velocity = Vector2.Lerp(Projectile.velocity, desejada, 0.06f);
                }
            }
            else if (Projectile.velocity.Length() < 20f)
                Projectile.velocity *= 1.03f;

            if (idade == (int)atraso)
                SoundEngine.PlaySound(SoundID.Item8 with { Volume = 0.4f, Pitch = 0.4f }, Projectile.Center);

            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, Aereo.Cor(Projectile.ai[0]).ToVector3() * 0.6f);
            if (Main.rand.NextBool(3))
                Dust.NewDustPerfect(Projectile.Center, Projectile.ai[0] == 0 ? DustID.IceTorch : DustID.PurpleTorch, -Projectile.velocity * 0.2f, 0, default, 1.1f).noGravity = true;
        }

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            if (Projectile.ai[0] == 0) target.AddBuff(BuffID.Chilled, 120);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Color c = Aereo.Cor(Projectile.ai[0]);
            Vector2[] pontos = new Vector2[Projectile.oldPos.Length];
            for (int i = 0; i < pontos.Length; i++)
                pontos[i] = Projectile.oldPos[i] == Vector2.Zero ? Projectile.Center : Projectile.oldPos[i] + Projectile.Size / 2f;
            PrimitiveRenderer.RenderTrail(pontos, new PrimitiveSettings(t => 14f * (1f - t), t => c * (1f - t) * 0.85f), 20);

            // Lâmina: losango afiado (ponta comprida na frente, curta atrás)
            Vector2 frente = Projectile.rotation.ToRotationVector2();
            Desenho.Ponta(Projectile.Center, Projectile.Center + frente * 34f, 16f, c, Color.White);
            Desenho.Ponta(Projectile.Center, Projectile.Center - frente * 14f, 16f, c, Color.White);
            Desenho.Brilho(Projectile.Center, c, 0.3f);

            // Antes de perseguir, um anel avisa que ela vai mirar
            int idade = 240 - Projectile.timeLeft;
            if (idade < Projectile.ai[1])
                Desenho.Brilho(Projectile.Center, c, 0.6f + 0.2f * MathF.Sin(idade * 0.6f));
            return false;
        }
    }

    /// <summary>
    /// Feixe que GIRA em volta de um ponto e varre a arena (não adianta voar: ele passa por toda altura).
    ///   ai[0] = ângulo inicial, ai[1] = sentido (+1 / -1) × ângulo total, ai[2] = índice do NPC dono (o feixe sai dele).
    ///   60 ticks de aviso (linha fina) → 150 ticks girando.
    /// </summary>
    public class FeixeGiratorio : ModProjectile
    {
        public override string Texture => "AscensaoAstral/Assets/Vazio";
        public const int Aviso = 60, Giro = 150;
        private const float Comprimento = 1700f;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 4000;

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Aviso + Giro;
        }

        private int Idade => Aviso + Giro - Projectile.timeLeft;
        private bool Ligado => Idade >= Aviso;

        private float Angulo
        {
            get
            {
                float t = Utils.GetLerpValue(Aviso, Aviso + Giro, Idade, true);
                // começa devagar, acelera, freia no fim
                float suave = t * t * (3f - 2f * t);
                return Projectile.ai[0] + Projectile.ai[1] * suave;
            }
        }

        private Vector2 Fim => Projectile.Center + Angulo.ToRotationVector2() * Comprimento;

        public override void AI()
        {
            NPC dono = Main.npc[(int)Projectile.ai[2]];
            if (dono.active)
                Projectile.Center = dono.Center;

            if (Idade == Aviso)
            {
                SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 0.6f }, Projectile.Center);
                ScreenShakeSystem.StartShakeAtPoint(Projectile.Center, 6f);
            }
            if (Ligado)
            {
                ScreenShakeSystem.SetUniversalRumble(1.2f, MathHelper.TwoPi, null, 0.2f);
                for (int i = 0; i < 2; i++)
                {
                    Vector2 p = Vector2.Lerp(Projectile.Center, Fim, Main.rand.NextFloat());
                    Dust.NewDustPerfect(p, DustID.PurpleTorch, Main.rand.NextVector2Circular(3f, 3f), 0, default, 1.4f).noGravity = true;
                }
            }
        }

        public override bool CanHitPlayer(Player target) => Ligado;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, Fim, 34f, ref _);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Color c = Aereo.Cor(1);
            if (!Ligado)
            {
                // Linha de aviso mostrando também PARA ONDE ele vai girar
                float pulso = 0.5f + 0.5f * MathF.Sin(Idade * 0.5f);
                Desenho.Risco(Projectile.Center, Fim, 8f + pulso * 6f, c * 0.7f);
                Vector2 seta = Projectile.Center + (Projectile.ai[0] + Math.Sign(Projectile.ai[1]) * 0.25f).ToRotationVector2() * 260f;
                Desenho.Brilho(seta, c, 0.5f + pulso * 0.3f);
                return false;
            }
            float largura = 70f * Utils.GetLerpValue(0, 8, Idade - Aviso, true) * Utils.GetLerpValue(0, 10, Projectile.timeLeft, true);
            Desenho.Golpe(Desenho.Estilo.Eter, Projectile.Center, Fim, largura, c, 1f + Utils.GetLerpValue(6, 0, Idade - Aviso, true));
            Desenho.Brilho(Projectile.Center, c, 1.6f);
            Desenho.Clarao(Projectile.Center, c, 0.9f, Main.GlobalTimeWrappedHourly * 2f);
            return false;
        }
    }

    /// <summary>
    /// Estrela/estalactite que fica parada no TETO da arena (aviso: risco até o chão) e depois despenca.
    /// ai[0] = cor (0 gelo, 1 éter), ai[1] = aviso em ticks, ai[2] = altura até o chão (tamanho do risco).
    /// </summary>
    public class QuedaEstelar : ModProjectile
    {
        public override string Texture => "AscensaoAstral/Assets/Vazio";

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 0;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2000;
        }

        public override void SetDefaults()
        {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.hostile = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 400;
        }

        private int Idade => 400 - Projectile.timeLeft;
        private bool Caindo => Idade >= Projectile.ai[1];

        public override void AI()
        {
            if (!Caindo)
            {
                Projectile.velocity = Vector2.Zero;
                return;
            }
            if (Idade == (int)Projectile.ai[1])
                Projectile.velocity = new Vector2(0f, 8f);
            if (Projectile.velocity.Y < 24f)
                Projectile.velocity.Y += 0.9f;
            if (Idade - Projectile.ai[1] > 70)
                Projectile.Kill();

            Lighting.AddLight(Projectile.Center, Aereo.Cor(Projectile.ai[0]).ToVector3() * 0.7f);
            if (Main.rand.NextBool(2))
                Dust.NewDustPerfect(Projectile.Center, Projectile.ai[0] == 0 ? DustID.IceTorch : DustID.PurpleTorch, Main.rand.NextVector2Circular(1f, 1f), 0, default, 1.2f).noGravity = true;
        }

        public override bool CanHitPlayer(Player target) => Caindo;

        public override void OnHitPlayer(Player target, Player.HurtInfo info)
        {
            if (Projectile.ai[0] == 0) target.AddBuff(BuffID.Chilled, 120);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Color c = Aereo.Cor(Projectile.ai[0]);
            if (!Caindo)
            {
                float pulso = 0.5f + 0.5f * MathF.Sin(Idade * 0.45f);
                Rectangle r = new((int)Projectile.Center.X - 10, (int)Projectile.Center.Y, 20, (int)Projectile.ai[2]);
                Desenho.RetanguloAviso(r, c, pulso, Idade / Projectile.ai[1]);
                Desenho.Brilho(Projectile.Center, c, 0.45f + pulso * 0.2f);
                return false;
            }
            Vector2[] pontos = new Vector2[Projectile.oldPos.Length];
            for (int i = 0; i < pontos.Length; i++)
                pontos[i] = Projectile.oldPos[i] == Vector2.Zero ? Projectile.Center : Projectile.oldPos[i] + Projectile.Size / 2f;
            // estrela cadente: cauda longa de energia + clarão em cruz na cabeça
            PrimitiveRenderer.RenderTrail(pontos, new PrimitiveSettings(t => 24f * MathF.Pow(1f - t, 1.3f), t => (Color.Lerp(Color.White, c, t * 1.6f) with { A = 0 }) * (1f - t)), 16);
            Desenho.Brilho(Projectile.Center, c, 0.6f);
            Desenho.Clarao(Projectile.Center, c, 0.3f, Projectile.identity);
            return false;
        }

        /// <summary>
        /// Uma "chuva" na largura toda, mas com BURACOS SEGUROS repetidos (um a cada <paramref name="periodo"/> px),
        /// então sempre existe um abrigo a poucos passos — a arena é fechada, não dá para fugir para longe.
        /// </summary>
        public static void Chuva(IEntitySourceLike fonte, float esq, float dir, float topo, float piso, float tipo, int dano, int aviso, float espacamento, float larguraDoBuraco, float periodo = 380f)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient) return;
            float deslocamento = Main.rand.NextFloat(periodo);
            for (float x = esq + espacamento / 2f; x < dir; x += espacamento)
            {
                float noPeriodo = ((x - esq + deslocamento) % periodo + periodo) % periodo;
                float distBuraco = Math.Abs(noPeriodo - periodo / 2f);
                if (distBuraco < larguraDoBuraco / 2f) continue;
                int atraso = aviso + (int)(x - esq) / 120; // varre da esquerda para a direita, dá para ler
                Projectile.NewProjectile(fonte.Fonte, new Vector2(x, topo + 10f), Vector2.Zero, ModContent.ProjectileType<QuedaEstelar>(), dano, 0f, Main.myPlayer, tipo, atraso, piso - topo);
            }
        }
    }
}
