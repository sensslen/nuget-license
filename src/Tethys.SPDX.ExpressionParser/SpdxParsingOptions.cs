// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System;

namespace Tethys.SPDX.ExpressionParser
{
    /// <summary>
    /// SPDX parsing options.
    /// </summary>
    [Flags]
    public enum SpdxParsingOptions
    {
        /// <summary>
        /// Default (= no) option.
        /// </summary>
        Default = 0x00,

        /// <summary>
        /// Allow unknown licenses.
        /// </summary>
        AllowUnknownLicenses = 0x01,

        /// <summary>
        /// Allow unknown exceptions.
        /// </summary>
        AllowUnknownExceptions = 0x02,
    } // SpdxParsingOptions
}
