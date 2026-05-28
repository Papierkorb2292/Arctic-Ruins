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
        }
        catch(Exception e)
        {
            Debug.LogWarning(e);
        }
    }
}
