using System;
using System.Collections.Generic;

namespace kkmia.TalkSystem
{
    public enum DialogueProgressMarkerType
    {
        Chapter,
        Route,
        Ending
    }

    [Serializable]
    public sealed class DialogueProgressState
    {
        public string CurrentChapterKey = string.Empty;
        public string CurrentRouteKey = string.Empty;
        public string CurrentEndingKey = string.Empty;
        public List<string> ReachedChapterKeys = new List<string>();
        public List<string> ReachedRouteKeys = new List<string>();
        public List<string> ReachedEndingKeys = new List<string>();

        public DialogueProgressState Clone()
        {
            return new DialogueProgressState
            {
                CurrentChapterKey = CurrentChapterKey ?? string.Empty,
                CurrentRouteKey = CurrentRouteKey ?? string.Empty,
                CurrentEndingKey = CurrentEndingKey ?? string.Empty,
                ReachedChapterKeys = CopyList(ReachedChapterKeys),
                ReachedRouteKeys = CopyList(ReachedRouteKeys),
                ReachedEndingKeys = CopyList(ReachedEndingKeys)
            };
        }

        public bool Mark(DialogueProgressMarkerType type, string key)
        {
            key = Normalize(key);
            if (key.Length == 0)
                return false;

            switch (type)
            {
                case DialogueProgressMarkerType.Chapter:
                    CurrentChapterKey = key;
                    if (ReachedChapterKeys == null) ReachedChapterKeys = new List<string>();
                    return AddUnique(ReachedChapterKeys, key);
                case DialogueProgressMarkerType.Route:
                    CurrentRouteKey = key;
                    if (ReachedRouteKeys == null) ReachedRouteKeys = new List<string>();
                    return AddUnique(ReachedRouteKeys, key);
                case DialogueProgressMarkerType.Ending:
                    CurrentEndingKey = key;
                    if (ReachedEndingKeys == null) ReachedEndingKeys = new List<string>();
                    return AddUnique(ReachedEndingKeys, key);
                default:
                    return false;
            }
        }

        private static string Normalize(string key)
        {
            return string.IsNullOrWhiteSpace(key) ? string.Empty : key.Trim();
        }

        private static List<string> CopyList(List<string> source)
        {
            return source != null ? new List<string>(source) : new List<string>();
        }

        private static bool AddUnique(List<string> values, string key)
        {
            if (values == null)
                return false;

            for (var i = 0; i < values.Count; i++)
            {
                if (values[i] == key)
                    return false;
            }

            values.Add(key);
            return true;
        }
    }

    public sealed class DialogueProgressMarker
    {
        public DialogueProgressMarker(DialogueProgressMarkerType type, string key, bool isFirstReach)
        {
            Type = type;
            Key = key ?? string.Empty;
            IsFirstReach = isFirstReach;
        }

        public DialogueProgressMarkerType Type { get; private set; }
        public string Key { get; private set; }
        public bool IsFirstReach { get; private set; }
    }

    public sealed class DialogueProgressEventContext
    {
        public DialogueProgressEventContext(DialogueData data, DialogueProgressMarker marker, DialogueProgressState progress)
        {
            Data = data;
            Marker = marker;
            Progress = progress != null ? progress.Clone() : new DialogueProgressState();
        }

        public DialogueData Data { get; private set; }
        public DialogueProgressMarker Marker { get; private set; }
        public DialogueProgressState Progress { get; private set; }
    }
}
