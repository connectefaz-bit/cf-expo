# CF Explorer (ASP.NET Core MVC)

A from-scratch ASP.NET Core MVC rewrite of the CF Explorer app: enter a Codeforces
handle to see an A-H grid of every finished contest, with solved/unsolved
problems highlighted, plus contest-type filtering and search. All data comes
live from the public Codeforces API (`https://codeforces.com/apiHelp`), called
server-side.

## Run in Visual Studio

1. Requires the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
2. Open `CfMvc.sln` in Visual Studio (or `code .` / `dotnet run` from this folder
   for VS Code / CLI).
3. Press F5 (or run `dotnet run`). It launches on `http://localhost:5275` using
   the profile in `Properties/launchSettings.json`.

No database, API keys, or extra setup needed — it's a single self-contained
project.

## Run from the command line

```bash
dotnet restore
dotnet run
```

## Project layout

- `Controllers/HomeController.cs` — handles the handle lookup, filtering, and search query params
- `Services/CodeforcesApiService.cs` — HTTP client for the Codeforces API, with in-memory caching
- `Services/TableBuilder.cs` — builds the A-H grid and solved/unsolved status per cell
- `Services/EnrichmentService.cs` — resolves "split round" contests that share problems across two simultaneous contest IDs
- `Views/Home/Index.cshtml` — the single-page UI (Razor + plain CSS, no client-side framework)

## Deploying

This is a standard ASP.NET Core app — it binds to `$PORT` if that environment
variable is set (used on Replit and most cloud hosts), otherwise it falls back
to the Visual Studio / `dotnet run` launch profile. Deploy it to any host that
runs .NET 8, e.g. `dotnet publish -c Release` then `dotnet CfMvc.dll` on the
target server, or push it to Render/Railway with a .NET buildpack.
