// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System;

namespace Tethys.SPDX.ExpressionParser
{
    /// <summary>
    /// Represents an SPDX expression enclosed by parenthesis.
    /// </summary>
    public class SpdxScopedExpression : SpdxExpression
    {
        //// Annex D SPDX license expressions
        //// "(" compound-expression ")"

        #region PUBLIC PROPERTIES
        /// <summary>
        /// Gets the expression node.
        /// </summary>
        public SpdxExpression Expression { get; }
        #endregion // PUBLIC PROPERTIES

        //// ---------------------------------------------------------------------

        #region CONSTRUCTION
        /// <summary>
        /// Initializes a new instance of the <see cref="SpdxScopedExpression"/> class.
        /// </summary>
        /// <param name="expression">The expression node.</param>
        public SpdxScopedExpression(SpdxExpression? expression)
        {
            Expression = expression ?? throw new ArgumentNullException(nameof(expression));
        } // SpdxScopedExpression()
        #endregion // CONSTRUCTION

        //// ---------------------------------------------------------------------

        #region PUBLIC METHODS
        /// <inheritdoc />
        public override string ToString()
        {
            return $"({Expression.ToString()})";
        } // ToString()
        #endregion // PUBLIC METHODS
    } // SpdxScopedExpression
}
