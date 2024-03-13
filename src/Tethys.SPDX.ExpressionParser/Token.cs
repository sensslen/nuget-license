// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System;

namespace Tethys.SPDX.ExpressionParser
{
    /// <summary>
    /// Implements a token.
    /// </summary>
    internal class Token
    {
        #region PUBLIC PROPERTIES

        /// <summary>
        /// Gets or sets the type.
        /// </summary>
        public TokenType Type { get; set; }

        /// <summary>
        /// Gets the value.
        /// </summary>
        public string Value { get; }
        #endregion // PUBLIC PROPERTIES

        //// ---------------------------------------------------------------------

        #region CONSTRUCTION
        /// <summary>
        /// Initializes a new instance of the <see cref="Token"/> class.
        /// </summary>
        /// <param name="type">The type.</param>
        /// <param name="value">The value.</param>
        public Token(TokenType type, string value)
        {
            Type = type;
            Value = value ?? throw new ArgumentNullException(nameof(value));
        } // Token()
        #endregion // CONSTRUCTION

        //// ---------------------------------------------------------------------

        #region PUBLIC METHODS
        /// <inheritdoc />
        public override string ToString()
        {
            return $"{Type}: {Value}";
        } // ToString()
        #endregion // PUBLIC METHODS
    } // Token
}
