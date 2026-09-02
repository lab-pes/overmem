# P3 - reproducible commands

```powershell
# from repository root
dotnet build Overmem.slnx
dotnet test Overmem.slnx --no-build
dotnet test tests\Overmem.Extensions.Pes2021.Tests\Overmem.Extensions.Pes2021.Tests.csproj --no-build --filter "FullyQualifiedName~Pes2021PlayerDiscoveryTests"
```

P3 tests use a synthetic in-memory gateway (`FakeProcessMemoryGateway`) and a
deterministic clock (`FakeSystemClock`). They do not require Windows or a
running PES2021.exe process.