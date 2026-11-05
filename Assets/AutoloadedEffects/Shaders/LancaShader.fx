// LANÇA: a arma tem FORMA. uv.x = 0 no cabo, 1 na ponta. O shader recorta o perfil (haste fina, guarda,
// lâmina larga que afina até a ponta), pinta metal com faixas, um reflexo que corre pela lâmina e
// a aura (cor) em volta. s1 = TurbulentNoise, s2 = WavyBlotchNoise.
// Luminance registra como "AscensaoAstral.LancaShader".

sampler texturaRuido : register(s1);
sampler texturaRuido2 : register(s2);

float globalTime;
matrix uWorldViewProjection;
float3 cor;          // cor principal do golpe
float intensidade;   // 1 = normal; >1 = o instante do golpe (estoura para o branco)
float opacidade;

struct VertexIn { float4 Position : POSITION0; float4 Color : COLOR0; float3 TexCoord : TEXCOORD0; };
struct VertexOut { float4 Position : SV_POSITION; float4 Color : COLOR0; float2 TexCoord : TEXCOORD0; };

VertexOut Vertices(in VertexIn input)
{
    VertexOut o = (VertexOut)0;
    o.Position = mul(input.Position, uWorldViewProjection);
    o.Position.z = 0;
    o.Color = input.Color;
    o.TexCoord = input.TexCoord.xy;
    o.TexCoord.y = (o.TexCoord.y - 0.5) / input.TexCoord.z + 0.5;
    return o;
}

// O C# desenha a faixa com o DOBRO da largura do golpe: o miolo (|y| < 0.5) é o golpe,
// a outra metade é só o brilho, que cai suave até zero (nada de borda reta).
float Brilho(float d, float forca) { return exp(-max(d - 0.5, 0.0) * 9.0) * forca; }
float Pontas(float x, float tam) { return smoothstep(0.0, tam, x) * smoothstep(1.0, 1.0 - tam, x); }

float Perfil(float x)
{
    // largura (0..0.5 da faixa) ao longo da lança
    float haste = 0.07;
    float guarda = 0.18 * exp(-pow((x - 0.62) * 40.0, 2.0));
    float lamina = x > 0.64 ? 0.24 * pow(saturate((1.0 - x) / 0.36), 0.75) * smoothstep(0.64, 0.7, x) : 0.0;
    return max(max(haste * smoothstep(0.0, 0.04, x), guarda), lamina);
}

float4 Pixels(VertexOut input) : COLOR0
{
    float2 uv = input.TexCoord;
    float t = globalTime;
    float y = uv.y - 0.5;
    float w = Perfil(uv.x);
    float dentro = smoothstep(w + 0.01, w - 0.01, abs(y));
    // metal: escuro nas bordas, claro no fio do meio (aresta da lâmina)
    float fio = 1.0 - saturate(abs(y) / max(w, 0.001));
    float3 metal = lerp(cor * 0.35, lerp(cor, float3(1, 1, 1), 0.7), pow(fio, 1.5));
    // faixas da haste
    metal *= uv.x < 0.6 ? 0.92 + 0.08 * step(0.5, frac(uv.x * 10.0)) : 1.0;
    // reflexo correndo do cabo para a ponta
    float reflexo = exp(-pow((uv.x - frac(t * 0.9)) * 18.0, 2.0)) * fio;
    metal += reflexo * 1.2;
    // ponta acesa
    metal += float3(1, 1, 1) * smoothstep(0.9, 1.0, uv.x) * dentro * 0.8;

    float aura = exp(-max(abs(y) - w, 0.0) * 22.0) * (1.0 - dentro);
    aura *= 0.55 + 0.45 * tex2D(texturaRuido, float2(uv.x * 3.0 - t * 2.0, y * 2.0)).r;
    float3 c = metal * dentro + cor * aura * 1.1;
    c = lerp(c, float3(1, 1, 1), saturate(intensidade - 1.0) * dentro);
    float a = saturate(dentro + aura);
    a *= smoothstep(1.0, 0.8, abs(input.TexCoord.y - 0.5) * 2.0); // nunca uma borda reta no limite da faixa
    return float4(c, a) * a * opacidade * input.Color.a;
}

technique Tecnica1
{
    pass AutoloadPass
    {
        VertexShader = compile vs_3_0 Vertices();
        PixelShader = compile ps_3_0 Pixels();
    }
}
