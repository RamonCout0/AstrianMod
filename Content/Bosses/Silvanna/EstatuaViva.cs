using AscensaoAstral.Content.Projectiles;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.ID;
using Terraria.ModLoader;

namespace AscensaoAstral.Content.Bosses.Silvanna
{
    /// <summary>Estátua viva da mecânica final: guarda um escudo. Quebrar a estátua destranca o escudo.</summary>
    public class EstatuaViva : ModNPC
    {
        public override void SetStaticDefaults()
        {
            NPCID.Sets.NPCBestiaryDrawOffset.Add(Type, new NPCID.Sets.NPCBestiaryDrawModifiers { Hide = true });
        }

        public override void SetDefaults()
        {
            NPC.width = 40;
            NPC.height = 64;
            NPC.damage = 30;
            NPC.defense = 15;
            NPC.lifeMax = 2400;
            NPC.knockBackResist = 0f;
            NPC.HitSound = SoundID.Tink;
            NPC.DeathSound = SoundID.Item70;
            NPC.aiStyle = -1;
            NPC.value = 0f;
        }

        public override bool CheckActive() => false;

        public override void AI()
        {
            if (!NPC.AnyNPCs(ModContent.NPCType<RaidSilvanna>()))
            {
                NPC.active = false;
                return;
            }
            NPC.velocity.X = 0f;
            Lighting.AddLight(NPC.Center, 0.3f, 0.5f, 0.7f);
        }

        public override void HitEffect(NPC.HitInfo hit)
        {
            for (int i = 0; i < 4; i++)
                Dust.NewDust(NPC.position, NPC.width, NPC.height, DustID.Stone, hit.HitDirection * 2f, -2f);
        }

        public override bool PreDraw(SpriteBatch spriteBatch, Vector2 screenPos, Color drawColor)
        {
            Texture2D tex = TextureAssets.Npc[Type].Value;
            Desenho.Brilho(NPC.Center, new Color(90, 170, 255), 0.9f);
            spriteBatch.Draw(tex, NPC.Center - screenPos, null, Color.Lerp(drawColor, Color.White, 0.6f), 0f, tex.Size() / 2f, 1f, SpriteEffects.None, 0f);
            return false;
        }
    }
}
