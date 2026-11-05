// SOLAR: fogo/luz do sol. Chamas subindo pelo golpe (ruído rolando no sentido do golpe e "lambendo" as
// bordas), rampa de cor branco → amarelo → laranja → vermelho do miolo para fora, calor tremendo.
// s1 = TurbulentNoise, s2 = WavyBlotchNoise.
// Luminance registra como "AscensaoAstral.SolarShader".

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

float3 Rampa(float k)
{
    float3 c = lerp(float3(0.55, 0.08, 0.02), float3(1.0, 0.45, 0.08), saturate(k * 2.0));
    c = lerp(c, float3(1.0, 0.85, 0.35), saturate(k * 2.0 - 0.6));
    return lerp(c, float3(1, 1, 0.92), saturate(k * 3.0 - 2.0));
}

float4 Pixels(VertexOut input) : COLOR0
{
    float2 uv = input.TexCoord;
    float t = globalTime;
    float d = abs(uv.y - 0.5) * 2.0;
    // o ruído corre do fim para o começo da faixa = as chamas "sobem" (o C# desenha do chão para o céu)
    float n1 = tex2D(texturaRuido, float2(uv.x * 2.2 - t * 1.6, uv.y * 1.1)).r;
    float n2 = tex2D(texturaRuido2, float2(uv.x * 4.0 - t * 2.8, uv.y * 2.0 + n1 * 0.4)).r;
    float fogo = n1 * 0.6 + n2 * 0.5;
    float calor = saturate(1.0 - d * 1.9 + (fogo - 0.55) * 0.9) * smoothstep(0.8, 0.4, d);   // 1 no miolo, 0 fora
    float3 c = Rampa(calor) * calor * 1.3;
    // brasas soltas
    float brasa = pow(tex2D(texturaRuido, float2(uv.x * 7.0 - t * 3.5, uv.y * 4.0)).r, 12.0) * 5.0 * (1.0 - calor);
    c += float3(1.0, 0.6, 0.2) * brasa;
    float halo = Brilho(d, 0.45);
    c += cor * halo * 0.8;
    c = lerp(c, float3(1, 1, 0.95), saturate(intensidade - 1.0) * calor);
    c *= lerp(float3(1, 1, 1), cor * 1.2 + 0.2, 0.35);
    float a = saturate(calor * 1.2 + halo + brasa);
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
