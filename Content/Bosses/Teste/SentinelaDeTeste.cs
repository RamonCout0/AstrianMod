using System.IO;
using AscensaoAstral.Common;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace AscensaoAstral.Content.Bosses.Teste
{
    /// <summary>
    /// SENTINELA DE TESTE: o boss do guia. Serve para você aprender e testar (Terraria puro e Calamity) antes de
    /// fazer os chefes de verdade. Todo número de balanceamento mora na classe V, logo abaixo.
    /// Sprites provisórios do Terraria; para trocar, sobrescreva Texture.
    /// </summary>
    public class SentinelaDeTeste : ModNPC
    {
        // ═════════ VARIÁVEIS: balanceie SOMENTE aqui ═════════
        // Tempos em ticks (60 ticks = 1 segundo). Danos são do modo Clássico: o jogo escala sozinho por dificuldade.
        static class V
        {
            // Corpo
            public const int Vida = 4500;
            public const int VidaComCalamity = 6000;   // no nível do Hive Mind / Desert Scourge (4.000 a 5.000)
            public const int DanoContato = 28;
            public const int Defesa = 8;
            public const float Velocidade = 8f;
            public const float Suavidade = 0.12f;

            // Ritmo
            public const int PausaEntreAtaques = 50;
            public const int Telegrafo = 40;

            // Ataque 1: rajada em leque
            public const int RajadaProjeteis = 5;
            public const float RajadaAbertura = 40f;
            public const float RajadaVelocidade = 9f;
            public const int RajadaDano = 18;
            public const int RajadaRepeticoes = 3;
            public const int RajadaIntervalo = 25;

            // Ataque 2: investida
            public const int InvestidaRepeticoes = 3;
            public const float InvestidaVelocidade = 17f;
            public const int InvestidaDuracao = 28;

            // Ataque 3: chuva
            public const int ChuvaProjeteis = 10;
            public const int ChuvaIntervalo = 6;
            public const int ChuvaDano = 16;
            public const float ChuvaVelocidade = 6f;
            public const int ChuvaLargura = 900;

            // Fase 2
            public const float Fase2EmVida = 0.5f;
            public const float Fase2DanoMult = 1.25f;
            public const float Fase2PausaMult = 0.7f;
            public const int Fase2ProjetilExtra = 2;

            // Loot
            public static readonly int[] ArmasDeTeste = { ItemID.IceBlade, ItemID.Boomstick, ItemID.MagicMissile };
            public const int BarrasMin = 15, BarrasMax = 25;
        }
        // ═════════ FIM DAS VARIÁVEIS ═════════

        enum Estado { Perseguir = 0, Rajada = 1, Investida = 2, Chuva = 3 }

        Estado Atual => (Estado)(int)NPC.ai[0];
        ref float Timer => ref NPC.ai[1];
        ref float Contador => ref NPC.ai[2];
        bool Fase2 => NPC.ai[3] == 1f;
        int rodada; // só o servidor usa

        public override string Texture => "Terraria/Images/NPC_" + NPCID.EyeofCthulhu;

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 6;
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.SpecificDebuffImmunity[Type][BuffID.Confused] = true;
        }

        public override void SetDefaults()
        {
            NPC.boss = true;
            NPC.width = 100; NPC.height = 100;
            // Com o Calamity: vida no nível dos chefes dele, e mais no Revengeance/Death (igual aos chefes da raid)
            Common.Raid.Dificuldade.AtualizarAgora();
            NPC.lifeMax = (int)((Equilibrio.ComCalamity ? V.VidaComCalamity : V.Vida) * Equilibrio.MultVida);
            NPC.damage = V.DanoContato;
            NPC.defense = V.Defesa;
            NPC.knockBackResist = 0f;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.aiStyle = -1;
            NPC.npcSlots = 10f;
            NPC.value = Item.buyPrice(gold: 5);
            NPC.HitSound = SoundID.NPCHit1;
            NPC.DeathSound = SoundID.NPCDeath1;
            Music = MusicID.Boss1;
        }

        // Com o Calamity em Revengeance/Death os chefes dele têm redução de dano; a Sentinela também.
        public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers)
        {
            if (Equilibrio.ReducaoDeDano > 0f)
                modifiers.FinalDamage *= 1f - Equilibrio.ReducaoDeDano;
        }

        public override void AI()
        {
            NPC.TargetClosest(true);
            Player alvo = Main.player[NPC.target];

            if (!alvo.active || alvo.dead)
            {
                NPC.velocity.Y -= 0.4f;
                NPC.EncourageDespawn(60);
                return;
            }

            if (Main.netMode != NetmodeID.MultiplayerClient && !Fase2 && NPC.life < NPC.lifeMax * V.Fase2EmVida)
            {
                NPC.ai[3] = 1f;
                NPC.netUpdate = true;
                SoundEngine.PlaySound(SoundID.Roar, NPC.Center);
            }
            NPC.damage = (int)(NPC.defDamage * (Fase2 ? V.Fase2DanoMult : 1f));

            Timer++;
            float pausa = V.PausaEntreAtaques * (Fase2 ? V.Fase2PausaMult : 1f);

            switch (Atual)
            {
                case Estado.Perseguir:
                    MoverPara(alvo.Center + new Vector2(0f, -300f), V.Velocidade, V.Suavidade);
                    if (Timer >= pausa && Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        rodada++;
                        IrPara((rodada % 3) switch { 1 => Estado.Rajada, 2 => Estado.Investida, _ => Estado.Chuva });
                    }
                    break;

                case Estado.Rajada:
                    NPC.velocity *= 0.92f;
                    int t = (int)Timer - V.Telegrafo;
                    if (t >= 0 && t % V.RajadaIntervalo == 0 && t / V.RajadaIntervalo < V.RajadaRepeticoes
                        && Main.netMode != NetmodeID.MultiplayerClient)
                        AtirarLeque(alvo);
                    if (t > V.RajadaIntervalo * V.RajadaRepeticoes)
                        IrPara(Estado.Perseguir);
                    break;

                case Estado.Investida:
                    if (Timer < V.Telegrafo)
                    {
                        float lado = Contador % 2 == 0 ? -1f : 1f;
                        MoverPara(alvo.Center + new Vector2(lado * 450f, -80f), 10f, 0.15f);
                    }
                    else if (Timer == V.Telegrafo)
                    {
                        if (Main.netMode != NetmodeID.MultiplayerClient)
                        {
                            NPC.velocity = NPC.DirectionTo(alvo.Center) * V.InvestidaVelocidade;
                            NPC.netUpdate = true;
                        }
                        SoundEngine.PlaySound(SoundID.ForceRoar, NPC.Center);
                    }
                    else if (Timer > V.Telegrafo + V.InvestidaDuracao)
                    {
                        Contador++;
                        if (Contador >= V.InvestidaRepeticoes) IrPara(Estado.Perseguir);
                        else { Timer = 0; NPC.netUpdate = true; }
                    }
                    break;

                case Estado.Chuva:
                    MoverPara(alvo.Center + new Vector2(0f, -450f), V.Velocidade, V.Suavidade);
                    int c = (int)Timer - V.Telegrafo;
                    if (c >= 0 && c % V.ChuvaIntervalo == 0 && c / V.ChuvaIntervalo < V.ChuvaProjeteis
                        && Main.netMode != NetmodeID.MultiplayerClient)
                    {
                        float x = alvo.Center.X + Main.rand.NextFloat(-V.ChuvaLargura / 2f, V.ChuvaLargura / 2f);
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), new Vector2(x, alvo.Center.Y - 600f),
                            Vector2.UnitY * V.ChuvaVelocidade, ModContent.ProjectileType<SentinelaProjetil>(), V.ChuvaDano, 0f, Main.myPlayer);
                    }
                    if (c > V.ChuvaIntervalo * V.ChuvaProjeteis + 30)
                        IrPara(Estado.Perseguir);
                    break;
            }
        }

        void IrPara(Estado novo)
        {
            NPC.ai[0] = (float)novo;
            NPC.ai[1] = 0f;
            NPC.ai[2] = 0f;
            NPC.netUpdate = true;
        }

        void MoverPara(Vector2 destino, float velocidade, float suavidade)
        {
            Vector2 ate = destino - NPC.Center;
            float freio = MathHelper.Clamp(ate.Length() / 200f, 0f, 1f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, ate.SafeNormalize(Vector2.Zero) * velocidade * freio, suavidade);
        }

        void AtirarLeque(Player alvo)
        {
            int n = V.RajadaProjeteis + (Fase2 ? V.Fase2ProjetilExtra : 0);
            float centro = NPC.AngleTo(alvo.Center);
            float abertura = MathHelper.ToRadians(V.RajadaAbertura);
            for (int i = 0; i < n; i++)
            {
                float t = n == 1 ? 0.5f : i / (float)(n - 1);
                float angulo = centro - abertura / 2f + abertura * t;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, angulo.ToRotationVector2() * V.RajadaVelocidade,
                    ModContent.ProjectileType<SentinelaProjetil>(), V.RajadaDano, 0f, Main.myPlayer);
            }
            SoundEngine.PlaySound(SoundID.Item33, NPC.Center);
        }

        public override void PostDraw(SpriteBatch sb, Vector2 screenPos, Color drawColor)
        {
            if (Atual != Estado.Rajada || Timer >= V.Telegrafo) return;
            Vector2 para = Main.player[NPC.target].Center - NPC.Center;
            float forca = Timer / V.Telegrafo;
            sb.Draw(TextureAssets.MagicPixel.Value, NPC.Center - screenPos, new Rectangle(0, 0, 1, 1),
                Color.Red * (0.2f + 0.5f * forca), para.ToRotation(), Vector2.Zero, new Vector2(para.Length(), 1f + 3f * forca),
                SpriteEffects.None, 0f);
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot)
        {
            npcLoot.Add(ItemDropRule.OneFromOptions(1, V.ArmasDeTeste));
            npcLoot.Add(ItemDropRule.Common(ItemID.IronBar, 1, V.BarrasMin, V.BarrasMax));
        }

        public override void BossLoot(ref int potionType) => potionType = ItemID.HealingPotion;

        public override void OnKill()
        {
            MundoDoTeste.Derrotado = true;
            if (Main.netMode == NetmodeID.Server) NetMessage.SendData(MessageID.WorldData);
        }
    }

    public class SentinelaProjetil : ModProjectile
    {
        public override string Texture => "Terraria/Images/Projectile_" + ProjectileID.DeathLaser;

        public override void SetDefaults()
        {
            Projectile.width = 14; Projectile.height = 14;
            Projectile.hostile = true; Projectile.friendly = false;
            Projectile.tileCollide = false;
            Projectile.timeLeft = 300;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation() + MathHelper.PiOver2;
            Lighting.AddLight(Projectile.Center, 0.6f, 0.1f, 0.1f);
        }

        public override Color? GetAlpha(Color lightColor) => Color.White;
    }

    /// <summary>Invoca a Sentinela. Não é consumido enquanto você testa.</summary>
    public class InvocadorDeTeste : ModItem
    {
        public override string Texture => "Terraria/Images/Item_" + ItemID.SuspiciousLookingEye;

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 3;
            ItemID.Sets.SortingPriorityBossSpawns[Type] = 12;
        }

        public override void SetDefaults()
        {
            Item.width = 20; Item.height = 20;
            Item.maxStack = Item.CommonMaxStack;
            Item.rare = ItemRarityID.Blue;
            Item.useAnimation = 45; Item.useTime = 45;
            Item.useStyle = ItemUseStyleID.HoldUp;
            Item.consumable = false;
        }

        public override bool CanUseItem(Player player) => !NPC.AnyNPCs(ModContent.NPCType<SentinelaDeTeste>());

        public override bool? UseItem(Player player)
        {
            if (player.whoAmI != Main.myPlayer) return true;
            int tipo = ModContent.NPCType<SentinelaDeTeste>();
            SoundEngine.PlaySound(SoundID.Roar, player.position);
            if (Main.netMode != NetmodeID.MultiplayerClient) NPC.SpawnOnPlayer(player.whoAmI, tipo);
            else NetMessage.SendData(MessageID.SpawnBossUseLicenseStartEvent, number: player.whoAmI, number2: tipo);
            return true;
        }

        public override void AddRecipes() =>
            CreateRecipe().AddIngredient(ItemID.Lens, 3).AddTile(TileID.DemonAltar).Register();
    }

    /// <summary>Guarda no mundo se a Sentinela já foi derrotada.</summary>
    public class MundoDoTeste : ModSystem
    {
        public static bool Derrotado;

        public override void ClearWorld() => Derrotado = false;
        public override void SaveWorldData(TagCompound tag) { if (Derrotado) tag["sentinela"] = true; }
        public override void LoadWorldData(TagCompound tag) => Derrotado = tag.ContainsKey("sentinela");
        public override void NetSend(BinaryWriter w) => w.Write(Derrotado);
        public override void NetReceive(BinaryReader r) => Derrotado = r.ReadBoolean();
    }
}
