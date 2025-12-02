// Licensed to the projects contributors.
// The license conditions are provided in the LICENSE file located in the project root

/**
 * SPDX-FileCopyrightText: Copyright (c) 2019 Source Auditor Inc.
 * SPDX-FileType: SOURCE
 * SPDX-License-Identifier: Apache-2.0
 */
namespace FileLicenseMatcher.SPDX.JavaCore;

/**
 * Holds information on lines and columns
 * @author Gary O'Neall
 *
 */
public record LineColumn(int Line, int Column, int Len);
