using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;

// Produces a candidate ONLY. Never writes into the Steam installation.
class PreparePatch {
    static System.Collections.Generic.IEnumerable<TypeDefinition> AllTypes(TypeDefinition t) {
        yield return t;
        foreach (var n in t.NestedTypes) foreach (var child in AllTypes(n)) yield return child;
    }
    static string Fingerprint(MethodDefinition m, int skip = 0) {
        return m.FullName + (m.HasBody ? string.Join("\n", m.Body.Instructions.Skip(skip).Select(i => i.OpCode.Name + " " +
            (i.Operand is Instruction ? "branch:" + (m.Body.Instructions.IndexOf((Instruction)i.Operand)-skip) :
             i.Operand is Instruction[] ? string.Join(",", ((Instruction[])i.Operand).Select(j => (m.Body.Instructions.IndexOf(j)-skip).ToString()).ToArray()) :
             i.Operand == null ? "" : i.Operand.ToString())).ToArray()) : "");
    }
    static int Main(string[] args) {
        if (args.Length != 3) throw new ArgumentException("source.dll output-directory installed-enable-flag-path");
        string source = Path.GetFullPath(args[0]);
        string output = Path.GetFullPath(args[1]);
        if (Path.GetDirectoryName(source) == output) throw new Exception("Cannot write to game directory");
        Directory.CreateDirectory(output);
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Path.GetDirectoryName(source));
        using (var a = AssemblyDefinition.ReadAssembly(source, new ReaderParameters { AssemblyResolver = resolver })) {
            var mod = a.MainModule;
            var shop = mod.Types.Single(t => t.FullName == "BongoCat.Shop");
            if (shop.Methods.Any(m => m.Name == "Update") || shop.Fields.Any(f => f.Name == "_localAutoClaimNext"))
                throw new Exception("Unexpected existing hook; stop");
            var originalMethods = mod.Types.SelectMany(AllTypes).SelectMany(t => t.Methods).ToDictionary(m => m.FullName, m => Fingerprint(m));
            var time = mod.GetMemberReferences().OfType<MethodReference>().First(m => m.DeclaringType.FullName == "UnityEngine.Time" && m.Name == "get_unscaledTime");
            var debug = mod.GetMemberReferences().OfType<MethodReference>().First(m => m.DeclaringType.FullName == "UnityEngine.Debug" && m.Name == "Log" && m.Parameters.Count == 1 && m.Parameters[0].ParameterType.FullName == "System.Object");
            var active = mod.GetMemberReferences().OfType<MethodReference>().First(m => m.DeclaringType.FullName == "UnityEngine.GameObject" && m.Name == "get_activeSelf");
            var next = new FieldDefinition("_localAutoClaimNext", FieldAttributes.Private, mod.TypeSystem.Single);
            shop.Fields.Add(next);
            var retry = new FieldDefinition("_localAutoClaimRetryAt", FieldAttributes.Private, mod.TypeSystem.Single);
            shop.Fields.Add(retry);
            var last = new FieldDefinition("_localAutoClaimLastState", FieldAttributes.Private, mod.TypeSystem.String);
            shop.Fields.Add(last);
            var report = new MethodDefinition("LocalAutoClaimReport", MethodAttributes.Private | MethodAttributes.HideBySig, mod.TypeSystem.Void);
            report.Parameters.Add(new ParameterDefinition("state", ParameterAttributes.None, mod.TypeSystem.String));
            shop.Methods.Add(report);
            var ri = report.Body.GetILProcessor();
            var rr = ri.Create(OpCodes.Ret);
            ri.Emit(OpCodes.Ldarg_0); ri.Emit(OpCodes.Ldfld, last); ri.Emit(OpCodes.Ldarg_1);
            ri.Emit(OpCodes.Call, mod.ImportReference(typeof(string).GetMethod("op_Equality", new [] { typeof(string), typeof(string) })));
            ri.Emit(OpCodes.Brtrue, rr);
            ri.Emit(OpCodes.Ldarg_0); ri.Emit(OpCodes.Ldarg_1); ri.Emit(OpCodes.Stfld, last);
            var normalLabel = ri.Create(OpCodes.Ldstr, "[LocalAutoClaim normal] ");
            var concatLabel = ri.Create(OpCodes.Ldarg_1);
            ri.Emit(OpCodes.Ldarg_0); ri.Emit(OpCodes.Ldfld, shop.Fields.Single(f => f.Name == "_isEmoteShop"));
            ri.Emit(OpCodes.Brfalse, normalLabel);
            ri.Emit(OpCodes.Ldstr, "[LocalAutoClaim emote] "); ri.Emit(OpCodes.Br, concatLabel);
            ri.Append(normalLabel); ri.Append(concatLabel);
            ri.Emit(OpCodes.Call, mod.ImportReference(typeof(string).GetMethod("Concat", new [] { typeof(string), typeof(string) })));
            ri.Emit(OpCodes.Call, debug); ri.Append(rr);
            // Log the actual game result callback, not merely a dispatched attempt.
            var result = shop.Methods.Single(m => m.Name == "SetSuccessVisuals");
            var resultIl = result.Body.GetILProcessor();
            var firstResult = result.Body.Instructions[0];
            var failure = resultIl.Create(OpCodes.Ldstr, "FAILURE_CALLBACK");
            var resultLog = resultIl.Create(OpCodes.Call, report);
            var resultPrefix = new [] {
                resultIl.Create(OpCodes.Ldarg_0), resultIl.Create(OpCodes.Ldarg_1),
                resultIl.Create(OpCodes.Brfalse, failure), resultIl.Create(OpCodes.Ldstr, "SUCCESS_CALLBACK"),
                resultIl.Create(OpCodes.Br, resultLog), failure, resultLog
            };
            foreach (var ins in resultPrefix) resultIl.InsertBefore(firstResult, ins);
            var update = new MethodDefinition("Update", MethodAttributes.Private | MethodAttributes.HideBySig, mod.TypeSystem.Void);
            shop.Methods.Add(update);
            var il = update.Body.GetILProcessor();
            var ret = il.Create(OpCodes.Ret);
            var disabled = il.Create(OpCodes.Ldarg_0);
            var opening = il.Create(OpCodes.Ldarg_0);
            var notReady = il.Create(OpCodes.Ldarg_0);
            var failed = il.Create(OpCodes.Ldarg_0);
            var success = il.Create(OpCodes.Ldarg_0);
            var idle = il.Create(OpCodes.Ldarg_0);
            // Poll at most once a second. Delay retries by 30 sec after an attempt.
            il.Emit(OpCodes.Call, time);
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, next); il.Emit(OpCodes.Blt, ret);
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Call, time); il.Emit(OpCodes.Ldc_R4, 1f); il.Emit(OpCodes.Add); il.Emit(OpCodes.Stfld, next);
            il.Emit(OpCodes.Ldstr, Path.GetFullPath(args[2]));
            il.Emit(OpCodes.Call, mod.ImportReference(typeof(File).GetMethod("Exists", new [] { typeof(string) })));
            il.Emit(OpCodes.Brfalse, disabled);
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, shop.Fields.Single(f => f.Name == "_openingChest")); il.Emit(OpCodes.Brtrue, opening);
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Call, shop.Methods.Single(m => m.Name == "get_CanGetChest")); il.Emit(OpCodes.Brfalse, notReady);
            il.Emit(OpCodes.Call, time); il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, retry); il.Emit(OpCodes.Blt, ret);
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Call, time); il.Emit(OpCodes.Ldc_R4, 30f); il.Emit(OpCodes.Add); il.Emit(OpCodes.Stfld, retry);
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldstr, "ATTEMPT"); il.Emit(OpCodes.Call, report);
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Call, shop.Methods.Single(m => m.Name == "OnClick"));
            il.Emit(OpCodes.Br, ret);
            il.Append(notReady); il.Emit(OpCodes.Ldfld, shop.Fields.Single(f => f.Name == "_successObj")); il.Emit(OpCodes.Callvirt, active); il.Emit(OpCodes.Brtrue, success);
            il.Emit(OpCodes.Ldarg_0); il.Emit(OpCodes.Ldfld, shop.Fields.Single(f => f.Name == "_failObj")); il.Emit(OpCodes.Callvirt, active); il.Emit(OpCodes.Brtrue, failed);
            il.Emit(OpCodes.Br, idle);
            il.Append(disabled); il.Emit(OpCodes.Ldstr, "DISABLED"); il.Emit(OpCodes.Call, report); il.Emit(OpCodes.Br, ret);
            il.Append(opening); il.Emit(OpCodes.Ldstr, "WAITING_FOR_SERVER"); il.Emit(OpCodes.Call, report); il.Emit(OpCodes.Br, ret);
            il.Append(success); il.Emit(OpCodes.Ldstr, "SUCCESS_VISUAL"); il.Emit(OpCodes.Call, report); il.Emit(OpCodes.Br, ret);
            il.Append(failed); il.Emit(OpCodes.Ldstr, "FAILURE_VISUAL"); il.Emit(OpCodes.Call, report); il.Emit(OpCodes.Br, ret);
            il.Append(idle); il.Emit(OpCodes.Ldstr, "NOT_READY_OR_INSUFFICIENT_POINTS"); il.Emit(OpCodes.Call, report); il.Emit(OpCodes.Br, ret);
            il.Append(ret);
            string dest = Path.Combine(output, "Assembly-CSharp.dll");
            a.Write(dest);
            using (var check = AssemblyDefinition.ReadAssembly(dest, new ReaderParameters { AssemblyResolver = resolver })) {
                var methods = check.MainModule.Types.SelectMany(AllTypes).SelectMany(t => t.Methods).ToDictionary(m => m.FullName, m => Fingerprint(m, m.FullName == result.FullName ? resultPrefix.Length : 0));
                foreach (var entry in originalMethods)
                    if (!methods.ContainsKey(entry.Key) || methods[entry.Key] != entry.Value) throw new Exception("Original method changed: " + entry.Key);
                if (methods.Count != originalMethods.Count + 2) throw new Exception("Unexpected method count");
            }
            Console.WriteLine("Candidate prepared. All " + originalMethods.Count + " original method bodies preserved; SetSuccessVisuals has an added result-log prefix only.");
            Console.WriteLine(dest);
        }
        return 0;
    }
}
