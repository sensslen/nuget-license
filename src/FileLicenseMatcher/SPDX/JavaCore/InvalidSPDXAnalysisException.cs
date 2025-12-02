// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System;

/**
 * SPDX-FileCopyrightText: Copyright (c) 2019 Source Auditor Inc.
 * SPDX-FileType: SOURCE
 * SPDX-License-Identifier: Apache-2.0
 */
namespace FileLicenseMatcher.SPDX.JavaCore;

/**
 * Exception for invalid SPDX Documents
 * 
 * @author Gary O'Neall
 *
 */
#pragma warning disable S101
public class InvalidSPDXAnalysisException : Exception
#pragma warning restore S101
{

    public InvalidSPDXAnalysisException()
    {
    }

    public InvalidSPDXAnalysisException(string arg0) : base(arg0)
    {
    }

    public InvalidSPDXAnalysisException(Exception arg0) : base(string.Empty, arg0)
    {
    }

    public InvalidSPDXAnalysisException(string arg0, Exception arg1) : base(arg0, arg1)
    {
    }
}
