using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Chat;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AscensaoAstral.Common
{
    /// <summary>
    /// Caixa de diálogo das cutscenes: nome de quem fala + texto aparecendo letra por letra.
    /// As falas entram numa fila e tocam uma depois da outra.
    /// </summary>
    public class Dialogo : ModSystem
    {
        private record Fala(string Nome, string Texto, Color Cor, int Duracao);

        private static readonly Queue<Fala> fila = new();
        private static Fala atual;
        private static int tempo;

        public const string NomeEco = "Eco Estelar"; // nome provisório do companheiro: troque à vontade

        /// <summary>Fala local (use dentro da IA dos chefes, que roda em todos os clientes).</summary>
        public static void Falar(string nome, string texto, Color cor, int duracao = 210)
        {
            if (Main.dedServ)
                return;
            // Uma MEMÓRIA não fala: é só um eco do que o chefe foi. Só o Eco Estelar comenta.
            if (nome != NomeEco && Raid.RaidBoss.Ativo is { EhMemoria: true })
                return;
            fila.Enqueue(new Fala(nome, texto, cor, duracao));
        }

        /// <summary>Fala para todo mundo a partir do servidor (use em OnKill, que só roda no servidor no multiplayer).</summary>
        public static void FalarParaTodos(string nome, string texto, Color cor)
        {
            if (Main.netMode == NetmodeID.Server)
                ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral($"{nome}: {texto}"), cor);
            else
                Falar(nome, texto, cor);
        }

        /// <summary>O companheiro comenta, mas só para quem está fundido com ele.</summary>
        public static void FalaDoEco(string texto)
        {
            if (Main.netMode == NetmodeID.Server)
            {
                ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral($"{NomeEco}: {texto}"), new Color(170, 200, 255));
                return;
            }
            if (Main.LocalPlayer.GetModPlayer<AstralJogador>().Fundido)
                Falar(NomeEco, texto, new Color(170, 200, 255));
        }

        public static bool Ocupado => atual is not null || fila.Count > 0;

        public override void OnWorldUnload()
        {
            fila.Clear();
            atual = null;
        }

        public override void PostUpdateEverything()
        {
            if (Main.dedServ)
                return;

            if (atual is null && fila.Count > 0)
            {
                atual = fila.Dequeue();
                tempo = 0;
            }

            if (atual is not null && ++tempo > atual.Duracao)
                atual = null;
        }

        public override void PostDrawInterface(SpriteBatch spriteBatch)
        {
            if (atual is null)
                return;

            float alfa = Math.Min(1f, tempo / 12f) * Math.Min(1f, (atual.Duracao - tempo) / 15f);
            int letras = Math.Min(atual.Texto.Length, tempo * 2);
            string visivel = atual.Texto[..letras];

            int largura = Math.Min(900, Main.screenWidth - 80);
            int altura = 90;
            int x = Main.screenWidth / 2 - largura / 2;
            int y = (int)(Main.screenHeight * 0.68f);
            Texture2D px = TextureAssets.MagicPixel.Value;
            Rectangle um = new(0, 0, 1, 1);

            spriteBatch.Draw(px, new Rectangle(x - 3, y - 3, largura + 6, altura + 6), um, atual.Cor * 0.6f * alfa);
            spriteBatch.Draw(px, new Rectangle(x, y, largura, altura), um, new Color(8, 6, 18) * 0.88f * alfa);

            Utils.DrawBorderString(spriteBatch, atual.Nome, new Vector2(x + 16, y + 10), atual.Cor * alfa, 1f);
            Utils.DrawBorderString(spriteBatch, QuebrarLinhas(visivel, largura - 32), new Vector2(x + 16, y + 38), Color.White * alfa, 0.9f);
        }

        private static string QuebrarLinhas(string texto, int larguraPx)
        {
            var fonte = FontAssets.MouseText.Value;
            string[] palavras = texto.Split(' ');
            string linha = "", resultado = "";
            foreach (string p in palavras)
            {
                string teste = linha.Length == 0 ? p : linha + " " + p;
                if (fonte.MeasureString(teste).X * 0.9f > larguraPx)
                {
                    resultado += linha + "\n";
                    linha = p;
                }
                else
                    linha = teste;
            }
            return resultado + linha;
        }
    }
}
