// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System;

namespace Tethys.SPDX.ExpressionParser
{
    /// <summary>
    /// Represents an SPDX expression.
    /// </summary>
    public class SpdxLicenseExpression : SpdxExpression
    {
        //// Annex D SPDX license expressions
        //// simple-expression = license-id / license-id"+" / license-ref
        //// we replace this with
        //// simple-expression = SpdxLicenseExpression / SpdxLicenseReference

        #region PUBLIC PROPERTIES
        /// <summary>
        /// Gets the license ID.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Gets a value indicating whether or not later versions of the license is accepted.
        /// </summary>
        public bool OrLater { get; }
        #endregion // PUBLIC PROPERTIES

        //// ---------------------------------------------------------------------

        #region CONSTRUCTION
        /// <summary>
        /// Initializes a new instance of the <see cref="SpdxLicenseExpression"/> class.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="orLater">if set to <c>true</c> [or later].</param>
        public SpdxLicenseExpression(string id, bool orLater)
        {
            Id = id ?? throw new ArgumentNullException(nameof(id));
            OrLater = orLater;
        } // SpdxLicenseExpression()
        #endregion // CONSTRUCTION

        //// ---------------------------------------------------------------------

        #region PUBLIC METHODS
        /// <inheritdoc />
        public override string ToString()
        {
            string plus = OrLater ? "+" : string.Empty;
            return $"{Id}{plus}";
        } // ToString()
        #endregion // PUBLIC METHODS
    } // SpdxLicenseExpression
}
