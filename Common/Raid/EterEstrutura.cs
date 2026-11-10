using AscensaoAstral.Content.Tiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AscensaoAstral.Common.Raid
{
    /// <summary>
    /// O Éter "de bolso": uma ponte longa + uma arena construídas lá no alto do céu, perto da borda esquerda do mundo,
    /// no MESMO mundo (por isso os jogadores de fora e de dentro lutam ao mesmo tempo — com a SubworldLibrary
    /// cada dimensão seria um mundo separado e isso não daria).
    ///
    ///   [início da ponte] ═══════════════ 200 blocos ═══════════════ [arena 90 blocos]
    ///
    /// É montado quando a raid do Astharoth chega na Separação das Almas e desmontado quando ela acaba.
    /// </summary>
    public static class EterEstrutura
    {
        public const int ComprimentoPonte = 320; // o corredor da Travessia (~5.100 px)
        public const int LarguraArena = 150; // 2400 px, como a caixa da Supreme Calamitas
        private const int AlturaLimpa = 90;
        private const int AlturaParede = 84;

        // Na dimensão do Astharoth o Éter fica ao lado da arena de fora; fora dela (testes), no céu do mundo
        public static int X0 => Raids.Atual is ArenaDoAstharoth ? ArenaDoAstharoth.EterX0 : 160;
        public static int Y0 => Raids.Atual is ArenaDoAstharoth ? ArenaDoAstharoth.Superficie : (int)(Main.worldSurface * 0.6);

        public static Vector2 InicioDaPonte => new((X0 + 4) * 16f, Y0 * 16f - 40f);
        public static float FimDaPonteX => (X0 + ComprimentoPonte) * 16f;
        public static Rectangle Arena => new((X0 + ComprimentoPonte) * 16, (Y0 - 82) * 16, LarguraArena * 16, 82 * 16);
        public static Vector2 CentroDaArena => new(Arena.Center.X, Y0 * 16f - 300f);
        public static Rectangle Regiao => new(X0 * 16, (Y0 - AlturaLimpa) * 16, (ComprimentoPonte + LarguraArena) * 16, (AlturaLimpa + 30) * 16);

        public static bool Dentro(Player p) => Regiao.Contains(p.Center.ToPoint());

        /// <param name="soArena">Solo: constrói só a arena (sem a ponte/corredor).</param>
        public static void Construir(bool soArena = false)
        {
            if (Main.netMode == NetmodeID.MultiplayerClient || AstralMundo.EterConstruido)
                return;

            ushort ponte = (ushort)ModContent.TileType<PonteEterea>();
            int largura = ComprimentoPonte + LarguraArena;

            int inicio = soArena ? X0 + ComprimentoPonte : X0;
            for (int x = inicio; x < X0 + largura; x++)
            {
                for (int y = Y0 - AlturaLimpa; y < Y0; y++)
                    if (Framing.GetTileSafely(x, y).HasTile)
                        WorldGen.KillTile(x, y, noItem: true);

                WorldGen.PlaceTile(x, Y0, ponte, mute: true, forced: true);
                WorldGen.PlaceTile(x, Y0 + 1, ponte, mute: true, forced: true);
            }

            // Paredes nas pontas para ninguém cair pelos lados
            for (int y = Y0 - AlturaParede; y < Y0; y++)
            {
                WorldGen.PlaceTile(inicio, y, ponte, mute: true, forced: true);
                WorldGen.PlaceTile(X0 + largura - 1, y, ponte, mute: true, forced: true);
            }

            AstralMundo.EterConstruido = true;
            Sincronizar(largura);
            AstralMundo.SincronizarMundo();
        }

        public static void Demolir()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient || !AstralMundo.EterConstruido)
                return;

            ushort ponte = (ushort)ModContent.TileType<PonteEterea>();
            int largura = ComprimentoPonte + LarguraArena;
            for (int x = X0; x < X0 + largura; x++)
                for (int y = System.Math.Min(Y0 - AlturaParede, (int)(Main.worldSurface * 0.45) - 22); y <= Y0 + 1; y++) // também limpa a versão antiga (menor e mais alta)
                {
                    Tile t = Framing.GetTileSafely(x, y);
                    if (t.HasTile && t.TileType == ponte)
                    {
                        t.HasTile = false; // KillTile seria barrado pelo CanKillTile da ponte
                        WorldGen.SquareTileFrame(x, y);
                    }
                }

            AstralMundo.EterConstruido = false;
            Sincronizar(largura);
            AstralMundo.SincronizarMundo();
        }

        private static void Sincronizar(int largura)
        {
            if (Main.netMode != NetmodeID.Server)
                return;
            for (int x = X0; x < X0 + largura; x += 40)
                NetMessage.SendTileSquare(-1, x, Y0 - AlturaLimpa, 40, AlturaLimpa + 3);
        }
    }
}
