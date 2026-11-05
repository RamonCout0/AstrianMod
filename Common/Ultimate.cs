using System;
using AscensaoAstral.Content.Projectiles;
using Luminance.Core.Graphics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.ID;
using Terraria.ModLoader;

namespace AscensaoAstral.Common
{
    /// <summary>
    /// A ULTIMATE DA FUSÃO (a "mioge" de cada classe). O Eco Estelar fundido carrega uma barra conforme você causa dano;
    /// cheia, aperte a tecla da ultimate (padrão: F — dá para trocar em Controles) e ela é liberada.
    ///
    ///   Guerreiro  Lâmina do Vazio     — uma meia-lua gigante que atravessa a tela
    ///   Mago       Chuva de Astra      — estrelas caem em volta do cursor
    ///   Atirador   Disparo Estelar     — um feixe que atravessa tudo na direção do cursor
    ///   Invocador  Coro dos Ecos       — ecos de luz giram em volta de você e atiram nos inimigos
    ///
    /// Pensado para crescer: a futura árvore de habilidades pode mexer em CargaPorDano, DanoBase e na própria ultimate.
    /// </summary>
    public class UltimateSistema : ModSystem
    {
        public static ModKeybind Tecla;
        public override void Load() => Tecla = KeybindLoader.RegisterKeybind(Mod, "UltimateDaFusao", "F");
        public override void Unload() => Tecla = null;
    }

    public class UltimateJogador : ModPlayer
    {
        public float Carga;                 // 0..100
        public const float CargaMax = 100f;
        public float CargaPorDano = 1f / 45f; // 45 de dano = 1% da barra (a árvore de habilidades pode mexer aqui)
        public int DanoBase = 260;
        public int TempoBrilho;

        private AstralJogador Astral => Player.GetModPlayer<AstralJogador>();
        public bool Pronta => Carga >= CargaMax;

        public override void OnHitNPCWithItem(Item item, NPC target, NPC.HitInfo hit, int damageDone) => Carregar(damageDone);
        public override void OnHitNPCWithProj(Projectile proj, NPC target, NPC.HitInfo hit, int damageDone)
        {
            if (proj.ModProjectile is not UltimateBase) Carregar(damageDone); // a própria ultimate não recarrega
        }

        private void Carregar(int dano)
        {
            if (!Astral.Fundido || Astral.Classe == ClasseAstral.Nenhuma) return;
            bool estava = Pronta;
            Carga = Math.Min(CargaMax, Carga + dano * CargaPorDano);
            if (!estava && Pronta && Player.whoAmI == Main.myPlayer)
            {
                SoundEngine.PlaySound(SoundID.Item29 with { Pitch = 0.5f, Volume = 0.7f });
                CombatText.NewText(Player.Hitbox, new Color(255, 225, 140), "ULTIMATE PRONTA!", true);
            }
        }

        public override void ProcessTriggers(TriggersSet triggersSet)
        {
            if (UltimateSistema.Tecla?.JustPressed != true || !Pronta || Player.dead || !Astral.Fundido) return;
            Carga = 0f;
            TempoBrilho = 40;
            Soltar();
        }

        public static Color CorDaClasse(ClasseAstral c) => c switch
        {
            ClasseAstral.Melee => new Color(255, 90, 100),
            ClasseAstral.Mago => new Color(140, 165, 255),
            ClasseAstral.Atirador => new Color(255, 215, 120),
            ClasseAstral.Invocador => new Color(120, 255, 190),
            _ => Color.White
        };

        private void Soltar()
        {
            IEntitySource fonte = Player.GetSource_FromThis("Ultimate");
            Vector2 mira = Main.MouseWorld;
            Vector2 dir = (mira - Player.Center).SafeNormalize(Vector2.UnitX * Player.direction);
            Hud.Clarao(0.2f);
            ScreenShakeSystem.StartShake(6f);

            switch (Astral.Classe)
            {
                case ClasseAstral.Melee:
                {
                    // investida → meia-lua → o caminho da investida "parte" um instante depois
                    int dano = (int)Player.GetTotalDamage(DamageClass.Melee).ApplyTo(DanoBase * 2);
                    Projectile.NewProjectile(fonte, Player.Center, dir, ModContent.ProjectileType<LaminaDoVazio>(), dano, 8f, Player.whoAmI, Player.Center.X, Player.Center.Y);
                    SoundEngine.PlaySound(SoundID.Item71 with { Pitch = -0.3f }, Player.Center);
                    Hud.Banner("LÂMINA DO VAZIO", CorDaClasse(ClasseAstral.Melee));
                    break;
                }
                case ClasseAstral.Mago:
                {
                    // desenha uma constelação no cursor; as estrelas caem em cada ponto (e procuram inimigos perto)
                    int dano = (int)Player.GetTotalDamage(DamageClass.Magic).ApplyTo(DanoBase);
                    Projectile.NewProjectile(fonte, mira, Vector2.Zero, ModContent.ProjectileType<ChuvaDeAstra>(), dano, 4f, Player.whoAmI);
                    SoundEngine.PlaySound(SoundID.Item105 with { Pitch = -0.2f }, mira);
                    Hud.Banner("CHUVA DE ASTRA", CorDaClasse(ClasseAstral.Mago));
                    break;
                }
                case ClasseAstral.Atirador:
                {
                    // meio segundo carregando (você fica pesado), depois o feixe
                    int dano = (int)Player.GetTotalDamage(DamageClass.Ranged).ApplyTo(DanoBase / 4);
                    Projectile.NewProjectile(fonte, Player.Center, dir, ModContent.ProjectileType<DisparoEstelar>(), dano, 2f, Player.whoAmI);
                    SoundEngine.PlaySound(SoundID.Item117 with { Pitch = -0.4f }, Player.Center);
                    Hud.Banner("DISPARO ESTELAR", CorDaClasse(ClasseAstral.Atirador));
                    break;
                }
                case ClasseAstral.Invocador:
                {
                    // os ecos saem de você em espiral, cantam em sequência e no fim se jogam no alvo
                    int dano = (int)Player.GetTotalDamage(DamageClass.Summon).ApplyTo(DanoBase / 3);
                    for (int i = 0; i < 5; i++)
                        Projectile.NewProjectile(fonte, Player.Center, Vector2.Zero, ModContent.ProjectileType<CoroDosEcos>(), dano, 2f, Player.whoAmI, i / 5f * MathHelper.TwoPi, i);
                    SoundEngine.PlaySound(SoundID.Item44 with { Pitch = 0.3f }, Player.Center);
                    Hud.Banner("CORO DOS ECOS", CorDaClasse(ClasseAstral.Invocador));
                    break;
                }
            }
        }

        public override void PostUpdate()
        {
            if (TempoBrilho > 0) TempoBrilho--;
            // Pronta: duas faíscas da cor da classe giram em volta de você
            if (Pronta && Astral.Fundido)
            {
                float t = Main.GlobalTimeWrappedHourly * 5f;
                for (int k = 0; k < 2; k++)
                {
                    Vector2 p = Player.Center + (t + k * MathHelper.Pi).ToRotationVector2() * new Vector2(26f, 34f);
                    Dust d = Dust.NewDustPerfect(p, DustID.RainbowMk2, Vector2.Zero, 0, CorDaClasse(Astral.Classe), 0.7f);
                    d.noGravity = true; d.fadeIn = 0.4f;
                }
            }
        }
    }

    /// <summary>A barra da ultimate, embaixo da vida.</summary>
    public class UltimateHud : ModSystem
    {
        public override void PostDrawInterface(SpriteBatch sb)
        {
            Player p = Main.LocalPlayer;
            if (p.dead || Main.gameMenu || !p.GetModPlayer<AstralJogador>().Fundido) return;
            UltimateJogador u = p.GetModPlayer<UltimateJogador>();
            Texture2D px = TextureAssets.MagicPixel.Value;
            Rectangle um = new(0, 0, 1, 1);
            int w = 180, h = 10, x = Main.screenWidth - w - 40, y = 92;
            float k = u.Carga / UltimateJogador.CargaMax;
            float pulso = u.Pronta ? 0.6f + 0.4f * MathF.Sin(Main.GlobalTimeWrappedHourly * 6f) : 1f;
            Color cor = UltimateJogador.CorDaClasse(p.GetModPlayer<AstralJogador>().Classe);
            sb.Draw(px, new Rectangle(x - 2, y - 2, w + 4, h + 4), um, Color.Black * 0.6f);
            sb.Draw(px, new Rectangle(x, y, (int)(w * k), h), um, Color.Lerp(cor * 0.55f, cor, k) * pulso);
            sb.Draw(px, new Rectangle(x, y, (int)(w * k), 2), um, Color.White * 0.35f); // brilho em cima
            for (int i = 1; i < 4; i++) // marcas de 25%
                sb.Draw(px, new Rectangle(x + w * i / 4, y, 1, h), um, Color.Black * 0.5f);
            if (u.Pronta)
            {
                Texture2D bloom = Luminance.Assets.MiscTexturesRegistry.BloomCircleSmall.Value;
                sb.Draw(bloom, new Vector2(x + w / 2f, y + h / 2f), null, (cor with { A = 0 }) * 0.35f * pulso, 0f, bloom.Size() / 2f, new Vector2(w / (float)bloom.Width * 1.6f, 0.25f), 0, 0f);
            }
            string tecla = UltimateSistema.Tecla?.GetAssignedKeys() is { Count: > 0 } teclas ? teclas[0] : "?";
            string texto = u.Pronta ? $"ULTIMATE [{tecla}]" : $"Fusão {(int)(k * 100)}%";
            Utils.DrawBorderString(sb, texto, new Vector2(x + w / 2f, y - 16f), u.Pronta ? new Color(255, 230, 150) : Color.White, 0.75f, 0.5f, 0f);
        }
    }

    // ═══════════════ PROJÉTEIS DAS ULTIMATES ═══════════════
    // Todas seguem o mesmo ritmo: PREPARO (você vê o que vai acontecer) → GOLPE → FINAL (o maior impacto).
    public abstract class UltimateBase : ModProjectile
    {
        public override string Texture => "AscensaoAstral/Assets/Vazio";
        public override void SetStaticDefaults()
        {
            ProjectileID.Sets.TrailCacheLength[Type] = 14;
            ProjectileID.Sets.TrailingMode[Type] = 2;
            ProjectileID.Sets.DrawScreenCheckFluff[Type] = 3000;
        }

        protected Player Dono => Main.player[Projectile.owner];
        protected bool Meu => Projectile.owner == Main.myPlayer;

        /// <summary>Rastro pelas posições antigas (oldPos).</summary>
        protected void Rastro(Func<float, float> largura, Func<float, Color> cor, int pontos = 14)
        {
            Vector2[] pts = new Vector2[Projectile.oldPos.Length];
            for (int i = 0; i < pts.Length; i++) pts[i] = Projectile.oldPos[i] == Vector2.Zero ? Projectile.Center : Projectile.oldPos[i] + Projectile.Size / 2f;
            PrimitiveRenderer.RenderTrail(pts, new PrimitiveSettings(t => largura(t), t => cor(t)), pontos);
        }

        /// <summary>Anel fino (para ondas de choque e círculos mágicos).</summary>
        public static void Anel(Vector2 centro, float raio, float espessura, Color cor, float giro = 0f, int lados = 48)
        {
            Vector2[] pts = new Vector2[lados + 1];
            for (int i = 0; i <= lados; i++) pts[i] = centro + (giro + i / (float)lados * MathHelper.TwoPi).ToRotationVector2() * raio;
            PrimitiveRenderer.RenderTrail(pts, new PrimitiveSettings(_ => espessura, _ => cor, Smoothen: false), lados);
        }

        /// <summary>Linha fina entre dois pontos (traços de constelação, mira).</summary>
        public static void Linha(Vector2 a, Vector2 b, float espessura, Color cor)
        {
            Texture2D px = TextureAssets.MagicPixel.Value;
            Vector2 d = b - a;
            Main.spriteBatch.Draw(px, a - Main.screenPosition, new Rectangle(0, 0, 1, 1), cor with { A = 0 }, d.ToRotation(), new Vector2(0f, 0.5f), new Vector2(d.Length(), espessura), 0, 0f);
        }
    }

    /// <summary>
    /// GUERREIRO — Lâmina do Vazio. ai[0], ai[1] = de onde você saiu; velocity = direção.
    ///   0–14   investida (você fica invulnerável e corta quem estiver no caminho)
    ///   14     solta a meia-lua para a frente
    ///   14–40  o caminho fica marcado por uma rachadura vermelha que pulsa…
    ///   40     …e PARTE: dano em toda a linha da investida
    /// </summary>
    public class LaminaDoVazio : UltimateBase
    {
        private const int Fim = 70, FimInvestida = 14, Ruptura = 40;
        private int T => Fim - Projectile.timeLeft;
        private Vector2 Inicio => new(Projectile.ai[0], Projectile.ai[1]);
        private Vector2 Final => new(Projectile.localAI[0], Projectile.localAI[1]);

        public override void SetDefaults()
        {
            Projectile.width = 90; Projectile.height = 90;
            Projectile.friendly = true; Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1; Projectile.tileCollide = false; Projectile.timeLeft = Fim;
            Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = 12;
        }

        public override void AI()
        {
            Player p = Dono;
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            if (T < FimInvestida)
            {
                Projectile.Center = p.Center;
                if (Meu)
                {
                    p.velocity = dir * 30f;
                    p.immune = true; p.immuneNoBlink = true;
                    p.immuneTime = Math.Max(p.immuneTime, 20);
                    p.fallStart = (int)(p.position.Y / 16f);
                }
                Projectile.localAI[0] = p.Center.X; Projectile.localAI[1] = p.Center.Y;
                for (int i = 0; i < 3; i++)
                    Dust.NewDustPerfect(p.Center + Main.rand.NextVector2Circular(14f, 20f), DustID.RedTorch, -dir * Main.rand.NextFloat(2f, 6f), 0, default, 1.8f).noGravity = true;
            }
            else if (T == FimInvestida)
            {
                if (Meu)
                {
                    p.velocity *= 0.15f;
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), p.Center, dir * 21f, ModContent.ProjectileType<MeiaLuaDoVazio>(), Projectile.damage, Projectile.knockBack, Projectile.owner);
                }
                ScreenShakeSystem.StartShake(7f);
                SoundEngine.PlaySound(SoundID.Item71 with { Pitch = 0.2f }, p.Center);
            }
            else if (T == Ruptura)
            {
                ScreenShakeSystem.StartShake(14f);
                Hud.Clarao(0.25f);
                SoundEngine.PlaySound(SoundID.Item14 with { Pitch = -0.4f }, Vector2.Lerp(Inicio, Final, 0.5f));
                SoundEngine.PlaySound(SoundID.Item122 with { Pitch = -0.3f }, Vector2.Lerp(Inicio, Final, 0.5f));
                for (int i = 0; i < 50; i++)
                {
                    Vector2 onde = Vector2.Lerp(Inicio, Final, Main.rand.NextFloat());
                    Dust.NewDustPerfect(onde, DustID.RedTorch, dir.RotatedBy(MathHelper.PiOver2 * (Main.rand.NextBool() ? 1 : -1)) * Main.rand.NextFloat(3f, 11f), 0, default, 2.2f).noGravity = true;
                }
            }
            if (T >= FimInvestida) Projectile.Center = Vector2.Lerp(Inicio, Final, 0.5f);
            Lighting.AddLight(Projectile.Center, 1.2f, 0.3f, 0.4f);
        }

        // Machuca na investida (em volta de você) e na ruptura (a linha toda)
        public override bool? CanDamage() => T < FimInvestida || (T >= Ruptura && T < Ruptura + 6) ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            if (T < FimInvestida) return projHitbox.Intersects(targetHitbox);
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Inicio, Final, 100f, ref _);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Color vermelho = UltimateJogador.CorDaClasse(ClasseAstral.Melee);
            if (T < FimInvestida)
            {
                // rastro de imagens da investida
                Rastro(t => 46f * (1f - t), t => Color.Lerp(Color.White, vermelho, t) * (1f - t) * 0.8f);
                Desenho.Brilho(Dono.Center, vermelho, 0.9f);
                return false;
            }
            if (Inicio.Distance(Final) < 8f) return false;
            if (T < Ruptura)
            {
                // a rachadura: fina, pulsando cada vez mais rápido até partir
                float k = (T - FimInvestida) / (float)(Ruptura - FimInvestida);
                float pulso = 0.5f + 0.5f * MathF.Sin(T * (0.3f + k * 0.9f));
                Linha(Inicio, Final, 2f + 6f * k, vermelho * (0.35f + 0.5f * pulso));
                Linha(Inicio, Final, 1.5f, Color.White * (0.3f + 0.6f * k));
                Desenho.Brilho(Inicio, vermelho, 0.4f + 0.4f * k);
                Desenho.Brilho(Final, vermelho, 0.4f + 0.4f * k);
            }
            else
            {
                float f = 1f - (T - Ruptura) / (float)(Fim - Ruptura);
                Desenho.Faixa(Inicio, Final, 130f * f * f, vermelho, Color.White);
                for (int i = 0; i <= 6; i++)
                    Desenho.Brilho(Vector2.Lerp(Inicio, Final, i / 6f), vermelho, 0.9f * f);
            }
            return false;
        }
    }

    /// <summary>A meia-lua que sai no fim da investida: um arco de luz que cresce e só corta pela frente.</summary>
    public class MeiaLuaDoVazio : UltimateBase
    {
        private const float Abertura = 1.15f;
        private float Raio => 80f * Projectile.scale;
        private Vector2 CentroDoArco => Projectile.Center - Projectile.velocity.SafeNormalize(Vector2.UnitX) * Raio;

        public override void SetDefaults()
        {
            Projectile.width = 40; Projectile.height = 40;
            Projectile.friendly = true; Projectile.DamageType = DamageClass.Melee;
            Projectile.penetrate = -1; Projectile.tileCollide = false; Projectile.timeLeft = 48;
            Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = 10;
        }

        public override void AI()
        {
            Projectile.rotation = Projectile.velocity.ToRotation();
            Projectile.scale = 1f + (48 - Projectile.timeLeft) / 48f * 1.1f;
            Projectile.velocity *= 0.985f;
            Lighting.AddLight(Projectile.Center, 1.3f, 0.35f, 0.45f);
            if (Main.rand.NextBool())
            {
                float a = Projectile.rotation + Main.rand.NextFloat(-Abertura, Abertura);
                Dust.NewDustPerfect(CentroDoArco + a.ToRotationVector2() * Raio, DustID.RedTorch, Projectile.velocity * 0.3f, 0, default, 1.6f).noGravity = true;
            }
        }

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            Vector2 c = targetHitbox.Center.ToVector2() - CentroDoArco;
            float d = c.Length();
            float dif = Math.Abs(MathHelper.WrapAngle(c.ToRotation() - Projectile.rotation));
            return d > Raio - 70f * Projectile.scale && d < Raio + 50f * Projectile.scale && dif < Abertura + 0.15f;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            float a = Math.Min(1f, Projectile.timeLeft / 14f) * Math.Min(1f, (48 - Projectile.timeLeft) / 4f + 0.3f);
            Color vermelho = UltimateJogador.CorDaClasse(ClasseAstral.Melee);
            const int n = 24;
            Vector2[] pts = new Vector2[n];
            for (int i = 0; i < n; i++)
                pts[i] = CentroDoArco + (Projectile.rotation + MathHelper.Lerp(-Abertura, Abertura, i / (float)(n - 1))).ToRotationVector2() * Raio;
            // halo largo + lâmina (mais grossa no meio, fina nas pontas = forma de meia-lua)
            PrimitiveRenderer.RenderTrail(pts, new PrimitiveSettings(t => 70f * Projectile.scale * MathF.Pow(MathF.Sin(MathHelper.Pi * t), 0.8f) * a, t => vermelho * 0.35f * a), n);
            PrimitiveRenderer.RenderTrail(pts, new PrimitiveSettings(t => 30f * Projectile.scale * MathF.Pow(MathF.Sin(MathHelper.Pi * t), 0.6f) * a, t => Color.Lerp(vermelho, Color.White, MathF.Sin(MathHelper.Pi * t)) * a), n);
            Desenho.Brilho(Projectile.Center, vermelho, 0.8f * a * Projectile.scale);
            return false;
        }
    }

    /// <summary>
    /// MAGO — Chuva de Astra. Nasce no cursor:
    ///   0–30   uma constelação se desenha no chão (anel + 8 estrelas)
    ///   30–65  uma estrela cai em cada ponto (se tiver inimigo perto do ponto, ela vai nele)
    ///   78     a estrela grande cai no centro
    /// </summary>
    public class ChuvaDeAstra : UltimateBase
    {
        private const int Fim = 120, Pontas = 8;
        private const float RaioConstelacao = 210f;
        private int T => Fim - Projectile.timeLeft;

        public override void SetDefaults()
        {
            Projectile.width = 20; Projectile.height = 20;
            Projectile.friendly = false; Projectile.penetrate = -1; Projectile.tileCollide = false; Projectile.timeLeft = Fim;
        }

        private Vector2 Ponto(int i) => Projectile.Center + (i / (float)Pontas * MathHelper.TwoPi - MathHelper.PiOver2 + T * 0.004f).ToRotationVector2() * RaioConstelacao * (i % 2 == 0 ? 1f : 0.62f);

        private static Vector2 AlvoPerto(Vector2 onde, float raio)
        {
            NPC melhor = null; float d = raio;
            foreach (NPC n in Main.ActiveNPCs)
                if (n.CanBeChasedBy() && n.Distance(onde) < d) { d = n.Distance(onde); melhor = n; }
            return melhor?.Center ?? onde;
        }

        public override void AI()
        {
            Lighting.AddLight(Projectile.Center, 0.4f, 0.5f, 1.1f);
            if (!Meu) return;
            int tipo = ModContent.ProjectileType<EstrelaDeAstra>();
            for (int i = 0; i < Pontas; i++)
                if (T == 30 + i * 5)
                {
                    Vector2 alvo = AlvoPerto(Ponto(i), 220f);
                    Vector2 de = alvo + new Vector2(Main.rand.NextFloat(-160f, 160f), -760f);
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), de, (alvo - de).SafeNormalize(Vector2.UnitY) * 32f, tipo, Projectile.damage, Projectile.knockBack, Projectile.owner, alvo.X, alvo.Y, 0f);
                }
            if (T == 78)
            {
                Vector2 alvo = AlvoPerto(Projectile.Center, 300f);
                Vector2 de = alvo + new Vector2(0f, -900f);
                Projectile.NewProjectile(Projectile.GetSource_FromThis(), de, Vector2.UnitY * 36f, tipo, Projectile.damage * 3, Projectile.knockBack * 2f, Projectile.owner, alvo.X, alvo.Y, 1f);
            }
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Color azul = UltimateJogador.CorDaClasse(ClasseAstral.Mago);
            float entra = Math.Min(1f, T / 30f), sai = Math.Min(1f, Projectile.timeLeft / 30f);
            float a = entra * sai;
            float desenho = Utils.GetLerpValue(0f, 30f, T, true); // as linhas vão sendo traçadas
            Anel(Projectile.Center, RaioConstelacao * (0.6f + 0.4f * entra), 3f, azul * 0.6f * a, T * 0.01f);
            Anel(Projectile.Center, RaioConstelacao * 0.62f, 2f, Color.White * 0.25f * a, -T * 0.015f);
            for (int i = 0; i < Pontas; i++)
            {
                Vector2 p1 = Ponto(i), p2 = Ponto((i + 1) % Pontas);
                float k = MathHelper.Clamp(desenho * Pontas - i, 0f, 1f);
                if (k > 0f) Linha(p1, Vector2.Lerp(p1, p2, k), 2f, azul * 0.8f * a);
                bool caiu = T >= 30 + i * 5 + 22;
                float brilho = caiu ? 0.25f : 0.5f + 0.25f * MathF.Sin(T * 0.3f + i);
                Desenho.Brilho(p1, azul, brilho * a * k);
                Desenho.Brilho(p1, Color.White, 0.15f * a * k);
            }
            float centro = T < 78 ? Utils.GetLerpValue(40f, 78f, T, true) : 0.4f;
            Desenho.Brilho(Projectile.Center, azul, (0.4f + 1.2f * centro) * a);
            return false;
        }
    }

    /// <summary>Estrela que cai e explode onde marcou. ai[0], ai[1] = alvo; ai[2] = 1 para a estrela grande.</summary>
    public class EstrelaDeAstra : UltimateBase
    {
        private bool Grande => Projectile.ai[2] == 1f;
        private ref float Explosao => ref Projectile.localAI[0];
        private const int TempoExplosao = 18;

        public override void SetDefaults()
        {
            Projectile.width = 30; Projectile.height = 30;
            Projectile.friendly = true; Projectile.DamageType = DamageClass.Magic;
            Projectile.penetrate = -1; Projectile.tileCollide = false; Projectile.timeLeft = 180;
            Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = -1; // cada estrela acerta 1x
        }

        public override void AI()
        {
            Vector2 alvo = new(Projectile.ai[0], Projectile.ai[1]);
            Lighting.AddLight(Projectile.Center, 0.6f, 0.7f, 1.3f);
            if (Explosao > 0f)
            {
                Explosao++;
                return;
            }
            Projectile.rotation += 0.3f;
            if (Projectile.Center.Y >= alvo.Y || Projectile.Distance(alvo) < Projectile.velocity.Length())
            {
                Projectile.Center = alvo;
                Projectile.velocity = Vector2.Zero;
                Explosao = 1f;
                Projectile.timeLeft = TempoExplosao;
                int r = Grande ? 190 : 85;
                Projectile.Resize(r * 2, r * 2);
                SoundEngine.PlaySound((Grande ? SoundID.Item89 : SoundID.Item14) with { Volume = Grande ? 1f : 0.45f, Pitch = Grande ? -0.3f : 0.4f }, Projectile.Center);
                ScreenShakeSystem.StartShake(Grande ? 12f : 2.5f);
                if (Grande) Hud.Clarao(0.2f);
                for (int i = 0; i < (Grande ? 50 : 16); i++)
                    Dust.NewDustPerfect(Projectile.Center, DustID.BlueFairy, Main.rand.NextVector2Circular(1f, 1f) * (Grande ? 14f : 7f), 0, default, 1.5f).noGravity = true;
            }
        }

        public override bool? CanDamage() => Explosao > 0f && Explosao < 5f ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) =>
            Vector2.Distance(Projectile.Center, targetHitbox.ClosestPointInRect(Projectile.Center)) < Projectile.width / 2f;

        public override bool PreDraw(ref Color lightColor)
        {
            Color azul = UltimateJogador.CorDaClasse(ClasseAstral.Mago);
            float g = Grande ? 2.2f : 1f;
            if (Explosao > 0f)
            {
                float k = Explosao / TempoExplosao, f = 1f - k;
                float raio = Projectile.width / 2f * (0.3f + 0.9f * MathF.Sqrt(k));
                Anel(Projectile.Center, raio, 10f * g * f, azul * f);
                Anel(Projectile.Center, raio * 0.8f, 4f * g * f, Color.White * f);
                Desenho.Brilho(Projectile.Center, azul, 1.6f * g * f);
                Desenho.Brilho(Projectile.Center, Color.White, 0.6f * g * f * f);
                return false;
            }
            Rastro(t => 26f * g * (1f - t), t => Color.Lerp(Color.White, azul, t) * (1f - t));
            Desenho.Brilho(Projectile.Center, azul, 0.8f * g);
            Desenho.Brilho(Projectile.Center, Color.White, 0.3f * g);
            // marca no chão de onde vai cair
            Vector2 alvo = new(Projectile.ai[0], Projectile.ai[1]);
            Anel(alvo, (Grande ? 90f : 40f) * (1f + 0.1f * MathF.Sin(Projectile.rotation)), 2f, azul * 0.5f);
            return false;
        }
    }

    /// <summary>
    /// ATIRADOR — Disparo Estelar. velocity = direção.
    ///   0–30   carregando: a luz junta na sua mão, uma mira fina aparece, você fica pesado
    ///   30–110 o feixe (segue o cursor devagar e te empurra para trás)
    /// </summary>
    public class DisparoEstelar : UltimateBase
    {
        private const float Comprimento = 1900f;
        private const int Fim = 110, Carga = 30;
        private int T => Fim - Projectile.timeLeft;
        private bool Atirando => T >= Carga;

        public override void SetDefaults()
        {
            Projectile.width = 20; Projectile.height = 20;
            Projectile.friendly = true; Projectile.DamageType = DamageClass.Ranged;
            Projectile.penetrate = -1; Projectile.tileCollide = false; Projectile.timeLeft = Fim;
            Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = 6;
        }

        public override void AI()
        {
            Player p = Dono;
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Projectile.Center = p.Center + dir * 20f;
            p.ChangeDir(dir.X > 0f ? 1 : -1);
            p.itemRotation = MathF.Atan2(dir.Y * p.direction, dir.X * p.direction);
            p.itemTime = p.itemAnimation = 2;
            if (Meu)
            {
                Vector2 alvo = (Main.MouseWorld - p.Center).SafeNormalize(dir);
                Projectile.velocity = Vector2.Lerp(dir, alvo, Atirando ? 0.05f : 0.25f).SafeNormalize(Vector2.UnitX);
                if (!Atirando) p.velocity *= 0.9f;                                                  // pesado carregando
                else if (Vector2.Dot(p.velocity, -dir) < 6f) p.velocity -= dir * 0.35f;              // coice
                Projectile.netUpdate = true;
            }

            if (!Atirando)
            {
                // a luz é puxada para a ponta
                for (int i = 0; i < 2; i++)
                {
                    Vector2 de = Projectile.Center + Main.rand.NextVector2CircularEdge(90f, 90f) * (1f - T / (float)Carga);
                    Dust d = Dust.NewDustPerfect(de, DustID.GoldFlame, (Projectile.Center - de) * 0.12f, 0, default, 1.3f);
                    d.noGravity = true;
                }
                return;
            }
            if (T == Carga)
            {
                SoundEngine.PlaySound(SoundID.Zombie104 with { Volume = 0.55f, Pitch = 0.3f }, p.Center);
                ScreenShakeSystem.StartShake(12f);
                Hud.Clarao(0.15f);
            }
            ScreenShakeSystem.SetUniversalRumble(1.5f, MathHelper.TwoPi, null, 0.2f);
            for (int i = 0; i < 6; i++)
                Lighting.AddLight(Projectile.Center + dir * Comprimento * i / 6f, 1.2f, 1f, 0.5f);
            // faíscas onde o feixe atravessa alguém
            foreach (NPC n in Main.ActiveNPCs)
            {
                float _ = 0f;
                if (!n.friendly && Main.rand.NextBool(3) && Collision.CheckAABBvLineCollision(n.TopLeft, n.Size, Projectile.Center, Projectile.Center + dir * Comprimento, 50f, ref _))
                    Dust.NewDustPerfect(n.Center, DustID.GoldFlame, dir.RotatedByRandom(0.9f) * Main.rand.NextFloat(4f, 12f), 0, default, 1.6f).noGravity = true;
            }
        }

        public override bool? CanDamage() => Atirando ? null : false;

        public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
        {
            float _ = 0f;
            return Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), Projectile.Center, Projectile.Center + Projectile.velocity * Comprimento, 50f, ref _);
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Color ouro = UltimateJogador.CorDaClasse(ClasseAstral.Atirador);
            Vector2 dir = Projectile.velocity.SafeNormalize(Vector2.UnitX);
            Vector2 fim = Projectile.Center + dir * Comprimento;
            if (!Atirando)
            {
                float k = T / (float)Carga;
                if (T % 6 < 4) Linha(Projectile.Center, fim, 1f + k * 2f, ouro * (0.25f + 0.5f * k)); // mira piscando
                Desenho.Brilho(Projectile.Center, ouro, 0.3f + 0.9f * k);
                Desenho.Brilho(Projectile.Center, Color.White, 0.2f + 0.3f * k);
                return false;
            }
            int t = T - Carga;
            float a = Math.Min(1f, Projectile.timeLeft / 14f);
            float abre = Math.Min(1f, t / 5f);
            float largura = 70f * a * (abre < 1f ? 1.6f - 0.6f * abre : 1f) * (1f + 0.07f * MathF.Sin(t * 1.3f));
            Desenho.Faixa(Projectile.Center, fim, largura * 1.7f, ouro * 0.5f, ouro); // halo
            Desenho.Faixa(Projectile.Center, fim, largura, ouro, Color.White);
            Desenho.Brilho(Projectile.Center, ouro, 1.5f * a);
            Desenho.Brilho(Projectile.Center, Color.White, 0.6f * a);
            return false;
        }
    }

    /// <summary>
    /// INVOCADOR — Coro dos Ecos. ai[0] = ângulo inicial, ai[1] = qual eco (0–4).
    ///   0–24     saem de você em espiral
    ///   24–440   giram e atiram EM SEQUÊNCIA (um depois do outro, como um coro)
    ///   últimos 40  se jogam no inimigo mais perto (ou no cursor) e explodem
    /// </summary>
    public class CoroDosEcos : UltimateBase
    {
        private const int Fim = 60 * 8, Mergulho = 40;
        private int T => Fim - Projectile.timeLeft;
        private bool Mergulhando => Projectile.timeLeft <= Mergulho;

        public override void SetDefaults()
        {
            Projectile.width = 26; Projectile.height = 26;
            Projectile.friendly = true; Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = -1; Projectile.tileCollide = false; Projectile.timeLeft = Fim;
            Projectile.usesLocalNPCImmunity = true; Projectile.localNPCHitCooldown = -1;
        }

        private NPC MaisPerto(Vector2 de, float raio)
        {
            NPC alvo = null; float dist = raio;
            foreach (NPC n in Main.ActiveNPCs)
                if (n.CanBeChasedBy() && n.Distance(de) < dist) { dist = n.Distance(de); alvo = n; }
            return alvo;
        }

        public override void AI()
        {
            Player dono = Dono;
            Lighting.AddLight(Projectile.Center, 0.4f, 1f, 0.7f);
            if (Mergulhando)
            {
                if (Projectile.localAI[1] == 0f)
                {
                    Projectile.localAI[1] = 1f;
                    if (Meu)
                    {
                        Vector2 destino = MaisPerto(Projectile.Center, 1200f)?.Center ?? Main.MouseWorld;
                        Projectile.velocity = Projectile.DirectionTo(destino) * 4f;
                        Projectile.netUpdate = true;
                    }
                    SoundEngine.PlaySound(SoundID.Item9 with { Pitch = 0.4f + Projectile.ai[1] * 0.1f, Volume = 0.6f }, Projectile.Center);
                }
                if (Projectile.velocity.Length() < 26f) Projectile.velocity *= 1.18f; // acelera
                return;
            }

            float k = Utils.GetLerpValue(0f, 24f, T, true);
            float raio = 95f * (1f - MathF.Pow(1f - k, 3f));
            float ang = Projectile.ai[0] + T * 0.04f + (1f - k) * 2.5f;
            Projectile.Center = dono.Center + ang.ToRotationVector2() * raio + new Vector2(0f, MathF.Sin(T * 0.08f + Projectile.ai[1]) * 6f);

            // o coro: cada eco atira 4 ticks depois do anterior
            if (Meu && T > 24 && (T + (int)Projectile.ai[1] * 4) % 24 == 0)
            {
                NPC alvo = MaisPerto(Projectile.Center, 900f);
                if (alvo is not null)
                    Projectile.NewProjectile(Projectile.GetSource_FromThis(), Projectile.Center, Projectile.DirectionTo(alvo.Center) * 19f, ModContent.ProjectileType<TiroDoEco>(), Projectile.damage, 1f, Projectile.owner);
            }
        }

        public override bool? CanDamage() => Mergulhando ? null : false;

        public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone) => Projectile.timeLeft = Math.Min(Projectile.timeLeft, 2);

        public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) => modifiers.SourceDamage *= 4f; // o mergulho é o golpe forte

        public override void OnKill(int timeLeft)
        {
            SoundEngine.PlaySound(SoundID.Item110 with { Volume = 0.6f, Pitch = 0.2f }, Projectile.Center);
            for (int i = 0; i < 20; i++)
                Dust.NewDustPerfect(Projectile.Center, DustID.GreenFairy, Main.rand.NextVector2Circular(8f, 8f), 0, default, 1.4f).noGravity = true;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Color verde = UltimateJogador.CorDaClasse(ClasseAstral.Invocador);
            float entra = Utils.GetLerpValue(0f, 12f, T, true);
            Rastro(t => (Mergulhando ? 22f : 12f) * (1f - t), t => Color.Lerp(Color.White, verde, t) * (1f - t) * entra * 0.8f);
            // pisca de leve quando é a vez dele de cantar
            float vez = (T + (int)Projectile.ai[1] * 4) % 24 < 4 ? 1.35f : 1f;
            Desenho.Brilho(Projectile.Center, verde, 0.65f * vez * entra);
            Desenho.Brilho(Projectile.Center, Color.White, 0.22f * vez * entra);
            return false;
        }
    }

    public class TiroDoEco : UltimateBase
    {
        public override void SetDefaults()
        {
            Projectile.width = 14; Projectile.height = 14;
            Projectile.friendly = true; Projectile.DamageType = DamageClass.Summon;
            Projectile.penetrate = 1; Projectile.tileCollide = false; Projectile.timeLeft = 90;
        }

        public override void AI()
        {
            // curva leve até o inimigo mais perto
            NPC alvo = null; float dist = 500f;
            foreach (NPC n in Main.ActiveNPCs)
                if (n.CanBeChasedBy() && n.Distance(Projectile.Center) < dist) { dist = n.Distance(Projectile.Center); alvo = n; }
            if (alvo is not null)
                Projectile.velocity = Vector2.Lerp(Projectile.velocity, Projectile.DirectionTo(alvo.Center) * 19f, 0.08f);
        }

        public override void OnKill(int timeLeft)
        {
            for (int i = 0; i < 6; i++)
                Dust.NewDustPerfect(Projectile.Center, DustID.GreenFairy, Main.rand.NextVector2Circular(4f, 4f), 0, default, 1f).noGravity = true;
        }

        public override bool PreDraw(ref Color lightColor)
        {
            Rastro(t => 12f * (1f - t), t => new Color(120, 255, 190) * (1f - t), 12);
            Desenho.Brilho(Projectile.Center, Color.White, 0.2f);
            return false;
        }
    }
}
