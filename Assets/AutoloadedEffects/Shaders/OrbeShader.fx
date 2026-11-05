// ORBE: uma esfera de energia "de verdade" desenhada num quadrado.
//   - o ruído é lido com a UV deformada pela curvatura da esfera e girando → parece que gira em 3D
//   - borda (fresnel) acesa, núcleo quente, e um anel fino em volta
//   - revelar 0 → 1 mistura a cor neutra com a cor verdadeira (o "eco" das orbes do Arcanjo)
// Luminance registra como "AscensaoAstral.OrbeShader". s1 = TurbulentNoise, s2 = DendriticNoise.

sampler texturaBase : register(s0);
sampler texturaRuido : register(s1);
sampler texturaRuido2 : register(s2);

float globalTime;
float3 corA;        // cor neutra
float3 corB;        // cor verdadeira
float revelar;      // 0..1
float semente;

float4 Orbe(float4 corVert : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float2 p = (uv - 0.5) * 2.0;          // -1..1
    float r = length(p);
    float raio = 0.62;                     // a esfera ocupa o miolo; o resto é brilho

    float3 cor = lerp(corA, corB, revelar);
    float3 saida = float3(0, 0, 0);
    float alfa = 0;

    // ── esfera ──
    if (r < raio)
    {
        float2 q = p / raio;
        float z = sqrt(saturate(1.0 - dot(q, q)));
        float2 esf = q / (z + 1.0);        // curvatura: o ruído "enrola" nas bordas
        float giro = globalTime * 0.35 + semente;
        float2 uvR = esf * 0.55 + float2(giro, semente * 0.37);
        float n1 = tex2D(texturaRuido, uvR * 0.6).r;
        float n2 = tex2D(texturaRuido2, esf * 0.9 - float2(giro * 0.6, globalTime * 0.1) + n1 * 0.25).r;
        float plasma = smoothstep(0.3, 0.95, n1 * 0.55 + n2 * 0.6);
        // veios brilhantes (onde o ruído cruza o meio): parece energia correndo dentro
        float veio = pow(1.0 - abs(n2 - 0.5) * 2.0, 9.0);

        float fresnel = pow(1.0 - z, 2.2);   // borda acesa
        float nucleo = pow(z, 6.0);          // centro quente
        float3 escuro = cor * 0.18;

        saida = escuro + cor * plasma * 1.25 + lerp(cor, float3(1, 1, 1), 0.5) * veio * 0.9 + cor * fresnel * 1.6 + lerp(cor, float3(1, 1, 1), 0.6) * nucleo * 0.45;
        // luz vindo de cima à esquerda
        float luz = saturate(dot(normalize(float3(q, z)), normalize(float3(-0.4, -0.5, 0.8))));
        saida += float3(1, 1, 1) * pow(luz, 24.0) * 0.35;
        alfa = 1.0;
        // borda suave da esfera
        float aa = smoothstep(raio, raio - 0.03, r);
        saida *= aa; alfa *= aa;
    }

    // ── brilho em volta ──
    float halo = exp(-max(r - raio, 0.0) * 7.0) * (r >= raio - 0.03 ? 1.0 : 0.0);
    // anel fino girando, com falhas
    float ang = atan2(p.y, p.x);
    float anel = exp(-pow((r - 0.82) * 28.0, 2.0)) * (0.55 + 0.45 * sin(ang * 3.0 - globalTime * 2.5 + semente));
    float pulso = 0.85 + 0.15 * sin(globalTime * 4.0 + semente);
    saida += cor * (halo * 0.8 + anel * 0.9) * pulso;
    alfa = saturate(alfa + (halo * 0.6 + anel * 0.6) * pulso);

    return float4(saida, alfa * 0.9) * corVert.a;
}

technique Tecnica1
{
    pass AutoloadPass
    {
        PixelShader = compile ps_3_0 Orbe();
    }
}
