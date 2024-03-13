// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System;

namespace Tethys.SPDX.ExpressionParser
{
    /*************************************************************************
    * SPDX Expressions
    * ----------------
    * idstring = 1*(ALPHA / DIGIT / "-" / "." )
    *
    * license-id = <short form license identifier in Annex A.1>
    *
    * license-exception-id = <short form license exception identifier in Annex A.2>
    *
    * license-ref = ["DocumentRef-"(idstring)":"]"LicenseRef-"(idstring)
    *
    * simple-expression = license-id / license-id"+" / license-ref
    *
    * compound-expression = (simple-expression /
    *    simple-expression "WITH" license-exception-id /
    *    compound-expression "AND" compound-expression /
    *    compound-expression "OR" compound-expression /
    *    "(" compound-expression ")" )
    *
    * license-expression = (simple-expression / compound-expression)
    *
    ************************************************************************/

    /// <summary>
    /// Represents an SPDX expression.
    /// </summary>
    public class SpdxExpressionParser
    {
        #region PRIVATE PROPERTIES
        private readonly string[] _tokens;
        private int _position = 0;
        private readonly Func<string, bool> _isSpdxIdentifier;
        private readonly Func<string, bool> _isSpdxException;
        private readonly SpdxParsingOptions _options;
        #endregion // PRIVATE PROPERTIES

        //// ---------------------------------------------------------------------

        /// <summary>
        /// Parses a SPDX expression.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <param name="isIdentifier">The is identifier.</param>
        /// <param name="isException">The is exception.</param>
        /// <param name="parsingOptions">The options.</param>
        /// <returns>
        /// A <see cref="SpdxExpression" />.
        /// </returns>
        /// <exception cref="SpdxExpressionException">
        /// Exception for parsing problems.
        /// </exception>
        public static SpdxExpression? Parse(
            string expression,
            Func<string, bool>? isIdentifier,
            Func<string, bool>? isException,
            SpdxParsingOptions parsingOptions = SpdxParsingOptions.Default)
        {
            if (string.IsNullOrEmpty(expression))
            {
                throw new ArgumentNullException(nameof(expression));
            } // if

            // ensure that we detect all parenthesis
            expression = expression.Replace("(", " ( ");
            expression = expression.Replace(")", " ) ");

            var parser = new SpdxExpressionParser(expression,
                                                  isIdentifier ?? throw new ArgumentNullException(nameof(isIdentifier)),
                                                  isException ?? throw new ArgumentNullException(nameof(isException)),
                                                  parsingOptions);

            return parser.Parse();
        } // Parse()

        private SpdxExpressionParser(string expression,
            Func<string, bool> isIdentifier,
            Func<string, bool> isException,
            SpdxParsingOptions parsingOptions)
        {
            _isSpdxIdentifier = isIdentifier;
            _isSpdxException = isException;
            _options = parsingOptions;

            // very much simplified ...
            _tokens = expression.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            _position = -1;
        }

        private SpdxExpression? Parse()
        {
            _ = GetNextToken() ?? throw new SpdxExpressionException(string.Empty);
            SpdxExpression? expr = ParseAnd();

            return expr;
        }

        /// <summary>
        /// Parses an "and" expression.
        /// </summary>
        /// <returns>A <see cref="SpdxExpression"/>.</returns>
        private SpdxExpression? ParseAnd()
        {
            SpdxExpression? expression = ParseOr();
            Token? currentToken = GetCurrentToken();
            while (currentToken?.Type == TokenType.And)
            {
                GetNextToken();
                expression = new SpdxAndExpression(expression, ParseOr());
                currentToken = GetCurrentToken();
            } // while

            return expression;
        } // ParseAnd()

        /// <summary>
        /// Parses an "or" expression.
        /// </summary>
        /// <returns>A <see cref="SpdxExpression"/>.</returns>
        private SpdxExpression? ParseOr()
        {
            SpdxExpression? expression;
            Token? currentToken = GetCurrentToken();
            if (currentToken?.Type == TokenType.Left)
            {
                expression = ParseScopedExpression();
            }
            else
            {
                expression = ParseLicense();
            } // if

            currentToken = GetCurrentToken();
            while (currentToken?.Type == TokenType.Or)
            {
                GetNextToken();
                expression = new SpdxOrExpression(expression, ParseOr());
                currentToken = GetCurrentToken();
            } // while

            return expression;
        } // ParseOr()

        /// <summary>
        /// Parses a scoped expression.
        /// </summary>
        /// <returns>A <see cref="SpdxExpression"/>.</returns>
        private SpdxExpression ParseScopedExpression()
        {
            GetNextToken();
            SpdxExpression? expression = ParseAnd();
            if (GetCurrentToken()?.Type != TokenType.Right)
            {
                throw new SpdxExpressionException("Unexpected end of expression.");
            } // if

            GetNextToken();

            return new SpdxScopedExpression(expression);
        } // ParseScopedExpression()

        /// <summary>
        /// Determines whether this expression contains invalid characters.
        /// Allowed are (ALPHA / DIGIT / "-" / "." ).
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <returns>
        ///   <c>true</c> if this expression contains invalid characters; otherwise, <c>false</c>.
        /// </returns>
        private static bool ContainsInvalidCharacters(string expression)
        {
            foreach (char c in expression)
            {
                if (c != '+' && c != '.' && c != '-' && c != '(' && c != ')' && !char.IsDigit(c) && !char.IsLetter(c))
                {
                    return true;
                } // if
            } // foreach

            return false;
        } // ContainsInvalidCharacters()

        /// <summary>
        /// Parses a license.
        /// </summary>
        /// <returns>A <see cref="SpdxExpression"/>.</returns>
        private SpdxExpression? ParseLicense()
        {
            Token? token = GetCurrentToken();
            if (token?.Type == TokenType.LicenseId)
            {
                if ((_options & SpdxParsingOptions.AllowUnknownLicenses) == 0
                    && !_isSpdxIdentifier(token.Value.TrimEnd('+')))
                {
                    throw new SpdxExpressionException("Invalid/unknown SPDX license id");
                } // if

                Token? tokenNext = PeekNextToken();
                if (tokenNext?.Type == TokenType.With)
                {
                    Token? t2 = PeekNextNextToken();
                    if (t2?.Type == TokenType.Exception)
                    {
                        GetNextToken();
                        GetNextToken();
                        GetNextToken();

                        return new SpdxWithExpression(
                            GetLicenseExpression(token.Value),
                            t2.Value);
                    } // if
                } // if

                GetNextToken();

                return GetLicenseExpression(token.Value);
            } // if

            if (token?.Type == TokenType.LicenseRef)
            {
                GetNextToken();
                return new SpdxLicenseReference(token.Value);
            } // if

            return null;
        } // ParseLicense()

        /// <summary>
        /// Gets a license expression from the given string.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <returns>A <see cref="SpdxLicenseExpression"/>.</returns>
        private static SpdxLicenseExpression GetLicenseExpression(string expression)
        {
            if (expression.EndsWith("+"))
            {
                return new SpdxLicenseExpression(expression.TrimEnd('+'), true);
            } // if

            return new SpdxLicenseExpression(expression, false);
        } // GetLicenseExpression()

        /// <summary>
        /// Gets a token from the given text.
        /// </summary>
        /// <param name="text">The text.</param>
        /// <returns>A <see cref="Token"/>.</returns>
        private Token GetToken(string text)
        {
            string textCompare = text.Trim().ToLower();
            if (textCompare == "(")
            {
                return new Token(TokenType.Left, string.Empty);
            } // if

            if (textCompare == ")")
            {
                return new Token(TokenType.Right, string.Empty);
            } // if

            if (textCompare == "and")
            {
                return new Token(TokenType.And, "AND");
            } // if

            if (textCompare == "or")
            {
                return new Token(TokenType.Or, "OR");
            } // if

            if (textCompare.Contains("with"))
            {
                return new Token(TokenType.With, text);
            } // if

            if (textCompare.StartsWith("licenseref"))
            {
                return new Token(TokenType.LicenseRef, text);
            } // if

            if (textCompare.EndsWith("+"))
            {
                return new Token(TokenType.LicenseId, text);
            } // if

            if (_isSpdxIdentifier(textCompare))
            {
                return new Token(TokenType.LicenseId, text);
            } // if

            if (_isSpdxException(textCompare))
            {
                return new Token(TokenType.Exception, text);
            } // if

            if (ContainsInvalidCharacters(textCompare))
            {
                throw new SpdxExpressionException("Invalid characters found");
            } // if

            if ((_options & SpdxParsingOptions.AllowUnknownExceptions) != 0)
            {
                return new Token(TokenType.Exception, text);
            } // if

            throw new SpdxExpressionException($"Unknown token: {text}");
        } // GetToken()

        /// <summary>
        /// Gets the current token.
        /// </summary>
        /// <returns>A <see cref="Token"/>.</returns>
        private Token? GetCurrentToken()
        {
            if (_position < _tokens.Length)
            {
                return GetToken(_tokens[_position]);
            } // if

            return null;
        }

        /// <summary>
        /// Gets the next token.
        /// </summary>
        /// <returns>A <see cref="Token"/> or null.</returns>
        private Token? GetNextToken()
        {
            if (_position < _tokens.Length - 1)
            {
                return GetToken(_tokens[++_position]);
            } // if

            return null;
        } // GetNextToken()

        /// <summary>
        /// Peeks the next token.
        /// </summary>
        /// <returns>A <see cref="Token"/> or null.</returns>
        private Token? PeekNextToken()
        {
            if (_position < _tokens.Length - 1)
            {
                return GetToken(_tokens[_position + 1]);
            } // if

            return null;
        } // PeekNextToken()

        /// <summary>
        /// Peeks the next token.
        /// </summary>
        /// <returns>
        /// A <see cref="Token" /> or null.
        /// </returns>
        private Token? PeekNextNextToken()
        {
            if (_position < _tokens.Length - 2)
            {
                return GetToken(_tokens[_position + 2]);
            } // if

            return null;
        } // PeekNextNextToken()
    } // SpdxExpression
}
