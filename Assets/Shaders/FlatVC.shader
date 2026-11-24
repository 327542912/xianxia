Shader "Xianxia/FlatVC"
{
    // 低模平涂：顶点色即最终色，不打灯、不用 Lumen/Nanite 等价物。
    // 这样在 URP 与内置管线下都能渲染（SRPDefaultUnlit 通道），移动端只付一次 draw call。
    Properties
    {
        _Tint ("Tint", Color) = (1,1,1,1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "Queue"="Geometry" }
        LOD 100
        Pass
        {
            Cull Off
            ZWrite On
            ZTest LEqual
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            fixed4 _Tint;

            struct appdata
            {
                float4 vertex : POSITION;
                fixed4 color  : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos   : SV_POSITION;
                fixed4 color : COLOR;
            };

            v2f vert (appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                o.pos = UnityObjectToClipPos(v.vertex);
                o.color = v.color * _Tint;
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                return fixed4(i.color.rgb, 1.0);
            }
            ENDCG
        }
    }
    Fallback "Unlit/Color"
}