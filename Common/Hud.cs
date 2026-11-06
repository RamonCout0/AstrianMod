using System;
using AscensaoAstral.Common.Raid;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ModLoader;

namespace AscensaoAstral.Common
{
    /// <summary>
    /// Tudo que é desenhado por cima da tela:
    ///  - banner com o nome da etapa
    ///  - aviso grande no centro ("SEGURE-SE COM O GANCHO!")
    ///  - símbolo que só um lado do Éter enxerga (Astharoth)
    ///  - barra de quebra (stagger) + tempo dos portões
    ///  - clarão branco (impacto, fusões)
    ///  - mensagem para quem está morto esperando reviver
    ///  - linha de diagnóstico (AscensaoAstral.DEBUG)
    /// </summary>
    public class Hud : ModSystem
    {
        // Banner
        private static string textoBanner;
        private static Color corBanner;
        private static int tempoBanner;
        private const int DuracaoBanner = 220;

        // Aviso central
        private static string textoAviso;
        private static Color corAviso;
        private static int tempoAviso;

        // Clarão
        private static float clarao;
        public static float ClaraoAlvo; // >0 mantém a tela branca (o chefe controla); 0 = só decai

        /// <summary>-1 = nenhum; 0 círculo, 1 triângulo, 2 quadrado. Usado na sequência de símbolos do Astharoth.</summary>
        public static int SimboloVisivel = -1;

        public static void Banner(string texto, Color cor)
        {
            if (Main.dedServ) return;
            textoBanner = texto;
            corBanner = cor;
            tempoBanner = DuracaoBanner;
        }

        /// <summary>
        /// Avisos de instrução ("FIQUE ATRÁS DELE!") e banners de etapa estão DESLIGADOS: no Terraria nenhum chefe
        /// explica a luta por texto — o golpe se mostra pelo próprio visual (área marcada, brilho, som).
        /// Ligue para depurar.
        /// </summary>
        public static bool AvisosDeTexto = false;

        /// <summary>Banner de etapa de chefe ("ETAPA 2: ..."): desligado junto com os avisos.</summary>
        public static void Fase(string texto, Color cor)
        {
            if (AvisosDeTexto) Banner(texto, cor);
        }

        public static void Aviso(string texto, Color cor, int duracao = 120)
        {
            if (Main.dedServ || !AvisosDeTexto) return;
            textoAviso = texto;
            corAviso = cor;
            tempoAviso = duracao;
        }

        public static void Clarao(float intensidade)
        {
            if (Main.dedServ) return;
            clarao = Math.Max(clarao, Math.Min(intensidade * 0.4f, 0.3f)); // flash curto e fraco: o Terraria quase nunca estoura a tela
        }

        public override void OnWorldUnload()
        {
            tempoBanner = tempoAviso = 0;
            clarao = ClaraoAlvo = 0f;
            SimboloVisivel = -1;
        }

        public override void PostUpdateEverything()
        {
            if (Main.dedServ) return;
            if (Raid.RaidBoss.Ativo is null) ClaraoAlvo = 0f; // sem luta, nada de tela clara presa
            clarao = ClaraoAlvo > 0f ? MathHelper.Lerp(clarao, ClaraoAlvo, 0.08f) : Math.Max(0f, clarao - 0.03f);
            if (tempoBanner > 0) tempoBanner--;
            if (tempoAviso > 0) tempoAviso--;
        }

        public override void PostDrawInterface(SpriteBatch spriteBatch)
        {
            Texture2D px = TextureAssets.MagicPixel.Value;
            Rectangle um = new(0, 0, 1, 1);

            if (clarao > 0.01f)
                spriteBatch.Draw(px, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), um, Color.White * clarao);

            if (tempoBanner > 0 && textoBanner is not null)
            {
                int passado = DuracaoBanner - tempoBanner;
                float alfa = Math.Min(1f, passado / 20f) * Math.Min(1f, tempoBanner / 30f);
                float escala = 1f + 0.08f * Math.Max(0f, 1f - passado / 25f);
                Utils.DrawBorderStringBig(spriteBatch, textoBanner, new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.2f), corBanner * alfa, escala, 0.5f, 0.5f);
            }

            if (tempoAviso > 0 && textoAviso is not null)
            {
                float pulso = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 10f);
                Utils.DrawBorderStringBig(spriteBatch, textoAviso, new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.32f), corAviso * Math.Min(1f, tempoAviso / 15f), 0.8f * pulso, 0.5f, 0.5f);
            }

            if (SimboloVisivel >= 0)
                DesenharSimbolo(spriteBatch, new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.42f), SimboloVisivel, 70f);

            RaidBoss boss = RaidBoss.Ativo;
            if (boss is not null)
            {
                if (boss.MostrarBarraDeQuebra)
                    DesenharStagger(spriteBatch, boss);

                if (Main.LocalPlayer.dead && !boss.Reiniciando)
                {
                    RaidJogador rj = Main.LocalPlayer.GetModPlayer<RaidJogador>();
                    string msg = rj.TemAlma
                        ? $"Sua alma espera. Um aliado precisa ficar PARADO sobre ela para te reviver ({rj.ProgressoRevive * 100 / RaidJogador.DuracaoRevive}%)"
                        : "Aguardando o fim da raid...";
                    Utils.DrawBorderString(spriteBatch, msg, new Vector2(Main.screenWidth / 2f, Main.screenHeight * 0.55f), new Color(180, 220, 255), 1f, 0.5f, 0.5f);
                }

                if (AscensaoAstral.DEBUG)
                {
                    Utils.DrawBorderString(spriteBatch, boss.Diagnostico(), new Vector2(Main.screenWidth / 2f, 110), new Color(180, 255, 180), 0.75f, 0.5f, 0.5f);
                    // Diz se cada shader foi compilado pela Luminance (✘ = o jogo está usando o "plano B" sem shader)
                    Utils.DrawBorderString(spriteBatch, EstadoDosShaders(), new Vector2(Main.screenWidth / 2f, 128), new Color(180, 220, 255), 0.7f, 0.5f, 0.5f);
                }
            }
        }

        private static string EstadoDosShaders()
        {
            static string S(string n) => Luminance.Core.Graphics.ShaderManager.TryGetShader("AscensaoAstral." + n, out _) ? n + " OK" : n + " FALHOU";
            static string F(string n) => Luminance.Core.Graphics.ShaderManager.TryGetFilter("AscensaoAstral." + n, out _) ? n + " OK" : n + " FALHOU";
            return "[shaders] " + string.Join(" | ", S("NuvensShader"), S("AuroraShader"), S("LaserEscarlateShader"), F("NevascaFilter"), F("GeloFilter"), F("BuracoNegroFilter"));
        }

        private static void DesenharStagger(SpriteBatch spriteBatch, RaidBoss boss)
        {
            Texture2D px = TextureAssets.MagicPixel.Value;
            Rectangle um = new(0, 0, 1, 1);
            int largura = 420, altura = 12;
            int x = Main.screenWidth / 2 - largura / 2;
            int y = Main.screenHeight - 120;

            float progresso = MathHelper.Clamp(boss.Stagger / boss.MetaStagger, 0f, 1f);
            bool portao = boss.PortaoAtivo;
            bool atordoado = boss.EstaAtordoado;

            Color cor = atordoado ? new Color(255, 255, 90) : portao ? new Color(200, 120, 255) : new Color(255, 200, 70);
            string rotulo = atordoado ? "ATORDOADO — dano x2!" : portao ? "QUEBRE A POSTURA!" : "Quebra";

            spriteBatch.Draw(px, new Rectangle(x - 3, y - 3, largura + 6, altura + 6), um, Color.Black * 0.75f);
            spriteBatch.Draw(px, new Rectangle(x, y, (int)(largura * (atordoado ? 1f : progresso)), altura), um, cor);
            Utils.DrawBorderString(spriteBatch, rotulo, new Vector2(Main.screenWidth / 2f, y - 14), Color.White, 0.8f, 0.5f, 0.5f);

            float resta = boss.TempoRestantePortao;
            if (resta >= 0f)
            {
                Color corTempo = resta < 0.25f ? Color.Lerp(new Color(255, 40, 40), Color.White, 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 12f)) : new Color(255, 70, 60);
                spriteBatch.Draw(px, new Rectangle(x, y + altura + 4, (int)(largura * resta), 4), um, corTempo);
            }
        }

        /// <summary>Desenha um dos 3 símbolos (círculo, triângulo, quadrado) com contorno brilhante.</summary>
        public static void DesenharSimbolo(SpriteBatch sb, Vector2 centro, int simbolo, float raio)
        {
            Texture2D px = TextureAssets.MagicPixel.Value;
            Color cor = CorDoSimbolo(simbolo);
            int lados = simbolo switch { 0 => 32, 1 => 3, _ => 4 };
            float giro = simbolo == 2 ? MathHelper.PiOver4 : -MathHelper.PiOver2;

            for (int i = 0; i < lados; i++)
            {
                Vector2 a = centro + (giro + i * MathHelper.TwoPi / lados).ToRotationVector2() * raio;
                Vector2 b = centro + (giro + (i + 1) * MathHelper.TwoPi / lados).ToRotationVector2() * raio;
                Vector2 d = b - a;
                sb.Draw(px, a, new Rectangle(0, 0, 1, 1), cor, d.ToRotation(), new Vector2(0f, 0.5f), new Vector2(d.Length(), 6f), SpriteEffects.None, 0f);
            }
        }

        public static Color CorDoSimbolo(int s) => s switch
        {
            0 => new Color(90, 230, 255),
            1 => new Color(255, 210, 80),
            _ => new Color(255, 90, 200)
        };
    }
}
