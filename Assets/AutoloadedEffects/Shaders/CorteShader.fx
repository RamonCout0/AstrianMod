// CORTE: um rasgo no ar. Linha central quente e fina, a ferida escura em volta com rachaduras,
// e lascas de luz correndo pelo fio. Pontas bem afiadas. s1 = TurbulentNoise, s2 = DendriticNoise.
// Luminance registra como "AscensaoAstral.CorteShader".

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
    float d = abs(uv.y - 0.5) * 2.0;                       // 0 meio, 1 borda da faixa toda
    float afina = lerp(0.15, 1.0, pow(sin(uv.x * 3.14159), 0.6));
    float dl = d / afina;                                 // distância "afinada" nas pontas

    float n = tex2D(texturaRuido, float2(uv.x * 3.0 - t * 2.5, uv.y * 0.6)).r;
    float rach = tex2D(texturaRuido2, float2(uv.x * 2.2 + t * 0.3, uv.y * 1.5)).r;

    // o fio: bem fino, tremendo
    float fio = exp(-pow(dl * (9.0 + 3.0 * n), 2.0));
    // a ferida: miolo escuro-vermelho com borda irregular
    float ferida = smoothstep(0.55, 0.15, dl + (n - 0.5) * 0.35);
    // rachaduras claras dentro da ferida
    float r = pow(saturate(1.0 - abs(rach - 0.5) * 6.0), 3.0) * ferida;
    // lascas correndo
    float lasca = pow(saturate(sin(uv.x * 40.0 - t * 30.0 + n * 6.0)), 20.0) * smoothstep(0.35, 0.0, dl);

    float3 c = cor * 0.25 * ferida + cor * r * 1.4 + lerp(cor, float3(1, 1, 1), 0.75) * (fio * 1.4 + lasca);
    float halo = Brilho(dl, 0.35) * (1.0 - ferida);
    c += cor * halo;
    c = lerp(c, float3(1, 1, 1) * (fio + ferida), saturate(intensidade - 1.0) * 0.7);
    float a = saturate(ferida * 0.75 + fio + halo);
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
