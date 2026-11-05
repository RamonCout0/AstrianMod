// Onda de choque (o impacto do Arcanjo): um anel que se expande distorcendo a tela e brilhando.
// Parâmetros da Luminance: globalTime, opacity, focusPosition, screenPosition, screenSize.
// Parâmetros que o Astharoth manda: zoom, raio (px do mundo), espessura (px), forca (0..1), corOnda.

sampler telaTex : register(s0);

float globalTime;
float opacity;
float2 focusPosition;
float2 screenPosition;
float2 screenSize;
float2 zoom;
float raio;
float espessura;
float forca;
float3 corOnda;

float4 Onda(float2 coords : TEXCOORD0) : COLOR0
{
    float4 original = tex2D(telaTex, coords);

    float2 centro = (focusPosition - screenPosition) / screenSize;
    centro = (centro - 0.5) * zoom + 0.5;
    float2 proporcao = float2(screenSize.x / screenSize.y, 1);
    float2 delta = (coords - centro) * proporcao;
    float dist = length(delta);

    float r = raio * zoom.y / screenSize.y;
    float e = max(espessura * zoom.y / screenSize.y, 0.0001);
    float x = (dist - r) / e;
    float anel = exp(-x * x);

    // Empurra a imagem para fora na borda da onda (a "lente" do choque)
    float2 dir = delta / max(dist, 0.0001);
    float2 desloc = dir * anel * forca * e * 0.6;
    float4 cor = tex2D(telaTex, coords - desloc / proporcao);
    cor.rgb += corOnda * anel * forca * 0.55;

    return float4(lerp(original.rgb, cor.rgb, opacity), 1);
}

technique Tecnica1
{
    pass AutoloadPass
    {
        PixelShader = compile ps_3_0 Onda();
    }
}
