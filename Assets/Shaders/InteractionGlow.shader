Shader "Custom/InteractionGlow"
{
    Properties
    {
        [HDR] _GlowColor ("Glow Color", Color) = (1.0, 0.75, 0.2, 1.0)
        _GlowIntensity ("Glow Intensity", Range(0, 5)) = 2.0
        _OutlineWidth ("Outline Width", Range(0, 0.08)) = 0.015
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 2.5

        [Header(Bottom Up Fade)]
        _FadeHeight ("Fade Height", Range(0, 1)) = 0.6
        _FadeSoftness ("Fade Softness", Range(0.01, 1)) = 0.3
        _BottomBoost ("Bottom Brightness Boost", Range(1, 5)) = 2.0

        [Header(Pulse)]
        _PulseSpeed ("Pulse Speed", Range(0.1, 5)) = 1.2
        _PulseMin ("Pulse Min", Range(0, 1)) = 0.5
        _PulseMax ("Pulse Max", Range(0, 1)) = 1.0

        [Header(Object Bounds)]
        _BoundsMinY ("Bounds Min Y (object space)", Float) = 0.0
        _BoundsMaxY ("Bounds Max Y (object space)", Float) = 2.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+10"
        }

        Pass
        {
            Name "GoldenEdgeGlow"
            Tags { "LightMode" = "UniversalForward" }

            Cull Front
            ZWrite Off
            Blend One One

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _GlowColor;
                half   _GlowIntensity;
                float  _OutlineWidth;
                half   _FresnelPower;
                half   _FadeHeight;
                half   _FadeSoftness;
                half   _BottomBoost;
                half   _PulseSpeed;
                half   _PulseMin;
                half   _PulseMax;
                float  _BoundsMinY;
                float  _BoundsMaxY;
            CBUFFER_END

            struct Attributes
            {
                float4 posOS  : POSITION;
                float3 normOS : NORMAL;
            };

            struct Varyings
            {
                float4 posCS  : SV_POSITION;
                float3 normWS : TEXCOORD0;
                float3 viewWS : TEXCOORD1;
                float  heightT : TEXCOORD2;
            };

            Varyings vert(Attributes IN)
            {
                Varyings o;
                float3 expanded = IN.posOS.xyz + IN.normOS * _OutlineWidth;
                o.posCS  = TransformObjectToHClip(expanded);
                o.normWS = TransformObjectToWorldNormal(IN.normOS);
                float3 wPos = TransformObjectToWorld(expanded);
                o.viewWS = normalize(GetWorldSpaceViewDir(wPos));

                // normalise object-space Y to 0..1 across bounds
                o.heightT = saturate((IN.posOS.y - _BoundsMinY) / max(_BoundsMaxY - _BoundsMinY, 0.001));
                return o;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // fresnel edge glow
                half NdotV = saturate(dot(normalize(IN.normWS), normalize(IN.viewWS)));
                half fresnel = pow(1.0 - NdotV, _FresnelPower);

                // bottom-up fade: strong at bottom, fades to nothing at top
                half heightFade = 1.0 - smoothstep(_FadeHeight - _FadeSoftness, _FadeHeight + _FadeSoftness, IN.heightT);

                // extra brightness boost at the very bottom
                half bottomGlow = saturate(1.0 - IN.heightT * 3.0) * _BottomBoost;

                // pulse
                half pulse = lerp(_PulseMin, _PulseMax,
                    sin(_Time.y * _PulseSpeed * 6.2832) * 0.5 + 0.5);

                half intensity = (fresnel + bottomGlow) * heightFade * pulse * _GlowIntensity;

                return half4(_GlowColor.rgb * intensity, intensity);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
