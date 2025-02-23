# OMNI - Overlay Map & Navigation Interface

An advanced Windows Forms application that provides real-time coordinate tracking and mapping interface for Pantheon: Rise of the Fallen.

## Main Interface

![Main Interface](https://raw.githubusercontent.com/Simplistik78/OMNI/master/Images/mainform.png)

*Main Interface with full map view and capture controls*

## Compact Mode

![Compact Mode](https://raw.githubusercontent.com/Simplistik78/OMNI/master/Images/compact.png)

*Compact mode for minimal screen space usage*

## Overview

OMNI (Overlay Map & Navigation Interface) is a specialized tool designed to enhance the gaming experience in Pantheon: Rise of the Fallen by providing real-time coordinate tracking and visualization. It offers two methods of coordinate capture:

1. Clipboard Monitoring (Default): Automatically captures coordinates when using `/loc` or `/jumploc` commands in-game
2. OCR-based capture: Captures coordinates by reading them directly from the screen

## Features

- Dual coordinate capture methods:
  - Clipboard monitoring for `/loc` and `/jumploc` commands (Default)
  - OCR-based screen coordinate detection
- Real-time coordinate tracking and mapping
- Support for multiple maps (World Map, Halnir Cave, Goblin Caves)
- Interactive map integration with shalazam.info
- Configurable capture area with visual positioning
- Debugging history with last 10 captures
- Compact UI mode for minimal screen space usage
- Automatic switching between capture methods

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
2. The application will start in clipboard monitoring mode by default
3. Use `/loc` or `/jumploc` in-game to see your location on the map

### Optional OCR Setup

If you want to use OCR-based capture:

1. Click "Position Capture Window" to set the capture area
2. Position the overlay where your coordinates appear in-game
   - Coordinates should be visible and in format: "Your location: X Z Y H"
   - Only X and Y coordinates are used for mapping
3. Click "Capture" to save the position

NOTE: When positioning the capture window postion it like so , being below the chat box is ok but you dont want it running above the first line of text you want the full line of numbers being displayed or what could possibly be displayed.

![image](https://github.com/user-attachments/assets/198546bb-dcd5-41a6-8981-be88a3e954e2)

NOT like this 

![image](https://github.com/user-attachments/assets/c4663063-5e85-47f1-8603-ddb5f8d1a8df)


## Usage

### Coordinate Capture Methods

1. **Clipboard Monitoring (Default)**
   - Simply use `/loc` or `/jumploc` in game
   - Your position will automatically update on the map
   - No additional setup required

2. **OCR-based Capture**
   - Click "Start Capture" to begin OCR monitoring
   - Application will automatically switch back to clipboard monitoring when OCR is stopped

### Main Controls
(Hotkeys Currently Disabled)
- **Start/Stop Capture (F9)**: Begin/end continuous coordinate capture
- **Single Capture (Ctrl+F10)**: Capture coordinates once
- **Test Capture Area (Ctrl+T)**: Highlight current capture area
- **Reset All Arrows (Ctrl+R)**: Clear all markers from map
- **Enable Compact UI**: Switch to minimal interface mode
- **Right-click Menu**: Switch between available maps

### Available Maps
Right-click on either the main form's control panel or the compact UI's title bar to access:
- World Map (Default)
- Halnir Cave
- Goblin Caves

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

1. When using clipboard monitoring:
   - Ensure you're using `/loc` command in-game
   - Check if clipboard access is allowed

2. When using OCR capture:
   - Ensure capture area is correctly positioned ( see note above in Optional OCR Setup )
   - Check that coordinate text is clearly visible
   - Verify text is white on dark background
   - Use "Test Capture Area" to confirm position

### Map Not Loading

1. Check internet connection
2. Verify WebView2 Runtime is installed
3. Ensure firewall isn't blocking connection to shalazam.info
4. Try "Reset All Arrows" to refresh map
5. Try switching maps using the right-click context menu

### Performance Issues

1. Increase capture interval in context menu
2. Reduce capture area size if using OCR
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
- binutils (Discord) for the clipboard monitoring implementation tip
- Shalazam.info for map services and the amazing map work they have done, none of this could be possible without them
- Tesseract OCR project
- Microsoft WebView2 team
