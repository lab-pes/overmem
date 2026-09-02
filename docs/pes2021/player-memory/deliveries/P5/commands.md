# P5 - reproducible commands

```powershell
# from repository root
dotnet build Overmem.slnx
dotnet test Overmem.slnx --no-build
dotnet test tests\Overmem.Extensions.Pes2021.Tests\Overmem.Extensions.Pes2021.Tests.csproj --no-build --filter "FullyQualifiedName~Pes2021PlayerCliSurfaceTests"
```

The CLI parser test runs against the in-memory option dictionary. The
end-to-end discovery test uses `FakeProcessMemoryGateway`; no live PES2021.exe
is attached.