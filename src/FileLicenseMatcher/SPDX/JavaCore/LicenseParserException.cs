// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System;

/**
 * SPDX-FileCopyrightText: Copyright (c) 2015 Source Auditor Inc.
 * SPDX-FileType: SOURCE
 * SPDX-License-Identifier: Apache-2.0
 */
namespace FileLicenseMatcher.SPDX.JavaCore;

/**
 * Exception caused by an invalid license expression
 * @author Gary O'Neall
 *
 */
public class LicenseParserException : InvalidSPDXAnalysisException
{

    public LicenseParserException(string msg) : base(msg)
    {
    }

    public LicenseParserException(string msg, Exception inner) : base(msg, inner)
    {
    }
}
