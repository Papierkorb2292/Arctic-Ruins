using System;
using UnityEditor;
using UnityEngine;

public class AssetBundleBuilder
{
    [MenuItem("Assets/Create Asset Bundle")]
    private static void BuildAssetBundle()
    {
        try
        {
            BuildPipeline.BuildAssetBundles(Application.dataPath + "/../../Resources/Windows", BuildAssetBundleOptions.None, BuildTarget.StandaloneWindows64);
            BuildPipeline.BuildAssetBundles(Application.dataPath + "/../../Resources/Mac", BuildAssetBundleOptions.None, BuildTarget.StandaloneOSX);
            BuildPipeline.BuildAssetBundles(Application.dataPath + "/../../Resources/Linux", BuildAssetBundleOptions.None, BuildTarget.StandaloneLinux64);
        }
        catch(Exception e)
        {
            Debug.LogWarning(e);
        }
    }
}
