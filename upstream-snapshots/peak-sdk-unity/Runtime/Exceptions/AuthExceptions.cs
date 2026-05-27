using System;

namespace Peak.Exceptions
{
    /// <summary>
    /// Exception thrown when authentication data is not found.
    /// </summary>
    public class NotAuthenticatedException : Exception
    {
        public NotAuthenticatedException(string message) : base(message) { }
    }

    /// <summary>
    /// Exception thrown when JWT token has expired.
    /// </summary>
    public class TokenExpiredException : Exception
    {
        public TokenExpiredException(string message) : base(message) { }
    }
}
