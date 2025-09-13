// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

/**
 * SPDX-FileCopyrightText: Copyright (c) 2013 Source Auditor Inc.
 * SPDX-FileType: SOURCE
 * SPDX-License-Identifier: Apache-2.0
 */
namespace SPDXLicenseMatcher.JavaCore;

/**
 * Handles output for parsed license templates
 * <p>
 * The methods are called during parsing
 * to handle the parsed rules and text.
 * 
 * @author Gary O'Neall
 */
public interface ILicenseTemplateOutputHandler
{
    /**
	 * Text for processing
	 * @param text text to be processed
	 */
    void text(string text);

    /**
	 * Variable rule found within the template
	 * @param rule license template rule
	 */
    void variableRule(LicenseTemplateRule rule);

    /**
	 * Begin optional rule found
	 * @param rule optional rule
	 */
    void beginOptional(LicenseTemplateRule rule);

    /**
	 * End optional rule found
	 * @param rule end optional rule
	 */
    void endOptional(LicenseTemplateRule rule);

    /**
	 * Signals all text has been added and parsing can be completed.
	 * @throws LicenseParserException if the license template could not be parsed
	 */
    void completeParsing();
}
