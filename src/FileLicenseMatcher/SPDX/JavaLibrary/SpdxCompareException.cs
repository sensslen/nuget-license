// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System;

/**
 * SPDX-FileCopyrightText: Copyright (c) 2020 Source Auditor Inc.
 * SPDX-FileType: SOURCE
 * SPDX-License-Identifier: Apache-2.0
 * <p>
 *   Licensed under the Apache License, Version 2.0 (the "License");
 *   you may not use this file except in compliance with the License.
 *   You may obtain a copy of the License at
 * <p>
 *       http://www.apache.org/licenses/LICENSE-2.0
 * <p>
 *   Unless required by applicable law or agreed to in writing, software
 *   distributed under the License is distributed on an "AS IS" BASIS,
 *   WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 *   See the License for the specific language governing permissions and
 *   limitations under the License.
 */
namespace FileLicenseMatcher.SPDX.JavaLibrary;

/**
 * Exception thrown when there is an error during the comparison of SPDX
 * documents
 * 
 * @author Gary O'Neall
 */
public class SpdxCompareException : Exception
{
    public SpdxCompareException(string message) : base(message)
    {
    }

    public SpdxCompareException(Exception cause) : this(string.Empty, cause)
    {
    }

    public SpdxCompareException(string message, Exception cause) : base(message, cause)
    {
    }

}
