using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace WhiteRoom.Novel
{
    public enum ScreenshotCaptureResultKind
    {
        Success,
        Unsupported,
        Busy,
        CaptureFailed,
        EncodingFailed,
        StorageFailed
    }

    public sealed class ScreenshotCaptureResult
    {
        private ScreenshotCaptureResult(
            ScreenshotCaptureResultKind kind,
            string message,
            string fileName,
            string directoryPath)
        {
            Kind = kind;
            Message = message ?? string.Empty;
            FileName = fileName ?? string.Empty;
            DirectoryPath = directoryPath ?? string.Empty;
        }

        public ScreenshotCaptureResultKind Kind { get; }
        public string Message { get; }
        public string FileName { get; }
        public string DirectoryPath { get; }
        public bool Succeeded => Kind == ScreenshotCaptureResultKind.Success;

        public static ScreenshotCaptureResult Success(string fileName, string directoryPath)
        {
            return new ScreenshotCaptureResult(
                ScreenshotCaptureResultKind.Success,
                "Screenshot saved: " + fileName + "\n" + directoryPath,
                fileName,
                directoryPath);
        }

        public static ScreenshotCaptureResult Failure(ScreenshotCaptureResultKind kind, string message)
        {
            return new ScreenshotCaptureResult(kind, message, string.Empty, string.Empty);
        }
    }

    public interface IScreenshotStorage
    {
        bool IsAvailable { get; }
        string UnavailableReason { get; }
        string DirectoryPath { get; }
        bool Exists(string fileName);
        void WritePng(string fileName, byte[] pngBytes);
    }

    public sealed class FileScreenshotStorage : IScreenshotStorage
    {
        private readonly string _directoryPath;

        public FileScreenshotStorage(string directoryPath = null)
        {
            _directoryPath = string.IsNullOrWhiteSpace(directoryPath)
                ? Path.Combine(Application.persistentDataPath, "Screenshots")
                : Path.GetFullPath(directoryPath);
        }

#if UNITY_WEBGL && !UNITY_EDITOR
        public bool IsAvailable => false;
        public string UnavailableReason => "Screenshots are unavailable in this WebGL build.";
#else
        public bool IsAvailable => true;
        public string UnavailableReason => string.Empty;
#endif
        public string DirectoryPath => _directoryPath;

        public bool Exists(string fileName)
        {
            return File.Exists(ResolveFilePath(fileName));
        }

        public void WritePng(string fileName, byte[] pngBytes)
        {
            if (!IsAvailable)
                throw new PlatformNotSupportedException(UnavailableReason);
            if (pngBytes == null || pngBytes.Length == 0)
                throw new ArgumentException("PNG payload is empty.", nameof(pngBytes));

            Directory.CreateDirectory(_directoryPath);
            using (var stream = new FileStream(
                       ResolveFilePath(fileName),
                       FileMode.CreateNew,
                       FileAccess.Write,
                       FileShare.None))
            {
                stream.Write(pngBytes, 0, pngBytes.Length);
                stream.Flush(true);
            }
        }

        private string ResolveFilePath(string fileName)
        {
            if (string.IsNullOrWhiteSpace(fileName)
                || !string.Equals(Path.GetFileName(fileName), fileName, StringComparison.Ordinal)
                || !string.Equals(Path.GetExtension(fileName), ".png", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Screenshot file name must be a plain PNG name.", nameof(fileName));

            return Path.Combine(_directoryPath, fileName);
        }
    }

    public static class ScreenshotFileNamePolicy
    {
        public static string CreateUnique(DateTime utcNow, Func<string, bool> exists)
        {
            if (exists == null)
                throw new ArgumentNullException(nameof(exists));

            var normalized = utcNow.Kind == DateTimeKind.Utc ? utcNow : utcNow.ToUniversalTime();
            var stem = "WhiteRoom_" + normalized.ToString("yyyyMMdd_HHmmss_fff") + "Z";
            var candidate = stem + ".png";
            if (!exists(candidate))
                return candidate;

            for (var suffix = 1; suffix <= 9999; suffix++)
            {
                candidate = stem + "_" + suffix.ToString("000") + ".png";
                if (!exists(candidate))
                    return candidate;
            }

            throw new IOException("No unique screenshot file name is available for the timestamp.");
        }
    }

    /// <summary>
    /// Owns one-at-a-time full-resolution screenshot capture and classified outcomes.
    /// Thumbnail resizing and sidecar storage deliberately remain outside this use case.
    /// </summary>
    public sealed class ScreenshotCaptureService
    {
        private readonly IScreenshotStorage _storage;
        private readonly Func<DateTime> _utcNow;
        private Func<Texture2D> _captureProvider;

        public ScreenshotCaptureService(
            IScreenshotStorage storage,
            Func<DateTime> utcNow = null,
            Func<Texture2D> captureProvider = null)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _utcNow = utcNow ?? (() => DateTime.UtcNow);
            _captureProvider = captureProvider;
        }

        public event Action CaptureStarted;
        public event Action<ScreenshotCaptureResult> CaptureCompleted;

        public bool IsAvailable => _storage.IsAvailable;
        public string UnavailableReason => _storage.UnavailableReason;
        public string DirectoryPath => _storage.DirectoryPath;
        public bool IsBusy { get; private set; }

        public void SetCaptureProvider(Func<Texture2D> provider)
        {
            _captureProvider = provider;
        }

        public bool TryBegin(out IEnumerator captureRoutine)
        {
            captureRoutine = null;
            if (IsBusy)
            {
                Complete(ScreenshotCaptureResult.Failure(
                    ScreenshotCaptureResultKind.Busy,
                    "A screenshot capture is already in progress."));
                return false;
            }
            if (!IsAvailable)
            {
                Complete(ScreenshotCaptureResult.Failure(
                    ScreenshotCaptureResultKind.Unsupported,
                    string.IsNullOrWhiteSpace(UnavailableReason)
                        ? "Screenshots are unavailable on this platform."
                        : UnavailableReason));
                return false;
            }

            IsBusy = true;
            CaptureStarted?.Invoke();
            captureRoutine = CaptureAtFrameEnd();
            return true;
        }

        private IEnumerator CaptureAtFrameEnd()
        {
            if (Application.isBatchMode)
                yield return null;
            else
                yield return new WaitForEndOfFrame();

            var result = CaptureAndStore();
            IsBusy = false;
            Complete(result);
        }

        private ScreenshotCaptureResult CaptureAndStore()
        {
            Texture2D screenshot = null;
            try
            {
                try
                {
                    screenshot = _captureProvider != null
                        ? _captureProvider()
                        : ScreenCapture.CaptureScreenshotAsTexture();
                    if (screenshot == null)
                        throw new InvalidOperationException("Screen capture returned no image.");
                }
                catch (Exception exception)
                {
                    return ScreenshotCaptureResult.Failure(
                        ScreenshotCaptureResultKind.CaptureFailed,
                        "Screenshot capture failed: " + exception.Message);
                }

                byte[] pngBytes;
                try
                {
                    pngBytes = screenshot.EncodeToPNG();
                    if (pngBytes == null || pngBytes.Length == 0)
                        throw new InvalidOperationException("PNG encoding returned no data.");
                }
                catch (Exception exception)
                {
                    return ScreenshotCaptureResult.Failure(
                        ScreenshotCaptureResultKind.EncodingFailed,
                        "Screenshot encoding failed: " + exception.Message);
                }

                try
                {
                    var fileName = ScreenshotFileNamePolicy.CreateUnique(_utcNow(), _storage.Exists);
                    _storage.WritePng(fileName, pngBytes);
                    return ScreenshotCaptureResult.Success(fileName, _storage.DirectoryPath);
                }
                catch (Exception exception)
                {
                    return ScreenshotCaptureResult.Failure(
                        ScreenshotCaptureResultKind.StorageFailed,
                        "Screenshot storage failed: " + exception.Message);
                }
            }
            finally
            {
                if (screenshot != null)
                {
                    if (Application.isPlaying)
                        UnityEngine.Object.Destroy(screenshot);
                    else
                        UnityEngine.Object.DestroyImmediate(screenshot);
                }
            }
        }

        private void Complete(ScreenshotCaptureResult result)
        {
            CaptureCompleted?.Invoke(result);
        }
    }
}
