using System;
using System.Reflection;
using System.Linq;

class Program {
    static void Main() {
        var paths = new string[] { typeof(object).Assembly.Location };
        var resolver = new PathAssemblyResolver(paths);
        using var ctx = new MetadataLoadContext(resolver);
        var ass = ctx.LoadFromAssemblyPath(@"C:\Users\Willian\.nuget\packages\modelcontextprotocol.aspnetcore\1.3.0\lib\net8.0\ModelContextProtocol.AspNetCore.dll");
        foreach(var t in ass.GetExportedTypes()) {
            Console.WriteLine(t.Name);
            foreach(var m in t.GetMethods(BindingFlags.Public | BindingFlags.Static)) {
                Console.WriteLine("  " + m.Name);
            }
        }
    }
}
