using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace kkmia.TalkSystem
{
    /// <summary>
    /// <see cref="IDialogueSaveStorage"/> のファイル実装。スロットを JSON、サムネイルを PNG として
    /// 既定では <c>Application.persistentDataPath</c> 配下に保存する。
    /// </summary>
    public sealed class FileDialogueSaveStorage : IDialogueSaveStorage
    {
        private const string SlotPrefix = "slot_";
        private readonly string _directory;

        public FileDialogueSaveStorage(string directory = null)
        {
            _directory = string.IsNullOrEmpty(directory)
                ? Path.Combine(Application.persistentDataPath, "dialogue_saves")
                : directory;
        }

        public bool TryLoad(int slot, out DialogueSaveSlot data)
        {
            data = null;
            var path = SlotPath(slot);
            if (!File.Exists(path)) return false;

            if (!TryReadSlotFile(path, out data))
            {
                var backupPath = BackupPath(path);
                if (!File.Exists(backupPath) || !TryReadSlotFile(backupPath, out data))
                    throw new InvalidDataException("Slot JSON did not contain a readable dialogue save slot.");
            }

            return true;
        }

        private static bool TryReadSlotFile(string path, out DialogueSaveSlot data)
        {
            data = null;
            var json = File.ReadAllText(path, Encoding.UTF8);
            var hasSchemaVersion = json.IndexOf("\"SchemaVersion\"", System.StringComparison.Ordinal) >= 0;
            try
            {
                data = JsonUtility.FromJson<DialogueSaveSlot>(json);
            }
            catch (System.ArgumentException)
            {
                return false;
            }

            if (data == null)
                return false;

            if (!hasSchemaVersion)
            {
                data.SchemaVersion = 0;
                if (data.Data != null)
                    data.Data.SchemaVersion = 0;
            }

            return true;
        }

        public void Save(DialogueSaveSlot slot)
        {
            if (slot == null) return;
            EnsureDirectory();
            AtomicWriteAllText(SlotPath(slot.SlotIndex), JsonUtility.ToJson(slot));
        }

        public void Delete(int slot)
        {
            DeleteWithBackup(SlotPath(slot));
            DeleteWithBackup(ThumbnailPath(slot));
        }

        public bool Exists(int slot)
        {
            return File.Exists(SlotPath(slot));
        }

        public IEnumerable<int> ListSlots()
        {
            var result = new List<int>();
            if (!Directory.Exists(_directory)) return result;

            foreach (var file in Directory.GetFiles(_directory, SlotPrefix + "*.json"))
            {
                var name = Path.GetFileNameWithoutExtension(file);
                int index;
                if (int.TryParse(name.Substring(SlotPrefix.Length), out index))
                    result.Add(index);
            }

            result.Sort();
            return result;
        }

        public byte[] LoadThumbnail(int slot)
        {
            var path = ThumbnailPath(slot);
            return File.Exists(path) ? File.ReadAllBytes(path) : null;
        }

        public void SaveThumbnail(int slot, byte[] pngBytes)
        {
            if (pngBytes == null || pngBytes.Length == 0) return;
            EnsureDirectory();
            AtomicWriteAllBytes(ThumbnailPath(slot), pngBytes);
        }

        private void EnsureDirectory()
        {
            if (!Directory.Exists(_directory))
                Directory.CreateDirectory(_directory);
        }

        private string SlotPath(int slot)
        {
            return Path.Combine(_directory, SlotPrefix + slot + ".json");
        }

        private string ThumbnailPath(int slot)
        {
            return Path.Combine(_directory, SlotPrefix + slot + ".png");
        }

        private static void AtomicWriteAllText(string path, string contents)
        {
            AtomicWriteAllBytes(path, Encoding.UTF8.GetBytes(contents ?? string.Empty));
        }

        private static void AtomicWriteAllBytes(string path, byte[] bytes)
        {
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                Directory.CreateDirectory(directory);

            var tempPath = path + ".tmp";
            try
            {
                using (var stream = new FileStream(tempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);
                }

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
                TryDelete(BackupPath(targetPath));
                File.Move(tempPath, targetPath);
                return;
            }

            var backupPath = BackupPath(targetPath);
            TryDelete(backupPath);

            try
            {
                File.Replace(tempPath, targetPath, backupPath, true);
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
            if (File.Exists(path))
                File.Delete(path);
        }

        private static void DeleteWithBackup(string path)
        {
            TryDelete(path);
            TryDelete(BackupPath(path));
        }

        private static string BackupPath(string path)
        {
            return path + ".bak";
        }
    }
}
