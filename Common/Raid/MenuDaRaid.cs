using System;
using System.Collections.Generic;
using System.IO;
using Luminance.Assets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.UI;

namespace AscensaoAstral.Common.Raid
{
    /// <summary>Tudo o que o menu de entrada e a tela de carregamento mostram de cada raid.</summary>
    public class InfoDaRaid
    {
        public string Nome, Local, Fala, QuemFala;
        public string[] Dicas;
        public string Retrato;          // textura
        public Rectangle? Quadro;       // recorte da folha (null = textura inteira)
        public float EscalaRetrato;
        public Color Cor;
        public Func<bool> JaVencida;
        public Action<Player> Entrar;
        public int Indice;

        public static readonly List<InfoDaRaid> Todas = new()
        {
            new InfoDaRaid
            {
                Indice = 0, Nome = "O Chapéu de Armotheus", Local = "Vale Gélido", QuemFala = "Silvanna",
                Fala = "A tempestade sempre vem. Ele é o único que fica de pé na frente dela.",
                Dicas = new[] { "No Giro, segure-se com o gancho", "Na Nevasca, fique atrás do cavaleiro: fora dele você congela", "Pegue um escudo antes da Lança Solar" },
                Retrato = "AscensaoAstral/Assets/Sheets/Idle", Quadro = new Rectangle(0, 0, 100, 100), EscalaRetrato = 2.3f,
                Cor = new Color(130, 210, 255), JaVencida = () => AstralMundo.DerrotouSilvanna,
                Entrar = p => Raids.Entrar<ArenaDaSilvanna>(p)
            },
            new InfoDaRaid
            {
                Indice = 1, Nome = "Astharoth, o Guerreiro Errante", Local = "Errância do Éter", QuemFala = "Astharoth",
                Fala = "Este mundo é pequeno demais para nós dois. Vou cortar vocês ao meio.",
                Dicas = new[] { "Metade do grupo vai ao Éter: só os de fora veem o símbolo", "Olhe para o dragão quando ele atravessar", "Quando o Arcanjo fechar a volta, fique na sombra" },
                Retrato = "AscensaoAstral/Content/Bosses/Astharoth/Astharoth", Quadro = null, EscalaRetrato = 1.8f,
                Cor = new Color(190, 150, 255), JaVencida = () => AstralMundo.DerrotouAstharoth,
                Entrar = p => Raids.Entrar<ArenaDoAstharoth>(p)
            },
        };

        public string Titulo => JaVencida() ? "Memória: " + Nome : Nome;
        public string FalaMostrada => JaVencida() ? "Isso não é ele. É só o que sobrou na memória da Astrânia." : Fala;
        public string QuemFalaMostrado => JaVencida() ? Dialogo.NomeEco : QuemFala;

        /// <summary>Desenha o retrato numa moldura (menu e tela de carregamento usam o mesmo).</summary>
        public void DesenharRetrato(SpriteBatch sb, Rectangle area)
        {
            Texture2D px = TextureAssets.MagicPixel.Value, bloom = MiscTexturesRegistry.BloomCircleSmall.Value;
            Rectangle um = new(0, 0, 1, 1);
            sb.Draw(px, area, um, new Color(12, 10, 24));
            sb.Draw(bloom, area.Center.ToVector2(), null, (Cor with { A = 0 }) * 0.45f, 0f, bloom.Size() / 2f, area.Width / (float)bloom.Width * 1.6f, 0, 0f);
            Texture2D tex = ModContent.Request<Texture2D>(Retrato).Value;
            Rectangle q = Quadro ?? tex.Bounds;
            float treme = JaVencida() ? 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 11f) : 1f;
            Color c = JaVencida() ? new Color(150, 165, 210) * 0.8f * treme : Color.White;
            sb.Draw(tex, area.Center.ToVector2() + new Vector2(0f, MathF.Sin(Main.GlobalTimeWrappedHourly * 2f) * 4f), q, c, 0f, q.Size() / 2f, EscalaRetrato, 0, 0f);
            Moldura(sb, area, Cor);
        }

        /// <summary>Prévia da arena: um desenho simples do lugar.</summary>
        public void DesenharArena(SpriteBatch sb, Rectangle a)
        {
            Texture2D px = TextureAssets.MagicPixel.Value, bloom = MiscTexturesRegistry.BloomCircleSmall.Value;
            Rectangle um = new(0, 0, 1, 1);
            float t = Main.GlobalTimeWrappedHourly;
            if (Indice == 0)
            {
                for (int i = 0; i < a.Height; i += 2)
                    sb.Draw(px, new Rectangle(a.X, a.Y + i, a.Width, 2), um, Color.Lerp(new Color(4, 8, 24), new Color(30, 50, 90), i / (float)a.Height));
                for (int i = 0; i < 6; i++) // aurora
                    sb.Draw(bloom, new Vector2(a.X + a.Width * (0.15f + i * 0.15f), a.Y + a.Height * 0.3f + MathF.Sin(t + i) * 4f), null, new Color(80, 255, 190, 0) * 0.35f, 0f, bloom.Size() / 2f, new Vector2(0.5f, 0.18f), 0, 0f);
                sb.Draw(px, new Rectangle(a.X, a.Bottom - a.Height / 5, a.Width, a.Height / 5), um, new Color(220, 235, 255));
                for (int i = 0; i < 5; i++) // cavaleiros ao fundo
                    sb.Draw(px, new Rectangle(a.X + a.Width * (i + 1) / 6 - 3, a.Bottom - a.Height / 5 - 14, 6, 14), um, new Color(110, 150, 210) * 0.7f);
            }
            else
            {
                for (int i = 0; i < a.Height; i += 2)
                    sb.Draw(px, new Rectangle(a.X, a.Y + i, a.Width, 2), um, Color.Lerp(new Color(6, 3, 16), new Color(35, 14, 50), i / (float)a.Height));
                Vector2 c = new(a.X + a.Width * 0.62f, a.Y + a.Height * 0.4f);
                sb.Draw(bloom, c, null, new Color(255, 120, 80, 0) * 0.5f, 0f, bloom.Size() / 2f, a.Height / (float)bloom.Height * 1.1f, 0, 0f);
                sb.Draw(ModContent.Request<Texture2D>("AscensaoAstral/Assets/Circulo").Value, c, null, Color.Black, 0f, new Vector2(128f), a.Height * 0.22f / 128f, 0, 0f);
                sb.Draw(px, new Rectangle(a.X, a.Bottom - a.Height / 6, a.Width * 2 / 5, a.Height / 6), um, new Color(60, 90, 50));
                sb.Draw(px, new Rectangle(a.X + a.Width * 2 / 5 + 6, a.Bottom - a.Height / 3, a.Width * 3 / 5 - 6, 4), um, new Color(150, 90, 255)); // a ponte do Éter
            }
            Moldura(sb, a, Cor * 0.8f);
        }

        public static void Moldura(SpriteBatch sb, Rectangle r, Color c)
        {
            Texture2D px = TextureAssets.MagicPixel.Value; Rectangle um = new(0, 0, 1, 1);
            sb.Draw(px, new Rectangle(r.X, r.Y, r.Width, 2), um, c); sb.Draw(px, new Rectangle(r.X, r.Bottom - 2, r.Width, 2), um, c);
            sb.Draw(px, new Rectangle(r.X, r.Y, 2, r.Height), um, c); sb.Draw(px, new Rectangle(r.Right - 2, r.Y, 2, r.Height), um, c);
        }
    }

    /// <summary>
    /// O MENU DE ENTRADA DA RAID: retrato do chefe, prévia da arena, dicas, o grupo e os botões.
    /// Em multiplayer todo mundo perto de quem abriu marca PRONTO; quando todos estão prontos, Iniciar leva o grupo.
    /// </summary>
    public class MenuDaRaid : ModSystem
    {
        public static InfoDaRaid Aberta;
        public static readonly bool[] Prontos = new bool[Main.maxPlayers];
        private static bool prontoLocal;

        public static void Abrir(int indice)
        {
            if (Main.dedServ) return;
            Aberta = InfoDaRaid.Todas[indice];
            prontoLocal = Main.netMode == NetmodeID.SinglePlayer;
            Array.Clear(Prontos);
            Prontos[Main.myPlayer] = prontoLocal;
            SoundEngine.PlaySound(SoundID.MenuOpen);
        }

        public static void Fechar()
        {
            Aberta = null;
            if (Main.netMode == NetmodeID.MultiplayerClient && prontoLocal) EnviarPronto(false);
            prontoLocal = false;
            SoundEngine.PlaySound(SoundID.MenuClose);
        }

        private static void EnviarPronto(bool pronto)
        {
            ModPacket p = ModContent.GetInstance<AscensaoAstral>().GetPacket();
            p.Write((byte)1); p.Write(pronto); p.Send();
        }

        /// <summary>Pacote 1 (cliente → servidor) e 2 (servidor → todos): quem está pronto.</summary>
        public static void ReceberPronto(BinaryReader r, int quem, bool doServidor)
        {
            if (!doServidor)
            {
                bool pronto = r.ReadBoolean();
                Prontos[quem] = pronto;
                ModPacket p = ModContent.GetInstance<AscensaoAstral>().GetPacket();
                p.Write((byte)2); p.Write((byte)quem); p.Write(pronto); p.Send();
            }
            else
            {
                int j = r.ReadByte(); bool pronto = r.ReadBoolean();
                Prontos[j] = pronto;
            }
        }

        private static List<Player> Grupo()
        {
            List<Player> g = new();
            foreach (Player p in Main.ActivePlayers)
                if (p.whoAmI == Main.myPlayer || p.Distance(Main.LocalPlayer.Center) < 3000f) g.Add(p);
            return g;
        }

        // O Bot de Teste está sempre pronto (ele não tem como clicar)
        private static bool EstaPronto(Player p) => p.whoAmI == BotDeTeste.Indice || Prontos[p.whoAmI];

        private static bool TodosProntos()
        {
            foreach (Player p in Grupo()) if (!EstaPronto(p)) return false;
            return true;
        }

        // ─── Interação ───
        private static Rectangle Painel => new(Main.screenWidth / 2 - 420, Main.screenHeight / 2 - 250, 840, 500);
        private static Rectangle BotaoPronto => new(Painel.X + 330, Painel.Bottom - 70, 200, 44);
        private static Rectangle BotaoIniciar => new(Painel.X + 545, Painel.Bottom - 70, 200, 44);
        private static Rectangle BotaoSair => new(Painel.Right - 46, Painel.Y + 12, 32, 32);

        private static InfoDaRaid entrarPendente;

        public override void UpdateUI(GameTime gameTime)
        {
            if (entrarPendente is not null)
            {
                InfoDaRaid info = entrarPendente;
                entrarPendente = null;
                info.Entrar(Main.LocalPlayer);
                return;
            }
            if (Aberta is null) return;
            if (Main.LocalPlayer.dead || Main.gameMenu || Main.keyState.IsKeyDown(Microsoft.Xna.Framework.Input.Keys.Escape)) Fechar();
        }

        // Os cliques são lidos DENTRO do desenho: lá a tela e o mouse estão na mesma escala da interface
        // (no UpdateUI o tamanho da tela não tem a escala da UI e os botões "fugiam" do lugar desenhado).
        private static void Interagir()
        {
            Point m = Main.MouseScreen.ToPoint();
            if (Painel.Contains(m)) { Main.LocalPlayer.mouseInterface = true; Main.blockMouse = true; }
            if (!(Main.mouseLeft && Main.mouseLeftRelease)) return;
            Main.mouseLeftRelease = false;

            if (BotaoSair.Contains(m)) { Fechar(); return; }
            if (BotaoPronto.Contains(m) && Main.netMode == NetmodeID.MultiplayerClient)
            {
                prontoLocal = !prontoLocal;
                Prontos[Main.myPlayer] = prontoLocal;
                EnviarPronto(prontoLocal);
                SoundEngine.PlaySound(SoundID.MenuTick);
            }
            if (BotaoIniciar.Contains(m) && TodosProntos())
            {
                entrarPendente = Aberta; // entra no próximo update (não dá para trocar de mundo no meio do desenho)
                Aberta = null;
                SoundEngine.PlaySound(SoundID.Item4);
            }
        }

        public override void ModifyInterfaceLayers(List<GameInterfaceLayer> camadas)
        {
            int i = camadas.FindIndex(c => c.Name == "Vanilla: Mouse Text");
            if (i < 0) return;
            camadas.Insert(i, new LegacyGameInterfaceLayer("AscensaoAstral: Menu da Raid", () => { if (Aberta is not null) Desenhar(Main.spriteBatch); return true; }, InterfaceScaleType.UI));
        }

        private static void Desenhar(SpriteBatch sb)
        {
            Interagir();
            if (Aberta is null) return;
            InfoDaRaid info = Aberta;
            Texture2D px = TextureAssets.MagicPixel.Value; Rectangle um = new(0, 0, 1, 1);
            Point m = Main.MouseScreen.ToPoint();
            sb.Draw(px, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), um, Color.Black * 0.55f);
            Rectangle p = Painel;
            sb.Draw(px, p, um, new Color(16, 13, 30) * 0.96f);
            InfoDaRaid.Moldura(sb, p, info.Cor);

            // Esquerda: retrato e arena
            info.DesenharRetrato(sb, new Rectangle(p.X + 24, p.Y + 24, 280, 280));
            info.DesenharArena(sb, new Rectangle(p.X + 24, p.Y + 320, 280, 150));
            Utils.DrawBorderString(sb, info.Local, new Vector2(p.X + 32, p.Y + 326), Color.White, 0.75f);

            // Direita: nome, fala, dicas, grupo
            int x = p.X + 330, y = p.Y + 28;
            Utils.DrawBorderString(sb, "RAID", new Vector2(x, y), info.Cor, 0.8f); y += 22;
            Utils.DrawBorderString(sb, info.Titulo, new Vector2(x, y), Color.White, 1.15f); y += 38;
            Utils.DrawBorderString(sb, $"\"{info.FalaMostrada}\"", new Vector2(x, y), new Color(200, 195, 225), 0.8f); y += 20;
            Utils.DrawBorderString(sb, "- " + info.QuemFalaMostrado, new Vector2(x, y), new Color(150, 145, 175), 0.7f); y += 36;
            Utils.DrawBorderString(sb, "O que lembrar", new Vector2(x, y), info.Cor, 0.85f); y += 24;
            foreach (string d in info.Dicas) { Utils.DrawBorderString(sb, "-  " + d, new Vector2(x + 6, y), Color.White, 0.75f); y += 22; }
            y += 14;
            List<Player> grupo = Grupo();
            Utils.DrawBorderString(sb, $"Grupo ({grupo.Count})  -  recomendado 1 a 4", new Vector2(x, y), info.Cor, 0.85f); y += 24;
            foreach (Player g in grupo)
            {
                bool pronto = EstaPronto(g);
                Utils.DrawBorderString(sb, (pronto ? "[pronto]  " : "[...]  ") + g.name, new Vector2(x + 6, y), pronto ? new Color(130, 240, 170) : new Color(190, 190, 200), 0.75f);
                y += 20;
                if (y > p.Bottom - 90) break;
            }

            // Botões
            if (Main.netMode == NetmodeID.MultiplayerClient)
                Botao(sb, BotaoPronto, prontoLocal ? "Pronto!" : "Marcar pronto", prontoLocal ? new Color(70, 150, 100) : new Color(60, 55, 90), m);
            bool pode = TodosProntos();
            Botao(sb, BotaoIniciar, pode ? "Iniciar raid" : "Esperando o grupo", pode ? info.Cor * 0.8f : new Color(50, 48, 64), m);
            Botao(sb, BotaoSair, "X", new Color(90, 50, 60), m);
        }

        private static void Botao(SpriteBatch sb, Rectangle r, string texto, Color cor, Point mouse)
        {
            Texture2D px = TextureAssets.MagicPixel.Value;
            bool sobre = r.Contains(mouse);
            sb.Draw(px, r, new Rectangle(0, 0, 1, 1), sobre ? Color.Lerp(cor, Color.White, 0.2f) : cor);
            InfoDaRaid.Moldura(sb, r, Color.White * (sobre ? 0.8f : 0.35f));
            Utils.DrawBorderString(sb, texto, r.Center.ToVector2() + new Vector2(0f, 3f), Color.White, 0.85f, 0.5f, 0.5f);
        }
    }

    /// <summary>A tela de carregamento da dimensão: a mesma arte do menu, grande.</summary>
    public static class TelaDeCarregamento
    {
        public static void Desenhar(InfoDaRaid info)
        {
            SpriteBatch sb = Main.spriteBatch;
            int w = Main.screenWidth, h = Main.screenHeight;
            Texture2D px = TextureAssets.MagicPixel.Value, bloom = MiscTexturesRegistry.BloomCircleSmall.Value;
            Rectangle um = new(0, 0, 1, 1);
            for (int i = 0; i < h; i += 4)
                sb.Draw(px, new Rectangle(0, i, w, 4), um, Color.Lerp(new Color(6, 5, 14), Color.Lerp(new Color(6, 5, 14), info.Cor, 0.25f), i / (float)h));
            float t = Main.GlobalTimeWrappedHourly;
            for (int i = 0; i < 40; i++) // poeira subindo
            {
                float x = (i * 0.137f % 1f) * w, y = h - ((t * 40f + i * 53f) % (h + 40f));
                sb.Draw(bloom, new Vector2(x, y), null, (info.Cor with { A = 0 }) * 0.4f, 0f, bloom.Size() / 2f, 0.05f, 0, 0f);
            }
            info.DesenharRetrato(sb, new Rectangle(w / 2 - 170, h / 2 - 260, 340, 340));
            Utils.DrawBorderString(sb, info.Titulo, new Vector2(w / 2f, h / 2f + 110), Color.White, 1.4f, 0.5f, 0f);
            Utils.DrawBorderString(sb, $"\"{info.FalaMostrada}\"", new Vector2(w / 2f, h / 2f + 160), new Color(200, 195, 225), 0.9f, 0.5f, 0f);
            Utils.DrawBorderString(sb, Main.statusText, new Vector2(w / 2f, h - 90), new Color(170, 165, 200), 0.8f, 0.5f, 0f);
            for (int i = 0; i < 12; i++) // anel girando = carregando
            {
                float a = t * 3f + i / 12f * MathHelper.TwoPi;
                sb.Draw(bloom, new Vector2(w / 2f, h - 140) + a.ToRotationVector2() * 18f, null, (info.Cor with { A = 0 }) * (i / 12f), 0f, bloom.Size() / 2f, 0.06f, 0, 0f);
            }
        }
    }
}
