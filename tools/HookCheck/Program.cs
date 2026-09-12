// HookCheck: verifies that every Valheim member BetterNetworking hooks or reflects on
// still exists in a given assembly_valheim.dll, and prints the IL facts the patches rely on.
// Usage: HookCheck <assembly_valheim.dll> <com.rlabrecque.steamworks.net.dll>
using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;

var valheim = new Asm(args[0]);
var steam = new Asm(args[1]);

// Optional: HookCheck <valheim.dll> <steam.dll> --il Type.Method [Type.Method ...]
if (args.Length > 2 && args[2] == "--il") {
    foreach (var spec in args.Skip(3)) valheim.PrintIl(spec, "full IL");
    return;
}

Console.WriteLine("== Hook targets (method: signature) ==");
string[] methods = {
    "ZNet.IsDedicated", "ZNet.RPC_PeerInfo", "ZNet.OnNewConnection", "ZNet.Shutdown", "ZNet.Disconnect",
    "ZNet.GetPeers", "ZNet.IsServer",
    "FejdStartup.ParseServerArguments",
    "ZDOMan.AddPeer", "ZDOMan.RPC_ZDOData", "ZDOMan.SendZDOToPeers2",
    "ZSteamSocket.GetSendQueueSize", "ZSteamSocket.RegisterGlobalCallbacks", "ZSteamSocket.SendQueuedPackages",
    "ZSteamSocket.Recv", "ZSteamSocket.IsConnected", "ZSteamSocket.Flush", "ZSteamSocket.GetHostName",
    "ZPlayFabSocket.GetSendQueueSize", "ZPlayFabSocket.Dispose", "ZPlayFabSocket.LateUpdate", "ZPlayFabSocket..ctor",
    "PlayFabZLibWorkQueue.DoCompress", "PlayFabZLibWorkQueue.DoUncompress",
    "PlayFabZLibWorkQueue.UncompressOnThisThread", "PlayFabZLibWorkQueue.Execute",
    "ZPlayFabMatchmaking.LookupPublicIP",
    "ZRpc.Register", "ZRpc.Invoke", "ZPackage..ctor", "ZPackage.GetArray",
};
foreach (var m in methods) valheim.PrintMethods(m);
foreach (var m in new[] { "Steamworks.SteamNetworkingUtils.SetConfigValue", "Steamworks.SteamNetworkingUtils.GetConfigValue",
                          "Steamworks.SteamGameServerNetworkingUtils.SetConfigValue", "Steamworks.SteamGameServerNetworkingUtils.GetConfigValue" })
    steam.PrintMethods(m);

Console.WriteLine("\n== Fields injected or reflected (type: field) ==");
string[] fields = {
    "ZNet.m_onlineBackend", "ZSteamSocket.m_sendQueue", "ZPlayFabSocket.m_inFlightQueue", "ZPlayFabSocket.m_zlibWorkQueue",
    "ZPlayFabSocket/InFlightQueue.Bytes", "PlayFabZLibWorkQueue.m_inCompress", "PlayFabZLibWorkQueue.m_outCompress",
    "PlayFabZLibWorkQueue.m_inDecompress", "PlayFabZLibWorkQueue.m_outDecompress",
    "PlayFabZLibWorkQueue.s_workersMutex", "PlayFabZLibWorkQueue.s_workers",
    "ZNetPeer.m_rpc", "ZNetPeer.m_socket", "ZNetPeer.m_server", "ZNetPeer.m_playerName",
};
foreach (var f in fields) valheim.PrintField(f);

Console.WriteLine("\n== IL facts the patches depend on ==");
valheim.PrintIl("ZNet.IsDedicated", "IsDedicated transpiler looks for ldc.i4.1 (true on the dedicated build)");
valheim.PrintIl("ZNet.RPC_PeerInfo", "Player limit transpiler replaces ldc.i4.s 10", onlyConstants: true);
valheim.PrintIl("ZSteamSocket.RegisterGlobalCallbacks", "Vanilla Steam send-rate config (mod overrides after this runs)", onlyConstants: true);
valheim.PrintCallers("GetSendQueueSize");
valheim.PrintCallers("SendZDOToPeers2");
valheim.PrintCallers("RegisterGlobalCallbacks");
valheim.PrintCallers("StartHost");

sealed class Asm {
    readonly MetadataReader md;
    readonly PEReader pe;
    readonly Dictionary<string, TypeDefinitionHandle> types = new();
    static readonly Dictionary<short, OpCode> ops = typeof(OpCodes).GetFields(BindingFlags.Public | BindingFlags.Static)
        .Select(f => (OpCode)f.GetValue(null)).ToDictionary(o => o.Value);

    public Asm(string path) {
        pe = new PEReader(File.OpenRead(path));
        md = pe.GetMetadataReader();
        foreach (var h in md.TypeDefinitions) types[FullName(h)] = h;
    }

    string FullName(TypeDefinitionHandle h) {
        var t = md.GetTypeDefinition(h);
        var name = md.GetString(t.Name);
        if (t.GetDeclaringType() is { IsNil: false } d) return FullName(d) + "/" + name;
        var ns = md.GetString(t.Namespace);
        return ns.Length > 0 ? ns + "." + name : name;
    }

    (string type, string member) Split(string s) {
        int i = s.EndsWith("..ctor") ? s.Length - 6 : s.LastIndexOf('.');
        return (s[..i], s[(i + 1)..]);
    }

    IEnumerable<MethodDefinitionHandle> Find(string typeName, string member) {
        if (!types.TryGetValue(typeName, out var th)) yield break;
        foreach (var mh in md.GetTypeDefinition(th).GetMethods())
            if (md.GetString(md.GetMethodDefinition(mh).Name) == member) yield return mh;
    }

    public void PrintMethods(string spec) {
        var (t, m) = Split(spec);
        if (!types.ContainsKey(t)) { Console.WriteLine($"MISSING TYPE   {spec}"); return; }
        var found = Find(t, m).ToList();
        if (found.Count == 0) { Console.WriteLine($"MISSING        {spec}"); return; }
        foreach (var mh in found) Console.WriteLine($"ok             {spec}{Signature(mh)}");
    }

    string Signature(MethodDefinitionHandle mh) {
        var def = md.GetMethodDefinition(mh);
        var sig = def.DecodeSignature(new Names(md), null);
        var names = def.GetParameters().Select(p => md.GetParameter(p)).Where(p => p.SequenceNumber > 0)
            .OrderBy(p => p.SequenceNumber).Select(p => md.GetString(p.Name)).ToList();
        var ps = sig.ParameterTypes.Select((ty, i) => ty + " " + (i < names.Count ? names[i] : "?"));
        var access = (def.Attributes & MethodAttributes.MemberAccessMask) == MethodAttributes.Public ? "public" : "nonpublic";
        var stat = def.Attributes.HasFlag(MethodAttributes.Static) ? " static" : "";
        return $"({string.Join(", ", ps)}) -> {sig.ReturnType}  [{access}{stat}]";
    }

    public void PrintField(string spec) {
        var (t, f) = Split(spec);
        if (!types.TryGetValue(t, out var th)) { Console.WriteLine($"MISSING TYPE   {spec}"); return; }
        foreach (var fh in md.GetTypeDefinition(th).GetFields()) {
            var fd = md.GetFieldDefinition(fh);
            if (md.GetString(fd.Name) == f) { Console.WriteLine($"ok             {spec} : {fd.DecodeSignature(new Names(md), null)}"); return; }
        }
        foreach (var ph in md.GetTypeDefinition(th).GetProperties()) {
            var pd = md.GetPropertyDefinition(ph);
            if (md.GetString(pd.Name) == f) { Console.WriteLine($"ok (property) {spec} : {pd.DecodeSignature(new Names(md), null).ReturnType}"); return; }
        }
        Console.WriteLine($"MISSING        {spec}");
    }

    List<(int offset, OpCode op, object arg)> Decode(MethodDefinitionHandle mh) {
        var res = new List<(int, OpCode, object)>();
        int rva = md.GetMethodDefinition(mh).RelativeVirtualAddress;
        if (rva == 0) return res;
        var r = pe.GetMethodBody(rva).GetILReader();
        while (r.RemainingBytes > 0) {
            int off = r.Offset;
            short code = r.ReadByte();
            if (code == 0xFE) code = unchecked((short)(0xFE00 | r.ReadByte()));
            var op = ops[code];
            object arg = null;
            switch (op.OperandType) {
                case OperandType.InlineNone: break;
                case OperandType.ShortInlineBrTarget: case OperandType.ShortInlineVar: arg = r.ReadByte(); break;
                case OperandType.ShortInlineI: arg = r.ReadSByte(); break;
                case OperandType.InlineVar: arg = r.ReadUInt16(); break;
                case OperandType.InlineI: case OperandType.InlineBrTarget: arg = r.ReadInt32(); break;
                case OperandType.InlineI8: arg = r.ReadInt64(); break;
                case OperandType.ShortInlineR: arg = r.ReadSingle(); break;
                case OperandType.InlineR: arg = r.ReadDouble(); break;
                case OperandType.InlineSwitch: int n = r.ReadInt32(); for (int i = 0; i < n; i++) r.ReadInt32(); break;
                case OperandType.InlineString: arg = "\"" + md.GetUserString(MetadataTokens.UserStringHandle(r.ReadInt32())) + "\""; break;
                default: arg = TokenName(r.ReadInt32()); break;
            }
            res.Add((off, op, arg));
        }
        return res;
    }

    string TokenName(int token) {
        var h = MetadataTokens.EntityHandle(token);
        try {
            switch (h.Kind) {
                case HandleKind.MethodDefinition: {
                    var d = md.GetMethodDefinition((MethodDefinitionHandle)h);
                    return FullName(d.GetDeclaringType()) + "::" + md.GetString(d.Name);
                }
                case HandleKind.MemberReference: {
                    var mr = md.GetMemberReference((MemberReferenceHandle)h);
                    string parent = mr.Parent.Kind switch {
                        HandleKind.TypeReference => md.GetString(md.GetTypeReference((TypeReferenceHandle)mr.Parent).Name),
                        HandleKind.TypeDefinition => FullName((TypeDefinitionHandle)mr.Parent),
                        _ => "?"
                    };
                    return parent + "::" + md.GetString(mr.Name);
                }
                case HandleKind.FieldDefinition: {
                    var f = md.GetFieldDefinition((FieldDefinitionHandle)h);
                    return FullName(f.GetDeclaringType()) + "::" + md.GetString(f.Name);
                }
                default: return h.Kind.ToString();
            }
        } catch { return "token"; }
    }

    public void PrintIl(string spec, string why, bool onlyConstants = false) {
        var (t, m) = Split(spec);
        Console.WriteLine($"-- {spec}: {why}");
        foreach (var mh in Find(t, m)) {
            var il = Decode(mh);
            Console.WriteLine($"   {il.Count} instructions");
            foreach (var (off, op, arg) in il) {
                bool isConst = op.Name.StartsWith("ldc.i4");
                bool isCall = op.Name.StartsWith("call") || op.Name == "newobj";
                if (onlyConstants && !isConst && !(isCall && arg is string s && (s.Contains("SetConfigValue") || s.Contains("Player")))) continue;
                Console.WriteLine($"   IL_{off:X4} {op.Name} {arg}");
            }
        }
    }

    // Every method that calls a method with this name, plus the int constants it loads (for queue/size logic).
    public void PrintCallers(string callee) {
        Console.WriteLine($"-- Callers of *::{callee}, with their int constants");
        foreach (var mh in md.MethodDefinitions) {
            var il = Decode(mh);
            if (!il.Any(x => (x.op.Name.StartsWith("call")) && x.arg is string s && s.EndsWith("::" + callee))) continue;
            var d = md.GetMethodDefinition(mh);
            var consts = il.Where(x => x.op.Name.StartsWith("ldc.i4") && x.op.Name != "ldc.i4.0")
                .Select(x => x.op.Name.StartsWith("ldc.i4.") && x.arg == null ? x.op.Name[7..] : x.arg?.ToString()).Distinct();
            Console.WriteLine($"   {FullName(d.GetDeclaringType())}::{md.GetString(d.Name)}  consts: {string.Join(" ", consts)}");
        }
    }
}

sealed class Names : ISignatureTypeProvider<string, object> {
    readonly MetadataReader md;
    public Names(MetadataReader md) { this.md = md; }
    public string GetPrimitiveType(PrimitiveTypeCode c) => c.ToString().ToLowerInvariant();
    public string GetTypeFromDefinition(MetadataReader r, TypeDefinitionHandle h, byte k) => r.GetString(r.GetTypeDefinition(h).Name);
    public string GetTypeFromReference(MetadataReader r, TypeReferenceHandle h, byte k) => r.GetString(r.GetTypeReference(h).Name);
    public string GetTypeFromSpecification(MetadataReader r, object c, TypeSpecificationHandle h, byte k) => "spec";
    public string GetSZArrayType(string e) => e + "[]";
    public string GetArrayType(string e, ArrayShape s) => e + "[,]";
    public string GetByReferenceType(string e) => "ref " + e;
    public string GetPointerType(string e) => e + "*";
    public string GetGenericInstantiation(string g, ImmutableArray<string> a) => g + "<" + string.Join(",", a) + ">";
    public string GetGenericTypeParameter(object c, int i) => "T" + i;
    public string GetGenericMethodParameter(object c, int i) => "M" + i;
    public string GetFunctionPointerType(MethodSignature<string> s) => "fnptr";
    public string GetModifiedType(string m, string u, bool r) => u;
    public string GetPinnedType(string e) => e;
}
