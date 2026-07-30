using System;
using UnityEngine;

namespace kkmia.TalkSystem
{
    /// <summary>Creates a bounded 16:9 PNG sidecar without retaining runtime textures.</summary>
    public static class DialogueThumbnailEncoder
    {
        public static byte[] EncodePng(Texture2D source, int width, int height, int maximumBytes)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            if (width <= 0 || height <= 0)
                throw new ArgumentOutOfRangeException(nameof(width));
            if (maximumBytes <= 0)
                throw new ArgumentOutOfRangeException(nameof(maximumBytes));

            var renderTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32);
            var previous = RenderTexture.active;
            Texture2D resized = null;
            try
            {
                var sourceAspect = source.width / (float)Mathf.Max(1, source.height);
                var targetAspect = width / (float)height;
                var scale = Vector2.one;
                var offset = Vector2.zero;
                if (sourceAspect > targetAspect)
                {
                    scale.x = targetAspect / sourceAspect;
                    offset.x = (1f - scale.x) * 0.5f;
                }
                else if (sourceAspect < targetAspect)
                {
                    scale.y = sourceAspect / targetAspect;
                    offset.y = (1f - scale.y) * 0.5f;
                }

                Graphics.Blit(source, renderTexture, scale, offset);
                RenderTexture.active = renderTexture;
                resized = new Texture2D(width, height, TextureFormat.RGB24, false);
                resized.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
                resized.Apply(false, false);
                var bytes = resized.EncodeToPNG();
                if (bytes == null || bytes.Length == 0)
                    throw new InvalidOperationException("PNG encoding returned no data.");
                if (bytes.Length > maximumBytes)
                    throw new InvalidOperationException("PNG thumbnail exceeded the configured size limit.");
                return bytes;
            }
            finally
            {
                RenderTexture.active = previous;
                if (resized != null)
                    UnityEngine.Object.Destroy(resized);
                RenderTexture.ReleaseTemporary(renderTexture);
            }
        }
    }
}
