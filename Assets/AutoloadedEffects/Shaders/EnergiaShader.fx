// ENERGIA: o shader de todo feixe/corte/pilar dos chefes (Desenho.Faixa e Desenho.Ponta).
// Em vez de uma faixa lisa: pontas afiladas, um corpo com borda "comida" pelo ruído, filamentos que correm
// ao longo do golpe, faixas quentes passando e um núcleo branco que tremula.
// Luminance registra como "AscensaoAstral.EnergiaShader". s1 = TurbulentNoise, s2 = WavyBlotchNoise.

sampler texturaRuido : register(s1);
sampler texturaRuido2 : register(s2);

float globalTime;
matrix uWorldViewProjection;
float3 corBorda;
float3 corNucleo;
float intensidade;   // 1 = normal; >1 deixa mais quente/branco (o instante do golpe)
float afilar;        // 0 = pontas retas (pilares), 1 = pontas bem afiladas (cortes)
float velocidade;    // quão rápido a energia corre

struct VertexIn
{
    float4 Position : POSITION0;
    float4 Color : COLOR0;
    float3 TexCoord : TEXCOORD0;
};

struct VertexOut
{
    float4 Position : SV_POSITION;
    float4 Color : COLOR0;
    float2 TexCoord : TEXCOORD0;
};

VertexOut Vertices(in VertexIn input)
{
    VertexOut output = (VertexOut)0;
    output.Position = mul(input.Position, uWorldViewProjection);
    output.Position.z = 0;
    output.Color = input.Color;
    output.TexCoord = input.TexCoord.xy;
    output.TexCoord.y = (output.TexCoord.y - 0.5) / input.TexCoord.z + 0.5;
    return output;
}

float4 Pixels(VertexOut input) : COLOR0
{
    float2 uv = input.TexCoord;
    float t = globalTime * velocidade;

    // pontas: o golpe fica mais fino perto das extremidades
    float ponta = min(uv.x, 1.0 - uv.x);
    float largura = lerp(1.0, smoothstep(0.0, 0.12, ponta), afilar);
    float d = abs(uv.y - 0.5) * 2.0 / max(largura, 0.02);

    // dois ruídos indo em sentidos diferentes = energia "viva"
    float n1 = tex2D(texturaRuido, float2(uv.x * 2.6 - t * 1.9, uv.y * 0.7)).r;
    float n2 = tex2D(texturaRuido2, float2(uv.x * 1.4 + t * 0.7, uv.y * 1.2 + n1 * 0.35)).r;
    float ruido = (n1 + n2) * 0.5;

    // corpo com a borda irregular
    float corpo = smoothstep(1.0, 0.45, d + (ruido - 0.5) * 0.7);

    // filamentos correndo ao longo do golpe
    float fio = frac(uv.y * 2.5 + ruido * 1.6 - t * 0.35);
    fio = pow(1.0 - abs(fio - 0.5) * 2.0, 10.0) * corpo;

    // faixas quentes passando (dá a sensação de pulso viajando)
    float faixa = pow(saturate(sin(uv.x * 16.0 - t * 11.0)), 8.0) * corpo;

    // núcleo branco tremulando
    float nucleo = smoothstep(0.3, 0.0, d) * (0.85 + 0.15 * sin(globalTime * 37.0 + uv.x * 23.0));

    float3 cor = corBorda * (corpo * 0.85 + fio * 0.9 + faixa * 0.45) + corNucleo * nucleo * 1.25 * intensidade;
    cor = lerp(cor, corNucleo, saturate(intensidade - 1.0) * corpo);
    float a = saturate(corpo * 0.9 + nucleo);
    return float4(cor, 1) * a * input.Color;
}

technique Tecnica1
{
    pass AutoloadPass
    {
        VertexShader = compile vs_3_0 Vertices();
        PixelShader = compile ps_3_0 Pixels();
    }
}
