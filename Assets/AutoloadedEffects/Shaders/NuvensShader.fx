// MAR DE NUVENS do Arcanjo — três camadas em alturas diferentes, cada uma com perspectiva, sombra própria
// e bordas acesas pela luz dele. Ao meio-dia (meioDia = 1) os topos das nuvens ficam brancos de luz.
// Relâmpagos: um clarão dentro das nuvens em relampagoPos (o raio em si é desenhado no C#).
//
// Retângulo na parte de BAIXO da tela; uv 0..1 cobre o retângulo (y = 0 é o horizonte).
// Luminance registra como "AscensaoAstral.NuvensShader". s1 = TurbulentNoise, s2 = WavyBlotchNoise.

sampler texturaBase : register(s0);
sampler ruido1 : register(s1);
sampler ruido2 : register(s2);

float globalTime;
float2 luzPos;        // posição do Arcanjo nas coordenadas do retângulo (y < 0 = acima do horizonte)
float luzForca;
float meioDia;        // 0..1: quanto ele está no alto
float3 corLuz;
float3 corEscura;
float3 corClara;
float3 corHorizonte;
float2 relampagoPos;
float relampagoForca;
float proporcao;
float opacidade;

float Densidade(float2 p, float semente)
{
    float d = tex2D(ruido1, p * 0.55 + semente).r * 0.55;
    d += tex2D(ruido2, p * 1.3 + float2(globalTime * 0.004, semente)).r * 0.3;
    d += tex2D(ruido1, p * 3.1 - float2(globalTime * 0.007, globalTime * 0.002) + semente).r * 0.15;
    return d;
}

// Uma camada de nuvens. horizonte = onde ela começa (em uv.y); vel = velocidade do vento; escuro = quanto é sombreada
float4 Camada(float2 uv, float horizonte, float vel, float semente, float escala, float escuro)
{
    float prof = uv.y - horizonte;
    if (prof <= 0.0) return float4(0, 0, 0, 0);
    prof = prof / (1.0 - horizonte);
    float z = 1.0 / (prof + 0.08);
    float2 mundo = float2((uv.x - 0.5) * proporcao * z * 0.22 * escala + globalTime * vel, z * 0.35 * escala + globalTime * 0.006);

    float dens = Densidade(mundo, semente);
    float nuvem = smoothstep(0.30, 0.62, dens);

    float2 paraLuz = normalize((luzPos - uv) * float2(proporcao, 1.0) + float2(0.0001, 0));
    float densPerto = Densidade(mundo + paraLuz * float2(0.06, 0.04) * z * 0.3, semente);
    float sombraPropria = saturate((densPerto - dens) * 5.0);

    float2 d = (uv - luzPos) * float2(proporcao, 1.0);
    float dist = length(d);
    float luz = luzForca * (exp(-dist * dist * 1.2) + 0.25 * exp(-dist * 1.4));

    float3 base = lerp(corEscura, corClara, nuvem * 0.8 + prof * 0.2) * (1.0 - escuro);
    base *= lerp(1.0, 0.55, sombraPropria);
    float borda = saturate(nuvem * (1.0 - nuvem) * 4.0);
    float3 res = base + corLuz * luz * (0.35 + borda * 1.4) * (1.0 - sombraPropria * 0.7);

    // Meio-dia: o topo das nuvens (lado sem sombra) brilha forte
    res += corLuz * meioDia * (nuvem * (1.0 - sombraPropria * 0.8) * 0.9 + borda * 0.6) * (0.6 + 0.8 * exp(-dist * 0.8));

    // Relâmpago: clarão azulado dentro das nuvens perto dele
    float2 dr = (uv - relampagoPos) * float2(proporcao, 1.0);
    res += float3(0.75, 0.8, 1.0) * relampagoForca * exp(-dot(dr, dr) * 6.0) * (0.4 + nuvem);

    float nevoa = exp(-prof * 7.0);
    res = lerp(res, corHorizonte + corLuz * luz * 0.4 + corLuz * meioDia * 0.25, nevoa * 0.85);

    // topo da camada "fofo": a borda sobe onde a nuvem é densa (silhueta de nuvem, não uma linha reta)
    float topo = smoothstep(0.0, 0.05, prof - (0.62 - dens) * 0.22);
    float alfa = topo * lerp(0.7, 1.0, nuvem);
    return float4(res * alfa, alfa);
}

float4 Nuvens(float4 cor : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    // de trás para frente: a camada lá no fundo (mais alta na tela), a do meio, e a da frente (maior e mais escura)
    float4 resultado = Camada(uv, 0.0, 0.004, 0.0, 1.0, 0.0);
    float4 meio = Camada(uv, 0.18 + sin(uv.x * 6.0 + globalTime * 0.05) * 0.015, 0.009, 0.37, 0.7, 0.12);
    resultado = meio + resultado * (1.0 - meio.a);
    float4 frente = Camada(uv, 0.45 + sin(uv.x * 4.0 - globalTime * 0.04) * 0.03, 0.016, 0.71, 0.45, 0.28);
    resultado = frente + resultado * (1.0 - frente.a);
    return resultado * opacidade * cor.a; // pré-multiplicado
}

technique Tecnica1
{
    pass AutoloadPass
    {
        PixelShader = compile ps_3_0 Nuvens();
    }
}
