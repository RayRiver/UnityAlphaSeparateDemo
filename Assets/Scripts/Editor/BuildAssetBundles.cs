using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class BuildAssetBundles 
{
    [MenuItem("Tools/Build Apk")]
    private static void DoBuildApk()
    {
        if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android)
        {
            return;
        }
        
        DoBuildAssetBundles();
        
        var scenes = new List<string>();
        foreach (EditorBuildSettingsScene scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled)
            {
                scenes.Add(scene.path);
            }
        }

        var outputDir = Application.dataPath + "/../Output";
        if (!Directory.Exists(outputDir))
        {
            Directory.CreateDirectory(outputDir);
        }
        
		var playerOptions = new BuildPlayerOptions();
        playerOptions.scenes = scenes.ToArray();
        playerOptions.locationPathName = outputDir + "/Game.apk";
		playerOptions.target = EditorUserBuildSettings.activeBuildTarget;
		playerOptions.options = BuildOptions.Development | BuildOptions.ConnectWithProfiler;

        BuildPipeline.BuildPlayer(playerOptions);
    }

    [MenuItem("Tools/Build Asset Bundles")]
    private static void DoBuildAssetBundles()
    {
        var abPath = Application.streamingAssetsPath;
        if (Directory.Exists(abPath))
        {
            Directory.Delete(abPath, true);
        }
        if (!Directory.Exists(abPath))
        {
            Directory.CreateDirectory(abPath);
        }
            
        var builds = new AssetBundleBuild[3];
        builds[0] = new AssetBundleBuild
        {
            assetBundleName = "textures_normal",
            assetNames = new []
            {
                "Assets/Textures/Normal/extended_arm_left.png",
                "Assets/Textures/Normal/extended_beard.png",
                "Assets/Textures/Normal/extended_body.png",
                "Assets/Textures/Normal/extended_ear.png",
                "Assets/Textures/Normal/extended_eye.png",
                "Assets/Textures/Normal/extended_feet_left.png",
                "Assets/Textures/Normal/extended_feet_right.png",
                "Assets/Textures/Normal/extended_hand_left.png",
                "Assets/Textures/Normal/extended_hand_right.png",
                "Assets/Textures/Normal/extended_hat.png",
                "Assets/Textures/Normal/extended_hat_character.png",
                "Assets/Textures/Normal/extended_head.png",
                "Assets/Textures/Normal/extended_nose.png",
            }
        };
        builds[1] = new AssetBundleBuild
        {
            assetBundleName = "textures_alphaseparated",
            assetNames = new []
            {
                "Assets/Textures/AlphaSeparated/extended_arm_left.png",
                "Assets/Textures/AlphaSeparated/extended_beard.png",
                "Assets/Textures/AlphaSeparated/extended_body.png",
                "Assets/Textures/AlphaSeparated/extended_ear.png",
                "Assets/Textures/AlphaSeparated/extended_eye.png",
                "Assets/Textures/AlphaSeparated/extended_feet_left.png",
                "Assets/Textures/AlphaSeparated/extended_feet_right.png",
                "Assets/Textures/AlphaSeparated/extended_hand_left.png",
                "Assets/Textures/AlphaSeparated/extended_hand_right.png",
                "Assets/Textures/AlphaSeparated/extended_hat.png",
                "Assets/Textures/AlphaSeparated/extended_hat_character.png",
                "Assets/Textures/AlphaSeparated/extended_head.png",
                "Assets/Textures/AlphaSeparated/extended_nose.png",
            }
        };
        builds[2] = new AssetBundleBuild
        {
            assetBundleName = "textures_alphaseparated_useoutput",
            assetNames = new []
            {
                "Assets/Textures/AlphaSeparatedUseOutput/extended_arm_left.png",
                "Assets/Textures/AlphaSeparatedUseOutput/extended_beard.png",
                "Assets/Textures/AlphaSeparatedUseOutput/extended_body.png",
                "Assets/Textures/AlphaSeparatedUseOutput/extended_ear.png",
                "Assets/Textures/AlphaSeparatedUseOutput/extended_eye.png",
                "Assets/Textures/AlphaSeparatedUseOutput/extended_feet_left.png",
                "Assets/Textures/AlphaSeparatedUseOutput/extended_feet_right.png",
                "Assets/Textures/AlphaSeparatedUseOutput/extended_hand_left.png",
                "Assets/Textures/AlphaSeparatedUseOutput/extended_hand_right.png",
                "Assets/Textures/AlphaSeparatedUseOutput/extended_hat.png",
                "Assets/Textures/AlphaSeparatedUseOutput/extended_hat_character.png",
                "Assets/Textures/AlphaSeparatedUseOutput/extended_head.png",
                "Assets/Textures/AlphaSeparatedUseOutput/extended_nose.png",
            }
        };
        
        AlphaSeparate.Perform(EditorUserBuildSettings.activeBuildTarget);
        BuildPipeline.BuildAssetBundles(abPath, builds, 
            BuildAssetBundleOptions.ChunkBasedCompression, EditorUserBuildSettings.activeBuildTarget); 
        AlphaSeparate.Revert();
    }
}