using System;
using AscensaoAstral.Content.Bosses.Astharoth;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace AscensaoAstral.Common.Raid
{
    /// <summary>
    /// BOT DE TESTE (só com DEBUG ligado, só em single player): um "segundo jogador" de mentira para testar as
    /// mecânicas de grupo sozinho. Ele ocupa uma vaga de jogador de verdade (Main.player), então o chefe CONTA com ele:
    /// a Separação das Almas sorteia metade para o Éter, os símbolos olham quem está em cada plataforma, o dragão
    /// pode escolher ele como alvo, o modo solo deixa de valer etc.
    ///
    /// Limites: os golpes não machucam o bot (no Terraria só o jogador local leva dano em single player) e ele não
    /// anda sozinho — você o leva com o Controle do Bot (clique = coloca no cursor, botão direito = me seguir/parar).
    /// Quando ele está no Éter na corrida da ponte, ele é levado junto com você.
    /// </summary>
    public class BotDeTeste : ModSystem
    {
        public static int Indice = -1;
        public static bool Seguindo;
        public static Vector2 Lugar;

        public static Player Bot => Indice >= 0 && Main.player[Indice].active ? Main.player[Indice] : null;

        public static void Colocar(Vector2 onde)
        {
            if (Main.netMode != NetmodeID.SinglePlayer) { Main.NewText("O bot de teste só funciona em single player.", 255, 120, 120); return; }
            if (Bot is null) Criar();
            if (Bot is null) return;
            Lugar = onde;
            Seguindo = false;
            Bot.Center = onde;
        }

        private static void Criar()
        {
            int i = -1;
            for (int k = 0; k < Main.maxPlayers; k++)
                if (k != Main.myPlayer && !Main.player[k].active) { i = k; break; }
            if (i < 0) return;

            Player eu = Main.LocalPlayer;
            Player b = new();
            b.name = "Bot de Teste";
            b.whoAmI = i;
            b.hair = eu.hair; b.hairColor = eu.hairColor; b.skinColor = Color.LightSkyBlue; b.eyeColor = eu.eyeColor;
            b.shirtColor = new Color(120, 90, 220); b.underShirtColor = eu.underShirtColor; b.pantsColor = new Color(60, 50, 90); b.shoeColor = eu.shoeColor;
            b.skinVariant = eu.skinVariant;
            b.statLifeMax = 400; b.statLife = 400;
            b.active = true;
            Main.player[i] = b;
            Indice = i;
            Main.NewText("Bot de Teste criado (o chefe agora conta 2 jogadores). Clique = mover · Botão direito = seguir/parar.", 180, 220, 255);
        }

        public static void Remover()
        {
            if (Indice >= 0 && Indice < Main.maxPlayers && Indice != Main.myPlayer)
                Main.player[Indice].active = false;
            Indice = -1;
            Seguindo = false;
        }

        public override void OnWorldUnload() => Remover();

        public override void PostUpdatePlayers()
        {
            Player b = Bot;
            if (b is null) return;
            if (!AscensaoAstral.DEBUG || Main.netMode != NetmodeID.SinglePlayer) { Remover(); return; }

            Player eu = Main.LocalPlayer;
            if (Seguindo)
                Lugar = Vector2.Lerp(Lugar, eu.Center + new Vector2(-eu.direction * 110f, 0f), 0.15f);

            // Na corrida da ponte do Astharoth, quem foi sorteado para o Éter é levado para lá
            if (RaidBoss.Ativo is Astharoth a && a.BotFoiParaOEter(b) && !EterEstrutura.Dentro(b))
                Lugar = EterEstrutura.InicioDaPonte + new Vector2(60f, 0f);

            // Ele fica onde você mandou (sem física, sem morrer)
            b.Center = Lugar;
            b.velocity = Vector2.Zero;
            b.dead = false;
            b.statLife = b.statLifeMax;
            b.direction = eu.Center.X > b.Center.X ? 1 : -1;
            if (Main.rand.NextBool(6))
                Dust.NewDustPerfect(b.Top + new Vector2(0f, -10f), DustID.PurpleTorch, -Vector2.UnitY, 0, default, 0.8f).noGravity = true;
        }
    }
}
