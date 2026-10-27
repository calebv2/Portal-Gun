using System.IO;
using UnityEditor;
using UnityEngine;
[InitializeOnLoad]
public static class PortalModExport
{
    static PortalModExport(){EditorApplication.delayCall+=Once;}
    static void Once(){if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating){EditorApplication.delayCall+=Once;return;}if(!File.Exists("Builds/PortalGun/export-complete.txt"))Export();}
    [MenuItem("Portal Gun/Export ATT Models")]
    public static void Export()
    {
        var shader=Shader.Find("PortalGun/StereoLit");if(shader==null)throw new System.Exception("Missing stereo shader");
        foreach(var guid in AssetDatabase.FindAssets("t:Material",new[]{"Assets/PortalGun/Materials"}))
        {
            var m=AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));var color=m.color;var texture=m.mainTexture;
            m.shader=shader;m.color=color;m.mainTexture=texture;m.shaderKeywords=new string[0];m.enableInstancing=true;EditorUtility.SetDirty(m);
        }
        AssetDatabase.SaveAssets();PlayerSettings.virtualRealitySupported=true;PlayerSettings.stereoRenderingPath=StereoRenderingPath.Instancing;
        Directory.CreateDirectory("Builds/PortalGun");
        var assets=new System.Collections.Generic.List<string>{"Assets/PortalGun/Prefabs/PortalGun.prefab","Assets/PortalGun/Shaders/PortalSurface.shader","Assets/PortalGun/Shaders/PortalShot.shader","Assets/PortalGun/Shaders/PortalSlice.shader"};
        foreach(var name in new[]{"PortalDoorA","PortalDoorB"}) { var path="Assets/PortalGun/Doorways/"+name+".prefab";if(!File.Exists(path))throw new System.Exception("Missing finished doorway "+name);assets.Add(path); }
        var manifest=BuildPipeline.BuildAssetBundles("Builds/PortalGun",new[]{new AssetBundleBuild{assetBundleName="portalgun-models",assetNames=assets.ToArray()}},BuildAssetBundleOptions.ChunkBasedCompression,BuildTarget.StandaloneWindows64);
        if(manifest==null)throw new System.Exception("Portal bundle export failed");
        File.WriteAllText("Builds/PortalGun/export-complete.txt","Portal gun model and stereo portal shader exported.");Debug.Log("PORTALGUN_BUNDLE_READY");
    }
}
