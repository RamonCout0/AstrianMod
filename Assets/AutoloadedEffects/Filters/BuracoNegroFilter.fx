// Filtro de TELA do buraco negro (fase 2 do Armotheus).
// A Luminance acha este arquivo em Assets/AutoloadedEffects/Filters e o registra como "ArmotheusLab.BuracoNegroFilter".
// Ela desenha a tela inteira passando por este shader enquanto o filtro estiver ativo.
//
// Parâmetros que a Luminance preenche sozinha: globalTime, opacity, focusPosition, screenPosition, screenSize.
// Parâmetros que o Armotheus manda: zoom, raioHorizonte, corDisco.

sampler telaTex : register(s0);  // a imagem da tela
sampler ruidoTex : register(s1); // ruído (SetTexture no C#)

float globalTime;
float opacity;         // 0 → 1: a Luminance faz o fade sozinha
float2 focusPosition;  // posição do buraco no MUNDO
float2 screenPosition;
float2 screenSize;
float2 zoom;           // Main.GameViewMatrix.Zoom
float raioHorizonte;   // raio do "horizonte de eventos", em pixels do mundo
float3 corDisco;       // cor do anel de matéria girando

float4 BuracoNegro(float2 coords : TEXCOORD0) : COLOR0
{
    float4 original = tex2D(telaTex, coords);

    // 1) Onde está o buraco na tela (0 a 1), considerando o zoom da câmera
    float2 centro = (focusPosition - screenPosition) / screenSize;
    centro = (centro - 0.5) * zoom + 0.5;

    // 2) Distância corrigida pela proporção da tela (senão o círculo vira elipse)
    float2 proporcao = float2(screenSize.x / screenSize.y, 1);
    float2 delta = (coords - centro) * proporcao;
    float dist = length(delta);
    float raio = raioHorizonte * zoom.y / screenSize.y;

    // 3) Lente gravitacional: perto do buraco a imagem gira e é puxada.
    //    A força cai com o quadrado da distância, como gravidade.
    float forca = min(raio * raio * 1.8 / max(dist * dist, 0.00001), 4.0);
    float giro = forca * 1.6 + forca * sin(globalTime * 0.7) * 0.15;
    float s = sin(giro);
    float c = cos(giro);
    float2 girado = float2(delta.x * c - delta.y * s, delta.x * s + delta.y * c);
    float2 puxado = girado * (1 + forca * 0.5);
    float4 cor = tex2D(telaTex, centro + puxado / proporcao);

    // 4) Disco de acreção: um anel brilhante girando em volta do horizonte
    float angulo = atan2(delta.y, delta.x);
    float largura = raio * 0.45;
    float anel = exp(-pow((dist - raio * 1.45) / largura, 2));
    float ruido = tex2D(ruidoTex, float2(angulo / 6.2831 + globalTime * 0.12, dist * 4 - globalTime * 0.5)).r;
    cor.rgb += corDisco * anel * (0.5 + ruido * 1.4);

    // Halo fraco mais para fora
    float halo = exp(-pow((dist - raio * 2.4) / (raio * 1.2), 2)) * 0.25;
    cor.rgb += corDisco * halo;

    // 5) O horizonte: preto absoluto, com borda suave
    float dentro = smoothstep(raio * 1.03, raio * 0.92, dist);
    cor.rgb = lerp(cor.rgb, float3(0, 0, 0), dentro);

    // 6) Mistura com a tela normal pelo fade da Luminance
    return float4(lerp(original.rgb, cor.rgb, opacity), 1);
}

technique Tecnica1
{
    pass AutoloadPass
    {
        PixelShader = compile ps_3_0 BuracoNegro();
    }
}
