// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System.Collections.Generic;
using SPDXLicenseMatcher.JavaCore;

namespace SPDXLicenseMatcher.JavaLibrary;

/**
 * Information about any difference found
 */
public class DifferenceDescription(bool differenceFound, string differenceMessage, List<LineColumn> differences)
{
    private const int MAX_DIFF_TEXT_LENGTH = 100;

    /**
		 * Creates a different description
		 */
    public DifferenceDescription() : this(false, "No difference found", [])
    {
    }

    public bool DifferenceFound { get; private set; } = differenceFound;
    public string DifferenceMessage { get; private set; } = differenceMessage;
    private readonly List<LineColumn> _differences = differences;
    public IReadOnlyList<LineColumn> Differences => _differences;

    /**
		 * Adds a new difference to the list of differences found during the comparison process
		 *
		 * @param location Location in the text of the difference
		 * @param token Token causing the difference
		 * @param msg Message for the difference
		 * @param text Template text being compared to
		 * @param rule Template rule where difference was found
		 * @param lastOptionalDifference The difference for the last optional difference that failed
		 */
    public void AddDifference(LineColumn? location, string? token, string msg, string? text,
            LicenseTemplateRule? rule, DifferenceDescription? lastOptionalDifference)
    {
        if (token == null)
        {
            token = "";
        }
        if (msg == null)
        {
            msg = "UNKNOWN (null)";
        }
        DifferenceMessage = msg;
        if (location != null)
        {
            DifferenceMessage = DifferenceMessage + " starting at line #" +
                    location.Line + " column #" +
                    location.Column + " \"" +
                    token + "\"";
            _differences.Add(location);
        }
        else
        {
            DifferenceMessage = DifferenceMessage + " at end of text";
        }
        if (text != null)
        {
            DifferenceMessage = DifferenceMessage + " when comparing to template text \"";
            if (text.Length > MAX_DIFF_TEXT_LENGTH)
            {
                DifferenceMessage = DifferenceMessage +
                        text.Substring(0, MAX_DIFF_TEXT_LENGTH) + "...\"";
            }
            else
            {
                DifferenceMessage = DifferenceMessage + text + "\"";
            }
        }
        if (rule != null)
        {
            DifferenceMessage = DifferenceMessage + " while processing rule " + rule;
        }
        if (lastOptionalDifference != null)
        {
            DifferenceMessage = DifferenceMessage +
                    ".  Last optional text was not found due to the optional difference: \n\t" +
                    lastOptionalDifference.DifferenceMessage;
        }
        DifferenceFound = true;
    }
}
