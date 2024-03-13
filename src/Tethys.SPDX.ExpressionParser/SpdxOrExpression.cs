// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System;

namespace Tethys.SPDX.ExpressionParser
{
    /// <summary>
    /// Represents an SPDX expression.
    /// </summary>
    public class SpdxOrExpression : SpdxExpression
    {
        //// Annex D SPDX license expressions
        //// compound-expression "OR" compound-expression

        #region PUBLIC PROPERTIES
        /// <summary>
        /// Gets the left side of the expression.
        /// </summary>
        public SpdxExpression Left { get; }

        /// <summary>
        /// Gets the right side of the expression.
        /// </summary>
        public SpdxExpression Right { get; }
        #endregion // PUBLIC PROPERTIES

        //// ---------------------------------------------------------------------

        #region CONSTRUCTION
        /// <summary>
        /// Initializes a new instance of the <see cref="SpdxOrExpression"/> class.
        /// </summary>
        /// <param name="left">The left.</param>
        /// <param name="right">The right.</param>
        public SpdxOrExpression(SpdxExpression? left, SpdxExpression? right)
        {
            Left = left ?? throw new ArgumentNullException(nameof(left));
            Right = right ?? throw new ArgumentNullException(nameof(right));
        } // SpdxOrExpression()
        #endregion // CONSTRUCTION

        //// ---------------------------------------------------------------------

        #region PUBLIC METHODS
        /// <inheritdoc />
        public override string ToString()
        {
            return $"{Left.ToString()} OR {Right.ToString()}";
        } // ToString()
        #endregion // PUBLIC METHODS
    } // SpdxOrExpression
}
