// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

namespace Tethys.SPDX.ExpressionParser
{
    /// <summary>
    /// The token types.
    /// </summary>
    internal enum TokenType
    {
        /// <summary>
        /// A license identifier like MIT, Apache-2.0 or GPL-2.0.
        /// </summary>
        LicenseId,

        /// <summary>
        /// A license reference like LicenseRed-someorg-somename.
        /// </summary>
        LicenseRef,

        /// <summary>
        /// A license exception like Autoconf-exception-2.0.
        /// </summary>
        Exception,

        /// <summary>
        /// A trailing plus sign to indicate "or later".
        /// </summary>
        Plus,

        /// <summary>
        /// A left parenthesis.
        /// </summary>
        Left,

        /// <summary>
        /// A right parenthesis.
        /// </summary>
        Right,

        /// <summary>
        /// The license exception combination keyword..
        /// </summary>
        With,

        /// <summary>
        /// The license conjunction keyword.
        /// </summary>
        And,

        /// <summary>
        /// The license disjunction keyword.
        /// </summary>
        Or,
    } // TokenType
}
