using Terraria;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace AscensaoAstral.Common
{
    public enum ClasseAstral { Nenhuma, Mago, Melee, Invocador, Atirador }

    /// <summary>
    /// A fusão com o Eco Estelar (estilo Megaman NT Warrior): cada jogador escolhe uma classe uma vez
    /// e fica "fundido" com o companheiro, que comenta as vitórias contra os chefes.
    /// Os bônus aqui são só um ponto de partida para você balancear.
    /// </summary>
    public class AstralJogador : ModPlayer
    {
        public ClasseAstral Classe = ClasseAstral.Nenhuma;
        public ClasseAstral ClasseEmEscolha = ClasseAstral.Mago; // o que o botão do NPC está mostrando agora
        public bool Fundido;

        public static string Nome(ClasseAstral c) => c switch
        {
            ClasseAstral.Mago => "Mago",
            ClasseAstral.Melee => "Guerreiro",
            ClasseAstral.Invocador => "Invocador",
            ClasseAstral.Atirador => "Atirador",
            _ => "Nenhuma"
        };

        public static string Descricao(ClasseAstral c) => c switch
        {
            ClasseAstral.Mago => "O caminho das estrelas que queimam. +12% de dano mágico, +5% de crítico e mais regeneração de mana.",
            ClasseAstral.Melee => "O caminho da lâmina que corta o vazio. +12% de dano corpo a corpo, +5% de crítico e +4 de defesa.",
            ClasseAstral.Invocador => "O caminho dos ecos que obedecem. +12% de dano de invocação e +1 lacaio.",
            ClasseAstral.Atirador => "O caminho da luz que nunca erra. +12% de dano à distância e +5% de crítico.",
            _ => ""
        };

        public static ClasseAstral Proxima(ClasseAstral c) => c switch
        {
            ClasseAstral.Mago => ClasseAstral.Melee,
            ClasseAstral.Melee => ClasseAstral.Invocador,
            ClasseAstral.Invocador => ClasseAstral.Atirador,
            _ => ClasseAstral.Mago
        };

        public override void SaveData(TagCompound tag)
        {
            tag["classe"] = (int)Classe;
            tag["fundido"] = Fundido;
        }

        public override void LoadData(TagCompound tag)
        {
            Classe = (ClasseAstral)tag.GetInt("classe");
            Fundido = tag.GetBool("fundido");
        }

        public override void PostUpdateEquips()
        {
            if (!Fundido)
                return;

            switch (Classe)
            {
                case ClasseAstral.Mago:
                    Player.GetDamage(DamageClass.Magic) += 0.12f;
                    Player.GetCritChance(DamageClass.Magic) += 5;
                    Player.manaRegenBonus += 25;
                    break;
                case ClasseAstral.Melee:
                    Player.GetDamage(DamageClass.Melee) += 0.12f;
                    Player.GetCritChance(DamageClass.Melee) += 5;
                    Player.statDefense += 4;
                    break;
                case ClasseAstral.Invocador:
                    Player.GetDamage(DamageClass.Summon) += 0.12f;
                    Player.maxMinions += 1;
                    break;
                case ClasseAstral.Atirador:
                    Player.GetDamage(DamageClass.Ranged) += 0.12f;
                    Player.GetCritChance(DamageClass.Ranged) += 5;
                    break;
            }
        }
    }
}
