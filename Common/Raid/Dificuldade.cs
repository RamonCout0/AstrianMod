using Terraria;
using Terraria.ModLoader;

namespace AscensaoAstral.Common.Raid
{
    /// <summary>
    /// DIFICULDADE DA RAID, no estilo MMO: cada nível LIBERA mais mecânicas (não só mais vida e dano).
    ///   Normal  (Clássico)                  — a luta base: menos ataques por fase, mecânicas grandes mais curtas
    ///   Heroico (Expert ou Revengeance)     — entra mais um ataque por fase, os cavaleiros do céu, o dano do frio…
    ///   Mítico  (Master ou Death do Calamity) — TODAS as mecânicas, no tempo cheio
    /// </summary>
    public enum NivelRaid { Normal = 0, Heroico = 1, Mitico = 2 }

    public class Dificuldade : ModSystem
    {
        private static bool calRevengeance, calDeath;

        public static NivelRaid Nivel =>
            Main.masterMode || calDeath ? NivelRaid.Mitico :
            Main.expertMode || calRevengeance ? NivelRaid.Heroico : NivelRaid.Normal;

        public static bool Heroico => Nivel >= NivelRaid.Heroico;
        public static bool Mitico => Nivel >= NivelRaid.Mitico;

        /// <summary>Um valor para cada nível.</summary>
        public static T Escolher<T>(T normal, T heroico, T mitico) => Nivel switch
        {
            NivelRaid.Mitico => mitico,
            NivelRaid.Heroico => heroico,
            _ => normal
        };

        public static string Nome => Nivel switch
        {
            NivelRaid.Mitico => calDeath ? "Mítico (Death)" : "Mítico",
            NivelRaid.Heroico => calRevengeance && !Main.expertMode ? "Heroico (Revengeance)" : "Heroico",
            _ => "Normal"
        };

        public static Microsoft.Xna.Framework.Color Cor => Nivel switch
        {
            NivelRaid.Mitico => new(255, 120, 90),
            NivelRaid.Heroico => new(190, 150, 255),
            _ => new(170, 220, 255)
        };

        public static string Descricao => Nivel switch
        {
            NivelRaid.Mitico => "Todas as mecânicas, no tempo cheio.",
            NivelRaid.Heroico => "Mais ataques por fase e mecânicas extras.",
            _ => "A luta base. Jogue no Expert ou no Master para ver mais."
        };

        // O Calamity diz se Revengeance/Death estão ligados pelo Mod.Call dele
        public override void PostUpdateWorld() => Atualizar();
        public override void PostUpdateEverything() { if (Main.netMode == Terraria.ID.NetmodeID.MultiplayerClient) Atualizar(); }

        private static int relogio;
        private static void Atualizar()
        {
            if (relogio++ % 60 != 0) return;
            calRevengeance = calDeath = false;
            if (!ModLoader.TryGetMod("CalamityMod", out Mod cal)) return;
            try
            {
                calRevengeance = cal.Call("GetDifficultyActive", "revengeance") is true;
                calDeath = cal.Call("GetDifficultyActive", "death") is true;
            }
            catch { }
        }
    }
}
