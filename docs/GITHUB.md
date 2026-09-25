# GitHub and local workflow

Repository: https://github.com/subodh-kumar11/order-processing-assignment

The repository was created as private. Before sending its URL to an interviewer, grant the reviewer access in GitHub repository Settings > Collaborators, or deliberately change its visibility if you prefer public submission. A private URL alone does not give reviewers access.

## Get a fresh local copy

With Git and the .NET 10 SDK installed (Node.js 24 is needed only for tests):

```powershell
git clone https://github.com/subodh-kumar11/order-processing-assignment.git
cd order-processing-assignment
.\scripts\Run.ps1
```

Authenticate using GitHub's normal sign-in when Git requests it. Do not paste passwords or access tokens into source files or documentation. For a private repository, downloads and clones require authorized access.

In a second PowerShell terminal inside the repository:

```powershell
.\scripts\Demo.ps1
.\scripts\Test.ps1
```

The demo exercises the API at http://localhost:5080. The default background interval is five minutes. Tests launch their own isolated servers and use temporary data. Stop the foreground API with Ctrl+C.

The PowerShell scripts temporarily use repository-local SDK settings to avoid machine-specific NuGet configuration problems, then restore the shell's original environment. Generated state lives under ignored .local/, bin/, obj/ and data/ directories.

If organizational execution policy blocks scripts, follow your organization's policy. The equivalent direct commands are in README.md; do not disable machine security settings to run this project.

## Make and publish a change

```powershell
.\scripts\Test.ps1
git status
git add .
git commit -m "Describe the change"
git push origin main
```

The GitHub Actions workflow builds the project and runs the HTTP integration suite. Check the Actions tab before sharing the result.

## If publishing an extracted ZIP to a different empty repository

A ZIP contains source files, not Git history. Create an empty repository in your own GitHub account without an initial README, license or .gitignore, then run these commands from the extracted project root:

```powershell
git init -b main
git add .
git commit -m "Add order processing assignment"
git remote add origin https://github.com/YOUR_USERNAME/YOUR_REPOSITORY.git
git push -u origin main
```

The prepared local project already has Git history; prefer cloning the published repository for further work.
