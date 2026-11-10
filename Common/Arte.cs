using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.ModLoader;

namespace AscensaoAstral.Common
{
    /// <summary>
    /// ARTE PROVISÓRIA × ARTE DE VERDADE, sem mexer no código.
    ///
    /// Quase tudo no mod nasceu com arte emprestada do Terraria (tingida) ou desenhada em código. Para trocar
    /// qualquer uma, basta soltar um PNG com o NOME CERTO em <c>Assets/Sprites/</c> e recompilar: o mod acha o
    /// arquivo sozinho, passa a usar ele e desliga a tinta. Sem o arquivo, continua no provisório.
    /// A lista de nomes e tamanhos está em Assets/Sprites/LEIA-ME.md.
    /// </summary>
    public static class Arte
    {
        public const string Pasta = "Assets/Sprites/";

        private static Mod Meu => ModContent.GetInstance<AscensaoAstral>();

        /// <summary>Existe um sprite próprio com este nome em Assets/Sprites?</summary>
        public static bool Tem(Mod mod, string nome) => mod is not null && mod.HasAsset(Pasta + nome);
        public static bool Tem(string nome) => Tem(Meu, nome);

        /// <summary>Textura de item: o sprite próprio (nome da classe) ou, se não houver, o item do Terraria indicado.</summary>
        public static string Item(ModItem item, int itemDoTerraria)
            => Tem(item.Mod, item.Name) ? $"{item.Mod.Name}/{Pasta}{item.Name}" : "Terraria/Images/Item_" + itemDoTerraria;

        /// <summary>Ícone de buff: o próprio (nome da classe) ou o do buff do Terraria indicado.</summary>
        public static string Buff(ModBuff buff, int buffDoTerraria)
            => Tem(buff.Mod, buff.Name) ? $"{buff.Mod.Name}/{Pasta}{buff.Name}" : "Terraria/Images/Buff_" + buffDoTerraria;

        /// <summary>A tinta do provisório: some sozinha quando o item ganha sprite próprio.</summary>
        public static Color Tinta(ModItem item, Color tintaDoProvisorio) => Tem(item.Mod, item.Name) ? default : tintaDoProvisorio;

        /// <summary>Textura desenhada em código (pets, lacaios, relíquias...): a própria, ou a reserva.</summary>
        public static Texture2D Textura(string nome, string reserva)
            => ModContent.Request<Texture2D>(Tem(nome) ? $"AscensaoAstral/{Pasta}{nome}" : reserva).Value;
    }
}
