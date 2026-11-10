using System;
using AscensaoAstral.Content.Projectiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AscensaoAstral.Content.Bosses.Astharoth
{
    /// <summary>
    /// O DRAGÃO DO ÉTER — o "irmão" do Astharoth. Não toma dano: ele é um perigo que PERSEGUE.
    ///
    /// Quem manda nele é o Astharoth (ai[0] = índice do chefe), pelo <see cref="Astharoth.ModoDragao"/>:
    ///   0 PERSEGUIR  vai atrás do alvo virando aos poucos; a cada 4 s pisca, para, mira e dá um BOTE em linha reta
    ///   1 RASANTE    se posiciona de um lado do alvo e atravessa a tela (a mecânica de "olhe para o dragão")
    ///   2 RONDAR     fica circulando no alto, sem atacar (na etapa 2 ele fica assim, DE FUNDO)
    ///  3 COMANDADO  o Astharoth diz para onde ir (a Travessia, gigante; e a mordida da etapa 2)
    ///  -1            some (fusão, despedida, chefe morto)
    ///
    /// PLACEHOLDER: desenha os sprites do Dragão Fantasma do Cultista pintados de éter. Troque por sprites seus
    /// mudando as texturas em <see cref="TexturaDoSegmento"/>.
    /// </summary>
    public class DragaoDoEter : ModNPC
    {
        public override string Texture => "AscensaoAstral/Assets/Vazio";

        private const int Segmentos = 14;
        private const float Distancia = 38f;
        private const int Ciclo = 300, InicioMira = 180, InicioBote = 240; // 1 s inteiro mirando antes do bote

        private NPC Dono => Main.npc[(int)NPC.ai[0]];
        private ref float CicloTimer => ref NPC.ai[1];

        // Corpo: só visual + colisão local (cada cliente calcula igual a partir da cabeça)
        private readonly Vector2[] corpo = new Vector2[Segmentos];
        private bool corpoIniciado;
        private int ultimoModo = -99;

        public override void SetStaticDefaults()
        {
            NPCID.Sets.NPCBestiaryDrawModifiers sem = new() { Hide = true };
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, sem);
            NPCID.Sets.MPAllowedEnemies[Type] = true;
            NPCID.Sets.MustAlwaysDraw[Type] = true; // o corpo é comprido: desenha mesmo com a cabeça fora da tela
        }

        public override void SetDefaults()
        {
            NPC.width = 54;
            NPC.height = 54;
            NPC.damage = 70;
            NPC.lifeMax = 1000;
            NPC.dontTakeDamage = true;
            NPC.immortal = true;
            NPC.noGravity = true;
            NPC.noTileCollide = true;
            NPC.knockBackResist = 0f;
            NPC.aiStyle = -1;
            NPC.npcSlots = 0f;
            NPC.netAlways = true;
            NPC.ShowNameOnHover = false;
        }

        public override bool CheckActive() => false;

        private Astharoth ChefeOuNulo => Dono.active && Dono.ModNPC is Astharoth a ? a : null;

        public override void AI()
        {
            Astharoth chefe = ChefeOuNulo;
            int modo = chefe?.ModoDragao ?? -1;
            if (modo < 0)
            {
                Sumir();
                return;
            }

            if (!corpoIniciado)
            {
                for (int i = 0; i < Segmentos; i++) corpo[i] = NPC.Center + new Vector2(0f, (i + 1) * Distancia);
                corpoIniciado = true;
            }

            if (modo != ultimoModo)
            {
                CicloTimer = 0;
                ultimoModo = modo;
            }

            Player alvo = chefe.AlvoDoDragao(NPC.Center);
            switch (modo)
            {
                case 0: Perseguir(alvo, chefe); break;
                case 1: Rasante(chefe); break;
                case 3: Comandado(chefe); break;
                default: Rondar(chefe); break;
            }

            if (NPC.velocity.LengthSquared() > 0.25f) // parado (esperando o counter), mantém a direção
                NPC.rotation = NPC.velocity.ToRotation() + MathHelper.PiOver2;
            AtualizarCorpo();
            if (modo == 0) ColisaoDoCorpo();

            Lighting.AddLight(NPC.Center, 0.6f, 0.35f, 1f);
            if (Main.rand.NextBool(2))
            {
                Vector2 p = corpo[Main.rand.Next(Segmentos)];
                Dust.NewDustPerfect(p + Main.rand.NextVector2Circular(16f, 16f), DustID.PurpleTorch, Main.rand.NextVector2Circular(1f, 1f), 0, default, 1.3f).noGravity = true;
            }
        }

        private void Perseguir(Player alvo, Astharoth chefe)
        {
            if (alvo is null) { Rondar(chefe); return; }
            CicloTimer++;
            float vel = chefe.DragaoNoDuelo ? 8f : 9f;

            if (CicloTimer < InicioMira)
            {
                Vector2 desejada = NPC.DirectionTo(alvo.Center) * vel;
                NPC.velocity = Vector2.Lerp(NPC.velocity, desejada, 0.03f);
            }
            else if (CicloTimer < InicioBote)
            {
                // Mira: desacelera e "encara" o alvo — é a sua deixa para sair da frente
                NPC.velocity = Vector2.Lerp(NPC.velocity, NPC.DirectionTo(alvo.Center) * 3f, 0.15f);
                if (CicloTimer == InicioMira)
                    SoundEngine.PlaySound(SoundID.DD2_BetsyScream with { Volume = 0.5f, Pitch = 0.3f }, NPC.Center);
            }
            else if (CicloTimer == InicioBote)
            {
                NPC.velocity = NPC.DirectionTo(alvo.Center) * 23f;
                SoundEngine.PlaySound(SoundID.DD2_BetsyWindAttack with { Pitch = -0.2f }, NPC.Center);
                NPC.netUpdate = true;
            }
            else if (CicloTimer > InicioBote + 40)
                NPC.velocity *= 0.94f;

            if (CicloTimer >= Ciclo)
            {
                CicloTimer = 0;
                NPC.netUpdate = true;
            }
        }

        private void Rasante(Astharoth chefe)
        {
            int t = chefe.TimerRasante;
            int lado = chefe.LadoDoDragao;
            Player alvo = chefe.AlvoDoDragao(NPC.Center);
            if (alvo is null) return;

            if (t < 100)
            {
                Vector2 destino = alvo.Center + new Vector2(lado * 950f, -20f);
                Vector2 v = (destino - NPC.Center) * 0.12f;
                NPC.velocity = v.Length() > 45f ? v.SafeNormalize(Vector2.Zero) * 45f : v;
                if (NPC.velocity.LengthSquared() < 0.5f) NPC.velocity = new Vector2(-lado * 0.8f, 0f); // fica "olhando" para dentro
            }
            else
                NPC.velocity = new Vector2(-lado * 30f, 0f);
        }

        private void Comandado(Astharoth chefe)
        {
            if (!chefe.ComandoDoDragao(NPC.Center, out Vector2 destino, out float vel, out float suave))
            {
                Rondar(chefe);
                return;
            }
            Vector2 para = destino - NPC.Center;
            float v = Math.Min(vel, para.Length() * 0.15f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, para.SafeNormalize(Vector2.Zero) * v, suave);
        }

        private float Escala => ChefeOuNulo?.EscalaDoDragao ?? 1f;

        private void Rondar(Astharoth chefe)
        {
            CicloTimer++;
            Vector2 destino = chefe.CentroArena + new Vector2(MathF.Cos(CicloTimer * 0.02f) * 520f, -300f + MathF.Sin(CicloTimer * 0.04f) * 60f);
            NPC.velocity = Vector2.Lerp(NPC.velocity, (destino - NPC.Center).SafeNormalize(Vector2.Zero) * 9f, 0.05f);
        }

        private void AtualizarCorpo()
        {
            Vector2 anterior = NPC.Center;
            for (int i = 0; i < Segmentos; i++)
            {
                Vector2 d = corpo[i] - anterior;
                float dist = Distancia * Escala;
                if (d.Length() > dist || d == Vector2.Zero)
                    corpo[i] = anterior + d.SafeNormalize(Vector2.UnitY) * dist;
                anterior = corpo[i];
            }
        }

        private void ColisaoDoCorpo()
        {
            if (Main.netMode == NetmodeID.Server) return;
            Player eu = Main.LocalPlayer;
            if (eu.dead || eu.immune) return;
            Rectangle hb = eu.Hitbox;
            for (int i = 0; i < Segmentos; i++)
            {
                int r = (int)(20 * Escala);
                Rectangle seg = new((int)corpo[i].X - r, (int)corpo[i].Y - r, 2 * r, 2 * r);
                if (seg.Intersects(hb))
                {
                    eu.Hurt(PlayerDeathReason.ByNPC(NPC.whoAmI), 50, Math.Sign(eu.Center.X - corpo[i].X));
                    break;
                }
            }
        }

        private void Sumir()
        {
            if (Main.netMode != NetmodeID.MultiplayerClient)
            {
                for (int i = 0; i < 30; i++)
                    Dust.NewDustPerfect(NPC.Center + Main.rand.NextVector2Circular(60f, 60f), DustID.PurpleTorch, Main.rand.NextVector2Circular(6f, 6f), 0, default, 1.8f).noGravity = true;
                NPC.active = false;
                NPC.netUpdate = true;
                if (Main.netMode == NetmodeID.Server)
                    NetMessage.SendData(MessageID.SyncNPC, number: NPC.whoAmI);
            }
        }

        /// <summary>Boca do dragão (de onde saem as bolas de fogo do duelo).</summary>
        public Vector2 Boca => NPC.Center + (NPC.rotation - MathHelper.PiOver2).ToRotationVector2() * 30f * Escala;

        public bool Mirando => ultimoModo == 0 && CicloTimer >= InicioMira && CicloTimer < InicioBote;

        public override bool CanHitPlayer(Player target, ref int cooldownSlot) => ultimoModo == 0;

        // ─── Desenho ───
        private static int TexturaDoSegmento(int i)
        {
            if (i == Segmentos - 1) return NPCID.CultistDragonTail;
            if (i == 0 || i == 7) return NPCID.CultistDragonBody1;     // com patas
            if (i == 4 || i == 11) return NPCID.CultistDragonBody4;
            return i % 2 == 0 ? NPCID.CultistDragonBody2 : NPCID.CultistDragonBody3;
        }

        private static Texture2D Tex(int tipo, out Rectangle quadro)
        {
            Main.instance.LoadNPC(tipo);
            Texture2D t = TextureAssets.Npc[tipo].Value;
            int quadros = Math.Max(1, Main.npcFrameCount[tipo]);
            quadro = new Rectangle(0, 0, t.Width, t.Height / quadros);
            return t;
        }

        public override bool PreDraw(SpriteBatch sb, Vector2 screenPos, Color drawColor)
        {
            if (!corpoIniciado) return false;

            Astharoth chefe = ChefeOuNulo;
            float esc = Escala;
            float alfa = chefe?.DragaoDeFundo == true ? 0.5f : 1f; // de fundo: uma silhueta
            Color tinta = new Color(235, 215, 255) * alfa;      // quase branco: o sprite do Cultista já é escuro
            Color brilho = new Color(170, 110, 255) with { A = 0 };
            brilho *= alfa;
            float pulso = 0.5f + 0.5f * MathF.Sin(Main.GlobalTimeWrappedHourly * 5f);

            // Aura (aditiva) por trás do corpo inteiro
            for (int i = Segmentos - 1; i >= 0; i -= 2)
                Desenho.Brilho(corpo[i], brilho * 0.6f, 1.1f * esc);

            // Corpo, da cauda para a cabeça
            for (int i = Segmentos - 1; i >= 0; i--)
            {
                Vector2 frente = i == 0 ? NPC.Center : corpo[i - 1];
                float rot = (frente - corpo[i]).ToRotation() + MathHelper.PiOver2;
                Texture2D t = Tex(TexturaDoSegmento(i), out Rectangle q);
                sb.Draw(t, corpo[i] - screenPos, q, tinta * 0.95f, rot, q.Size() / 2f, 1.1f * esc, SpriteEffects.None, 0f);
                sb.Draw(t, corpo[i] - screenPos, q, brilho * 0.55f, rot, q.Size() / 2f, 1.22f * esc, SpriteEffects.None, 0f);
            }

            // Cabeça
            Texture2D cab = Tex(NPCID.CultistDragonHead, out Rectangle qc);
            Color tintaCabeca = chefe?.JanelaAberta == true ? Color.Lerp(tinta, new Color(110, 190, 255), 0.7f) : tinta; // azul no counter
            sb.Draw(cab, NPC.Center - screenPos, qc, tintaCabeca, NPC.rotation, qc.Size() / 2f, 1.15f * esc, SpriteEffects.None, 0f);
            sb.Draw(cab, NPC.Center - screenPos, qc, brilho * 0.6f, NPC.rotation, qc.Size() / 2f, 1.3f * esc, SpriteEffects.None, 0f);

            // Olhos acesos; quando vai dar o bote, brilham forte + linha de mira
            Desenho.Brilho(Boca, new Color(255, 120, 255), Mirando ? 1.2f + pulso * 0.5f : 0.35f);
            if (Mirando && ChefeOuNulo?.AlvoDoDragao(NPC.Center) is Player alvo)
            {
                Vector2 dir = NPC.DirectionTo(alvo.Center);
                Desenho.Faixa(NPC.Center, NPC.Center + dir * 1400f, 4f + pulso * 6f, new Color(200, 90, 255) * 0.5f, new Color(255, 180, 255));
            }

            chefe?.DesenharTravessia(); // desmoronamento, parede de luz, marca da mordida, brilho do counter
            return false;
        }
    }
}
