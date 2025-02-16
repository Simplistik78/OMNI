# OMNI - Overlay Map & Navigation Interface

An advanced Windows Forms application that provides real-time coordinate tracking and mapping interface for Pantheon: Rise of the Fallen.

![Main Interface](https://raw.githubusercontent.com/Simplistik78/OMNI/master/Images/mainform.png)
*Main Interface with full map view and capture controls*

![Compact Mode](https://raw.githubusercontent.com/Simplistik78/OMNI/master/Images/compact.png)
*Compact mode for minimal screen space usage*

## Overview

OMNI (Overlay Map & Navigation Interface) is a specialized tool designed to enhance the gaming experience in Pantheon: Rise of the Fallen by providing real-time coordinate tracking and visualization. It captures in-game coordinates and displays them on an interactive map interface, allowing players to better navigate the world.

## Features

- Real-time coordinate capture and mapping
- Interactive map integration with shalazam.info
- Advanced OCR-based coordinate detection
- Configurable capture area with visual positioning
- Debugging history with last 10 captures
- Global hotkey support
- Compact UI mode for minimal screen space usage

## Requirements

- Windows OS
- .NET 8.0 Runtime
- WebView2 Runtime
- Tesseract OCR Engine (included in release)

## Installation

1. Download the latest release from the Releases page
2. Extract all files to your preferred location
3. Run `OMNI.exe`

## First Time Setup

1. Launch OMNI
2. Click "Position Capture Window" to set the capture area
3. Position the overlay where your coordinates appear in-game
   - Coordinates should be visible and in format: "Your location: X Z Y H"
   - Only X and Y coordinates are used for mapping
4. Click "Capture" to save the position

## Usage

### Main Controls

- **Start/Stop Capture (F9)**: Begin/end continuous coordinate capture
- **Single Capture (Ctrl+F10)**: Capture coordinates once
- **Test Capture Area (Ctrl+T)**: Highlight current capture area
- **Reset All Arrows (Ctrl+R)**: Clear all markers from map
- **Enable Compact UI**: Switch to minimal interface mode

### Capture Settings

1. **Manual Position**:
   - X: Horizontal position from left screen edge
   - Y: Vertical position from top screen edge
   - Width: Capture area width
   - Height: Capture area height

2. **Visual Position**:
   - Click "Position Capture Window"
   - Drag overlay to desired location
   - Resize using edges if needed
   - Click "Capture" to save position

### Compact UI Mode

1. Click "Enable Compact UI" for a minimal interface
2. Window can be:
   - Dragged by the title bar
   - Resized from any edge
   - Always stays on top
3. Press Ctrl+U to toggle between full and compact modes

## Troubleshooting

### No Coordinates Detected

1. Ensure capture area is correctly positioned
2. Check that coordinate text is clearly visible
3. Verify text is white on dark background
4. Use "Test Capture Area" to confirm position

### Map Not Loading

1. Check internet connection
2. Verify WebView2 Runtime is installed
3. Ensure firewall isn't blocking connection
4. Try "Reset All Arrows" to refresh map

### Performance Issues

1. Increase capture interval in context menu
2. Reduce capture area size
3. Close other resource-intensive applications

## Support

For issues, questions, or suggestions:
1. Check existing Issues on GitHub
2. Create a new Issue with:
   - Description of problem
   - Steps to reproduce
   - Screenshots if applicable
   - System specifications

## Legal Notice

This project is licensed under the GNU Affero General Public License v3 (AGPLv3) with an additional clause forbidding commercial use - see the LICENSE file for details.

OMNI is provided as-is, without any warranty or guarantee of functionality. This software is not affiliated with, endorsed by, or connected to Pantheon: Rise of the Fallen or Visionary Realms, Inc.

## Acknowledgments

- Pantheon: Rise of the Fallen community
- Shalazam.info for map services and the amazing map work they have done , none of this could be possible without them.
- Tesseract OCR project
- Microsoft WebView2 team
