// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

using System;

/**
 * SPDX-FileCopyrightText: Copyright (c) 2013 Source Auditor Inc.
 * SPDX-FileType: SOURCE
 * SPDX-License-Identifier: Apache-2.0
 */
namespace SPDXLicenseMatcher.JavaCore;

/**
 * Exception for license template rules
 * @author Gary O'Neall
 *
 */
public class LicenseTemplateRuleException : Exception
{
    public LicenseTemplateRuleException(string msg) : base(msg)
    {
    }

    public LicenseTemplateRuleException(string msg, Exception inner) : base(msg, inner)
    {
    }
}
