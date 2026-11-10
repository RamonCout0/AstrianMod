using System;
using System.Collections.Generic;
using System.IO;
using AscensaoAstral.Common;
using AscensaoAstral.Common.Raid;
using AscensaoAstral.Content.Buffs;
using AscensaoAstral.Content.Cenario;
using AscensaoAstral.Content.Projectiles;
using Luminance.Assets;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace AscensaoAstral.Content.Bosses.Astharoth
{
    /// <summary>
    /// RAID DO ASTHAROTH, O GUERREIRO ERRANTE (pós Golem).
    ///
    ///   ETAPA 1     Cortes de espada em cruz · rajadas de lâmina
    ///   TRANSIÇÃO   70%: A Separação das Almas
    ///                 Multiplayer: metade do grupo (sorteada) é levada ao Éter. Lá, CORRIDA pela ponte com raios de
    ///                 atordoar e o dragão mordendo; os de fora seguram o chefe. Quando alguém chega ao fim da ponte,
    ///                 o chefe mostra um SÍMBOLO que só os de FORA enxergam: eles dizem qual é, e os de dentro sobem
    ///                 na plataforma certa. Errou = wipe nos dois mundos. Acertou = todos são puxados para o Éter.
    ///                 Solo: vai direto para a arena do Éter.
    ///   ETAPA 2     Duelo no Éter (buraco negro no fundo): o cavaleiro luta de igual para igual; o dragão, no fundo,
    ///               solta rajadas e atravessa o mapa — OLHE para o lado dele quando ele vier
    ///   TRANSIÇÃO   35%: fusão → clarão → céu de nuvens → ARCANJO. Pegue 4 orbes boas (os ecos revelam quais atordoam),
    ///               fique na SOMBRA quando a rajada solar cair. Com 4 orbes vem a barreira; sem ela, o mergulho mata
    ///   ETAPA FINAL (em aberto no documento) por enquanto: todos os ataques anteriores até a morte
    /// </summary>
    [AutoloadBossHead]
    public class Astharoth : RaidBoss
    {
        private const int INTRO = 1, PAUSA = 2, CORTE = 3, RAJADAS = 4, ATORDOADO = 5, T_ALMA = 6,
            DUELO_CORTE = 7, PASSO_DO_ETER = 8, RASANTE = 9, T_FUSAO = 10, DESPEDIDA = 11,
            ESPIRAL = 12, LAMINAS = 13, FEIXE = 14, CHUVA = 15, // ataques que cobrem o ar (contra quem voa)
            // Etapa final — o ARCANJO tem ataques só dele, de luz
            JULGAMENTO = 16, ASAS = 17, CRUZ_DE_LUZ = 18,
            GOLPE_PESADO = 19, // golpe com janela de counter (fica azul)
            MORDIDA_DRAGAO = 20; // etapa 2: o dragão (de fundo) tenta uma mordida, com counter

        public override IEnumerable<Common.Raid.Vitrine.Item> AtaquesDaVitrine(bool comMecanicas)
        {
            yield return new(GOLPE_PESADO, 1, "GolpePesado"); yield return new(CORTE, 1, "Corte"); yield return new(RAJADAS, 1, "Rajadas"); yield return new(LAMINAS, 1, "Laminas"); yield return new(CHUVA, 1, "Chuva");
            yield return new(DUELO_CORTE, 2, "DueloCorte"); yield return new(ESPIRAL, 2, "Espiral"); yield return new(PASSO_DO_ETER, 2, "PassoDoEter");
            yield return new(FEIXE, 2, "Feixe"); yield return new(RASANTE, 2, "Rasante"); yield return new(MORDIDA_DRAGAO, 2, "MordidaDoDragao");
            yield return new(JULGAMENTO, 3, "Julgamento"); yield return new(ASAS, 3, "Asas"); yield return new(CRUZ_DE_LUZ, 3, "CruzDeLuz");
            yield return new(FEIXE, 3, "Feixe_F3"); yield return new(CHUVA, 3, "Chuva_F3");
            if (!comMecanicas) yield break;
            yield return new(T_ALMA, 1, "M_Alma", true); yield return new(T_FUSAO, 2, "M_Fusao", true);
        }

        private const float VelMover = 12f;
        private const float LimiarAlma = 0.70f, LimiarFusao = 0.35f;
        private static int TempoSimbolos => Dificuldade.Escolher(16, 14, 12) * 60;
        private static int TempoOrbes => Dificuldade.Escolher(30, 38, 45) * 60;
        // Ciclo da luz do Arcanjo: a sombra surge devagar → a luz carrega → rajada → respiro
        // Ele entra no fundo do céu (Entrada) e dá voltas: cada VOLTA é um "dia"; no fim de cada volta, a rajada
        private const int Entrada = 100, VoltaTicks = 720;
        private int TempoNaVolta => (int)Timer - Entrada;
        private float ProgressoDaVolta => TempoNaVolta < 0 ? 0f : TempoNaVolta % VoltaTicks / (float)VoltaTicks;
        private float CargaDaVolta => Utils.GetLerpValue(0.78f, 1f, ProgressoDaVolta, true);
        // O mergulho: sobe → a estrela pisca lá em cima → cai como estrela cadente
        private const int MergulhoSobe = 80, MergulhoCai = 170;
        public static int OrbesNecessarias => Dificuldade.Escolher(3, 4, 4);
        private static int OrbesRuins => Dificuldade.Escolher(2, 4, 4);

        // Rotações: cada nível libera mais ataques
        private static int[] RotacaoDuelo => Dificuldade.Escolher(
            new[] { CORTE, GOLPE_PESADO, RAJADAS, LAMINAS },
            new[] { CORTE, GOLPE_PESADO, RAJADAS, LAMINAS, CHUVA },
            new[] { CORTE, GOLPE_PESADO, RAJADAS, LAMINAS, CHUVA });
        private static int[] RotacaoAlma => Dificuldade.Escolher(
            new[] { DUELO_CORTE, PASSO_DO_ETER, MORDIDA_DRAGAO, GOLPE_PESADO, FEIXE },
            new[] { DUELO_CORTE, ESPIRAL, MORDIDA_DRAGAO, PASSO_DO_ETER, GOLPE_PESADO, FEIXE },
            new[] { DUELO_CORTE, ESPIRAL, MORDIDA_DRAGAO, PASSO_DO_ETER, GOLPE_PESADO, FEIXE, RASANTE });
        private static int[] RotacaoArcanjo => Dificuldade.Escolher(
            new[] { JULGAMENTO, ASAS, GOLPE_PESADO, FEIXE, CRUZ_DE_LUZ, PASSO_DO_ETER },
            new[] { JULGAMENTO, ESPIRAL, ASAS, FEIXE, CRUZ_DE_LUZ, LAMINAS, PASSO_DO_ETER },
            new[] { JULGAMENTO, ESPIRAL, ASAS, FEIXE, CRUZ_DE_LUZ, LAMINAS, CHUVA, PASSO_DO_ETER });
        private static readonly Color CorAstharoth = new(190, 150, 255);

        // A barra acompanha a vida (Expert, Calamity, Revengeance): ~13% da vida em dano quebra a postura.
        public override float StaggerMax => NPC.lifeMax * 0.08f;
        // No portão da Fusão a meta continua a de antes (proporcional à vida base)
        public override float MetaStagger => PortaoAtivo ? NPC.lifeMax * 0.046f : StaggerMax;
        public override bool PortaoAtivo => Estado == T_FUSAO && etapa >= 1 && etapa <= 2;
        public override bool EstaAtordoado => Estado == ATORDOADO;
        public override bool MostrarBarraDeQuebra => Estado != T_FUSAO && Estado != INTRO && Estado != DESPEDIDA;
        protected override bool PausarStagger => Estado == INTRO || Estado == T_ALMA || Estado == DESPEDIDA;
        public override float TempoRestantePortao => Estado switch
        {
            T_ALMA when etapa == 2 => 1f - Timer / TempoSimbolos,
            T_FUSAO when etapa == 1 => 1f - Timer / TempoOrbes,
            _ => -1f
        };

        public bool EmFormaDeArcanjo => Fase >= 3;

        public override string NomeDoEstado => Estado switch
        {
            INTRO => "Intro", PAUSA => "Pausa", CORTE => "Corte", RAJADAS => "Rajadas", ATORDOADO => "Atordoado",
            T_ALMA => "T:Alma", DUELO_CORTE => "DueloCorte", PASSO_DO_ETER => "PassoDoEter", RASANTE => "Rasante",
            T_FUSAO => "T:Fusao", DESPEDIDA => "Despedida", ESPIRAL => "Espiral", LAMINAS => "Laminas", FEIXE => "Feixe",
            CHUVA => "Chuva", JULGAMENTO => "Julgamento", ASAS => "AsasDeLuz", CRUZ_DE_LUZ => "CruzDeLuz", GOLPE_PESADO => "GolpePesado", MORDIDA_DRAGAO => "MordidaDoDragao", REINICIANDO => "Reiniciando", _ => Estado.ToString()
        };

        // ─── Dados extras ───
        private bool fezAlma, fezFusao, morteLiberada, modoSolo;
        private int mascaraEter;        // bit por jogador: quem foi levado ao Éter na separação
        private int simbolo = -1;       // símbolo certo (0 círculo, 1 triângulo, 2 quadrado)
        public int OrbesBoas;
        private int ladoDragao = 1, ladoAtaque = 1;
        private float sombraX;
        private float foraEsq, foraDir, foraTopo, foraPiso;
        private Vector2 alvoCorte;

        protected override void EscreverExtra(BinaryWriter w)
        {
            w.Write(new BitsByte(fezAlma, fezFusao, morteLiberada, modoSolo));
            w.Write(mascaraEter); w.Write(simbolo); w.Write(OrbesBoas); w.Write(ladoDragao); w.Write(ladoAtaque); w.Write(sombraX);
            w.Write(foraEsq); w.Write(foraDir); w.Write(foraTopo); w.Write(foraPiso); w.WriteVector2(alvoCorte);
            w.Write(alvoMordida); w.Write(counterNoCiclo);
        }

        protected override void LerExtra(BinaryReader r)
        {
            BitsByte b = r.ReadByte();
            fezAlma = b[0]; fezFusao = b[1]; morteLiberada = b[2]; modoSolo = b[3];
            mascaraEter = r.ReadInt32(); simbolo = r.ReadInt32(); OrbesBoas = r.ReadInt32(); ladoDragao = r.ReadInt32(); ladoAtaque = r.ReadInt32(); sombraX = r.ReadSingle();
            foraEsq = r.ReadSingle(); foraDir = r.ReadSingle(); foraTopo = r.ReadSingle(); foraPiso = r.ReadSingle(); alvoCorte = r.ReadVector2();
            alvoMordida = r.ReadInt32(); counterNoCiclo = r.ReadBoolean();
        }

        private Vector2 PosNoChao => new(CentroArena.X, ArenaPiso - 64f);
        private Vector2 CentroFora => new((foraEsq + foraDir) / 2f, foraPiso - 64f);
        private bool NoEter => Fase >= 2;

        // ─── Arena fechada ───
        protected override bool ConfinarAgora => Estado != INTRO && Estado != DESPEDIDA && Estado != REINICIANDO && !(Estado == T_ALMA && etapa == 3);
        // Enquanto a arena ainda é a de fora, quem está no Éter (na ponte) não é confinado nela
        protected override bool DeveConfinar(Player p) => NoEter || !(AstralMundo.EterConstruido && EterEstrutura.Dentro(p));
        protected override Color CorDaParede => Fase >= 3 ? new Color(255, 220, 140) : new Color(170, 110, 255);

        // ─── O dragão (veja DragaoDoEter.cs) ───
        /// <summary>-1 sem dragão · 0 persegue · 1 rasante · 2 ronda sem atacar.</summary>
        public int ModoDragao
        {
            get
            {
                if (Estado == DESPEDIDA || Estado == REINICIANDO || Estado == T_FUSAO || Fase >= 3) return -1;
                if (Estado == T_ALMA) return etapa == 1 ? 3 : -1;   // a Travessia: o dragão gigante, comandado daqui
                if (Estado == MORDIDA_DRAGAO) return 3;
                if (Fase == 2) return Estado == RASANTE ? 1 : 2;     // na etapa 2 ele fica DE FUNDO, rondando
                return -1;
            }
        }
        public bool DragaoNoDuelo => Fase == 2;
        /// <summary>Tamanho do dragão: gigante na Travessia, grande de fundo na etapa 2.</summary>
        public float EscalaDoDragao => NaTravessia ? 2.6f : 1.8f;
        /// <summary>Na etapa 2, rondando, ele é só uma silhueta de fundo (meio transparente).</summary>
        public bool DragaoDeFundo => Fase == 2 && ModoDragao == 2;
        public int TimerRasante => (int)Timer;
        public int LadoDoDragao => ladoDragao;

        /// <summary>Na ponte, o dragão caça quem está no Éter; no duelo, o mais perto.</summary>
        public Player AlvoDoDragao(Vector2 de)
        {
            if (Estado == RASANTE) return Alvo;
            bool soNaPonte = Estado == T_ALMA;
            Player melhor = null;
            float dist = float.MaxValue;
            foreach (Player p in Main.ActivePlayers)
            {
                if (p.dead || !JogadorNaRaid(p)) continue;
                if (soNaPonte && !EterEstrutura.Dentro(p)) continue;
                float d = p.DistanceSQ(de);
                if (d < dist) { dist = d; melhor = p; }
            }
            return melhor;
        }

        private DragaoDoEter Dragao
        {
            get
            {
                int i = NPC.FindFirstNPC(ModContent.NPCType<DragaoDoEter>());
                return i >= 0 ? Main.npc[i].ModNPC as DragaoDoEter : null;
            }
        }

        private void GarantirDragao()
        {
            if (!Servidor || ModoDragao < 0 || NPC.AnyNPCs(ModContent.NPCType<DragaoDoEter>()))
                return;
            Player alvo = AlvoDoDragao(NPC.Center);
            Vector2 de = alvo is not null ? alvo.Center + new Vector2(-600f, -380f) : CentroArena + new Vector2(0f, -400f); // nasce já perto da tela
            int i = NPC.NewNPC(NPC.GetSource_FromAI(), (int)de.X, (int)de.Y, ModContent.NPCType<DragaoDoEter>(), 0, NPC.whoAmI);
            if (i < Main.maxNPCs && Main.netMode == NetmodeID.Server)
                NetMessage.SendData(MessageID.SyncNPC, number: i);
            SoundEngine.PlaySound(SoundID.DD2_BetsyScream with { Pitch = -0.4f }, de);
            Hud.Aviso("O DRAGÃO DO ÉTER CHEGOU!", new Color(200, 150, 255), 120);
        }

        // Com o Calamity: no nível do Plaguebringer Goliath (70 mil; um pouco abaixo, pela luta ter mecânicas)
        protected override int VidaComCalamity => 60000;

        public override void SetBestiary(Terraria.GameContent.Bestiary.BestiaryDatabase database, Terraria.GameContent.Bestiary.BestiaryEntry bestiaryEntry)
        {
            bestiaryEntry.Info.AddRange(new Terraria.GameContent.Bestiary.IBestiaryInfoElement[]
            {
                Terraria.GameContent.Bestiary.BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Sky,
                new Terraria.GameContent.Bestiary.FlavorTextBestiaryInfoElement("Mods.AscensaoAstral.Bestiario.Astharoth")
            });
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) => Items.LootDosChefes.Astharoth(npcLoot);
        public override void BossLoot(ref int potionType) => potionType = ItemID.GreaterHealingPotion;

        protected override bool JaFoiDerrotado => AstralMundo.DerrotouAstharoth;

        public override bool JogadorNaRaid(Player p)
        {
            if (!p.active) return false;
            if (Raids.DentroDeUmaRaid) return true;
            if (AstralMundo.EterConstruido && EterEstrutura.Dentro(p)) return true;
            Vector2 c = new((foraEsq + foraDir) / 2f, (foraTopo + foraPiso) / 2f);
            return Vector2.Distance(p.Center, foraEsq == 0f ? CentroArena : c) < 2600f;
        }

        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.TrailCacheLength[Type] = 12;
            NPCID.Sets.TrailingMode[Type] = 3;
        }

        public override void SetDefaults()
        {
            NPC.width = 70;
            NPC.height = 120;
            // Equilíbrio (ver o documento): empata com a Empress of Light. Expert = 96.000, Master = 144.000
            NPC.damage = 80;
            NPC.defense = 40;
            NPC.lifeMax = 48000;
            NPC.HitSound = SoundID.NPCHit4;
            NPC.DeathSound = SoundID.NPCDeath14;
            NPC.value = Item.buyPrice(gold: 25);
            NPC.scale = 1.1f;
            Music = MusicID.Boss4;
            ConfigurarRaid();
        }

        public override void OnSpawn(IEntitySource source)
        {
            base.OnSpawn(source);
            foraEsq = ArenaEsq; foraDir = ArenaDir; foraTopo = ArenaTopo; foraPiso = ArenaPiso;
        }

        private void UsarArenaDeFora()
        {
            ArenaEsq = foraEsq; ArenaDir = foraDir; ArenaTopo = foraTopo; ArenaPiso = foraPiso;
        }

        private void UsarArenaDoEter()
        {
            Rectangle a = EterEstrutura.Arena;
            ArenaEsq = a.Left + 32f;
            ArenaDir = a.Right - 32f;
            ArenaPiso = EterEstrutura.Y0 * 16f;
            ArenaTopo = ArenaPiso - 1300f;
        }

        protected override void AoReiniciar()
        {
            ArcanjoNoCeu.Presenca = ArcanjoNoCeu.Carga = 0f;
            NPC.Opacity = 1f;
            NPC.dontTakeDamage = false;
            LimparPerigos();
            foreach (Projectile p in Main.ActiveProjectiles)
                if (p.type == ModContent.ProjectileType<OrbeEterea>())
                    p.Kill();
            mascaraEter = 0;
            simbolo = -1;
            OrbesBoas = 0;
            Hud.SimboloVisivel = -1;
            Hud.ClaraoAlvo = 0f;

            if (checkpointEstado == T_ALMA)
            {
                fezAlma = false;
                Fase = 1;
                UsarArenaDeFora();
                NPC.Center = CentroFora;
            }
            else if (Fase >= 2)
            {
                if (checkpointEstado == T_FUSAO) { fezFusao = false; Fase = 2; }
                UsarArenaDoEter();
                NPC.Center = PosNoChao;
            }
            else
                NPC.Center = PosNoChao;
        }

        // ═══════════════ MÁQUINA DE ESTADOS ═══════════════
        protected override void ExecutarEstado()
        {
            Music = Fase >= 3 ? MusicID.OtherworldlyBoss2 : Fase == 2 ? MusicID.LunarBoss : MusicID.Boss4;

            switch (Estado)
            {
                case INTRO: FazerIntro(); break;
                case PAUSA: FazerPausa(); break;
                case CORTE: FazerCorte(false); break;
                case DUELO_CORTE: FazerCorte(true); break;
                case RAJADAS: FazerRajadas(); break;
                case ATORDOADO: FazerAtordoado(); break;
                case T_ALMA: FazerSeparacao(); break;
                case PASSO_DO_ETER: FazerPassoDoEter(); break;
                case RASANTE: FazerRasante(); break;
                case T_FUSAO: FazerFusao(); break;
                case DESPEDIDA: FazerDespedida(); break;
                case ESPIRAL: FazerEspiral(); break;
                case LAMINAS: FazerLaminas(); break;
                case FEIXE: FazerFeixe(); break;
                case CHUVA: FazerChuva(); break;
                case JULGAMENTO: FazerJulgamento(); break;
                case ASAS: FazerAsas(); break;
                case CRUZ_DE_LUZ: FazerCruzDeLuz(); break;
                case GOLPE_PESADO: FazerGolpePesado(); break;
                case MORDIDA_DRAGAO: FazerMordidaDoDragao(); break;
                default: IrPara(INTRO); break;
            }

            GarantirDragao();
            if (!Main.dedServ)
            {
                bool noCeu = Estado == T_FUSAO && etapa == 1;
                ArcanjoNoCeu.Presenca = noCeu ? Utils.GetLerpValue(Entrada * 0.4f, Entrada, Timer, true) : Math.Max(0f, ArcanjoNoCeu.Presenca - 0.03f);
                ArcanjoNoCeu.Volta = noCeu ? ProgressoDaVolta : ArcanjoNoCeu.Volta;
                ArcanjoNoCeu.Carga = noCeu ? CargaDaVolta : 0f;
            }

            // No duelo, o dragão cospe bolas de fogo pela boca de vez em quando
            if (Dificuldade.Heroico && Fase == 2 && ModoDragao == 0 && Main.GameUpdateCount % 150 == 0 && Servidor && Dragao is DragaoDoEter dragao && !dragao.Mirando)
            {
                Player alvo = AlvoDoDragao(dragao.Boca);
                if (alvo is not null)
                {
                    Vector2 dir = (alvo.Center - dragao.Boca).SafeNormalize(Vector2.UnitY);
                    for (int k = -1; k <= 1; k++)
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), dragao.Boca, dir.RotatedBy(k * 0.3f) * 7f,
                            ModContent.ProjectileType<Rajada>(), 36, 0f, Main.myPlayer, 2f);
                    SoundEngine.PlaySound(SoundID.DD2_BetsyFireballShot, dragao.Boca);
                }
            }

            if (Estado != T_ALMA && Main.netMode != NetmodeID.Server)
                Hud.SimboloVisivel = -1;

            Lighting.AddLight(NPC.Center, Fase >= 3 ? new Vector3(1.2f, 1f, 0.6f) : new Vector3(0.7f, 0.5f, 1.1f));
        }

        private void FazerIntro()
        {
            NPC.dontTakeDamage = true;
            if (Timer == 1)
            {
                NPC.Center = new Vector2(CentroArena.X, ArenaPiso - 64f);
                NPC.Opacity = 0f;
                RaidJogador.Foco = NPC.Center;
                RaidJogador.ForcaFoco = 0.8f;
            }
            NPC.Opacity = Math.Min(1f, Timer / 60f);
            if (Timer == 30) Dialogo.Falar("Astharoth", "Mais um que ouviu o chamado da Astrânia. Mostre-me se sua alma aguenta ser cortada.", CorAstharoth, 220);
            if (Timer == 260)
            {
                RaidJogador.Foco = null;
                NPC.dontTakeDamage = false;
                Fase = 1;
                Hud.Fase("ETAPA 1: O Guerreiro Errante", CorAstharoth);
                SalvarCheckpoint(PAUSA, PosNoChao);
                IrPara(PAUSA);
            }
        }

        private void FazerPausa()
        {
            if (Timer == 1 && stunPendente)
            {
                stunPendente = false;
                ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 12f);
                CombatText.NewText(NPC.Hitbox, new Color(255, 230, 80), "POSTURA QUEBRADA!", true);
                IrPara(ATORDOADO);
                return;
            }

            MoverAte(new Vector2(MathHelper.Lerp(NPC.Center.X, Alvo.Center.X, 0.03f), ArenaPiso - 64f), VelMover * 0.5f);
            if (Timer < 55)
                return;

            float vida = NPC.life / (float)NPC.lifeMax;
            if (!fezAlma && vida <= LimiarAlma) { IrPara(T_ALMA); return; }
            if (!fezFusao && fezAlma && vida <= LimiarFusao) { IrPara(T_FUSAO); return; }

            int i = contadorAtaque++;
            IrPara(Fase switch
            {
                1 => RotacaoDuelo[i % RotacaoDuelo.Length],
                2 => RotacaoAlma[i % RotacaoAlma.Length],
                // Etapa final (o Arcanjo): os ataques de luz dele + os mais fortes de antes (5 / 7 / 8 conforme o nível)
                _ => RotacaoArcanjo[i % RotacaoArcanjo.Length]
            });
        }

        // ─── Cortes em cruz no lugar do jogador ───
        private void FazerCorte(bool duelo)
        {
            NPC.velocity *= 0.85f;
            int ciclo = duelo ? 50 : 65;
            int t = (int)(Timer - 1) % ciclo;
            int repeticoes = duelo ? 4 : 3;

            if (Timer > ciclo * repeticoes + 20)
            {
                IrPara(PAUSA);
                return;
            }

            if (t == 0 && Timer <= ciclo * repeticoes && Servidor)
            {
                alvoCorte = Alvo.Center;
                RetanguloPerigo.Criar(NPC, new Vector2(alvoCorte.X, CentroArena.Y), new Vector2(90f, ArenaPiso - ArenaTopo), duelo ? 36 : 44, TipoPerigo.Eter, 40);
                RetanguloPerigo.Criar(NPC, new Vector2(CentroArena.X, alvoCorte.Y), new Vector2(ArenaDir - ArenaEsq, 80f), duelo ? 36 : 44, TipoPerigo.Eter, 40);
                NPC.netUpdate = true;
            }
            if (t == 0)
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.2f }, NPC.Center);
        }

        private void FazerRajadas()
        {
            NPC.velocity *= 0.85f;
            bool solta = Timer == 20 || Timer == 45 || Timer == 70;
            if (solta && Servidor)
            {
                Vector2 dir = NPC.DirectionTo(Alvo.Center);
                for (int k = -2; k <= 2; k++)
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, dir.RotatedBy(k * 0.22f) * 9f, ModContent.ProjectileType<Rajada>(), 34, 0f, Main.myPlayer, 1f, 1f); // ai[1] = 1: persegue um pouco
            }
            if (solta)
                SoundEngine.PlaySound(SoundID.Item60, NPC.Center);
            if (Timer >= 120)
                IrPara(PAUSA);
        }

        private void FazerAtordoado()
        {
            NPC.velocity *= 0.9f;
            if (Main.rand.NextBool(3))
                Dust.NewDustPerfect(NPC.Top + Main.rand.NextVector2Circular(30f, 10f), DustID.GoldFlame, -Vector2.UnitY, 0, default, 1.1f).noGravity = true;
            if (Timer >= 240)
            {
                Stagger = 0f;
                IrPara(PAUSA);
            }
        }

        /// <summary>
        /// GOLPE PESADO (counter): ele crava o pé perto de você, ergue a lâmina e FICA AZUL com o "tudun".
        /// Acerte o Contra-ataque nessa janela e o golpe é cancelado; ele fica atordoado. Se ninguém acertar,
        /// sai um corte em meia-lua forte na sua direção (dois no Mítico, um para cada lado).
        /// </summary>
        private void FazerGolpePesado()
        {
            const int Chega = 40, Janela = 45, Solta = Chega + Janela;
            if (Timer < Chega)
            {
                if (Timer == 1 && Servidor)
                {
                    ladoAtaque = Alvo.Center.X > NPC.Center.X ? 1 : -1;
                    NPC.netUpdate = true;
                }
                float x = MathHelper.Clamp(Alvo.Center.X - ladoAtaque * 170f, ArenaEsq + 60f, ArenaDir - 60f);
                MoverAte(new Vector2(x, ArenaPiso - 64f), VelMover * 1.3f);
                return;
            }
            NPC.velocity *= 0.8f;
            if (Timer == Chega && Servidor)
                AbrirJanelaDeCounter(Janela);
            if (Timer == Solta)
            {
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.6f, Volume = 1.2f }, NPC.Center);
                ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 8f);
                if (Servidor)
                {
                    int dir = Alvo.Center.X > NPC.Center.X ? 1 : -1;
                    float arcanjo = Fase >= 3 ? 1f : 0f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, new Vector2(dir * 20f, 0f), ModContent.ProjectileType<CorteDoPasso>(), 55, 0f, Main.myPlayer, arcanjo);
                    if (Dificuldade.Mitico)
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, new Vector2(-dir * 20f, 0f), ModContent.ProjectileType<CorteDoPasso>(), 55, 0f, Main.myPlayer, arcanjo);
                }
            }
            if (Timer >= Solta + 45)
                IrPara(PAUSA);
        }

        private bool CounterNoDragao => (Estado == T_ALMA || Estado == MORDIDA_DRAGAO) && Dragao is not null;
        public override Vector2 OndeBrilhaCounter => CounterNoDragao ? Dragao.NPC.Center : NPC.Center;
        public override Rectangle AreaDeCounter => CounterNoDragao
            ? Utils.CenteredRectangle(Dragao.NPC.Center, new Vector2(54f * EscalaDoDragao + 60f))
            : NPC.Hitbox;

        protected override void AoSofrerCounter(int quem)
        {
            if (Estado == T_ALMA || Estado == MORDIDA_DRAGAO)
            {
                // counter no dragão: ele recua sem dar o golpe. Na etapa 2 o Astharoth fica exposto (quebra quase cheia)
                counterNoCiclo = true;
                if (Estado == MORDIDA_DRAGAO)
                    Stagger = Math.Min(StaggerMax, Stagger + StaggerMax * 0.4f);
                return;
            }
            Stagger = 0f;
            stunPendente = false;
            IrPara(ATORDOADO);
        }

        /// <summary>
        /// Passo do Éter: ele SOME, uma fenda se abre no espaço ao seu lado e ele sai dela cortando em meia-lua.
        /// É o duelo dele: não corre pelo chão (isso é do cavaleiro de gelo) — ele atravessa o espaço.
        /// Olhe a fenda: o corte vem dela na sua direção. Pule ou atravesse para o outro lado.
        /// </summary>
        private void FazerPassoDoEter()
        {
            const int Ciclo = 48, Abre = 30;
            bool arcanjo = Fase >= 3;
            int n = Dificuldade.Escolher(3, 3, 4) + (arcanjo ? 1 : 0);
            int idx = (int)(Timer - 1) / Ciclo, t = (int)(Timer - 1) % Ciclo;
            if (idx >= n)
            {
                NPC.Opacity = 1f;
                NPC.velocity *= 0.85f;
                if (Timer >= n * Ciclo + 30) IrPara(PAUSA);
                return;
            }
            if (t == 0)
            {
                if (Servidor)
                {
                    // a primeira fenda abre ATRÁS de você (do lado que você não está olhando); depois vai alternando
                    ladoAtaque = idx == 0 ? -Alvo.direction : -ladoAtaque;
                    alvoCorte = new Vector2(MathHelper.Clamp(Alvo.Center.X + ladoAtaque * 240f, ArenaEsq + 60f, ArenaDir - 60f),
                        MathHelper.Clamp(Alvo.Center.Y - 10f, ArenaTopo + 80f, ArenaPiso - 60f));
                    NPC.netUpdate = true;
                }
                SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.5f }, NPC.Center);
                for (int i = 0; i < 16; i++)
                    Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(30f, 50f), DustID.PurpleTorch, Main.rand.NextVector2Circular(4f, 4f), 0, default, 1.6f).noGravity = true;
            }
            if (t < Abre)
            {
                NPC.velocity = Vector2.Zero;
                NPC.Opacity = Math.Max(0f, NPC.Opacity - 0.15f);
                return;
            }
            if (t == Abre)
            {
                NPC.Center = alvoCorte;
                NPC.Opacity = 1f;
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.3f }, NPC.Center);
                if (Servidor)
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, new Vector2(-ladoAtaque * 17f, 0f), ModContent.ProjectileType<CorteDoPasso>(), 40, 0f, Main.myPlayer, arcanjo ? 1f : 0f);
            }
            NPC.velocity = new Vector2(-ladoAtaque * 7f * (1f - (t - Abre) / (float)(Ciclo - Abre)), 0f); // o impulso do corte
        }

        // ─── O dragão atravessa: olhe para ele ───
        /// <summary>
        /// Rasante do dragão: o dragão do Éter atravessa a arena de um lado ao outro, rasgando uma faixa na sua altura.
        /// A faixa aparece antes (aviso) — saia da altura dela. Duas passadas (três no Mítico), cada uma de um lado.
        /// </summary>
        private void FazerRasante()
        {
            NPC.velocity *= 0.85f;
            int passadas = Dificuldade.Mitico ? 3 : 2;
            int intervalo = 70;
            if (Timer == 1)
                SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.8f });
            for (int k = 0; k < passadas; k++)
                if (Timer == 10 + k * intervalo && Servidor)
                {
                    ladoDragao = k % 2 == 0 ? (Main.rand.NextBool() ? 1 : -1) : -ladoDragao;
                    NPC.netUpdate = true;
                    float y = MathHelper.Clamp(Alvo.Center.Y, ArenaTopo + 80f, ArenaPiso - 65f);
                    float largura = ArenaDir - ArenaEsq;
                    RetanguloPerigo.Criar(NPC, new Vector2((ArenaEsq + ArenaDir) / 2f, y), new Vector2(largura, 130f), 55, TipoPerigo.Eter, 40);
                }
            if (Timer >= 10 + passadas * intervalo + 40)
                IrPara(PAUSA);
        }

        // ═══════════════ ATAQUES AÉREOS (não adianta só voar) ═══════════════
        /// <summary>Espiral: anéis de rajadas girando a partir do centro — cobre a arena toda, o jeito é passar entre elas.</summary>
        private void FazerEspiral()
        {
            MoverAte(new Vector2(CentroArena.X, ArenaTopo + 170f), VelMover);
            if (Timer >= 30 && Timer <= 150 && Timer % 20 == 10)
            {
                SoundEngine.PlaySound(SoundID.Item9 with { Pitch = -0.3f }, NPC.Center);
                if (Servidor)
                {
                    int n = Fase >= 3 ? 20 : 17;
                    // uma onda gira para um lado, a seguinte para o outro: as brechas mudam de lugar
                    float giro = (Timer / 20 % 2 == 0 ? 1f : -1f) * Timer * 0.045f;
                    for (int k = 0; k < n; k++)
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, (giro + k * MathHelper.TwoPi / n).ToRotationVector2() * 3.8f,
                            ModContent.ProjectileType<Rajada>(), 34, 0f, Main.myPlayer, Fase >= 3 ? 2f : 1f);
                }
            }
            if (Timer >= 210) IrPara(PAUSA);
        }

        /// <summary>Lâminas guiadas: leque de lâminas que param, miram e perseguem quem estiver mais perto.</summary>
        private void FazerLaminas()
        {
            MoverAte(new Vector2(MathHelper.Lerp(NPC.Center.X, Alvo.Center.X, 0.05f), ArenaPiso - 220f), VelMover * 0.6f);
            if (Timer == 20 || Timer == 60 || Timer == 100 || Timer == 140)
            {
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.3f }, NPC.Center);
                if (Servidor)
                    for (int k = -1; k <= 1; k++)
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, (-Vector2.UnitY).RotatedBy(k * 0.6f) * 11f,
                            ModContent.ProjectileType<LaminaGuiada>(), 34, 0f, Main.myPlayer, Fase >= 3 ? 2f : 1f, 30 + Math.Abs(k) * 8);
            }
            if (Timer >= 220) IrPara(PAUSA);
        }

        /// <summary>
        /// Feixe: ele vai para um lado da arena e solta feixes HORIZONTAIS que cruzam tudo, um de cada vez:
        /// o baixo (rente ao chão → pule) e o alto (na altura do peito de quem voa → fique no chão ou passe por cima).
        /// No Arcanjo são quatro, mais rápidos, de luz solar.
        /// </summary>
        private void FazerFeixe()
        {
            bool arcanjo = Fase >= 3;
            int n = arcanjo ? 4 : 3, intervalo = arcanjo ? 42 : 52;
            if (Timer == 1 && Servidor) { ladoAtaque = Alvo.Center.X > CentroArena.X ? -1 : 1; NPC.netUpdate = true; }
            MoverAte(new Vector2(ladoAtaque < 0 ? ArenaEsq + 90f : ArenaDir - 90f, ArenaPiso - 170f), VelMover * 1.3f);
            for (int k = 0; k < n; k++)
                if (Timer == 30 + k * intervalo && Servidor)
                {
                    bool baixo = k % 2 == 0;
                    float largura = ArenaDir - ArenaEsq;
                    Vector2 centro = baixo ? new Vector2((ArenaEsq + ArenaDir) / 2f, ArenaPiso - 36f) : new Vector2((ArenaEsq + ArenaDir) / 2f, ArenaPiso - 230f);
                    Vector2 tamanho = baixo ? new Vector2(largura, 72f) : new Vector2(largura, 230f);
                    RetanguloPerigo.Criar(NPC, centro, tamanho, arcanjo ? 38 : 45, arcanjo ? TipoPerigo.Solar : TipoPerigo.Eter, 45);
                    SoundEngine.PlaySound(SoundID.Item15 with { Pitch = baixo ? -0.4f : 0.2f }, NPC.Center);
                }
            if (Timer >= 30 + n * intervalo + 50) IrPara(PAUSA);
        }

        /// <summary>Chuva estelar: cai do teto na largura toda, com um único buraco seguro por onda.</summary>
        private void FazerChuva()
        {
            MoverAte(new Vector2(CentroArena.X, ArenaTopo + 80f), VelMover);
            if (Timer == 20 || Timer == 80 || Timer == 140 || Timer == 200)
            {
                SoundEngine.PlaySound(SoundID.Item105 with { Pitch = -0.2f }, NPC.Center);
                QuedaEstelar.Chuva(NPC, ArenaEsq, ArenaDir, ArenaTopo, ArenaPiso, Fase >= 3 ? 2f : 1f, 36, 46, 40f, 130f); // 40 px: sem fresta entre as estrelas
            }
            if (Timer >= 290) IrPara(PAUSA);
        }

        // ═══════════════ O ARCANJO (etapa final) ═══════════════
        /// <summary>Julgamento solar: pilares de luz caem do céu um atrás do outro em cima de você (e onde você vai estar).</summary>
        private void FazerJulgamento()
        {
            MoverAte(new Vector2(CentroArena.X, ArenaTopo + 150f), VelMover);
            int t = (int)Timer - 30;
            if (t >= 0 && t % 18 == 0 && t / 18 < 10)
            {
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = 0.4f, Volume = 0.6f }, Alvo.Center);
                if (Servidor)
                {
                    // um pilar onde você está e outro onde você estaria se continuasse correndo
                    float x = Alvo.Center.X, previsto = Alvo.Center.X + Alvo.velocity.X * 32f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), new Vector2(x, ArenaTopo), Vector2.Zero, ModContent.ProjectileType<PilarSolar>(), 44, 0f, Main.myPlayer, ArenaTopo, ArenaPiso, 34);
                    if (Math.Abs(previsto - x) > 140f)
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), new Vector2(MathHelper.Clamp(previsto, ArenaEsq + 60f, ArenaDir - 60f), ArenaTopo), Vector2.Zero, ModContent.ProjectileType<PilarSolar>(), 44, 0f, Main.myPlayer, ArenaTopo, ArenaPiso, 34);
                }
            }
            if (Timer >= 30 + 18 * 10 + 60) IrPara(PAUSA);
        }

        /// <summary>Asas de luz: leques de penas que fazem curva; uma onda curva para a esquerda, a outra para a direita.</summary>
        private void FazerAsas()
        {
            MoverAte(new Vector2(CentroArena.X, ArenaTopo + 220f), VelMover);
            if (Timer >= 40 && Timer <= 160 && (Timer - 40) % 30 == 0)
            {
                SoundEngine.PlaySound(SoundID.Item8 with { Pitch = -0.2f }, NPC.Center);
                if (Servidor)
                {
                    float curva = ((Timer - 40) / 30 % 2 == 0 ? 1f : -1f) * 0.009f;
                    for (int k = 0; k < 16; k++)
                    {
                        float ang = MathHelper.Lerp(0.15f, MathHelper.Pi - 0.15f, k / 15f); // leque para baixo
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, ang.ToRotationVector2() * 8f, ModContent.ProjectileType<PenaDeLuz>(), 34, 0f, Main.myPlayer, curva);
                    }
                }
            }
            if (Timer >= 260) IrPara(PAUSA);
        }

        /// <summary>Cruz de luz: ele risca a arena em três investidas cruzadas; os riscos explodem logo depois.</summary>
        private void FazerCruzDeLuz()
        {
            int idx = (int)Timer / 50;
            int t = (int)Timer % 50;
            if (idx >= 3)
            {
                NPC.velocity *= 0.85f;
                if (Timer >= 3 * 50 + 70) IrPara(PAUSA);
                return;
            }
            // cantos: ↘, ↙, e uma linha reta passando por você
            Vector2 de, para;
            if (idx == 0) { de = new(ArenaEsq + 120f, ArenaTopo + 120f); para = new(ArenaDir - 120f, ArenaPiso - 90f); }
            else if (idx == 1) { de = new(ArenaDir - 120f, ArenaTopo + 120f); para = new(ArenaEsq + 120f, ArenaPiso - 90f); }
            else { de = new(ArenaEsq + 120f, MathHelper.Clamp(Alvo.Center.Y, ArenaTopo + 100f, ArenaPiso - 80f)); para = new(ArenaDir - 120f, de.Y); }
            if (t < 20)
            {
                NPC.Center = Vector2.Lerp(NPC.Center, de, 0.35f);
                if (t == 0 && Servidor)
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), de, Vector2.Zero, ModContent.ProjectileType<RastroDeLuz>(), 46, 0f, Main.myPlayer, para.X, para.Y, 55);
            }
            else
            {
                NPC.Center = Vector2.Lerp(de, para, Utils.GetLerpValue(20, 34, t, true));
                if (t == 20) SoundEngine.PlaySound(SoundID.Item74 with { Pitch = 0.3f }, NPC.Center);
            }
        }

        // ═══════════════ TRANSIÇÃO: A SEPARAÇÃO DAS ALMAS ═══════════════
        private bool EstaNaMascara(int quem) => (mascaraEter & (1 << quem)) != 0;
        /// <summary>Para o Bot de Teste: ele foi sorteado para o Éter e a corrida da ponte está acontecendo?</summary>
        public bool BotFoiParaOEter(Player bot) => NaTravessia; // na Travessia vão todos

        private Rectangle Plataforma(int i)
        {
            float x = MathHelper.Lerp(EterEstrutura.Arena.Left + 200f, EterEstrutura.Arena.Right - 200f, i / 2f);
            float chao = EterEstrutura.Y0 * 16f;
            return new Rectangle((int)(x - 60f), (int)(chao - 48f), 120, 48);
        }

        // ═══════════════ TRANSIÇÃO: A TRAVESSIA DO ÉTER ═══════════════
        // 70%: ele some numa fenda e o GRUPO TODO vai para o corredor (a ponte do Éter). Atrás de vocês a ponte
        // DESMORONA; à frente, uma parede de luz segura o grupo — então é andar junto, no ritmo do Éter.
        // O DRAGÃO GIGANTE acompanha e alterna dois golpes, os dois com janela de COUNTER (fica azul, "tudun"):
        //   INVESTIDA DE VENTO  sem counter, empurra todo mundo para trás (na direção do desmoronamento)
        //   MORDIDA             sem counter, morde o jogador marcado e o arremessa para trás
        // Ninguém voa no corredor. Quem cair da ponte derruba o grupo (WIPE). Todos no fim → ETAPA 2.
        private const float VelColapso = 1.1f, FolgaDaFrente = 1700f;
        private const int AtrasoColapso = 180;
        private const int CicloTrav = 330, TravPrepara = 120, TravJanela = 165, TravGolpe = 210, TravFim = 250;
        private const int JanelaDoDragao = 45;
        private const float DanoVento = 0.10f, DanoMordida = 0.30f; // fração da vida máxima
        private int colunaColapso = -1;  // (só o servidor usa) última coluna da ponte que já caiu
        private int alvoMordida;
        private bool counterNoCiclo;
        private bool estiveNaPonte; // (cliente) já pisei no corredor nesta travessia? quem caiu não volta teleportado

        public bool NaTravessia => Estado == T_ALMA && etapa == 1;
        /// <summary>Até onde a ponte já caiu (cresce com o tempo; igual em todos os clientes).</summary>
        public float ColapsoX => Math.Min(EterEstrutura.X0 * 16f + Math.Max(0f, Timer - AtrasoColapso) * VelColapso, EterEstrutura.FimDaPonteX - 16f);
        /// <summary>A parede de luz à frente do grupo.</summary>
        public float FrenteX => Math.Min(ColapsoX + FolgaDaFrente, EterEstrutura.Arena.Right - 40f);
        private int TCiclo => (int)Timer % CicloTrav;
        private bool CicloEhInvestida => (int)Timer / CicloTrav % 2 == 0;

        private float MediaXDoGrupo(out float maiorX)
        {
            float soma = 0f; int n = 0; maiorX = ColapsoX + 600f;
            foreach (Player p in Main.ActivePlayers)
                if (!p.dead && EterEstrutura.Dentro(p))
                {
                    soma += p.Center.X; n++;
                    if (n == 1 || p.Center.X > maiorX) maiorX = p.Center.X;
                }
            return n > 0 ? soma / n : ColapsoX + 600f;
        }

        private void FazerSeparacao()
        {
            Player eu = Main.LocalPlayer;
            bool souCliente = Main.netMode != NetmodeID.Server;

            switch (etapa)
            {
                case 0: // ele abre a fenda e some nela
                    if (Timer == 1)
                    {
                        fezAlma = true;
                        SalvarCheckpoint(T_ALMA, CentroFora);
                        LimparPerigos();
                        Hud.Fase("TRANSIÇÃO: A Travessia do Éter", CorAstharoth);
                        Dialogo.Falar("Astharoth", "Querem me alcançar? Atravessem. O Éter não espera quem fica para trás.", CorAstharoth, 200);
                    }
                    MoverAte(PosNoChao, VelMover);
                    NPC.Opacity = Math.Max(0f, 1f - Math.Max(0f, Timer - 90f) / 60f);
                    if (Main.rand.NextBool(2))
                        Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(60f, 80f), DustID.PurpleTorch, -Vector2.UnitY * 2f, 0, default, 1.6f).noGravity = true;

                    if (Timer >= 150)
                    {
                        if (Servidor)
                        {
                            if (AstralMundo.EterConstruido) EterEstrutura.Demolir(); // ponte inteira de novo (pode ter caído na tentativa anterior)
                            EterEstrutura.Construir(soArena: false);
                            colunaColapso = EterEstrutura.X0 - 1;
                        }
                        counterNoCiclo = false;
                        estiveNaPonte = false;
                        Hud.Clarao(0.9f);
                        ScreenShakeSystem.StartShake(18f);
                        SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.8f, Volume = 1.2f });
                        Timer = 0;
                        etapa = 1;
                        NPC.netUpdate = true;
                    }
                    break;

                case 1:
                    FazerTravessia(eu, souCliente);
                    break;

                case 3: // todos chegaram: a arena do Éter
                    if (Timer == 1)
                    {
                        Hud.SimboloVisivel = -1;
                        Hud.Clarao(0.8f);
                        UsarArenaDoEter();
                        NPC.Center = PosNoChao;
                        NPC.velocity = Vector2.Zero;
                        NPC.Opacity = 1f;
                        NPC.dontTakeDamage = false;
                        NPC.netUpdate = true;
                    }
                    if (souCliente && !eu.dead && JogadorNaRaid(eu) && !EterEstrutura.Arena.Intersects(eu.Hitbox))
                        RaidJogador.Teleportar(eu, new Vector2(ArenaEsq + 160f, ArenaPiso - 60f));

                    if (Timer >= 40)
                    {
                        Fase = 2;
                        Hud.Fase("ETAPA 2: O Duelo no Éter", CorAstharoth);
                        Dialogo.Falar("Astharoth", "Chegaram inteiros. Agora não existe chão para fugir. Só eu, você... e ele.", CorAstharoth, 200);
                        SalvarCheckpoint(PAUSA, new Vector2(ArenaEsq + 160f, ArenaPiso - 60f));
                        IrPara(PAUSA);
                    }
                    break;

                default: // (a antiga sequência de símbolos não existe mais)
                    etapa = 3;
                    Timer = 0;
                    break;
            }
        }

        private void FazerTravessia(Player eu, bool souCliente)
        {
            // O Astharoth espera no fim do corredor, fora de alcance
            NPC.velocity = Vector2.Zero;
            NPC.Center = EterEstrutura.CentroDaArena;
            NPC.Opacity = 0f;
            NPC.dontTakeDamage = true;

            float chao = EterEstrutura.Y0 * 16f;
            int tc = TCiclo;
            bool investida = CicloEhInvestida;
            if (tc == TravPrepara)
                counterNoCiclo = false;

            if (Servidor)
            {
                // A ponte desmorona atrás do grupo
                if (colunaColapso < EterEstrutura.X0 - 1) colunaColapso = EterEstrutura.X0 - 1;
                int ate = (int)(ColapsoX / 16f) - 1;
                ushort ponte = (ushort)ModContent.TileType<Tiles.PonteEterea>();
                while (colunaColapso < ate)
                {
                    colunaColapso++;
                    for (int y = EterEstrutura.Y0 - 90; y <= EterEstrutura.Y0 + 1; y++)
                    {
                        Tile t = Framing.GetTileSafely(colunaColapso, y);
                        if (t.HasTile && t.TileType == ponte)
                        {
                            t.HasTile = false; // (KillTile é barrado pelo CanKillTile da ponte)
                            WorldGen.SquareTileFrame(colunaColapso, y);
                        }
                    }
                    if (Main.netMode == NetmodeID.Server)
                        NetMessage.SendTileSquare(-1, colunaColapso, EterEstrutura.Y0 - 90, 1, 92);
                }

                // Raios de atordoar à frente de quem anda (só entre os golpes do dragão)
                if (tc < TravPrepara && Timer % 140 == 70)
                    foreach (Player p in Main.ActivePlayers)
                        if (!p.dead && EterEstrutura.Dentro(p) && p.Center.X + 260f < FrenteX)
                            RetanguloPerigo.Criar(NPC, new Vector2(p.Center.X + 260f, chao - 180f), new Vector2(60f, 360f), 45, TipoPerigo.Atordoar, 30);

                // O dragão escolhe quem morder, abre a janela e trava a mordida
                if (tc == TravPrepara && !investida)
                {
                    List<int> candidatos = new();
                    foreach (Player p in Main.ActivePlayers)
                        if (!p.dead && EterEstrutura.Dentro(p)) candidatos.Add(p.whoAmI);
                    alvoMordida = candidatos.Count > 0 ? candidatos[Main.rand.Next(candidatos.Count)] : Alvo.whoAmI;
                    NPC.netUpdate = true;
                }
                if (tc == TravJanela)
                    AbrirJanelaDeCounter(JanelaDoDragao);
                if (tc == TravGolpe - 10 && !investida)
                {
                    alvoCorte = Main.player[alvoMordida].Center;
                    NPC.netUpdate = true;
                }

                // Caiu da ponte = wipe. Todos no fim = etapa 2.
                bool alguem = false, todosChegaram = true;
                foreach (Player p in Main.ActivePlayers)
                {
                    if (p.dead || !JogadorNaRaid(p)) continue;
                    alguem = true;
                    bool debaixoDaPonte = p.Center.X > EterEstrutura.X0 * 16f - 100f && p.Center.X < EterEstrutura.Arena.Right
                        && p.Center.Y > chao + 320f && p.Center.Y < chao + 1600f;
                    if (Timer > 60 && debaixoDaPonte)
                    {
                        WipeGlobal();
                        return;
                    }
                    if (p.Center.X < EterEstrutura.FimDaPonteX + 40f) todosChegaram = false;
                }
                if (alguem && todosChegaram)
                {
                    etapa = 3;
                    Timer = 0;
                    NPC.netUpdate = true;
                    return;
                }
            }

            if (!souCliente)
                return;

            // A beirada que desmorona
            if (Timer > AtrasoColapso && Main.rand.NextBool(2))
                Dust.NewDustPerfect(new Vector2(ColapsoX + Main.rand.NextFloat(-12f, 12f), chao + Main.rand.NextFloat(0f, 24f)), DustID.PurpleTorch, new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(2f, 5f)), 0, default, 1.5f);

            if (eu.dead || !JogadorNaRaid(eu))
                return;

            // Quem ainda não entrou vai para a ponte, à frente do desmoronamento (quem caiu dela, não)
            if (EterEstrutura.Dentro(eu) && eu.Center.Y < chao + 40f)
                estiveNaPonte = true;
            if (!estiveNaPonte && !EterEstrutura.Dentro(eu))
            {
                RaidJogador.Teleportar(eu, new Vector2(ColapsoX + 260f, chao - 40f));
                return;
            }

            // Ninguém voa no corredor; a parede de luz segura quem corre na frente
            eu.wingTime = 0f;
            eu.rocketTime = 0;
            if (eu.mount.Active && eu.mount.CanFly())
                eu.mount.Dismount(eu);
            if (eu.Right.X > FrenteX)
                eu.velocity.X = Math.Min(eu.velocity.X, 0f) - 1.6f;

            if (counterNoCiclo)
                return;

            if (investida)
            {
                // Investida de vento: empurra todo mundo para trás
                if (tc >= TravGolpe && tc < TravFim && EterEstrutura.Dentro(eu))
                {
                    float forca = 1f - (tc - TravGolpe) / (float)(TravFim - TravGolpe);
                    eu.velocity.X = Math.Min(eu.velocity.X, -11f * forca);
                    if (tc == TravGolpe)
                    {
                        CastigoLocal(eu, DanoVento);
                        ScreenShakeSystem.StartShake(10f);
                        SoundEngine.PlaySound(SoundID.DD2_BetsyWindAttack with { Volume = 1.2f, Pitch = -0.4f });
                    }
                    if (Main.rand.NextBool(2))
                        Dust.NewDustPerfect(eu.Center + new Vector2(Main.rand.NextFloat(-300f, 300f), Main.rand.NextFloat(-120f, 40f)), DustID.PurpleTorch, new Vector2(-12f, 0f), 0, default, 1.3f).noGravity = true;
                }
            }
            else
                MorderSeEstiverPerto(eu, tc == TravGolpe + 4);
        }

        /// <summary>A mordida (Travessia e etapa 2): quem estiver perto do ponto travado leva o golpe e é arremessado.</summary>
        private void MorderSeEstiverPerto(Player eu, bool agora)
        {
            if (!agora || eu.dead || Vector2.Distance(eu.Center, alvoCorte) > 170f) return;
            CastigoLocal(eu, DanoMordida);
            float lado = Dragao is DragaoDoEter d && eu.Center.X < d.NPC.Center.X ? -1f : 1f;
            eu.velocity = new Vector2(NaTravessia ? -17f : lado * 14f, -8f);
            ScreenShakeSystem.StartShake(12f);
            SoundEngine.PlaySound(SoundID.DD2_BetsyScream with { Pitch = 0.4f });
        }

        // ─── Etapa 2: o dragão (de fundo) tenta uma mordida ───
        private const int MordJanela = 45, MordGolpe = 90, MordFim = 140;
        private void FazerMordidaDoDragao()
        {
            MoverAte(new Vector2(MathHelper.Clamp(NPC.Center.X, ArenaEsq + 200f, ArenaDir - 200f), ArenaPiso - 64f), VelMover * 0.5f);
            if (Timer == 1)
            {
                counterNoCiclo = false;
                SoundEngine.PlaySound(SoundID.DD2_BetsyScream with { Pitch = 0.2f, Volume = 0.8f });
                if (Servidor)
                {
                    alvoMordida = Alvo.whoAmI;
                    NPC.netUpdate = true;
                }
            }
            if (Timer == MordJanela && Servidor)
                AbrirJanelaDeCounter(JanelaDoDragao);
            if (Timer == MordGolpe - 10 && Servidor)
            {
                alvoCorte = Main.player[alvoMordida].Center;
                NPC.netUpdate = true;
            }
            if (Main.netMode != NetmodeID.Server && !counterNoCiclo)
                MorderSeEstiverPerto(Main.LocalPlayer, Timer == MordGolpe + 4);
            if (Timer >= MordFim)
                IrPara(PAUSA);
        }

        /// <summary>
        /// Para onde o dragão vai (Travessia e mordida da etapa 2). Calculado igual em todos os clientes a partir do
        /// estado sincronizado. false = ele fica só rondando.
        /// </summary>
        public bool ComandoDoDragao(Vector2 cabeca, out Vector2 destino, out float vel, out float suave)
        {
            destino = cabeca; vel = 0f; suave = 0.1f;
            float chao = EterEstrutura.Y0 * 16f;
            Player mordido = Main.player[Math.Clamp(alvoMordida, 0, Main.maxPlayers - 1)];

            if (NaTravessia)
            {
                int tc = TCiclo;
                float media = MediaXDoGrupo(out float maiorX);
                if (tc < TravPrepara || tc >= TravFim)
                {
                    destino = new Vector2(media + 520f, chao - 430f + MathF.Sin(Timer * 0.03f) * 60f);
                    vel = 16f; suave = 0.06f;
                    return true;
                }
                if (counterNoCiclo && tc >= TravJanela)
                {
                    destino = cabeca + new Vector2(260f, -380f); // levou o counter: recua
                    vel = 10f; suave = 0.08f;
                    return true;
                }
                if (tc < TravGolpe)
                {
                    // fica AO ALCANCE do counter: na frente do grupo (vento) ou ao lado de quem vai morder
                    destino = CicloEhInvestida ? new Vector2(maiorX + 260f, chao - 110f) : mordido.Center + new Vector2(220f, -120f);
                    vel = tc < TravJanela ? 22f : 3f; suave = 0.12f;
                    return true;
                }
                destino = CicloEhInvestida ? new Vector2(cabeca.X - 2400f, chao - 150f) : alvoCorte;
                vel = CicloEhInvestida ? 34f : 30f; suave = 0.3f;
                return true;
            }

            if (Estado == MORDIDA_DRAGAO)
            {
                if (counterNoCiclo && Timer >= MordJanela)
                {
                    destino = cabeca + new Vector2(0f, -420f);
                    vel = 10f; suave = 0.08f;
                    return true;
                }
                if (Timer < MordGolpe)
                {
                    float lado = cabeca.X >= mordido.Center.X ? 1f : -1f;
                    destino = mordido.Center + new Vector2(lado * 240f, -140f);
                    vel = Timer < MordJanela ? 24f : 3f; suave = 0.12f;
                    return true;
                }
                if (Timer < MordGolpe + 20)
                {
                    destino = alvoCorte; vel = 30f; suave = 0.3f;
                    return true;
                }
            }
            return false;
        }

        /// <summary>Visual da Travessia (desmoronamento, parede de luz, marca da mordida) e do counter no dragão.
        /// Chamado pelo dragão, que está sempre na tela.</summary>
        public void DesenharTravessia()
        {
            float pulso = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f);
            if (NaTravessia)
            {
                float chao = EterEstrutura.Y0 * 16f;
                for (int i = 0; i < 6; i++)
                    Desenho.Brilho(new Vector2(ColapsoX - 40f, chao - 40f - i * 110f), new Color(120, 60, 200) * 0.6f, 2.4f);
                Desenho.Faixa(new Vector2(FrenteX, chao - 700f), new Vector2(FrenteX, chao + 20f), 10f + 6f * pulso, new Color(190, 150, 255) * 0.35f, new Color(240, 220, 255));
            }

            // Marca de quem vai ser mordido
            bool marcando = NaTravessia ? !CicloEhInvestida && TCiclo >= TravPrepara && TCiclo < TravGolpe
                                        : Estado == MORDIDA_DRAGAO && Timer < MordGolpe;
            if (marcando && !counterNoCiclo)
            {
                Vector2 alvo = Main.player[Math.Clamp(alvoMordida, 0, Main.maxPlayers - 1)].Center;
                Desenho.Brilho(alvo, new Color(255, 90, 160) * (0.5f + 0.4f * pulso), 1.3f);
                if (Dragao is DragaoDoEter d)
                    Desenho.Faixa(d.NPC.Center, alvo, 3f + 4f * pulso, new Color(255, 90, 160) * 0.35f, new Color(255, 190, 220));
            }

            if (CounterNoDragao)
                DesenharCounter();
        }

        private void SortearAlmas()
        {
            List<int> vivos = new();
            foreach (Player p in Main.ActivePlayers)
                if (!p.dead && JogadorNaRaid(p))
                    vivos.Add(p.whoAmI);

            mascaraEter = 0;
            modoSolo = vivos.Count <= 1;
            if (modoSolo)
            {
                foreach (int q in vivos) mascaraEter |= 1 << q;
            }
            else
            {
                // Embaralha e manda metade (arredondando para cima) para o Éter
                for (int i = vivos.Count - 1; i > 0; i--)
                {
                    int j = Main.rand.Next(i + 1);
                    (vivos[i], vivos[j]) = (vivos[j], vivos[i]);
                }
                int metade = (vivos.Count + 1) / 2;
                for (int i = 0; i < metade; i++)
                    mascaraEter |= 1 << vivos[i];
            }
            NPC.netUpdate = true;
        }

        private Player AlguemDeFora()
        {
            foreach (Player p in Main.ActivePlayers)
                if (!p.dead && JogadorNaRaid(p) && !EterEstrutura.Dentro(p))
                    return p;
            return null;
        }

        private bool NaPlataformaCerta(Player p) => Plataforma(simbolo).Intersects(p.Hitbox);

        /// <summary>Acertou se pelo menos um de dentro está na plataforma certa e ninguém está numa errada.</summary>
        private bool ResolverSimbolos()
        {
            bool algumCerto = false;
            foreach (Player p in Main.ActivePlayers)
            {
                if (p.dead || !EterEstrutura.Dentro(p)) continue;
                for (int i = 0; i < 3; i++)
                {
                    if (!Plataforma(i).Intersects(p.Hitbox)) continue;
                    if (i == simbolo) algumCerto = true;
                    else return false;
                }
            }
            return algumCerto;
        }

        // ═══════════════ TRANSIÇÃO: O ARCANJO ═══════════════
        private Rectangle Sombra => new((int)(sombraX - 130f), (int)ArenaTopo, 260, (int)(ArenaPiso - ArenaTopo));

        private void FazerFusao()
        {
            Player eu = Main.LocalPlayer;
            bool souCliente = Main.netMode != NetmodeID.Server;

            switch (etapa)
            {
                case 0: // os dois se fundem e a tela fica branca
                    if (Timer == 1)
                    {
                        fezFusao = true;
                        SalvarCheckpoint(T_FUSAO, new Vector2(CentroArena.X, ArenaPiso - 60f));
                        LimparPerigos();
                        Hud.Fase("TRANSIÇÃO: O Arcanjo", new Color(255, 225, 140));
                        Dialogo.Falar("Astharoth", "Vamos, irmão. Uma última vez.", CorAstharoth, 150);
                    }
                    NPC.velocity *= 0.9f;
                    Hud.ClaraoAlvo = Math.Min(1f, Timer / 110f);
                    if (Timer >= 130)
                    {
                        Fase = 3;
                        Hud.ClaraoAlvo = 0f;
                        AbrirPortao();
                        NPC.Center = new Vector2(CentroArena.X, ArenaTopo - 260f);
                        OrbesBoas = 0;
                        if (Servidor)
                        {
                            GerarOrbes();
                            sombraX = Main.rand.NextFloat(ArenaEsq + 150f, ArenaDir - 150f);
                        }
                        Hud.Aviso($"PEGUE {OrbesNecessarias} ORBES BOAS — OS ECOS REVELAM QUAIS ATORDOAM", new Color(255, 225, 140), 220);
                        etapa = 1;
                        Timer = 0;
                        NPC.netUpdate = true;
                    }
                    break;

                case 1: // orbes + sombra — ele vai para o FUNDO do céu e vira a luz; a cada volta completa, a rajada
                {
                    NPC.dontTakeDamage = true;
                    NPC.velocity = Vector2.Zero;

                    if (Timer < Entrada)
                    {
                        // Subindo para o fundo da cena
                        NPC.Center = Vector2.Lerp(NPC.Center, new Vector2(CentroArena.X, ArenaTopo - 300f), 0.04f);
                        if (Timer == 1) SoundEngine.PlaySound(SoundID.Item29 with { Pitch = -0.4f });
                        if (Timer == Entrada - 1)
                            Hud.Aviso("ELE VIROU A LUZ DO CÉU — QUANDO DER A VOLTA, FIQUE NA SOMBRA!", new Color(255, 230, 150), 220);
                    }
                    else
                    {
                        NPC.Center = new Vector2(CentroArena.X, ArenaTopo - 300f); // fica parado fora da vista; quem aparece é o céu
                        int passo = TempoNaVolta % VoltaTicks;

                        if (passo == 0 && Servidor)
                        {
                            // A sombra nasce perto de alguém (a 250–550 px), nunca do outro lado da arena
                            sombraX = MathHelper.Clamp(Alvo.Center.X + Main.rand.NextFloat(250f, 550f) * (Main.rand.NextBool() ? 1 : -1), ArenaEsq + 220f, ArenaDir - 220f);
                            NPC.netUpdate = true;
                        }
                        if (passo == (int)(VoltaTicks * 0.78f))
                            SoundEngine.PlaySound(SoundID.Item117 with { Pitch = -0.6f, Volume = 0.9f });

                        if (souCliente)
                        {
                            float carga = CargaDaVolta;
                            Hud.ClaraoAlvo = 0f;
                            if (carga > 0f)
                                ScreenShakeSystem.SetUniversalRumble(carga * 3f, MathHelper.TwoPi, null, 0.2f);
                        }

                        if (passo == VoltaTicks - 1) // completou a volta
                        {
                            Hud.Clarao(0.45f);
                            ArcanjoNoCeu.Explosao = 1f;
                            ScreenShakeSystem.StartShake(18f);
                            SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.5f, Volume = 1.2f });
                            SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.3f });
                            if (souCliente && JogadorNaRaid(eu) && !Sombra.Intersects(eu.Hitbox))
                                MatarLocal(eu);
                        }
                    }

                    if (OrbesBoas >= OrbesNecessarias || Timer >= TempoOrbes)
                    {
                        Hud.ClaraoAlvo = 0f;
                        etapa = 2;
                        Timer = 0;
                    }
                    break;
                }

                case 2: // barreira e o MERGULHO: estrela cadente
                    if (Timer == 1)
                    {
                        if (souCliente && OrbesBoas >= OrbesNecessarias && JogadorNaRaid(eu))
                            eu.AddBuff(ModContent.BuffType<BarreiraEterea>(), 900);
                        Hud.Aviso(OrbesBoas >= OrbesNecessarias ? "A BARREIRA SE ERGUE!" : "SEM BARREIRA...", new Color(255, 225, 140), 120);
                        if (Servidor)
                            foreach (Projectile p in Main.ActiveProjectiles)
                                if (p.type == ModContent.ProjectileType<OrbeEterea>())
                                    p.Kill();
                        SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.4f });
                    }

                    if (Timer < MergulhoSobe)
                    {
                        // Sobe rápido e some lá em cima
                        NPC.velocity = Vector2.Zero;
                        NPC.Center = Vector2.Lerp(NPC.Center, new Vector2(CentroArena.X, ArenaTopo - 900f), 0.08f);
                        NPC.Opacity = Utils.GetLerpValue(MergulhoSobe, MergulhoSobe * 0.4f, Timer, true);
                    }
                    else if (Timer < MergulhoCai)
                    {
                        // A estrela brilha no alto da tela, bem em cima de onde ele vai cair
                        NPC.Center = new Vector2(CentroArena.X, ArenaTopo - 900f);
                        if (Timer == MergulhoSobe)
                        {
                            Hud.Aviso("ELE VAI CAIR DO CÉU!", new Color(255, 230, 150), 110);
                            SoundEngine.PlaySound(SoundID.Item9 with { Pitch = 0.8f, Volume = 1.2f });
                        }
                        if (Timer == MergulhoCai - 25)
                            SoundEngine.PlaySound(SoundID.Item163 with { Pitch = -0.4f, Volume = 1.1f });
                    }
                    else
                    {
                        // Cai com tudo
                        NPC.Opacity = 1f;
                        if (Timer == MergulhoCai)
                        {
                            NPC.velocity = new Vector2(0f, 24f);
                            ScreenShakeSystem.StartShake(6f);
                        }
                        NPC.velocity.X = 0f;
                        NPC.velocity.Y = Math.Min(NPC.velocity.Y + 3f, 72f);
                        NPC.rotation = 0f;
                        if (souCliente)
                            for (int k = 0; k < 4; k++)
                                Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(30f, 50f), DustID.GoldFlame, new Vector2(Main.rand.NextFloat(-3f, 3f), -6f), 0, default, 2f).noGravity = true;

                        if (NPC.Center.Y + NPC.velocity.Y >= ArenaPiso - 64f)
                        {
                            NPC.Center = new Vector2(CentroArena.X, ArenaPiso - 64f);
                            NPC.velocity = Vector2.Zero;
                            IniciarImpacto(NPC.Center + new Vector2(0f, 60f), new Color(255, 215, 130));
                            if (souCliente && JogadorNaRaid(eu) && !eu.HasBuff(ModContent.BuffType<BarreiraEterea>()))
                                MatarLocal(eu);
                            NPC.dontTakeDamage = false;
                            SalvarCheckpoint(PAUSA, new Vector2(CentroArena.X, ArenaPiso - 60f));
                            Hud.Fase("ETAPA FINAL", new Color(255, 225, 140));
                            IrPara(ATORDOADO);
                        }
                    }
                    break;
            }
        }

        private void GerarOrbes()
        {
            // as boas (o que precisa pegar) e as que atordoam, embaralhadas pelo chão da arena
            int[] tipos = new int[OrbesNecessarias + OrbesRuins];
            for (int k = OrbesNecessarias; k < tipos.Length; k++) tipos[k] = 1;
            for (int i = tipos.Length - 1; i > 0; i--)
            {
                int j = Main.rand.Next(i + 1);
                (tipos[i], tipos[j]) = (tipos[j], tipos[i]);
            }
            for (int i = 0; i < tipos.Length; i++)
            {
                float x = MathHelper.Lerp(ArenaEsq + 80f, ArenaDir - 80f, i / (float)(tipos.Length - 1));
                Projectile.NewProjectile(NPC.GetSource_FromAI(), new Vector2(x, ArenaPiso - 50f - (i % 2) * 70f), Vector2.Zero,
                    ModContent.ProjectileType<OrbeEterea>(), 0, 0f, Main.myPlayer, NPC.whoAmI, tipos[i], i * 37);
            }
        }

        // ═══════════════ FIM ═══════════════
        public override bool CheckDead()
        {
            if (morteLiberada)
                return true;
            NPC.life = 1;
            NPC.dontTakeDamage = true;
            LimparPerigos();
            IrPara(DESPEDIDA);
            return false;
        }

        private void FazerDespedida()
        {
            NPC.dontTakeDamage = true;
            NPC.velocity *= 0.9f;
            if (Timer == 1) Hud.Clarao(0.6f);
            if (Timer == 20) Dialogo.Falar("Astharoth", "...Então é assim que um errante para de andar.", CorAstharoth, 180);
            if (Timer == 220) Dialogo.Falar("Astharoth", "Astra está vindo. Quando ela se partir, não vai sobrar céu para ninguém.", CorAstharoth, 200);

            // Antes de sumir, devolve todo mundo para fora do Éter
            if (Timer == 440 && Main.netMode != NetmodeID.Server)
            {
                Player eu = Main.LocalPlayer;
                if (EterEstrutura.Dentro(eu))
                    RaidJogador.Teleportar(eu, CentroFora);
            }

            if (Timer >= 520 && Servidor)
            {
                morteLiberada = true;
                NPC.dontTakeDamage = false;
                NPC.StrikeInstantKill();
            }
        }

        public override void OnKill()
        {
            MundoDeRaid.Vencida = true;
            AstralMundo.DerrotouAstharoth = true;
            EterEstrutura.Demolir();
            AstralMundo.SincronizarMundo();
            AstralMundo.Mensagem("Astra está cada vez maior no céu...", new Color(130, 190, 255));
            Dialogo.FalaDoEco("Ele não lutava por ódio. Lutava porque alguém precisava segurar a porta.");
            Hud.SimboloVisivel = -1;
            Hud.ClaraoAlvo = 0f;
        }

        // ═══════════════ DESENHO ═══════════════
        /// <summary>Sombra que surge aos poucos, luz carregando, a estrela lá no alto e o impacto.</summary>
        public override void DesenharEfeitosDeMundo(SpriteBatch sb)
        {
            base.DesenharEfeitosDeMundo(sb);
            Texture2D nevoa = ModContent.Request<Texture2D>("AscensaoAstral/Assets/Nevoa").Value;
            float t = Main.GlobalTimeWrappedHourly;
            float altura = ArenaPiso - ArenaTopo;

            if (Estado == T_FUSAO && etapa == 1)
            {
                // A sombra vai surgindo ao longo da volta inteira (o tempo passando) e fica forte no fim
                float alfa = TempoNaVolta < 0 ? 0f : 0.15f + 0.85f * MathF.Pow(ProgressoDaVolta, 1.3f);
                float carga = CargaDaVolta;

                // Luz dourada caindo em tudo que NÃO é a sombra
                if (carga > 0f)
                    Desenho.Raios(RetanguloArena, new Color(255, 215, 130), carga, Sombra.Left, Sombra.Right);

                // A sombra: uma coluna escura de bordas suaves, com fiapos subindo
                Vector2 meio = new(sombraX, ArenaTopo + altura * 0.5f);
                for (int k = 0; k < 3; k++)
                    sb.Draw(nevoa, meio - Main.screenPosition, null, new Color(25, 12, 45) * alfa * (0.55f + k * 0.15f), 0f, nevoa.Size() / 2f,
                        new Vector2((1.9f - k * 0.35f) * Sombra.Width / 256f, altura * 1.35f / 256f), 0, 0f);
                for (int i = 0; i < 10; i++)
                {
                    float y = ArenaPiso - ((t * 60f + i * 97f) % altura);
                    float x = sombraX + MathF.Sin(t * 1.3f + i * 2.1f) * Sombra.Width * 0.35f;
                    Desenho.Brilho(new Vector2(x, y), new Color(150, 110, 255) * alfa * 0.5f, 0.14f);
                }
                // Marca no chão
                Desenho.Brilho(new Vector2(sombraX, ArenaPiso - 8f), new Color(160, 120, 255) * alfa, 1.1f);
                // Bordas da sombra bem marcadas (linhas de luz roxa dos dois lados)
                Texture2D px = TextureAssets.MagicPixel.Value;
                int topoT = (int)(ArenaTopo - Main.screenPosition.Y), alt = (int)altura;
                foreach (int borda in new[] { Sombra.Left, Sombra.Right })
                    sb.Draw(px, new Rectangle(borda - (int)Main.screenPosition.X - 1, topoT, 3, alt), new Rectangle(0, 0, 1, 1), new Color(190, 150, 255) * alfa * 0.8f);

                // Se a sombra está fora da tela, uma seta na borda aponta para ela
                float telaEsq = Main.screenPosition.X + 60f, telaDir = Main.screenPosition.X + Main.screenWidth - 60f;
                if (alfa > 0.05f && (sombraX < telaEsq || sombraX > telaDir))
                {
                    bool direita = sombraX > telaDir;
                    Vector2 seta = new(direita ? telaDir : telaEsq, Main.screenPosition.Y + Main.screenHeight * 0.5f);
                    float pulsoSeta = 0.6f + 0.4f * MathF.Sin(t * 8f);
                    for (int k = 0; k < 4; k++)
                        Desenho.Brilho(seta + new Vector2((direita ? -1 : 1) * k * 18f, 0f), new Color(200, 160, 255) * pulsoSeta * (1f - k * 0.2f), 0.35f - k * 0.05f);
                    Utils.DrawBorderString(sb, direita ? "SOMBRA >>" : "<< SOMBRA", seta - Main.screenPosition + new Vector2(direita ? -70f : 70f, -30f), new Color(220, 190, 255), 0.9f, 0.5f, 0.5f);
                }
            }

            if (Estado == T_FUSAO && etapa == 2 && Timer >= MergulhoSobe && Timer < MergulhoCai)
                DesenharAvisoDeQueda(sb, CentroArena.X, Utils.GetLerpValue(MergulhoSobe, MergulhoCai, Timer, true), new Color(255, 220, 140));
        }

        public override bool CanHitPlayer(Player target, ref int cooldownSlot) => Estado == PASSO_DO_ETER && NPC.Opacity > 0.9f && (int)(Timer - 1) % 48 >= 30 && NPC.velocity.Length() > 1.5f; // só durante o impulso do corte

        public override bool PreDraw(SpriteBatch sb, Vector2 screenPos, Color drawColor)
        {
            DesenharMecanicas();
            if (!CounterNoDragao) DesenharCounter(); // no dragão, quem desenha é o próprio dragão (ele está sempre na tela)

            Texture2D tex = TextureAssets.Npc[Type].Value;
            bool orbitando = Estado == T_FUSAO && etapa == 1;
            bool mergulho = Estado == T_FUSAO && etapa == 2;
            if (orbitando && Timer >= Entrada)
                return false; // agora ele está NO CÉU (CeuDasNuvens desenha ele como a luz)
            float prof = orbitando ? 1f - Timer / Entrada : 1f; // encolhe e apaga enquanto vai para o fundo
            float escala = orbitando ? MathHelper.Lerp(0.3f, 1.8f, prof) : mergulho ? 1.8f : NPC.scale;

            if ((mergulho && Timer >= MergulhoCai) || orbitando)
                DesenharRastroDeEstrela(new Color(255, 215, 120), new Color(255, 120, 40));
            Color tinta = Fase >= 3 ? new Color(255, 235, 170) : Color.White;
            Color cor = NPC.GetAlpha(Color.Lerp(drawColor, Color.White, 0.85f)).MultiplyRGB(tinta);
            if (EstaAtordoado) cor = Color.Lerp(cor, new Color(255, 255, 60), 0.55f);
            else if (JanelaAberta) cor = TintaDeCounter(cor);
            else if (FlashDano > 0) cor = Color.Lerp(cor, new Color(255, 120, 120), 0.6f);
            if (orbitando) // indo para o fundo ele vira só luz
                cor = Color.Lerp(new Color(255, 235, 190, 0), cor, prof) * prof;

            if (Fase >= 3)
            {
                // Halo girando atrás dele
                float giro = Main.GlobalTimeWrappedHourly * 0.8f;
                for (int k = 0; k < 24; k++)
                {
                    float ang = k / 24f * MathHelper.TwoPi + giro;
                    Desenho.Brilho(NPC.Center + ang.ToRotationVector2() * 70f * escala, new Color(255, 225, 150) * (0.4f + 0.6f * prof) * NPC.Opacity, 0.16f * escala);
                }
            }

            if (Fase >= 3)
            {
                // Asas de luz (provisórias)
                Desenho.Brilho(NPC.Center + new Vector2(-70f, -20f) * escala, new Color(255, 230, 150), 2.2f * escala);
                Desenho.Brilho(NPC.Center + new Vector2(70f, -20f) * escala, new Color(255, 230, 150), 2.2f * escala);
            }
            Desenho.Brilho(NPC.Center, Fase >= 3 ? new Color(255, 220, 120) : new Color(150, 90, 255), 2f * escala);

            SpriteEffects flip = Alvo.Center.X < NPC.Center.X ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            DesenharPosImagens(sb, tex, null, tex.Size() / 2f, escala, Fase >= 3 ? new Color(255, 220, 140) : new Color(160, 100, 255), flip);
            sb.Draw(tex, NPC.Center - screenPos + new Vector2(0f, NPC.gfxOffY), null, TintaDeMemoria(cor) * NPC.Opacity, NPC.rotation, tex.Size() / 2f, escala, flip, 0f);
            return false;
        }

        private void DesenharMecanicas()
        {
            float pulso = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 10f);

            // Passo do Éter: a fenda no espaço de onde ele vai sair (vertical, abrindo)
            if (Estado == PASSO_DO_ETER)
            {
                int t = (int)(Timer - 1) % 48;
                if (t < 30 && (int)(Timer - 1) / 48 < 5)
                {
                    float k = t / 30f;
                    Color cor = Fase >= 3 ? new Color(255, 215, 140) : new Color(190, 120, 255);
                    Desenho.Golpe(Desenho.Estilo.Eter, alvoCorte + new Vector2(0f, -90f * (0.4f + 0.6f * k)), alvoCorte + new Vector2(0f, 90f * (0.4f + 0.6f * k)), 4f + 22f * k, cor, 1f + k * 0.5f, 0.5f + 0.5f * k);
                    Desenho.Clarao(alvoCorte, cor, 0.25f + 0.35f * k * pulso, t * 0.05f);
                }
            }


            // Plataformas dos símbolos (dentro do Éter)
            if (Estado == T_ALMA && etapa == 2)
                for (int i = 0; i < 3; i++)
                {
                    Rectangle r = Plataforma(i);
                    Desenho.RetanguloAviso(r, Hud.CorDoSimbolo(i), 0.4f);
                    Hud.DesenharSimbolo(Main.spriteBatch, r.Center.ToVector2() - Main.screenPosition + new Vector2(0f, -70f), i, 26f);
                }

            // O ECO do símbolo certo pisca no céu do Éter: dá para resolver sozinho (com grupo, os de fora veem ele fixo)
            if (Estado == T_ALMA && etapa == 2)
            {
                float ciclo = (Timer + 110f) % 150f;
                if (ciclo < 40f)
                {
                    float a = MathF.Sin(ciclo / 40f * MathHelper.Pi);
                    Vector2 ceu = new(EterEstrutura.Arena.Center.X, EterEstrutura.Arena.Top + 300f);
                    Desenho.Brilho(ceu, Hud.CorDoSimbolo(simbolo) * a, 2.2f);
                    Hud.DesenharSimbolo(Main.spriteBatch, ceu - Main.screenPosition, simbolo, 34f * (0.9f + 0.1f * a));
                }
            }

            // Rasante: seta piscando do lado de onde o dragão vem (o dragão de verdade está lá)
            if (Estado == RASANTE && Timer < 100)
            {
                Vector2 seta = Main.LocalPlayer.Center + new Vector2(ladoDragao * 380f, 0f);
                Desenho.Brilho(seta, new Color(200, 140, 255), 1.2f + pulso * 0.4f);
            }

            // (a sombra, a estrela e o impacto são desenhados em EfeitosDoArcanjo, fora do NPC, para não sumirem fora da tela)
        }
    }

    /// <summary>
    /// Orbe do Arcanjo. ai[0] = Astharoth, ai[1] = 0 boa / 1 atordoa, ai[2] = semente do "eco".
    /// Todas parecem iguais; de tempos em tempos um ECO revela a cor verdadeira por um instante.
    /// </summary>
    public class OrbeEterea : ModProjectile
    {
        public override string Texture => "AscensaoAstral/Assets/Vazio";

        public override void SetDefaults()
        {
            Projectile.width = 36;
            Projectile.height = 36;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60 * 60;
        }

        private bool Atordoa => Projectile.ai[1] == 1f;
        private bool Revelando => ((Projectile.localAI[0] + Projectile.ai[2]) % 200f) < 45f;

        public override void AI()
        {
            Projectile.localAI[0]++;
            Projectile.velocity = new Vector2(0f, MathF.Sin(Projectile.localAI[0] * 0.05f + Projectile.ai[2]) * 0.4f);

            if (Main.npc[(int)Projectile.ai[0]].ModNPC is not Astharoth dono || !dono.NPC.active)
            {
                Projectile.Kill();
                return;
            }

            // Efeito local de quem pegou uma orbe ruim
            if (Main.netMode != NetmodeID.Server && Atordoa)
            {
                Player eu = Main.LocalPlayer;
                if (!eu.dead && eu.Hitbox.Intersects(Projectile.Hitbox))
                {
                    eu.AddBuff(BuffID.Frozen, 120);
                    CombatText.NewText(eu.Hitbox, new Color(190, 110, 255), "Atordoado!", true);
                }
            }

            // O servidor conta as orbes boas e remove as pegas
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                foreach (Player p in Main.ActivePlayers)
                {
                    if (p.dead || !p.Hitbox.Intersects(Projectile.Hitbox)) continue;
                    if (!Atordoa)
                    {
                        dono.OrbesBoas++;
                        dono.NPC.netUpdate = true;
                        CombatText.NewText(p.Hitbox, new Color(255, 225, 140), $"{dono.OrbesBoas}/{Astharoth.OrbesNecessarias}", true);
                    }
                    SoundEngine.PlaySound(SoundID.Item29 with { Pitch = Atordoa ? -0.6f : 0.4f }, Projectile.Center);
                    Projectile.Kill();
                    return;
                }
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Color neutra = new(230, 230, 255);
            Color verdadeira = Atordoa ? new Color(190, 90, 255) : new Color(255, 215, 110);
            Color cor = Revelando ? verdadeira : neutra;
            float flutua = MathF.Sin(Projectile.localAI[0] * 0.06f + Projectile.ai[2]) * 4f;
            Vector2 c = Projectile.Center + new Vector2(0f, flutua);

            float idade = Projectile.localAI[0];
            float entra = Utils.GetLerpValue(0f, 30f, idade, true);
            // revelar suave (sobe e desce em vez de trocar de cor de uma vez)
            float fase = (idade + Projectile.ai[2]) % 200f;
            float revela = Utils.GetLerpValue(0f, 8f, fase, true) * Utils.GetLerpValue(45f, 37f, fase, true);

            // coluna de luz até o chão: dá para achar a orbe de longe
            Desenho.Risco(c + new Vector2(0f, -260f), c + new Vector2(0f, 260f), 26f, Color.Lerp(neutra, verdadeira, revela) * 0.22f * entra);
            // sombra escura atrás (a orbe aparece até contra o céu claro do meio-dia)
            Texture2D circulo = ModContent.Request<Texture2D>("AscensaoAstral/Assets/Circulo").Value;
            Main.spriteBatch.Draw(circulo, c - Main.screenPosition, null, new Color(10, 6, 26) * 0.7f * entra, 0f, circulo.Size() / 2f, 60f / circulo.Width, 0, 0f);
            // a esfera
            Desenho.Orbe(c, 92f * (0.6f + 0.4f * entra), new Color(200, 205, 255), verdadeira, revela, Projectile.ai[2] * 0.013f, entra);
            // três faíscas orbitando com rastro
            for (int i = 0; i < 3; i++)
            {
                float ang = idade * 0.07f + i / 3f * MathHelper.TwoPi;
                for (int k = 0; k < 6; k++)
                {
                    float a2 = ang - k * 0.09f;
                    Vector2 o = c + new Vector2(MathF.Cos(a2) * 40f, MathF.Sin(a2) * 14f);
                    Desenho.Brilho(o, Color.Lerp(neutra, verdadeira, revela) * (1f - k / 6f) * entra, 0.07f);
                }
            }
            Desenho.Clarao(c, Color.Lerp(neutra, verdadeira, revela), (0.25f + 0.25f * revela) * entra, idade * 0.02f);
            Lighting.AddLight(c, cor.ToVector3() * 0.6f);
            if (Revelando)
            {
                // O eco: uma silhueta de luz por cima da orbe, na cor verdadeira
                Desenho.Clarao(c + new Vector2(0f, -62f), verdadeira, 0.35f * revela, 0f);
            }
            return false;
        }
    }
}
