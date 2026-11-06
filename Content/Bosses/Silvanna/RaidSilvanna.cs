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
using Terraria.ID;
using Terraria.ModLoader;

namespace AscensaoAstral.Content.Bosses.Silvanna
{
    /// <summary>
    /// RAID DA SILVANNA (pós Queen Slime, bioma da neve) — a raid que apresenta o conceito.
    ///
    ///   Intro       Silvanna surge e arremessa o chapéu (Armotheus)
    ///   ETAPA 1     Chapéu: Pêndulo U com rastro · Rajada tripla gélida · Varredura do chão (vá até o limite)
    ///   TRANSIÇÃO   75%: forma verdadeira gira e puxa todos ao centro. Segure-se com o GANCHO e quebre a postura.
    ///               Encostar = wipe · tempo acabou = wipe
    ///   ETAPA 2     Cortes horizontais/verticais com brecha · Clones dos cavaleiros (mire na cor ERRADA) · Saltos cortantes
    ///   TRANSIÇÃO   45%: nevasca; Armotheus vira cavaleiro de gelo e bloqueia o vento na esquerda. Fique ATRÁS dele;
    ///               ele empurra com a lança até a postura quebrar
    ///   ÚLTIMA      Cavaleiro: arremesso de lança · investida · empurrão de escudo (atordoa). Silvanna só observa
    ///   FINAL       15%: ele sobe com a lança carregada. Quebre as estátuas vivas, pegue os escudos. Sem escudo = morte
    ///   Fim         Silvanna conversa e vai embora
    /// </summary>
    [AutoloadBossHead]
    public class RaidSilvanna : RaidBoss
    {
        // ─── Estados ───
        private const int INTRO = 1, PAUSA = 2, PENDULO = 3, RAJADA_TRIPLA = 4, VARREDURA_CHAO = 5, ATORDOADO = 6,
            T_GIRO = 7, CORTES = 8, CLONES = 9, SALTO = 10, T_NEVASCA = 11, LANCA = 12, INVESTIDA = 13, ESCUDO = 14,
            FINAL = 15, DESPEDIDA = 16,
            ESTRELA_GELO = 17, CHUVA_GELIDA = 18,
            // Cavaleiro de gelo (etapa final): todos os ataques são de GELO
            ESPINHOS = 19, ANEL = 20, PRISAO = 21, SOPRO = 22;

        public override IEnumerable<Common.Raid.Vitrine.Item> AtaquesDaVitrine(bool comMecanicas)
        {
            yield return new(PENDULO, 1, "Pendulo"); yield return new(RAJADA_TRIPLA, 1, "RajadaTripla"); yield return new(VARREDURA_CHAO, 1, "VarreduraChao");
            yield return new(CORTES, 2, "Cortes"); yield return new(CLONES, 2, "Clones"); yield return new(SALTO, 2, "Salto");
            yield return new(ESTRELA_GELO, 2, "EstrelaGelo"); yield return new(CHUVA_GELIDA, 2, "ChuvaGelida");
            yield return new(LANCA, 3, "Lanca"); yield return new(ESPINHOS, 3, "Espinhos"); yield return new(INVESTIDA, 3, "Investida");
            yield return new(ANEL, 3, "Anel"); yield return new(PRISAO, 3, "Prisao"); yield return new(ESCUDO, 3, "Escudo"); yield return new(SOPRO, 3, "Sopro");
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
            new[] { CORTES, SALTO },
            new[] { CORTES, CLONES, SALTO },
            new[] { CORTES, CLONES, ESTRELA_GELO, SALTO, CHUVA_GELIDA });
        private static int[] RotacaoCavaleiro => Dificuldade.Escolher(
            new[] { LANCA, ESPINHOS, INVESTIDA, ESCUDO },
            new[] { LANCA, ESPINHOS, INVESTIDA, ANEL, ESCUDO, SOPRO },
            new[] { LANCA, ESPINHOS, INVESTIDA, ANEL, PRISAO, ESCUDO, SOPRO });
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
            INTRO => "Intro", PAUSA => "Pausa", PENDULO => "Pendulo", RAJADA_TRIPLA => "RajadaTripla", VARREDURA_CHAO => "VarreduraChao",
            ATORDOADO => "Atordoado", T_GIRO => "T:Giro", CORTES => "Cortes", CLONES => "Clones", SALTO => "Salto",
            T_NEVASCA => "T:Nevasca", LANCA => "Lanca", INVESTIDA => "Investida", ESCUDO => "Escudo", FINAL => "Final",
            DESPEDIDA => "Despedida", ESTRELA_GELO => "EstrelaGelo", CHUVA_GELIDA => "ChuvaGelida",
            ESPINHOS => "Espinhos", ANEL => "AnelDeGelo", PRISAO => "Prisao", SOPRO => "Sopro", REINICIANDO => "Reiniciando", _ => Estado.ToString()
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

        protected override bool JaFoiDerrotado => AstralMundo.DerrotouSilvanna;

        protected override bool ConfinarAgora => Estado != INTRO && Estado != DESPEDIDA && Estado != REINICIANDO;
        protected override Color CorDaParede => Fase >= 2 ? new Color(255, 110, 110) : new Color(120, 210, 255);
        protected override Color CorDasFaiscas => new(170, 230, 255);

        private Vector2 PosOcioso => new(CentroArena.X, ArenaPiso - 110f);
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
            NPC.damage = 55;
            NPC.defense = 24;
            NPC.lifeMax = 42000;
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
                case RAJADA_TRIPLA: FazerRajadaTripla(); break;
                case VARREDURA_CHAO: FazerVarreduraChao(); break;
                case ATORDOADO: FazerAtordoado(); break;
                case T_GIRO: FazerGiro(); break;
                case CORTES: FazerCortes(); break;
                case CLONES: FazerClones(); break;
                case SALTO: FazerSalto(); break;
                case T_NEVASCA: FazerNevasca(); break;
                case LANCA: FazerLanca(); break;
                case INVESTIDA: FazerInvestida(); break;
                case ESCUDO: FazerEscudo(); break;
                case FINAL: FazerFinal(); break;
                case DESPEDIDA: FazerDespedida(); break;
                case ESTRELA_GELO: FazerEstrelaDeGelo(); break;
                case CHUVA_GELIDA: FazerChuvaGelida(); break;
                case ESPINHOS: FazerEspinhos(); break;
                case ANEL: FazerAnel(); break;
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
                posSilvanna = new Vector2(ArenaDir - 130f, ArenaPiso - 64f);
                NPC.Center = posSilvanna + new Vector2(0f, -70f);
                NPC.Opacity = 1f;
                RaidJogador.Foco = posSilvanna;
                RaidJogador.ForcaFoco = 0.85f;
                NPC.netUpdate = true;
            }
            if (Timer == 20) Dialogo.Falar("Silvanna", "Então foi você quem arrancou a Astrânia da terra...", CorSilvanna, 170);
            if (Timer == 200) Dialogo.Falar("Silvanna", "Armotheus. Mostre a essa pessoa o tamanho do erro dela.", CorSilvanna, 150);

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
                Hud.Banner("ETAPA 1: O Chapéu", new Color(255, 110, 90));
                SalvarCheckpoint(PAUSA, PosOcioso);
                IrPara(PAUSA);
            }
        }

        // ─── Pausa: escolhe o próximo ataque, checa stun e transições ───
        private void FazerPausa()
        {
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
                MoverAte(new Vector2(MathHelper.Lerp(NPC.Center.X, Alvo.Center.X, 0.02f), ArenaPiso - 62f), VelMover * 0.5f);
            else
                MoverAte(PosOcioso + new Vector2(0f, MathF.Sin(Timer * 0.06f) * 8f), VelMover);

            int pausa = Fase == 1 ? 70 : Fase == 2 ? 60 : 50;
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
                1 => (i % 3) switch { 0 => PENDULO, 1 => RAJADA_TRIPLA, _ => VARREDURA_CHAO },
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

        private void FazerRajadaTripla()
        {
            MoverAte(PosOcioso + new Vector2(0f, -60f), VelMover);
            if ((Timer == 25 || Timer == 45 || Timer == 65) && Servidor)
            {
                Vector2 dir = NPC.DirectionTo(Alvo.Center);
                for (int k = -1; k <= 1; k++) // leque de 3 que persegue um pouco (ai[1] = 1)
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, dir.RotatedBy(k * 0.3f) * 8.5f, ModContent.ProjectileType<Rajada>(), 24, 0f, Main.myPlayer, 0f, 1f);
            }
            if (Timer == 25 || Timer == 45 || Timer == 65)
                SoundEngine.PlaySound(SoundID.Item28, NPC.Center);
            if (Timer >= 95)
                IrPara(PAUSA);
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
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), new Vector2(x, ArenaPiso), Vector2.Zero, ModContent.ProjectileType<EspinhoDeGelo>(), 32, 0f, Main.myPlayer,
                        30 + k * 3, 170f + (k % 3) * 40f);
                }
            }
            if (onda) SoundEngine.PlaySound(SoundID.Item28 with { Pitch = -0.6f }, NPC.Center);
            if (Timer >= 230) IrPara(PAUSA);
        }

        /// <summary>Anel de gelo: anéis que crescem a partir dele, cada um com uma brecha dourada — passe pela brecha.</summary>
        private void FazerAnel()
        {
            MoverAte(new Vector2(CentroArena.X, ArenaPiso - 240f), VelMover);
            bool anel = Timer == 30 || Timer == 85 || Timer == 140 || Timer == 195;
            if (anel && Servidor)
            {
                float brecha = NPC.AngleTo(Alvo.Center) + Main.rand.NextFloat(-1.4f, 1.4f);
                Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<AnelDeGelo>(), 34, 0f, Main.myPlayer,
                    brecha, Main.rand.NextBool() ? 0.009f : -0.009f, 1500f);
            }
            if (anel) SoundEngine.PlaySound(SoundID.Item30 with { Pitch = 0.2f }, NPC.Center);
            if (Timer >= 300) IrPara(PAUSA);
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

        /// <summary>Estrela de gelo: anéis de cristais em volta dela, cada anel girado — passe pelas frestas.</summary>
        private void FazerEstrelaDeGelo()
        {
            MoverAte(new Vector2(CentroArena.X, ArenaTopo + 150f), VelMover);
            if (Timer >= 35 && Timer <= 125 && Timer % 30 == 5)
            {
                SoundEngine.PlaySound(SoundID.Item28 with { Pitch = -0.2f }, NPC.Center);
                if (Servidor)
                {
                    int n = Fase >= 3 ? 14 : 11;
                    float giro = (Timer / 30f) * (MathHelper.Pi / n);
                    for (int k = 0; k < n; k++)
                        Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, (giro + k * MathHelper.TwoPi / n).ToRotationVector2() * 3.5f,
                            ModContent.ProjectileType<Rajada>(), 24, 0f, Main.myPlayer, 0f);
                }
            }
            if (Timer >= 190) IrPara(PAUSA);
        }

        /// <summary>Chuva gélida: estalactites caem na largura toda; cada onda deixa só um buraco seguro.</summary>
        private void FazerChuvaGelida()
        {
            MoverAte(PosOcioso + new Vector2(0f, -120f), VelMover);
            int ondas = Fase >= 3 ? 3 : 2;
            for (int k = 0; k < ondas; k++)
                if (Timer == 20 + k * 75)
                {
                    SoundEngine.PlaySound(SoundID.Item30 with { Pitch = -0.3f }, NPC.Center);
                    QuedaEstelar.Chuva(NPC, ArenaEsq, ArenaDir, ArenaTopo, ArenaPiso, 0f, 26, 55, 56f, 170f);
                }
            if (Timer >= 20 + ondas * 75 + 60) IrPara(PAUSA);
        }

        private void FazerVarreduraChao()
        {
            if (etapa == 0)
            {
                if (Timer == 1 && Servidor)
                {
                    ladoAtaque = Main.rand.NextBool() ? 1 : -1; // 1 = começa na esquerda e varre para a direita
                    NPC.netUpdate = true;
                }
                Vector2 canto = new(ladoAtaque == 1 ? ArenaEsq + 40f : ArenaDir - 40f, ArenaTopo + 50f);
                if (Timer > 1 && MoverAte(canto, VelMover))
                {
                    etapa = 1;
                    Timer = 0;
                    SoundEngine.PlaySound(SoundID.Item30, NPC.Center);
                    Hud.Aviso("CORRA ATÉ O LIMITE!", new Color(140, 220, 255), 80);
                }
                return;
            }

            if (etapa == 1 && Timer >= 70)
            {
                if (Servidor)
                    Projectile.NewProjectile(NPC.GetSource_FromAI(), NPC.Center, Vector2.Zero, ModContent.ProjectileType<VarreduraDoChao>(), 30, 0f, Main.myPlayer, NPC.whoAmI, ladoAtaque);
                SoundEngine.PlaySound(SoundID.Item120, NPC.Center);
                etapa = 2;
                Timer = 0;
            }

            if (etapa == 2 && Timer >= VarreduraDoChao.Duracao + 20)
                IrPara(PAUSA);
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
                    Hud.Banner("TRANSIÇÃO: O Giro de Armotheus", new Color(255, 120, 120));
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

            // Encostar no centro = wipe (o servidor vê a posição de todos)
            if (Servidor)
                foreach (Player p in Main.ActivePlayers)
                    if (!p.dead && p.Distance(NPC.Center) < 70f)
                        WipeGlobal();

            if (PortaoQuebrado)
            {
                CombatText.NewText(NPC.Hitbox, new Color(255, 230, 120), "POSTURA QUEBRADA!", true);
                Hud.Banner("ETAPA 2: A Forma Verdadeira", new Color(255, 140, 140));
                ScreenShakeSystem.StartShake(14f);
                NPC.rotation = 0f;
                Stagger = 0f;
                SalvarCheckpoint(PAUSA, PosOcioso);
                IrPara(PAUSA);
            }
            else if (Timer >= TempoGiro)
                WipeGlobal();
        }

        // ═══════════════ ETAPA 2: A FORMA VERDADEIRA ═══════════════
        private void FazerCortes()
        {
            MoverAte(PosOcioso + new Vector2(0f, -80f), VelMover);

            if ((Timer == 10 || Timer == 80) && Servidor)
            {
                bool vertical = (Timer == 10) == (contadorAtaque % 2 == 0);
                if (vertical)
                {
                    // 7 colunas, 1 brecha segura
                    const int colunas = 7;
                    int brecha = Main.rand.Next(colunas);
                    float largura = (ArenaDir - ArenaEsq) / colunas;
                    for (int c = 0; c < colunas; c++)
                        if (c != brecha)
                            RetanguloPerigo.Criar(NPC, new Vector2(ArenaEsq + largura * (c + 0.5f), CentroArena.Y), new Vector2(largura * 0.86f, ArenaPiso - ArenaTopo), 55, TipoPerigo.Corte, 32);
                }
                else
                {
                    // 8 faixas na altura TODA da arena, 2 brechas (antes eram 4 faixas baixas: era só voar por cima)
                    const int n = 8;
                    float altura = ArenaPiso - ArenaTopo;
                    int b1 = Main.rand.Next(n), b2 = (b1 + 3 + Main.rand.Next(3)) % n;
                    for (int k = 0; k < n; k++)
                        if (k != b1 && k != b2)
                            RetanguloPerigo.Criar(NPC, new Vector2(CentroArena.X, ArenaPiso - 40f - k * ((altura - 80f) / (n - 1))), new Vector2(ArenaDir - ArenaEsq, 90f), 55, TipoPerigo.Corte, 32);
                }
            }
            if (Timer == 10 || Timer == 80)
                SoundEngine.PlaySound(SoundID.Item71, NPC.Center);
            if (Timer >= 160)
                IrPara(PAUSA);
        }

        private Vector2 PosClone(int lado) => new(lado < 0 ? ArenaEsq + 90f : ArenaDir - 90f, ArenaPiso - 90f);

        private void FazerClones()
        {
            MoverAte(PosOcioso + new Vector2(0f, -140f), VelMover);
            if (Timer == 1)
            {
                if (Servidor)
                {
                    ladoErrado = Main.rand.NextBool() ? 1 : -1;
                    NPC.netUpdate = true;
                }
                SoundEngine.PlaySound(SoundID.Item8, NPC.Center);
                Hud.Aviso("MIRE (MOUSE E CORPO) NO CLONE DE COR ERRADA!", new Color(255, 120, 120), 110);
            }

            if (Timer == 110)
            {
                // Resolução local: o mouse E o corpo virados para o clone errado
                if (Main.netMode != NetmodeID.Server)
                {
                    Player eu = Main.LocalPlayer;
                    if (!eu.dead && JogadorNaRaid(eu))
                    {
                        bool mouseCerto = Math.Sign(Main.MouseWorld.X - eu.Center.X) == ladoErrado;
                        bool corpoCerto = eu.direction == ladoErrado;
                        if (!(mouseCerto && corpoCerto))
                            eu.Hurt(PlayerDeathReason.ByNPC(NPC.whoAmI), 130, -ladoErrado);
                    }
                }
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.3f }, NPC.Center);
                ScreenShakeSystem.StartShake(8f);
            }
            if (Timer >= 150)
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
                    alvoSalto = new Vector2(MathHelper.Clamp(Alvo.Center.X, ArenaEsq + 60f, ArenaDir - 60f), ArenaPiso - 62f);
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
                RetanguloPerigo.Criar(NPC, alvoSalto + new Vector2(0f, 20f), new Vector2(300f, 90f), 1, TipoPerigo.Corte, 38);
                ScreenShakeSystem.StartShakeAtPoint(alvoSalto, 10f);
                SoundEngine.PlaySound(SoundID.Item70, NPC.Center);
            }
        }

        // ═══════════════ TRANSIÇÃO 2: A NEVASCA ═══════════════
        private Rectangle Abrigo => new((int)(NPC.Center.X + 40f), (int)ArenaTopo, 190, (int)(ArenaPiso - ArenaTopo));

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
                    posSilvanna = new Vector2(ArenaDir - 130f, ArenaPiso - 64f);
                    Hud.Clarao(0.8f);
                    Hud.Banner("TRANSIÇÃO: A Nevasca", new Color(170, 230, 255));
                    Hud.Aviso("FIQUE ATRÁS DO CAVALEIRO!", new Color(170, 230, 255), 180);
                    Dialogo.Falar("Silvanna", "A tempestade sempre vem. Ele é o único que fica de pé na frente dela.", CorSilvanna, 200);
                    SoundEngine.PlaySound(SoundID.Item120 with { Pitch = -0.4f }, NPC.Center);
                }
                if (MoverAte(new Vector2(ArenaEsq + 110f, ArenaPiso - 62f), VelMover * 1.4f))
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

                    // A lança: a cada 110 ticks ele tenta empurrar quem está atrás dele
                    if (Timer % 110 == 109 && abrigado && Dificuldade.Mitico)
                    {
                        eu.velocity.X += 6f;
                        eu.Hurt(PlayerDeathReason.ByNPC(NPC.whoAmI), 20, 1);
                    }
                }

                for (int i = 0; i < 4; i++)
                {
                    Vector2 de = new(Main.screenPosition.X - 20f, Main.screenPosition.Y + Main.rand.NextFloat(Main.screenHeight));
                    Dust.NewDustPerfect(de, DustID.Snow, new Vector2(Main.rand.NextFloat(14f, 22f), Main.rand.NextFloat(-1f, 2f)), 0, default, 1.2f).noGravity = true;
                }
            }
            if (Timer % 110 == 109)
                SoundEngine.PlaySound(SoundID.Item1 with { Pitch = -0.5f }, NPC.Center);

            if (PortaoQuebrado)
            {
                CombatText.NewText(NPC.Hitbox, new Color(255, 230, 120), "POSTURA QUEBRADA!", true);
                Hud.Banner("ÚLTIMA ETAPA: O Cavaleiro", new Color(170, 230, 255));
                Stagger = 0f;
                SalvarCheckpoint(PAUSA, new Vector2(CentroArena.X, ArenaPiso - 60f));
                IrPara(PAUSA);
            }
            else if (Timer >= TempoNevasca)
                WipeGlobal();
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
                if (Timer > 1 && MoverAte(new Vector2(ladoAtaque == 1 ? ArenaEsq + 70f : ArenaDir - 70f, ArenaPiso - 62f), VelMover * 1.5f))
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

            NPC.velocity = new Vector2(ladoAtaque * 24f, 0f);
            if (Fase >= 3 && Timer % 4 == 0 && Servidor) // Deslize Glacial: deixa o chão congelado
                Projectile.NewProjectile(NPC.GetSource_FromAI(), new Vector2(NPC.Center.X, ArenaPiso - 9f), Vector2.Zero, ModContent.ProjectileType<RastroDeGelo>(), 0, 0f, Main.myPlayer);
            bool chegou = ladoAtaque == 1 ? NPC.Center.X >= ArenaDir - 70f : NPC.Center.X <= ArenaEsq + 70f;
            if (chegou || Timer > 90)
            {
                NPC.velocity = Vector2.Zero;
                ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 8f);
                IrPara(PAUSA);
            }
        }

        private void FazerEscudo()
        {
            int dir = Alvo.Center.X > NPC.Center.X ? 1 : -1;
            if (etapa == 0)
            {
                MoverAte(new Vector2(Alvo.Center.X - dir * 150f, ArenaPiso - 62f), VelMover);
                if (Math.Abs(Alvo.Center.X - NPC.Center.X) < 230f || Timer > 70)
                {
                    etapa = 1;
                    Timer = 0;
                    ladoAtaque = dir;
                }
                return;
            }

            NPC.velocity *= 0.8f;
            if (Timer == 35)
            {
                Vector2 frente = NPC.Center + new Vector2(ladoAtaque * 130f, 0f);
                RetanguloPerigo.Criar(NPC, frente, new Vector2(210f, 160f), 1, TipoPerigo.Atordoar, 30);
                if (Main.netMode != NetmodeID.Server)
                {
                    Player eu = Main.LocalPlayer;
                    Rectangle area = new((int)(frente.X - 105f), (int)(frente.Y - 80f), 210, 160);
                    if (!eu.dead && area.Intersects(eu.Hitbox))
                        eu.velocity += new Vector2(ladoAtaque * 14f, -4f);
                }
                SoundEngine.PlaySound(SoundID.Item37 with { Pitch = -0.5f }, NPC.Center);
                ScreenShakeSystem.StartShakeAtPoint(NPC.Center, 7f);
            }
            if (Timer >= 70)
                IrPara(PAUSA);
        }

        // ═══════════════ MECÂNICA FINAL: A LANÇA SOLAR ═══════════════
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
                        Hud.Banner("MECÂNICA FINAL: A Lança Solar", new Color(255, 215, 110));
                        Dialogo.Falar("Silvanna", "Ele vai trazer o sol inteiro para baixo. Achem um escudo.", CorSilvanna, 180);
                        AbrirPortao();
                    }
                    NPC.dontTakeDamage = true;
                    if (Timer < 70)
                    {
                        NPC.velocity *= 0.85f;
                        if (Main.rand.NextBool(2))
                            Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(40f, 40f), DustID.GoldFlame, -Vector2.UnitY * 2f, 0, default, 1.5f).noGravity = true;
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

                case 2: // a lança solar carrega lá em cima: o sol se forma sobre a arena
                {
                    const int Carga = 130;
                    float k = Math.Min(1f, Timer / Carga);
                    if (Main.netMode != NetmodeID.Server)
                    {
                        Hud.ClaraoAlvo = Timer < Carga ? k * 0.35f : 0f; // depois da explosão a tela volta ao normal
                        if (Timer < Carga) ScreenShakeSystem.SetUniversalRumble(k * 4f, MathHelper.TwoPi, null, 0.2f);
                    }
                    if (Timer == 1)
                        Hud.Aviso("O SOL VAI CAIR — SÓ O ESCUDO SALVA!", new Color(255, 215, 110), 120);
                    if (Timer == Carga)
                    {
                        // A explosão vem do SOL lá em cima (não do chão — senão parece que ele caiu duas vezes)
                        Hud.ClaraoAlvo = 0f;
                        Hud.Clarao(0.6f);
                        ScreenShakeSystem.StartShake(20f);
                        SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.2f, Pitch = -0.4f });
                        SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.6f });
                        if (Main.netMode != NetmodeID.Server)
                        {
                            Player eu = Main.LocalPlayer;
                            if (JogadorNaRaid(eu) && !eu.HasBuff(ModContent.BuffType<EscudoAstral>()))
                                MatarLocal(eu);
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
                    if (NPC.Center.Y + NPC.velocity.Y >= ArenaPiso - 62f)
                    {
                        NPC.Center = new Vector2(CentroArena.X, ArenaPiso - 62f);
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

        /// <summary>O sol se formando sobre a arena (Final) e o aviso da queda.</summary>
        public override void DesenharEfeitosDeMundo(SpriteBatch sb)
        {
            base.DesenharEfeitosDeMundo(sb);
            Texture2D bloom = MiscTexturesRegistry.BloomCircleSmall.Value;
            if (Estado == FINAL && etapa == 2)
            {
                float k = Timer <= 130 ? Timer / 130f : Math.Max(0f, 1f - (Timer - 130) / 25f); // some depois de explodir
                Vector2 sol = new Vector2(CentroArena.X, Math.Max(ArenaTopo + 160f, Main.screenPosition.Y + 140f)) - Main.screenPosition;
                float t = Main.GlobalTimeWrappedHourly;
                for (int i = 0; i < 16; i++) // raios sendo puxados para o sol
                {
                    float ang = i / 16f * MathHelper.TwoPi + t * 0.6f;
                    float dist = MathHelper.Lerp(900f, 120f, (t * 0.8f + i * 0.37f) % 1f);
                    sb.Draw(bloom, sol + ang.ToRotationVector2() * dist, null, new Color(255, 210, 120, 0) * k * 0.5f, ang, bloom.Size() / 2f, new Vector2(1.6f, 0.12f), 0, 0f);
                }
                sb.Draw(bloom, sol, null, new Color(255, 180, 80, 0) * k * 0.6f, 0f, bloom.Size() / 2f, 1f + k * 2.5f, 0, 0f);
                sb.Draw(bloom, sol, null, new Color(255, 250, 230, 0) * k, 0f, bloom.Size() / 2f, 0.5f + k * 1.2f, 0, 0f);
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
                int estatua = NPC.NewNPC(NPC.GetSource_FromAI(), (int)x, (int)ArenaPiso - 10, ModContent.NPCType<EstatuaViva>());
                Projectile.NewProjectile(NPC.GetSource_FromAI(), new Vector2(x + 60f, ArenaPiso - 30f), Vector2.Zero,
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
            if (Timer == 20) Dialogo.Falar("Silvanna", "...Chega, Armotheus. Você já mostrou o que precisava.", CorSilvanna, 180);
            if (Timer == 210) Dialogo.Falar("Silvanna", "A Astrânia não é sua, nem minha. É de algo que está vindo. Prepare-se.", CorSilvanna, 200);

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

            if (Estado == T_GIRO && etapa == 1 && ShaderManager.TryGetFilter("AscensaoAstral.BuracoNegroFilter", out ManagedScreenFilter giro))
            {
                giro.SetFocusPosition(NPC.Center);
                giro.TrySetParameter("zoom", Main.GameViewMatrix.Zoom);
                giro.TrySetParameter("raioHorizonte", 40f);
                giro.TrySetParameter("corDisco", new Vector3(0.5f, 0.85f, 1f));
                giro.SetTexture(MiscTexturesRegistry.TurbulentNoise.Value, 1, SamplerState.LinearWrap);
                giro.Activate();
            }

            Player local = Main.LocalPlayer;
            if (NevascaAtiva && JogadorNaRaid(local) && ShaderManager.TryGetFilter("AscensaoAstral.NevascaFilter", out ManagedScreenFilter nev))
            {
                // A nevasca "crítica": neve em camadas + névoa + cristais de gelo crescendo das bordas conforme você congela
                nev.TrySetParameter("intensidade", 1f);
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
            => (Estado == PENDULO && etapa == 1) || (Estado == INVESTIDA && etapa == 2) || (Estado == SALTO);

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

            if (Estado == CLONES)
            {
                DesenharClone(sb, screenPos, -1);
                DesenharClone(sb, screenPos, 1);
            }
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

            if (Estado == VARREDURA_CHAO && etapa == 1)
            {
                // Faixa do chão que vai ser varrida (tudo menos a ponta segura)
                float seguro = VarreduraDoChao.Seguro;
                Rectangle r = ladoAtaque == 1
                    ? new Rectangle((int)ArenaEsq, (int)ArenaPiso - 50, (int)(ArenaDir - ArenaEsq - seguro), 50)
                    : new Rectangle((int)(ArenaEsq + seguro), (int)ArenaPiso - 50, (int)(ArenaDir - ArenaEsq - seguro), 50);
                Desenho.RetanguloAviso(r, new Color(120, 210, 255), pulso);
            }

            if (Estado == INVESTIDA && etapa == 1)
                Desenho.RetanguloAviso(new Rectangle((int)ArenaEsq, (int)(NPC.Center.Y - 60f), (int)(ArenaDir - ArenaEsq), 120), new Color(200, 240, 255), pulso);

            if (Estado == LANCA && (int)(Timer - 1) % 60 < 35)
                Desenho.Risco(NPC.Center, NPC.Center + NPC.DirectionTo(Alvo.Center) * 1400f, 10f, new Color(150, 230, 255) * 0.55f);

            if (Estado == SALTO)
                Desenho.RetanguloAviso(new Rectangle((int)(alvoSalto.X - 150f), (int)(alvoSalto.Y - 25f), 300, 90), new Color(255, 80, 80), pulso);

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
                string folha = Estado switch { PENDULO => "Ataque", RAJADA_TRIPLA or VARREDURA_CHAO => "Laser", _ => "Idle" };
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
                            Projectile.NewProjectile(NPC.GetSource_FromAI(), de, new Vector2(0f, -24f), ModContent.ProjectileType<LancaDoCeu>(), 28, 0f, Main.myPlayer, x, ArenaTopo, ArenaPiso);
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

    /// <summary>Varre o chão de uma parede até quase a outra. A ponta final (Seguro) fica livre: "passe até o limite".</summary>
    public class VarreduraDoChao : ModProjectile
    {
        public override string Texture => "AscensaoAstral/Assets/Vazio";
        public const int Duracao = 100;
        public const float Seguro = 90f;
        private const float Altura = 50f;

        public override void SetStaticDefaults() => ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3000;

        public override void SetDefaults()
        {
            Projectile.width = 16;
            Projectile.height = 16;
            Projectile.hostile = true;
            Projectile.tileCollide = false;
            Projectile.penetrate = -1;
            Projectile.timeLeft = Duracao;
        }

        private RaidSilvanna Dono => Main.npc[(int)Projectile.ai[0]].ModNPC as RaidSilvanna;

        private Rectangle Area
        {
            get
            {
                RaidSilvanna d = Dono;
                if (d is null) return Rectangle.Empty;
                float progresso = Utils.GetLerpValue(0f, Duracao * 0.75f, Duracao - Projectile.timeLeft, true);
                float total = d.ArenaDir - d.ArenaEsq - Seguro;
                float comprimento = total * progresso;
                return Projectile.ai[1] == 1
                    ? new Rectangle((int)d.ArenaEsq, (int)(d.ArenaPiso - Altura), (int)comprimento, (int)Altura)
                    : new Rectangle((int)(d.ArenaDir - comprimento), (int)(d.ArenaPiso - Altura), (int)comprimento, (int)Altura);
            }
        }

        public override void AI()
        {
            if (Dono is null || !Dono.NPC.active) Projectile.Kill();
            ScreenShakeSystem.SetUniversalRumble(1.5f, MathHelper.TwoPi, null, 0.2f);
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) => Area.Intersects(targetHitbox);

        public override void OnHitPlayer(Player target, Player.HurtInfo info) => target.AddBuff(BuffID.Chilled, 180);

        public override bool PreDraw(ref Color lightColor)
        {
            Rectangle r = Area;
            if (r.Width < 4) return false;
            Desenho.Golpe(Desenho.Estilo.Gelo, new Vector2(r.Left, r.Center.Y), new Vector2(r.Right, r.Center.Y), Altura * 1.1f, new Color(110, 200, 255));
            return false;
        }
    }

    /// <summary>Lança arremessada pelo cavaleiro.</summary>
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
