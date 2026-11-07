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

        private static ManagedShader Energia(Color cor, Color nucleo, float intensidade, float afilar)
        {
            if (ShaderManager.TryGetShader("AscensaoAstral.EnergiaShader", out ManagedShader e))
            {
                e.TrySetParameter("corBorda", cor.ToVector3());
                e.TrySetParameter("corNucleo", nucleo.ToVector3());
                e.TrySetParameter("intensidade", intensidade);
                e.TrySetParameter("afilar", afilar);
                e.TrySetParameter("velocidade", 1f);
                e.SetTexture(MiscTexturesRegistry.TurbulentNoise.Value, 1, SamplerState.LinearWrap);
                e.SetTexture(MiscTexturesRegistry.WavyBlotchNoise.Value, 2, SamplerState.LinearWrap);
                return e;
            }
            if (ShaderManager.TryGetShader("AscensaoAstral.LaserEscarlateShader", out ManagedShader s))
            {
                s.TrySetParameter("corBorda", cor.ToVector3());
                s.TrySetParameter("corNucleo", nucleo.ToVector3());
                s.SetTexture(MiscTexturesRegistry.WavyBlotchNoise.Value, 1, SamplerState.LinearWrap);
                return s;
            }
            return null;
        }

        private static Vector2[] Pontos(Vector2 a, Vector2 b, int n)
        {
            Vector2[] p = new Vector2[n];
            for (int i = 0; i < n; i++) p[i] = Vector2.Lerp(a, b, i / (float)(n - 1));
            return p;
        }

        // ═══ OS ESTILOS: cada família de ataque tem o seu shader ═══
        public enum Estilo { Energia, Corte, Gelo, Solar, Eter }

        public static Estilo DoTipo(TipoPerigo t) => t switch
        {
            TipoPerigo.Gelo => Estilo.Gelo,
            TipoPerigo.Solar => Estilo.Solar,
            TipoPerigo.Atordoar or TipoPerigo.Eter => Estilo.Eter,
            _ => Estilo.Corte
        };

        private static ManagedShader Estilo_(Estilo e, Color cor, float intensidade, float opacidade)
        {
            string nome = e switch { Estilo.Corte => "CorteShader", Estilo.Gelo => "GeloShader", Estilo.Solar => "SolarShader", Estilo.Eter => "EterShader", _ => null };
            if (nome is null || !ShaderManager.TryGetShader("AscensaoAstral." + nome, out ManagedShader sh)) return null;
            sh.TrySetParameter("cor", cor.ToVector3());
            sh.TrySetParameter("intensidade", intensidade);
            sh.TrySetParameter("opacidade", opacidade);
            Texture2D r1 = e == Estilo.Gelo ? MiscTexturesRegistry.DendriticNoise.Value : MiscTexturesRegistry.TurbulentNoise.Value;
            Texture2D r2 = e is Estilo.Corte or Estilo.Eter ? MiscTexturesRegistry.DendriticNoise.Value : MiscTexturesRegistry.WavyBlotchNoise.Value;
            sh.SetTexture(r1, 1, SamplerState.LinearWrap);
            sh.SetTexture(r2, 2, SamplerState.LinearWrap);
            return sh;
        }

        /// <summary>
        /// Golpe reto no estilo pedido. <paramref name="largura"/> é a largura do GOLPE; a faixa desenhada tem o dobro,
        /// e o shader usa a sobra para o brilho (que some suave — sem "barra" em volta).
        /// </summary>
        public static void Golpe(Estilo estilo, Vector2 a, Vector2 b, float largura, Color cor, float intensidade = 1f, float opacidade = 1f)
        {
            if (largura < 0.5f || a == b) return;
            ManagedShader sh = Estilo_(estilo, cor, intensidade, opacidade);
            if (sh is null) { Faixa(a, b, largura, cor, Color.White, intensidade); return; }
            Vector2[] pts = Pontos(a, b, 16);
            PrimitiveRenderer.RenderTrail(pts, new PrimitiveSettings(_ => largura * 2f, _ => Color.White, Smoothen: false, Shader: sh), 16);
        }

        /// <summary>Golpe CURVO (meia-lua, arco de espada) passando pelos pontos dados; a largura segue a função (0 → 1 ao longo do arco).</summary>
        public static void GolpeCurvo(Estilo estilo, Vector2[] pts, Func<float, float> largura, Color cor, float intensidade = 1f, float opacidade = 1f)
        {
            if (pts.Length < 2) return;
            ManagedShader sh = Estilo_(estilo, cor, intensidade, opacidade);
            if (sh is null) { PrimitiveRenderer.RenderTrail(pts, new PrimitiveSettings(t => largura(t), t => (cor with { A = 0 }) * opacidade), pts.Length); return; }
            PrimitiveRenderer.RenderTrail(pts, new PrimitiveSettings(t => largura(t) * 2f, _ => Color.White, Smoothen: true, Shader: sh), pts.Length * 2);
        }

        /// <summary>Lança com forma (cabo → guarda → lâmina → ponta) pelo LancaShader. <paramref name="largura"/> = largura da lâmina.</summary>
        public static void Lanca(Vector2 cabo, Vector2 ponta, float largura, Color cor, float intensidade = 1f, float opacidade = 1f)
        {
            if (cabo == ponta) return;
            if (!ShaderManager.TryGetShader("AscensaoAstral.LancaShader", out ManagedShader sh)) { Ponta(cabo, ponta, largura, cor, Color.White, intensidade); return; }
            sh.TrySetParameter("cor", cor.ToVector3());
            sh.TrySetParameter("intensidade", intensidade);
            sh.TrySetParameter("opacidade", opacidade);
            sh.SetTexture(MiscTexturesRegistry.TurbulentNoise.Value, 1, SamplerState.LinearWrap);
            sh.SetTexture(MiscTexturesRegistry.WavyBlotchNoise.Value, 2, SamplerState.LinearWrap);
            Vector2[] pts = Pontos(cabo, ponta, 16);
            PrimitiveRenderer.RenderTrail(pts, new PrimitiveSettings(_ => largura / 0.48f, _ => Color.White, Smoothen: false, Shader: sh), 16);
            Brilho(ponta, cor, 0.25f * opacidade);
        }

        /// <summary>Raios de luz caindo (god rays) num retângulo do mundo, com uma faixa de sombra sem luz.</summary>
        public static void Raios(Rectangle area, Color cor, float carga, float sombraEsq, float sombraDir)
        {
            if (carga <= 0.01f || !ShaderManager.TryGetShader("AscensaoAstral.RaiosShader", out ManagedShader sh)) return;
            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            sh.TrySetParameter("cor", cor.ToVector3());
            sh.TrySetParameter("carga", carga);
            sh.TrySetParameter("sombraEsq", (sombraEsq - area.X) / area.Width);
            sh.TrySetParameter("sombraDir", (sombraDir - area.X) / area.Width);
            sh.TrySetParameter("proporcao", area.Width / (float)area.Height);
            sh.SetTexture(MiscTexturesRegistry.TurbulentNoise.Value, 1, SamplerState.LinearWrap);
            sh.Apply();
            sb.Draw(ModContent.Request<Texture2D>("AscensaoAstral/Assets/Branco").Value, new Rectangle(area.X - (int)Main.screenPosition.X, area.Y - (int)Main.screenPosition.Y, area.Width, area.Height), Color.White);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
        }

        /// <summary>Faixa reta de energia genérica (o estilo "neutro").</summary>
        /// <summary>
        /// Golpe reto de energia (cortes, feixes, pilares). Três camadas: halo largo e suave → corpo com o
        /// EnergiaShader (filamentos correndo, borda irregular, pontas afiladas) → clarão nas pontas.
        /// <paramref name="afilar"/>: 1 = pontas finas (corte), 0 = pontas retas (pilar que bate no chão).
        /// </summary>
        public static void Faixa(Vector2 a, Vector2 b, float largura, Color cor, Color nucleo, float intensidade = 1f, float afilar = 0.6f)
        {
            if (largura < 0.5f || a == b) return;
            Vector2[] pts = Pontos(a, b, 12);
            // corpo
            PrimitiveRenderer.RenderTrail(pts, new PrimitiveSettings(_ => largura, _ => Color.White, Smoothen: false, Shader: Energia(cor, nucleo, intensidade, afilar)), 12);
            // pontas
            float ponta = MathHelper.Clamp(largura / 90f, 0.15f, 1.4f);
            Brilho(a, cor, ponta * (1f - afilar * 0.6f));
            Brilho(b, cor, ponta * (1f - afilar * 0.6f));
        }

        private static float Afinar(float t, float afilar) => MathHelper.Lerp(1f, MathF.Pow(MathF.Sin(MathHelper.Pi * t), 0.5f), afilar);

        /// <summary>Ponta: larga na base, afiada na ponta (espinhos, cristais, lâminas, penas).</summary>
        public static void Ponta(Vector2 baseDaPonta, Vector2 ponta, float largura, Color cor, Color nucleo, float intensidade = 1f, Estilo estilo = Estilo.Energia)
        {
            if (largura < 0.5f || baseDaPonta == ponta) return;
            Vector2[] pts = Pontos(baseDaPonta, ponta, 10);
            if (estilo != Estilo.Energia && Estilo_(estilo, cor, intensidade, 1f) is ManagedShader sh)
                PrimitiveRenderer.RenderTrail(pts, new PrimitiveSettings(t => largura * 2f * MathF.Pow(1f - t, 0.85f), _ => Color.White, Smoothen: false, Shader: sh), 10);
            else
                PrimitiveRenderer.RenderTrail(pts, new PrimitiveSettings(t => largura * MathF.Pow(1f - t, 0.85f), _ => Color.White, Smoothen: false, Shader: Energia(cor, nucleo, intensidade, 0f)), 10);
            Brilho(ponta, Color.White, 0.12f + largura / 260f);
        }

        /// <summary>Clarão em cruz (estrela de 4 pontas) — o "brilho" de impacto/ativação.</summary>
        public static void Clarao(Vector2 mundo, Color cor, float escala, float giro = 0f)
        {
            Texture2D flare = MiscTexturesRegistry.ShineFlareTexture.Value;
            Vector2 tela = mundo - Main.screenPosition;
            Main.spriteBatch.Draw(flare, tela, null, cor with { A = 0 }, giro, flare.Size() / 2f, escala, 0, 0f);
            Main.spriteBatch.Draw(flare, tela, null, Color.White with { A = 0 } * 0.8f, giro, flare.Size() / 2f, escala * 0.5f, 0, 0f);
        }

        /// <summary>Linha de luz (BloomLine) entre dois pontos.</summary>
        /// <summary>Corta o segmento a→b na linha do chão da arena (nada de golpe/aviso aparecendo embaixo da terra).</summary>
        public static Vector2 CortarNoChao(Vector2 a, Vector2 b)
        {
            if (RaidBoss.Ativo is not RaidBoss chefe) return b;
            float piso = chefe.ArenaPiso;
            if (b.Y <= piso || a.Y >= piso) return b;
            return Vector2.Lerp(a, b, (piso - a.Y) / (b.Y - a.Y));
        }

        public static void Risco(Vector2 a, Vector2 b, float espessura, Color cor)
        {
            Texture2D linha = MiscTexturesRegistry.BloomLineTexture.Value;
            Vector2 d = b - a;
            if (d.LengthSquared() < 1f) return;
            Main.spriteBatch.Draw(linha, (a + b) / 2f - Main.screenPosition, null, cor with { A = 0 }, d.ToRotation() + MathHelper.PiOver2, linha.Size() / 2f,
                new Vector2(espessura / linha.Width, d.Length() / linha.Height), 0, 0f);
        }

        /// <summary>
        /// Esfera de energia com o OrbeShader (gira em 3D, borda acesa, veios de energia). <paramref name="revelar"/> mistura
        /// <paramref name="corA"/> → <paramref name="corB"/>. Sem shader, cai num brilho simples.
        /// </summary>
        public static void Orbe(Vector2 mundo, float diametro, Color corA, Color corB, float revelar, float semente, float opacidade = 1f)
        {
            if (!ShaderManager.TryGetShader("AscensaoAstral.OrbeShader", out ManagedShader shader))
            {
                Color c = Color.Lerp(corA, corB, revelar) * opacidade;
                Brilho(mundo, c, diametro / 70f);
                Brilho(mundo, Color.White * opacidade, diametro / 220f);
                return;
            }
            SpriteBatch sb = Main.spriteBatch;
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            shader.TrySetParameter("corA", corA.ToVector3());
            shader.TrySetParameter("corB", corB.ToVector3());
            shader.TrySetParameter("revelar", MathHelper.Clamp(revelar, 0f, 1f));
            shader.TrySetParameter("semente", semente);
            shader.SetTexture(MiscTexturesRegistry.TurbulentNoise.Value, 1, SamplerState.LinearWrap);
            shader.SetTexture(MiscTexturesRegistry.WavyBlotchNoise.Value, 2, SamplerState.LinearWrap);
            shader.Apply();
            Vector2 tela = mundo - Main.screenPosition;
            sb.Draw(ModContent.Request<Texture2D>("AscensaoAstral/Assets/Branco").Value, new Rectangle((int)(tela.X - diametro / 2f), (int)(tela.Y - diametro / 2f), (int)diametro, (int)diametro), Color.White * opacidade);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, Main.GameViewMatrix.TransformationMatrix);
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

            int idade = Tempo - Aviso;
            float some = Utils.GetLerpValue(Ativo, Ativo - 8, idade, true);   // apaga no fim
            float bate = Utils.GetLerpValue(5f, 0f, idade, true);             // o instante do golpe
            bool horizontal = r.Width >= r.Height;
            Vector2 a = horizontal ? new Vector2(r.Left, r.Center.Y) : new Vector2(r.Center.X, r.Top);
            Vector2 b = horizontal ? new Vector2(r.Right, r.Center.Y) : new Vector2(r.Center.X, r.Bottom);
            float curto = horizontal ? r.Height : r.Width;
            Vector2 lado = horizontal ? Vector2.UnitY : Vector2.UnitX;

            // o corpo: o shader do tipo (corte = rasgo, gelo = cristal, solar = fogo, éter = vazio).
            // O golpe estoura para o branco no instante em que bate e afina quando some.
            float largura = curto * (1f + 0.15f * bate) * MathHelper.Lerp(0.6f, 1f, some);
            Vector2 de = a, ate = b;
            if (Tipo == TipoPerigo.Solar && !horizontal) { de = b; ate = a; } // o fogo sobe do chão
            Desenho.Golpe(Desenho.DoTipo(Tipo), de, ate, largura, cor, 1f + bate * 1.2f, some);

            // 4) o detalhe de cada tipo
            ulong semente = (ulong)Projectile.identity * 7919UL + 13UL;
            float comprimento = Vector2.Distance(a, b);
            Vector2 ao = (b - a).SafeNormalize(Vector2.UnitX);
            switch (Tipo)
            {
                case TipoPerigo.Corte:
                    // o clarão do fio nas duas pontas, no instante do corte
                    if (bate > 0f) { Desenho.Clarao(a, cor, 0.6f * bate); Desenho.Clarao(b, cor, 0.6f * bate); }
                    break;
                case TipoPerigo.Gelo:
                    // cristais saindo das bordas
                    for (int i = 0; i < Math.Max(2, (int)(comprimento / 60f)); i++)
                    {
                        float pos = (i + 0.5f) / Math.Max(2, (int)(comprimento / 60f)) * comprimento;
                        float alt = (18f + Utils.RandomFloat(ref semente) * 26f) * some * (1f - bate * 0.5f);
                        int s1 = i % 2 == 0 ? 1 : -1;
                        Vector2 baseC = a + ao * pos + lado * s1 * curto / 2f;
                        Desenho.Ponta(baseC, baseC + (lado * s1).RotatedBy((Utils.RandomFloat(ref semente) - 0.5f) * 0.8f) * alt, 9f, cor, Color.White, 1f, Desenho.Estilo.Gelo);
                    }
                    break;
                case TipoPerigo.Solar:
                    Desenho.Brilho(de, new Color(255, 170, 70), curto / 70f * some);
                    break;
                default:
                    // éter / atordoar: centelhas piscando dentro
                    for (int i = 0; i < 5; i++)
                    {
                        Vector2 c = a + ao * Utils.RandomFloat(ref semente) * comprimento + lado * (Utils.RandomFloat(ref semente) - 0.5f) * curto;
                        float pisca = 0.5f + 0.5f * MathF.Sin(Tempo * 0.4f + i * 1.7f);
                        Desenho.Clarao(c, cor, 0.18f * pisca * some, Tempo * 0.05f);
                    }
                    break;
            }
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
            // bateu no chão da arena: estoura (não atravessa o chão iluminando a terra por baixo)
            if (RaidBoss.Ativo is RaidBoss chefe && Projectile.Center.Y > chefe.ArenaPiso + 4f)
            {
                for (int i = 0; i < 8; i++)
                    Dust.NewDustPerfect(Projectile.Center, Projectile.ai[0] switch { 1 => DustID.PurpleTorch, 2 => DustID.Torch, _ => DustID.IceTorch }, Main.rand.NextVector2Circular(4f, 4f) - Vector2.UnitY * 2f, 0, default, 1.3f).noGravity = true;
                Projectile.Kill();
                return;
            }
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
            PrimitiveRenderer.RenderTrail(pontos, new PrimitiveSettings(t => 22f * MathF.Pow(1f - t, 1.4f), t => (Color.Lerp(Color.White, c, t * 1.5f) with { A = 0 }) * (1f - t)), 16);
            float giro = Main.GlobalTimeWrappedHourly * 6f + Projectile.identity;
            Desenho.Brilho(Projectile.Center, c, 0.55f);
            Desenho.Clarao(Projectile.Center, c, 0.22f + 0.04f * MathF.Sin(giro * 2f), giro);
            Desenho.Brilho(Projectile.Center, Color.White, 0.16f);
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
