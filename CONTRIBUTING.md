# Contributing

Thanks for considering a contribution!

## Development setup

1. Install the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).
   `dotnet --list-sdks` should show `8.0.x`.
2. Clone this repo.
3. Open `TyriaUploader.sln` in Visual Studio 2022 (Community is free) or
   JetBrains Rider, or just use `dotnet build`.

## Running

```cmd
dotnet run --project TyriaUploader
```

For a production-like single-file build:

```cmd
dotnet publish TyriaUploader -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true
```

The exe lands at `TyriaUploader/bin/Release/net8.0-windows/win-x64/publish/TyriaUploader.exe`.

## Testing the OAuth flow against a local server

The companion API server (Tyria Planner) lives in a separate repository. To run
the uploader against a local API:

1. Run the API on port 3000.
2. Run the web app on port 5173 (the OAuth approve page lives there).
3. In the uploader Settings, set **Server URL** to `http://localhost:3000`.
4. Click **Sign In…**. The browser opens to the local web app's
   `/oauth/authorize` page.

## Code style

* Follow `.editorconfig` (C# 4-space, CRLF, expression-bodied where natural).
* Comments explain *why*, names should be self-documenting.
* No `async void` outside event handlers.
* Always `await`, no `.Result` / `.Wait()` on UI thread.
* Throw on unexpected state; never swallow exceptions silently except in
  cleanup paths (logged to the file logger).

## Pull requests

* Keep PRs small and single-purpose.
* If you change anything in `OAuth/` or `Api/`, call it out in the PR
  description so security reviewers know to look closely.
* New dependencies need justification, every NuGet package is one more
  thing for downstream auditors to vet.

## Security issues

Don't open a public issue. See [SECURITY.md](SECURITY.md).
