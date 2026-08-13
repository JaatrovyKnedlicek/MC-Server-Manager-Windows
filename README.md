
# MC Server Manager 3

A lightweight Windows desktop manager for running and managing local Minecraft server instances. The application is a Windows Forms app written in C# and targets .NET 9.
![screenshot](https://github.com/JaatrovyKnedlicek/MC-Server-Manager-Windows/blob/main/Sn%C3%ADmka%20obrazovky%202026-08-13%20025506.png?raw=true)

## Features

- Manage multiple server instances stored under the `servers` folder
- Detects `server.properties` for port when available
- Minimal, easy-to-use interface for starting/stopping servers and editing properties

## Prerequisites

- .NET 9 runtime / SDK
- Windows (Windows Forms desktop app)
- Visual Studio 2022/2023 or `dotnet` CLI for building

## Instalation
To install this app:

 1. Go to the Releases tab
 2. Download the `win-x64.zip` file
 3. Unzip it and run the `.exe` file

To run this app, you need .NET 9 Runtime installed.

## Build and run

Using Visual Studio:
1. Open the solution in Visual Studio
2. Build the solution
3. Run the `MC Server Manager 3` project

Using `dotnet` CLI:

1. From the repository root, run:

```
dotnet build
# or run the specific project
dotnet run --project "./path/to/MC-Server-Manager-Windows.csproj"
```

Note: replace the project path with the actual `.csproj` file location in the repository.

## Servers folder and configuration

Server instances are stored under the `servers` directory located next to the application binary. Each server is a folder that may contain a `config.json` file. If `config.json` is missing, the manager will attempt a best-effort detection using `server.properties`.

A `config.json` has the following shape:

```json
{
  "Name": "My Server",
  "Version": "1.20.1",
  "Port": 25565,
  "RamMB": 2048,
  "PropertiesFileName": "server.properties",
  "EulaAccepted": true
}
```

If `PropertiesFileName` is set, it should be the filename of the properties file that sits inside the server folder.

To add a server manually, create a new folder under `servers` and add a `config.json` like the example above, or place an existing server folder (with `server.properties`) into the `servers` directory and the manager will attempt to infer the port.

## Usage notes

- The application expects servers to run locally (the default IP is `127.0.0.1`).
- RAM allocation is stored in the server config (`RamMB`). The manager uses this value when launching server processes.
- EULA acceptance is tracked in the config (`EulaAccepted`) and must be set to `true` for servers that require it.

## Contributing

Contributions, bug reports and pull requests are welcome. Please fork the repository and open a pull request with a clear description of changes.

## License

Copyright 2026 Ján Repka

Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the “Software”), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED “AS IS”, WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
