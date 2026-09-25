# Verification record

Executed locally on Windows with .NET SDK 10.0.400 and Node.js 24.19.0.

- Release build: passed, 0 warnings, 0 errors.
- `node --test tests/api.test.mjs`: passed, 10 reported tests, 0 failures, 0 skips (one parent test plus nine subtests).
- The HTTP tests ran against the compiled application, restarted it with the same temporary data file, and exercised its actual periodic background worker using a one-second test interval.

The initial restore failed because the sandbox denied access to the user-level NuGet configuration. The correction was to use workspace-local `APPDATA`, `DOTNET_CLI_HOME`, and `NUGET_PACKAGES` environment paths for the build, together with the checked-in NuGet.Config. No package download was required. This was an environment issue, not an application test failure.

Commands used after setting those environment paths:

```sh
dotnet restore OrderProcessing.csproj --configfile NuGet.Config
dotnet build OrderProcessing.csproj -c Release --no-restore
node --test tests/api.test.mjs
```

Not executed during the initial local verification: Docker image build, GitHub Actions on a remote runner, extended load testing, power-loss recovery, or a wall-clock five-minute wait. The default interval is 300 seconds in code; the integration suite overrides it to one second to verify the same worker promptly. Persistence failure handling is implemented but has not been tested with injected disk faults.

## Follow-up verification

The Windows scripts were exercised: Test.ps1 completed a Release build with zero warnings/errors and all 10 reported tests passing; Run.ps1 started the API on port 5080; Demo.ps1 completed creation, retrieval, delivery, rejected cancellation (409), pending cancellation and filtered listing against isolated demonstration storage. These checks used the same production code.


## GitHub verification and full-interval observation

The published repository's Build and test workflow completed successfully on Ubuntu:
https://github.com/subodh-kumar11/order-processing-assignment/actions/runs/36189248314

Verified commit: 54e59a4de548e53dcf274d696b4da3a2ba896383. The run completed in 29 seconds. GitHub emitted a non-blocking warning that the v4 setup/checkout actions target deprecated Node.js 20 and are being forced to use Node.js 24. The application integration tests use Node.js 24 as configured.

The local demonstration server also remained running long enough for its default 300-second worker tick; its log reported moving the one remaining pending demo order to PROCESSING. The server was then stopped.

Publication used the signed-in GitHub browser because the connector could not access the newly created private repository and shell Git authentication was unavailable. The remote has its own browser-created commit history. Clone the GitHub repository for subsequent pushes; the supplied ZIP contains the complete source without Git metadata. Docker execution and injected storage-failure testing remain unverified.
