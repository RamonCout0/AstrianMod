// Filtro de TELA do Véu rasgado: muda a cara do mundo inteiro na fase 2.
// Registrado pela Luminance como "ArmotheusLab.VeuFilter".
//  - tira a cor do mundo e tinge de roxo
//  - escurece as bordas (vinheta)
//  - ondula a imagem de leve, como se a realidade estivesse "mole"
//  - separa as cores nas bordas (aberração cromática)

sampler telaTex : register(s0);

float globalTime;
float opacity;      // fade automático da Luminance
float2 screenSize;
float intensidade;  // 0 a 1, controlado pelo Armotheus (cresce enquanto o Véu abre)

float4 Veu(float2 coords : TEXCOORD0) : COLOR0
{
    float forca = opacity * intensidade;

    // 1) Ondulação lenta (bem sutil: 0,3% da tela)
    float2 onda = float2(sin(coords.y * 18 + globalTime * 1.3), cos(coords.x * 14 + globalTime * 1.1)) * 0.003 * forca;
    float2 uv = coords + onda;

    // 2) Aberração cromática: mais forte longe do centro
    float2 doCentro = uv - 0.5;
    float borda = dot(doCentro, doCentro) * 4;       // 0 no centro, ~2 nos cantos
    float2 desvio = doCentro * 0.012 * borda * forca;
    float r = tex2D(telaTex, uv + desvio).r;
    float g = tex2D(telaTex, uv).g;
    float b = tex2D(telaTex, uv - desvio).b;
    float3 cor = float3(r, g, b);

    // 3) Tira a cor e tinge de roxo (as luzes fortes ficam em vermelho-rosado)
    float brilho = dot(cor, float3(0.3, 0.59, 0.11));
    float3 sombra = float3(0.16, 0.08, 0.30);
    float3 luz = float3(1.0, 0.62, 0.72);
    float3 tingido = lerp(sombra, luz, brilho) * (0.35 + brilho);
    cor = lerp(cor, tingido, 0.65 * forca);

    // 4) Vinheta pulsando devagar
    float vinheta = 1 - smoothstep(0.25, 0.95, length(doCentro * float2(1.2, 1))) * (0.75 + sin(globalTime * 0.9) * 0.08);
    cor *= lerp(1, vinheta, forca);

    return float4(cor, 1);
}

technique Tecnica1
{
    pass AutoloadPass
    {
        PixelShader = compile ps_3_0 Veu();
    }
}
