Shader "PortalGun/Shot" {
 Properties { _Color("Color",Color)=(0,0.5,1,1) }
 SubShader { Tags {"Queue"="Transparent" "RenderType"="Transparent"} Blend SrcAlpha One ZWrite Off Cull Off Pass {
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #pragma multi_compile __ STEREO_INSTANCING_ON UNITY_SINGLE_PASS_STEREO
 #include "UnityCG.cginc"
 struct appdata {float4 vertex:POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct v2f {float4 pos:SV_POSITION; UNITY_VERTEX_OUTPUT_STEREO};
 float4 _Color;
 v2f vert(appdata v){v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_OUTPUT(v2f,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.pos=UnityObjectToClipPos(v.vertex);return o;}
 fixed4 frag(v2f i):SV_Target {UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);return _Color;}
 ENDCG
 }}
}
