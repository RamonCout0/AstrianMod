using System;
using System.IO;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AscensaoAstral.Common
{
    /// <summary>
    /// A Luminance só usa shaders JÁ COMPILADOS (.xnb). Ela compila um .fx quando percebe que o arquivo mudou
    /// enquanto o jogo está aberto (um "vigia" na pasta da ModSources). Num PC novo, ou depois de trocar os .fx
    /// pelo zip, não existe .xnb nenhum — e todos os shaders ficam "FALHOU".
    ///
    /// Este sistema resolve sozinho: ao entrar num mundo, ele "toca" (atualiza a data de) cada .fx que está sem
    /// .xnb ou com .xnb mais velho. O vigia da Luminance vê a mudança e compila. Depois disso, dê Build + Reload
    /// uma vez para os .xnb irem para dentro do mod.
    /// </summary>
    public class CompiladorDeShaders : ModSystem
    {
        private static bool feito;

        public override void OnWorldLoad()
        {
            if (feito || Main.dedServ || Main.netMode != NetmodeID.SinglePlayer)
                return;
            feito = true;

            string raiz = Path.Combine(Main.SavePath, "ModSources", Mod.Name, "Assets", "AutoloadedEffects");
            if (!Directory.Exists(raiz))
                return; // rodando de um .tmod do Workshop: os .xnb já vêm dentro dele

            int tocados = 0;
            foreach (string fx in Directory.GetFiles(raiz, "*.fx", SearchOption.AllDirectories))
            {
                if (fx.Contains(Path.DirectorySeparatorChar + "Compiler" + Path.DirectorySeparatorChar))
                    continue;
                string xnb = Path.ChangeExtension(fx, ".xnb");
                bool precisa = !File.Exists(xnb) || File.GetLastWriteTimeUtc(xnb) < File.GetLastWriteTimeUtc(fx);
                if (!precisa) continue;
                try
                {
                    File.SetLastWriteTimeUtc(fx, DateTime.UtcNow);
                    tocados++;
                }
                catch (Exception e)
                {
                    Mod.Logger.Warn($"Não consegui pedir a compilação de {Path.GetFileName(fx)}: {e.Message}");
                }
            }

            if (tocados > 0)
            {
                Mod.Logger.Info($"Pedi à Luminance para compilar {tocados} shader(s).");
                Main.NewText($"[Ascensão Astral] Compilando {tocados} shader(s)... quando terminar, dê Build + Reload uma vez.", 180, 220, 255);
            }
        }
    }
}
