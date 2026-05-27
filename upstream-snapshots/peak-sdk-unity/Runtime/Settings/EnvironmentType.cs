namespace Peak.Settings
{
    /// <summary>
    /// Defines the environment types for Peak SDK configuration
    /// </summary>
    public enum EnvironmentType
    {
        /// <summary>
        /// Development environment for local testing
        /// </summary>
        Development = 0,

        /// <summary>
        /// Staging environment for pre-production testing
        /// </summary>
        Staging = 1,

        /// <summary>
        /// Production environment for live deployment
        /// </summary>
        Production = 2
    }
}