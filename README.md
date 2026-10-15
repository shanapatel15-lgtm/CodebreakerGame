# CodebreakerGame (ASP.NET Core MVC)

A medium-level C# MVC number-code puzzle built for Visual Studio.

## Requirements
- Visual Studio 2022 with the ASP.NET and web development workload
- .NET 8 SDK (or change `TargetFramework` in the project file to an installed supported version)

## Run
1. Extract the ZIP.
2. Open `CodebreakerGame.csproj` in Visual Studio.
3. Allow NuGet restore to finish.
4. Press Ctrl+F5 (or click the green Run button).
5. Enter four different digits and try to crack the code in eight guesses.

## Rules
- Exact: correct digit in the correct position.
- Misplaced: digit exists in the code but is in a different position.
- Four distinct digits are required.
- Session stores game state temporarily; no database or persistent leaderboard is included.

## Project structure
- `Controllers/GameController.cs`: game logic and MVC actions
- `Models/GuessRecord.cs`: history row model
- `Models/GameViewModel.cs`: data sent to the view
- `Views/Game/Index.cshtml`: game UI
