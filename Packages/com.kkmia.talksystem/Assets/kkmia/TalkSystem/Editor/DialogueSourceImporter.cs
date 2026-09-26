using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.AssetImporters;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace kkmia.TalkSystem.Editor
{
    [ScriptedImporter(1, "dialogue")]
    public sealed class DialogueSourceImporter : ScriptedImporter
    {
        [Serializable]
        private sealed class Manifest
        {
            public string[] sources;
        }

        public override void OnImportAsset(AssetImportContext context)
        {
            var asset = ScriptableObject.CreateInstance<CompiledDialogueAsset>();
            asset.name = Path.GetFileNameWithoutExtension(context.assetPath);
            try
            {
                asset.Compile(ReadSourceCsv(context.assetPath, context.DependsOnSourceAsset));
            }
            catch (Exception exception)
            {
                context.LogImportError(context.assetPath + ": " + exception.Message);
            }
            // Even failed imports replace old data with an unusable asset, never stale dialogue.
            context.AddObjectToAsset("dialogue", asset);
            context.SetMainObject(asset);
        }

        /// <summary>Assemble sources with the existing codec for validation and temporary previews.</summary>
        public static string ReadSourceCsv(string manifestPath, Action<string> dependency = null)
        {
            var manifest = JsonUtility.FromJson<Manifest>(File.ReadAllText(manifestPath));
            if (manifest?.sources == null || manifest.sources.Length == 0)
                throw new InvalidDataException("Manifest must list at least one CSV source.");

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            IReadOnlyList<string> headers = null;
            var rows = new List<IReadOnlyList<string>>();
            foreach (var source in manifest.sources)
            {
                if (string.IsNullOrWhiteSpace(source) || !source.StartsWith("Assets/", StringComparison.Ordinal)
                    || source.Contains("..") || source.Contains('\\') || !source.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidDataException("Expected a project-relative Assets/...csv path: " + source);
                if (!seen.Add(source)) throw new InvalidDataException("Duplicate source: " + source);
                dependency?.Invoke(source);
                if (!File.Exists(source)) throw new FileNotFoundException("Missing dialogue source: " + source);
                var document = DialogueCsvCodec.Parse(File.ReadAllText(source));
                if (document.Diagnostics.HasErrors)
                    throw new InvalidDataException(source + ": " + string.Join("\n", document.Diagnostics.Messages));
                if (headers == null) headers = document.Headers;
                else if (!headers.SequenceEqual(document.Headers))
                    throw new InvalidDataException("CSV headers must match the first source: " + source);
                rows.AddRange(document.Rows.Select(row => row.Values));
            }
            return DialogueCsvCodec.Write(headers, rows);
        }

        [MenuItem("Assets/Talk System/Export Combined CSV for Preview", true)]
        private static bool CanExport() => Selection.activeObject is CompiledDialogueAsset;

        [MenuItem("Assets/Talk System/Export Combined CSV for Preview")]
        private static void ExportPreview()
        {
            var csv = ReadSourceCsv(AssetDatabase.GetAssetPath(Selection.activeObject));
            const string directory = "Assets/Editor/DialoguePreview";
            Directory.CreateDirectory(directory);
            var path = directory + "/" + Selection.activeObject.name + ".csv";
            File.WriteAllText(path, csv);
            AssetDatabase.ImportAsset(path);
            Selection.activeObject = AssetDatabase.LoadAssetAtPath<TextAsset>(path);
        }
    }

    public sealed class DialogueSourceBuildGate : IPreprocessBuildWithReport
    {
        public int callbackOrder => -10;

        public void OnPreprocessBuild(BuildReport report)
        {
            foreach (var path in Directory.GetFiles(Application.dataPath, "*.dialogue", SearchOption.AllDirectories))
            {
                var asset = ScriptableObject.CreateInstance<CompiledDialogueAsset>();
                try { asset.Compile(DialogueSourceImporter.ReadSourceCsv(path)); }
                catch (Exception exception) { throw new BuildFailedException(path + ": " + exception.Message); }
                finally { UnityEngine.Object.DestroyImmediate(asset); }
            }
        }
    }
}
