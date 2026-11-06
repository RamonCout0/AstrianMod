using AscensaoAstral.Content.Projectiles;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.DataStructures;
using Terraria.ID;
using Terraria.ModLoader;

namespace AscensaoAstral.Common.Raid
{
    /// <summary>
    /// A regra de morte das raids:
    ///  1. Morreu durante uma raid → deixa uma ALMA onde caiu e NÃO renasce sozinho (o contador fica congelado).
    ///  2. Um aliado precisa ir até a alma e ficar PARADO em cima dela por 3 s → você volta ali com 30% de vida.
    ///  3. Enquanto espera, a câmera segue um aliado vivo (espectador).
    ///  4. Todos morreram (ou uma mecânica de wipe pegou todo mundo) → o chefe volta ao checkpoint e todos renascem lá.
    /// No solo, isso vira "morreu = volta ao checkpoint", sem esperar os 30 s do Terraria.
    /// Também guarda a câmera de cutscene (Foco).
    /// </summary>
    public class RaidJogador : ModPlayer
    {
        public const int DuracaoRevive = 180; // 3 s parado em cima da alma
        private const float RaioAlma = 56f;

        public bool TemAlma { get; private set; }
        public Vector2 AlmaPos { get; private set; }
        public int ProgressoRevive { get; private set; }

        private bool reviverAoNascer;
        private bool voltarAoCheckpoint;
        private Vector2 camEspectador;

        // Câmera de cutscene (os chefes ligam/desligam)
        public static Vector2? Foco;
        public static float ForcaFoco;
        private Vector2 camCinema;
        private float forcaAtual;

        // Com o menu da raid aberto, o clique é do MENU: não usa o item da mão (senão o clique no Iniciar
        // usava o item de novo e reabria o menu por cima)
        public override void SetControls()
        {
            if (MenuDaRaid.Aberta is not null && Player.whoAmI == Main.myPlayer)
            {
                Player.controlUseItem = false;
                Player.controlUseTile = false;
                Player.mouseInterface = true;
            }
        }

        public override void OnEnterWorld()
        {
            TemAlma = false;
            reviverAoNascer = voltarAoCheckpoint = false;
            Foco = null;
        }

        public override void Kill(double damage, int hitDirection, bool pvp, PlayerDeathReason damageSource)
        {
            if (Player.whoAmI != Main.myPlayer)
                return;

            RaidBoss b = RaidBoss.Ativo;
            if (b is null || b.Reiniciando || !b.JogadorNaRaid(Player))
                return;

            TemAlma = true;
            AlmaPos = Player.Center;
            ProgressoRevive = 0;
            Projectile.NewProjectile(Player.GetSource_Death(), AlmaPos, Vector2.Zero,
                ModContent.ProjectileType<AlmaDoJogador>(), 0, 0f, Player.whoAmI, Player.whoAmI);
        }

        public override void UpdateDead()
        {
            if (Player.whoAmI != Main.myPlayer)
                return;

            RaidBoss b = RaidBoss.Ativo;
            if (b is null)
            {
                TemAlma = false; // sem raid: respawn normal do Terraria
                return;
            }

            if (b.Reiniciando)
            {
                // Wipe: todo mundo renasce rápido e vai para o checkpoint
                TemAlma = false;
                voltarAoCheckpoint = true;
                if (Player.respawnTimer > 90)
                    Player.respawnTimer = 90;
                return;
            }

            if (TemAlma)
            {
                bool alguemParado = false;
                foreach (Player p in Main.ActivePlayers)
                {
                    if (p.whoAmI == Player.whoAmI || p.dead)
                        continue;
                    if (p.Distance(AlmaPos) < RaioAlma && p.velocity.Length() < 1f)
                        alguemParado = true;
                }

                ProgressoRevive = alguemParado ? ProgressoRevive + 1 : System.Math.Max(0, ProgressoRevive - 2);

                if (ProgressoRevive >= DuracaoRevive)
                {
                    TemAlma = false;
                    reviverAoNascer = true;
                    Player.respawnTimer = 0;
                    return;
                }
            }

            // Sem checkpoint: o contador normal do Terraria vale (a alma só deixa um aliado reviver ANTES dele acabar)
        }

        public override void PreUpdate()
        {
            if (Player.whoAmI != Main.myPlayer || Player.dead)
                return;

            if (reviverAoNascer)
            {
                reviverAoNascer = false;
                Teleportar(Player, AlmaPos);
                Player.statLife = System.Math.Max(1, Player.statLifeMax2 * 3 / 10);
                Player.SetImmuneTimeForAllTypes(120);
            }
            else if (voltarAoCheckpoint)
            {
                voltarAoCheckpoint = false;
                RaidBoss b = RaidBoss.Ativo;
                if (b is not null)
                {
                    Teleportar(Player, b.PontoDeRetorno);
                    Player.statLife = Player.statLifeMax2;
                    Player.SetImmuneTimeForAllTypes(120);
                }
            }
        }

        /// <summary>Teleporta o jogador local (pelo centro) e avisa o servidor.</summary>
        public static void Teleportar(Player p, Vector2 centro)
        {
            Vector2 pos = centro - new Vector2(p.width / 2f, p.height / 2f);
            p.Teleport(pos, TeleportationStyleID.RodOfDiscord);
            p.velocity = Vector2.Zero;
            if (Main.netMode == NetmodeID.MultiplayerClient)
                NetMessage.SendData(MessageID.TeleportEntity, -1, -1, null, 0, p.whoAmI, pos.X, pos.Y, TeleportationStyleID.RodOfDiscord);
        }

        public override void ModifyScreenPosition()
        {
            Vector2 meiaTela = new(Main.screenWidth / 2f, Main.screenHeight / 2f);

            // Espectador: morto durante a raid → câmera num aliado vivo
            RaidBoss b = RaidBoss.Ativo;
            if (Player.dead && b is not null && !b.Reiniciando)
            {
                Player alvo = null;
                float melhor = float.MaxValue;
                foreach (Player p in Main.ActivePlayers)
                {
                    if (p.dead || !b.JogadorNaRaid(p)) continue;
                    float d = p.Distance(Player.Center);
                    if (d < melhor) { melhor = d; alvo = p; }
                }
                if (alvo is not null)
                {
                    camEspectador = Vector2.Lerp(camEspectador == Vector2.Zero ? Main.screenPosition : camEspectador, alvo.Center - meiaTela, 0.12f);
                    Main.screenPosition = camEspectador;
                    return;
                }
            }
            camEspectador = Vector2.Zero;

            // Cutscene: a câmera vai suavemente até o Foco
            float forcaDesejada = Foco.HasValue ? ForcaFoco : 0f;
            forcaAtual = MathHelper.Lerp(forcaAtual, forcaDesejada, 0.06f);
            if (Foco.HasValue)
                camCinema = Foco.Value - meiaTela;
            if (forcaAtual > 0.01f)
                Main.screenPosition = Vector2.Lerp(Main.screenPosition, camCinema, forcaAtual);
        }
    }
}
