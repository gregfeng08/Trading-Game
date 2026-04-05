Shader "Custom/GlowSigil"
{
    Properties
    {
        [HDR] _Color ("Color", Color) = (1.0, 0.75, 0.2, 1.0)
        _Intensity ("Intensity", Range(0, 8)) = 3.0

        [Header(Square)]
        _BorderWidth ("Border Width", Range(0.005, 0.15)) = 0.04
        _BorderSoftness ("Border Softness", Range(0.001, 0.1)) = 0.02
        _InnerPadding ("Inner Padding", Range(0, 0.4)) = 0.08

        [Header(Corner Accents)]
        _CornerLength ("Corner Accent Length", Range(0, 0.3)) = 0.15
        _CornerWidth ("Corner Accent Width", Range(0.005, 0.1)) = 0.05

        [Header(Inner Glow)]
        _InnerGlow ("Inner Fill Glow", Range(0, 1)) = 0.08

        [Header(Pulse)]
        _PulseSpeed ("Pulse Speed", Range(0.1, 5)) = 1.2
        _PulseMin ("Pulse Min", Range(0, 1)) = 0.4
        _PulseMax ("Pulse Max", Range(0, 1)) = 1.0

        [Header(Rotation)]
        _RotSpeed ("Rotation Speed (deg/s)", Float) = 0.0
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent+5"
        }

        Pass
        {
            Name "Sigil"
            Tags { "LightMode" = "UniversalForward" }

            Cull Off
            ZWrite Off
            ZTest LEqual
            Blend One One
            Offset -1, -1

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4  _Color;
                half   _Intensity;
                half   _BorderWidth;
                half   _BorderSoftness;
                half   _InnerPadding;
                half   _CornerLength;
                half   _CornerWidth;
                half   _InnerGlow;
                half   _PulseSpeed;
                half   _PulseMin;
                half   _PulseMax;
                float  _RotSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 posOS : POSITION;
                float2 uv    : TEXCOORD0;
            };

            struct Varyings
            {
                float4 posCS : SV_POSITION;
                float2 uv    : TEXCOORD0;
            };

            Varyings vert(Attributes IN)
            {
                Varyings o;
                o.posCS = TransformObjectToHClip(IN.posOS.xyz);
                o.uv = IN.uv;
                return o;
            }

            // signed distance to an axis-aligned box centered at origin with half-size b
            float sdBox(float2 p, float2 b)
            {
                float2 d = abs(p) - b;
                return length(max(d, 0.0)) + min(max(d.x, d.y), 0.0);
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // centre UV at origin: -0.5 to 0.5
                float2 uv = IN.uv - 0.5;

                // optional slow rotation
                float angle = _Time.y * _RotSpeed * 0.01745; // deg to rad
                float c = cos(angle);
                float s = sin(angle);
                uv = float2(uv.x * c - uv.y * s, uv.x * s + uv.y * c);

                // ─── outer square border ───
                float halfSize = 0.5 - _InnerPadding;
                float dist = sdBox(uv, float2(halfSize, halfSize));

                // border ring: solid in the middle, soft on both edges
                float border = smoothstep(_BorderWidth + _BorderSoftness, _BorderWidth, abs(dist))
                             * smoothstep(halfSize + _BorderSoftness * 2.0, halfSize, length(uv) * 0.7 + abs(dist));

                // cleaner version: just a soft band around dist=0
                border = smoothstep(_BorderSoftness, 0.0, abs(dist) - _BorderWidth * 0.5)
                       * smoothstep(_BorderSoftness, 0.0, abs(dist) - _BorderWidth * 0.5);
                border = smoothstep(_BorderWidth + _BorderSoftness, _BorderWidth * 0.2, abs(dist));

                // ─── corner accents: thicker lines at the 4 corners ───
                float2 absUV = abs(uv);
                float atCornerX = smoothstep(halfSize, halfSize - _CornerLength, absUV.x);
                float atCornerY = smoothstep(halfSize, halfSize - _CornerLength, absUV.y);
                // only show accent when BOTH axes are near the corner
                float cornerMask = (1.0 - atCornerX) * (1.0 - atCornerY);

                float cornerDist = sdBox(uv, float2(halfSize, halfSize));
                float cornerAccent = smoothstep(_CornerWidth + _BorderSoftness, _CornerWidth * 0.2, abs(cornerDist))
                                   * cornerMask;

                // ─── soft inner fill ───
                float innerDist = sdBox(uv, float2(halfSize - _BorderWidth, halfSize - _BorderWidth));
                float inner = smoothstep(0.0, halfSize, -innerDist) * _InnerGlow;

                // ─── circular vignette fade at edges of quad ───
                float vignette = 1.0 - smoothstep(0.35, 0.52, length(uv));

                // ─── combine ───
                half combined = (border + cornerAccent + inner) * vignette;

                // pulse
                half pulse = lerp(_PulseMin, _PulseMax,
                    sin(_Time.y * _PulseSpeed * 6.2832) * 0.5 + 0.5);

                half final_intensity = combined * pulse * _Intensity;

                // fade out fully transparent pixels
                clip(final_intensity - 0.001);

                return half4(_Color.rgb * final_intensity, final_intensity);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
