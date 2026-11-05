// RAIOS DE LUZ: feixes inclinados caindo do céu (god rays), com poeira brilhando dentro.
// Desenhado num retângulo do tamanho da arena. A SOMBRA (sombraEsq..sombraDir, em uv.x) fica sem luz,
// com uma transição suave. carga = o quanto a luz está forte. s1 = TurbulentNoise.
// Luminance registra como "AscensaoAstral.RaiosShader".

sampler texturaBase : register(s0);
sampler texturaRuido : register(s1);

float globalTime;
float3 cor;
float carga;
float sombraEsq;
float sombraDir;
float proporcao;    // largura / altura do retângulo

float4 Raios(float4 corVert : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float t = globalTime;
    // inclinação: os raios vêm de cima, um pouco de lado
    float x = uv.x * proporcao + uv.y * 0.35;
    float r1 = tex2D(texturaRuido, float2(x * 0.9 + t * 0.03, 0.1)).r;
    float r2 = tex2D(texturaRuido, float2(x * 2.3 - t * 0.05, 0.6)).r;
    float raio = pow(saturate(r1 * 0.7 + r2 * 0.5 - 0.25), 1.8) * 1.6;
    // mais forte em cima, some perto do chão (mas acende no chão onde bate)
    float altura = lerp(1.0, 0.35, uv.y) + smoothstep(0.9, 1.0, uv.y) * 0.8;
    // poeira dourada flutuando
    float p = tex2D(texturaRuido, float2(uv.x * proporcao * 6.0, uv.y * 6.0 - t * 0.15)).r;
    float poeira = pow(p, 22.0) * 10.0;
    // a sombra: sem luz (borda suave)
    float fora = 1.0 - smoothstep(sombraEsq - 0.03, sombraEsq + 0.01, uv.x) * smoothstep(sombraDir + 0.03, sombraDir - 0.01, uv.x);
    float bordas = smoothstep(0.0, 0.04, uv.x) * smoothstep(1.0, 0.96, uv.x) * smoothstep(0.0, 0.08, uv.y);
    float a = saturate((raio * altura + poeira) * carga * fora * bordas);
    float3 c = lerp(cor, float3(1, 0.97, 0.88), saturate(raio - 0.4)) * a;
    return float4(c, a * 0.6) * corVert.a;
}

technique Tecnica1
{
    pass AutoloadPass
    {
        PixelShader = compile ps_3_0 Raios();
    }
}
