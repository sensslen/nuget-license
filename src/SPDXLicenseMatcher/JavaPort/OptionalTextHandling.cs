// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

namespace SPDXLicenseMatcher.JavaPort
{
    public enum OptionalTextHandling
    {
        /**
		 * Omit the optional text
		 */
        OMIT,
        /**
		 * Retain the optional text
		 */
        ORIGINAL,
        /**
		 * Create a regex for the optional text with the REGEX_ESCAPE string tokenizing the words
		 */
        REGEX_USING_TOKENS
    }
}
