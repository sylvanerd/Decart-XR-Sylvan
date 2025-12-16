Shader "Custom/FlexibleStencilLens"
{
    Properties
    {
        // 1. Color and Transparency
        _Color ("Tint Color", Color) = (0, 1, 1, 0.3) // Cyan, transparent
        
        // 2. The Stencil Key (Exposed to Inspector)
        [IntRange] _StencilID ("Stencil ID (Key)", Range(0, 255)) = 1
    }
    SubShader
    {
        // FORCE Early Rendering so it writes the key BEFORE the object reads it
        Tags { "RenderType"="Transparent" "Queue"="Geometry-1" }
        LOD 100

        Pass
        {
            // VISUAL SETUP
            Blend SrcAlpha OneMinusSrcAlpha // Allow transparency
            ZWrite Off                      // CRITICAL: Don't act like a solid wall
            
            // STENCIL LOGIC
            Stencil
            {
                Ref [_StencilID]   // Use the number from the Inspector
                Comp Always        // Always write it
                Pass Replace       // Replace the pixel buffer with the ID
            }

            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata {
                float4 vertex : POSITION;
            };

            struct v2f {
                float4 vertex : SV_POSITION;
            };

            fixed4 _Color;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return _Color; // Return the Tint Color
            }
            ENDCG
        }
    }
}