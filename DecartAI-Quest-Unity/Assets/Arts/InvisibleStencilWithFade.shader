Shader "Custom/FlexibleStencilLensWithFade"
{
    Properties
    {
        // 1. Color and Transparency
        _Color ("Tint Color", Color) = (0, 1, 1, 0.3) // Cyan, transparent
        
        // 2. The Stencil Key (Exposed to Inspector)
        [IntRange] _StencilID ("Stencil ID (Key)", Range(0, 255)) = 1
        
        // 3. Edge Fade Control
        _FadeWidth ("Edge Fade Width", Range(0, 1)) = 0.1 // Controls how wide the fade zone is at the edge
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
                float2 uv : TEXCOORD0; // UV coordinates for edge fade calculation
            };

            struct v2f {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0; // Pass UV to fragment shader
            };

            fixed4 _Color;
            float _FadeWidth;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv; // Pass UV coordinates through
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // Calculate distance from center (0.5, 0.5) in UV space
                float2 center = float2(0.5, 0.5);
                float distFromCenter = length(i.uv - center);
                
                // For a circle inscribed in a square UV space (0 to 1),
                // the radius is exactly 0.5.
                float radius = 0.5;
                
                // Create a fade that is:
                // - 1.0 (fully opaque) inside the fade zone
                // - Fades to 0.0 as it approaches the radius (0.5)
                float fade = 1.0 - smoothstep(radius - _FadeWidth, radius, distFromCenter);
                
                // Ensure anything beyond the radius is completely transparent/masked
                fade *= step(distFromCenter, radius);

                // --- DITHERING FOR SOFT STENCIL EDGE ---
                // We use screen-space dithering to decide whether to write to the stencil.
                // This simulates a soft edge for the objects revealed by the stencil.
                
                // 4x4 Bayer Dither Matrix
                static const float4x4 ditherMatrix = float4x4(
                    0.0625, 0.5625, 0.1875, 0.6875,
                    0.8125, 0.3125, 0.9375, 0.4375,
                    0.25,   0.75,   0.125,  0.625,
                    1.0,    0.5,    0.875,  0.375
                );

                // Get screen-space coordinates for dithering (in pixels)
                // SV_POSITION.xy provides the pixel coordinates in the fragment shader.
                uint2 pixelPos = (uint2)i.vertex.xy;
                float ditherValue = ditherMatrix[pixelPos.x % 4][pixelPos.y % 4];

                // If the fade value is less than the dither threshold, discard the fragment.
                // This prevents the stencil from being written at that pixel.
                if (fade < ditherValue) {
                    discard;
                }
                
                // Apply fade to the visual tint color as well
                fixed4 color = _Color;
                color.a *= fade;
                
                return color;
            }
            ENDCG
        }
    }
}
