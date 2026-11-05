// AVISO de perigo: o retângulo que mostra onde o golpe vai cair.
// Borda acesa que pulsa, listras diagonais correndo por dentro e um preenchimento que avança do centro
// para as bordas conforme o golpe se aproxima (progresso 0 → 1). No fim ele pisca branco.
// Luminance registra como "AscensaoAstral.AvisoShader". Desenhado como sprite esticado no retângulo.

sampler texturaBase : register(s0);

float globalTime;
float progresso;   // 0 = acabou de aparecer, 1 = vai bater agora
float3 cor;
float2 tamanho;    // largura e altura do retângulo em pixels

float4 Aviso(float4 corVert : COLOR0, float2 uv : TEXCOORD0) : COLOR0
{
    float2 px = uv * tamanho;
    float2 dBorda = min(px, tamanho - px);
    float borda = min(dBorda.x, dBorda.y);                   // distância até a borda mais perto (px)

    float linha = 1.0 - smoothstep(1.5, 3.5, borda);         // a borda fina
    float brilhoBorda = exp(-borda / 10.0) * 0.6;            // o brilho que sai dela para dentro

    // listras diagonais correndo
    float listras = frac((px.x + px.y) / 26.0 - globalTime * 1.6);
    listras = smoothstep(0.0, 0.15, listras) * (1.0 - smoothstep(0.45, 0.6, listras));

    // preenchimento: cresce do centro (no lado mais curto) até as bordas
    float2 centro = abs(uv - 0.5) * 2.0;
    float eixo = tamanho.x < tamanho.y ? centro.x : centro.y;
    float cheio = 1.0 - smoothstep(progresso - 0.05, progresso, eixo);

    float pulso = 0.75 + 0.25 * sin(globalTime * (8.0 + progresso * 18.0));
    float final = smoothstep(0.85, 1.0, progresso);           // pisca branco antes do golpe

    float a = (linha * 0.95 + brilhoBorda * 0.5) * pulso + listras * 0.16 + cheio * 0.22;
    float3 c = lerp(cor, float3(1, 1, 1), linha * 0.35 + final * 0.6);
    a = saturate(a + final * 0.15);
    return float4(c * a, a * 0.85) * corVert.a; // pré-multiplicado, um pouco aditivo
}

technique Tecnica1
{
    pass AutoloadPass
    {
        PixelShader = compile ps_3_0 Aviso();
    }
}
