// ÉTER: um rasgo para o vazio. O miolo é ESCURO (o vazio) com estrelas piscando dentro, a borda é roxa e
// brilhante, e a energia gira em espiral. s1 = TurbulentNoise, s2 = DendriticNoise.
// Luminance registra como "AscensaoAstral.EterShader".

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
    float afina = lerp(0.3, 1.0, Pontas(uv.x, 0.1));
    float dl = d / afina;
    float n = tex2D(texturaRuido, float2(uv.x * 2.0 + t * 0.4, uv.y * 0.9 - t * 0.2)).r;
    float giro = tex2D(texturaRuido2, float2(uv.x * 3.0 - t * 0.8 + uv.y * 1.5, uv.y * 2.0 + t * 0.3)).r;

    float corpo = smoothstep(0.52, 0.42, dl + (n - 0.5) * 0.18);
    float borda = exp(-pow((dl - 0.47 - (n - 0.5) * 0.18) * 14.0, 2.0));
    float vazio = smoothstep(0.42, 0.1, dl);
    // estrelas no vazio
    float2 ce = float2(uv.x * 22.0, uv.y * 6.0);
    float est = pow(tex2D(texturaRuido, ce * 0.09 + floor(t * 1.5) * 0.31).r, 28.0) * 14.0 * vazio;
    float3 roxo = cor;
    float3 c = float3(0.02, 0.0, 0.05) * corpo                         // o vazio
             + roxo * pow(giro, 3.0) * corpo * 1.2                       // energia girando dentro
             + lerp(roxo, float3(1, 1, 1), 0.4) * borda * 1.6            // a borda acesa
             + float3(0.9, 0.85, 1.0) * est;
    float halo = Brilho(dl, 0.4) * (1.0 - corpo);
    c += roxo * halo;
    c = lerp(c, float3(1, 1, 1), saturate(intensidade - 1.0) * (borda + corpo * 0.5));
    float a = saturate(corpo * 0.95 + borda + halo);
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
