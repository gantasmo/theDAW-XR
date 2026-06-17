#if UNITY_ANDROID
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor.Android;
using UnityEngine;

/// <summary>
/// Fixes applied to the GENERATED Gradle project on every Android build — after
/// Unity writes it and before Gradle runs. Each fix is idempotent, logs exactly
/// what it changed, and surfaces failures as console errors.
///
/// Nothing here mutes or suppresses output: it removes the *root causes* of the
/// errors/warnings in the generated project so they never get emitted.
///
///   1. SDKTelemetry.aar (Voice SDK) and OVRPlugin.aar (Core SDK) both declare
///      the namespace "com.oculus.Integration". AGP 9 requires unique namespaces
///      per library, so the manifest merge fails. SDKTelemetry.aar has no
///      resources / R class / manifest components, so renaming its namespace is
///      safe. The AAR is rebuilt as a valid, fully-DEFLATED zip on a seekable
///      stream (no data descriptors) so Jetifier/AGP can read it.
///   2. gradle.properties sets the deprecated android.enableJetifier=true and
///      android.builtInKotlin=false (removed in AGP 10). This project uses no
///      Kotlin and only AndroidX deps, so both lines are dropped -> AGP 9
///      defaults (jetifier off, builtInKotlin on).
///   3. xrmanifest.androidlib/AndroidManifest.xml declares package="..."; AGP
///      ignores a manifest package (the namespace is set in build.gradle) and
///      warns. The redundant attribute is removed.
///   4. The com.oculus.supportedDevices meta-data carries tools:replace, but no
///      lower-priority manifest declares it, so the merger warns. The redundant
///      tools:replace is removed; the value is left untouched.
/// </summary>
public class AndroidBuildFixes : IPostGenerateGradleAndroidProject
{
    // Run last so Meta's own post-generate processors can't re-introduce these.
    public int callbackOrder => 10000;

    public void OnPostGenerateGradleAndroidProject(string path)
    {
        // 'path' is the unityLibrary module (Unity 6); fall back gracefully if
        // it's ever the Gradle root instead.
        string gradleRoot = path;
        string leaf = Path.GetFileName(path.TrimEnd('/', '\\'));
        if (string.Equals(leaf, "unityLibrary", StringComparison.OrdinalIgnoreCase))
            gradleRoot = Directory.GetParent(path)?.FullName ?? path;

        FixSdkTelemetryNamespace(gradleRoot);
        FixGradleProperties(gradleRoot);
        FixXrManifestPackage(gradleRoot);
        FixSupportedDevicesToolsReplace(gradleRoot);
    }

    // ---- 1. AAR namespace collision ----------------------------------------
    const string OldNs = "com.oculus.Integration";
    const string NewNs = "com.oculus.sdktelemetry";

    static void FixSdkTelemetryNamespace(string gradleRoot)
    {
        foreach (var aar in SafeFiles(gradleRoot, "SDKTelemetry.aar"))
        {
            try
            {
                string manifest = ReadZipEntryText(aar, "AndroidManifest.xml");
                if (manifest == null) continue;
                if (manifest.IndexOf(OldNs, StringComparison.Ordinal) < 0) continue; // already unique

                string patched = manifest.Replace("\"" + OldNs + "\"", "\"" + NewNs + "\"");
                RebuildAarWithManifest(aar, patched);
                Debug.Log($"[AndroidBuildFixes] SDKTelemetry namespace -> {NewNs}: {aar}");
            }
            catch (Exception e)
            {
                Debug.LogError($"[AndroidBuildFixes] Failed to fix namespace in {aar}: {e}");
            }
        }
    }

    static string ReadZipEntryText(string zipPath, string entryName)
    {
        using (var z = ZipFile.OpenRead(zipPath))
        {
            var e = z.GetEntry(entryName);
            if (e == null) return null;
            using (var r = new StreamReader(e.Open(), Encoding.UTF8))
                return r.ReadToEnd();
        }
    }

    // Rebuild the AAR into a valid zip: seekable output => CRC/size back-patched
    // into local headers, NO data descriptors, every entry DEFLATED. This avoids
    // the "only DEFLATED entries can have EXT descriptor" Jetifier failure that a
    // naive ZipArchive.Update rewrite produces.
    static void RebuildAarWithManifest(string aar, string newManifest)
    {
        string tmp = aar + ".fixtmp";
        if (File.Exists(tmp)) File.Delete(tmp);

        using (var src = ZipFile.OpenRead(aar))
        using (var outStream = new FileStream(tmp, FileMode.CreateNew, FileAccess.Write))
        using (var dst = new ZipArchive(outStream, ZipArchiveMode.Create))
        {
            foreach (var entry in src.Entries)
            {
                var ne = dst.CreateEntry(entry.FullName, System.IO.Compression.CompressionLevel.Optimal);
                using (var os = ne.Open())
                {
                    if (string.Equals(entry.FullName, "AndroidManifest.xml", StringComparison.Ordinal))
                    {
                        var bytes = Encoding.UTF8.GetBytes(newManifest);
                        os.Write(bytes, 0, bytes.Length);
                    }
                    else
                    {
                        using (var ins = entry.Open()) ins.CopyTo(os);
                    }
                }
            }
        }

        File.Delete(aar);
        File.Move(tmp, aar);
    }

    // ---- 2. gradle.properties deprecations ---------------------------------
    static void FixGradleProperties(string gradleRoot)
    {
        string file = Path.Combine(gradleRoot, "gradle.properties");
        if (!File.Exists(file))
        {
            Debug.LogError($"[AndroidBuildFixes] gradle.properties not found at {file}");
            return;
        }
        try
        {
            var lines = File.ReadAllLines(file);
            var kept = new List<string>(lines.Length);
            bool changed = false;
            foreach (var line in lines)
            {
                string t = line.TrimStart();
                if (t.StartsWith("android.enableJetifier", StringComparison.Ordinal) ||
                    t.StartsWith("android.builtInKotlin", StringComparison.Ordinal))
                {
                    changed = true; // drop the deprecated line -> AGP 9 default
                    continue;
                }
                kept.Add(line);
            }
            if (changed)
            {
                File.WriteAllLines(file, kept);
                Debug.Log("[AndroidBuildFixes] Removed deprecated android.enableJetifier / android.builtInKotlin from gradle.properties");
            }
        }
        catch (Exception e) { Debug.LogError($"[AndroidBuildFixes] Failed to edit gradle.properties: {e}"); }
    }

    // ---- 3. xrmanifest package attribute -----------------------------------
    static readonly Regex ManifestPackageAttr =
        new Regex("(<manifest\\b[^>]*?)\\s+package=\"[^\"]*\"", RegexOptions.Singleline);

    static void FixXrManifestPackage(string gradleRoot)
    {
        foreach (var mf in SafeFiles(gradleRoot, "AndroidManifest.xml"))
        {
            if (mf.Replace('\\', '/').IndexOf("xrmanifest.androidlib/", StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            try
            {
                string text = File.ReadAllText(mf);
                string patched = ManifestPackageAttr.Replace(text, "$1", 1);
                if (patched != text)
                {
                    File.WriteAllText(mf, patched);
                    Debug.Log($"[AndroidBuildFixes] Removed redundant package= from {mf}");
                }
            }
            catch (Exception e) { Debug.LogError($"[AndroidBuildFixes] Failed to edit {mf}: {e}"); }
        }
    }

    // ---- 4. supportedDevices tools:replace ---------------------------------
    static readonly Regex SupportedDevicesToolsReplace =
        new Regex("tools:replace=\"android:value\"\\s+(?=android:name=\"com\\.oculus\\.supportedDevices\")");

    static void FixSupportedDevicesToolsReplace(string gradleRoot)
    {
        foreach (var mf in SafeFiles(gradleRoot, "AndroidManifest.xml"))
        {
            try
            {
                string text = File.ReadAllText(mf);
                if (text.IndexOf("com.oculus.supportedDevices", StringComparison.Ordinal) < 0) continue;
                string patched = SupportedDevicesToolsReplace.Replace(text, "");
                if (patched != text)
                {
                    File.WriteAllText(mf, patched);
                    Debug.Log($"[AndroidBuildFixes] Removed redundant tools:replace on com.oculus.supportedDevices in {mf}");
                }
            }
            catch (Exception e) { Debug.LogError($"[AndroidBuildFixes] Failed to edit {mf}: {e}"); }
        }
    }

    // ---- helpers -----------------------------------------------------------
    static string[] SafeFiles(string root, string pattern)
    {
        try { return Directory.GetFiles(root, pattern, SearchOption.AllDirectories); }
        catch (Exception e)
        {
            Debug.LogError($"[AndroidBuildFixes] Could not scan {root} for {pattern}: {e.Message}");
            return Array.Empty<string>();
        }
    }
}
#endif
