using System;
using System.Collections.Generic;
using System.IO;
using AscensaoAstral.Content.Bosses.Astharoth;
using AscensaoAstral.Content.Bosses.Silvanna;
using AscensaoAstral.Content.Tiles;
using Microsoft.Xna.Framework;
using SubworldLibrary;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.IO;
using Terraria.ModLoader;
using Terraria.WorldBuilding;

namespace AscensaoAstral.Common.Raid
{
    /// <summary>
    /// CADA RAID TEM A SUA DIMENSÃO (SubworldLibrary). O mapa É a arena: a borda do mundo é a parede, então
    /// ninguém foge e nada do mundo principal é tocado.
    ///
    ///   - O item de invocação leva o grupo para cá (veja InvocacaoDeRaid).
    ///   - Quando todo mundo chega, o chefe nasce sozinho.
    ///   - Venceu → o progresso volta para o mundo principal e todos saem depois de alguns segundos.
    ///   - Nada é salvo (ShouldSave = false): toda entrada gera a arena do zero.
    ///
    /// Para TROCAR A ARENA: mexa em Gerar() de cada mundo (ou carregue uma estrutura do StructureHelper ali).
    /// </summary>
    public abstract class MundoDeRaid : Subworld
    {
        /// <summary>Faixa invisível de ~41 blocos em cada borda que o Terraria não deixa ninguém pisar.</summary>
        public const int Margem = 42;

        protected abstract int TipoDoChefe { get; }
        /// <summary>Qual InfoDaRaid a tela de carregamento mostra.</summary>
        protected abstract int IndiceDaRaid { get; }

        public override void DrawMenu(GameTime gameTime) => TelaDeCarregamento.Desenhar(InfoDaRaid.Todas[IndiceDaRaid]);
        /// <summary>A arena principal (em PIXELS do mundo) — o chefe usa isto no lugar de "em volta do jogador".</summary>
        public abstract Rectangle Arena { get; }
        protected abstract void Gerar();

        public override bool ShouldSave => false;
        public override bool NoPlayerSaving => false;

        public static bool Vencida;
        private static int tempoComJogadores;
        private static int tempoDepoisDaVitoria;

        public override List<GenPass> Tasks => new() { new PassoSimples("Arena", Gerar) };

        public override void OnLoad()
        {
            Vencida = false;
            tempoComJogadores = 0;
            tempoDepoisDaVitoria = 0;
            Main.dayTime = false;
            Main.time = 16200; // meia-noite: céu escuro, nossos céus aparecem melhor
        }

        public override void Update()
        {
            Main.dayTime = false;
            Main.time = 16200;

            bool alguem = false;
            foreach (Player p in Main.ActivePlayers)
                alguem = true;
            if (!alguem) return;

            if (Vencida)
            {
                // Depois da despedida, todo mundo volta para casa
                if (++tempoDepoisDaVitoria == 60 * 8)
                    VoltarTodos();
                return;
            }

            // O chefe aparece 3 s depois que alguém chega (dá tempo de todo mundo carregar)
            if (!NPC.AnyNPCs(TipoDoChefe) && ++tempoComJogadores > 180)
            {
                Rectangle a = Arena;
                int i = NPC.NewNPC(new EntitySource_WorldEvent(), a.Center.X, a.Bottom - 200, TipoDoChefe);
                if (Main.netMode == NetmodeID.Server && i < Main.maxNPCs)
                    NetMessage.SendData(MessageID.SyncNPC, number: i);
                tempoComJogadores = 0;
            }
        }

        public static void VoltarTodos()
        {
            if (Main.netMode == NetmodeID.SinglePlayer)
                SubworldSystem.Exit();
            else if (Main.netMode == NetmodeID.Server)
                foreach (Player p in Main.ActivePlayers)
                    SubworldSystem.MovePlayerToMainWorld(p.whoAmI);
        }

        // ─── O progresso da história vai e volta entre o mundo e a dimensão ───
        public override void CopyMainWorldData() => CopiarFlags();
        public override void CopySubworldData() => CopiarFlags();
        public override void ReadCopiedMainWorldData() => LerFlags();
        public override void ReadCopiedSubworldData() => LerFlags();

        private static void CopiarFlags()
        {
            SubworldSystem.CopyWorldData("astrania", AstralMundo.AstraniaLiberada);
            SubworldSystem.CopyWorldData("impacto", AstralMundo.ImpactoAconteceu);
            SubworldSystem.CopyWorldData("silvanna", AstralMundo.DerrotouSilvanna);
            SubworldSystem.CopyWorldData("astharoth", AstralMundo.DerrotouAstharoth);
        }

        private static void LerFlags()
        {
            AstralMundo.AstraniaLiberada = SubworldSystem.ReadCopiedWorldData<bool>("astrania");
            AstralMundo.ImpactoAconteceu = SubworldSystem.ReadCopiedWorldData<bool>("impacto");
            // Derrotas só "somam": nunca apaga uma vitória que já existia
            AstralMundo.DerrotouSilvanna |= SubworldSystem.ReadCopiedWorldData<bool>("silvanna");
            AstralMundo.DerrotouAstharoth |= SubworldSystem.ReadCopiedWorldData<bool>("astharoth");
        }

        // ─── Ferramentas de geração ───
        protected static void Preencher(int x0, int y0, int x1, int y1, ushort tipo)
        {
            for (int x = x0; x < x1; x++)
                for (int y = y0; y < y1; y++)
                {
                    Tile t = Main.tile[x, y];
                    t.HasTile = true;
                    t.TileType = tipo;
                    t.Slope = 0;
                    t.IsHalfBlock = false;
                }
        }

        protected static void PrepararMundo(int superficie)
        {
            Main.worldSurface = superficie + 12; // tudo acima do chão conta como "superfície" (céu de fundo)
            Main.rockLayer = superficie + 20;
            Main.spawnTileX = Main.maxTilesX / 2;
            Main.spawnTileY = superficie - 3;
        }

        protected static void Enquadrar()
        {
            for (int x = 1; x < Main.maxTilesX - 1; x++)
                for (int y = 1; y < Main.maxTilesY - 1; y++)
                    WorldGen.SquareTileFrame(x, y, false);
        }

        private class PassoSimples : GenPass
        {
            private readonly Action acao;
            public PassoSimples(string nome, Action acao) : base(nome, 1) => this.acao = acao;
            protected override void ApplyPass(GenerationProgress progress, GameConfiguration configuration)
            {
                progress.Message = "Montando a arena...";
                acao();
            }
        }
    }

    /// <summary>Raid da Silvanna: um vale de neve, 150 × 80 blocos de arena.</summary>
    public class ArenaDaSilvanna : MundoDeRaid
    {
        public const int LarguraArena = 150, AlturaAr = 80, Chao = 25;
        public override int Width => LarguraArena + Margem * 2;
        public override int Height => Margem + AlturaAr + Chao + Margem;
        private static int Superficie => Margem + AlturaAr;

        protected override int TipoDoChefe => ModContent.NPCType<RaidSilvanna>();
        protected override int IndiceDaRaid => 0;

        public override Rectangle Arena => new((Margem + 1) * 16, (Superficie - AlturaAr + 2) * 16, (LarguraArena - 2) * 16, (AlturaAr - 2) * 16);

        protected override void Gerar()
        {
            PrepararMundo(Superficie);
            Preencher(0, Superficie, Main.maxTilesX, Main.maxTilesY, TileID.SnowBlock);
            // umas placas de gelo no chão, só para dar textura
            for (int x = Margem; x < Main.maxTilesX - Margem; x += 9)
                Preencher(x, Superficie + 1, x + 4, Superficie + 3, TileID.IceBlock);
            Enquadrar();
        }
    }

    /// <summary>
    /// Raid do Astharoth. Tudo no MESMO mundo, lado a lado:
    ///   [arena de fora 150] [parede 20] [ponte do Éter 200] [arena do Éter 150]
    /// O Éter é montado/desmontado durante a luta pela EterEstrutura, igual antes — só que aqui dentro.
    /// </summary>
    public class ArenaDoAstharoth : MundoDeRaid
    {
        public const int LarguraArena = 150, AlturaAr = 92, Chao = 25, Parede = 20;
        public static int EterX0 => Margem + LarguraArena + Parede;
        public static int Superficie => Margem + AlturaAr;
        public override int Width => EterX0 + EterEstrutura.ComprimentoPonte + EterEstrutura.LarguraArena + Margem;
        public override int Height => Superficie + Chao + Margem;

        protected override int TipoDoChefe => ModContent.NPCType<Astharoth>();
        protected override int IndiceDaRaid => 1;

        public override Rectangle Arena => new((Margem + 1) * 16, (Superficie - 80) * 16, (LarguraArena - 2) * 16, 80 * 16);

        protected override void Gerar()
        {
            PrepararMundo(Superficie);
            Main.spawnTileX = Margem + LarguraArena / 2;
            // Chão de pedra com grama só embaixo da arena de fora
            Preencher(0, Superficie, Margem + LarguraArena, Main.maxTilesY, TileID.Stone);
            Preencher(0, Superficie, Margem + LarguraArena, Superficie + 1, TileID.Grass);
            Preencher(Margem + LarguraArena, Superficie, Margem + LarguraArena + 4, Superficie + 6, TileID.Stone);
            // A parede que separa o mundo de fora do Éter (indestrutível)
            Preencher(Margem + LarguraArena, Margem, EterX0, Superficie, (ushort)ModContent.TileType<PonteEterea>());
            Enquadrar();
        }
    }

    /// <summary>Atalhos para saber em qual raid estamos.</summary>
    public static class Raids
    {
        public static MundoDeRaid Atual => SubworldSystem.Current as MundoDeRaid;
        public static bool DentroDeUmaRaid => SubworldSystem.Current is MundoDeRaid;

        /// <summary>Leva quem está perto de <paramref name="quem"/> para a dimensão da raid.</summary>
        public static void Entrar<T>(Player quem) where T : MundoDeRaid
        {
            if (Main.netMode == NetmodeID.SinglePlayer)
            {
                SubworldSystem.Enter<T>();
                return;
            }
            if (Main.netMode == NetmodeID.MultiplayerClient)
            {
                // Pede ao servidor: ele move todo mundo que estiver perto
                ModPacket pacote = ModContent.GetInstance<AscensaoAstral>().GetPacket();
                pacote.Write((byte)0);
                pacote.Write(SubworldSystem.GetIndex<T>());
                pacote.Write((byte)quem.whoAmI);
                pacote.Send();
            }
        }

        public static void ReceberPedido(BinaryReader r)
        {
            int indice = r.ReadInt32();
            Player quem = Main.player[r.ReadByte()];
            if (Main.netMode != NetmodeID.Server || SubworldSystem.Current is not null) return;
            foreach (Player p in Main.ActivePlayers)
                if (p.Distance(quem.Center) < 3000f)
                    MoverPorIndice(indice, p.whoAmI);
        }

        private static void MoverPorIndice(int indice, int jogador)
        {
            if (indice == SubworldSystem.GetIndex<ArenaDaSilvanna>()) SubworldSystem.MovePlayerToSubworld<ArenaDaSilvanna>(jogador);
            else if (indice == SubworldSystem.GetIndex<ArenaDoAstharoth>()) SubworldSystem.MovePlayerToSubworld<ArenaDoAstharoth>(jogador);
        }
    }
}
