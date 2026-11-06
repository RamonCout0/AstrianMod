using System;
using AscensaoAstral.Common;
using AscensaoAstral.Common.Raid;
using AscensaoAstral.Content.Bosses.Astharoth;
using Luminance.Assets;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.Graphics.Effects;
using Terraria.ModLoader;

namespace AscensaoAstral.Content.Cenario
{
    /// <summary>
    /// O PLANETA ASTRA no fundo do céu. Aparece quando a Astrânia é liberada e cresce a cada chefe derrotado.
    /// Fica bem no fundo (atrás das montanhas) e NÃO pinta o céu inteiro — por isso convive com os céus
    /// de outros mods. Com Calamity ou Wrath of the Gods instalados ele vai para a esquerda, para não ficar
    /// em cima do buraco negro do Avatar.
    /// </summary>
    public class CeuAstral : CustomSky
    {
        public const string Chave = "AscensaoAstral:Astra";
        private bool ativo;
        private float intensidade;

        private static bool LadoEsquerdo => ModLoader.HasMod("CalamityMod") || ModLoader.HasMod("NoxusBoss");

        public override void Update(GameTime gameTime) => intensidade = MathHelper.Clamp(intensidade + (ativo ? 0.01f : -0.01f), 0f, 1f);

        public override void Draw(SpriteBatch sb, float minDepth, float maxDepth)
        {
            // Camada mais ao fundo (atrás das montanhas e árvores do bioma)
            if (!(maxDepth >= float.MaxValue && minDepth < float.MaxValue))
                return;

            float progresso = AstralMundo.ProgressoAstra;
            Vector2 centro = new(Main.screenWidth * (LadoEsquerdo ? 0.2f : 0.5f), Main.screenHeight * 0.22f);
            float raio = MathHelper.Lerp(28f, 230f, MathF.Pow(progresso, 0.8f));
            float t = Main.GlobalTimeWrappedHourly;

            if (AstralMundo.ChefesDerrotados >= AstralMundo.TotalDeChefes)
                DesenharDespedacado(sb, centro, raio, t);
            else
                DesenharPlaneta(sb, centro, raio, t);
        }

        private void DesenharPlaneta(SpriteBatch sb, Vector2 c, float raio, float t)
        {
            Texture2D circulo = ModContent.Request<Texture2D>("AscensaoAstral/Assets/Circulo").Value;
            Texture2D bloom = MiscTexturesRegistry.BloomCircleSmall.Value;
            float a = intensidade;
            float escalaBloom = raio / (bloom.Width * 0.5f);

            // Atmosfera
            sb.Draw(bloom, c, null, new Color(90, 150, 255, 0) * 0.5f * a, 0f, bloom.Size() / 2f, escalaBloom * 2.2f, 0, 0f);
            // Corpo
            sb.Draw(circulo, c, null, new Color(70, 115, 210) * a, 0f, circulo.Size() / 2f, raio * 2f / circulo.Width, 0, 0f);
            // "Continentes" girando devagar (manchas dentro do disco)
            for (int i = 0; i < 9; i++)
            {
                float ang = i * 2.399f + t * 0.03f;
                float dist = raio * (0.2f + 0.55f * ((i * 37 % 10) / 10f));
                Vector2 p = c + new Vector2(MathF.Cos(ang) * dist, MathF.Sin(ang) * dist * 0.6f);
                sb.Draw(bloom, p, null, new Color(140, 200, 255, 0) * 0.35f * a, ang, bloom.Size() / 2f, new Vector2(escalaBloom * 0.5f, escalaBloom * 0.28f), 0, 0f);
            }
            // Brilho da borda iluminada
            sb.Draw(bloom, c + new Vector2(-raio * 0.35f, -raio * 0.35f), null, new Color(220, 240, 255, 0) * 0.35f * a, 0f, bloom.Size() / 2f, escalaBloom * 0.8f, 0, 0f);
        }

        private void DesenharDespedacado(SpriteBatch sb, Vector2 c, float raio, float t)
        {
            Texture2D circulo = ModContent.Request<Texture2D>("AscensaoAstral/Assets/Circulo").Value;
            Texture2D bloom = MiscTexturesRegistry.BloomCircleSmall.Value;
            for (int i = 0; i < 14; i++)
            {
                float ang = i * MathHelper.TwoPi / 14f + t * 0.02f;
                float dist = raio * (0.4f + 0.8f * ((i * 53 % 10) / 10f)) + MathF.Sin(t * 0.3f + i) * 10f;
                Vector2 p = c + ang.ToRotationVector2() * dist;
                float tam = raio * (0.12f + 0.12f * ((i * 17 % 10) / 10f));
                sb.Draw(bloom, p, null, new Color(255, 140, 80, 0) * 0.4f * intensidade, 0f, bloom.Size() / 2f, tam / 40f, 0, 0f);
                sb.Draw(circulo, p, null, new Color(60, 90, 170) * intensidade, ang, circulo.Size() / 2f, tam * 2f / circulo.Width, 0, 0f);
            }
        }

        public override void Activate(Vector2 position, params object[] args) => ativo = true;
        public override void Deactivate(params object[] args) => ativo = false;
        public override void Reset() => ativo = false;
        public override bool IsActive() => ativo || intensidade > 0f;
    }

    /// <summary>
    /// O ÉTER: vazio roxo, estrelas e um BURACO NEGRO enorme no fundo (só dentro da região do Éter).
    /// Mesmo desenho do Laboratório do Armotheus: metade de trás do disco → anel de fótons → centro preto → frente.
    /// </summary>
    public class CeuDoEter : CustomSky
    {
        public const string Chave = "AscensaoAstral:Eter";
        private bool ativo;
        private float intensidade;
        private readonly Vector3[] estrelas = new Vector3[160];

        public CeuDoEter()
        {
            var rng = new Random(77);
            for (int i = 0; i < estrelas.Length; i++)
                estrelas[i] = new Vector3((float)rng.NextDouble(), (float)rng.NextDouble(), (float)rng.NextDouble() * MathHelper.TwoPi);
        }

        public override void Update(GameTime gameTime) => intensidade = MathHelper.Clamp(intensidade + (ativo ? 0.015f : -0.015f), 0f, 1f);

        public override void Draw(SpriteBatch sb, float minDepth, float maxDepth)
        {
            if (!(maxDepth >= 0f && minDepth < 0f))
                return;

            Texture2D px = TextureAssets.MagicPixel.Value;
            Texture2D bloom = MiscTexturesRegistry.BloomCircleSmall.Value;
            int w = Main.screenWidth, h = Main.screenHeight;
            float t = Main.GlobalTimeWrappedHourly;

            // Degradê liso (antes eram 24 faixas — eram aquelas linhas horizontais no fundo)
            Degrade.Desenhar(sb, new Rectangle(0, 0, w, h), new Color(5, 3, 14), new Color(30, 10, 40), intensidade);
            foreach (Vector3 e in estrelas)
            {
                float pisca = 0.55f + 0.45f * MathF.Sin(t * 2f + e.Z);
                sb.Draw(bloom, new Vector2(e.X * w, e.Y * h), null, new Color(220, 190, 255, 0) * pisca * intensidade, 0f, bloom.Size() / 2f, 0.06f, 0, 0f);
            }

            // Fica "longe": acompanha a câmera com só 10% do movimento (paralaxe), então está sempre no céu
            Vector2 meioDaTela = Main.screenPosition + new Vector2(w, h) / 2f;
            Vector2 centro = new Vector2(w * 0.5f, h * 0.3f) + (EterEstrutura.CentroDaArena - meioDaTela) * 0.1f;
            const float raio = 330f;
            PoeiraEstelar(sb, centro, raio, t, intensidade, frente: false);
            BuracoNegro.Desenhar(sb, centro, raio, t, intensidade);
            PoeiraEstelar(sb, centro, raio, t, intensidade, frente: true);
        }

        /// <summary>
        /// Poeira estelar caindo em espiral no buraco negro. Cada partícula tem um ciclo (fase 0 → 1): nasce longe,
        /// gira cada vez mais rápido, encolhe e some no horizonte. Tudo calculado a partir do tempo, sem lista.
        /// </summary>
        private static void PoeiraEstelar(SpriteBatch sb, Vector2 centro, float raio, float t, float a, bool frente)
        {
            Texture2D bloom = MiscTexturesRegistry.BloomCircleSmall.Value;
            const int quantidade = 320;
            for (int i = 0; i < quantidade; i++)
            {
                float semente = i * 0.6180339f;
                float fase = (t * (0.05f + (i % 7) * 0.006f) + semente) % 1f;
                float distancia = MathHelper.Lerp(raio * (2.6f + (i % 5) * 0.5f), raio * 1.05f, MathF.Pow(fase, 1.6f));
                float angulo = semente * MathHelper.TwoPi + fase * fase * 7f;
                Vector2 local = new Vector2(MathF.Cos(angulo) * 1.6f, MathF.Sin(angulo) * 0.55f) * distancia;
                local = local.RotatedBy(-0.18f);
                bool estaNaFrente = MathF.Sin(angulo) > 0f;
                if (estaNaFrente != frente || (!frente && local.Length() < raio * 1.1f))
                    continue;

                float brilho = MathF.Sin(fase * MathHelper.Pi) * a;
                Color cor = (i % 3) switch { 0 => new Color(255, 210, 160, 0), 1 => new Color(200, 160, 255, 0), _ => new Color(255, 255, 255, 0) };
                Vector2 tangente = new Vector2(-MathF.Sin(angulo) * 1.6f, MathF.Cos(angulo) * 0.55f).RotatedBy(-0.18f);
                float esticar = 1f + fase * 1.2f; // perto do buraco ela estica só um pouco (riscos longos pareciam linhas soltas)
                sb.Draw(bloom, centro + local, null, cor * brilho * 0.8f, tangente.ToRotation(), bloom.Size() / 2f,
                    new Vector2(0.045f * esticar, 0.035f) * (1.4f - fase * 0.6f), 0, 0f);
            }
        }

        public override Color OnTileColor(Color inColor) => Color.Lerp(inColor, new Color(170, 130, 230), intensidade * 0.35f);
        public override float GetCloudAlpha() => 1f - intensidade;
        public override void Activate(Vector2 position, params object[] args) => ativo = true;
        public override void Deactivate(params object[] args) => ativo = false;
        public override void Reset() => ativo = false;
        public override bool IsActive() => ativo || intensidade > 0f;
    }

    /// <summary>
    /// O que o Astharoth conta para o céu a cada tick (só visual, em cada cliente).
    /// </summary>
    public static class ArcanjoNoCeu
    {
        /// <summary>0 = ele não está no céu; 1 = ele É a luz do céu.</summary>
        public static float Presenca;
        /// <summary>0 → 1: uma volta completa no céu (no fim da volta, a rajada).</summary>
        public static float Volta;
        /// <summary>0 → 1: a luz carregando no fim da volta.</summary>
        public static float Carga;
        /// <summary>1 no momento da rajada, depois apaga sozinho.</summary>
        public static float Explosao;

        /// <summary>
        /// Uma volta = um dia inteiro, como o relógio do Terraria acelerado:
        ///   0.00 nasce no canto esquerdo · 0.25 meio-dia lá no alto · 0.50 se põe no canto direito ·
        ///   0.50–1.00 noite: ele passa POR BAIXO das nuvens de volta para a esquerda (elas brilham por baixo) · 1.00 rajada
        /// </summary>
        public static Vector2 PosicaoNaTela(float volta)
        {
            int w = Main.screenWidth, h = Main.screenHeight;
            float horizonte = BaseDoCeu() - h * 0.4f;
            if (volta < 0.5f)
            {
                float k = volta / 0.5f;
                return new Vector2(MathHelper.Lerp(-0.06f, 1.06f, k) * w, horizonte - MathF.Sin(k * MathHelper.Pi) * h * 0.5f);
            }
            float n = (volta - 0.5f) / 0.5f;
            return new Vector2(MathHelper.Lerp(1.06f, -0.06f, n) * w, horizonte + h * 0.12f + MathF.Sin(n * MathHelper.Pi) * h * 0.1f);
        }

        /// <summary>
        /// Onde o céu "termina" na tela: a linha do chão da arena. O mar de nuvens fica EM CIMA do chão,
        /// senão o chão da arena cobre metade das nuvens (e o Arcanjo some atrás dele).
        /// </summary>
        public static float BaseDoCeu()
        {
            int h = Main.screenHeight;
            if (Common.Raid.RaidBoss.Ativo is not Common.Raid.RaidBoss chefe)
                return h;
            // PARALLAX: o céu fica bem longe, então anda só 25% do que a câmera anda (contínuo, sem trava).
            // Com você no chão, a base das nuvens fica logo abaixo da linha do chão.
            float chaoNaTela = chefe.RetanguloArena.Bottom - Main.screenPosition.Y;   // onde o chão está na tela
            float referencia = h * 0.78f;                                                // onde o chão fica com você em pé
            float base_ = referencia + 30f + (chaoNaTela - referencia) * 0.25f;
            // limite macio: perto das pontas a base desacelera em vez de parar de uma vez
            float min = h * 0.5f, max = h * 1.35f;
            if (base_ < min + 80f) base_ = min + 80f * MathF.Exp((base_ - min - 80f) / 80f);
            if (base_ > max - 120f) base_ = max - 120f * MathF.Exp(-(base_ - max + 120f) / 120f);
            return base_;
        }

        /// <summary>1 ao meio-dia, 0 à noite.</summary>
        public static float Dia(float volta) => volta < 0.5f ? MathF.Sin(volta / 0.5f * MathHelper.Pi) : 0f;
        /// <summary>1 no nascer/pôr do sol (céu alaranjado).</summary>
        public static float Crepusculo(float volta)
        {
            float d = Math.Min(Math.Abs(volta - 0f), Math.Min(Math.Abs(volta - 0.5f), Math.Abs(volta - 1f)));
            return Utils.GetLerpValue(0.12f, 0f, d, true);
        }
    }

    /// <summary>
    /// O CÉU DO ARCANJO: em cima, céu escuro e estrelado; embaixo, um mar de nuvens (shader) iluminado por ELE.
    /// O Arcanjo é a luz: ele gira no fundo e passa por trás das nuvens — cada volta é como um dia passando.
    /// </summary>
    public class CeuDasNuvens : CustomSky
    {
        public const string Chave = "AscensaoAstral:Nuvens";
        private bool ativo;
        private float intensidade;
        private readonly Vector3[] estrelas = new Vector3[220];

        public CeuDasNuvens()
        {
            Random rng = new(777);
            for (int i = 0; i < estrelas.Length; i++)
                estrelas[i] = new Vector3((float)rng.NextDouble(), (float)rng.NextDouble() * 0.62f, (float)rng.NextDouble() * MathHelper.TwoPi);
        }

        // Relâmpagos dentro do mar de nuvens
        private Vector2 relampagoUv;
        private float relampago;
        private int sementeRaio;

        public override void Update(GameTime gameTime)
        {
            intensidade = MathHelper.Clamp(intensidade + (ativo ? 0.02f : -0.02f), 0f, 1f);
            ArcanjoNoCeu.Explosao = Math.Max(0f, ArcanjoNoCeu.Explosao - 0.02f);
            relampago = Math.Max(0f, relampago - 0.06f);
            // mais raios à noite e quando ele está carregando
            float chance = 0.004f + (1f - ArcanjoNoCeu.Dia(ArcanjoNoCeu.Volta)) * 0.006f + ArcanjoNoCeu.Carga * 0.03f;
            if (intensidade > 0.5f && Main.rand.NextFloat() < chance)
            {
                relampagoUv = new Vector2(Main.rand.NextFloat(0.05f, 0.95f), Main.rand.NextFloat(0.15f, 0.7f));
                relampago = Main.rand.NextFloat(0.8f, 1.4f);
                sementeRaio = Main.rand.Next(100000);
                if (Main.rand.NextBool(3))
                    Terraria.Audio.SoundEngine.PlaySound(Terraria.ID.SoundID.Thunder with { Volume = 0.35f });
            }
        }

        public override void Draw(SpriteBatch sb, float minDepth, float maxDepth)
        {
            if (!(maxDepth >= 0f && minDepth < 0f))
                return;

            Texture2D bloom = MiscTexturesRegistry.BloomCircleSmall.Value;
            int w = Main.screenWidth, h = Main.screenHeight;
            float t = Main.GlobalTimeWrappedHourly;
            float a = intensidade;
            float presenca = ArcanjoNoCeu.Presenca;
            Vector2 luz = presenca > 0f ? ArcanjoNoCeu.PosicaoNaTela(ArcanjoNoCeu.Volta) : new Vector2(w * 0.5f, h * 0.1f);
            float volta = ArcanjoNoCeu.Volta;
            float dia = ArcanjoNoCeu.Dia(volta) * presenca;
            float crep = ArcanjoNoCeu.Crepusculo(volta) * presenca;
            float alturaDaLuz = dia;
            float forcaLuz = MathHelper.Lerp(0.35f, 1f, presenca) * (0.45f + 0.55f * dia) + ArcanjoNoCeu.Carga * 0.9f + ArcanjoNoCeu.Explosao;

            // 1) Céu: noite escura → amanhecer alaranjado → meio-dia azul-violeta claro (o dia passando)
            Color topo = Color.Lerp(Color.Lerp(new Color(4, 5, 18), new Color(45, 60, 130), dia), new Color(60, 40, 90), crep * 0.5f);
            Color horizonte = Color.Lerp(Color.Lerp(new Color(40, 30, 75), new Color(150, 150, 210), dia), new Color(230, 120, 90), crep * 0.8f);
            int baseCeu = (int)ArcanjoNoCeu.BaseDoCeu();
            int linhaHorizonte = baseCeu - (int)(h * 0.38f);
            Degrade.Desenhar(sb, new Rectangle(0, 0, w, linhaHorizonte), topo, horizonte, a);
            Degrade.Desenhar(sb, new Rectangle(0, linhaHorizonte - 1, w, h - linhaHorizonte + 1), horizonte, new Color(20, 15, 40), a);

            // 2) Estrelas (somem perto da luz dele)
            foreach (Vector3 e in estrelas)
            {
                Vector2 p = new(e.X * w, e.Y / 0.62f * linhaHorizonte);
                float perto = Utils.GetLerpValue(420f, 120f, Vector2.Distance(p, luz), true) * presenca;
                float pisca = (0.5f + 0.5f * MathF.Sin(t * 2f + e.Z)) * (1f - perto) * (1f - dia * 0.9f); // de dia as estrelas somem
                sb.Draw(bloom, p, null, new Color(210, 200, 255, 0) * pisca * a * 0.8f, 0f, bloom.Size() / 2f, 0.035f + (e.Z % 1f) * 0.03f, 0, 0f);
            }

            // 3) ELE: a luz do céu (antes das nuvens, para que elas o cubram quando passa por baixo)
            if (presenca > 0f)
                DesenharArcanjo(sb, luz, presenca * a, t);

            // 4) Mar de nuvens com shader, iluminado pela luz dele
            int yNuvens = baseCeu - (int)(h * 0.45f);
            Rectangle areaNuvens = new(0, yNuvens, w, (int)(h * 0.45f));
            if (areaNuvens.Bottom < h) // embaixo da linha do chão: o "fundo" das nuvens, sem buraco
                Degrade.Desenhar(sb, new Rectangle(0, areaNuvens.Bottom - 2, w, h - areaNuvens.Bottom + 2), new Color(26, 23, 51), new Color(10, 8, 20), a);
            Vector2 luzUv = new(luz.X / w, (luz.Y - yNuvens) / (float)areaNuvens.Height);
            if (!DesenharNuvensComShader(sb, areaNuvens, luzUv, forcaLuz * a, dia, relampagoUv, relampago * a))
                DesenharNuvensSimples(sb, areaNuvens, a);

            // 4b) O raio em si (zigue-zague de luz), por cima das nuvens
            if (relampago > 0.3f)
                DesenharRaio(sb, new Vector2(relampagoUv.X * w, yNuvens + relampagoUv.Y * areaNuvens.Height), relampago * a, sementeRaio);

            // 5) Rajada: o céu inteiro acende por um instante
            if (ArcanjoNoCeu.Explosao > 0f)
                sb.Draw(bloom, luz, null, new Color(255, 230, 170, 0) * ArcanjoNoCeu.Explosao * a, 0f, bloom.Size() / 2f, 14f * ArcanjoNoCeu.Explosao, 0, 0f);
        }

        private static void DesenharArcanjo(SpriteBatch sb, Vector2 pos, float a, float t)
        {
            Texture2D bloom = MiscTexturesRegistry.BloomCircleSmall.Value;
            float carga = ArcanjoNoCeu.Carga;
            Color dourado = new(255, 215, 140, 0);

            // ESTRELA CADENTE: a cauda de luz pelo caminho que ele acabou de fazer no céu
            {
                const int n = 26;
                float volta = ArcanjoNoCeu.Volta;
                Vector2 antes = pos;
                for (int i = 1; i <= n; i++)
                {
                    float k = i / (float)n;
                    Vector2 p = ArcanjoNoCeu.PosicaoNaTela(Math.Max(0f, volta - k * 0.07f));
                    if (volta - k * 0.07f < 0f) break;
                    Vector2 meio = (antes + p) / 2f, d = p - antes;
                    float largura = (1f - k);
                    Color cauda = Color.Lerp(new Color(255, 245, 220, 0), new Color(255, 130, 60, 0), k);
                    // núcleo fino + halo largo, os dois afinando para o fim
                    sb.Draw(bloom, meio, null, cauda * a * 0.9f * largura, d.ToRotation(), bloom.Size() / 2f, new Vector2(d.Length() / bloom.Width * 1.6f + 0.02f, 0.06f + 0.10f * largura), 0, 0f);
                    sb.Draw(bloom, meio, null, cauda * a * 0.25f * largura, d.ToRotation(), bloom.Size() / 2f, new Vector2(d.Length() / bloom.Width * 1.8f + 0.02f, 0.3f + 0.5f * largura), 0, 0f);
                    antes = p;
                }
                // centelhas soltas caindo da cauda
                for (int i = 0; i < 10; i++)
                {
                    float k = ((t * 0.7f + i * 0.137f) % 1f);
                    if (volta - k * 0.07f < 0f) continue;
                    Vector2 p = ArcanjoNoCeu.PosicaoNaTela(volta - k * 0.07f) + new Vector2(MathF.Sin(i * 12.3f) * 14f, k * 40f);
                    sb.Draw(bloom, p, null, new Color(255, 220, 150, 0) * a * (1f - k) * 0.8f, 0f, bloom.Size() / 2f, 0.04f, 0, 0f);
                }
            }

            // Raios girando em volta dele
            for (int i = 0; i < 10; i++)
            {
                float ang = i / 10f * MathHelper.TwoPi + t * 0.25f;
                float tam = 1.6f + 0.6f * MathF.Sin(t * 1.3f + i * 2.1f) + carga * 2f;
                sb.Draw(bloom, pos + ang.ToRotationVector2() * 90f * tam, null, dourado * 0.12f * a, ang, bloom.Size() / 2f, new Vector2(tam * 1.6f, 0.07f), 0, 0f);
            }
            // Halo e núcleo
            sb.Draw(bloom, pos, null, new Color(255, 170, 90, 0) * 0.35f * a, 0f, bloom.Size() / 2f, 3.2f + carga * 2.5f, 0, 0f);
            sb.Draw(bloom, pos, null, dourado * 0.7f * a, 0f, bloom.Size() / 2f, 1.3f + carga, 0, 0f);
            // Anel girando (as asas de luz)
            for (int i = 0; i < 28; i++)
            {
                float ang = i / 28f * MathHelper.TwoPi - t * 0.6f;
                Vector2 off = new(MathF.Cos(ang) * 70f, MathF.Sin(ang) * 26f);
                sb.Draw(bloom, pos + off, null, new Color(255, 240, 200, 0) * 0.5f * a, 0f, bloom.Size() / 2f, 0.07f, 0, 0f);
            }
            // A silhueta dele, pequenininha, no meio da luz
            Texture2D tex = ModContent.Request<Texture2D>("AscensaoAstral/Content/Bosses/Astharoth/Astharoth").Value;
            sb.Draw(tex, pos, null, new Color(255, 250, 235) * 0.9f * a, 0f, tex.Size() / 2f, 0.45f, 0, 0f);
            sb.Draw(bloom, pos, null, new Color(255, 255, 255, 0) * a, 0f, bloom.Size() / 2f, 0.45f + carga * 0.6f, 0, 0f);
        }

        private static void DesenharRaio(SpriteBatch sb, Vector2 baixo, float a, int semente)
        {
            Texture2D bloom = MiscTexturesRegistry.BloomCircleSmall.Value;
            Random r = new(semente);
            Vector2 p = baixo;
            float altura = 120f + (float)r.NextDouble() * 160f;
            int passos = 9;
            for (int i = 0; i < passos; i++)
            {
                Vector2 q = p + new Vector2(((float)r.NextDouble() - 0.5f) * 46f, -altura / passos);
                Vector2 meio = (p + q) / 2f, d = q - p;
                sb.Draw(bloom, meio, null, new Color(190, 205, 255, 0) * a * 0.9f, d.ToRotation(), bloom.Size() / 2f, new Vector2(d.Length() / bloom.Width * 1.2f, 0.035f), 0, 0f);
                sb.Draw(bloom, meio, null, new Color(120, 140, 255, 0) * a * 0.35f, d.ToRotation(), bloom.Size() / 2f, new Vector2(d.Length() / bloom.Width * 1.4f, 0.18f), 0, 0f);
                p = q;
            }
            sb.Draw(bloom, baixo, null, new Color(170, 190, 255, 0) * a * 0.6f, 0f, bloom.Size() / 2f, 1.4f, 0, 0f);
        }

        private static bool DesenharNuvensComShader(SpriteBatch sb, Rectangle area, Vector2 luzUv, float forca, float meioDia, Vector2 relampagoUv, float relampago)
        {
            if (!Luminance.Core.Graphics.ShaderManager.TryGetShader("AscensaoAstral.NuvensShader", out Luminance.Core.Graphics.ManagedShader shader))
                return false;

            Matrix matriz = Luminance.Common.Utilities.Utilities.GetCustomSkyBackgroundMatrix();
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone, null, matriz);

            shader.TrySetParameter("luzPos", luzUv);
            shader.TrySetParameter("luzForca", forca);
            shader.TrySetParameter("meioDia", meioDia * meioDia);
            shader.TrySetParameter("relampagoPos", relampagoUv);
            shader.TrySetParameter("relampagoForca", relampago);
            shader.TrySetParameter("corLuz", new Vector3(1f, 0.78f, 0.45f));
            shader.TrySetParameter("corEscura", new Vector3(0.10f, 0.09f, 0.20f));
            shader.TrySetParameter("corClara", new Vector3(0.40f, 0.36f, 0.56f));
            shader.TrySetParameter("corHorizonte", new Vector3(0.20f, 0.15f, 0.32f));
            shader.TrySetParameter("proporcao", area.Width / (float)area.Height);
            shader.TrySetParameter("opacidade", 1f);
            shader.SetTexture(MiscTexturesRegistry.TurbulentNoise.Value, 1, SamplerState.LinearWrap);
            shader.SetTexture(MiscTexturesRegistry.WavyBlotchNoise.Value, 2, SamplerState.LinearWrap);
            shader.Apply();

            Texture2D branco = ModContent.Request<Texture2D>("AscensaoAstral/Assets/Branco").Value;
            sb.Draw(branco, area, Color.White);

            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, matriz);
            return true;
        }

        private static void DesenharNuvensSimples(SpriteBatch sb, Rectangle area, float a)
        {
            // Plano B (enquanto o shader não compilou): névoa arroxeada
            Texture2D nevoa = ModContent.Request<Texture2D>("AscensaoAstral/Assets/Nevoa").Value;
            float t = Main.GlobalTimeWrappedHourly;
            for (int i = 0; i < 12; i++)
            {
                float x = ((i * 0.17f + t * 0.006f * (1 + i % 3)) % 1.4f - 0.2f) * area.Width;
                float y = area.Y + area.Height * (0.15f + (i % 5) * 0.18f);
                sb.Draw(nevoa, new Vector2(x, y), null, new Color(90, 80, 130) * 0.5f * a, 0f, nevoa.Size() / 2f, new Vector2(5f, 1.2f), 0, 0f);
            }
        }

        public override Color OnTileColor(Color inColor) => Color.Lerp(inColor, new Color(200, 185, 230), intensidade * 0.25f);
        public override float GetCloudAlpha() => 0f;
        public override void Activate(Vector2 position, params object[] args) => ativo = true;
        public override void Deactivate(params object[] args) => ativo = false;
        public override void Reset() => ativo = false;
        public override bool IsActive() => ativo || intensidade > 0f;
    }

    /// <summary>
    /// O CÉU DO ARMOTHEUS (raid da Silvanna): noite de inverno estrelada com AURORA (shader) lá no fundo,
    /// e os cavaleiros do céu no plano de fundo, na linha do chão da arena. Na Nevasca o céu fecha e escurece.
    /// </summary>
    public class CeuDoArmotheus : CustomSky
    {
        public const string Chave = "AscensaoAstral:Armotheus";
        private bool ativo;
        private float intensidade, fechado;
        private readonly Vector3[] estrelas = new Vector3[240];

        public CeuDoArmotheus()
        {
            Random rng = new(4242);
            for (int i = 0; i < estrelas.Length; i++)
                estrelas[i] = new Vector3((float)rng.NextDouble(), (float)rng.NextDouble() * 0.7f, (float)rng.NextDouble() * MathHelper.TwoPi);
        }

        public override void Update(GameTime gameTime)
        {
            intensidade = MathHelper.Clamp(intensidade + (ativo ? 0.02f : -0.02f), 0f, 1f);
            bool nevasca = Common.Raid.RaidBoss.Ativo is Bosses.Silvanna.RaidSilvanna s && s.NevascaAtiva;
            fechado = MathHelper.Clamp(fechado + (nevasca ? 0.01f : -0.01f), 0f, 1f);
        }

        public override void Draw(SpriteBatch sb, float minDepth, float maxDepth)
        {
            int w = Main.screenWidth, h = Main.screenHeight;
            float a = intensidade;
            float t = Main.GlobalTimeWrappedHourly;

            // Camada mais distante: o céu em si (as montanhas de neve do Terraria continuam na frente)
            if (maxDepth >= float.MaxValue && minDepth < float.MaxValue)
            {
                Color topo = Color.Lerp(new Color(3, 6, 20), new Color(30, 38, 55), fechado);
                Color baixo = Color.Lerp(new Color(28, 40, 80), new Color(120, 135, 160), fechado);
                Degrade.Desenhar(sb, new Rectangle(0, 0, w, h), topo, baixo, a);

                Texture2D bloom = MiscTexturesRegistry.BloomCircleSmall.Value;
                foreach (Vector3 e in estrelas)
                {
                    float pisca = (0.5f + 0.5f * MathF.Sin(t * 1.7f + e.Z * 3f)) * (1f - fechado);
                    sb.Draw(bloom, new Vector2(e.X * w, e.Y * h), null, new Color(215, 230, 255, 0) * pisca * a, 0f, bloom.Size() / 2f, 0.03f + (e.Z % 1f) * 0.035f, 0, 0f);
                }
                DesenharAurora(sb, new Rectangle(0, 0, w, (int)(h * 0.6f)), a * (1f - fechado * 0.85f));
            }

            // Camada da frente do fundo: os cavaleiros do céu (atrás dos blocos e dos jogadores)
            if (maxDepth >= 0f && minDepth < 0f && Common.Raid.RaidBoss.Ativo is Bosses.Silvanna.RaidSilvanna chefe)
                chefe.DesenharCavaleirosNoFundo(sb, Main.screenPosition);
        }

        private static void DesenharAurora(SpriteBatch sb, Rectangle area, float a)
        {
            if (a <= 0.01f || !Luminance.Core.Graphics.ShaderManager.TryGetShader("AscensaoAstral.AuroraShader", out Luminance.Core.Graphics.ManagedShader shader))
                return;
            Matrix matriz = Luminance.Common.Utilities.Utilities.GetCustomSkyBackgroundMatrix();
            sb.End();
            sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearWrap, DepthStencilState.None, RasterizerState.CullNone, null, matriz);
            shader.TrySetParameter("intensidade", 1.1f * a);
            shader.TrySetParameter("corBaixo", new Vector3(0.3f, 1f, 0.75f));
            shader.TrySetParameter("corAlto", new Vector3(0.6f, 0.35f, 1f));
            shader.TrySetParameter("proporcao", area.Width / (float)area.Height);
            shader.SetTexture(MiscTexturesRegistry.TurbulentNoise.Value, 1, SamplerState.LinearWrap);
            shader.Apply();
            sb.Draw(ModContent.Request<Texture2D>("AscensaoAstral/Assets/Branco").Value, area, Color.White);
            sb.End();
            sb.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, Main.DefaultSamplerState, DepthStencilState.None, RasterizerState.CullCounterClockwise, null, matriz);
        }

        public override Color OnTileColor(Color inColor) => Color.Lerp(inColor, new Color(170, 200, 255), intensidade * 0.25f);
        public override float GetCloudAlpha() => 1f - intensidade;
        public override void Activate(Vector2 position, params object[] args) => ativo = true;
        public override void Deactivate(params object[] args) => ativo = false;
        public override void Reset() => ativo = false;
        public override bool IsActive() => ativo || intensidade > 0f;
    }

    /// <summary>Degradê vertical liso: pinta a cor de baixo e por cima a de cima com um alfa que some (Assets/Degrade.png).</summary>
    public static class Degrade
    {
        public static void Desenhar(SpriteBatch sb, Rectangle area, Color topo, Color baixo, float opacidade)
        {
            Texture2D px = TextureAssets.MagicPixel.Value;
            Texture2D deg = ModContent.Request<Texture2D>("AscensaoAstral/Assets/Degrade").Value;
            sb.Draw(px, area, new Rectangle(0, 0, 1, 1), baixo * opacidade);
            sb.Draw(deg, area, null, topo * opacidade);
        }
    }

    /// <summary>O buraco negro feito só com sprites (4 camadas). Reutilizável em qualquer céu.</summary>
    public static class BuracoNegro
    {
        public static void Desenhar(SpriteBatch sb, Vector2 centro, float raio, float t, float a)
        {
            Texture2D bloom = MiscTexturesRegistry.BloomCircleSmall.Value;
            Texture2D circulo = ModContent.Request<Texture2D>("AscensaoAstral/Assets/Circulo").Value;
            float eb = raio / (bloom.Width * 0.5f);

            sb.Draw(bloom, centro, null, new Color(120, 40, 200, 0) * 0.35f * a, 0f, bloom.Size() / 2f, eb * 5f, 0, 0f);
            sb.Draw(bloom, centro, null, new Color(255, 90, 60, 0) * 0.25f * a, 0f, bloom.Size() / 2f, eb * 3f, 0, 0f);
            Disco(sb, centro, raio, t, a, false);
            for (int i = 0; i < 56; i++)
            {
                float ang = i / 56f * MathHelper.TwoPi + t * 0.3f;
                float brilho = 0.7f + 0.3f * MathF.Sin(ang * 3f + t * 2f);
                sb.Draw(bloom, centro + ang.ToRotationVector2() * raio * 1.08f, null, new Color(255, 200, 150, 0) * 0.5f * brilho * a, 0f, bloom.Size() / 2f, eb * 0.18f, 0, 0f);
            }
            sb.Draw(circulo, centro, null, Color.Black * a, 0f, circulo.Size() / 2f, raio * 2f / circulo.Width, 0, 0f);
            Disco(sb, centro, raio, t, a, true);
        }

        private static void Disco(SpriteBatch sb, Vector2 centro, float raio, float t, float a, bool frente)
        {
            Texture2D bloom = MiscTexturesRegistry.BloomCircleSmall.Value;
            float eb = raio / (bloom.Width * 0.5f);
            for (int i = 0; i < 80; i++)
            {
                float anel = 1.45f + (i % 5) * 0.3f;
                float ang = i * 2.399f + t * (1.1f / anel);
                float seno = MathF.Sin(ang);
                if ((seno > 0f) != frente)
                    continue;
                Vector2 local = new Vector2(MathF.Cos(ang) * raio * anel * 1.7f, seno * raio * anel * 0.3f).RotatedBy(-0.18f);
                Color cor = Color.Lerp(new Color(200, 40, 60), new Color(255, 220, 170), 1f - (anel - 1.45f) / 1.2f);
                float tam = eb * 0.35f * (0.8f + 0.4f * MathF.Sin(i * 1.7f + t));
                sb.Draw(bloom, centro + local, null, cor with { A = 0 } * 0.5f * a, -0.18f, bloom.Size() / 2f, new Vector2(tam * 2.4f, tam), 0, 0f);
            }
        }
    }

    /// <summary>
    /// Liga e desliga os céus sozinho:
    ///   Astra → sempre que a Astrânia foi liberada
    ///   Éter → quando o jogador local está na região do Éter
    ///   Nuvens → quando o Astharoth virou Arcanjo
    /// Vários céus podem estar ativos ao mesmo tempo (inclusive os de outros mods).
    /// </summary>
    /// <summary>
    /// O CÉU DO DUELO (Astharoth antes do Éter): crepúsculo violeta com uma nebulosa em camadas (parallax),
    /// poeira de éter subindo e, no alto, um ECLIPSE — um sol escondido atrás de um disco preto.
    /// A coroa dourada do eclipse vai acendendo conforme a vida dele cai: é o Arcanjo esperando para nascer.
    /// </summary>
    public class CeuDoDuelo : CustomSky
    {
        public const string Chave = "AscensaoAstral:Duelo";
        private bool ativo;
        private float intensidade;
        private readonly Vector3[] estrelas = new Vector3[260];
        private readonly Vector4[] nebulosa = new Vector4[34];

        public CeuDoDuelo()
        {
            Random rng = new(4242);
            for (int i = 0; i < estrelas.Length; i++)
                estrelas[i] = new Vector3((float)rng.NextDouble(), (float)rng.NextDouble() * 0.8f, (float)rng.NextDouble() * MathHelper.TwoPi);
            for (int i = 0; i < nebulosa.Length; i++) // x, y, tamanho, camada (0 = longe, 2 = perto)
                nebulosa[i] = new Vector4((float)rng.NextDouble() * 1.4f - 0.2f, 0.15f + (float)rng.NextDouble() * 0.5f, 0.6f + (float)rng.NextDouble() * 1.4f, i % 3);
        }

        public override void Update(GameTime gameTime) => intensidade = MathHelper.Clamp(intensidade + (ativo ? 0.02f : -0.02f), 0f, 1f);

        public override void Draw(SpriteBatch sb, float minDepth, float maxDepth)
        {
            if (!(maxDepth >= 0f && minDepth < 0f))
                return;
            Texture2D bloom = MiscTexturesRegistry.BloomCircleSmall.Value;
            Texture2D circulo = ModContent.Request<Texture2D>("AscensaoAstral/Assets/Circulo").Value;
            int w = Main.screenWidth, h = Main.screenHeight;
            float t = Main.GlobalTimeWrappedHourly;
            float a = intensidade;
            float raiva = RaidBoss.Ativo is RaidBoss b && b.NPC.lifeMax > 0 ? 1f - b.NPC.life / (float)b.NPC.lifeMax : 0f;
            int baseCeu = (int)ArcanjoNoCeu.BaseDoCeu();
            float camX = Main.screenPosition.X;

            // 1) degradê: quase preto lá em cima → violeta → um rosa-alaranjado queimado no horizonte
            int linha = baseCeu - (int)(h * 0.3f);
            Degrade.Desenhar(sb, new Rectangle(0, 0, w, linha), new Color(5, 4, 16), new Color(48, 26, 78), a);
            Degrade.Desenhar(sb, new Rectangle(0, linha - 1, w, baseCeu - linha + 1), new Color(48, 26, 78), new Color(120, 52, 82), a);
            Degrade.Desenhar(sb, new Rectangle(0, baseCeu - 1, w, Math.Max(1, h - baseCeu + 1)), new Color(40, 18, 40), new Color(8, 5, 14), a);

            // 2) estrelas, com um pouquinho de parallax
            foreach (Vector3 e in estrelas)
            {
                float x = ((e.X * w - camX * 0.02f) % w + w) % w;
                Vector2 p = new(x, e.Y * linha);
                float pisca = 0.45f + 0.55f * MathF.Sin(t * 1.7f + e.Z * 3f);
                sb.Draw(bloom, p, null, new Color(220, 205, 255, 0) * pisca * a * 0.7f, 0f, bloom.Size() / 2f, 0.03f + (e.Z % 1f) * 0.025f, 0, 0f);
            }

            // 3) o eclipse: coroa dourada (cresce com a raiva dele) + raios lentos + disco negro
            Vector2 ec = new(w * 0.72f - camX * 0.01f % (w * 0.1f), baseCeu - h * 0.62f);
            float coroa = 0.35f + raiva * 0.9f + 0.08f * MathF.Sin(t * 1.3f);
            sb.Draw(bloom, ec, null, new Color(255, 170, 90, 0) * a * 0.35f * coroa, 0f, bloom.Size() / 2f, 3.4f + raiva * 1.5f, 0, 0f);
            for (int i = 0; i < 12; i++)
            {
                float ang = i * MathHelper.TwoPi / 12f + t * 0.04f;
                float len = (0.9f + 0.4f * MathF.Sin(t * 0.8f + i * 1.7f)) * (1f + raiva);
                sb.Draw(bloom, ec + ang.ToRotationVector2() * 60f * len, null, new Color(255, 210, 140, 0) * a * 0.22f * coroa, ang, bloom.Size() / 2f, new Vector2(1.2f * len, 0.07f), 0, 0f);
            }
            sb.Draw(bloom, ec, null, new Color(255, 235, 190, 0) * a * 0.9f * coroa, 0f, bloom.Size() / 2f, 1.05f, 0, 0f);
            sb.Draw(circulo, ec, null, new Color(6, 4, 12) * a, 0f, circulo.Size() / 2f, 92f / circulo.Width, 0, 0f);

            // 4) nebulosa em 3 camadas: manchas grandes violeta/magenta/azul, cada camada anda num ritmo
            foreach (Vector4 n in nebulosa)
            {
                float par = 0.03f + n.W * 0.05f;
                float x = (((n.X * w - camX * par + t * (4f + n.W * 3f)) % (w * 1.4f)) + w * 1.4f) % (w * 1.4f) - w * 0.2f;
                Vector2 p = new(x, linha - (0.7f - n.Y) * h * 0.8f + n.W * 40f);
                Color c = n.W == 0 ? new Color(90, 50, 170, 0) : n.W == 1 ? new Color(170, 60, 150, 0) : new Color(60, 90, 190, 0);
                float respira = 0.8f + 0.2f * MathF.Sin(t * 0.5f + n.X * 9f);
                sb.Draw(bloom, p, null, c * a * 0.16f * respira, n.X * 6f, bloom.Size() / 2f, new Vector2(n.Z * 3.2f, n.Z * 1.4f), 0, 0f);
            }

            // 5) poeira de éter subindo do horizonte
            for (int i = 0; i < 40; i++)
            {
                float k = (t * 0.05f + i * 0.0731f) % 1f;
                float x = (i * 0.618f % 1f) * w + MathF.Sin(t * 0.6f + i) * 20f;
                Vector2 p = new(x, baseCeu - k * h * 0.7f);
                float brilho = MathF.Sin(k * MathHelper.Pi);
                sb.Draw(bloom, p, null, new Color(200, 150, 255, 0) * a * brilho * 0.6f, 0f, bloom.Size() / 2f, 0.05f, 0, 0f);
            }
        }

        public override void Activate(Vector2 position, params object[] args) => ativo = true;
        public override void Deactivate(params object[] args) => ativo = false;
        public override void Reset() => ativo = false;
        public override bool IsActive() => ativo || intensidade > 0f;
    }

    public class SistemaDeCeus : ModSystem
    {
        public override void Load()
        {
            if (Main.dedServ) return;
            SkyManager.Instance[CeuAstral.Chave] = new CeuAstral();
            SkyManager.Instance[CeuDoEter.Chave] = new CeuDoEter();
            SkyManager.Instance[CeuDasNuvens.Chave] = new CeuDasNuvens();
            SkyManager.Instance[CeuDoArmotheus.Chave] = new CeuDoArmotheus();
            SkyManager.Instance[CeuDoDuelo.Chave] = new CeuDoDuelo();
        }

        public override void PostUpdateEverything()
        {
            if (Main.dedServ || Main.gameMenu) return;

            Ligar(CeuAstral.Chave, AstralMundo.AstraniaLiberada);
            Ligar(CeuDoEter.Chave, AstralMundo.EterConstruido && EterEstrutura.Dentro(Main.LocalPlayer));
            Ligar(CeuDasNuvens.Chave, RaidBoss.Ativo is Astharoth a && a.EmFormaDeArcanjo);
            bool noEter = AstralMundo.EterConstruido && EterEstrutura.Dentro(Main.LocalPlayer);
            Ligar(CeuDoDuelo.Chave, RaidBoss.Ativo is Astharoth d && !d.EmFormaDeArcanjo && !noEter);
            Ligar(CeuDoArmotheus.Chave, RaidBoss.Ativo is Bosses.Silvanna.RaidSilvanna);
        }

        private static void Ligar(string chave, bool ligado)
        {
            CustomSky ceu = SkyManager.Instance[chave];
            if (ceu is null) return;
            if (ligado && !ceu.IsActive())
                SkyManager.Instance.Activate(chave, Main.LocalPlayer.Center);
            else if (!ligado && ceu.IsActive())
                SkyManager.Instance.Deactivate(chave);
        }
    }
}
