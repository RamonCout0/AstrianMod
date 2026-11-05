using System.IO;
using AscensaoAstral.Common.Raid;
using AscensaoAstral.Content.NPCs;
using AscensaoAstral.Content.Tiles;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.Chat;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace AscensaoAstral.Common
{
    /// <summary>
    /// O estado da história no mundo:
    ///   1. Parede de Carne morta + altar quebrado → "O mundo está tendo uma interferência exterior" + gera Astrânia
    ///   2. Primeiro jogador minera Astrânia → clarão, tremor, explosão, cratera com o Eco Estelar dentro
    ///   3. Cada chefe derrotado aproxima o planeta Astra (veja Content/Cenario/CeuAstral.cs)
    /// </summary>
    public class AstralMundo : ModSystem
    {
        public static bool AstraniaLiberada;
        public static bool ImpactoAconteceu;
        public static bool DerrotouSilvanna;
        public static bool DerrotouAstharoth;
        public static bool EterConstruido;   // o Éter (ponte + arena no céu) ficou de pé? Se o jogo fechar no meio, desmonta ao carregar
        public static Point Cratera;

        /// <summary>5 no Hardmode + 4 pós-Moon Lord + 1 intermediário + 1 endgame.</summary>
        public const int TotalDeChefes = 11;

        public static int ChefesDerrotados => (DerrotouSilvanna ? 1 : 0) + (DerrotouAstharoth ? 1 : 0);

        /// <summary>0 = Astra lá longe, 1 = todos derrotados (o planeta se despedaça).</summary>
        public static float ProgressoAstra => ChefesDerrotados / (float)TotalDeChefes;

        // ─── Salvar / carregar / rede ───
        public override void ClearWorld()
        {
            AstraniaLiberada = ImpactoAconteceu = DerrotouSilvanna = DerrotouAstharoth = EterConstruido = false;
            Cratera = Point.Zero;
            impactoVisto = false;
            primeiroTick = true;
        }

        public override void SaveWorldData(TagCompound tag)
        {
            tag["astrania"] = AstraniaLiberada;
            tag["impacto"] = ImpactoAconteceu;
            tag["silvanna"] = DerrotouSilvanna;
            tag["astharoth"] = DerrotouAstharoth;
            tag["eter"] = EterConstruido;
            tag["crateraX"] = Cratera.X;
            tag["crateraY"] = Cratera.Y;
        }

        public override void LoadWorldData(TagCompound tag)
        {
            AstraniaLiberada = tag.GetBool("astrania");
            ImpactoAconteceu = tag.GetBool("impacto");
            DerrotouSilvanna = tag.GetBool("silvanna");
            DerrotouAstharoth = tag.GetBool("astharoth");
            EterConstruido = tag.GetBool("eter");
            Cratera = new Point(tag.GetInt("crateraX"), tag.GetInt("crateraY"));
        }

        public override void NetSend(BinaryWriter writer)
        {
            writer.Write(new BitsByte(AstraniaLiberada, ImpactoAconteceu, DerrotouSilvanna, DerrotouAstharoth, EterConstruido));
        }

        public override void NetReceive(BinaryReader reader)
        {
            BitsByte b = reader.ReadByte();
            AstraniaLiberada = b[0];
            ImpactoAconteceu = b[1];
            DerrotouSilvanna = b[2];
            DerrotouAstharoth = b[3];
            EterConstruido = b[4];
        }

        public static void SincronizarMundo()
        {
            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.WorldData);
        }

        /// <summary>Mensagem no chat para todo mundo (funciona em single e multiplayer).</summary>
        public static void Mensagem(string texto, Color cor)
        {
            if (Main.netMode == NetmodeID.SinglePlayer)
                Main.NewText(texto, cor);
            else if (Main.netMode == NetmodeID.Server)
                ChatHelper.BroadcastChatMessage(NetworkText.FromLiteral(texto), cor);
        }

        // ─── 1. Liberar a Astrânia ───
        public override void PostUpdateWorld()
        {
            // Roda só no servidor / single player
            if (!AstraniaLiberada && Main.hardMode && WorldGen.altarCount > 0)
                LiberarAstrania();

            // Se o jogo fechou com o Éter de pé e não há luta acontecendo, desmonta
            if (EterConstruido && RaidBoss.Ativo is null)
                EterEstrutura.Demolir();
        }

        public static void LiberarAstraniaParaTeste() => LiberarAstrania();

        private static void LiberarAstrania()
        {
            AstraniaLiberada = true;
            ushort tipo = (ushort)ModContent.TileType<AstraniaTile>();
            int veios = (int)(Main.maxTilesX * Main.maxTilesY * 0.00006);

            for (int k = 0; k < veios; k++)
            {
                int x = WorldGen.genRand.Next(100, Main.maxTilesX - 100);
                int y = WorldGen.genRand.Next((int)Main.rockLayer, Main.maxTilesY - 250);
                WorldGen.OreRunner(x, y, WorldGen.genRand.Next(3, 6), WorldGen.genRand.Next(3, 7), tipo);
            }

            Mensagem("O mundo está tendo uma interferência exterior...", new Color(130, 190, 255));
            SincronizarMundo();
        }

        // ─── 2. O impacto (chamado pela AstraniaTile quando alguém minera a primeira) ───
        public static void DispararImpacto(int i, int j)
        {
            if (ImpactoAconteceu || Main.netMode == NetmodeID.MultiplayerClient)
                return;

            ImpactoAconteceu = true;

            // A cratera fica na superfície, um pouco para o lado de onde a pessoa minerou
            int x = i + (WorldGen.genRand.NextBool() ? 1 : -1) * WorldGen.genRand.Next(35, 60);
            x = Utils.Clamp(x, 60, Main.maxTilesX - 60);
            int superficie = AcharSuperficie(x);
            CavarCratera(x, superficie);

            Cratera = new Point(x, superficie);
            int yNpc = (superficie + 6) * 16;
            NPC.NewNPC(new EntitySource_WorldEvent(), x * 16, yNpc, ModContent.NPCType<EcoEstelar>());

            int distancia = System.Math.Abs(x - i);
            string lado = x > i ? "leste (direita)" : "oeste (esquerda)";
            Mensagem($"Algo caiu do céu... na superfície, uns {distancia} blocos a {lado} de onde a Astrânia foi minerada.", new Color(255, 230, 160));
            SincronizarMundo();
        }

        private static int AcharSuperficie(int x)
        {
            for (int y = 40; y < Main.worldSurface + 60; y++)
            {
                Tile t = Framing.GetTileSafely(x, y);
                if (t.HasTile && Main.tileSolid[t.TileType])
                    return y;
            }
            return (int)Main.worldSurface;
        }

        private static void CavarCratera(int cx, int cy)
        {
            const int raioX = 16, raioY = 8;
            ushort minerio = (ushort)ModContent.TileType<AstraniaTile>();

            for (int dx = -raioX; dx <= raioX; dx++)
            {
                for (int dy = -raioY * 2; dy <= raioY; dy++)
                {
                    float nx = dx / (float)raioX;
                    float ny = dy / (float)raioY;
                    int x = cx + dx, y = cy + dy;
                    if (!WorldGen.InWorld(x, y, 10))
                        continue;

                    if (dy <= 0 || nx * nx + ny * ny <= 1f)
                        WorldGen.KillTile(x, y, noItem: true);
                }

                // Borda do fundo da cratera com Astrânia
                float fundo = raioY * (float)System.Math.Sqrt(System.Math.Max(0, 1 - (dx / (float)raioX) * (dx / (float)raioX)));
                int yFundo = cy + (int)fundo + 1;
                if (WorldGen.InWorld(cx + dx, yFundo, 10))
                    WorldGen.PlaceTile(cx + dx, yFundo, minerio, mute: true, forced: true);
            }

            if (Main.netMode == NetmodeID.Server)
                NetMessage.SendTileSquare(-1, cx - raioX - 1, cy - raioY * 2 - 1, raioX * 2 + 3, raioY * 3 + 4);
        }

        // ─── Efeitos do impacto para cada jogador (cliente) ───
        private static bool impactoVisto;
        private static bool primeiroTick = true;

        public override void PostUpdateEverything()
        {
            if (Main.dedServ)
                return;

            // No primeiro tick do mundo só "lembramos" o estado, para não repetir o clarão ao carregar o save
            if (primeiroTick)
            {
                primeiroTick = false;
                impactoVisto = ImpactoAconteceu;
            }

            if (ImpactoAconteceu && !impactoVisto)
            {
                impactoVisto = true;
                Hud.Clarao(1f);
                ScreenShakeSystem.StartShake(22f);
                SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1f, Pitch = -0.7f });
                SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.6f, Pitch = -0.9f });
            }
        }
    }
}
