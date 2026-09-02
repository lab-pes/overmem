# P7 - reproducible commands

```powershell
# from repository root
dotnet build Overmem.slnx
dotnet test Overmem.slnx --no-build
dotnet test tests\Overmem.Extensions.Pes2021.Tests\Overmem.Extensions.Pes2021.Tests.csproj --no-build --filter "FullyQualifiedName~Pes2021PlayerTransactionTests"
```

The transaction tests use `FakeProcessMemoryGateway` so no live process is
attached. PES2021 is not in the default allowlist; an override is required to
write against it.