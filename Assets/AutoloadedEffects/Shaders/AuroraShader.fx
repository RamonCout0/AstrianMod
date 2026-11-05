// AURORA no céu do Armotheus: cortinas de luz que ondulam devagar.
// Retângulo na parte de CIMA da tela. Luminance registra como "AscensaoAstral.AuroraShader".
// s1 = ruído (TurbulentNoise) para os "fios" verticais da cortina.

sampler texturaBase : register(s0);
sampler ruido1 : register(s1);

float globalTime;
float intensidade;
float3 corBaixo;   // base da cortina (verde-gelo)
float3 corAlto;    // topo da cortina (violeta)
float proporcao;

float Cortina(float2 uv, float altura, float fase, float largura)
{
    float onda = sin(uv.x * 2.3 * proporcao * 0.3 + globalTime * 0.15 + fase) * 0.06
               + sin(uv.x * 5.1 * proporcao * 0.3 - globalTime * 0.11 + fase * 2.0) * 0.03;
    float centro = altura + onda;
    float dy = uv.y - centro;
    // mais comprida para cima (os raios sobem), corte firme embaixo
    // uv.y cresce para BAIXO: os raios sobem (dy < 0) bem compridos; embaixo a cortina tem um corte firme
    float forma = dy < 0.0 ? exp(dy / (largura * 3.5)) : exp(-dy * dy / (largura * largura * 1.5));
    float fios = tex2D(ruido1, float2(uv.x * proporcao * 1.2 + fase + globalTime * 0.01, uv.y * 0.15 + globalTime * 0.03 + fase)).r;
    fios = pow(saturate(fios * 1.4), 2.0);
    float pulso = 0.6 + 0.4 * sin(globalTime * 0.7 + uv.x * 9.0 + fase);
    return forma * (0.35 + fios * 0.9) * pulso;
}

float4 Aurora(float4 cor : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float a1 = Cortina(uv, 0.55, 0.0, 0.05);
    float a2 = Cortina(uv, 0.40, 1.7, 0.04) * 0.7;
    float a3 = Cortina(uv, 0.68, 3.1, 0.035) * 0.5;
    float total = a1 + a2 + a3;
    // cor muda com a altura: em cima violeta, embaixo verde-gelo
    float3 c = lerp(corAlto, corBaixo, saturate(uv.y * 1.6 - 0.2));
    float3 res = c * total * intensidade;
    // some suavemente nas bordas do retângulo
    res *= smoothstep(0.0, 0.15, uv.y) * smoothstep(1.0, 0.8, uv.y);
    return float4(res, 0) * cor.a; // aditivo (alfa 0 no modo pré-multiplicado)
}

technique Tecnica1
{
    pass AutoloadPass
    {
        PixelShader = compile ps_3_0 Aurora();
    }
}
