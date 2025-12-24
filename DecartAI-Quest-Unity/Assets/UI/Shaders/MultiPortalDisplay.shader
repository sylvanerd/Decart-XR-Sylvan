Shader "UI/MultiPortalDisplay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Video Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        
        _ParallaxStrength ("Parallax Strength", Float) = 0.02
        _ParallaxDepth ("Parallax Depth", Float) = 1.0
        
        _PortalCount ("Portal Count", Int) = 0
        _DefaultRadius ("Default Corner Radius", Float) = 0.02
        
        // Glow effect
        _GlowColor ("Glow Color", Color) = (0.3, 0.6, 1.0, 1.0)
        _GlowWidth ("Glow Width", Float) = 0.02
        _GlowIntensity ("Glow Intensity", Float) = 1.5

        // UI Stencil support
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15

        [Toggle(UNITY_UI_ALPHACLIP)] _UseUIAlphaClip ("Use Alpha Clip", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend One OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            Name "MultiPortal"
        CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            #include "Utils.cginc"
            
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #pragma multi_compile_local _ UNITY_UI_ALPHACLIP

            #define MAX_PORTALS 8

            struct appdata_t
            {
                float4 vertex   : POSITION;
                float4 color    : COLOR;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 vertex       : SV_POSITION;
                fixed4 color        : COLOR;
                float2 texcoord     : TEXCOORD0;
                float4 worldPosition : TEXCOORD1;
                float3 viewDir      : TEXCOORD2;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _TextureSampleAdd;
            float4 _ClipRect;
            float4 _MainTex_ST;
            
            float _ParallaxStrength;
            float _ParallaxDepth;
            
            // Portal data arrays
            float4 _PortalRects[MAX_PORTALS];  // xy = uvMin, zw = uvMax
            float _PortalRadii[MAX_PORTALS];   // Corner radius per portal
            int _PortalCount;
            float _DefaultRadius;
            
            // Glow
            fixed4 _GlowColor;
            float _GlowWidth;
            float _GlowIntensity;

            v2f vert(appdata_t v)
            {
                v2f OUT;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                
                float4 vPosition = UnityObjectToClipPos(v.vertex);
                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;

                OUT.worldPosition = v.vertex;
                OUT.vertex = vPosition;
                OUT.texcoord = TRANSFORM_TEX(v.texcoord.xy, _MainTex);
                OUT.color = v.color * _Color;
                
                // Calculate view direction for parallax
                float3 worldViewDir = normalize(UnityWorldSpaceViewDir(worldPos));
                OUT.viewDir = mul(unity_WorldToObject, float4(worldViewDir, 0)).xyz;

                return OUT;
            }

            // Sample video texture with zoom and edge reflection
            half4 sampleVideoTexture(sampler2D tex, float2 uv)
            {
                // Zoom into center
                uv = uv * 2 - 1;
                uv *= 0.7;
                uv = uv * 0.5 + 0.5;
                
                // Reflect UV coordinates when out of bounds
                float2 reflectedUV = uv;
                
                if (reflectedUV.x < 0.0) 
                    reflectedUV.x = -reflectedUV.x;
                else if (reflectedUV.x > 1.0) 
                    reflectedUV.x = 2.0 - reflectedUV.x;
                
                if (reflectedUV.y < 0.0) 
                    reflectedUV.y = -reflectedUV.y;
                else if (reflectedUV.y > 1.0) 
                    reflectedUV.y = 2.0 - reflectedUV.y;
                
                return tex2D(tex, reflectedUV);
            }

            // Calculate signed distance to a single portal's rounded rectangle
            // Returns negative values inside the portal
            float portalSDF(float2 uv, float4 portalRect, float radius)
            {
                float2 uvMin = portalRect.xy;
                float2 uvMax = portalRect.zw;
                
                // Convert UV to portal-local coordinates centered at portal center
                float2 center = (uvMin + uvMax) * 0.5;
                float2 halfSize = (uvMax - uvMin) * 0.5;
                float2 localPos = uv - center;
                
                // Clamp radius to not exceed half the smallest dimension
                float maxRadius = min(halfSize.x, halfSize.y);
                radius = min(radius, maxRadius);
                
                // SDF for rounded box
                float2 d = abs(localPos) - halfSize + radius;
                return min(max(d.x, d.y), 0.0) + length(max(d, 0.0)) - radius;
            }

            // Check if UV is inside any portal and return combined mask
            // Returns: x = inside mask (1 inside, 0 outside), y = min distance to any edge
            float2 getPortalMask(float2 uv)
            {
                float minDist = 1000.0;
                float insideMask = 0.0;
                
                for (int i = 0; i < MAX_PORTALS; i++)
                {
                    if (i >= _PortalCount) break;
                    
                    float4 rect = _PortalRects[i];
                    float radius = _PortalRadii[i];
                    
                    // Skip invalid portals
                    if (rect.z <= rect.x || rect.w <= rect.y) continue;
                    
                    float dist = portalSDF(uv, rect, radius);
                    minDist = min(minDist, dist);
                    
                    // Smooth edge for anti-aliasing
                    float pixelWidth = fwidth(dist) * 1.5;
                    float portalAlpha = 1.0 - smoothstep(-pixelWidth, pixelWidth, dist);
                    
                    // Combine portals (max for overlapping)
                    insideMask = max(insideMask, portalAlpha);
                }
                
                return float2(insideMask, minDist);
            }

            fixed4 frag(v2f IN) : SV_Target
            {
                // Get portal mask
                float2 maskData = getPortalMask(IN.texcoord);
                float insideMask = maskData.x;
                float minDist = maskData.y;
                
                // Early out if completely outside all portals
                if (insideMask < 0.001 && minDist > _GlowWidth)
                {
                    return fixed4(0, 0, 0, 0);
                }
                
                // Calculate parallax offset for depth effect
                float2 parallaxOffset = IN.viewDir.xy / (IN.viewDir.z + _ParallaxDepth) * _ParallaxStrength;
                float2 parallaxUV = IN.texcoord + parallaxOffset;
                
                // Sample video texture
                half4 videoColor = sampleVideoTexture(_MainTex, parallaxUV);
                videoColor = videoColor + _TextureSampleAdd;
                
                // Apply tint
                fixed4 color = IN.color * videoColor;
                
                // Apply portal mask
                color.a *= insideMask;
                
                // Add glow effect at portal edges
                float glowMask = smoothstep(_GlowWidth, 0.0, abs(minDist)) * (1.0 - insideMask * 0.5);
                fixed4 glow = _GlowColor * glowMask * _GlowIntensity;
                color.rgb += glow.rgb * glow.a;

                #ifdef UNITY_UI_CLIP_RECT
                half2 m = saturate((_ClipRect.zw - _ClipRect.xy - abs(IN.worldPosition.xy - (_ClipRect.xy + _ClipRect.zw) * 0.5) * 2) * 0.5);
                color.a *= m.x * m.y;
                #endif

                #ifdef UNITY_UI_ALPHACLIP
                clip(color.a - 0.001);
                #endif

                // Premultiply alpha for proper blending
                color.rgb *= color.a;

                return color;
            }
        ENDCG
        }
    }
}

