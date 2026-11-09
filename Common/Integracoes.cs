using System;
using System.Collections.Generic;
using AscensaoAstral.Content.Bosses.Astharoth;
using AscensaoAstral.Content.Bosses.Silvanna;
using AscensaoAstral.Content.Items;
using Terraria.Localization;
using Terraria.ModLoader;

namespace AscensaoAstral.Common
{
    /// <summary>
    /// Registro no BOSS CHECKLIST (se estiver instalado): os chefes aparecem na lista com o lugar certo na progressão,
    /// o item de invocação e os colecionáveis. Pesos de referência do Boss Checklist: Queen Slime 7, Golem 12,
    /// Moon Lord 14 (e com o Calamity, Providence ~19).
    /// </summary>
    public class Integracoes : ModSystem
    {
        public override void PostSetupContent()
        {
            if (!ModLoader.TryGetMod("BossChecklist", out Mod bc)) return;
            try
            {
                Registrar(bc, nameof(RaidSilvanna), 7.6f, () => AstralMundo.DerrotouSilvanna, ModContent.NPCType<RaidSilvanna>(), ModContent.ItemType<ChamadoDaBruxa>(),
                    new List<int> { ModContent.ItemType<RelicariaDaSilvanna>(), ModContent.ItemType<CartolaDeArmotheus>(), ModContent.ItemType<TrofeuDaSilvanna>(), ModContent.ItemType<ChapeuDeArmotheus>() },
                    "Use o Chamado da Bruxa no bioma da neve, depois da Queen Slime. Fora da neve ela luta enfurecida.");
                Registrar(bc, nameof(Astharoth), 12.15f, () => AstralMundo.DerrotouAstharoth, ModContent.NPCType<Astharoth>(), ModContent.ItemType<LaminaErrante>(),
                    new List<int> { ModContent.ItemType<RelicariaDoAstharoth>(), ModContent.ItemType<OvoDoEter>(), ModContent.ItemType<TrofeuDoAstharoth>(), ModContent.ItemType<ElmoDoErrante>() },
                    "Use o Chamado do Errante depois do Golem para abrir a raid.");
                Registrar(bc, "InvernoEterno", Equilibrio.ComCalamity ? 19.5f : 14.5f, () => AstralMundo.DerrotouInverno, ModContent.NPCType<RaidSilvanna>(), -1,
                    new List<int> { ModContent.ItemType<CoroaDoInvernoEterno>() },
                    Equilibrio.ComCalamity ? "Fale com a Silvanna e escolha Desafiar depois da Providence." : "Fale com a Silvanna e escolha Desafiar depois do Moon Lord.");
            }
            catch (Exception e)
            {
                Mod.Logger.Warn("Boss Checklist: não consegui registrar os chefes: " + e.Message);
            }
        }

        private void Registrar(Mod bc, string nome, float peso, Func<bool> derrotado, int npc, int item, List<int> colecionaveis, string comoInvocar)
        {
            Dictionary<string, object> extra = new()
            {
                ["collectibles"] = colecionaveis,
                ["spawnInfo"] = Language.GetOrRegister(Mod.GetLocalizationKey($"BossChecklist.{nome}.Invocar"), () => comoInvocar),
            };
            if (item > 0) extra["spawnItems"] = item;
            if (nome == "InvernoEterno") extra["displayName"] = Language.GetOrRegister(Mod.GetLocalizationKey("BossChecklist.InvernoEterno.Nome"), () => "Silvanna: o Inverno Eterno");
            bc.Call("LogBoss", Mod, nome, peso, derrotado, npc, extra);
        }
    }
}
