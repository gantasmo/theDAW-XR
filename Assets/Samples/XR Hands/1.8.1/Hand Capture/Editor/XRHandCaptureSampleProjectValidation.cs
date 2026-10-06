using System;
using System.IO;
using System.Collections.Generic;
using Unity.XR.CoreUtils.Editor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.PackageManager.UI;
using UnityEditor.XR.Hands.ProjectValidation;
using UnityEngine;
#if TEXT_MESH_PRO_PRESENT || (UGUI_2_0_PRESENT && UNITY_6000_0_OR_NEWER)
using TMPro;
#endif


namespace UnityEditor.XR.Hands.Samples.Capture
{
    /// <summary>
    /// Unity Editor class which registers Project Validation rules for the XR Hand Capture sample,
    /// checking that other required samples and packages are installed.
    /// </summary>
    static class XRHandCaptureSampleProjectValidation
    {
        const string k_SampleDisplayName = "XR Hand Capture Sample";
        const string k_ProjectValidationSettingsPath = "Project/XR Plug-in Management/Project Validation";
        const string k_HandsPackageDisplayName = "XR Hands";
        const string k_HandVisualizerSampleName = "HandVisualizer";
        const string k_HandsPackageName = "com.unity.xr.hands";
        static readonly PackageVersion s_MinimumPackageVersion = new PackageVersion("1.7.0");

        const string k_XRIPackageDisplayName = "XR Interaction Toolkit";
        const string k_StarterAssetsSampleName = "Starter Assets";
        const string k_HandsInteractionDemoSampleName = "Hands Interaction Demo";
        const string k_SpatialKeyboardSampleName = "Spatial Keyboard";
        const string k_XRIPackageName = "com.unity.xr.interaction.toolkit";
        static readonly PackageVersion s_MinimumXRIPackageVersion = new PackageVersion("3.0.7");
        static readonly PackageVersion s_RecommendedXRIPackageVersion = new PackageVersion("3.2.0");
        static AddRequest s_XRIPackageAddRequest;

#if UNITY_6000_0_OR_NEWER
        static readonly PackageVersion s_MinimumUIPackageVersion = new PackageVersion("2.0.0");
        const string k_UIPackageName = "com.unity.ugui";
        const string k_UIPackageDisplayName = "Unity UI";
#else
        static readonly PackageVersion s_MinimumUIPackageVersion = new PackageVersion("3.0.8");
        const string k_UIPackageName = "com.unity.textmeshpro";
        const string k_UIPackageDisplayName = "TextMeshPro";
#endif
        static AddRequest s_UIPackageAddRequest;

        static readonly BuildTargetGroup[] s_SupportedBuildTargets =
        {
            BuildTargetGroup.Standalone,
            BuildTargetGroup.Android
        };

        static readonly List<BuildValidationRule> s_BuildValidationRules = new List<BuildValidationRule>
        {
            // Hand Visualizer sample from XR Hands
            new BuildValidationRule
            {
                IsRuleEnabled = () => PackageVersionUtility.GetPackageVersion(k_HandsPackageName) >= s_MinimumPackageVersion,
                Message = $"[{k_SampleDisplayName}] {k_HandVisualizerSampleName} sample from XR Hands package ({k_HandsPackageName}) must be imported or updated to use this sample.",
                Category = k_HandsPackageDisplayName,
                CheckPredicate = () => ProjectValidationUtility.SampleImportMeetsMinimumVersion(
                    k_HandsPackageDisplayName, k_HandVisualizerSampleName, PackageVersionUtility.GetPackageVersion(k_HandsPackageName)),
                FixIt = () =>
                {
                    if (TryFindSample(k_HandsPackageName, string.Empty, k_HandVisualizerSampleName, out var sample))
                    {
                        sample.Import(Sample.ImportOptions.OverridePreviousImports);
                    }
                },
                FixItAutomatic = true,
                Error = !ProjectValidationUtility.HasSampleImported(k_HandsPackageDisplayName, k_HandVisualizerSampleName),
            },
            // XRI package
            new BuildValidationRule
            {
                IsRuleEnabled = () => s_XRIPackageAddRequest == null || s_XRIPackageAddRequest.IsCompleted,
                Message = $"[{k_SampleDisplayName}] XRI package ({k_XRIPackageName}) must be installed or updated to use this sample.",
                Category = k_HandsPackageDisplayName,
                CheckPredicate = () => PackageVersionUtility.GetPackageVersion(k_XRIPackageName) >= s_MinimumXRIPackageVersion,
                FixIt = () =>
                {
                    ProjectValidationUtility.TryInstallPackage(k_XRIPackageName, s_RecommendedXRIPackageVersion, ref s_XRIPackageAddRequest);
                    if(s_XRIPackageAddRequest.Error != null)
                    {
                        Debug.LogError($"Package installation error: {s_XRIPackageAddRequest.Error}: {s_XRIPackageAddRequest.Error.message}");
                    }
                },
                FixItAutomatic = true,
                Error = true,
            },
            // Starter Assets sample from XRI
            new BuildValidationRule
            {
                Message = $"[{k_SampleDisplayName}] {k_StarterAssetsSampleName} sample from XR Interaction Toolkit ({k_XRIPackageName}) package must be imported or updated to use this sample.",
                Category = k_HandsPackageDisplayName,
                CheckPredicate = () => ProjectValidationUtility.SampleImportMeetsMinimumVersion(
                    k_XRIPackageDisplayName, k_StarterAssetsSampleName, PackageVersionUtility.GetPackageVersion(k_XRIPackageName)),
                FixIt = () =>
                {
                    if (TryFindSample(k_XRIPackageName, string.Empty, k_StarterAssetsSampleName, out var sample))
                    {
                        sample.Import(Sample.ImportOptions.OverridePreviousImports);
                    }
                },
                FixItAutomatic = true,
                Error = !ProjectValidationUtility.HasSampleImported(k_XRIPackageDisplayName, k_StarterAssetsSampleName),
            },
            // Hands Interaction Demo sample from XRI
            new BuildValidationRule
            {
                Message = $"[{k_SampleDisplayName}] {k_HandsInteractionDemoSampleName} sample from XR Interaction Toolkit ({k_XRIPackageName}) package must be imported or updated to use this sample.",
                Category = k_HandsPackageDisplayName,
                CheckPredicate = () => ProjectValidationUtility.SampleImportMeetsMinimumVersion(
                    k_XRIPackageDisplayName, k_HandsInteractionDemoSampleName, PackageVersionUtility.GetPackageVersion(k_XRIPackageName)),
                FixIt = () =>
                {
                    if (TryFindSample(k_XRIPackageName, string.Empty, k_HandsInteractionDemoSampleName, out var sample))
                    {
                        sample.Import(Sample.ImportOptions.OverridePreviousImports);
                    }
                },
                FixItAutomatic = true,
                Error = !ProjectValidationUtility.HasSampleImported(k_XRIPackageDisplayName, k_HandsInteractionDemoSampleName),
            },

            new BuildValidationRule
            {
                IsRuleEnabled = () => s_UIPackageAddRequest == null || s_UIPackageAddRequest.IsCompleted,
                Message = $"[{k_SampleDisplayName}] {k_UIPackageDisplayName} ({k_UIPackageName}) package must be installed and at minimum version {s_MinimumUIPackageVersion}.",
                Category = k_HandsPackageDisplayName,
                CheckPredicate = () => PackageVersionUtility.GetPackageVersion(k_UIPackageName) >= s_MinimumUIPackageVersion,
                FixIt = () =>
                {
                    ProjectValidationUtility.TryInstallPackage(k_UIPackageName, s_MinimumUIPackageVersion, ref s_UIPackageAddRequest);
                    if(s_UIPackageAddRequest.Error != null)
                    {
                        Debug.LogError($"Package installation error: {s_UIPackageAddRequest.Error}: {s_UIPackageAddRequest.Error.message}");
                    }
                },
                FixItAutomatic = true,
                Error = true,
            },
#if TEXT_MESH_PRO_PRESENT || (UGUI_2_0_PRESENT && UNITY_6000_0_OR_NEWER)
            new BuildValidationRule
            {
                IsRuleEnabled = () => PackageVersionUtility.IsPackageInstalled(k_UIPackageName),
                Message = $"[{k_SampleDisplayName}] TextMesh Pro - TMP Essentials must be installed for this sample.",
                HelpText = "Can be installed using Window > TextMeshPro > Import TMP Essential Resources or by clicking this Edit button and then Import TMP Essentials in the window that appears.",
                Category = k_HandsPackageDisplayName,
                CheckPredicate = () => PackageVersionUtility.IsPackageInstalled(k_UIPackageName) && TextMeshProEssentialsInstalled(),
                FixIt = () =>
                {
                    TMP_PackageResourceImporterWindow.ShowPackageImporterWindow();
                },
                FixItAutomatic = false,
                Error = true,
            },
#endif
            // Spatial Keyboard sample from XRI
            new BuildValidationRule
            {
                Message = $"[{k_SampleDisplayName}] {k_SpatialKeyboardSampleName} sample from XR Interaction Toolkit ({k_XRIPackageName}) package must be imported or updated to use this sample.",
                Category = k_HandsPackageDisplayName,
                CheckPredicate = () => ProjectValidationUtility.SampleImportMeetsMinimumVersion(
                    k_XRIPackageDisplayName, k_SpatialKeyboardSampleName, PackageVersionUtility.GetPackageVersion(k_XRIPackageName)),
                FixIt = () =>
                {
                    if (TryFindSample(k_XRIPackageName, string.Empty, k_SpatialKeyboardSampleName, out var sample))
                    {
                        sample.Import(Sample.ImportOptions.OverridePreviousImports);
                    }
                },
                FixItAutomatic = true,
                Error = !ProjectValidationUtility.HasSampleImported(k_XRIPackageDisplayName, k_SpatialKeyboardSampleName),
            }
        };

        [InitializeOnLoadMethod]
        static void RegisterProjectValidationRules()
        {
            // Delay evaluating conditions for issues to give time for Package Manager and UPM cache to fully initialize.
            EditorApplication.delayCall += AddRulesAndRunCheck;
        }

        static void AddRulesAndRunCheck()
        {
            foreach (var buildTargetGroup in s_SupportedBuildTargets)
            {
                BuildValidator.AddRules(buildTargetGroup, s_BuildValidationRules);
            }

            ShowWindowIfIssuesExist();
        }

        static void ShowWindowIfIssuesExist()
        {
            foreach (var validation in s_BuildValidationRules)
            {
                if (validation.CheckPredicate == null || !validation.CheckPredicate.Invoke())
                {
                    ShowWindow();
                    return;
                }
            }
        }

        static void ShowWindow()
        {
            // Delay opening the window since sometimes other settings in the player settings provider redirect to the
            // project validation window causing serialized objects to be nullified.
            EditorApplication.delayCall += () =>
            {
                SettingsService.OpenProjectSettings(k_ProjectValidationSettingsPath);
            };
        }

        static bool TryFindSample(string packageName, string packageVersion, string sampleDisplayName, out Sample sample)
        {
            sample = default;

            if (!PackageVersionUtility.IsPackageInstalled(packageName))
                return false;

            IEnumerable<Sample> packageSamples;
            try
            {
                packageSamples = Sample.FindByPackage(packageName, packageVersion);
            }
            catch (Exception e)
            {
                Debug.LogError($"Couldn't find samples of the {ToString(packageName, packageVersion)} package; aborting project validation rule. Exception: {e}");
                return false;
            }

            if (packageSamples == null)
            {
                Debug.LogWarning($"Couldn't find samples of the {ToString(packageName, packageVersion)} package; aborting project validation rule.");
                return false;
            }

            foreach (var packageSample in packageSamples)
            {
                if (packageSample.displayName == sampleDisplayName)
                {
                    sample = packageSample;
                    return true;
                }
            }

            Debug.LogWarning($"Couldn't find {sampleDisplayName} sample in the {ToString(packageName, packageVersion)} package; aborting project validation rule.");
            return false;
        }

        static bool TextMeshProEssentialsInstalled()
        {
            // Matches logic in Project Settings window, see TMP_PackageResourceImporter.cs.
            // For simplicity, we don't also copy the check if the asset needs to be updated.
            return File.Exists("Assets/TextMesh Pro/Resources/TMP Settings.asset");
        }

        static string ToString(string packageName, string packageVersion)
        {
            return string.IsNullOrEmpty(packageVersion) ? packageName : $"{packageName}@{packageVersion}";
        }
    }
}
