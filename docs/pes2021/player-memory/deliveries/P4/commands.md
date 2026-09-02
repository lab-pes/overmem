# P4 - reproducible commands

```powershell
# from repository root
dotnet build Overmem.slnx
dotnet test Overmem.slnx --no-build
dotnet test tests\Overmem.Extensions.Pes2021.Tests\Overmem.Extensions.Pes2021.Tests.csproj --no-build --filter "FullyQualifiedName~Pes2021PlayerCatalogTests"
```

The exporter test writes to `%TEMP%` and cleans up after itself. No live
process is required.