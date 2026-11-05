// Shader do laser da Varredura Escarlate.
// A Luminance acha este arquivo sozinha (por estar em Assets/AutoloadedEffects/Shaders) e o registra como
// "ArmotheusLab.LaserEscarlateShader". Na primeira vez que o mod roda em single player, ela mesma compila
// o .fx com o fxc que vem junto dela e gera o .xnb ao lado deste arquivo.

sampler texturaRuido : register(s1); // definida no C# com SetTexture(..., 1)

float globalTime;              // a Luminance preenche sozinha a cada Apply()
matrix uWorldViewProjection;   // a Luminance preenche sozinha no RenderTrail
float3 corBorda;
float3 corNucleo;

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

// Parte dos vértices: só posiciona os pontos da faixa na tela. Quase todo shader de primitiva usa esta mesma parte.
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

// Parte dos pixels: aqui está a "arte" do laser.
// uv.x = ao longo do laser (0 = olho, 1 = ponta); uv.y = de uma borda à outra (0.5 = meio).
float4 Pixels(VertexOut input) : COLOR0
{
    float2 uv = input.TexCoord;

    // Distância até o meio do laser: 0 no centro, 1 nas bordas
    float distMeio = abs(uv.y - 0.5) * 2;

    // Ruído rolando para fora do olho: dá a sensação de energia correndo pelo raio
    float ruido = tex2D(texturaRuido, float2(uv.x * 2.5 - globalTime * 3.0, uv.y * 0.8)).r;
    float ruido2 = tex2D(texturaRuido, float2(uv.x * 1.3 - globalTime * 1.7, uv.y * 1.4 + 0.3)).r;
    float energia = (ruido + ruido2) * 0.5;

    // Borda irregular: o ruído "come" as bordas
    float forma = smoothstep(1.0, 0.55, distMeio + (energia - 0.5) * 0.5);

    // Núcleo quente: bem no meio, quase branco
    float nucleo = smoothstep(0.35, 0.0, distMeio);
    float3 cor = lerp(corBorda, corNucleo, nucleo);

    // Brilho extra onde o ruído é forte
    cor += energia * 0.35 * corBorda;

    return float4(cor, 1) * forma * input.Color;
}

technique Tecnica1
{
    pass AutoloadPass
    {
        VertexShader = compile vs_3_0 Vertices();
        PixelShader = compile ps_3_0 Pixels();
    }
}
