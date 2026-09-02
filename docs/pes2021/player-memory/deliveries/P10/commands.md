# P10 - reproducible commands

```powershell
# full verification before requesting authorization for P6 / P8
dotnet build Overmem.slnx
dotnet test Overmem.slnx --no-build
dotnet test tests\Overmem.Extensions.Pes2021.Tests\Overmem.Extensions.Pes2021.Tests.csproj --no-build `
    --filter "FullyQualifiedName~Pes2021Player|FullyQualifiedName~Pes2021PlayerCliSurfaceTests"
```

Expected: 78 player-memory tests pass, full solution build is clean (0 warnings,
0 errors).