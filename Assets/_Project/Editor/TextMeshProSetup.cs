using System.IO;
using UnityEditor;
using UnityEngine;

namespace CoH.Editor
{
    /// <summary>
    /// Imports the TextMeshPro essential resources if the project has not got
    /// them yet.
    ///
    /// TextMeshPro ships its default font and settings inside a package that
    /// Unity normally asks you to import through a dialog the first time you
    /// create a text object. A generated scene has no one to click that dialog,
    /// so this does it, once, and does nothing on every later run.
    /// </summary>
    public static class TextMeshProSetup
    {
        private const string SettingsPath = "Assets/TextMesh Pro/Resources/TMP Settings.asset";

        public static bool IsInstalled => File.Exists(SettingsPath);

        [MenuItem("Conquest of Hearthstone/Import TextMeshPro Essentials")]
        public static void EnsureInstalled()
        {
            if (IsInstalled)
            {
                Debug.Log("TextMeshPro essentials are already present.");
                return;
            }

            string package = FindEssentialsPackage();

            if (package == null)
            {
                Debug.LogError(
                    "Could not find the TMP Essential Resources package in the package cache. " +
                    "Import it manually from Window > TextMeshPro > Import TMP Essential Resources.");
                return;
            }

            AssetDatabase.ImportPackage(package, false);
            AssetDatabase.Refresh();

            Debug.Log("Imported TextMeshPro essentials from " + package);
        }

        private static string FindEssentialsPackage()
        {
            string cache = Path.Combine(Directory.GetCurrentDirectory(), "Library", "PackageCache");

            if (!Directory.Exists(cache))
            {
                return null;
            }

            string[] matches = Directory.GetFiles(
                cache,
                "TMP Essential Resources.unitypackage",
                SearchOption.AllDirectories);

            return matches.Length > 0 ? matches[0] : null;
        }
    }
}
