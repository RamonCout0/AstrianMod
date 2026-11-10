using System.IO;
using Terraria.ModLoader;

namespace AscensaoAstral
{
    public class AscensaoAstral : Mod
    {
        /// <summary>
        /// Modo de teste (itens de debug, /vitrine, linha de diagnóstico, invocação sem pré-requisito).
        /// Vem da configuração do mod (Common/Configuracao.cs) e fica DESLIGADO por padrão — nada para lembrar antes de publicar.
        /// </summary>
        public static bool DEBUG => Terraria.ModLoader.ModContent.GetInstance<Common.Configuracao>()?.ModoDeTeste ?? false;

        // Pacotes do mod: 0 = pedido para levar o grupo para a dimensão da raid; 3 = contra-ataque acertou
        public override void HandlePacket(BinaryReader reader, int whoAmI)
        {
            byte tipo = reader.ReadByte();
            if (tipo == 0)
                Common.Raid.Raids.ReceberPedido(reader);
            else if (tipo == 1) // alguém marcou/desmarcou PRONTO no menu da raid (chega no servidor)
                Common.Raid.MenuDaRaid.ReceberPronto(reader, whoAmI, false);
            else if (tipo == 2) // o servidor repassando quem está pronto
                Common.Raid.MenuDaRaid.ReceberPronto(reader, whoAmI, true);
            else if (tipo == 3) // um contra-ataque acertou o chefe (chega no servidor)
            {
                int npc = reader.ReadInt32();
                if (npc >= 0 && npc < Terraria.Main.maxNPCs && Terraria.Main.npc[npc].ModNPC is Common.Raid.RaidBoss chefe)
                    chefe.ReceberCounter(whoAmI);
            }
        }
    }
}
