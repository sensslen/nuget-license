// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System;

namespace Tethys.SPDX.ExpressionParser
{
    /// <summary>
    /// Represents an SPDX expression.
    /// </summary>
    public class SpdxExpressionException : Exception
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="SpdxExpressionException"/> class.
        /// </summary>
        /// <param name="message">The message that describes the error.</param>
        public SpdxExpressionException(string message)
            : base(message)
        {
        } // SpdxExpressionException()
    } // SpdxExpressionException
}
