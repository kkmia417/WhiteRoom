using System.IO;
using System.Text;
using UnityEngine;

namespace kkmia.TalkSystem
{
    public sealed class FileDialogueUnlockStorage : IDialogueUnlockStorage
    {
        private readonly string _filePath;

        public FileDialogueUnlockStorage(string filePath = null)
        {
            _filePath = string.IsNullOrEmpty(filePath)
                ? Path.Combine(Application.persistentDataPath, "dialogue_saves", "global_unlocks.json")
                : filePath;
        }

        public bool TryLoad(out DialogueUnlockState state)
        {
            state = null;
            if (!File.Exists(_filePath))
                return false;

            var json = File.ReadAllText(_filePath, Encoding.UTF8);
            state = JsonUtility.FromJson<DialogueUnlockState>(json);
            if (state == null)
                state = new DialogueUnlockState();

            state.EnsureEntries();
            return true;
        }

        public void Save(DialogueUnlockState state)
        {
            var snapshot = state != null ? state.Clone() : new DialogueUnlockState();
            AtomicWriteAllText(_filePath, JsonUtility.ToJson(snapshot));
        }

        public void Delete()
        {
            TryDelete(_filePath);
        }

        public bool Exists()
        {
            return File.Exists(_filePath);
        }

        private static void AtomicWriteAllText(string path, string contents)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var tempPath = path + ".tmp";
            try
            {
                File.WriteAllText(tempPath, contents ?? string.Empty, Encoding.UTF8);
                ReplaceAtomically(tempPath, path);
            }
            catch
            {
                TryDelete(tempPath);
                throw;
            }
        }

        private static void ReplaceAtomically(string tempPath, string targetPath)
        {
            if (!File.Exists(targetPath))
            {
                File.Move(tempPath, targetPath);
                return;
            }

            var backupPath = targetPath + ".bak";
            TryDelete(backupPath);

            try
            {
                File.Replace(tempPath, targetPath, backupPath, true);
                TryDelete(backupPath);
            }
            catch (System.PlatformNotSupportedException)
            {
                ReplaceWithMoveFallback(tempPath, targetPath, backupPath);
            }
            catch (IOException)
            {
                ReplaceWithMoveFallback(tempPath, targetPath, backupPath);
            }
        }

        private static void ReplaceWithMoveFallback(string tempPath, string targetPath, string backupPath)
        {
            TryDelete(backupPath);
            if (File.Exists(targetPath))
                File.Move(targetPath, backupPath);

            try
            {
                File.Move(tempPath, targetPath);
                TryDelete(backupPath);
            }
            catch
            {
                if (!File.Exists(targetPath) && File.Exists(backupPath))
                    File.Move(backupPath, targetPath);
                throw;
            }
        }

        private static void TryDelete(string path)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
                File.Delete(path);
        }
    }
}
