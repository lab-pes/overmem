# P0 - reproducible commands

```powershell
# from repository root
dotnet build tests\Overmem.Extensions.Pes2021.Tests\Overmem.Extensions.Pes2021.Tests.csproj
dotnet test tests\Overmem.Extensions.Pes2021.Tests\Overmem.Extensions.Pes2021.Tests.csproj --no-build --filter "FullyQualifiedName~Pes2021PlayerContractsTests|FullyQualifiedName~Pes2021SourceManifestTests"
```

Expected: all P0 tests pass (contracts + source manifest). Build must succeed
with zero warnings or errors.
