using System;
using System.Collections.Generic;
using UnityEngine;

namespace kkmia.TalkSystem
{
    [Serializable]
    public sealed class BackgroundEntry
    {
        public string backgroundKey;
        public Sprite sprite;
    }

    /// <summary>
    /// 背景キー -> Sprite の対応表。CSV の <c>Background</c> 列のキーを実アセットへ解決する。
    /// </summary>
    [CreateAssetMenu(menuName = "kkmia/Talk System/Background Database")]
    public sealed class BackgroundDatabase : ScriptableObject
    {
        [SerializeField] private List<BackgroundEntry> backgrounds = new List<BackgroundEntry>();

        public IReadOnlyList<BackgroundEntry> Backgrounds
        {
            get { return backgrounds; }
        }

        public bool TryGetSprite(string backgroundKey, out Sprite sprite)
        {
            if (!string.IsNullOrEmpty(backgroundKey))
            {
                for (var i = 0; i < backgrounds.Count; i++)
                {
                    var entry = backgrounds[i];
                    if (entry != null && entry.backgroundKey == backgroundKey)
                    {
                        sprite = entry.sprite;
                        return sprite != null;
                    }
                }
            }

            sprite = null;
            return false;
        }
    }
}
