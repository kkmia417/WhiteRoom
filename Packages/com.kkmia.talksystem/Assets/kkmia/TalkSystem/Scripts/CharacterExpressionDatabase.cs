using System;
using System.Collections.Generic;
using UnityEngine;

namespace kkmia.TalkSystem
{
    [Serializable]
    public sealed class CharacterExpression
    {
        public string emotionKey;
        public Sprite sprite;
    }

    [Serializable]
    public sealed class CharacterDefinition
    {
        public string speakerKey;
        public string displayName;
        public Color nameColor = Color.white;
        public Sprite defaultSprite;
        public List<CharacterExpression> expressions = new List<CharacterExpression>();

        public bool TryGetSprite(string emotionKey, out Sprite sprite)
        {
            if (string.IsNullOrEmpty(emotionKey))
            {
                sprite = defaultSprite;
                return sprite != null;
            }

            for (var i = 0; i < expressions.Count; i++)
            {
                var expression = expressions[i];
                if (expression != null && expression.emotionKey == emotionKey)
                {
                    sprite = expression.sprite;
                    return sprite != null;
                }
            }

            sprite = defaultSprite;
            return sprite != null;
        }
    }

    [CreateAssetMenu(menuName = "kkmia/Talk System/Character Expression Database")]
    public sealed class CharacterExpressionDatabase : ScriptableObject
    {
        [SerializeField] private List<CharacterDefinition> characters = new List<CharacterDefinition>();

        public IReadOnlyList<CharacterDefinition> Characters
        {
            get { return characters; }
        }

        public bool TryGetCharacter(string speakerKey, out CharacterDefinition character)
        {
            for (var i = 0; i < characters.Count; i++)
            {
                var candidate = characters[i];
                if (candidate != null && candidate.speakerKey == speakerKey)
                {
                    character = candidate;
                    return true;
                }
            }

            character = null;
            return false;
        }

        public bool TryGetSprite(string speakerKey, string emotionKey, out Sprite sprite)
        {
            CharacterDefinition character;
            if (TryGetCharacter(speakerKey, out character))
                return character.TryGetSprite(emotionKey, out sprite);

            sprite = null;
            return false;
        }
    }
}
