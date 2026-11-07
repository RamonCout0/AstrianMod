using AscensaoAstral.Content.Bosses.Astharoth;
using AscensaoAstral.Content.Bosses.Silvanna;
using AscensaoAstral.Content.Tiles;
using AscensaoAstral.Common;
using Terraria;
using Terraria.Localization;
using Terraria.Audio;
using Terraria.ID;
using Terraria.ModLoader;

namespace AscensaoAstral.Content.Items
{
    public class Astrania : ModItem
    {
        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 100;
            ItemID.Sets.SortingPriorityMaterials[Type] = 70;
        }

        public override void SetDefaults()
        {
            Item.DefaultToPlaceableTile(ModContent.TileType<AstraniaTile>());
            Item.width = 16;
            Item.height = 16;
            Item.value = Item.sellPrice(silver: 30);
            Item.rare = ItemRarityID.LightRed;
        }
    }

    /// <summary>Base dos itens que começam uma raid.</summary>
    public abstract class InvocacaoDeRaid : ModItem
    {
        protected abstract int TipoDoChefe { get; }
        protected abstract bool PodeInvocar(Player player);
        /// <summary>Leva o grupo para a dimensão desta raid.</summary>
        protected abstract void Entrar(Player player);
        /// <summary>Qual raid o menu de entrada mostra (InfoDaRaid.Todas).</summary>
        protected abstract int IndiceDaRaid { get; }

        public override void SetStaticDefaults() => ItemID.Sets.SortingPriorityBossSpawns[Type] = 12;

        public override void SetDefaults()
        {
            Item.width = 32;
            Item.height = 32;
            Item.maxStack = 20;
            Item.rare = ItemRarityID.Pink;
            Item.useAnimation = 30;
            Item.useTime = 30;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.consumable = false; // base de testes: não gasta
        }

        // Com DEBUG ligado, os pré-requisitos (chefe derrotado, bioma) são ignorados para facilitar os testes
        // Só funciona no mundo principal: lá dentro o chefe aparece sozinho
        public override bool CanUseItem(Player player) => Common.Raid.MenuDaRaid.Aberta is null && !Common.Raid.Raids.DentroDeUmaRaid && (AscensaoAstral.DEBUG || PodeInvocar(player));

        public override bool? UseItem(Player player)
        {
            if (player.whoAmI == Main.myPlayer)
            {
                // Abre o MENU DE ENTRADA (retrato, arena, grupo, Pronto/Iniciar); o Iniciar leva o grupo
                Common.Raid.MenuDaRaid.Abrir(IndiceDaRaid);
            }
            return true;
        }
    }

    /// <summary>
    /// Chefe da Silvanna: depois da Queen Slime, no bioma da neve. NÃO usa dimensão: como um chefe do Terraria,
    /// ela aparece aqui mesmo, no mundo (fora da neve ela luta enfurecida).
    /// </summary>
    public class ChamadoDaBruxa : InvocacaoDeRaid
    {
        protected override int TipoDoChefe => ModContent.NPCType<RaidSilvanna>();
        protected override bool PodeInvocar(Player player) => NPC.downedQueenSlime && player.ZoneSnow;
        protected override void Entrar(Player player) { }
        protected override int IndiceDaRaid => 0;

        public override bool CanUseItem(Player player) => !NPC.AnyNPCs(TipoDoChefe) && Common.Raid.RaidBoss.Ativo is null && !Common.Raid.Raids.DentroDeUmaRaid
            && (AscensaoAstral.DEBUG || PodeInvocar(player));

        public override bool? UseItem(Player player)
        {
            if (player.whoAmI == Main.myPlayer)
            {
                SoundEngine.PlaySound(SoundID.Roar, player.position);
                if (Main.netMode != NetmodeID.MultiplayerClient)
                    NPC.SpawnOnPlayer(player.whoAmI, TipoDoChefe);
                else
                    NetMessage.SendData(MessageID.SpawnBossUseLicenseStartEvent, number: player.whoAmI, number2: TipoDoChefe);
            }
            return true;
        }

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient<Astrania>(8)
                .AddIngredient(ItemID.IceBlock, 20)
                .AddIngredient(ItemID.CrystalShard, 5)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }

    /// <summary>Raid do Astharoth: depois do Golem.</summary>
    public class LaminaErrante : InvocacaoDeRaid
    {
        protected override int TipoDoChefe => ModContent.NPCType<Astharoth>();
        protected override bool PodeInvocar(Player player) => NPC.downedGolemBoss;
        protected override void Entrar(Player player) => Common.Raid.Raids.Entrar<Common.Raid.ArenaDoAstharoth>(player);
        protected override int IndiceDaRaid => 1;

        public override void AddRecipes()
        {
            CreateRecipe()
                .AddIngredient<Astrania>(15)
                .AddIngredient(ItemID.BeetleHusk, 3)
                .AddIngredient(ItemID.Ectoplasm, 8)
                .AddTile(TileID.MythrilAnvil)
                .Register();
        }
    }
}

namespace AscensaoAstral.Content.Items
{
    /// <summary>
    /// SÓ PARA TESTES (aparece quando AscensaoAstral.DEBUG = true). Receita: 1 bloco de terra, sem estação.
    /// Cada uso faz o próximo passo da história:
    ///   1º uso: libera a Astrânia (como se você tivesse quebrado um altar no Hardmode)
    ///   2º uso: dispara o impacto ao seu lado (cratera + Eco Estelar)
    ///   depois: teleporta você até a cratera
    /// </summary>
    public class FerramentaDeTeste : ModItem
    {
        public override string Texture => "AscensaoAstral/Content/Items/Astrania";

        public override bool IsLoadingEnabled(Mod mod) => AscensaoAstral.DEBUG;

        public override void SetDefaults()
        {
            Item.width = 16;
            Item.height = 16;
            Item.useAnimation = 30;
            Item.useTime = 30;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.rare = ItemRarityID.Expert;
            Item.color = new Microsoft.Xna.Framework.Color(255, 120, 255);
        }

        public override bool? UseItem(Player player)
        {
            if (player.whoAmI != Main.myPlayer)
                return true;

            if (!AstralMundo.AstraniaLiberada || !AstralMundo.ImpactoAconteceu)
            {
                if (Main.netMode == NetmodeID.MultiplayerClient)
                {
                    Main.NewText("A Ferramenta de Teste só faz os eventos em single player.", 255, 120, 120);
                    return true;
                }
                if (!AstralMundo.AstraniaLiberada)
                    AstralMundo.LiberarAstraniaParaTeste();
                else
                    AstralMundo.DispararImpacto((int)(player.Center.X / 16f), (int)(player.Center.Y / 16f));
                return true;
            }

            Common.Raid.RaidJogador.Teleportar(player, new Microsoft.Xna.Framework.Vector2(AstralMundo.Cratera.X * 16f, (AstralMundo.Cratera.Y + 2) * 16f));
            Main.NewText("Levado até a cratera.", 255, 230, 160);
            return true;
        }

        public override void AddRecipes()
        {
            CreateRecipe().AddIngredient(ItemID.DirtBlock).Register();
        }
    }

    /// <summary>
    /// SÓ PARA TESTES (DEBUG): controla o Bot de Teste, um segundo "jogador" para testar as mecânicas de grupo sozinho.
    /// Clique: coloca o bot no cursor · Botão direito: o bot passa a te seguir (ou para).
    /// </summary>
    public class ControleDoBot : ModItem
    {
        public override string Texture => "AscensaoAstral/Content/Items/LaminaErrante";
        public override bool IsLoadingEnabled(Mod mod) => AscensaoAstral.DEBUG;

        public override void SetDefaults()
        {
            Item.width = 16; Item.height = 16;
            Item.useAnimation = 15; Item.useTime = 15;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.rare = ItemRarityID.Expert;
            Item.color = new Microsoft.Xna.Framework.Color(140, 200, 255);
        }

        public override bool AltFunctionUse(Player player) => true;

        public override bool? UseItem(Player player)
        {
            if (player.whoAmI != Main.myPlayer) return true;
            if (player.altFunctionUse == 2)
            {
                if (Common.Raid.BotDeTeste.Bot is null) Common.Raid.BotDeTeste.Colocar(player.Center + new Microsoft.Xna.Framework.Vector2(-110f, 0f));
                Common.Raid.BotDeTeste.Seguindo = !Common.Raid.BotDeTeste.Seguindo;
                Main.NewText(Common.Raid.BotDeTeste.Seguindo ? "Bot: te seguindo." : "Bot: parado aqui.", 180, 220, 255);
            }
            else
                Common.Raid.BotDeTeste.Colocar(Main.MouseWorld);
            return true;
        }

        public override void AddRecipes() => CreateRecipe().AddIngredient(ItemID.DirtBlock, 2).Register();
    }
}
