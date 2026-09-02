# P9 - reproducible commands

```powershell
# from repository root
dotnet build Overmem.slnx
dotnet test Overmem.slnx --no-build
dotnet test tests\Overmem.Extensions.Pes2021.Tests\Overmem.Extensions.Pes2021.Tests.csproj --no-build --filter "FullyQualifiedName~Pes2021PlayerWritePolicyTests"
```

The policy tests are pure; they need no process memory gateway and no
process attachment.