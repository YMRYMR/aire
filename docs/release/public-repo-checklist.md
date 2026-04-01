# Public Repo Checklist

## Source tree

- build outputs are ignored
- local data and temp folders are ignored
- no API keys, tokens, or personal data are present
- installer or machine-local helper scripts are either removed or clearly intentional

## Documentation

- [README.md]README.md) matches the current architecture and build steps
- [CONTRIBUTING.md]CONTRIBUTING.md) tells contributors how to build, test, and navigate the repo
- [SECURITY.md]SECURITY.md) describes how to report issues
- architecture docs reflect the current `UI -> Application -> Domain -> Infrastructure` direction

## Validation

- `dotnet build .\aire.sln -m:1`
- `dotnet test .\Aire.Tests\Aire.Tests.csproj --no-build`
- `dotnet test .\Aire.Tests\Aire.Tests.csproj --collect:"XPlat Code Coverage"`
- manual smoke checklist run from [manual-smoke-checklist.md]docs/testing/manual-smoke-checklist.md)

## Git

- repository is initialized
- branch naming and ignore rules are in place
- first public commit excludes generated artifacts and local state

## Recommendation

- the current architecture is good enough to publish without a physical multi-project split
- if stricter compile-time boundaries are desired later, treat that as a follow-up change after the first public release
