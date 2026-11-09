using System;
using AscensaoAstral.Common;
using AscensaoAstral.Common.Raid;
using AscensaoAstral.Content.Bosses.Silvanna;
using AscensaoAstral.Content.Items;
using AscensaoAstral.Content.Projectiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AscensaoAstral.Content.NPCs
{
    /// <summary>
    /// SILVANNA, DEPOIS DA PRIMEIRA LUTA. Ela não era inimiga: depois de testar o jogador, se muda para perto do ponto
    /// de partida do mundo. Vende o que uma bruxa do gelo venderia e, depois do Moon Lord (com o Calamity, depois da
    /// Providence), aceita o DESAFIO: a revanche "Inverno Eterno", na dimensão dela, com o Armotheus sem se segurar.
    /// </summary>
    public class SilvannaNPC : ModNPC
    {
        public override string Texture => "AscensaoAstral/Assets/Sheets/Silvanna";
        private static readonly Color Cor = new(120, 210, 230);

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, new NPCID.Sets.NPCBestiaryDrawModifiers { Hide = true });
        }

        public override void SetDefaults()
        {
            NPC.width = 28;
            NPC.height = 54;
            NPC.friendly = true;
            NPC.dontTakeDamage = true;
            NPC.lifeMax = 250;
            NPC.aiStyle = -1;
            NPC.knockBackResist = 0f;
            NPC.noGravity = true;
        }

        public override bool CheckActive() => false;
        public override bool CanChat() => true;

        /// <summary>A revanche já pode? Moon Lord no Terraria puro; Providence com o Calamity.</summary>
        public static bool RevancheLiberada
        {
            get
            {
                if (AscensaoAstral.DEBUG && Main.LocalPlayer.HeldItem.type == ModContent.ItemType<FerramentaDeTeste>()) return true;
                if (!NPC.downedMoonlord) return false;
                if (!ModLoader.TryGetMod("CalamityMod", out Mod cal)) return true;
                try { return cal.Call("GetBossDowned", "providence") is true; }
                catch { return true; }
            }
        }

        public override void AI()
        {
            // flutua devagar no lugar (ela é bruxa), virada para quem estiver perto
            NPC.velocity = new Vector2(0f, MathF.Sin(Main.GlobalTimeWrappedHourly * 1.5f) * 0.15f);
            NPC.TargetClosest(false);
            NPC.spriteDirection = NPC.direction;
            Lighting.AddLight(NPC.Center, 0.25f, 0.45f, 0.6f);
            if (Main.rand.NextBool(10))
                Dust.NewDustPerfect(NPC.Bottom + Main.rand.NextVector2Circular(14f, 4f), DustID.Snow, new Vector2(0f, -0.5f), 0, default, 0.9f).noGravity = true;
        }

        public override string GetChat()
        {
            if (AstralMundo.DerrotouInverno)
                return Main.rand.Next(2) == 0 ? "Armotheus está emburrado desde aquela luta. Ele vai superar. Talvez." : "Se precisar do inverno, chame. Ele agora gosta de você.";
            if (RevancheLiberada)
                return "Você derrubou a Lua. Agora ele não tem mais desculpa para se segurar... quer ver o inverno de verdade?";
            return Main.rand.Next(3) switch
            {
                0 => "Na nossa luta ele estava se segurando, sabia? Volte quando tiver vencido algo maior que a Lua.",
                1 => "A Astrânia está chamando coisas lá de cima. O Astharoth é uma delas. Ele não testa: ele corta.",
                _ => "Armotheus diz que gostou de você. Ele não diz isso de ninguém."
            };
        }

        public override void SetChatButtons(ref string button, ref string button2)
        {
            button = "Loja";
            if (RevancheLiberada) button2 = AstralMundo.DerrotouInverno ? "Desafiar de novo" : "Desafiar";
        }

        public override void OnChatButtonClicked(bool firstButton, ref string shopName)
        {
            if (firstButton) { shopName = "Loja"; return; }
            if (!RevancheLiberada) return;
            Main.npcChatText = "Então venha. Armotheus está esperando faz tempo.";
            SoundEngine.PlaySound(SoundID.Item30 with { Pitch = -0.3f }, NPC.Center);
            MoradaDaSilvanna.DesafioPendente = true; // troca de mundo no próximo update (não no meio do desenho do chat)
        }

        public override void AddShops()
        {
            new NPCShop(Type, "Loja")
                .Add(ItemID.Snowball) // munição do Lançador de Avalanche
                .Add<ChapeuDeArmotheus>()
                .Add(ItemID.EskimoHood).Add(ItemID.EskimoCoat).Add(ItemID.EskimoPants)
                .Add(ItemID.IceTorch)
                .Add<GeloEterno>(new Condition("Depois do Inverno Eterno", () => AstralMundo.DerrotouInverno))
                .Register();
        }

        public override bool PreDraw(SpriteBatch sb, Vector2 screenPos, Color drawColor)
        {
            Texture2D tex = Terraria.GameContent.TextureAssets.Npc[Type].Value;
            float esc = 0.62f;
            Vector2 pe = NPC.Bottom - screenPos + new Vector2(0f, 2f);
            Desenho.Brilho(NPC.Center, new Color(80, 160, 255), 0.7f);
            sb.Draw(tex, pe, null, Color.Lerp(drawColor, Color.White, 0.4f), 0f, new Vector2(tex.Width / 2f, tex.Height), esc,
                NPC.spriteDirection > 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
            return false;
        }
    }

    /// <summary>Depois da primeira vitória, a Silvanna aparece perto do ponto de partida do mundo (e volta se sumir).</summary>
    public class MoradaDaSilvanna : ModSystem
    {
        private static int relogio;
        public static bool DesafioPendente;

        public override void PostUpdateEverything()
        {
            if (!DesafioPendente || Main.dedServ) return;
            DesafioPendente = false;
            Main.LocalPlayer.SetTalkNPC(-1);
            Main.npcChatText = "";
            Raids.Entrar<ArenaDaSilvanna>(Main.LocalPlayer);
        }

        public override void PostUpdateWorld()
        {
            if (++relogio % 300 != 0 || Raids.DentroDeUmaRaid || !AstralMundo.DerrotouSilvanna) return;
            int tipo = ModContent.NPCType<SilvannaNPC>();
            if (NPC.AnyNPCs(tipo) || RaidBoss.Ativo is not null) return;
            int x = (Main.spawnTileX + 8) * 16;
            int y = (int)RaidBoss.ChaoEm(x, (Main.spawnTileY - 30) * 16, 80, Main.spawnTileY * 16) - 40;
            NPC.NewNPC(new EntitySource_WorldEvent(), x, y, tipo);
            AstralMundo.Mensagem("Silvanna se mudou para perto do ponto de partida do mundo.", new Color(120, 210, 230));
        }
    }
}

namespace AscensaoAstral.Content.Items
{
    // ═══════════════ LOOT DA REVANCHE (Inverno Eterno, pós-Moon Lord) ═══════════════

    /// <summary>Material de fim de jogo: o frio que sobrou do Inverno Eterno.</summary>
    public class EssenciaDoInverno : ModItem
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.FragmentStardust;

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 25;
            ItemID.Sets.ItemNoGravity[Type] = true;
        }

        public override void SetDefaults()
        {
            Item.color = new Color(170, 230, 255);
            Item.width = 22; Item.height = 22;
            Item.maxStack = Item.CommonMaxStack;
            Item.value = Item.sellPrice(gold: 2);
            Item.rare = ItemRarityID.Red;
        }

        public override void PostUpdate() => Lighting.AddLight(Item.Center, 0.4f, 0.7f, 1f);
    }

    /// <summary>
    /// Coroa do Inverno Eterno (acessório de fim de jogo): +10% de dano, +5% de crítico, imune ao frio, e a cada
    /// 2 s uma lasca de gelo sai de você em direção ao inimigo mais perto.
    /// </summary>
    public class CoroaDoInvernoEterno : ModItem
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.GoldCrown;

        public override void SetDefaults()
        {
            Item.color = new Color(170, 230, 255);
            Item.width = 28; Item.height = 24;
            Item.accessory = true;
            Item.rare = ItemRarityID.Red;
            Item.value = Item.sellPrice(gold: 20);
        }

        public override void UpdateAccessory(Player player, bool hideVisual)
        {
            player.GetDamage(DamageClass.Generic) += 0.10f;
            player.GetCritChance(DamageClass.Generic) += 5f;
            player.buffImmune[BuffID.Chilled] = true;
            player.buffImmune[BuffID.Frozen] = true;
            player.buffImmune[BuffID.Frostburn] = true;
            player.buffImmune[BuffID.Frostburn2] = true;
            player.GetModPlayer<CoroaJogador>().Ativa = true;
        }
    }

    public class CoroaJogador : ModPlayer
    {
        public bool Ativa;
        private int relogio;

        public override void ResetEffects() => Ativa = false;

        public override void PostUpdateEquips()
        {
            if (!Ativa || Player.whoAmI != Main.myPlayer || ++relogio % 120 != 0) return;
            NPC alvo = null;
            float d = 700f;
            foreach (NPC n in Main.ActiveNPCs)
                if (n.CanBeChasedBy() && n.Distance(Player.Center) < d) { d = n.Distance(Player.Center); alvo = n; }
            if (alvo is null) return;
            int dano = (int)Player.GetTotalDamage(DamageClass.Generic).ApplyTo(Equilibrio.ComCalamity ? 260 : 160);
            int i = Projectile.NewProjectile(Player.GetSource_Accessory(Player.armor[0]), Player.Center, (alvo.Center - Player.Center).SafeNormalize(Vector2.UnitY) * 14f,
                ModContent.ProjectileType<LascaAmiga>(), dano, 2f, Player.whoAmI);
            Main.projectile[i].DamageType = DamageClass.Generic;
            Main.projectile[i].tileCollide = false;
            SoundEngine.PlaySound(SoundID.Item28 with { Volume = 0.4f, Pitch = 0.5f }, Player.Center);
        }
    }
}
