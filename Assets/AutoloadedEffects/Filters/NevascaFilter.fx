// NEVASCA da Silvanna (filtro de tela): a nevasca do Terraria, só que com cara de "situação crítica".
//  - neve em 3 camadas soprada pelo vento (rápida e inclinada)
//  - névoa branca que fecha a visão
//  - CONGELAMENTO: quanto mais tempo fora do abrigo (congelamento 0→1), mais as bordas viram cristais de gelo
//    e a imagem perde cor
// Luminance registra como "AscensaoAstral.NevascaFilter". s1 = ruído de cristal (DendriticNoise), s2 = TurbulentNoise.

sampler telaTex : register(s0);
sampler cristal : register(s1);
sampler ruido : register(s2);

float globalTime;
float opacity;
float2 screenSize;
float intensidade;    // força da nevasca (0..1)
float congelamento;   // quanto o jogador já congelou (0..1)
float abrigado;       // 1 = atrás do cavaleiro (a nevasca diminui)
float2 abrigoX;       // faixa do abrigo na tela (x0, x1 em UV): a tempestade quebra ali

float Neve(float2 uv, float escala, float vel, float semente)
{
    float2 p = uv * float2(escala * screenSize.x / screenSize.y, escala);
    p += float2(-globalTime * vel, globalTime * vel * 0.25);          // vento para a direita e um pouco para baixo
    p.y += p.x * 0.35;                                                 // inclinação
    float n = tex2D(ruido, p * 0.09 + semente).r;
    float flocos = smoothstep(0.66, 0.8, n);
    return flocos;
}

float4 Nevasca(float2 coords : TEXCOORD0) : COLOR0
{
    // o abrigo: um corredor calmo atrás do cavaleiro, com as bordas marcadas pelo vento que bate e desvia
    float naFaixa = smoothstep(abrigoX.x - 0.015, abrigoX.x + 0.01, coords.x) * smoothstep(abrigoX.y + 0.015, abrigoX.y - 0.01, coords.x);
    float f = opacity * intensidade * (1.0 - abrigado * 0.4) * (1.0 - naFaixa * 0.8);
    float2 uv = coords + float2(sin(coords.y * 30.0 + globalTime * 7.0) * 0.002 * f, 0);
    float3 cor = tex2D(telaTex, uv).rgb;

    // perde a cor e esfria
    float luz = dot(cor, float3(0.3, 0.59, 0.11));
    float3 frio = lerp(float3(0.12, 0.2, 0.35), float3(0.85, 0.93, 1.0), luz);
    cor = lerp(cor, frio, saturate(0.22 * f + 0.45 * congelamento));

    // névoa branca soprando
    float nevoa = tex2D(ruido, coords * float2(1.5, 0.8) + float2(-globalTime * 0.15, globalTime * 0.02)).r;
    cor = lerp(cor, float3(0.88, 0.94, 1.0), saturate(nevoa * 0.3 * f));

    // neve em camadas
    float neve = Neve(coords, 6.0, 0.9, 0.0) * 0.5 + Neve(coords, 3.0, 1.4, 0.3) * 0.8 + Neve(coords, 1.6, 2.2, 0.7);
    cor += float3(0.9, 0.95, 1.0) * neve * f * 0.8;

    // borda do abrigo: cortina de neve batendo (faixa clara que ondula) + um leve calor verde por dentro
    float2 bordas = abs(coords.xx - abrigoX) * screenSize.x;
    float cortina = exp(-min(bordas.x, bordas.y) / 10.0) * (0.6 + 0.4 * tex2D(ruido, float2(coords.y * 3.0 - globalTime * 0.8, globalTime * 0.1)).r);
    cor += float3(0.75, 0.95, 1.0) * cortina * opacity * 0.55;
    cor += float3(0.1, 0.25, 0.15) * naFaixa * opacity * 0.35;

    // congelamento: cristais de gelo crescendo das bordas para dentro
    float2 doCentro = (coords - 0.5) * float2(1.3, 1.0);
    float borda = length(doCentro);
    float cristais = tex2D(cristal, coords * 1.5).r;
    float alcance = lerp(0.85, 0.35, congelamento);                    // quanto mais congelado, mais perto do centro
    float gelo = smoothstep(alcance - 0.12, alcance, borda + cristais * 0.22) * saturate(congelamento * 1.5);
    float3 corGelo = lerp(float3(0.55, 0.8, 1.0), float3(0.95, 1.0, 1.0), cristais);
    cor = lerp(cor, corGelo, saturate(gelo * 0.85));

    return float4(cor, 1);
}

technique Tecnica1
{
    pass AutoloadPass
    {
        PixelShader = compile ps_3_0 Nevasca();
    }
}
