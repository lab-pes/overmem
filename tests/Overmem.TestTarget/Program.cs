using System.Runtime.InteropServices;
using System.Text.Json;

var intAddress = Marshal.AllocHGlobal(sizeof(int));
var doubleAddress = Marshal.AllocHGlobal(sizeof(double));
var utf8Bytes = System.Text.Encoding.UTF8.GetBytes("overmem-target\0");
var utf8Address = Marshal.AllocHGlobal(utf8Bytes.Length);
var level1PointerAddress = Marshal.AllocHGlobal(IntPtr.Size);
var level2PointerAddress = Marshal.AllocHGlobal(IntPtr.Size);
var patternBytes = new byte[] { 0xDE, 0xAD, 0xBE, 0xEF, 0x44, 0x99 };
var patternAddress = Marshal.AllocHGlobal(patternBytes.Length);
var mutableIntAddress = Marshal.AllocHGlobal(sizeof(int));

Marshal.WriteInt32(intAddress, 1337);
Marshal.WriteInt32(mutableIntAddress, 0);
Marshal.Copy(BitConverter.GetBytes(42.5d), 0, doubleAddress, sizeof(double));
Marshal.Copy(utf8Bytes, 0, utf8Address, utf8Bytes.Length);
Marshal.WriteIntPtr(level1PointerAddress, intAddress);
Marshal.WriteIntPtr(level2PointerAddress, level1PointerAddress);
Marshal.Copy(patternBytes, 0, patternAddress, patternBytes.Length);

using var self = System.Diagnostics.Process.GetCurrentProcess();
var mainModule = self.MainModule ?? throw new InvalidOperationException("Main module not available.");
var moduleRelativeOffset = checked(level2PointerAddress.ToInt64() - mainModule.BaseAddress.ToInt64());

var payload = new
{
	pid = Environment.ProcessId,
	values = new
	{
		int32 = new { address = unchecked((ulong)intAddress.ToInt64()), value = 1337 },
		mutableInt = new { address = unchecked((ulong)mutableIntAddress.ToInt64()), frozenValue = 777, mutationIntervalMs = 50 },
		@double = new { address = unchecked((ulong)doubleAddress.ToInt64()), value = 42.5d },
		utf8 = new { address = unchecked((ulong)utf8Address.ToInt64()), size = utf8Bytes.Length, value = "overmem-target" },
		pointerChain = new { baseAddress = unchecked((ulong)level2PointerAddress.ToInt64()), offsets = new long[] { 0, 0 }, resolvedAddress = unchecked((ulong)intAddress.ToInt64()) },
		modulePointerChain = new { moduleName = mainModule.ModuleName, baseOffset = moduleRelativeOffset, offsets = new long[] { 0, 0 }, resolvedAddress = unchecked((ulong)intAddress.ToInt64()) },
		pattern = new { address = unchecked((ulong)patternAddress.ToInt64()), pattern = "DE AD BE EF 44 99", wildcardPattern = "DE AD ?? EF 44 99" },
	},
};

Console.WriteLine(JsonSerializer.Serialize(payload));
Console.Out.Flush();

var shutdown = new ManualResetEventSlim(false);
using var mutatorCancellation = new CancellationTokenSource();
var mutationTask = Task.Run(async () =>
{
	var value = 0;
	try
	{
		while (!mutatorCancellation.IsCancellationRequested)
		{
			Marshal.WriteInt32(mutableIntAddress, value++);
			await Task.Delay(50, mutatorCancellation.Token);
		}
	}
	catch (OperationCanceledException)
	{
	}
});

Console.CancelKeyPress += (_, args) =>
{
	args.Cancel = true;
	shutdown.Set();
};
AppDomain.CurrentDomain.ProcessExit += (_, _) => shutdown.Set();

shutdown.Wait();
mutatorCancellation.Cancel();
await mutationTask;

Marshal.FreeHGlobal(utf8Address);
Marshal.FreeHGlobal(doubleAddress);
Marshal.FreeHGlobal(intAddress);
Marshal.FreeHGlobal(level1PointerAddress);
Marshal.FreeHGlobal(level2PointerAddress);
Marshal.FreeHGlobal(patternAddress);
Marshal.FreeHGlobal(mutableIntAddress);
