#ifndef BOKE_SPRITE_HIGHLIGHT_COMMON_INCLUDED
#define BOKE_SPRITE_HIGHLIGHT_COMMON_INCLUDED

CBUFFER_START(UnityPerMaterial)
    float4 _Color;
    float4 _OutlineColor;
    float _OutlineWidth;
    float _OutlineIntensity;
    float _Highlight;
    float _PulseSpeed;
    float _PulseAmount;
CBUFFER_END

TEXTURE2D(_MainTex);
SAMPLER(sampler_MainTex);
float4 _MainTex_TexelSize;
TEXTURE2D(_AlphaTex);
SAMPLER(sampler_AlphaTex);

#ifdef UNITY_INSTANCING_ENABLED
    UNITY_INSTANCING_BUFFER_START(PerDrawSprite)
        UNITY_DEFINE_INSTANCED_PROP(float4, _RendererColor)
        UNITY_DEFINE_INSTANCED_PROP(float2, _Flip)
    UNITY_INSTANCING_BUFFER_END(PerDrawSprite)

    #define _RendererColor UNITY_ACCESS_INSTANCED_PROP(PerDrawSprite, _RendererColor)
    #define _Flip UNITY_ACCESS_INSTANCED_PROP(PerDrawSprite, _Flip)
#else
    float4 _RendererColor;
    float2 _Flip;
#endif

float _EnableExternalAlpha;

struct Attributes
{
    float4 positionOS : POSITION;
    float4 color : COLOR;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    float4 positionCS : SV_POSITION;
    float4 color : COLOR;
    float2 uv : TEXCOORD0;
    UNITY_VERTEX_OUTPUT_STEREO
};

float4 UnityFlipSprite(float3 pos, float2 flip)
{
    return float4(pos.xy * flip, pos.z, 1.0);
}

Varyings SpriteVert(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    float4 pos = UnityFlipSprite(input.positionOS.xyz, _Flip);
    output.positionCS = TransformObjectToHClip(pos.xyz);
    output.uv = input.uv;
    output.color = input.color * _Color * _RendererColor;
    return output;
}

float4 SampleSpriteTexture(float2 uv)
{
    float4 color = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv);
#if ETC1_EXTERNAL_ALPHA
    float4 alpha = SAMPLE_TEXTURE2D(_AlphaTex, sampler_AlphaTex, uv);
    color.a = lerp(color.a, alpha.r, _EnableExternalAlpha);
#endif
    return color;
}

float SampleSpriteAlpha(float2 uv)
{
    return SampleSpriteTexture(uv).a;
}

// Alpha edge: outer rim (transparent next to opaque) + inner rim.
float SampleEdgeMask(float2 uv, float centerAlpha)
{
    float2 texel = _MainTex_TexelSize.xy * max(0.5, _OutlineWidth);

    float aR  = SampleSpriteAlpha(uv + float2( texel.x, 0));
    float aL  = SampleSpriteAlpha(uv + float2(-texel.x, 0));
    float aU  = SampleSpriteAlpha(uv + float2(0,  texel.y));
    float aD  = SampleSpriteAlpha(uv + float2(0, -texel.y));
    float aRU = SampleSpriteAlpha(uv + float2( texel.x,  texel.y));
    float aLU = SampleSpriteAlpha(uv + float2(-texel.x,  texel.y));
    float aRD = SampleSpriteAlpha(uv + float2( texel.x, -texel.y));
    float aLD = SampleSpriteAlpha(uv + float2(-texel.x, -texel.y));

    float neighborMax = max(max(max(aR, aL), max(aU, aD)), max(max(aRU, aLU), max(aRD, aLD)));
    float neighborMin = min(min(min(aR, aL), min(aU, aD)), min(min(aRU, aLU), min(aRD, aLD)));

    float outer = saturate(neighborMax - centerAlpha);
    float inner = saturate(centerAlpha - neighborMin) * centerAlpha;
    return saturate(max(outer, inner));
}

float4 SpriteFrag(Varyings input) : SV_Target
{
    float4 tex = SampleSpriteTexture(input.uv);
    float4 color = tex * input.color;

    float highlightOn = saturate(_Highlight);
    if (highlightOn > 0.001)
    {
        float edge = SampleEdgeMask(input.uv, tex.a);
        float pulse = 1.0 + sin(_Time.y * _PulseSpeed) * _PulseAmount;
        float outlineStrength = edge * highlightOn * _OutlineIntensity * pulse;

        // Premultiply-friendly: add outline RGB, boost alpha so outer rim is visible.
        float3 outlineRgb = _OutlineColor.rgb * outlineStrength * _OutlineColor.a;
        color.rgb = color.rgb * color.a + outlineRgb;
        color.a = saturate(max(color.a, outlineStrength * _OutlineColor.a));
        return color;
    }

    color.rgb *= color.a;
    return color;
}

#endif
