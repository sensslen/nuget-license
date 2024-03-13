// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System;

namespace Tethys.SPDX.ExpressionParser
{
    /// <summary>
    /// Represents an SPDX expression.
    /// </summary>
    public class SpdxLicenseReference : SpdxExpression
    {
        //// Annex D SPDX license expressions
        //// LicenseRef-"(idstring)

        #region PUBLIC PROPERTIES
        /// <summary>
        /// Gets the license reference.
        /// </summary>
        public string LicenseRef { get; }
        #endregion // PUBLIC PROPERTIES

        //// ---------------------------------------------------------------------

        #region CONSTRUCTION
        /// <summary>
        /// Initializes a new instance of the <see cref="SpdxLicenseReference"/> class.
        /// </summary>
        /// <param name="licenseRef">The license reference.</param>
        public SpdxLicenseReference(string licenseRef)
        {
            LicenseRef = licenseRef ?? throw new ArgumentNullException();
        } // SpdxLicenseReference()
        #endregion // CONSTRUCTION

        //// ---------------------------------------------------------------------

        #region PUBLIC METHODS
        /// <inheritdoc />
        public override string ToString()
        {
            string text = $"{LicenseRef}";
            return text;
        } // ToString()
        #endregion // PUBLIC METHODS
    } // SpdxLicenseReference
}
