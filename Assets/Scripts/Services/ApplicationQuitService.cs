namespace WhiteRoom.Novel
{
    public interface IApplicationQuitter
    {
        bool IsAvailable { get; }
        string UnavailableReason { get; }
        void Quit();
    }

    public sealed class ApplicationQuitService
    {
        private readonly IApplicationQuitter _quitter;

        public ApplicationQuitService(IApplicationQuitter quitter)
        {
            _quitter = quitter;
        }

        public bool IsAvailable => _quitter != null && _quitter.IsAvailable;
        public string UnavailableReason => _quitter != null
            ? _quitter.UnavailableReason
            : "Quit is not available on this platform.";

        public bool ConfirmQuit()
        {
            if (!IsAvailable)
                return false;
            _quitter.Quit();
            return true;
        }
    }
}
