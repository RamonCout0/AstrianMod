// GELO: cristal. Facetas (o ruído é "fatiado" em degraus = planos de cristal), arestas brancas onde as
// facetas se encontram, interior translúcido azulado e brilhos que piscam. Borda dura de cristal + bruma fria em volta.
// s1 = DendriticNoise, s2 = WavyBlotchNoise.
// Luminance registra como "AscensaoAstral.GeloShader".

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

float4 Pixels(VertexOut input) : COLOR0
{
    float2 uv = input.TexCoord;
    float t = globalTime;
    float d = abs(uv.y - 0.5) * 2.0;
    float afina = lerp(0.35, 1.0, Pontas(uv.x, 0.08));
    float dl = d / afina;

    float2 p = float2(uv.x * 5.0, uv.y * 1.6);
    float n = tex2D(texturaRuido2, p * 0.35).r;
    float face = floor(n * 7.0) / 7.0;                     // facetas
    float aresta = saturate(1.0 - abs(frac(n * 7.0) - 0.5) * 2.0);
    aresta = pow(aresta, 14.0);
    float veio = pow(tex2D(texturaRuido, p * 0.25 + float2(t * 0.02, 0)).r, 3.0);

    float corpo = step(dl, 0.5 + (n - 0.5) * 0.12);       // borda dura e irregular, de cristal
    float3 dentro = lerp(cor * 0.35, lerp(cor, float3(1, 1, 1), 0.5), face) + float3(1, 1, 1) * aresta * 0.8 + cor * veio * 0.6;
    // reflexo: uma faixa clara atravessando o cristal
    float reflexo = exp(-pow((uv.x - frac(t * 0.35)) * 12.0 - (uv.y - 0.5) * 3.0, 2.0)) * 0.7;
    dentro += reflexo;
    // brilhinhos
    float brilho = tex2D(texturaRuido, uv * float2(9.0, 3.0) + floor(t * 4.0) * 0.137).r;
    dentro += pow(brilho, 18.0) * 6.0;

    float bruma = Brilho(dl, 0.4) * (1.0 - corpo) * (0.7 + 0.3 * tex2D(texturaRuido2, uv * 2.0 + t * 0.1).r);
    float3 c = dentro * corpo + cor * bruma;
    c = lerp(c, float3(1, 1, 1), saturate(intensidade - 1.0) * 0.6 * corpo);
    float a = saturate(corpo * 0.85 + bruma);
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
