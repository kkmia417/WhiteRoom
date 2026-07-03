using System;
using kkmia.TalkSystem;

namespace WhiteRoom.Novel
{
    /// <summary>
    /// Resolves the {playerName} dialogue variable from the configured player name.
    /// </summary>
    public sealed class PlayerNameVariableResolver : IDialogueVariableResolver
    {
        private readonly Func<string> _playerNameProvider;

        public PlayerNameVariableResolver(Func<string> playerNameProvider)
        {
            _playerNameProvider = playerNameProvider;
        }

        public bool TryResolve(string variableName, DialogueData data, out string value)
        {
            if (string.Equals(variableName, "playerName", StringComparison.OrdinalIgnoreCase))
            {
                value = _playerNameProvider();
                return true;
            }

            value = null;
            return false;
        }
    }
}
