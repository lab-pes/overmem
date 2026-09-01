# P2 - reproducible commands

```powershell
# from repository root
dotnet build Overmem.slnx
dotnet test Overmem.slnx --no-build
dotnet test tests\Overmem.Extensions.Pes2021.Tests\Overmem.Extensions.Pes2021.Tests.csproj --no-build --filter "FullyQualifiedName~Pes2021PlayerParserTests|FullyQualifiedName~Pes2021PlayerBitfieldCodecTests"
```

Expected: all tests pass. Build must succeed with zero warnings or errors.

Pure tests need no Windows process: every P2 test runs against a synthetic
380-byte span built from the profile.