// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System;

/// <summary>
/// Copyright (c) 2015 Source Auditor Inc.
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
    /// Represents errors that occur during SPDX analysis.
    /// (This is a placeholder definition based on the Java code.)
    /// </summary>
    public class InvalidSpdxAnalysisException : Exception
    {
        public InvalidSpdxAnalysisException() { }
        public InvalidSpdxAnalysisException(string message) : base(message) { }
        public InvalidSpdxAnalysisException(string message, Exception inner) : base(message, inner) { }
    }
}
