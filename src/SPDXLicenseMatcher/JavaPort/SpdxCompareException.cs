// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System;

namespace SPDXLicenseMatcher.JavaPort
{
    /// <summary>
    /// Exception thrown when there is an error during the comparison of SPDX documents.
    /// </summary>
    public class SpdxCompareException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SpdxCompareException"/> class.
        /// </summary>
        public SpdxCompareException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SpdxCompareException"/> class with a specified error message.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public SpdxCompareException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SpdxCompareException"/> class with a specified error message 
        /// and a reference to the inner exception that is the cause of this exception.
        /// </summary>
        /// <param name="message">The error message that explains the reason for the exception.</param>
        /// <param name="innerException">The exception that is the cause of the current exception.</param>
        public SpdxCompareException(string message, Exception innerException) : base(message, innerException)
        {
        }
    }
}
