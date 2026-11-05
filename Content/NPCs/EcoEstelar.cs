using AscensaoAstral.Common;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AscensaoAstral.Content.NPCs
{
    /// <summary>
    /// O ser que caiu na cratera. Conversando com ele, o jogador escolhe a classe e se FUNDE com ele
    /// (cada jogador uma vez). Depois disso ele comenta as vitórias contra os chefes.
    /// Nome e falas são provisórios: troque pelo que a história pedir.
    /// </summary>
    public class EcoEstelar : ModNPC
    {
        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, new NPCID.Sets.NPCBestiaryDrawModifiers { Hide = true });
        }

        public override void SetDefaults()
        {
            NPC.width = 24;
            NPC.height = 50;
            NPC.friendly = true;
            NPC.dontTakeDamage = true;
            NPC.lifeMax = 250;
            NPC.aiStyle = -1;
            NPC.knockBackResist = 0f;
            NPC.noGravity = false;
        }

        public override bool CheckActive() => false;
        public override bool CanChat() => true;

        public override void AI()
        {
            NPC.velocity.X = 0f;
            NPC.TargetClosest(false);
            NPC.spriteDirection = NPC.direction;
            Lighting.AddLight(NPC.Center, 0.4f, 0.55f, 1f);

            if (Main.rand.NextBool(6))
                Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(20f, 30f), DustID.BlueTorch, -Vector2.UnitY, 0, default, 1.1f).noGravity = true;
        }

        public override string GetChat()
        {
            AstralJogador aj = Main.LocalPlayer.GetModPlayer<AstralJogador>();
            if (aj.Fundido)
                return $"Estou com você. Nossa chama é a do {AstralJogador.Nome(aj.Classe)}. O céu está mudando... sente?";

            return Main.rand.Next(3) switch
            {
                0 => "Você... consegue me ver? A Astrânia que você tirou da terra me chamou até aqui.",
                1 => "Meu corpo não aguenta este mundo sozinho. Se nos fundirmos, eu te empresto o que restou da minha força.",
                _ => "Lá em cima, Astra está se aproximando. Quanto mais forte você ficar, mais perto ela vem."
            };
        }

        public override void SetChatButtons(ref string button, ref string button2)
        {
            AstralJogador aj = Main.LocalPlayer.GetModPlayer<AstralJogador>();
            if (aj.Fundido)
                return;

            button = $"Caminho: {AstralJogador.Nome(aj.ClasseEmEscolha)}";
            button2 = "Fundir";
        }

        public override void OnChatButtonClicked(bool firstButton, ref string shopName)
        {
            AstralJogador aj = Main.LocalPlayer.GetModPlayer<AstralJogador>();
            if (aj.Fundido)
                return;

            if (firstButton)
            {
                aj.ClasseEmEscolha = AstralJogador.Proxima(aj.ClasseEmEscolha);
                Main.npcChatText = AstralJogador.Descricao(aj.ClasseEmEscolha);
                SoundEngine.PlaySound(SoundID.MenuTick);
                return;
            }

            // Fundir
            aj.Classe = aj.ClasseEmEscolha;
            aj.Fundido = true;
            Main.npcChatText = $"Então que seja. O caminho do {AstralJogador.Nome(aj.Classe)}... agora é nosso.";
            SoundEngine.PlaySound(SoundID.Item29, NPC.Center);
            Hud.Clarao(0.6f);
            for (int i = 0; i < 50; i++)
                Dust.NewDustPerfect(Main.LocalPlayer.Center, DustID.BlueTorch, Main.rand.NextVector2Circular(8f, 8f), 0, default, 1.8f).noGravity = true;
        }
    }
}
