using System.Collections.Generic;
using System;
using System.IO;
using AscensaoAstral.Common;
using AscensaoAstral.Common.Raid;
using AscensaoAstral.Content.Buffs;
using AscensaoAstral.Content.Projectiles;
using Luminance.Assets;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent.ItemDropRules;
using Terraria.ID;
using Terraria.ModLoader;

namespace AscensaoAstral.Content.Bosses.Silvanna
{
    /// <summary>
    /// SILVANNA E ARMOTHEUS (pós Queen Slime, bioma da neve) — chefe do MUNDO, sem dimensão: luta onde for chamada.
    /// Fora da neve ela fica enfurecida (ataques 50% mais fortes, pausas pela metade).
    ///
    /// IDENTIDADE: a chefe do TERRENO. Tudo dela é gelo físico — quica, rola pelo chão de verdade, crava e estilhaça —
    /// e os avisos são do mundo (geada se espalhando, sombra no chão, pingente rachando). Os retângulos de aviso,
    /// cortes no espaço, chuvas de estrela e anéis de projéteis ficam com o Astharoth.
    ///
    ///   Intro       Silvanna surge e arremessa o chapéu (Armotheus)
    ///   ETAPA 1     Chapéu: Pêndulo · Ricochete (quica até você soltando lascas) · Rasteira (passa rente ao chão; a geada vira espinho)
    ///   TRANSIÇÃO   75%: o Giro — redemoinho de neve que puxa todos; segure-se com o GANCHO e quebre a postura.
    ///               Encostar machuca e joga longe; o tempo acabar faz o redemoinho explodir em gelo (a luta segue)
    ///   ETAPA 2     Forma verdadeira: Pingentes gigantes · Clones · Salto (o impacto racha o chão em espinhos) · Avalanche (Mítico)
    ///   TRANSIÇÃO   45%: Nevasca; Armotheus vira cavaleiro e segura o vento — fique ATRÁS dele (no Mítico o abrigo encolhe)
    ///   ÚLTIMA      Cavaleiro: lança · espinhos · deslize glacial · onda do escudo · sopro · prisão
    ///   FINAL       15%: o Inverno Eterno — ele congela a lua; quebre as estátuas e pegue os escudos antes dela cair
    ///   Fim         Silvanna conversa e vai embora
    /// </summary>
    [AutoloadBossHead]
    public class RaidSilvanna : RaidBoss
    {
        // ─── Estados ───
        // A Silvanna é a chefe do TERRENO: tudo dela é gelo físico — quica, rola, crava no chão e estilhaça.
        // (Os cortes no espaço, os retângulos de aviso, as chuvas de estrela e os anéis são do Astharoth.)
        private const int INTRO = 1, PAUSA = 2, PENDULO = 3, RICOCHETE = 4, RASTEIRA = 5, ATORDOADO = 6,
            T_GIRO = 7, PINGENTES = 8, CLONES = 9, SALTO = 10, T_NEVASCA = 11, LANCA = 12, INVESTIDA = 13, ESCUDO = 14,
            FINAL = 15, DESPEDIDA = 16,
            AVALANCHE = 18,
            // Cavaleiro de gelo (etapa final)
            ESPINHOS = 19, PRISAO = 21, SOPRO = 22;

        public override IEnumerable<Common.Raid.Vitrine.Item> AtaquesDaVitrine(bool comMecanicas)
        {
            yield return new(PENDULO, 1, "Pendulo"); yield return new(RICOCHETE, 1, "Ricochete"); yield return new(RASTEIRA, 1, "Rasteira");
            yield return new(PINGENTES, 2, "Pingentes"); yield return new(CLONES, 2, "Clones"); yield return new(SALTO, 2, "Salto");
            yield return new(AVALANCHE, 2, "Avalanche");
            yield return new(LANCA, 3, "Lanca"); yield return new(ESPINHOS, 3, "Espinhos"); yield return new(INVESTIDA, 3, "Investida");
            yield return new(PRISAO, 3, "Prisao"); yield return new(ESCUDO, 3, "Escudo"); yield return new(SOPRO, 3, "Sopro");
            if (!comMecanicas) yield break;
            yield return new(T_GIRO, 1, "M_Giro", true); yield return new(T_NEVASCA, 2, "M_Nevasca", true); yield return new(FINAL, 3, "M_Final", true);
        }

        // ─── Balanceamento ───
        private const float VelMover = 11f;
        private const float LimiarGiro = 0.75f, LimiarNevasca = 0.45f, LimiarFinal = 0.15f;
        // Mecânicas grandes: mais curtas no Normal, tempo cheio no Mítico (veja Common/Raid/Dificuldade.cs)
        private static int TempoGiro => Dificuldade.Escolher(15, 20, 25) * 60;
        private static int TempoNevasca => Dificuldade.Escolher(20, 25, 30) * 60;
        private static int TempoFinal => Dificuldade.Escolher(20, 25, 30) * 60;

        // Rotações: cada nível libera mais ataques
        private static int[] RotacaoForma => Dificuldade.Escolher(
            new[] { PINGENTES, SALTO },
            new[] { PINGENTES, CLONES, SALTO },
            new[] { PINGENTES, CLONES, SALTO, AVALANCHE });
        private static int[] RotacaoCavaleiro => Dificuldade.Escolher(
            new[] { LANCA, ESPINHOS, INVESTIDA, ESCUDO },
            new[] { LANCA, ESPINHOS, INVESTIDA, ESCUDO, SOPRO },
            new[] { LANCA, ESPINHOS, INVESTIDA, PRISAO, ESCUDO, SOPRO });
        private const float PuxaoGiro = 9f, PuxaoComGancho = 0.8f;
        private static readonly Color CorSilvanna = new(120, 210, 230);

        public override float StaggerMax => 1500f;
        public override float MetaStagger => Estado == T_GIRO || Estado == T_NEVASCA ? 1600f : StaggerMax;
        public override bool PortaoAtivo => (Estado == T_GIRO || Estado == T_NEVASCA || Estado == FINAL) && etapa >= 1;
        public override bool MostrarBarraDeQuebra => Estado != FINAL && Estado != INTRO && Estado != DESPEDIDA;
        public override bool EstaAtordoado => Estado == ATORDOADO;
        protected override bool PausarStagger => Estado == INTRO || Estado == DESPEDIDA;
        public override float TempoRestantePortao => !PortaoAtivo ? -1f : Estado switch
        {
            T_GIRO => 1f - Timer / TempoGiro,
            T_NEVASCA => 1f - Timer / TempoNevasca,
            FINAL when etapa == 1 => 1f - Timer / TempoFinal,
            _ => -1f
        };

        public override string NomeDoEstado => Estado switch
        {
            INTRO => "Intro", PAUSA => "Pausa", PENDULO => "Pendulo", RICOCHETE => "Ricochete", RASTEIRA => "Rasteira",
            ATORDOADO => "Atordoado", T_GIRO => "T:Giro", PINGENTES => "Pingentes", CLONES => "Clones", SALTO => "Salto",
            T_NEVASCA => "T:Nevasca", LANCA => "Lanca", INVESTIDA => "Investida", ESCUDO => "Escudo", FINAL => "Final",
            DESPEDIDA => "Despedida", AVALANCHE => "Avalanche",
            ESPINHOS => "Espinhos", PRISAO => "Prisao", SOPRO => "Sopro", REINICIANDO => "Reiniciando", _ => Estado.ToString()
        };

        // ─── Dados extras ───
        private bool fezGiro, fezNevasca, fezFinal, morteLiberada;
        private int ladoErrado = 1;     // clones: lado do clone de cor errada
        private int ladoAtaque = 1;     // varredura / investida
        private Vector2 posSilvanna;
        private Vector2 alvoSalto, inicioSalto;
        private float tPendulo;
        private Vector2 inicioPendulo;
        private float fimPenduloX;

        protected override void EscreverExtra(BinaryWriter w)
        {
            w.Write(new BitsByte(fezGiro, fezNevasca, fezFinal, morteLiberada));
            w.Write(ladoErrado); w.Write(ladoAtaque);
            w.WriteVector2(posSilvanna); w.WriteVector2(alvoSalto); w.WriteVector2(inicioSalto); w.WriteVector2(inicioPendulo); w.Write(fimPenduloX);
        }

        protected override void LerExtra(BinaryReader r)
        {
            BitsByte b = r.ReadByte();
            fezGiro = b[0]; fezNevasca = b[1]; fezFinal = b[2]; morteLiberada = b[3];
            ladoErrado = r.ReadInt32(); ladoAtaque = r.ReadInt32();
            posSilvanna = r.ReadVector2(); alvoSalto = r.ReadVector2(); inicioSalto = r.ReadVector2(); inicioPendulo = r.ReadVector2(); fimPenduloX = r.ReadSingle();
        }

        // Com o Calamity: no nível do Cryogen (25 mil)
        protected override int VidaComCalamity => Revanche ? 220000 : 25000; // revanche: depois da Providence

        /// <summary>
        /// A REVANCHE: depois do Moon Lord (com o Calamity, depois da Providence), a Silvanna NPC aceita o desafio.
        /// É a mesma luta, dentro da dimensão dela, com Armotheus sem se segurar: tudo no Mítico, números de fim de jogo.
        /// </summary>
        public static bool Revanche => Raids.Atual is ArenaDaSilvanna;

        public override void ModifyTypeName(ref string typeName)
        {
            base.ModifyTypeName(ref typeName);
            if (Revanche) typeName = "Armotheus, o Inverno Eterno";
        }

        public override void SetBestiary(Terraria.GameContent.Bestiary.BestiaryDatabase database, Terraria.GameContent.Bestiary.BestiaryEntry bestiaryEntry)
        {
            bestiaryEntry.Info.AddRange(new Terraria.GameContent.Bestiary.IBestiaryInfoElement[]
            {
                Terraria.GameContent.Bestiary.BestiaryDatabaseNPCsPopulator.CommonTags.SpawnConditions.Biomes.Snow,
                new Terraria.GameContent.Bestiary.FlavorTextBestiaryInfoElement("Mods.AscensaoAstral.Bestiario.Silvanna")
            });
        }

        public override void ModifyNPCLoot(NPCLoot npcLoot) => Items.LootDosChefes.Silvanna(npcLoot);
        public override void BossLoot(ref int potionType) => potionType = ItemID.GreaterHealingPotion;

        protected override bool JaFoiDerrotado => !Revanche && AstralMundo.DerrotouSilvanna; // na revanche ela está viva e lutando de verdade

        protected override bool ConfinarAgora => Estado != INTRO && Estado != DESPEDIDA && Estado != REINICIANDO;
        protected override Color CorDaParede => Fase >= 2 ? new Color(255, 110, 110) : new Color(120, 210, 255);
        protected override Color CorDasFaiscas => new(170, 230, 255);

        private Vector2 PosOcioso => new(CentroArena.X, Chao(CentroArena.X) - 110f);
        private Vector2 PosCavaleiroNoChao => new(NPC.Center.X, ArenaPiso - 62f);

        // ─── Setup ───
        public override void SetStaticDefaults()
        {
            Main.npcFrameCount[Type] = 1;
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.BossBestiaryPriority.Add(Type);
            NPCID.Sets.TrailCacheLength[Type] = 16;
            NPCID.Sets.TrailingMode[Type] = 3;
        }

        public override void SetDefaults()
        {
            NPC.width = 90;
            NPC.height = 90;
            // Equilíbrio (ver o documento): entre o Cryogen e os Gêmeos. Expert = 48.000, Master = 72.000
            NPC.damage = 60;
            NPC.defense = 24;
            NPC.lifeMax = 24000;
            if (Revanche) // pós-Moon Lord: perto do Moon Lord inteiro no Expert (sem o Calamity)
            {
                NPC.damage = 150;
                NPC.defense = 70;
                NPC.lifeMax = 130000;
                NPC.value = Item.buyPrice(gold: 40);
            }
            NPC.HitSound = SoundID.NPCHit5;
            NPC.DeathSound = SoundID.NPCDeath7;
            NPC.value = Item.buyPrice(gold: 15);
            NPC.scale = 1.35f;
            Music = MusicID.Boss2;
            ConfigurarRaid();
        }

        protected override void AoReiniciar()
        {
            Hud.ClaraoAlvo = 0f;
            relogioCeu = 0;
            LimparPerigos();
            foreach (NPC n in Main.ActiveNPCs)
                if (n.type == ModContent.NPCType<EstatuaViva>())
                    n.active = false;
            RaidJogador.Foco = null;

            NPC.Center = checkpointEstado == T_NEVASCA ? new Vector2(ArenaEsq + 110f, ArenaPiso - 62f) : PosOcioso;
            if (checkpointEstado == T_GIRO) fezGiro = false;
            if (checkpointEstado == T_NEVASCA) fezNevasca = false;
            if (checkpointEstado == FINAL) fezFinal = false;
        }

        // ═══════════════ MÁQUINA DE ESTADOS ═══════════════
        protected override void ExecutarEstado()
        {
            Music = Fase >= 3 ? MusicID.OtherworldlyBoss1 : MusicID.Boss2;

            switch (Estado)
            {
                case INTRO: FazerIntro(); break;
                case PAUSA: FazerPausa(); break;
                case PENDULO: FazerPendulo(); break;
                case RICOCHETE: FazerRicochete(); break;
                case RASTEIRA: FazerRasteira(); break;
                case ATORDOADO: FazerAtordoado(); break;
                case T_GIRO: FazerGiro(); break;
                case PINGENTES: FazerPingentes(); break;
                case CLONES: FazerClones(); break;
                case SALTO: FazerSalto(); break;
                case T_NEVASCA: FazerNevasca(); break;
                case LANCA: FazerLanca(); break;
                case INVESTIDA: FazerInvestida(); break;
                case ESCUDO: FazerEscudo(); break;
                case FINAL: FazerFinal(); break;
                case DESPEDIDA: FazerDespedida(); break;
                case AVALANCHE: FazerAvalanche(); break;
                case ESPINHOS: FazerEspinhos(); break;
                case PRISAO: FazerPrisao(); break;
                case SOPRO: FazerSopro(); break;
                default: IrPara(INTRO); break;
            }

            AtualizarFiltros();
            AtualizarCavaleirosDoCeu();
            Lighting.AddLight(NPC.Center, Fase >= 3 ? new Vector3(0.4f, 0.8f, 1.2f) : new Vector3(1f, 0.4f, 0.35f));
        }

        // ─── Intro: Silvanna arremessa o chapéu ───
        private void FazerIntro()
        {
            NPC.dontTakeDamage = true;
            if (Timer == 1)
            {
                posSilvanna = new Vector2(ArenaDir - 130f, Chao(ArenaDir - 130f) - 64f);
                NPC.Center = posSilvanna + new Vector2(0f, -70f);
                NPC.Opacity = 1f;
                RaidJogador.Foco = posSilvanna;
                RaidJogador.ForcaFoco = 0.85f;
                NPC.netUpdate = true;
            }
            if (Revanche)
            {
                if (Timer == 20) Dialogo.Falar("Silvanna", "Você derrubou a Lua. Então acabou a desculpa para ele se segurar.", CorSilvanna, 170);
                if (Timer == 200) Dialogo.Falar("Silvanna", "Armotheus. Tudo. O inverno inteiro.", CorSilvanna, 150);
            }
            else
            {
                if (Timer == 20) Dialogo.Falar("Silvanna", "Então foi você quem arrancou a Astrânia da terra...", CorSilvanna, 170);
                if (Timer == 200) Dialogo.Falar("Silvanna", "Armotheus. Mostre a essa pessoa o tamanho do erro dela.", CorSilvanna, 150);
            }

            if (Timer >= 330 && Timer < 400)
            {
                // O arremesso: um arco do topo da cabeça dela até o centro da arena
                float s = (Timer - 330) / 70f;
                Vector2 de = posSilvanna + new Vector2(0f, -70f);
                NPC.Center = Vector2.Lerp(de, PosOcioso, s) + new Vector2(0f, -MathF.Sin(s * MathHelper.Pi) * 220f);
                NPC.rotation += 0.35f;
                RaidJogador.Foco = NPC.Center;
                if (Timer == 330) SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.4f }, NPC.Center);
            }

            if (Timer == 400)
            {
                NPC.rotation = 0f;
                NPC.Center = PosOcioso;
                RaidJogador.Foco = null;
                Fase = 1;
                NPC.dontTakeDamage = false;
                ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 8f);
                Hud.Fase("ETAPA 1: O Chapéu", new Color(255, 110, 90));
                SalvarCheckpoint(PAUSA, PosOcioso);
                IrPara(PAUSA);
            }
        }

        // ═══════════════ NO MUNDO DE VERDADE ═══════════════
        // A Silvanna não tem dimensão: ela luta onde foi chamada, como a Queen Slime ou o Deerclops.
        // A "arena" é só uma referência que acompanha o alvo (centro e chão), e o chão de cada ataque é o terreno real.
        private bool avisouFuria;

        /// <summary>Fora da neve ela não se segura: ataques 50% mais fortes e pausas pela metade (como chefes do Terraria fora do bioma).</summary>
        public bool Enfurecida => NoMundoAberto && !Common.Raid.Vitrine.Ligada && Fase >= 1 && Estado != DESPEDIDA && !Alvo.ZoneSnow;

        private void SeguirOAlvo()
        {
            if (!NoMundoAberto || Common.Raid.Vitrine.Ligada) return;
            float cx = MathHelper.Lerp(CentroArena.X, Alvo.Center.X, 0.06f);
            float chao = ChaoEm(Alvo.Center.X, Alvo.Bottom.Y - 8f, 40);
            float piso = MathHelper.Lerp(ArenaPiso, chao, 0.08f);
            ArenaEsq = cx - LarguraArena / 2f;
            ArenaDir = cx + LarguraArena / 2f;
            ArenaPiso = piso;
            ArenaTopo = piso - AlturaArena;
        }

        public override void ModifyHitPlayer(Player target, ref Player.HurtModifiers modifiers)
        {
            if (Enfurecida) modifiers.SourceDamage *= 1.5f;
        }

        // ─── Pausa: escolhe o próximo ataque, checa stun e transições ───
        private void FazerPausa()
        {
            SeguirOAlvo();
            if (Enfurecida && !avisouFuria)
            {
                avisouFuria = true;
                Dialogo.Falar("Silvanna", "Fora da neve? Então ele não tem mais motivo para se segurar.", CorSilvanna, 160);
            }
            if (Timer == 1 && stunPendente)
            {
                stunPendente = false;
                SoundEngine.PlaySound(SoundID.NPCDeath14 with { Pitch = 0.3f }, NPC.Center);
                ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 12f);
                CombatText.NewText(NPC.Hitbox, new Color(255, 230, 80), "POSTURA QUEBRADA!", true);
                IrPara(ATORDOADO);
                return;
            }

            if (Fase >= 3)
            {
                float xp = MathHelper.Lerp(NPC.Center.X, Alvo.Center.X, 0.02f);
                MoverAte(new Vector2(xp, Chao(xp) - 62f), VelMover * 0.5f);
            }
            else
                MoverAte(PosOcioso + new Vector2(0f, MathF.Sin(Timer * 0.06f) * 8f), VelMover);

            int pausa = Fase == 1 ? 70 : Fase == 2 ? 60 : 50;
            if (Enfurecida) pausa /= 2;
            if (Revanche) pausa = pausa * 3 / 5;
            if (Timer < pausa)
                return;

            float vida = NPC.life / (float)NPC.lifeMax;
            if (!fezGiro && vida <= LimiarGiro) { IrPara(T_GIRO); return; }
            if (!fezNevasca && vida <= LimiarNevasca) { IrPara(T_NEVASCA); return; }
            if (!fezFinal && vida <= LimiarFinal) { IrPara(FINAL); return; }

            int i = contadorAtaque++;
            IrPara(Fase switch
            {
                // Etapa 1 (o chapéu) e 2 (a forma verdadeira): as mecânicas do As Astral
                1 => (i % 3) switch { 0 => PENDULO, 1 => RICOCHETE, _ => RASTEIRA },
                2 => RotacaoForma[i % RotacaoForma.Length],
                // Cavaleiro de gelo: só ataques de gelo (4 no Normal, 6 no Heroico, os 7 no Mítico)
                _ => RotacaoCavaleiro[i % RotacaoCavaleiro.Length]
            });
        }

        // ═══════════════ ETAPA 1: O CHAPÉU ═══════════════
        private void FazerPendulo()
        {
            if (etapa == 0)
            {
                if (Timer == 1 && Servidor)
                {
                    bool daEsquerda = Main.rand.NextBool();
                    inicioPendulo = new Vector2(daEsquerda ? ArenaEsq + 40f : ArenaDir - 40f, ArenaTopo + 40f);
                    NPC.netUpdate = true;
                }
                if (Timer > 1 && MoverAte(inicioPendulo, VelMover))
                {
                    fimPenduloX = MathHelper.Clamp(Alvo.Center.X, ArenaEsq, ArenaDir);
                    tPendulo = 0f;
                    etapa = 1;
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.3f }, NPC.Center);
                }
                return;
            }

            tPendulo += 1.8f / 60f;
            float t = MathHelper.Clamp(tPendulo, 0f, 1f);
            float curva = MathF.Pow(Math.Max(0f, MathF.Sin(t * MathHelper.Pi)), 0.6f); // Max(0): evita o NaN de pow(negativo)
            Vector2 nova = new(MathHelper.Lerp(inicioPendulo.X, fimPenduloX, t), MathHelper.Lerp(inicioPendulo.Y, ArenaPiso - 15f, curva));
            NPC.velocity = nova - NPC.Center;
            NPC.rotation = NPC.velocity.X * 0.02f;

            if (tPendulo >= 1f)
            {
                NPC.rotation = 0f;
                IrPara(PAUSA);
            }
        }

        // ─── Ricochete: o chapéu despenca e QUICA pelo chão até você; cada quique espirra lascas para cima ───
        private void FazerRicochete()
        {
            const int Quique = 34;
            int n = Dificuldade.Escolher(3, 4, 5);
            if (etapa == 0)
            {
                // sobe girando cada vez mais rápido (o aviso)
                MoverAte(new Vector2(NPC.Center.X, Chao(NPC.Center.X) - 300f), VelMover);
                NPC.rotation += 0.05f + Timer * 0.006f;
                if (Timer >= 40)
                {
                    etapa = 1;
                    Timer = 0;
                    inicioSalto = NPC.Center;
                    ProximoQuique();
                }
                return;
            }
            float s = Math.Min(1f, Timer / Quique);
            Vector2 nova = Vector2.Lerp(inicioSalto, alvoSalto, s) + new Vector2(0f, -MathF.Sin(s * MathHelper.Pi) * 200f);
            NPC.velocity = nova - NPC.Center;
            NPC.rotation += 0.35f * Math.Sign(alvoSalto.X - inicioSalto.X + 0.01f);
            if (Timer >= Quique)
            {
                NPC.Center = alvoSalto;
                NPC.velocity = Vector2.Zero;
                ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 5f);
                SoundEngine.PlaySound(SoundID.Item70 with { Pitch = 0.4f, Volume = 0.8f }, NPC.Center);
                for (int i = 0; i < 12; i++)
                    Dust.NewDustPerfect(NPC.Bottom, DustID.Snow, new Vector2(Main.rand.NextFloat(-5f, 5f), -Main.rand.NextFloat(1f, 4f)), 0, default, 1.4f);
                LascaDeGelo.Leque(NPC, NPC.Bottom - new Vector2(0f, 12f), 5, 0.8f, 6.5f, 9.5f, 30);
                if (etapa >= n)
                {
                    NPC.rotation = 0f;
                    IrPara(PAUSA);
                    return;
                }
                etapa++;
                Timer = 0;
                inicioSalto = NPC.Center;
                ProximoQuique();
            }
        }

        private void ProximoQuique()
        {
            if (!Servidor) return;
            float dx = MathHelper.Clamp(Alvo.Center.X - NPC.Center.X, -420f, 420f);
            if (Math.Abs(dx) < 120f) dx = 120f * (Main.rand.NextBool() ? 1 : -1);
            float x = NPC.Center.X + dx;
            alvoSalto = new Vector2(x, Chao(x) - NPC.height * 0.5f);
            NPC.netUpdate = true;
        }

        // ─── Rasteira: o chapéu desce rente ao chão e passa rasgando; a geada que ele deixa vira espinho logo depois ───
        private void FazerRasteira()
        {
            if (etapa == 0)
            {
                if (Timer == 1 && Servidor) { ladoAtaque = Alvo.Center.X > NPC.Center.X ? 1 : -1; NPC.netUpdate = true; }
                float x = Alvo.Center.X - ladoAtaque * 650f;
                NPC.rotation += 0.3f;
                if ((Timer > 1 && MoverAte(new Vector2(x, Chao(x) - 34f), VelMover * 1.4f)) || Timer > 120)
                {
                    etapa = 1;
                    Timer = 0;
                    SoundEngine.PlaySound(SoundID.Item30 with { Pitch = -0.2f }, NPC.Center);
                }
                return;
            }
            if (etapa == 1)
            {
                // gira no lugar levantando neve; a geada se espalha pelo caminho (o aviso)
                NPC.velocity = Vector2.Zero;
                NPC.rotation += 0.15f + Timer * 0.012f;
                if (Main.rand.NextBool())
                    Dust.NewDustPerfect(NPC.Bottom, DustID.Snow, new Vector2(-ladoAtaque * Main.rand.NextFloat(2f, 5f), -Main.rand.NextFloat(1f, 3f)), 0, default, 1.3f);
                if (Timer >= 45)
                {
                    etapa = 2;
                    Timer = 0;
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.4f }, NPC.Center);
                }
                return;
            }
            float vx = ladoAtaque * 21f;
            float alvoY = Chao(NPC.Center.X + vx) - 34f;
            NPC.velocity = new Vector2(vx, MathHelper.Clamp(alvoY - NPC.Center.Y, -20f, 20f));
            NPC.rotation += 0.45f * ladoAtaque;
            if (Timer % 3 == 0 && Servidor) // a geada no caminho vira espinho daqui a pouco
                Projectile.NewProjectile(NPC.GetSource_FromAI(), new Vector2(NPC.Center.X, Chao(NPC.Center.X)), Vector2.Zero, ModContent.ProjectileType<EspinhoDeGelo>(), 34, 0f, Main.myPlayer,
                    32, 70f + Main.rand.NextFloat(30f));
            if (Timer >= 64)
            {
                NPC.velocity *= 0.2f;
                NPC.rotation = 0f;
                IrPara(PAUSA);
            }
        }

        // ═══════════════ ETAPA 2: A FORMA VERDADEIRA ═══════════════
        /// <summary>Pingentes: pingentes enormes racham lá em cima, despencam, CRAVAM no chão e depois estilhaçam.</summary>
        private void FazerPingentes()
        {
            MoverAte(PosOcioso + new Vector2(0f, -120f), VelMover);
            int ondas = Dificuldade.Escolher(2, 3, 3);
            for (int k = 0; k < ondas; k++)
                if (Timer == 15 + k * 70)
                {
                    SoundEngine.PlaySound(SoundID.Item30 with { Pitch = 0.3f }, NPC.Center);
                    if (Servidor)
                    {
                        float[] xs = { Alvo.Center.X + Alvo.velocity.X * 18f, Alvo.Center.X - Main.rand.NextFloat(230f, 330f), Alvo.Center.X + Main.rand.NextFloat(230f, 330f) };
                        for (int j = 0; j < xs.Length; j++)
                        {
                            float chao = Chao(xs[j]);
                            float topo = Math.Min(chao, Alvo.Center.Y) - 560f;
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), new Vector2(xs[j], topo + 75f), Vector2.Zero, ModContent.ProjectileType<PingenteGigante>(), 40, 0f, Main.myPlayer,
                                chao, 50 + j * 10);
                        }
                    }
                }
            if (Timer >= 15 + ondas * 70 + 80) IrPara(PAUSA);
        }

        /// <summary>Avalanche: bolas de neve descem rolando pelo terreno de verdade, crescendo — pule por cima.</summary>
        private void FazerAvalanche()
        {
            MoverAte(PosOcioso + new Vector2(0f, -160f), VelMover);
            int n = Dificuldade.Escolher(2, 3, 4);
            if (Timer == 1) SoundEngine.PlaySound(SoundID.Item120 with { Pitch = -0.7f }, NPC.Center);
            for (int k = 0; k < n; k++)
                if (Timer == 20 + k * 55 && Servidor)
                {
                    int lado = (k + contadorAtaque) % 2 == 0 ? 1 : -1; // 1 = vem da esquerda rolando para a direita
                    float x = Alvo.Center.X - lado * 1100f;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), new Vector2(x, Chao(x) - 30f), Vector2.Zero, ModContent.ProjectileType<BolaDeNeve>(), 38, 0f, Main.myPlayer, lado);
                }
            if (Timer >= 20 + n * 55 + 200) IrPara(PAUSA);
        }

        // ═══════════════ CAVALEIRO DE GELO ═══════════════
        /// <summary>Espinhos: uma onda de espinhos brota do chão vindo na sua direção (pule ou voe por cima no tempo certo).</summary>
        private void FazerEspinhos()
        {
            NPC.velocity *= 0.85f;
            bool onda = Timer == 20 || Timer == 75 || Timer == 130;
            if (onda && Servidor)
            {
                // a 2ª onda vem do lado contrário: quem pulou para longe precisa pular de novo
                int lado = (Alvo.Center.X > NPC.Center.X ? 1 : -1) * (Timer == 75 ? -1 : 1);
                // espinhos colados uns nos outros (com 60 px de espaço dava para ficar parado entre dois)
                for (int k = 0; k < 64; k++)
                {
                    float x = NPC.Center.X + lado * (90f + k * 34f);
                    if (x < ArenaEsq + 20f || x > ArenaDir - 20f) break;
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), new Vector2(x, Chao(x)), Vector2.Zero, ModContent.ProjectileType<EspinhoDeGelo>(), 34, 0f, Main.myPlayer,
                        30 + k * 3, 170f + (k % 3) * 40f);
                }
            }
            if (onda) SoundEngine.PlaySound(SoundID.Item28 with { Pitch = -0.6f }, NPC.Center);
            if (Timer >= 230) IrPara(PAUSA);
        }

        /// <summary>Prisão de gelo: três círculos marcam onde você está (um atrás do outro). Continue se mexendo.</summary>
        private void FazerPrisao()
        {
            NPC.velocity *= 0.85f;
            if ((Timer - 15) % 24 == 0 && Timer <= 15 + 24 * 5 && Servidor)
                foreach (Player p in Main.ActivePlayers)
                    if (!p.dead && JogadorNaRaid(p))
                    {
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), p.Center, Vector2.Zero, ModContent.ProjectileType<PrisaoDeGelo>(), 0, 0f, Main.myPlayer, 50f);
                        // e outra onde você vai estar, se seguir correndo
                        if (p.velocity.Length() > 3f)
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), p.Center + p.velocity * 40f, Vector2.Zero, ModContent.ProjectileType<PrisaoDeGelo>(), 0, 0f, Main.myPlayer, 50f);
                    }
            if (Timer == 15) Hud.Aviso("NÃO FIQUE PARADO!", new Color(170, 230, 255), 90);
            if (Timer >= 220) IrPara(PAUSA);
        }

        /// <summary>Sopro glacial: ele sobe e sopra um cone de gelo que varre a arena; fique atrás dele.</summary>
        private void FazerSopro()
        {
            if (Timer < 40)
            {
                MoverAte(new Vector2(MathHelper.Clamp(Alvo.Center.X, ArenaEsq + 300f, ArenaDir - 300f), ArenaPiso - 360f), VelMover * 1.2f);
                return;
            }
            NPC.velocity *= 0.8f;
            if (Timer == 40 && Servidor)
            {
                int lado = Alvo.Center.X > NPC.Center.X ? 1 : -1;
                // começa olhando para baixo do lado do jogador e varre para o lado oposto
                float inicio = MathHelper.PiOver2 - lado * 1.1f;
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<SoproGlacial>(), 38, 0f, Main.myPlayer,
                    inicio, lado * 2.2f, NPC.whoAmI);
            }
            if (Timer == 40) Hud.Aviso("FIQUE ATRÁS DELE!", new Color(170, 230, 255), 90);
            if (Timer >= 40 + SoproGlacial.Aviso + SoproGlacial.Duracao + 15) IrPara(PAUSA);
        }

        private void FazerAtordoado()
        {
            NPC.velocity *= 0.9f;
            NPC.rotation = MathF.Sin(Timer * 0.15f) * 0.3f;
            if (Main.rand.NextBool(3))
                Dust.NewDustPerfect(NPC.Top + Main.rand.NextVector2Circular(30f, 10f), DustID.GoldFlame, -Vector2.UnitY, 0, default, 1.1f).noGravity = true;
            if (Timer >= 240)
            {
                Stagger = 0f;
                NPC.rotation = 0f;
                IrPara(PAUSA);
            }
        }

        // ═══════════════ TRANSIÇÃO 1: O GIRO (GANCHO) ═══════════════
        private void FazerGiro()
        {
            if (etapa == 0)
            {
                if (Timer == 1)
                {
                    fezGiro = true;
                    SalvarCheckpoint(T_GIRO, PosOcioso);
                    LimparPerigos();
                    Hud.Fase("TRANSIÇÃO: O Giro de Armotheus", new Color(255, 120, 120));
                    Dialogo.Falar("Silvanna", "Cansou, Armotheus? Então pare de fingir.", CorSilvanna, 150);
                }
                if (MoverAte(new Vector2(CentroArena.X, ArenaPiso - 90f), VelMover))
                {
                    Fase = 2; // forma verdadeira
                    Hud.Clarao(0.7f);
                    ScreenShakeSystem.StartShake(12f);
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.5f }, NPC.Center);
                    Hud.Aviso("SEGURE-SE COM O GANCHO!", new Color(255, 200, 120), 180);
                    AbrirPortao();
                    etapa = 1;
                    Timer = 0;
                }
                return;
            }

            NPC.velocity = Vector2.Zero;
            NPC.rotation += 0.25f;
            ScreenShakeSystem.SetUniversalRumble(2f, MathHelper.TwoPi, null, 0.2f);

            // Puxão: cada cliente cuida do próprio jogador. Com o gancho preso (grapCount > 0), quase não puxa.
            if (Main.netMode != NetmodeID.Server)
            {
                Player eu = Main.LocalPlayer;
                if (!eu.dead && JogadorNaRaid(eu))
                {
                    Vector2 dir = eu.DirectionTo(NPC.Center);
                    float forca = eu.grapCount > 0 ? PuxaoComGancho : PuxaoGiro;
                    eu.position.X += dir.X * forca;
                    eu.velocity.Y += dir.Y * 0.15f;
                }
                for (int i = 0; i < 3; i++)
                {
                    Vector2 de = NPC.Center + Main.rand.NextVector2CircularEdge(1f, 1f) * Main.rand.NextFloat(150f, 600f);
                    Dust.NewDustPerfect(de, DustID.IceTorch, (NPC.Center - de) * 0.03f, 0, default, 1.3f).noGravity = true;
                }
            }

            // Encostar no redemoinho: as lâminas do chapéu cortam e jogam você para fora (cada cliente cuida de si)
            if (Main.netMode != NetmodeID.Server)
            {
                Player eu = Main.LocalPlayer;
                if (!eu.dead && JogadorNaRaid(eu) && eu.Distance(NPC.Center) < 80f && eu.immuneTime <= 0)
                {
                    CastigoLocal(eu, Dificuldade.Escolher(0.25f, 0.35f, 0.5f));
                    eu.velocity = eu.DirectionFrom(NPC.Center) * 14f + new Vector2(0f, -6f);
                }
            }

            if (PortaoQuebrado)
            {
                CombatText.NewText(NPC.Hitbox, new Color(255, 230, 120), "POSTURA QUEBRADA!", true);
                Hud.Fase("ETAPA 2: A Forma Verdadeira", new Color(255, 140, 140));
                ScreenShakeSystem.StartShake(14f);
                NPC.rotation = 0f;
                Stagger = 0f;
                SalvarCheckpoint(PAUSA, PosOcioso);
                IrPara(PAUSA);
            }
            else if (Timer >= TempoGiro)
            {
                // não quebraram a postura a tempo: o redemoinho EXPLODE em gelo e a luta segue
                if (Main.netMode != NetmodeID.Server && JogadorNaRaid(Main.LocalPlayer))
                    CastigoLocal(Main.LocalPlayer, Dificuldade.Escolher(0.3f, 0.45f, 0.6f), 40);
                LascaDeGelo.Leque(NPC, NPC.Center, 14, 1.5f, 8f, 13f, 34);
                ScreenShakeSystem.StartShake(14f);
                SoundEngine.PlaySound(SoundID.Item27 with { Pitch = -0.6f, Volume = 1.2f }, NPC.Center);
                NPC.rotation = 0f;
                Stagger = 0f;
                IrPara(PAUSA);
            }
        }

        private Vector2 PosClone(int lado) => new(lado < 0 ? ArenaEsq + 90f : ArenaDir - 90f, ArenaPiso - 90f);

        /// <summary>
        /// Clones de gelo: cópias dela surgem em volta de você, cada uma risca uma linha até onde você estava
        /// e avança por ela, uma depois da outra. Saia da linha antes que o clone venha.
        /// </summary>
        private void FazerClones()
        {
            MoverAte(PosOcioso + new Vector2(0f, -140f), VelMover);
            int n = Dificuldade.Escolher(2, 3, 4);
            if (Timer == 20)
            {
                SoundEngine.PlaySound(SoundID.Item8, NPC.Center);
                if (Servidor)
                {
                    float base_ = Main.rand.NextFloat(MathHelper.TwoPi);
                    for (int k = 0; k < n; k++)
                    {
                        float ang = base_ + k * MathHelper.TwoPi / n;
                        Vector2 de = Alvo.Center + ang.ToRotationVector2() * 520f;
                        de.X = MathHelper.Clamp(de.X, ArenaEsq + 40f, ArenaDir - 40f);
                        de.Y = MathHelper.Clamp(de.Y, ArenaTopo + 40f, ArenaPiso - 50f);
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), de, Vector2.Zero, ModContent.ProjectileType<CloneDeGelo>(), 34, 0f, Main.myPlayer,
                            Alvo.Center.X, Alvo.Center.Y, 55 + k * 20);
                    }
                }
            }
            if (Timer >= 20 + 55 + n * 20 + 45)
                IrPara(PAUSA);
        }

        private void FazerSalto()
        {
            int ciclo = (int)(Timer - 1) / 70;
            int t = (int)(Timer - 1) % 70;

            if (ciclo >= 3)
            {
                IrPara(PAUSA);
                return;
            }

            if (t == 0)
            {
                inicioSalto = NPC.Center;
                if (Servidor)
                {
                    float xs = MathHelper.Clamp(Alvo.Center.X, ArenaEsq + 60f, ArenaDir - 60f);
                    alvoSalto = new Vector2(xs, Chao(xs) - 62f);
                    NPC.netUpdate = true;
                }
            }

            if (t < 20)
                NPC.velocity *= 0.8f; // agacha
            else if (t < 50)
            {
                float s = (t - 20) / 30f;
                Vector2 nova = Vector2.Lerp(inicioSalto, alvoSalto, s) + new Vector2(0f, -MathF.Sin(s * MathHelper.Pi) * 240f);
                NPC.velocity = nova - NPC.Center;
                NPC.rotation += 0.3f * Math.Sign(alvoSalto.X - inicioSalto.X);
            }
            else if (t == 50)
            {
                NPC.Center = alvoSalto;
                NPC.velocity = Vector2.Zero;
                NPC.rotation = 0f;
                // o impacto racha o chão: espinhos brotam para os dois lados, cada vez mais baixos
                if (Servidor)
                    for (int lado = -1; lado <= 1; lado += 2)
                        for (int k = 0; k < 4; k++)
                        {
                            float x = alvoSalto.X + lado * (60f + k * 55f);
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), new Vector2(x, Chao(x)), Vector2.Zero, ModContent.ProjectileType<EspinhoDeGelo>(), 36, 0f, Main.myPlayer,
                                2 + k * 5, 130f - k * 22f);
                        }
                ScreenShakeSystem.StartShakeAtPoint(alvoSalto, 10f);
                SoundEngine.PlaySound(SoundID.Item70, NPC.Center);
            }
        }

        // ═══════════════ TRANSIÇÃO 2: A NEVASCA ═══════════════
        // No Mítico o escudo dele vai cedendo à tempestade: o abrigo encolhe com o tempo (fiquem juntos, colados nele)
        private int LarguraAbrigo => Estado == T_NEVASCA && etapa >= 1 && Dificuldade.Mitico ? (int)MathHelper.Lerp(190f, 105f, Math.Min(1f, Timer / TempoNevasca)) : 190;
        private Rectangle Abrigo => new((int)(NPC.Center.X + 40f), (int)ArenaTopo, LarguraAbrigo, (int)(ArenaPiso - ArenaTopo));

        private void FazerNevasca()
        {
            if (etapa == 0)
            {
                if (Timer == 1)
                {
                    fezNevasca = true;
                    SalvarCheckpoint(T_NEVASCA, new Vector2(ArenaEsq + 260f, ArenaPiso - 60f));
                    LimparPerigos();
                    Fase = 3; // cavaleiro de gelo
                    posSilvanna = new Vector2(ArenaDir - 130f, Chao(ArenaDir - 130f) - 64f);
                    Hud.Clarao(0.8f);
                    Hud.Fase("TRANSIÇÃO: A Nevasca", new Color(170, 230, 255));
                    Hud.Aviso("FIQUE ATRÁS DO CAVALEIRO!", new Color(170, 230, 255), 180);
                    Dialogo.Falar("Silvanna", "A tempestade sempre vem. Ele é o único que fica de pé na frente dela.", CorSilvanna, 200);
                    SoundEngine.PlaySound(SoundID.Item120 with { Pitch = -0.4f }, NPC.Center);
                }
                if (MoverAte(new Vector2(ArenaEsq + 110f, Chao(ArenaEsq + 110f) - 62f), VelMover * 1.4f))
                {
                    AbrirPortao();
                    etapa = 1;
                    Timer = 0;
                }
                return;
            }

            NPC.velocity = Vector2.Zero;

            if (Main.netMode != NetmodeID.Server)
            {
                Player eu = Main.LocalPlayer;
                if (!eu.dead && JogadorNaRaid(eu))
                {
                    bool abrigado = Abrigo.Intersects(eu.Hitbox);
                    nevascaAbrigado = abrigado ? 1f : Math.Max(0f, nevascaAbrigado - 0.05f);
                    if (!abrigado)
                    {
                        FrioJogador frio = eu.GetModPlayer<FrioJogador>();
                        frio.Exposto = true; // vai congelando: mais lento, voa menos, toma mais dano
                        eu.velocity.X = Math.Min(eu.velocity.X + 0.2f, 6.5f);   // o vento leva para a direita (dá para andar contra, devagar)
                        if (Timer % 45 == 0 && Dificuldade.Heroico) // no Normal o frio só deixa lento
                            eu.Hurt(PlayerDeathReason.ByNPC(NPC.whoAmI), (int)(14 * (1f + frio.Congelamento * 2f)), 1);
                    }

                }

                for (int i = 0; i < 4; i++)
                {
                    Vector2 de = new(Main.screenPosition.X - 20f, Main.screenPosition.Y + Main.rand.NextFloat(Main.screenHeight));
                    Dust.NewDustPerfect(de, DustID.Snow, new Vector2(Main.rand.NextFloat(14f, 22f), Main.rand.NextFloat(-1f, 2f)), 0, default, 1.2f).noGravity = true;
                }
            }
            if (Timer % 110 == 109) // o escudo range contra o vento
                SoundEngine.PlaySound(SoundID.Item52 with { Pitch = -0.6f, Volume = 0.6f }, NPC.Center);

            if (PortaoQuebrado)
            {
                CombatText.NewText(NPC.Hitbox, new Color(255, 230, 120), "POSTURA QUEBRADA!", true);
                Hud.Fase("ÚLTIMA ETAPA: O Cavaleiro", new Color(170, 230, 255));
                Stagger = 0f;
                SalvarCheckpoint(PAUSA, new Vector2(CentroArena.X, ArenaPiso - 60f));
                IrPara(PAUSA);
            }
            else if (Timer >= TempoNevasca)
            {
                // a tempestade passa: quem não está atrás do cavaleiro congela de vez
                if (Main.netMode != NetmodeID.Server && JogadorNaRaid(Main.LocalPlayer) && !Abrigo.Intersects(Main.LocalPlayer.Hitbox))
                    CastigoLocal(Main.LocalPlayer, Dificuldade.Escolher(0.3f, 0.45f, 0.6f), 90);
                Stagger = 0f;
                IrPara(PAUSA);
            }
        }

        // ═══════════════ ÚLTIMA ETAPA: O CAVALEIRO ═══════════════
        private void FazerLanca()
        {
            NPC.velocity *= 0.85f;
            int t = (int)(Timer - 1) % 50;
            if (Timer > 150)
            {
                IrPara(PAUSA);
                return;
            }
            if (t == 35)
            {
                if (Servidor)
                    for (int k = -1; k <= 1; k++) // 3 lanças: com só 2, quem ficava parado no meio nunca era atingido
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, NPC.DirectionTo(Alvo.Center).RotatedBy(k * 0.16f) * 19f, ModContent.ProjectileType<LancaVoadora>(), 34, 0f, Main.myPlayer);
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.2f }, NPC.Center);
            }
        }

        private void FazerInvestida()
        {
            if (etapa == 0)
            {
                if (Timer == 1 && Servidor)
                {
                    ladoAtaque = Alvo.Center.X > CentroArena.X ? -1 : 1; // começa do lado oposto ao jogador
                    NPC.netUpdate = true;
                }
                float xi = ladoAtaque == 1 ? ArenaEsq + 70f : ArenaDir - 70f;
                if (Timer > 1 && MoverAte(new Vector2(xi, Chao(xi) - 62f), VelMover * 1.5f))
                {
                    etapa = 1;
                    Timer = 0;
                }
                return;
            }
            if (etapa == 1)
            {
                NPC.velocity = Vector2.Zero;
                if (Timer >= 45)
                {
                    etapa = 2;
                    Timer = 0;
                    SoundEngine.PlaySound(SoundID.Roar with { Pitch = 0.4f }, NPC.Center);
                }
                return;
            }

            float yi = Chao(NPC.Center.X + ladoAtaque * 24f) - 62f;
            NPC.velocity = new Vector2(ladoAtaque * 24f, MathHelper.Clamp(yi - NPC.Center.Y, -24f, 24f));
            if (Fase >= 3 && Timer % 4 == 0 && Servidor) // Deslize Glacial: deixa o chão congelado
                Projectile.NewProjectile(NPC.GetSource_FromAI(), new Vector2(NPC.Center.X, Chao(NPC.Center.X) - 9f), Vector2.Zero, ModContent.ProjectileType<RastroDeGelo>(), 0, 0f, Main.myPlayer);
            bool chegou = ladoAtaque == 1 ? NPC.Center.X >= ArenaDir - 70f : NPC.Center.X <= ArenaEsq + 70f;
            if (chegou || Timer > 90)
            {
                NPC.velocity = Vector2.Zero;
                ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 8f);
                IrPara(PAUSA);
            }
        }

        /// <summary>
        /// Escudo: ele crava o escudo no chão e solta uma ONDA de gelo que corre rente ao chão até a parede — pule
        /// por cima (como a onda da Queen Slime). No Heroico/Mítico ela vai e volta.
        /// </summary>
        private void FazerEscudo()
        {
            if (etapa == 0)
            {
                float xe = MathHelper.Clamp(NPC.Center.X, ArenaEsq + 160f, ArenaDir - 160f);
                MoverAte(new Vector2(xe, Chao(xe) - 62f), VelMover);
                if (Timer > 30)
                {
                    etapa = 1;
                    Timer = 0;
                    ladoAtaque = Alvo.Center.X > NPC.Center.X ? 1 : -1;
                    NPC.netUpdate = true;
                }
                return;
            }

            NPC.velocity *= 0.8f;
            if (Timer == 1 && Servidor)
                Projectile.NewProjectile(NPC.GetSource_FromAI(), new Vector2(NPC.Center.X + ladoAtaque * 70f, Chao(NPC.Center.X + ladoAtaque * 70f)), Vector2.Zero,
                    ModContent.ProjectileType<OndaDeEscudo>(), 34, 0f, Main.myPlayer, ladoAtaque, Dificuldade.Heroico ? 1f : 0f);
            if (Timer == 40)
            {
                SoundEngine.PlaySound(SoundID.Item37 with { Pitch = -0.5f }, NPC.Center);
                ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 7f);
            }
            if (Timer >= (Dificuldade.Heroico ? 300 : 190))
                IrPara(PAUSA);
        }

        // ═══════════════ MECÂNICA FINAL: O INVERNO ETERNO (a lua de gelo) ═══════════════
        private void FazerFinal()
        {
            switch (etapa)
            {
                case 0: // carrega a lança e sobe
                    if (Timer == 1)
                    {
                        fezFinal = true;
                        SalvarCheckpoint(FINAL, new Vector2(CentroArena.X, ArenaPiso - 60f));
                        LimparPerigos();
                        Hud.Fase("MECÂNICA FINAL: O Inverno Eterno", new Color(170, 230, 255));
                        Dialogo.Falar("Silvanna", "Ele vai congelar a lua e jogar em cima de vocês. Achem um escudo.", CorSilvanna, 180);
                        AbrirPortao();
                    }
                    NPC.dontTakeDamage = true;
                    if (Timer < 70)
                    {
                        NPC.velocity *= 0.85f;
                        if (Main.rand.NextBool(2))
                            Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(40f, 40f), DustID.IceTorch, -Vector2.UnitY * 2f, 0, default, 1.5f).noGravity = true;
                    }
                    else
                        NPC.velocity = new Vector2(0f, -26f);

                    if (NPC.Center.Y < ArenaTopo - 500f)
                    {
                        NPC.velocity = Vector2.Zero;
                        GerarEstatuasEEscudos();
                        Hud.Aviso("QUEBRE AS ESTÁTUAS E PEGUE OS ESCUDOS!", new Color(255, 215, 110), 200);
                        etapa = 1;
                        Timer = 0;
                    }
                    break;

                case 1: // estátuas + escudos
                    NPC.velocity = Vector2.Zero;
                    if (TodosComEscudo() || Timer >= TempoFinal)
                    {
                        etapa = 2;
                        Timer = 0;
                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.6f });
                    }
                    break;

                case 2: // a lua de gelo se forma lá em cima, sobre a arena
                {
                    const int Carga = 130;
                    float k = Math.Min(1f, Timer / Carga);
                    if (Main.netMode != NetmodeID.Server)
                    {
                        Hud.ClaraoAlvo = Timer < Carga ? k * 0.35f : 0f; // depois da explosão a tela volta ao normal
                        if (Timer < Carga) ScreenShakeSystem.SetUniversalRumble(k * 4f, MathHelper.TwoPi, null, 0.2f);
                    }
                    if (Timer == 1)
                        Hud.Aviso("A LUA DE GELO VAI CAIR — SÓ O ESCUDO SALVA!", new Color(170, 230, 255), 120);
                    if (Timer == Carga)
                    {
                        // A explosão vem da LUA lá em cima (não do chão — senão parece que ele caiu duas vezes)
                        Hud.ClaraoAlvo = 0f;
                        Hud.Clarao(0.6f);
                        ScreenShakeSystem.StartShake(20f);
                        SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.2f, Pitch = -0.4f });
                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.6f });
                        if (Main.netMode != NetmodeID.Server)
                        {
                            Player eu = Main.LocalPlayer;
                            if (JogadorNaRaid(eu) && !eu.HasBuff(ModContent.BuffType<EscudoAstral>()))
                            {
                                if (Dificuldade.Mitico) MatarLocal(eu);
                                else CastigoLocal(eu, Dificuldade.Escolher(0.5f, 0.75f, 1f), 90);
                            }
                        }
                        RemoverEstatuasEEscudos();
                    }
                    if (Timer >= Carga + 50)
                    {
                        NPC.Center = new Vector2(CentroArena.X, ArenaTopo - 700f);
                        NPC.velocity = Vector2.Zero;
                        etapa = 3;
                        Timer = 0;
                    }
                    break;
                }

                case 3: // ele cai do céu como uma estrela cadente
                    if (Timer < QuedaAviso)
                    {
                        NPC.Center = new Vector2(CentroArena.X, ArenaTopo - 700f);
                        NPC.velocity = Vector2.Zero;
                        if (Timer == 1) SoundEngine.PlaySound(SoundID.Item9 with { Pitch = 0.8f, Volume = 1.2f });
                        if (Timer == QuedaAviso - 25) SoundEngine.PlaySound(SoundID.Item163 with { Pitch = -0.3f });
                        break;
                    }
                    if (Timer == QuedaAviso)
                    {
                        NPC.velocity = new Vector2(0f, 24f);
                        ScreenShakeSystem.StartShake(6f);
                    }
                    NPC.velocity.X = 0f;
                    NPC.velocity.Y = Math.Min(NPC.velocity.Y + 3f, 72f);
                    if (Main.netMode != NetmodeID.Server)
                        for (int d = 0; d < 4; d++)
                            Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(30f, 50f), DustID.IceTorch, new Vector2(Main.rand.NextFloat(-3f, 3f), -6f), 0, default, 2f).noGravity = true;
                    if (NPC.Center.Y + NPC.velocity.Y >= Chao(CentroArena.X) - 62f)
                    {
                        NPC.Center = new Vector2(CentroArena.X, Chao(CentroArena.X) - 62f);
                        NPC.velocity = Vector2.Zero;
                        IniciarImpacto(NPC.Center + new Vector2(0f, 60f), new Color(170, 225, 255));
                        NPC.dontTakeDamage = false;
                        SalvarCheckpoint(PAUSA, new Vector2(CentroArena.X, ArenaPiso - 60f));
                        IrPara(ATORDOADO);
                    }
                    break;
            }
        }

        private const int QuedaAviso = 75;

        /// <summary>A lua de gelo (Final), o redemoinho do Giro e o aviso da queda.</summary>
        public override void DesenharEfeitosDeMundo(SpriteBatch sb)
        {
            base.DesenharEfeitosDeMundo(sb);
            Texture2D bloom = MiscTexturesRegistry.BloomCircleSmall.Value;
            if (Estado == FINAL && etapa == 2)
            {
                // A LUA DE GELO: uma esfera de gelo se formando sobre a arena, puxando neve e cristais para ela
                float k = Timer <= 130 ? Timer / 130f : Math.Max(0f, 1f - (Timer - 130) / 25f); // some depois de explodir
                Vector2 lua = new(CentroArena.X, Math.Max(ArenaTopo + 160f, Main.screenPosition.Y + 150f));
                float t = Main.GlobalTimeWrappedHourly;
                for (int i = 0; i < 18; i++) // neve sendo sugada em espiral
                {
                    float fase = (t * 0.7f + i * 0.37f) % 1f;
                    float ang = i / 18f * MathHelper.TwoPi + t * 0.8f + fase * 2f;
                    float dist = MathHelper.Lerp(900f, 90f, fase);
                    sb.Draw(bloom, lua - Main.screenPosition + ang.ToRotationVector2() * dist, null, new Color(190, 235, 255, 0) * k * 0.5f * (1f - fase * 0.5f), ang + 1.2f, bloom.Size() / 2f, new Vector2(1.2f, 0.08f), 0, 0f);
                }
                sb.Draw(bloom, lua - Main.screenPosition, null, new Color(110, 190, 255, 0) * k * 0.5f, 0f, bloom.Size() / 2f, 1.2f + k * 2.6f, 0, 0f);
                Desenho.Orbe(lua, 60f + k * 230f, new Color(120, 200, 255), new Color(235, 250, 255), 1f, 3.1f, k);
            }
            if (Estado == T_GIRO && etapa == 1)
            {
                // O GIRO: um redemoinho de neve e lâminas de gelo em volta do chapéu (não é buraco negro — isso é do Éter)
                float t = Main.GlobalTimeWrappedHourly;
                Vector2 c = NPC.Center - Main.screenPosition;
                for (int i = 0; i < 28; i++)
                {
                    float fase = (t * 0.55f + i * 0.0357f * 7f) % 1f;
                    float dist = MathHelper.Lerp(620f, 60f, fase);
                    float ang = i * 2.399f - t * 3f - fase * 4f;
                    Color cor = (i % 3 == 0 ? new Color(230, 245, 255, 0) : new Color(120, 200, 255, 0)) * (0.25f + 0.5f * fase);
                    sb.Draw(bloom, c + ang.ToRotationVector2() * dist, null, cor, ang + MathHelper.PiOver2, bloom.Size() / 2f, new Vector2(0.9f + fase, 0.07f), 0, 0f);
                }
                sb.Draw(bloom, c, null, new Color(150, 220, 255, 0) * 0.55f, 0f, bloom.Size() / 2f, 1.6f, 0, 0f);
            }
            if (Estado == FINAL && etapa == 3 && Timer < QuedaAviso)
                DesenharAvisoDeQueda(sb, CentroArena.X, Timer / (float)QuedaAviso, new Color(170, 225, 255));
        }

        private void GerarEstatuasEEscudos()
        {
            if (!Servidor) return;
            int n = Utils.Clamp(JogadoresVivosNaRaid(), 1, 4);
            for (int i = 0; i < n; i++)
            {
                float x = MathHelper.Lerp(ArenaEsq + 150f, ArenaDir - 150f, n == 1 ? 0.5f : i / (float)(n - 1));
                int estatua = NPC.NewNPC(NPC.GetSource_FromAI(), (int)x, (int)Chao(x) - 10, ModContent.NPCType<EstatuaViva>());
                Projectile.NewProjectile(NPC.GetSource_FromAI(), new Vector2(x + 60f, Chao(x + 60f) - 30f), Vector2.Zero,
                    ModContent.ProjectileType<EscudoNoChao>(), 0, 0f, Main.myPlayer, estatua);
            }
        }

        private void RemoverEstatuasEEscudos()
        {
            if (!Servidor) return;
            foreach (NPC n in Main.ActiveNPCs)
                if (n.type == ModContent.NPCType<EstatuaViva>())
                {
                    n.active = false;
                    n.netUpdate = true;
                }
            foreach (Projectile p in Main.ActiveProjectiles)
                if (p.type == ModContent.ProjectileType<EscudoNoChao>())
                    p.Kill();
        }

        private bool TodosComEscudo()
        {
            int tipo = ModContent.BuffType<EscudoAstral>();
            bool algum = false;
            foreach (Player p in Main.ActivePlayers)
            {
                if (p.dead || !JogadorNaRaid(p)) continue;
                algum = true;
                if (!p.HasBuff(tipo)) return false;
            }
            return algum;
        }

        // ═══════════════ FIM: SILVANNA CONVERSA E VAI EMBORA ═══════════════
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
            NPC.rotation = MathHelper.Lerp(NPC.rotation, 0.4f, 0.05f);

            if (Timer == 1)
            {
                RaidJogador.Foco = posSilvanna;
                RaidJogador.ForcaFoco = 0.7f;
                Hud.Clarao(0.5f);
            }
            if (Revanche)
            {
                if (Timer == 20) Dialogo.Falar("Silvanna", "...Ele nunca perdeu com tudo. Até hoje.", CorSilvanna, 180);
                if (Timer == 210) Dialogo.Falar("Silvanna", "Leve isto. Onde você vai, vai precisar de um inverno do seu lado.", CorSilvanna, 200);
            }
            else
            {
                if (Timer == 20) Dialogo.Falar("Silvanna", "...Chega, Armotheus. Você já mostrou o que precisava.", CorSilvanna, 180);
                if (Timer == 210) Dialogo.Falar("Silvanna", "A Astrânia não é sua, nem minha. É de algo que está vindo. Prepare-se.", CorSilvanna, 200);
            }

            if (Timer > 420)
                posSilvanna += new Vector2(3f, -7f); // ela vai embora voando

            if (Timer >= 520 && Servidor)
            {
                RaidJogador.Foco = null;
                morteLiberada = true;
                NPC.dontTakeDamage = false;
                NPC.StrikeInstantKill();
            }
        }

        public override void OnKill()
        {
            MundoDeRaid.Vencida = true;
            if (Revanche)
            {
                AstralMundo.DerrotouInverno = true;
                AstralMundo.SincronizarMundo();
                AstralMundo.Mensagem("O inverno se curva a você.", new Color(170, 230, 255));
                RaidJogador.Foco = null;
                return;
            }
            AstralMundo.DerrotouSilvanna = true;
            AstralMundo.SincronizarMundo();
            AstralMundo.Mensagem("Lá em cima, Astra parece um pouco maior...", new Color(130, 190, 255));
            Dialogo.FalaDoEco("Ela não era nossa inimiga... mas também não estava do nosso lado. Fique atento.");
            RaidJogador.Foco = null;
        }

        // ═══════════════ FILTROS ═══════════════
        private void AtualizarFiltros()
        {
            if (Main.dedServ) return;

            // Garante a nevasca do Terraria ligada (a IA do NPC roda depois da do jogador, então esta é a palavra final)
            if (NevascaAtiva && JogadorNaRaid(Main.LocalPlayer) && Terraria.Graphics.Effects.Filters.Scene["Blizzard"] is { } nevasca && !nevasca.IsActive())
                Terraria.Graphics.Effects.Filters.Scene.Activate("Blizzard", Main.LocalPlayer.Center);

            Player local = Main.LocalPlayer;
            if (NevascaAtiva && JogadorNaRaid(local) && ShaderManager.TryGetFilter("AscensaoAstral.NevascaFilter", out ManagedScreenFilter nev))
            {
                // A nevasca "crítica": neve em camadas + névoa + cristais de gelo crescendo das bordas conforme você congela
                nev.TrySetParameter("intensidade", 0.8f);
                Rectangle ab = Abrigo;
                Matrix m = Main.GameViewMatrix.TransformationMatrix;
                float x0 = Vector2.Transform(new Vector2(ab.Left, 0f) - Main.screenPosition, m).X / Main.screenWidth;
                float x1 = Vector2.Transform(new Vector2(ab.Right, 0f) - Main.screenPosition, m).X / Main.screenWidth;
                nev.TrySetParameter("abrigoX", etapa >= 1 ? new Vector2(x0, x1) : new Vector2(-2f, -2f));
                nev.TrySetParameter("congelamento", local.GetModPlayer<FrioJogador>().Congelamento);
                nev.TrySetParameter("abrigado", nevascaAbrigado);
                nev.SetTexture(MiscTexturesRegistry.DendriticNoise.Value, 1, SamplerState.LinearWrap);
                nev.SetTexture(MiscTexturesRegistry.TurbulentNoise.Value, 2, SamplerState.LinearWrap);
                nev.Activate();
            }
            else if (Fase >= 3 && ShaderManager.TryGetFilter("AscensaoAstral.GeloFilter", out ManagedScreenFilter gelo))
            {
                gelo.TrySetParameter("intensidade", 0.35f);
                gelo.Activate();
            }
        }

        // ═══════════════ DESENHO ═══════════════
        private float nevascaAbrigado;

        /// <summary>A Nevasca usa a nevasca de verdade do Terraria (veja NevascaDaRaid).</summary>
        public bool NevascaAtiva => Estado == T_NEVASCA && etapa >= 1;

        public override bool CanHitPlayer(Player target, ref int cooldownSlot)
            => (Estado == PENDULO && etapa == 1) || (Estado == INVESTIDA && etapa == 2) || (Estado == SALTO) || (Estado == RICOCHETE && etapa >= 1) || (Estado == RASTEIRA && etapa == 2);

        public override bool PreDraw(SpriteBatch sb, Vector2 screenPos, Color drawColor)
        {
            if (Fase >= 3 || Estado == INTRO || Estado == DESPEDIDA)
                DesenharSilvanna(sb, screenPos);
            // (os cavaleiros do céu agora são desenhados no PLANO DE FUNDO pelo CeuDoArmotheus)

            if (Estado == PENDULO && etapa == 1)
                DesenharRastro(new Color(140, 220, 255), new Color(40, 60, 160));
            if (Estado == INVESTIDA && etapa == 2)
                DesenharRastro(new Color(200, 240, 255), new Color(60, 120, 200));
            if (Estado == FINAL && etapa == 3 && Timer >= QuedaAviso)
                DesenharRastroDeEstrela(new Color(170, 225, 255), new Color(60, 120, 255));

            DesenharAvisos();

            if (Estado == FINAL && etapa >= 1 && etapa <= 2)
                return false; // ele está lá no alto, fora da arena

            DesenharCorpo(sb, screenPos, drawColor);

            return false;
        }

        private void DesenharRastro(Color inicio, Color fim)
        {
            Vector2[] pontos = new Vector2[NPC.oldPos.Length];
            for (int i = 0; i < pontos.Length; i++)
                pontos[i] = NPC.oldPos[i] == Vector2.Zero ? NPC.Center : NPC.oldPos[i] + NPC.Size / 2f;
            PrimitiveRenderer.RenderTrail(pontos, new PrimitiveSettings(t => MathHelper.Lerp(70f, 4f, t), t => Color.Lerp(inicio, fim, t) * (1f - t) * 0.7f), 40);
        }

        private void DesenharAvisos()
        {
            float pulso = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 12f);

            // Os avisos da Silvanna são do MUNDO (geada se espalhando, sombra no chão, a lança brilhando),
            // nunca retângulos — esses são a assinatura do Astharoth.
            Texture2D bloom = MiscTexturesRegistry.BloomCircleSmall.Value;
            if (Estado == RASTEIRA && etapa == 1)
            {
                // a geada correndo pelo chão à frente do chapéu
                float alcance = 1350f * Utils.GetLerpValue(0f, 40f, Timer, true);
                for (float d = 40f; d < alcance; d += 46f)
                {
                    float x = NPC.Center.X + ladoAtaque * d;
                    Vector2 p = new Vector2(x, Chao(x) - 2f) - Main.screenPosition;
                    Main.spriteBatch.Draw(bloom, p, null, new Color(150, 220, 255, 0) * (0.35f + 0.25f * pulso) * (1f - d / 1500f), 0f, bloom.Size() / 2f, new Vector2(0.7f, 0.12f), 0, 0f);
                }
            }

            if (Estado == INVESTIDA && etapa == 1)
            {
                // a ponta da lança acende e um rastro de geada marca o caminho do deslize
                float k = Utils.GetLerpValue(0f, 45f, Timer, true);
                Vector2 fim = new(ladoAtaque == 1 ? ArenaDir : ArenaEsq, NPC.Center.Y);
                Desenho.Risco(NPC.Center + new Vector2(ladoAtaque * 60f, 10f), fim + new Vector2(0f, 10f), 3f + k * 6f, new Color(200, 240, 255) * (0.2f + 0.4f * k));
                Desenho.Clarao(NPC.Center + new Vector2(ladoAtaque * 120f, -30f), new Color(200, 240, 255), 0.2f + 0.5f * k, Timer * 0.05f);
            }

            if (Estado == LANCA && (int)(Timer - 1) % 60 < 35)
                Desenho.Risco(NPC.Center, Desenho.CortarNoChao(NPC.Center, NPC.Center + NPC.DirectionTo(Alvo.Center) * 1400f), 10f, new Color(150, 230, 255) * 0.55f);

            if (Estado == SALTO)
            {
                // a SOMBRA dela no chão, crescendo enquanto ela desce (como no King Slime / Deerclops)
                int t = (int)(Timer - 1) % 70;
                if (t < 50)
                {
                    float k = Utils.GetLerpValue(0f, 50f, t, true);
                    Vector2 p = new Vector2(alvoSalto.X, Chao(alvoSalto.X)) - Main.screenPosition;
                    Main.spriteBatch.Draw(bloom, p, null, Color.Black * (0.25f + 0.45f * k), 0f, bloom.Size() / 2f, new Vector2(1.2f + k * 1.4f, 0.22f + k * 0.12f), 0, 0f);
                }
            }

            if (Estado == ESCUDO && etapa == 1 && Timer < 35)
                Desenho.Brilho(NPC.Center + new Vector2(ladoAtaque * 50f, 0f), new Color(190, 110, 255), 1.2f + pulso * 0.3f);

            if (Estado == T_NEVASCA && etapa == 1)
                Desenho.RetanguloAviso(Abrigo, new Color(170, 255, 200), 0.3f);
        }

        private void DesenharCorpo(SpriteBatch sb, Vector2 screenPos, Color drawColor)
        {
            Texture2D tex;
            Rectangle? recorte = null;
            Color tinta = Color.White;

            if (Fase <= 1 || Estado == INTRO)
            {
                string folha = Estado switch { PENDULO or RICOCHETE or RASTEIRA => "Ataque", _ => "Idle" };
                int quadros = folha == "Ataque" ? 7 : 6;
                tex = ModContent.Request<Texture2D>($"AscensaoAstral/Assets/Sheets/{folha}").Value;
                recorte = new Rectangle((int)(Main.GlobalTimeWrappedHourly * 10f) % quadros * 100, 0, 100, 100);
            }
            else
            {
                tex = ModContent.Request<Texture2D>("AscensaoAstral/Assets/Sheets/VerdadeiraForma").Value;
                if (Fase >= 3)
                    tinta = new Color(170, 225, 255); // cavaleiro de gelo (provisório: forma verdadeira tingida)
            }

            Vector2 origem = recorte.HasValue ? new Vector2(50f) : tex.Size() / 2f;
            Vector2 pos = NPC.Center - screenPos + new Vector2(0f, NPC.gfxOffY);

            Color cor = NPC.GetAlpha(Color.Lerp(drawColor, Color.White, 0.85f)).MultiplyRGB(tinta);
            if (EstaAtordoado) cor = Color.Lerp(cor, new Color(255, 255, 60), 0.55f);
            else if (FlashDano > 0) cor = Color.Lerp(cor, new Color(255, 120, 120), 0.6f);

            Desenho.Brilho(NPC.Center, Fase >= 3 ? new Color(80, 170, 255) : new Color(255, 60, 50), 1.9f * NPC.scale);
            if (Enfurecida) // aura vermelha pulsando: está fora da neve
                Desenho.Brilho(NPC.Center, new Color(255, 70, 90), (2.4f + 0.4f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f)) * NPC.scale);
            SpriteEffects flip = Fase >= 2 && Alvo.Center.X > NPC.Center.X ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            DesenharPosImagens(sb, tex, recorte, origem, NPC.scale, Fase >= 3 ? new Color(120, 200, 255) : new Color(255, 90, 80), flip);
            sb.Draw(tex, pos, recorte, TintaDeMemoria(cor) * NPC.Opacity, NPC.rotation, origem, NPC.scale, flip, 0f);

            // Lança e escudo do cavaleiro (provisórios, desenhados com luz)
            if (Fase >= 3)
            {
                int dir = Alvo.Center.X > NPC.Center.X ? 1 : -1;
                Desenho.Lanca(NPC.Center + new Vector2(-dir * 40f, 18f), NPC.Center + new Vector2(dir * 120f, -34f), 22f, new Color(150, 220, 255));
                Desenho.Brilho(NPC.Center + new Vector2(dir * 30f, 20f), new Color(120, 200, 255), 0.9f);
            }
        }

        private void DesenharClone(SpriteBatch sb, Vector2 screenPos, int lado)
        {
            Texture2D tex = ModContent.Request<Texture2D>("AscensaoAstral/Assets/Sheets/VerdadeiraForma").Value;
            Color cor = lado == ladoErrado ? new Color(255, 90, 90) : new Color(170, 235, 255);
            Vector2 p = PosClone(lado);
            float aparecer = Math.Min(1f, Timer / 20f);
            Desenho.Brilho(p, cor, 1.6f);
            sb.Draw(tex, p - screenPos, null, cor * 0.9f * aparecer, 0f, tex.Size() / 2f, NPC.scale, lado > 0 ? SpriteEffects.None : SpriteEffects.FlipHorizontally, 0f);
        }

        private void DesenharSilvanna(SpriteBatch sb, Vector2 screenPos)
        {
            bool voando = Estado == DESPEDIDA && Timer > 420;
            Texture2D tex = ModContent.Request<Texture2D>(voando ? "AscensaoAstral/Assets/Sheets/Bruxa" : "AscensaoAstral/Assets/Sheets/Silvanna").Value;
            Vector2 p = posSilvanna + new Vector2(0f, MathF.Sin(Main.GlobalTimeWrappedHourly * 2f) * 3f);
            Desenho.Brilho(p, new Color(80, 160, 255), 1.3f);
            sb.Draw(tex, p - screenPos, null, Color.White, 0f, tex.Size() / 2f, 1.1f, SpriteEffects.FlipHorizontally, 0f);
        }

        // ─── Os cavaleiros do céu: um de cada vez acorda, brilha e arremessa lanças em você ───
        private int relogioCeu;
        private static int CicloCavaleiro => Dificuldade.Mitico ? 85 : 110;
        private const int AvisoCavaleiro = 50;
        private bool CavaleirosAtivos => Dificuldade.Heroico && Fase >= 3 && Estado != FINAL && Estado != DESPEDIDA && Estado != REINICIANDO && Estado != T_NEVASCA && Estado != ATORDOADO;

        // No plano de fundo, na linha do chão da arena (perto de onde você está olhando)
        private Vector2 PosCavaleiro(int i) => new(MathHelper.Lerp(ArenaEsq + 200f, ArenaDir - 200f, i / 4f),
            ArenaPiso - 90f + MathF.Sin(relogioCeu * 0.013f + i) * 6f);

        private void AtualizarCavaleirosDoCeu()
        {
            if (!CavaleirosAtivos) { relogioCeu = 0; return; }
            relogioCeu++;
            int fase = relogioCeu % CicloCavaleiro;
            int quem = relogioCeu / CicloCavaleiro % 5;
            if (fase == 1)
                SoundEngine.PlaySound(SoundID.Item30 with { Pitch = 0.3f, Volume = 0.6f }, PosCavaleiro(quem));
            if (fase == AvisoCavaleiro)
            {
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.2f }, PosCavaleiro(quem));
                if (Servidor)
                {
                    // Joga a lança PARA CIMA; ela some no céu e cai em cima de quem está mais perto
                    Vector2 de = PosCavaleiro(quem);
                    Player alvo = Aereo.MaisPerto(de);
                    if (alvo is not null)
                    {
                        float x1 = alvo.Center.X, x2 = MathHelper.Clamp(alvo.Center.X + Main.rand.NextFloat(180f, 320f) * (Main.rand.NextBool() ? 1 : -1), ArenaEsq + 40f, ArenaDir - 40f);
                        foreach (float x in new[] { x1, x2 })
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), de, new Vector2(0f, -24f), ModContent.ProjectileType<LancaDoCeu>(), 32, 0f, Main.myPlayer, x, Math.Min(ArenaTopo, Chao(x) - 900f), Chao(x));
                    }
                }
            }
        }

        /// <summary>Chamado pelo céu (CeuDoArmotheus), atrás de tudo.</summary>
        public void DesenharCavaleirosNoFundo(SpriteBatch sb, Vector2 screenPos)
        {
            if (Fase >= 3) DesenharCavaleirosDoCeu(sb, screenPos);
        }

        private void DesenharCavaleirosDoCeu(SpriteBatch sb, Vector2 screenPos)
        {
            Texture2D tex = ModContent.Request<Texture2D>("AscensaoAstral/Assets/Sheets/VerdadeiraForma").Value;
            int fase = relogioCeu % CicloCavaleiro;
            int quem = relogioCeu / CicloCavaleiro % 5;
            for (int i = 0; i < 5; i++)
            {
                Vector2 p = PosCavaleiro(i);
                bool acordado = CavaleirosAtivos && i == quem && fase < AvisoCavaleiro + 15;
                float brilho = acordado ? Utils.GetLerpValue(0, AvisoCavaleiro, fase, true) : 0f;
                // Plano de fundo: menores, azulados e meio transparentes; quem vai atacar acende
                if (acordado)
                {
                    Desenho.Brilho(p, new Color(150, 220, 255), 0.9f + brilho * 1.4f);
                    if (fase < AvisoCavaleiro) // a lança sendo erguida
                        Desenho.Lanca(p + new Vector2(14f, 14f), p + new Vector2(14f, -60f - brilho * 30f), 14f + brilho * 6f, new Color(140, 210, 255), 1f + brilho * 0.5f);
                }
                Color cor = Color.Lerp(new Color(110, 150, 210) * 0.45f, new Color(225, 245, 255), brilho);
                sb.Draw(tex, p - screenPos, null, cor, 0f, tex.Size() / 2f, 0.6f, Fase >= 3 && i % 2 == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
            }
        }
    }

    // ═══════════════ PROJÉTEIS DA RAID ═══════════════

    public class LancaVoadora : ModProjectile
    {
        public override string Texture => "AscensaoAstral/Assets/Vazio";

        public override void SetDefaults()
        {
            Projectile.width = 20;
            Projectile.height = 20;
            Projectile.hostile = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 150;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Lighting.AddLight(Projectile.Center, 0.5f, 0.8f, 1f);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Desenho.Lanca(Projectile.Center - dir * 90f, Projectile.Center + dir * 40f, 24f, new Color(150, 220, 255));
            Desenho.Brilho(Projectile.Center + dir * 30f, new Color(200, 240, 255), 0.5f);
            return false;
        }
    }

    /// <summary>
    /// Lança dos cavaleiros do fundo: sobe e some no céu, depois um risco gelado marca onde vai cair, e ela despenca.
    /// ai[0] = X onde cai, ai[1] = topo da arena, ai[2] = chão da arena.
    /// </summary>
    public class LancaDoCeu : ModProjectile
    {
        public override string Texture => "AscensaoAstral/Assets/Vazio";
        private const int Subindo = 30, Aviso = 45;

        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 8;
            ProjectileID.Sets.TrailingMode[Type] = 0;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3000;
        }

        public override void SetDefaults()
        {
            Projectile.width = 22;
            Projectile.height = 22;
            Projectile.hostile = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 200;
        }

        private int Idade => 200 - Projectile.timeLeft;
        private bool Caindo => Idade >= Subindo + Aviso;

        public override void AI()
        {
            if (Idade < Subindo) { Projectile.velocity.Y -= 0.6f; return; } // subindo e sumindo
            if (Idade == Subindo)
            {
                Projectile.Center = new Vector2(Projectile.ai[0], Projectile.ai[1] - 60f);
                Projectile.velocity = Vector2.Zero;
            }
            if (Idade == Subindo + Aviso)
            {
                Projectile.velocity = new Vector2(0f, 30f);
                SoundEngine.PlaySound(SoundID.Item28 with { Pitch = -0.3f, Volume = 0.7f }, Projectile.Center);
            }
            if (Caindo && Projectile.Center.Y > Projectile.ai[2])
            {
                for (int i = 0; i < 14; i++)
                    Dust.NewDustPerfect(new Vector2(Projectile.Center.X, Projectile.ai[2]), DustID.IceTorch, Main.rand.NextVector2Circular(6f, 3f) - Vector2.UnitY * 3f, 0, default, 1.6f).noGravity = true;
                ScreenShakeSystem.StartShakeAtPoint(Projectile.Center, 3f);
                Projectile.Kill();
            }
            Lighting.AddLight(Projectile.Center, 0.4f, 0.7f, 1f);
        }

        public override bool CanHitPlayer(Player target) => Caindo;
        public override void OnHitPlayer(Player target, Player.HurtInfo info) => target.AddBuff(BuffID.Chilled, 150);

        public override bool PreDraw(ref Color lightColor)
        {
            Color c = new(150, 220, 255);
            if (Idade < Subindo)
            {
                Desenho.Lanca(Projectile.Center + new Vector2(0f, 50f), Projectile.Center - new Vector2(0f, 40f), 20f, c);
                return false;
            }
            if (!Caindo)
            {
                float pulso = 0.5f + 0.5f * MathF.Sin(Idade * 0.5f);
                float k = (Idade - Subindo) / (float)Aviso;
                Rectangle r = new((int)Projectile.ai[0] - 14, (int)Projectile.ai[1], 28, (int)(Projectile.ai[2] - Projectile.ai[1]));
                Desenho.RetanguloAviso(r, c, pulso * k, k);
                Desenho.Brilho(new Vector2(Projectile.ai[0], Projectile.ai[2] - 6f), c, 0.6f + k * 0.6f);
                return false;
            }
            Desenho.Lanca(Projectile.Center - new Vector2(0f, 130f), Projectile.Center + new Vector2(0f, 30f), 28f, c, 1.3f);
            Desenho.Brilho(Projectile.Center, Color.White, 0.35f);
            return false;
        }
    }

    /// <summary>Escudo no chão. Fica TRANCADO enquanto a estátua dele (ai[0]) estiver viva. Encostar = pega.</summary>
    public class EscudoNoChao : ModProjectile
    {
        public override string Texture => "AscensaoAstral/Assets/Vazio";

        public override void SetDefaults()
        {
            Projectile.width = 40;
            Projectile.height = 40;
            Projectile.friendly = false;
            Projectile.hostile = false;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = 60 * 60;
        }

        private bool Trancado
        {
            get
            {
                NPC e = Main.npc[(int)Projectile.ai[0]];
                return e.active && e.type == ModContent.NPCType<EstatuaViva>();
            }
        }

        public override void AI()
        {
            if (Trancado || Main.netMode == NetmodeID.Server)
                return;

            Player eu = Main.LocalPlayer;
            int buff = ModContent.BuffType<EscudoAstral>();
            if (!eu.dead && !eu.HasBuff(buff) && eu.Hitbox.Intersects(Projectile.Hitbox))
            {
                eu.AddBuff(buff, 60 * 40);
                SoundEngine.PlaySound(SoundID.Item29, Projectile.Center);
                CombatText.NewText(eu.Hitbox, new Color(140, 220, 255), "Escudo!", true);
                Projectile.Kill();
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            float pulso = 0.8f + 0.2f * MathF.Sin(Main.GlobalTimeWrappedHourly * 5f);
            Color cor = Trancado ? new Color(90, 90, 110) : new Color(120, 210, 255);
            Desenho.Brilho(Projectile.Center, cor, 0.9f * pulso);
            Hud.DesenharSimbolo(Main.spriteBatch, Projectile.Center - Main.screenPosition, 0, 16f);
            if (Trancado)
                Utils.DrawBorderString(Main.spriteBatch, "trancado", Projectile.Center - Main.screenPosition + new Vector2(0f, -36f), Color.Gray, 0.7f, 0.5f, 0.5f);
            return false;
        }
    }

    /// <summary>
    /// Liga a NEVASCA do próprio Terraria (o filtro "Blizzard" da tundra) durante a transição da Silvanna,
    /// junto com o som de vento forte. Funciona em qualquer bioma.
    /// </summary>
    public class NevascaDaRaid : ModSceneEffect
    {
        public override SceneEffectPriority Priority => SceneEffectPriority.BossHigh;

        private static bool Ativa(Player p) => RaidBoss.Ativo is RaidSilvanna s && s.NevascaAtiva && s.JogadorNaRaid(p);

        public override bool IsSceneEffectActive(Player player) => Ativa(player);

        public override void SpecialVisuals(Player player, bool isActive)
        {
            // Só LIGA; quem desliga é o próprio Terraria quando a nevasca acaba (assim não brigamos com a tundra de verdade)
            if (isActive)
            {
                player.ManageSpecialBiomeVisuals("Blizzard", true);
                Main.windSpeedCurrent = Math.Max(Main.windSpeedCurrent, 0.8f); // as partículas de neve voam para a direita
                if (Main.rand.NextBool(2))
                {
                    Vector2 de = new(Main.screenPosition.X - 20f, Main.screenPosition.Y + Main.rand.NextFloat(Main.screenHeight));
                    Dust.NewDustPerfect(de, DustID.Snow, new Vector2(Main.rand.NextFloat(16f, 24f), Main.rand.NextFloat(-1f, 2f)), 0, default, 1.4f).noGravity = true;
                }
            }
        }
    }
}

namespace AscensaoAstral.Content.Bosses.Silvanna
{
    /// <summary>Clone de gelo: ai[0], ai[1] = onde mira; ai[2] = ticks de aviso antes de avançar.</summary>
    public class CloneDeGelo : ModProjectile
    {
        public override string Texture => "AscensaoAstral/Assets/Vazio";
        private const int Corrida = 32;
        private int Idade => (int)Projectile.ai[2] + Corrida - Projectile.timeLeft;
        private bool Ativo => Idade >= Projectile.ai[2];
        private Vector2 Inicio => new(Projectile.localAI[0], Projectile.localAI[1]);
        private Vector2 Direcao => (new Vector2(Projectile.ai[0], Projectile.ai[1]) - Inicio).SafeNormalize(Vector2.UnitX);

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 2400;
        public override void SetDefaults()
        {
            Projectile.width = 56; Projectile.height = 56;
            Projectile.hostile = true; Projectile.tileCollide = false; Projectile.penetrate = -1; Projectile.timeLeft = 2;
        }
        public override void OnSpawn(Terraria.DataStructures.IEntitySource source) => Projectile.timeLeft = (int)Projectile.ai[2] + Corrida;

        public override void AI()
        {
            if (Projectile.localAI[2] == 0f) { Projectile.localAI[2] = 1f; Projectile.localAI[0] = Projectile.Center.X; Projectile.localAI[1] = Projectile.Center.Y; }
            if (!Ativo) { Projectile.velocity = Vector2.Zero; return; }
            if (Idade == (int)Projectile.ai[2])
            {
                Projectile.velocity = Direcao * 34f;
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.2f, Volume = 0.8f }, Projectile.Center);
            }
            Lighting.AddLight(Projectile.Center, 0.4f, 0.7f, 1f);
            if (Main.rand.NextBool(2))
                Dust.NewDustPerfect(Projectile.Center, DustID.IceTorch, -Projectile.velocity * 0.2f, 0, default, 1.4f).noGravity = true;
        }

        public override bool CanHitPlayer(Player target) => Ativo;
        public override void OnHitPlayer(Player target, Player.HurtInfo info) => target.AddBuff(BuffID.Chilled, 150);

        public override bool PreDraw(ref Color lightColor)
        {
            Color gelo = new(150, 225, 255);
            Texture2D tex = ModContent.Request<Texture2D>("AscensaoAstral/Assets/Sheets/VerdadeiraForma").Value;
            SpriteEffects flip = Direcao.X >= 0f ? SpriteEffects.None : SpriteEffects.FlipHorizontally;
            float surge = Utils.GetLerpValue(0f, 18f, Idade, true);
            if (!Ativo)
            {
                // a linha por onde ele vai passar, cada vez mais forte
                float k = Idade / Projectile.ai[2];
                Desenho.Risco(Inicio, Desenho.CortarNoChao(Inicio, Inicio + Direcao * 1150f), 4f + k * 10f, gelo * (0.25f + 0.55f * k));
                Desenho.Brilho(Projectile.Center, gelo, 0.5f + k * 0.4f);
            }
            float some = Math.Min(1f, Projectile.timeLeft / 8f);
            if (Ativo)
            {
                // imagens residuais (como as dashes do Duke Fishron / Empress): cópias azuladas que vão sumindo pra trás
                for (int i = 4; i >= 1; i--)
                {
                    Vector2 atras = Projectile.Center - Direcao * i * 34f;
                    Main.spriteBatch.Draw(tex, atras - Main.screenPosition, null, new Color(90, 170, 255, 0) * (0.5f - i * 0.1f) * some, 0f, tex.Size() / 2f, 1f - i * 0.03f, flip, 0f);
                }
                // um fio de gelo fino e afilado no caminho (nada de laje cinza)
                Desenho.Golpe(Desenho.Estilo.Gelo, Projectile.Center - Direcao * 160f, Projectile.Center, 18f, gelo, 1f, 0.7f * some);
                Desenho.Lanca(Projectile.Center - Direcao * 30f, Projectile.Center + Direcao * 80f, 18f, gelo, 1.3f);
            }
            Main.spriteBatch.Draw(tex, Projectile.Center - Main.screenPosition, null, (gelo with { A = 180 }) * 0.85f * surge * some, 0f, tex.Size() / 2f, 1f, flip, 0f);
            return false;
        }
    }

    /// <summary>Onda de gelo do escudo: corre rente ao chão. ai[0] = direção (±1), ai[1] = 1 → volta depois de bater na parede.</summary>
    public class OndaDeEscudo : ModProjectile
    {
        public override string Texture => "AscensaoAstral/Assets/Vazio";
        private const int Aviso = 40;
        private const float Altura = 110f, Velocidade = 15f;
        private ref float Idade => ref Projectile.localAI[0];
        private ref float Voltou => ref Projectile.localAI[1];

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3000;
        public override void SetDefaults()
        {
            Projectile.width = 70; Projectile.height = (int)Altura;
            Projectile.hostile = true; Projectile.tileCollide = false; Projectile.penetrate = -1; Projectile.timeLeft = 600;
        }

        public override void AI()
        {
            Idade++;
            if (Idade == 1) Projectile.Bottom = new Vector2(Projectile.Center.X, Projectile.Center.Y); // nasce em cima do chão
            float dir = Projectile.ai[0];
            if (Idade < Aviso) { Projectile.velocity = Vector2.Zero; return; }
            Projectile.velocity = new Vector2(dir * Velocidade, 0f);
            if (RaidBoss.Ativo is RaidBoss bc) // corre colada no terreno de verdade
                Projectile.Bottom = new Vector2(Projectile.Center.X, MathHelper.Lerp(Projectile.Bottom.Y, bc.Chao(Projectile.Center.X + Projectile.velocity.X), 0.5f));
            if (RaidBoss.Ativo is RaidBoss b && (Projectile.Right.X > b.ArenaDir || Projectile.Left.X < b.ArenaEsq))
            {
                if (Projectile.ai[1] == 1f && Voltou == 0f) { Voltou = 1f; Projectile.ai[0] = -dir; Projectile.netUpdate = true; ScreenShakeSystem.StartShakeAtPoint(Projectile.Center, 5f); }
                else Projectile.Kill();
            }
            if (Main.rand.NextBool())
                Dust.NewDustPerfect(new Vector2(Projectile.Center.X, Projectile.Bottom.Y - 4f), DustID.Ice, new Vector2(-dir * 2f, -Main.rand.NextFloat(2f, 6f)), 0, default, 1.2f);
            Lighting.AddLight(Projectile.Center, 0.4f, 0.7f, 1f);
        }

        public override bool CanHitPlayer(Player target) => Idade >= Aviso;
        public override void OnHitPlayer(Player target, Player.HurtInfo info) { target.AddBuff(BuffID.Chilled, 150); target.velocity.Y = -7f; }

        public override bool PreDraw(ref Color lightColor)
        {
            Color gelo = new(140, 215, 255);
            float chao = Projectile.Bottom.Y;
            if (Idade < Aviso && RaidBoss.Ativo is RaidBoss b)
            {
                // a pista rente ao chão por onde a onda vai correr
                // o escudo crava e o chão à frente congela (a pista da onda)
                float dir = Projectile.ai[0];
                float alcance = Math.Abs((dir > 0 ? b.ArenaDir : b.ArenaEsq) - Projectile.Center.X) * (Idade / Aviso);
                Texture2D bloom = MiscTexturesRegistry.BloomCircleSmall.Value;
                for (float d = 0f; d < alcance; d += 46f)
                {
                    float x = Projectile.Center.X + dir * d;
                    Main.spriteBatch.Draw(bloom, new Vector2(x, b.Chao(x) - 2f) - Main.screenPosition, null, (gelo with { A = 0 }) * 0.5f * (0.6f + 0.4f * MathF.Sin(Idade * 0.5f)), 0f, bloom.Size() / 2f, new Vector2(0.7f, 0.12f), 0, 0f);
                }
                return false;
            }
            // a onda: um paredão de cristal correndo, mais alto na frente
            Vector2 frente = new(Projectile.Center.X + Projectile.ai[0] * 30f, chao);
            Desenho.Golpe(Desenho.Estilo.Gelo, new Vector2(frente.X, chao), new Vector2(frente.X, chao - Altura), 70f, gelo, 1.2f);
            for (int i = 1; i <= 3; i++)
                Desenho.Ponta(new Vector2(frente.X - Projectile.ai[0] * i * 26f, chao), new Vector2(frente.X - Projectile.ai[0] * i * 30f, chao - Altura * (1f - i * 0.22f)), 20f, gelo, Color.White, 1f, Desenho.Estilo.Gelo);
            return false;
        }
    }
}
