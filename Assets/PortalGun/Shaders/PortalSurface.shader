Shader "PortalGun/Surface" {
 Properties { _Color("Color",Color)=(0,0.5,1,1) _ViewLeft("Left",2D)="black"{} _ViewRight("Right",2D)="black"{} _HasView("Linked view",Float)=0 _Rectangular("Door opening",Float)=0 }
 SubShader { Tags {"RenderType"="Opaque"} Cull Off Pass {
 CGPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma multi_compile_instancing
 #pragma multi_compile __ STEREO_INSTANCING_ON UNITY_SINGLE_PASS_STEREO
 #include "UnityCG.cginc"
 struct appdata {float4 vertex:POSITION; float2 uv:TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct v2f {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;float4 screen:TEXCOORD1; UNITY_VERTEX_OUTPUT_STEREO};
 float4 _Color;float _HasView,_PortalCapture,_Rectangular;sampler2D _ViewLeft,_ViewRight;
 v2f vert(appdata v){v2f o;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_OUTPUT(v2f,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);o.pos=UnityObjectToClipPos(v.vertex);o.screen=ComputeNonStereoScreenPos(o.pos);o.uv=v.uv;return o;}
 fixed4 frag(v2f i):SV_Target {
 UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
 clip(.5-_PortalCapture);
 float2 edge=abs(i.uv*2-1);float r=lerp(length(edge),max(edge.x,edge.y),_Rectangular);clip(1-r);float ring=smoothstep(lerp(.85,.97,_Rectangular),lerp(.94,.995,_Rectangular),r);
 float wave=.05+.07*(.5+.5*sin(r*22-_Time.y*4));float2 uv=i.screen.xy/i.screen.w;
 fixed3 scene=tex2D(_ViewLeft,uv).rgb;
 #if defined(UNITY_STEREO_INSTANCING_ENABLED) || defined(UNITY_SINGLE_PASS_STEREO) || defined(UNITY_STEREO_MULTIVIEW_ENABLED)
 if(unity_StereoEyeIndex==1)scene=tex2D(_ViewRight,uv).rgb;
 #endif
 return float4(lerp(lerp(_Color.rgb*wave,scene,_HasView),_Color.rgb,ring),1);
 }
 ENDCG
 }}
}
