# Stream-integrated Counter for Elgato Stream Deck

A counter plugin you can use to keep score (kills/deaths/etc). The count is saved to a text file which you can then show live on your stream

**Author's website and contact information:** [https://barraider.com](https://barraider.com)

## New in v1.7
- Multi-Action support (Counter can now be updated in a Multi-Action based on the Short Press setting)
- Support for `\n` (new line) in the prefix title (for display on the SD key)
- Fixed issue where resetting the counter did not clear the text file

## New in v1.6
- ***SOUND SUPPORT*** You can now choose a sound that will be played when you press the button. Choosing the playback device allows you to send the sound directly to your stream.
- Different sounds for short-press and long-press
- New option to create a text file that shows both a title and the value of the counter. Useful to display as a source on stream.
- Counter is read from file on keypress to ensure an accurate value
- Counter is now refreshed every few seconds. Modifying the file will update the value on the key.
- Improved UI to allow setting the filename using a file picker

## New in v1.5
- Select what type of action a short / long press will perform (Add/Subtract/Multiply/Divide)
- Supports increments other than 1
- Supports an initial value other than 0
- :new: With the new features you can now easily create a Count-Down Counter

### Download

* [Download plugin](https://github.com/BarRaider/streamdeck-streamcounter/releases)

## I found a bug, who do I contact?
For support please contact the developer. Contact information is available at https://barraider.com

## I have a feature request, who do I contact?
Please contact the developer. Contact information is available at https://barraider.com

## Dependencies
* Uses StreamDeck-Tools by BarRaider: [![NuGet](https://img.shields.io/nuget/v/streamdeck-tools.svg?style=flat)](https://www.nuget.org/packages/streamdeck-tools)
* Uses [Easy-PI](https://github.com/BarRaider/streamdeck-easypi) by BarRaider - Provides seamless integration with the Stream Deck PI (Property Inspector) 