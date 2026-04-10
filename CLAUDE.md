# CLAUDE.md

## Project Overview

This repository contains a Windows desktop application that provides OCR (Optical Character Recognition) capabilities similar to Apple's Live Text experience.

The goal is to create a clean, native, offline-first app that allows users to:

* Open an image
* Detect text via OCR
* Display detected text as an overlay
* Copy text easily (full or partial)

## Tech Stack Requirements

Claude must follow these priorities:

* Language: C#
* Framework: .NET 8
* UI: WinUI 3 (preferred) or WPF if justified
* Architecture: MVVM
* OCR:

  * Prefer Windows native OCR APIs
  * Fallback to Tesseract (offline only)

No web stack unless absolutely necessary.

## Core Features

The application must include:

1. Image Viewer

   * Open image (file picker + drag & drop)
   * Support PNG, JPG, JPEG, BMP (WebP optional)
   * Proper scaling and layout

2. OCR Button

   * Visible only when an image is loaded
   * Positioned bottom-right
   * Has states: idle, loading, success, error

3. OCR Processing

   * Fully offline
   * Extract:

     * Full text
     * Lines
     * Bounding boxes
   * French language support required

4. OCR Overlay

   * Draw bounding boxes over detected text
   * Allow interaction:

     * Click to select text
     * Copy selected text
     * Copy all text
   * Toggle visibility

5. Clipboard Integration

   * Native Windows clipboard
   * Preserve formatting when possible

6. Error Handling

   * Graceful handling of:

     * Unsupported files
     * No text detected
     * OCR failures

## UX Guidelines

* Minimalist UI
* Native Windows feel
* Smooth interactions
* No clutter
* Clean spacing and typography

Avoid "developer-looking" UI.

## Constraints

* 100% offline (no API calls)
* No telemetry
* No authentication
* No unnecessary features

## Development Rules

* Produce real, working code (no pseudo-code)
* Avoid placeholders and TODOs
* Keep code clean and structured
* Use clear naming
* Only comment when useful

## Expected Workflow

When starting from an empty repository:

1. Create full project structure
2. Generate solution and project files
3. Implement UI
4. Implement OCR logic
5. Connect everything
6. Provide build/run instructions

## Deliverables

Claude should always:

* Create all necessary files
* Ensure the project builds in Visual Studio
* Provide clear instructions to run the app
* Validate consistency and fix obvious issues

## Priority

Favor:

1. Simplicity
2. Reliability
3. Native experience
4. Maintainability

Over:

* Fancy features
* Over-engineering
