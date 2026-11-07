using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AscensaoAstral.Common.Raid
{
    /// <summary>
    /// Durante uma raid, o mundo para de criar monstros perto de quem está lutando (senão um Wyvern ou um
    /// Mimic atrapalha a mecânica). Quando a raid começa, os inimigos comuns que já estavam perto vão embora.
    /// </summary>
    public class SemSpawnNaRaid : GlobalNPC
    {
        public override void EditSpawnRate(Player player, ref int spawnRate, ref int maxSpawns)
        {
            RaidBoss chefe = RaidBoss.Ativo;
            if (chefe is not null && chefe.JogadorNaRaid(player))
            {
                spawnRate = int.MaxValue;
                maxSpawns = 0;
            }
        }
    }

    public class LimpezaDeInimigos : ModSystem
    {
        private int ultimoChefe = -1;

        public override void PostUpdateNPCs()
        {
            if (Main.netMode == NetmodeID.MultiplayerClient)
                return;

            RaidBoss chefe = RaidBoss.Ativo;
            int atual = chefe?.NPC.whoAmI ?? -1;

            // A cada 2 s (e no começo da raid) tira os inimigos comuns de perto
            // (no mundo aberto — a Silvanna — ninguém some: só param de nascer monstros novos, como num chefe do Terraria)
            if (chefe is not null && !RaidBoss.NoMundoAberto && (atual != ultimoChefe || Main.GameUpdateCount % 120 == 0))
            {
                foreach (NPC n in Main.ActiveNPCs)
                {
                    if (n.friendly || n.townNPC || n.boss || n.ModNPC is RaidBoss || n.CountsAsACritter || n.dontTakeDamageFromHostiles && n.dontTakeDamage)
                        continue;
                    if (n.ModNPC?.Mod == Mod) // nossos ajudantes (dragão, estátuas) ficam
                        continue;
                    if (n.realLife >= 0 && Main.npc[n.realLife].boss) // pedaços de chefes de verme
                        continue;
                    if (n.Distance(chefe.CentroArena) > 2600f && !(AstralMundo.EterConstruido && EterEstrutura.Regiao.Contains(n.Center.ToPoint())))
                        continue;

                    // Some com um "puf", sem drop
                    for (int i = 0; i < 10; i++)
                        Dust.NewDust(n.position, n.width, n.height, DustID.PurpleTorch, 0f, -1f, 0, default, 1.3f);
                    n.active = false;
                    if (Main.netMode == NetmodeID.Server)
                        NetMessage.SendData(MessageID.SyncNPC, number: n.whoAmI);
                }
            }
            ultimoChefe = atual;
        }
    }
}
