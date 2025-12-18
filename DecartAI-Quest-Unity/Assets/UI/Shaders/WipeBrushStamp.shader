Shader "Hidden/WipeBrushStamp"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
        _BrushPos ("Brush Position", Vector) = (0,0,0,0)
        _BrushSize ("Brush Size", Float) = 0.05
        _FadeSpeed ("Fade Speed", Float) = 0.5
        _DebugFillWhite ("Debug Fill White", Float) = 0
    }
    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        // Pass 0: Fade
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;
            float _FadeSpeed;

            fixed4 frag (v2f i) : SV_Target {
                fixed4 col = tex2D(_MainTex, i.uv);
                // Reduce alpha/intensity over time
                return saturate(col - _FadeSpeed * unity_DeltaTime.x);
            }
            ENDCG
        }

        // Pass 1: Stamp
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            v2f vert (appdata v) {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                return o;
            }

            sampler2D _MainTex;
            float4 _BrushPos; // xy = UV position
            float _BrushSize;
            float _DebugFillWhite;

            fixed4 frag (v2f i) : SV_Target {
                // Debug mode: just fill with white to verify pass is being called
                if (_DebugFillWhite > 0.5) {
                    return fixed4(1,1,1,1);
                }
                
                fixed4 existing = tex2D(_MainTex, i.uv);
                float dist = distance(i.uv, _BrushPos.xy);
                
                // Much larger brush for visibility (size is full diameter)
                float innerRadius = _BrushSize * 0.3;
                float outerRadius = _BrushSize;
                float stamp = 1.0 - smoothstep(innerRadius, outerRadius, dist);
                
                // Strong accumulation
                return saturate(existing + stamp);
            }
            ENDCG
        }
    }
}
