# Deobfuscation Map Editor (DME)

Deobfuscation Map Editor (DME) is a C# tool designed to manage and edit obfuscation maps, typically used for deobfuscating class and method names in games. This editor helps you search, add, remove, clean, and update obfuscated names, making it easier to work with deobfuscated assets.

## Features

- **Search Obfuscated/identifiers**: Search for obfuscated or real names to find and edit entries.
- **Add New Entries**: Add new obfuscated names with their corresponding real names.
- **Update identifiers**: Edit all obfuscated names pointing to a specific real name and update them to a new real name.
- **Clean Invalid Data**: Clean up invalid data entries that may not be valid C# identifiers.
- **Reload and Save**: Reload the CSV file and save the map to disk, including gzipped support for file compression.
