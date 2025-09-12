// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

namespace SPDXLicenseMatcher.JavaPort
{
    public enum VarTextHandling
    {
        /**
		 * Omit the var text all together
		 */
        OMIT,
        /**
		 * Include the original text for the regex
		 */
        ORIGINAL,
        /**
		 * Include the regex itself included by the REGEX_ESCAPE strings
		 */
        REGEX
    }
}
