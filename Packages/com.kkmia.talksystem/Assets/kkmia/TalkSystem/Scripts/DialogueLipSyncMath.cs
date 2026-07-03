using System;

namespace kkmia.TalkSystem
{
    /// <summary>
    /// リップシンクの信号処理（純関数）。Unity 型に依存しないためユニットテスト可能。
    /// </summary>
    public static class DialogueLipSyncMath
    {
        /// <summary>
        /// PCM サンプル列の RMS（二乗平均平方根）を返します。null/空は 0。
        /// </summary>
        public static float Rms(float[] samples, int count)
        {
            if (samples == null || count <= 0) return 0f;
            if (count > samples.Length) count = samples.Length;

            double sum = 0.0;
            for (var i = 0; i < count; i++)
            {
                var s = samples[i];
                sum += s * (double)s;
            }

            return (float)Math.Sqrt(sum / count);
        }

        /// <summary>
        /// 振幅（RMS）を口の開き具合 0..1 に変換します。
        /// <paramref name="threshold"/> 未満は無音とみなし 0、それ以上を <paramref name="sensitivity"/> 倍して 0..1 にクランプ。
        /// </summary>
        public static float Openness(float rms, float threshold, float sensitivity)
        {
            if (sensitivity <= 0f) sensitivity = 1f;
            if (rms <= threshold) return 0f;

            var value = (rms - threshold) * sensitivity;
            if (value < 0f) return 0f;
            if (value > 1f) return 1f;
            return value;
        }
    }
}
