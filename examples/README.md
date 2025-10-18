# Memux Examples

This folder contains example integrations and usage patterns for Memux.

## Dark Souls Integration

`DarkSoulsIntegration.cs` demonstrates how to create a game-specific integration for Memux. This example shows:

- How to map abstract actions to game-specific inputs
- How to detect game-specific UI elements and states
- How to provide context hints for perception
- How to handle game-specific launch requirements (Steam integration)

## Usage

To use these examples:

1. Copy the integration class to your project
2. Modify the constants and mappings for your target application
3. Register your program in the database using the UI or programmatically
4. Use the integration class to provide game-specific context to Memux

## Creating New Integrations

When creating a new integration:

1. **Identify the target process**: Process name, window title patterns
2. **Map actions**: Abstract actions → specific inputs (keys, mouse, controller)
3. **Detect states**: Game-specific UI elements, menus, loading screens
4. **Handle launch**: Command line arguments, admin requirements, dependencies
5. **Provide context**: Game-specific hints for perception and skill selection

## Example Program Registration

```csharp
// Register a new program in the database
var db = new MemuxDatabase();
db.UpsertProgram(
    id: null,
    name: "my_game",
    exePath: @"C:\Games\MyGame\MyGame.exe",
    processName: "MyGame",
    windowTitlePattern: "My Game Title",
    launchArgs: "-windowed",
    runAsAdmin: false,
    preferredWidth: 1920,
    preferredHeight: 1080,
    isDefault: false
);
```
