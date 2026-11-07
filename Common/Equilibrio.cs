using System;
using System.Collections.Generic;
using AscensaoAstral.Common.Raid;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AscensaoAstral.Common
{
    /// <summary>Uma arma do mod que copia o dano de uma arma de referência da mesma época da progressão.</summary>
    public interface IArmaComReferencia
    {
        /// <summary>Arma do Terraria puro da mesma época (usada sem o Calamity).</summary>
        int ReferenciaVanilla { get; }
        /// <summary>Nome interno da arma do Calamity da mesma época (ex.: "Avalanche"). Vazio = usa a vanilla.</summary>
        string ReferenciaCalamity { get; }
        /// <summary>Quanto do DPS da referência ela entrega (1 = igual; menos se ela tem efeito extra, como espinhos).</summary>
        float Fator { get; }
    }

    /// <summary>
    /// PERFIL DE EQUILÍBRIO. O problema de jogar com o Calamity é que ele sobe tudo de nível e os outros mods
    /// ficam inúteis. Então o Ascensão Astral SE AJUSTA:
    ///   - Sem o Calamity: números do Terraria (régua: Queen Slime / Golem / Empress).
    ///   - Com o Calamity: a vida dos chefes segue os chefes dele na mesma época (Cryogen, Plaguebringer),
    ///     sobe no Revengeance e no Death, e os chefes ganham redução de dano como os dele.
    ///   - As armas não têm dano fixo: cada uma LÊ o dano de uma arma de referência da mesma época
    ///     (do Calamity, se ele estiver instalado) e ajusta pelo próprio tempo de uso. Se o Calamity mudar
    ///     os números numa atualização, as nossas armas acompanham sozinhas.
    /// </summary>
    public class Equilibrio : ModSystem
    {
        public static bool ComCalamity { get; private set; }

        /// <summary>Vida extra sobre o Expert com o Calamity (o Plaguebringer sobe 1,5× no Rev e 1,9× no Death).</summary>
        public static float MultVida => !ComCalamity ? 1f : Dificuldade.CalDeath ? 1.9f : Dificuldade.CalRevengeance ? 1.5f : 1f;

        /// <summary>Redução de dano dos chefes (os do Calamity têm; sem ela as armas dele derretem a luta).</summary>
        public static float ReducaoDeDano => !ComCalamity ? 0f : Dificuldade.CalDeath ? 0.15f : Dificuldade.CalRevengeance ? 0.08f : 0f;

        private static readonly Dictionary<int, int> danoCalculado = new();

        public override void Load() => ComCalamity = ModLoader.HasMod("CalamityMod");
        public override void Unload() => danoCalculado.Clear();

        /// <summary>Chame no fim do SetDefaults da arma: troca o dano provisório pelo calculado pela referência.</summary>
        public static int Dano(int tipo, int provisorio) => danoCalculado.TryGetValue(tipo, out int d) ? d : provisorio;

        public override void PostSetupContent()
        {
            danoCalculado.Clear();
            foreach (ModItem mi in Mod.GetContent<ModItem>())
            {
                if (mi is not IArmaComReferencia r) continue;
                int tipoRef = r.ReferenciaVanilla;
                if (ComCalamity && !string.IsNullOrEmpty(r.ReferenciaCalamity) && ModContent.TryFind("CalamityMod", r.ReferenciaCalamity, out ModItem cal))
                    tipoRef = cal.Type;
                try
                {
                    Item refe = new(tipoRef);
                    Item nossa = new(mi.Type);
                    if (refe.damage <= 0 || refe.useTime <= 0) continue;
                    // mesmo DPS que a referência, ajustado pelo tempo de uso de cada uma
                    int dano = (int)Math.Round(refe.damage * r.Fator * nossa.useTime / (float)refe.useTime);
                    danoCalculado[mi.Type] = Math.Max(1, dano);
                    if (ContentSamples.ItemsByType.TryGetValue(mi.Type, out Item amostra))
                        amostra.SetDefaults(mi.Type);
                }
                catch (Exception e)
                {
                    Mod.Logger.Warn($"Equilíbrio: não consegui ler a referência de {mi.Name}: {e.Message}");
                }
            }
        }
    }
}
