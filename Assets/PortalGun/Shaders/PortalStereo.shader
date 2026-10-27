Shader "PortalGun/StereoLit" {
 Properties { _Color("Color",Color)=(1,1,1,1) _MainTex("Albedo",2D)="white"{} }
 SubShader { Tags {"RenderType"="Opaque"} Pass { Tags {"LightMode"="ForwardBase"}
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_fwdbase
 #pragma multi_compile_instancing
 #pragma multi_compile __ STEREO_INSTANCING_ON UNITY_SINGLE_PASS_STEREO
 #include "UnityCG.cginc"
 #include "Lighting.cginc"
 #include "AutoLight.cginc"
 struct appdata { float4 vertex:POSITION; float3 normal:NORMAL; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
 struct v2f { float4 pos:SV_POSITION; float3 normal:TEXCOORD0; float2 uv:TEXCOORD1; float3 worldPos:TEXCOORD2; SHADOW_COORDS(3) UNITY_VERTEX_OUTPUT_STEREO };
 sampler2D _MainTex; float4 _MainTex_ST, _Color;
 v2f vert(appdata v) {
  v2f o; UNITY_SETUP_INSTANCE_ID(v); UNITY_INITIALIZE_OUTPUT(v2f,o); UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
  o.pos=UnityObjectToClipPos(v.vertex); o.worldPos=mul(unity_ObjectToWorld,v.vertex).xyz;
  o.normal=UnityObjectToWorldNormal(v.normal); o.uv=TRANSFORM_TEX(v.uv,_MainTex); TRANSFER_SHADOW(o); return o;
 }
 fixed4 frag(v2f i):SV_Target {
  UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
  float3 n=normalize(i.normal);
  UNITY_LIGHT_ATTENUATION(atten,i,i.worldPos);
  float3 ambient=max(ShadeSH9(float4(n,1)),0);
  float3 direct=_LightColor0.rgb*saturate(dot(n,normalize(UnityWorldSpaceLightDir(i.worldPos))))*atten;
  return float4(tex2D(_MainTex,i.uv).rgb*_Color.rgb*(ambient+direct),1);
 }
 ENDCG
 }
 UsePass "Legacy Shaders/VertexLit/SHADOWCASTER"
 }
}
