using System.IO;
using Terraria.ModLoader;

namespace AscensaoAstral
{
    public class AscensaoAstral : Mod
    {
        /// <summary>Mostra a linha verde de diagnóstico dos chefes no HUD. Desligue para lançar o mod.</summary>
        public const bool DEBUG = true;

        // Pacotes do mod: 0 = pedido para levar o grupo para a dimensão da raid
        public override void HandlePacket(BinaryReader reader, int whoAmI)
        {
            byte tipo = reader.ReadByte();
            if (tipo == 0)
                Common.Raid.Raids.ReceberPedido(reader);
            else if (tipo == 1) // alguém marcou/desmarcou PRONTO no menu da raid (chega no servidor)
                Common.Raid.MenuDaRaid.ReceberPronto(reader, whoAmI, false);
            else if (tipo == 2) // o servidor repassando quem está pronto
                Common.Raid.MenuDaRaid.ReceberPronto(reader, whoAmI, true);
        }
    }
}
