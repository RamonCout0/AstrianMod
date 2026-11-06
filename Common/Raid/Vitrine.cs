using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AscensaoAstral.Common.Raid
{
    /// <summary>
    /// VITRINE DE ATAQUES (só com DEBUG): dentro de uma raid, digite <c>/vitrine</c> no chat.
    /// O chefe faz TODOS os ataques, um atrás do outro (sem precisar de dano nem de fase), e o jogo tira
    /// prints sozinho durante cada um — salvos em ModSources/AscensaoAstral/Capturas/&lt;data&gt;/.
    /// Assim dá para revisar o visual de tudo sem jogar.
    ///   /vitrine          → todos os ataques + as mecânicas grandes
    ///   /vitrine ataques  → só os ataques (rápido)
    ///   /vitrine video    → grava VÍDEO: um quadro a cada 4 ticks em 1/3 da resolução (vira .mp4 depois)
    ///   /vitrine pular    → pula o ataque atual (se ele travar)
    ///   /vitrine parar    → encerra
    /// Se a janela do jogo for minimizada, o jogo para de desenhar: a vitrine percebe e REPETE o ataque quando a
    /// janela voltar, para nenhum ficar sem foto.
    /// </summary>
    public static class Vitrine
    {
        public struct Item
        {
            public int Estado, Fase, Limite, Intervalo, MaxFotos;
            public string Nome;
            public Item(int estado, int fase, string nome, bool mecanica = false)
            {
                Estado = estado; Fase = fase; Nome = nome;
                Limite = mecanica ? 60 * 50 : 60 * 16;
                Intervalo = mecanica ? 75 : 22;
                MaxFotos = mecanica ? 20 : 11;
            }

            public Item ComoVideo() { Item v = this; v.Intervalo = 4; v.MaxFotos = v.Limite / 4 + 1; return v; }
        }

        public static bool Ligada;
        /// <summary>Onde o jogador fica preso durante a vitrine (meio da arena, no chão).</summary>
        public static Microsoft.Xna.Framework.Vector2? PontoFixo;
        public static bool Video;
        private static bool refazer;
        private static List<Item> fila = new();
        private static int indice = -1, tempo, fotos, esperaNaPausa;
        public static string Pasta;
        public static int TotalDeFotos;

        public static void Iniciar(RaidBoss chefe, bool comMecanicas, bool video = false)
        {
            Video = video;
            fila = new List<Item>();
            foreach (Item it in chefe.AtaquesDaVitrine(comMecanicas)) fila.Add(video ? it.ComoVideo() : it);
            refazer = false;
            if (fila.Count == 0) { Main.NewText("Esse chefe não tem lista de vitrine.", 255, 120, 120); return; }
            Pasta = Path.Combine(Main.SavePath, "ModSources", "AscensaoAstral", "Capturas", DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss") + "_" + chefe.Name + (video ? "_video" : ""));
            Directory.CreateDirectory(Pasta);
            File.WriteAllText(Path.Combine(Pasta, "_indice.txt"), "foto;chefe;ataque;estado;fase;tick\n");
            Ligada = true;
            indice = -1; tempo = 0; fotos = 0; esperaNaPausa = 0; TotalDeFotos = 0;
            Main.NewText($"Vitrine: {fila.Count} ataques. As fotos vão para {Pasta}", 180, 220, 255);
        }

        /// <summary>Pula o ataque atual (se travar).</summary>
        public static void Pular(RaidBoss chefe)
        {
            if (!Ligada || indice < 0 || indice >= fila.Count) return;
            Main.NewText($"[vitrine] pulando {fila[indice].Nome}", 255, 220, 150);
            chefe.VitrineIr(chefe.EstadoDePausa, fila[indice].Fase);
            esperaNaPausa = 0;
        }

        /// <summary>Segura o jogador no ponto fixo (chamado pela vitrine e pelo próprio jogador a cada tick).</summary>
        public static void Prender(Player eu)
        {
            if (!Ligada || PontoFixo is not Vector2 p) return;
            eu.Center = p;
            eu.velocity = Vector2.Zero;
            eu.fallStart = (int)(eu.position.Y / 16f);
            eu.immune = true; eu.immuneTime = Math.Max(eu.immuneTime, 30); eu.immuneNoBlink = true;
            eu.statLife = eu.statLifeMax2;
        }

        public static void Parar(string motivo)
        {
            if (!Ligada) return;
            Ligada = false;
            PontoFixo = null;
            Main.NewText($"Vitrine encerrada ({motivo}). {TotalDeFotos} fotos em {Pasta}", 180, 220, 255);
        }

        /// <summary>Chamado pelo RaidBoss antes de cada tick do estado.</summary>
        public static void Controlar(RaidBoss chefe)
        {
            NPC n = chefe.NPC;
            n.life = n.lifeMax; // vida cheia: nenhuma transição automática atrapalha a ordem
            chefe.Stagger = 0f;

            // o jogador fica PARADO no meio da arena (a arena atual do chefe), imortal: só assiste
            Player eu = Main.LocalPlayer;
            PontoFixo = new Vector2((chefe.ArenaEsq + chefe.ArenaDir) / 2f, chefe.ArenaPiso - eu.height / 2f - 2f);
            Prender(eu);
            eu.immune = true; eu.immuneTime = Math.Max(eu.immuneTime, 30);
            eu.statLife = eu.statLifeMax2;

            // Janela minimizada = o jogo não desenha = sem foto. Espera voltar e repete o ataque do começo.
            bool desenhando = Main.GameUpdateCount - Captura.UltimoDesenho < 20;
            if (!desenhando)
            {
                if (indice >= 0 && !refazer) refazer = true;
                return;
            }
            if (refazer)
            {
                refazer = false;
                if (indice >= 0)
                {
                    Main.NewText($"[vitrine] a janela voltou — repetindo {fila[indice].Nome}", 255, 220, 150);
                    indice--;
                    chefe.VitrineIr(chefe.EstadoDePausa, fila[indice + 1].Fase);
                    esperaNaPausa = 0;
                    return;
                }
            }

            bool naPausa = chefe.Estado == chefe.EstadoDePausa;
            if (indice < 0 || naPausa)
            {
                // espera o chefe chegar na pausa (fim da intro / fim do ataque anterior) e chama o próximo
                if (!naPausa || ++esperaNaPausa < 40) return;
                esperaNaPausa = 0;
                indice++;
                if (indice >= fila.Count) { Parar("acabou a lista"); return; }
                Item it = fila[indice];
                chefe.VitrineIr(it.Estado, it.Fase);
                tempo = 0; fotos = 0;
                Main.NewText($"[vitrine {indice + 1}/{fila.Count}] {it.Nome}", 200, 200, 255);
                return;
            }

            Item atual = fila[indice];
            tempo++;
            if (tempo % atual.Intervalo == 8 % atual.Intervalo && fotos < atual.MaxFotos)
            {
                fotos++;
                string nome = Video ? $"{indice + 1:00}_{atual.Nome}_{fotos:0000}" : $"{indice + 1:00}_{atual.Nome}_{fotos:00}";
                Captura.Pedir(nome, $"{nome};{chefe.Name};{atual.Nome};{chefe.NomeDoEstado};{atual.Fase};{tempo}");
            }
            if (tempo > atual.Limite) // travou ou é longo demais: volta para a pausa e segue a fila
                chefe.VitrineIr(chefe.EstadoDePausa, atual.Fase);
        }
    }

    /// <summary>Tira um print do jogo (o quadro inteiro, já desenhado) e salva como JPG em meia resolução.</summary>
    public class Captura : ModSystem
    {
        private static string pendente, linha;
        public static uint UltimoDesenho;
        private static Color[] dados;

        public static void Pedir(string nome, string linhaDoIndice) { pendente = nome; linha = linhaDoIndice; }

        public override void Load() { if (!Main.dedServ) Main.OnPostDraw += DepoisDeDesenhar; }
        public override void Unload() { Main.OnPostDraw -= DepoisDeDesenhar; dados = null; }

        private static void DepoisDeDesenhar(GameTime _)
        {
            UltimoDesenho = Main.GameUpdateCount;
            if (pendente is null || Vitrine.Pasta is null) return;
            string nome = pendente; pendente = null;
            try
            {
                GraphicsDevice gd = Main.instance.GraphicsDevice;
                int w = gd.PresentationParameters.BackBufferWidth, h = gd.PresentationParameters.BackBufferHeight;
                if (dados is null || dados.Length != w * h) dados = new Color[w * h];
                gd.GetBackBufferData(dados);
                for (int i = 0; i < dados.Length; i++) dados[i].A = 255;
                using Texture2D tex = new(gd, w, h);
                tex.SetData(dados);
                using FileStream fs = File.Create(Path.Combine(Vitrine.Pasta, nome + ".jpg"));
                int div = Vitrine.Video ? 3 : 2;
                tex.SaveAsJpeg(fs, w / div, h / div);
                File.AppendAllText(Path.Combine(Vitrine.Pasta, "_indice.txt"), linha + "\n");
                Vitrine.TotalDeFotos++;
            }
            catch (Exception e)
            {
                ModContent.GetInstance<AscensaoAstral>().Logger.Warn("Falha ao capturar a tela: " + e.Message);
            }
        }
    }

    public class ComandoVitrine : ModCommand
    {
        public override bool IsLoadingEnabled(Mod mod) => AscensaoAstral.DEBUG;
        public override CommandType Type => CommandType.Chat;
        public override string Command => "vitrine";
        public override string Usage => "/vitrine [ataques|video|pular|parar]";
        public override string Description => "O chefe da raid faz todos os ataques em sequência e o jogo tira prints de cada um.";

        public override void Action(CommandCaller caller, string input, string[] args)
        {
            if (args.Length > 0 && args[0] == "parar") { Vitrine.Parar("pedido"); return; }
            if (args.Length > 0 && args[0] == "pular") { if (RaidBoss.Ativo is RaidBoss c) Vitrine.Pular(c); return; }
            if (Main.netMode != NetmodeID.SinglePlayer) { caller.Reply("A vitrine só funciona em single player.", Color.OrangeRed); return; }
            if (RaidBoss.Ativo is not RaidBoss chefe) { caller.Reply("Entre numa raid (o chefe precisa estar vivo) e digite /vitrine de novo.", Color.OrangeRed); return; }
            bool video = args.Length > 0 && args[0] == "video";
            Vitrine.Iniciar(chefe, !(args.Length > 0 && args[0] == "ataques"), video);
        }
    }
}
