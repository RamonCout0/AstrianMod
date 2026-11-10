using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace AscensaoAstral.Common
{
    /// <summary>
    /// Configuração do mod (Workshop → Configuração de Mods → Ascensão Astral).
    /// O MODO DE TESTE fica DESLIGADO por padrão: quem baixar o mod joga a versão de verdade. Para desenvolver,
    /// ligue aqui uma vez (o jogo recarrega os mods) — a escolha fica salva no seu PC.
    /// </summary>
    public class Configuracao : ModConfig
    {
        public override ConfigScope Mode => ConfigScope.ServerSide;

        /// <summary>
        /// Ligado: aparecem a Ferramenta de Teste, o Controle do Bot e o comando /vitrine; os itens de invocação não
        /// são gastos e ignoram os pré-requisitos (chefe derrotado, bioma); e a linha verde de diagnóstico aparece na tela.
        /// </summary>
        [DefaultValue(false)]
        [ReloadRequired]
        public bool ModoDeTeste;
    }
}
