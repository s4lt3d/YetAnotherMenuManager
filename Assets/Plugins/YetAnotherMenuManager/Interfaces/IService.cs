namespace Core.Interfaces
{
    public interface IService
    {
        /// <summary>
        ///     Do any initialization needed for the service
        /// </summary>
        void InitializeService();

        /// <summary>
        ///     Start the service, can be used to set up listeners or other runtime logic
        /// </summary>
        void StartService();

        /// <summary>
        ///     Cleanup any resources or listeners when the service is no longer needed
        /// </summary>
        void CleanupService();
    }
}