using Terraria;
using Terraria.ModLoader;

namespace AscensaoAstral.Content.Buffs
{
    /// <summary>Raid da Silvanna, mecânica final: só quem tem o escudo sobrevive à rajada solar.</summary>
    public class EscudoAstral : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;
            Main.debuff[Type] = false;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            Lighting.AddLight(player.Center, 0.4f, 0.7f, 1f);
        }
    }

    /// <summary>Raid do Astharoth: a barreira das 4 orbes protege do mergulho do Arcanjo.</summary>
    public class BarreiraEterea : ModBuff
    {
        public override void SetStaticDefaults()
        {
            Main.buffNoSave[Type] = true;
            Main.debuff[Type] = false;
        }

        public override void Update(Player player, ref int buffIndex)
        {
            Lighting.AddLight(player.Center, 1f, 0.85f, 0.4f);
        }
    }
}
