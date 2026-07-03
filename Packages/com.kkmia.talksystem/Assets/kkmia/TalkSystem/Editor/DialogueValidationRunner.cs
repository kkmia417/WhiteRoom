using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace kkmia.TalkSystem.Editor
{
    public sealed class DialogueValidationRunner : IPreprocessBuildWithReport
    {
        private const string ProfileArgument = "-talkSystemValidationProfile";
        private const string ReportArgument = "-talkSystemValidationReport";

        public int callbackOrder
        {
            get { return 0; }
        }

        public void OnPreprocessBuild(BuildReport report)
        {
            var profiles = LoadAllProfiles().Where(p => p != null && p.RunAsBuildGate).ToArray();
            foreach (var profile in profiles)
            {
                var validationReport = ValidateProfile(profile);
                LogReport(validationReport);

                if (profile.FailBuildOnErrors && validationReport.HasErrors)
                    throw new BuildFailedException("TalkSystem dialogue validation failed for profile: " + AssetDatabase.GetAssetPath(profile));
            }
        }

        [MenuItem("Tools/kkmia/Validate Dialogue Profiles")]
        public static void ValidateAllProfilesFromMenu()
        {
            var profiles = LoadAllProfiles();
            var report = ValidateProfiles(profiles);
            LogReport(report);

            if (report.HasErrors)
                Debug.LogError("[TalkSystem] Dialogue validation finished with errors.");
            else
                Debug.Log("[TalkSystem] Dialogue validation finished.");
        }

        public static void ValidateFromCommandLine()
        {
            var args = Environment.GetCommandLineArgs();
            var profilePath = GetArgument(args, ProfileArgument);
            var reportPath = GetArgument(args, ReportArgument);

            var profiles = string.IsNullOrEmpty(profilePath)
                ? LoadAllProfiles()
                : new[] { AssetDatabase.LoadAssetAtPath<DialogueValidationProfile>(profilePath) };

            var report = ValidateProfiles(profiles);
            LogReport(report);

            if (!string.IsNullOrEmpty(reportPath))
                WriteJsonReport(report, reportPath);

            EditorApplication.Exit(report.HasErrors ? 1 : 0);
        }

        public static DialogueValidationReport ValidateProfile(DialogueValidationProfile profile)
        {
            var report = new DialogueValidationReport();
            if (profile == null)
            {
                report.Add(DialogueValidationSeverity.Error, 0, string.Empty, "Dialogue validation profile is missing.");
                return report;
            }

            if (profile.CsvFiles.Count == 0)
                report.Add(DialogueValidationSeverity.Warning, 0, string.Empty, "Dialogue validation profile has no CSV files.");

            var rows = new List<DialogueData>();
            foreach (var csvFile in profile.CsvFiles)
            {
                if (csvFile == null)
                {
                    report.Add(DialogueValidationSeverity.Error, 0, string.Empty, "Dialogue validation profile contains a missing CSV file reference.");
                    continue;
                }

                report.AddRange(DialogueValidator.ValidateCsv(csvFile.text).Messages);
                rows.AddRange(CsvLoader.ParseText<DialogueData>(csvFile.text).Values);
            }

            report.AddRange(DialogueValidator.ValidateAssets(rows, profile).Messages);
            return report;
        }

        public static DialogueValidationReport ValidateProfiles(DialogueValidationProfile[] profiles)
        {
            var report = new DialogueValidationReport();
            if (profiles == null || profiles.Length == 0)
            {
                report.Add(DialogueValidationSeverity.Warning, 0, string.Empty, "No dialogue validation profiles were found.");
                return report;
            }

            foreach (var profile in profiles)
                report.AddRange(ValidateProfile(profile).Messages);

            return report;
        }

        public static void WriteJsonReport(DialogueValidationReport report, string path)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
                Directory.CreateDirectory(directory);

            File.WriteAllText(path, ToJson(report), Encoding.UTF8);
        }

        private static DialogueValidationProfile[] LoadAllProfiles()
        {
            return AssetDatabase.FindAssets("t:DialogueValidationProfile")
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<DialogueValidationProfile>)
                .Where(profile => profile != null)
                .ToArray();
        }

        private static string GetArgument(string[] args, string name)
        {
            if (args == null) return string.Empty;

            for (var i = 0; i < args.Length - 1; i++)
                if (args[i] == name)
                    return args[i + 1];

            return string.Empty;
        }

        private static void LogReport(DialogueValidationReport report)
        {
            foreach (var message in report.Messages)
            {
                if (message.Severity == DialogueValidationSeverity.Error)
                    Debug.LogError(message.ToString());
                else if (message.Severity == DialogueValidationSeverity.Warning)
                    Debug.LogWarning(message.ToString());
                else
                    Debug.Log(message.ToString());
            }
        }

        private static string ToJson(DialogueValidationReport report)
        {
            var builder = new StringBuilder();
            builder.Append("{\"messages\":[");

            for (var i = 0; i < report.Messages.Count; i++)
            {
                if (i > 0) builder.Append(",");

                var message = report.Messages[i];
                builder.Append("{");
                builder.Append("\"severity\":\"").Append(EscapeJson(message.Severity.ToString())).Append("\",");
                builder.Append("\"rowNumber\":").Append(message.RowNumber).Append(",");
                builder.Append("\"fieldName\":\"").Append(EscapeJson(message.FieldName)).Append("\",");
                builder.Append("\"message\":\"").Append(EscapeJson(message.Message)).Append("\"");
                builder.Append("}");
            }

            builder.Append("],\"hasErrors\":").Append(report.HasErrors ? "true" : "false").Append("}");
            return builder.ToString();
        }

        private static string EscapeJson(string value)
        {
            if (string.IsNullOrEmpty(value)) return string.Empty;

            return value
                .Replace("\\", "\\\\")
                .Replace("\"", "\\\"")
                .Replace("\r", "\\r")
                .Replace("\n", "\\n")
                .Replace("\t", "\\t");
        }
    }
}
