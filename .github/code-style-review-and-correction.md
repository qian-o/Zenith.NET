# Code Style Review and Correction

## Overview

This is a long-term maintenance task that leverages Copilot assistance to maintain consistent code style and ensure the project's long-term maintainability.

## Workflow

1. **Review Schedule**: Conduct periodic reviews based on discussion forum notifications
2. **PR Standards**: Pull Request titles must include the review date
3. **Prerequisites**: ⚠️ **You must learn and understand the project's code style before conducting reviews**

## Checklist

### 1. File Naming Conventions
- [ ] Verify that file names are consistent with class names

### 2. Code Formatting
- [ ] Check and correct excessive blank lines in code (remove consecutive blank lines exceeding one line, or blank lines at the beginning of files)
- [ ] Prefer using `new()` syntax sugar

### 3. Lambda Expressions
- [ ] Lambda expressions should preferably use the `static` modifier when not accessing external members

### 4. Type Conversion
- [ ] Reduce implicit conversions in code to improve readability

### 5. Syntax Optimization
- [ ] Prefer using pattern matching syntax

### 6. Member Ordering Standards
- [ ] Arrange class members in the following order:
  1. Fields
  2. Constructors
  3. Properties
  4. Methods

### 7. Access Modifier Ordering
- [ ] Within each member type, sort by access level:
  - `public` → `internal` → `protected` → `private`

### 8. Syntax Details
- [ ] The last element in property assignments and enum definitions should not contain trailing commas

### 9. Compilation Checks
- [ ] Ensure code compiles successfully
- [ ] Check and fix all compilation errors (Errors)
- [ ] Resolve compilation warnings (Warnings)
- [ ] Handle compilation messages (Messages)
