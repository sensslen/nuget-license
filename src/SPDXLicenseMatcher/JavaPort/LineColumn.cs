// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

/// <summary>
/// Copyright (c) 2017 Source Auditor Inc.
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
    /// <summary>
    /// Holds information on lines and columns.
    /// </summary>
    public class LineColumn
    {
        /// <summary>
        /// Gets or sets the line number.
        /// </summary>
        public int Line { get; set; }

        /// <summary>
        /// Gets or sets the column number.
        /// </summary>
        public int Column { get; set; }

        /// <summary>
        /// Gets or sets the length of the token or text.
        /// </summary>
        public int Length { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="LineColumn"/> class.
        /// </summary>
        /// <param name="line">The line number.</param>
        /// <param name="column">The column number.</param>
        /// <param name="length">The length of the token or text.</param>
        public LineColumn(int line, int column, int length)
        {
            Line = line;
            Column = column;
            Length = length;
        }
    }
}
