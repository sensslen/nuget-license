// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System;

namespace Tethys.SPDX.ExpressionParser
{
    /// <summary>
    /// Represents an SPDX expression.
    /// </summary>
    public class SpdxWithExpression : SpdxExpression
    {
        //// Annex D SPDX license expressions
        //// simple-expression "WITH" license-exception-id

        #region PUBLIC PROPERTIES
        /// <summary>
        /// Gets the expression node.
        /// </summary>
        public SpdxExpression Expression { get; }

        /// <summary>
        /// Gets the license exception node.
        /// </summary>
        public string Exception { get; }
        #endregion // PUBLIC PROPERTIES

        //// ---------------------------------------------------------------------

        #region CONSTRUCTION
        /// <summary>
        /// Initializes a new instance of the <see cref="SpdxWithExpression"/> class.
        /// </summary>
        /// <param name="expression">The expression node.</param>
        /// <param name="exception">The license exception node.</param>
        public SpdxWithExpression(SpdxExpression expression, string exception)
        {
            Expression = expression ?? throw new ArgumentNullException(nameof(expression));
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
        } // SpdxWithExpression()
        #endregion // CONSTRUCTION

        //// ---------------------------------------------------------------------

        #region PUBLIC METHODS
        /// <inheritdoc />
        public override string ToString()
        {
            return $"{Expression.ToString()} WITH {Exception}";
        } // ToString()
        #endregion // PUBLIC METHODS
    } // SpdxExpression
}
