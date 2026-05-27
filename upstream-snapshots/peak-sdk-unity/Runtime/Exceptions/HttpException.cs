using System;

namespace Peak.Exceptions
{
    /// <summary>
    /// Exception thrown for HTTP request failures.
    /// </summary>
    public class HttpException : Exception
    {
        public int StatusCode { get; }
        public string ResponseBody { get; }

        public HttpException(int statusCode, string message, string responseBody = null)
            : base(message)
        {
            StatusCode = statusCode;
            ResponseBody = responseBody;
        }

        public HttpException(int statusCode, string message, Exception innerException)
            : base(message, innerException)
        {
            StatusCode = statusCode;
        }
    }
}
