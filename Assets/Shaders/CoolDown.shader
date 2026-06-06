Shader "UI/CoolDown"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        _CoolDownColor ("CoolDown Color", Color) = (0.5, 0.5, 0.5, 0.8)
        _Progress ("Progress", Range(0, 1)) = 0
        _Direction ("Direction (1=CW, -1=CCW)", Float) = 1

        [HideInInspector] _StencilComp ("Stencil Comparison", Float) = 8
        [HideInInspector] _Stencil ("Stencil ID", Float) = 0
        [HideInInspector] _StencilOp ("Stencil Operation", Float) = 0
        [HideInInspector] _StencilWriteMask ("Stencil Write Mask", Float) = 255
        [HideInInspector] _StencilReadMask ("Stencil Read Mask", Float) = 255
        [HideInInspector] _ColorMask ("Color Mask", Float) = 15
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
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 color : COLOR;
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
                float4 color : COLOR;
            };

            sampler2D _MainTex;
            fixed4 _Color;
            fixed4 _CoolDownColor;
            float _Progress;
            float _Direction;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                // 采样原始纹理
                fixed4 texColor = tex2D(_MainTex, i.uv) * i.color;

                // 将 UV 坐标转换到中心为原点
                float2 centeredUV = i.uv - 0.5;

                // 计算角度（-PI 到 PI）
                float angle = atan2(centeredUV.y, centeredUV.x);

                // 转换到 0 到 2PI 范围
                // 从12点钟方向开始（顶部）
                float normalizedAngle = angle + 3.14159265;

                // 转换为 0-1 范围
                float angleProgress = normalizedAngle / (2.0 * 3.14159265);

                // 根据方向调整（顺时针/逆时针）
                if (_Direction < 0)
                    angleProgress = 1.0 - angleProgress;

                // 判断是否在冷却覆盖区域内
                // Progress 表示剩余冷却比例（1=刚开始，0=结束）
                float coolDownMask = step(_Progress, angleProgress);

                // 混合原始颜色和冷却颜色
                fixed4 finalColor;
                finalColor.rgb = lerp(_CoolDownColor.rgb, texColor.rgb, coolDownMask);
                finalColor.a = lerp(_CoolDownColor.a, texColor.a, coolDownMask);

                return finalColor;
            }
            ENDCG
        }
    }
}
