// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

/// <summary>
/// Copyright (c) 2013 Source Auditor Inc.
///
///    Licensed under the Apache License, Version 2.0 (the "License");
///    you may not use this file except in compliance with the License.
///    You may obtain a copy of the License at
///
///        http://www.apache.org/licenses/LICENSE-2.0
///
///    Unless required by applicable law or agreed to in writing, software
///    distributed under the License is distributed on an "AS IS" BASIS,
///    WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
///    See the License for the specific language governing permissions and
///    limitations under the License.
/// </summary>
namespace SPDXLicenseMatcher.JavaPort
{
    // Assuming LicenseParserException and LicenseTemplateRule types are defined in C#

    /// <summary>
    /// Handles output for parsed license templates. The methods are called during parsing
    /// to handle the parsed rules and text.
    /// </summary>
    public interface ILicenseTemplateOutputHandler
    {
        /// <summary>
        /// Text for processing.
        /// </summary>
        /// <param name="text">The text content.</param>
        void Text(string text);

        /// <summary>
        /// Variable rule found within the template.
        /// </summary>
        /// <param name="rule">The variable rule.</param>
        void VariableRule(LicenseTemplateRule rule);

        /// <summary>
        /// Begin optional rule found.
        /// </summary>
        /// <param name="rule">The optional rule.</param>
        void BeginOptional(LicenseTemplateRule rule);

        /// <summary>
        /// End optional rule found.
        /// </summary>
        /// <param name="rule">The optional rule.</param>
        void EndOptional(LicenseTemplateRule rule);

        /// <summary>
        /// Signals all text has been added and parsing can be completed.
        /// </summary>
        /// <exception cref="LicenseParserException">Thrown if an error occurs during the completion of parsing.</exception>
        void CompleteParsing();
    }
}
