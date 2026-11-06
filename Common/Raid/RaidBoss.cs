using System.Collections.Generic;
using System;
using System.IO;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AscensaoAstral.Common.Raid
{
    /// <summary>
    /// BASE DE TODAS AS RAIDS. Um chefe novo herda daqui e só escreve os próprios estados.
    ///
    /// O que a base já faz sozinha:
    ///  - arena em volta de quem invocou (DefinirArena)
    ///  - máquina de estados: Estado + Timer + etapa (IrPara)
    ///  - stagger: 60% do dano enche a barra; cheia → stunPendente; nos PORTÕES a vida congela e o dano vira stagger
    ///  - checkpoints: SalvarCheckpoint() no começo de cada etapa
    ///  - WIPE: se todos na raid morrerem, reseta a luta para o último checkpoint e todo mundo renasce lá
    ///  - despawn controlado: só vai embora se ninguém estiver na raid (CheckActive = false)
    ///  - desenho por cima dos jogadores, rede de segurança contra NaN, diagnóstico no HUD
    /// </summary>
    public abstract class RaidBoss : ModNPC
    {
        public const int REINICIANDO = 999;
        protected const float StaggerRatio = 0.6f;
        protected const float StaggerDecaimento = 1.2f;

        // ─── Estado sincronizado (ai[] já vai pela rede sozinho) ───
        public int Estado
        {
            get => (int)NPC.ai[0];
            protected set => NPC.ai[0] = value;
        }
        protected ref float Timer => ref NPC.ai[1];
        public ref float Stagger => ref NPC.ai[2];
        public ref float Fase => ref NPC.ai[3];

        // ─── Estado extra (vai em SendExtraAI) ───
        protected int etapa;
        protected int contadorAtaque;
        protected bool stunPendente;
        public float ArenaEsq, ArenaDir, ArenaTopo, ArenaPiso;
        protected int checkpointEstado;
        protected int checkpointVida;
        public Vector2 PontoDeRetorno;
        protected float vidaCongelada;
        private bool wipeAgendado;

        // ─── Local ───
        private float vidaAnterior;
        private int tempoSemNinguemVivo;
        private int tempoSemNinguem;
        public int FlashDano;

        // ─── O que cada chefe define ───
        public abstract float StaggerMax { get; }
        public virtual float MetaStagger => StaggerMax;
        /// <summary>True quando o chefe está num portão (vida congelada, dano vira stagger).</summary>
        public virtual bool PortaoAtivo => false;
        public virtual float TempoRestantePortao => -1f;
        public virtual bool EstaAtordoado => false;
        public virtual bool MostrarBarraDeQuebra => true;
        public virtual string NomeDoEstado => Estado.ToString();
        public virtual float LarguraArena => 2400f;  // 150 blocos — a Supreme Calamitas usa uma caixa de 2500 px
        public virtual float AlturaArena => 1300f;  // ~80 blocos de altura

        protected abstract void ExecutarEstado();

        // ─── Vitrine de ataques (debug): veja Vitrine.cs ───
        public virtual int EstadoDePausa => 2;
        public virtual IEnumerable<Vitrine.Item> AtaquesDaVitrine(bool comMecanicas) { yield break; }
        public void VitrineIr(int estado, int fase)
        {
            LimparPerigos();
            Fase = fase;
            IrPara(estado);
        }
        /// <summary>Chamado ao voltar para o checkpoint: reposicione o chefe, limpe flags da etapa.</summary>
        protected abstract void AoReiniciar();
        /// <summary>Faça a luta normal "não contar" o stun enquanto isto for verdade (cutscenes etc.).</summary>
        protected virtual bool PausarStagger => false;
        /// <summary>Arena fechada: quem sai pelos lados ou voa acima do teto é empurrado de volta e se machuca.</summary>
        protected virtual bool ConfinarAgora => true;
        /// <summary>Permite liberar jogadores específicos (ex.: quem está no Éter enquanto a arena é a de fora).</summary>
        protected virtual bool DeveConfinar(Player p) => true;
        protected virtual Color CorDaParede => new(170, 120, 255);
        protected virtual Color CorDasFaiscas => new(255, 220, 160);

        protected virtual void EscreverExtra(BinaryWriter w) { }
        protected virtual void LerExtra(BinaryReader r) { }

        // ─── Atalhos ───
        protected Player Alvo => Main.player[NPC.target];
        protected static bool Servidor => Main.netMode != NetmodeID.MultiplayerClient;
        public Vector2 CentroArena => new((ArenaEsq + ArenaDir) / 2f, (ArenaTopo + ArenaPiso) / 2f);
        public Rectangle RetanguloArena => new((int)ArenaEsq, (int)ArenaTopo, (int)(ArenaDir - ArenaEsq), (int)(ArenaPiso - ArenaTopo));
        public bool Reiniciando => Estado == REINICIANDO;

        public static RaidBoss Ativo
        {
            get
            {
                foreach (NPC n in Main.ActiveNPCs)
                    if (n.ModNPC is RaidBoss b)
                        return b;
                return null;
            }
        }

        /// <summary>Quem conta como "na raid". O Astharoth inclui também quem está no Éter.</summary>
        public virtual bool JogadorNaRaid(Player p) => p.active && (Raids.DentroDeUmaRaid || Vector2.Distance(p.Center, CentroArena) < 2600f);

        /// <summary>Configuração comum: chame no fim do SetDefaults de cada chefe.</summary>
        protected void ConfigurarRaid()
        {
            NPC.boss = true;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;
            NPC.npcSlots = 15f;
            NPC.hide = true; // desenhado depois dos jogadores (DrawBehind)
        }

        public override void DrawBehind(int index) => Main.instance.DrawCacheNPCsOverPlayers.Add(index);

        public override bool CheckActive() => false; // nós cuidamos do despawn

        public override void OnSpawn(IEntitySource source)
        {
            Player p = Main.player[Player.FindClosest(NPC.position, NPC.width, NPC.height)];
            EhMemoria = JaFoiDerrotado;
            DefinirArena(p);
            SalvarCheckpoint(0, new Vector2(CentroArena.X, ArenaPiso - 60f));
            NPC.netUpdate = true;
        }

        /// <summary>A arena nasce em volta de quem invocou: chão = pés do jogador.</summary>
        protected virtual void DefinirArena(Player p)
        {
            // Dentro da dimensão da raid a arena é fixa: o próprio mapa
            if (Raids.Atual is MundoDeRaid mundo)
            {
                Rectangle a = mundo.Arena;
                ArenaEsq = a.Left; ArenaDir = a.Right; ArenaTopo = a.Top; ArenaPiso = a.Bottom;
                return;
            }
            ArenaEsq = p.Center.X - LarguraArena / 2f;
            ArenaDir = p.Center.X + LarguraArena / 2f;
            ArenaPiso = p.Bottom.Y;
            ArenaTopo = ArenaPiso - AlturaArena;
        }

        // ═══════════════ IA ═══════════════
        public override void AI()
        {
            if (float.IsNaN(NPC.position.X) || float.IsNaN(NPC.position.Y) || float.IsNaN(NPC.velocity.X) || float.IsNaN(NPC.velocity.Y))
            {
                NPC.velocity = Vector2.Zero;
                NPC.Center = CentroArena;
            }

            int naRaid = 0, vivos = 0;
            foreach (Player p in Main.ActivePlayers)
            {
                if (!JogadorNaRaid(p)) continue;
                naRaid++;
                if (!p.dead) vivos++;
            }

            // Ninguém por perto (todo mundo fugiu ou desconectou): vai embora
            if (naRaid == 0)
            {
                NPC.velocity.Y -= 0.3f;
                if (++tempoSemNinguem > 180 && Servidor)
                {
                    NPC.active = false;
                    NPC.netUpdate = true;
                }
                return;
            }
            tempoSemNinguem = 0;

            // Todos na raid morreram → WIPE (volta ao checkpoint)
            if (!Reiniciando && vivos == 0)
            {
                if (++tempoSemNinguemVivo > 60)
                    IniciarReinicio();
            }
            else
                tempoSemNinguemVivo = 0;

            if (Alvo.dead || !Alvo.active || !JogadorNaRaid(Alvo))
                NPC.TargetClosest(false);

            Timer++;

            if (Reiniciando)
            {
                FazerReinicio();
                return;
            }

            // Wipe global agendado pelo servidor: cada cliente mata o próprio jogador até todos caírem
            if (wipeAgendado)
                Wipe();

            AtualizarStagger();
            if (EhMemoria && !avisouMemoria && Main.netMode != NetmodeID.Server)
            {
                avisouMemoria = true;
                Dialogo.FalaDoEco("Isso não é ele. É só o que sobrou na memória da Astrânia... sem alma nenhuma lá dentro.");
            }
            if (Vitrine.Ligada && Main.netMode == NetmodeID.SinglePlayer)
                Vitrine.Controlar(this);
            ExecutarEstado();
            ConfinarJogadorLocal();
            AtualizarImpacto();

            if (FlashDano > 0)
                FlashDano--;
        }

        // ─── Arena fechada (contra quem só voa e desvia de tudo) ───
        private int tempoForaDaArena;

        private void ConfinarJogadorLocal()
        {
            if (Main.netMode == NetmodeID.Server || !ConfinarAgora || ArenaDir <= ArenaEsq)
                return;

            Player eu = Main.LocalPlayer;
            if (eu.dead || !JogadorNaRaid(eu) || !DeveConfinar(eu))
            {
                tempoForaDaArena = 0;
                return;
            }

            bool esquerda = eu.Left.X < ArenaEsq, direita = eu.Right.X > ArenaDir, acima = eu.Top.Y < ArenaTopo;
            if (!esquerda && !direita && !acima)
            {
                tempoForaDaArena = 0;
                return;
            }

            // Empurra de volta para dentro
            if (esquerda) eu.velocity.X = Math.Max(eu.velocity.X, 0f) + 1.4f;
            if (direita) eu.velocity.X = Math.Min(eu.velocity.X, 0f) - 1.4f;
            if (acima)
            {
                eu.velocity.Y = Math.Max(eu.velocity.Y, 0f) + 1.4f;
                eu.wingTime = Math.Min(eu.wingTime, 0f); // corta o voo de quem tenta passar por cima
                eu.rocketTime = 0;
            }

            if (tempoForaDaArena % 30 == 0)
                eu.Hurt(PlayerDeathReason.ByNPC(NPC.whoAmI), 30, 0);
            if (tempoForaDaArena % 150 == 0)
                Hud.Aviso("O VÉU FERE QUEM FOGE DA ARENA!", CorDaParede, 90);
            tempoForaDaArena++;
        }

        /// <summary>
        /// A arena no estilo da Supreme Calamitas (Calamity): tudo FORA da caixa fica escurecido e a borda é uma
        /// linha de luz fina. Desenhado pelo EfeitosDaRaid (depois dos blocos), não pelo NPC — assim não some fora da tela.
        /// </summary>
        public virtual void DesenharEfeitosDeMundo(Microsoft.Xna.Framework.Graphics.SpriteBatch sb)
        {
            DesenharArena(sb);
            DesenharImpacto(sb);
        }

        protected void DesenharArena(Microsoft.Xna.Framework.Graphics.SpriteBatch sb)
        {
            if (!ConfinarAgora || ArenaDir <= ArenaEsq)
                return;
            if (Estado == REINICIANDO) return;

            Microsoft.Xna.Framework.Graphics.Texture2D px = Terraria.GameContent.TextureAssets.MagicPixel.Value;
            Rectangle um = new(0, 0, 1, 1);
            Vector2 s0 = Main.screenPosition;
            int esq = (int)(ArenaEsq - s0.X), dir = (int)(ArenaDir - s0.X), topo = (int)(ArenaTopo - s0.Y), piso = (int)(ArenaPiso - s0.Y);
            const int longe = 4000;
            Color escuro = Color.Black * 0.55f;

            // Fora da caixa: escurecido (em cima, embaixo, esquerda, direita)
            sb.Draw(px, new Rectangle(esq - longe, topo - longe, dir - esq + longe * 2, longe), um, escuro);
            sb.Draw(px, new Rectangle(esq - longe, topo, longe, piso - topo + 16), um, escuro);
            sb.Draw(px, new Rectangle(dir, topo, longe, piso - topo + 16), um, escuro);

            // Borda: linha fina + brilho em volta, pulsando devagar
            float pulso = 0.75f + 0.25f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2f);
            Color c = CorDaParede;
            Color brilho = (c with { A = 0 }) * 0.35f * pulso;
            for (int g = 8; g >= 2; g -= 3) // brilho largo → fino
            {
                sb.Draw(px, new Rectangle(esq - g, topo - g, dir - esq + g * 2, g), um, brilho);
                sb.Draw(px, new Rectangle(esq - g, topo, g, piso - topo), um, brilho);
                sb.Draw(px, new Rectangle(dir, topo, g, piso - topo), um, brilho);
            }
            sb.Draw(px, new Rectangle(esq - 2, topo - 2, dir - esq + 4, 2), um, c * pulso);
            sb.Draw(px, new Rectangle(esq - 2, topo, 2, piso - topo), um, c * pulso);
            sb.Draw(px, new Rectangle(dir, topo, 2, piso - topo), um, c * pulso);

            // Faíscas subindo pelas paredes
            if (!Main.gamePaused && Main.rand.NextBool(3))
            {
                Vector2 p = Main.rand.NextBool() ? new Vector2(Main.rand.NextBool() ? ArenaEsq : ArenaDir, Main.rand.NextFloat(ArenaTopo, ArenaPiso))
                                                 : new Vector2(Main.rand.NextFloat(ArenaEsq, ArenaDir), ArenaTopo);
                Dust d = Dust.NewDustPerfect(p, DustID.FireworkFountain_Yellow, Main.rand.NextVector2Circular(1f, 1f), 0, c, 0.9f);
                d.noGravity = true;
            }
        }

        // ─── Impacto (queda de estrela): anel de luz + pilar + onda de choque na tela. Efeito local de cada cliente ───
        private int tempoImpacto;
        private Vector2 posImpacto;
        private Color corImpacto;
        protected const int DuracaoImpacto = 100;

        protected void IniciarImpacto(Vector2 onde, Color cor)
        {
            Hud.Clarao(0.55f);
            Luminance.Core.Graphics.ScreenShakeSystem.StartShake(24f);
            SoundEngine.PlaySound(SoundID.Item14 with { Volume = 1.4f, Pitch = -0.7f });
            SoundEngine.PlaySound(SoundID.Item122 with { Volume = 1.2f, Pitch = -0.8f });
            SoundEngine.PlaySound(SoundID.Roar with { Volume = 0.6f, Pitch = -0.9f });
            if (Main.dedServ) return;
            tempoImpacto = DuracaoImpacto;
            posImpacto = onde;
            corImpacto = cor;
            for (int i = 0; i < 90; i++)
            {
                Vector2 v = Main.rand.NextVector2CircularEdge(1f, 0.35f) * Main.rand.NextFloat(8f, 26f);
                Dust.NewDustPerfect(onde, DustID.FireworkFountain_Yellow, v, 0, cor, Main.rand.NextFloat(1.4f, 2.4f)).noGravity = true;
            }
            for (int i = 0; i < 30; i++)
                Dust.NewDustPerfect(onde + new Vector2(Main.rand.NextFloat(-30f, 30f), 0f), DustID.FireworkFountain_Yellow, new Vector2(0f, -Main.rand.NextFloat(10f, 30f)), 0, Color.White, 2f).noGravity = true;
        }

        private void AtualizarImpacto()
        {
            if (Main.dedServ || tempoImpacto <= 0) return;
            tempoImpacto--;
            float k = 1f - tempoImpacto / (float)DuracaoImpacto;
            // (sem shader de tela: o impacto é só sprites — anel, pilar e faíscas — para não ofuscar a visão)
            Lighting.AddLight(posImpacto, corImpacto.ToVector3() * 2f * (1f - k));
        }

        private void DesenharImpacto(Microsoft.Xna.Framework.Graphics.SpriteBatch sb)
        {
            if (tempoImpacto <= 0) return;
            Microsoft.Xna.Framework.Graphics.Texture2D bloom = Luminance.Assets.MiscTexturesRegistry.BloomCircleSmall.Value;
            Microsoft.Xna.Framework.Graphics.Texture2D nevoa = ModContent.Request<Microsoft.Xna.Framework.Graphics.Texture2D>("AscensaoAstral/Assets/Nevoa").Value;
            float k = 1f - tempoImpacto / (float)DuracaoImpacto;
            float some = 1f - k;
            float raio = MathHelper.Lerp(30f, 1100f, MathF.Pow(k, 0.6f));
            Color c = corImpacto with { A = 0 };
            for (int i = 0; i < 72; i++)
            {
                float ang = i / 72f * MathHelper.TwoPi;
                Vector2 p = posImpacto + new Vector2(MathF.Cos(ang) * raio, MathF.Sin(ang) * raio * 0.35f);
                sb.Draw(bloom, p - Main.screenPosition, null, c * some, 0f, bloom.Size() / 2f, 0.35f * some + 0.1f, 0, 0f);
            }
            float pilar = some * some;
            sb.Draw(nevoa, posImpacto - new Vector2(0f, 500f) - Main.screenPosition, null, new Color(255, 245, 230, 0) * pilar * 0.7f, 0f, nevoa.Size() / 2f, new Vector2(0.6f * some + 0.1f, 8f), 0, 0f);
            sb.Draw(bloom, posImpacto - Main.screenPosition, null, new Color(255, 255, 255, 0) * pilar * 0.6f, 0f, bloom.Size() / 2f, 1.6f, 0, 0f);
            sb.Draw(bloom, posImpacto - Main.screenPosition, null, c * pilar * 0.5f, 0f, bloom.Size() / 2f, 3f, 0, 0f);
        }

        /// <summary>O rastro de estrela cadente (use no PreDraw enquanto o chefe cai).</summary>
        protected void DesenharRastroDeEstrela(Color nucleo, Color cauda)
        {
            Vector2[] pontos = new Vector2[NPC.oldPos.Length];
            for (int i = 0; i < pontos.Length; i++)
                pontos[i] = NPC.oldPos[i] == Vector2.Zero ? NPC.Center : NPC.oldPos[i] + NPC.Size / 2f;
            Luminance.Core.Graphics.PrimitiveRenderer.RenderTrail(pontos, new Luminance.Core.Graphics.PrimitiveSettings(
                t => MathHelper.Lerp(150f, 6f, MathF.Pow(t, 0.6f)),
                t => Color.Lerp(Color.Lerp(Color.White, nucleo, MathF.Min(1f, t * 1.6f)), cauda, MathF.Max(0f, t * 2f - 1f)) * (1f - t)), 40);
            Microsoft.Xna.Framework.Graphics.Texture2D bloom = Luminance.Assets.MiscTexturesRegistry.BloomCircleSmall.Value;
            Main.spriteBatch.Draw(bloom, NPC.Center - Main.screenPosition, null, nucleo with { A = 0 }, 0f, bloom.Size() / 2f, 4f, 0, 0f);
            Main.spriteBatch.Draw(bloom, NPC.Center - Main.screenPosition, null, new Color(255, 255, 255, 0), 0f, bloom.Size() / 2f, 1.6f, 0, 0f);
        }

        /// <summary>A estrela piscando no alto da tela (k: 0→1 vai crescendo) + coluna de luz onde vai cair.</summary>
        protected void DesenharAvisoDeQueda(Microsoft.Xna.Framework.Graphics.SpriteBatch sb, float x, float k, Color cor)
        {
            Microsoft.Xna.Framework.Graphics.Texture2D bloom = Luminance.Assets.MiscTexturesRegistry.BloomCircleSmall.Value;
            Microsoft.Xna.Framework.Graphics.Texture2D nevoa = ModContent.Request<Microsoft.Xna.Framework.Graphics.Texture2D>("AscensaoAstral/Assets/Nevoa").Value;
            float t = Main.GlobalTimeWrappedHourly;
            float pulso = 0.5f + 0.5f * MathF.Sin(t * 40f);
            Color c = cor with { A = 0 };
            Vector2 estrela = new Vector2(x, Math.Max(Main.screenPosition.Y + 90f, ArenaTopo)) - Main.screenPosition;
            float tam = 0.4f + k * 1.4f + pulso * 0.3f;
            sb.Draw(bloom, estrela, null, c, 0f, bloom.Size() / 2f, tam * 2f, 0, 0f);
            sb.Draw(bloom, estrela, null, new Color(255, 255, 255, 0), 0f, bloom.Size() / 2f, tam, 0, 0f);
            for (int r = 0; r < 2; r++)
                sb.Draw(bloom, estrela, null, new Color(255, 250, 235, 0) * (0.6f + pulso * 0.4f), r * MathHelper.PiOver2 + t * 0.5f, bloom.Size() / 2f, new Vector2(tam * 5f, tam * 0.18f), 0, 0f);

            float altura = ArenaPiso - ArenaTopo;
            sb.Draw(nevoa, new Vector2(x, ArenaPiso - altura * 0.5f) - Main.screenPosition, null, c * k * 0.5f, 0f, nevoa.Size() / 2f, new Vector2(0.9f, altura / 200f), 0, 0f);
            sb.Draw(bloom, new Vector2(x, ArenaPiso - 10f) - Main.screenPosition, null, c * k, 0f, bloom.Size() / 2f, 1.5f + pulso * 0.5f, 0, 0f);
        }

        /// <summary>Pós-imagens quando o chefe se move rápido.</summary>
        protected void DesenharPosImagens(Microsoft.Xna.Framework.Graphics.SpriteBatch sb, Microsoft.Xna.Framework.Graphics.Texture2D tex, Rectangle? recorte, Vector2 origem, float escala, Color cor, Microsoft.Xna.Framework.Graphics.SpriteEffects flip)
        {
            if (NPC.velocity.Length() < 5f)
                return;
            for (int i = NPC.oldPos.Length - 1; i > 0; i -= 2)
            {
                if (NPC.oldPos[i] == Vector2.Zero) continue;
                float f = 1f - i / (float)NPC.oldPos.Length;
                sb.Draw(tex, NPC.oldPos[i] + NPC.Size / 2f - Main.screenPosition, recorte, (cor with { A = 0 }) * f * 0.45f, NPC.rotation, origem, escala, flip, 0f);
            }
        }

        // ─── Stagger ───
        private void AtualizarStagger()
        {
            float perda = vidaAnterior > 0f ? vidaAnterior - NPC.life : 0f;

            if (PortaoAtivo)
            {
                if (perda > 0f)
                    Stagger = Math.Min(MetaStagger, Stagger + perda * StaggerRatio);
                NPC.life = Math.Max(1, (int)vidaCongelada);
            }
            else if (!EstaAtordoado && !PausarStagger)
            {
                if (perda > 0f)
                    Stagger = Math.Min(StaggerMax, Stagger + perda * StaggerRatio);
                else
                    Stagger = Math.Max(0f, Stagger - StaggerDecaimento);

                if (Stagger >= StaggerMax)
                    stunPendente = true;
            }

            vidaAnterior = NPC.life;
        }

        /// <summary>Começa um portão: congela a vida e zera o stagger.</summary>
        protected void AbrirPortao()
        {
            vidaCongelada = NPC.life;
            vidaAnterior = NPC.life;
            Stagger = 0f;
            NPC.netUpdate = true;
        }

        protected bool PortaoQuebrado => Stagger >= MetaStagger;

        public override void ModifyIncomingHit(ref NPC.HitModifiers modifiers)
        {
            if (EstaAtordoado)
                modifiers.SourceDamage *= 2f;
        }

        public override void HitEffect(NPC.HitInfo hit)
        {
            FlashDano = 6;
            if (Main.dedServ) return;
            Vector2 ponto = NPC.Center + Main.rand.NextVector2Circular(NPC.width * 0.4f, Math.Min(NPC.height, 100) * 0.4f);
            for (int i = 0; i < 6; i++)
            {
                Dust d = Dust.NewDustPerfect(ponto, DustID.FireworkFountain_Yellow, Main.rand.NextVector2Circular(5f, 5f), 0, CorDasFaiscas, 1.1f);
                d.noGravity = true;
            }
        }

        // ─── Checkpoint e wipe ───
        protected void SalvarCheckpoint(int estado, Vector2 retorno)
        {
            checkpointEstado = estado;
            checkpointVida = NPC.life;
            PontoDeRetorno = retorno;
        }

        private void IniciarReinicio()
        {
            Estado = REINICIANDO;
            Timer = 0;
            etapa = 0;
            wipeAgendado = false;
            NPC.netUpdate = true;
            LimparPerigos();
            Hud.Banner("WIPE", new Color(255, 70, 70));
            SoundEngine.PlaySound(SoundID.Roar with { Pitch = -0.8f }, NPC.Center);
        }

        private void FazerReinicio()
        {
            NPC.dontTakeDamage = true;
            NPC.velocity *= 0.9f;

            if (Timer == 2)
            {
                NPC.life = Math.Max(1, checkpointVida);
                vidaAnterior = NPC.life;
                Stagger = 0f;
                stunPendente = false;
            }

            // Os mortos renascem neste meio-tempo (RaidJogador) e são levados até PontoDeRetorno
            if (Timer >= 240)
            {
                NPC.dontTakeDamage = false;
                Hud.Banner("Tentando de novo...", new Color(200, 200, 255));
                AoReiniciar();
                Estado = checkpointEstado;
                Timer = 0;
                etapa = 0;
                NPC.netUpdate = true;
            }
        }

        /// <summary>Mecânica falhou: mata quem está na raid (cada cliente cuida de si). Todos mortos → reinício.</summary>
        protected void Wipe()
        {
            if (Main.netMode == NetmodeID.Server)
                return;
            Player eu = Main.LocalPlayer;
            if (!eu.dead && JogadorNaRaid(eu))
                eu.KillMe(PlayerDeathReason.ByNPC(NPC.whoAmI), 99999, 0);
        }

        /// <summary>
        /// Wipe de verdade: o servidor decide e TODO MUNDO na raid cai (não só quem errou).
        /// Use quando uma mecânica falha (portão estourou, alguém encostou no núcleo...).
        /// </summary>
        protected void WipeGlobal()
        {
            if (!Servidor || wipeAgendado) return;
            wipeAgendado = true;
            NPC.netUpdate = true;
            Hud.Aviso("WIPE!", new Color(255, 60, 60), 90);
        }

        /// <summary>Morte instantânea só do jogador local (zonas mortais, rajadas solares...).</summary>
        protected void MatarLocal(Player eu)
        {
            if (!eu.dead)
                eu.KillMe(PlayerDeathReason.ByNPC(NPC.whoAmI), 99999, 0);
        }

        protected virtual void LimparPerigos()
        {
            if (!Servidor) return;
            foreach (Projectile p in Main.ActiveProjectiles)
                if (p.ModProjectile is not null && p.ModProjectile.Mod == Mod && p.hostile)
                    p.Kill();
        }

        // ─── Movimento ───
        protected void IrPara(int novo)
        {
            Estado = novo;
            Timer = 0;
            etapa = 0;
            NPC.netUpdate = true;
        }

        protected bool MoverAte(Vector2 alvo, float vel)
        {
            Vector2 d = alvo - NPC.Center;
            if (d.Length() <= vel)
            {
                NPC.Center = alvo;
                NPC.velocity = Vector2.Zero;
                return true;
            }
            NPC.velocity = d.SafeNormalize(Vector2.Zero) * vel;
            return false;
        }

        protected int JogadoresVivosNaRaid()
        {
            int n = 0;
            foreach (Player p in Main.ActivePlayers)
                if (!p.dead && JogadorNaRaid(p))
                    n++;
            return n;
        }

        // ─── Rede ───
        // ─── Memória ───
        /// <summary>
        /// A primeira luta é contra o chefe DE VERDADE. Depois que ele morre, quem aparece nas próximas vezes é só uma
        /// memória: sem alma, sem falas, com o corpo desbotado e tremido. (Cada chefe diz se já foi vencido.)
        /// </summary>
        public bool EhMemoria;
        protected virtual bool JaFoiDerrotado => false;
        private bool avisouMemoria;

        public override void ModifyTypeName(ref string typeName)
        {
            if (EhMemoria) typeName = "Memória de " + typeName;
        }

        /// <summary>Use na cor final do desenho do chefe.</summary>
        protected Color TintaDeMemoria(Color c)
        {
            if (!EhMemoria) return c;
            float t = Main.GlobalTimeWrappedHourly;
            float treme = 0.75f + 0.25f * MathF.Sin(t * 13f) * MathF.Sin(t * 3.1f);
            return Color.Lerp(c, new Color(150, 165, 210), 0.65f) * (0.7f * treme);
        }

        public override void SendExtraAI(BinaryWriter w)
        {
            w.Write(EhMemoria);
            w.Write(etapa); w.Write(contadorAtaque); w.Write(stunPendente); w.Write(wipeAgendado);
            w.Write(ArenaEsq); w.Write(ArenaDir); w.Write(ArenaTopo); w.Write(ArenaPiso);
            w.Write(checkpointEstado); w.Write(checkpointVida); w.WriteVector2(PontoDeRetorno); w.Write(vidaCongelada);
            EscreverExtra(w);
        }

        public override void ReceiveExtraAI(BinaryReader r)
        {
            EhMemoria = r.ReadBoolean();
            etapa = r.ReadInt32(); contadorAtaque = r.ReadInt32(); stunPendente = r.ReadBoolean(); wipeAgendado = r.ReadBoolean();
            ArenaEsq = r.ReadSingle(); ArenaDir = r.ReadSingle(); ArenaTopo = r.ReadSingle(); ArenaPiso = r.ReadSingle();
            checkpointEstado = r.ReadInt32(); checkpointVida = r.ReadInt32(); PontoDeRetorno = r.ReadVector2(); vidaCongelada = r.ReadSingle();
            LerExtra(r);
        }

        public string Diagnostico()
        {
            Vector2 rel = NPC.Center - Main.LocalPlayer.Center;
            return $"[debug] {NomeDoEstado} etapa {etapa} t {Timer:0} | pos rel. ({rel.X:0}, {rel.Y:0}) | vida {NPC.life} | quebra {Stagger:0}/{MetaStagger:0} | vivos {JogadoresVivosNaRaid()}";
        }
    }

    /// <summary>Desenha a arena e os efeitos grandes do chefe ativo depois dos blocos.</summary>
    public class EfeitosDaRaid : ModSystem
    {
        public override void PostDrawTiles()
        {
            if (RaidBoss.Ativo is not RaidBoss chefe)
                return;
            Main.spriteBatch.Begin(Microsoft.Xna.Framework.Graphics.SpriteSortMode.Deferred, Microsoft.Xna.Framework.Graphics.BlendState.AlphaBlend,
                Microsoft.Xna.Framework.Graphics.SamplerState.LinearClamp, Microsoft.Xna.Framework.Graphics.DepthStencilState.None,
                Microsoft.Xna.Framework.Graphics.RasterizerState.CullNone, null, Main.GameViewMatrix.TransformationMatrix);
            chefe.DesenharEfeitosDeMundo(Main.spriteBatch);
            Main.spriteBatch.End();
        }
    }
}
