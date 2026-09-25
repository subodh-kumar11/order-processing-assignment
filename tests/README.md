# Integration tests

From the repository root, run dotnet build -c Release, followed by node --test tests/api.test.mjs. On Windows, scripts/Test.ps1 performs both steps. Tests use isolated temporary storage and real HTTP requests.
