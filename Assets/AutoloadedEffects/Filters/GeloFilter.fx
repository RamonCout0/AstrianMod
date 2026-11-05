// Filtro de TELA da nevasca (Raid da Silvanna): tudo fica azul-gelado e as bordas congelam.
// Registrado pela Luminance como "AscensaoAstral.GeloFilter".

sampler telaTex : register(s0);

float globalTime;
float opacity;      // fade automático da Luminance
float intensidade;  // 0 a 1, controlado pelo chefe

float4 Gelo(float2 coords : TEXCOORD0) : COLOR0
{
    float forca = opacity * intensidade;

    // Leve tremida horizontal do vento
    float2 uv = coords + float2(sin(coords.y * 40 + globalTime * 6) * 0.0015 * forca, 0);
    float3 cor = tex2D(telaTex, uv).rgb;

    // Tira parte da cor e puxa para o azul
    float brilho = dot(cor, float3(0.3, 0.59, 0.11));
    float3 frio = lerp(float3(0.10, 0.18, 0.32), float3(0.85, 0.95, 1.0), brilho);
    cor = lerp(cor, frio, 0.55 * forca);

    // Bordas "congeladas": esbranquiçadas
    float2 doCentro = coords - 0.5;
    float borda = smoothstep(0.35, 0.75, length(doCentro * float2(1.25, 1)));
    cor = lerp(cor, float3(0.9, 0.97, 1.0), borda * 0.45 * forca);

    return float4(cor, 1);
}

technique Tecnica1
{
    pass AutoloadPass
    {
        PixelShader = compile ps_3_0 Gelo();
    }
}
