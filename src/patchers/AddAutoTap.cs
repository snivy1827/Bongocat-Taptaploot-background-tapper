using System;
using System.IO;
using System.Linq;
using Mono.Cecil;
using Mono.Cecil.Cil;
class AddAutoTap {
    static System.Collections.Generic.IEnumerable<TypeDefinition> Types(TypeDefinition t) {
        yield return t;
        foreach(var n in t.NestedTypes)foreach(var child in Types(n))yield return child;
    }
    static string Body(MethodDefinition m) {
        return m.FullName+(m.HasBody?string.Join("\n",m.Body.Instructions.Select(i=>i.OpCode.Name+" "+
            (i.Operand is Instruction?"branch:"+m.Body.Instructions.IndexOf((Instruction)i.Operand):
             i.Operand is Instruction[]?string.Join(",",((Instruction[])i.Operand).Select(j=>m.Body.Instructions.IndexOf(j).ToString()).ToArray()):
             i.Operand==null?"":i.Operand.ToString())).ToArray()):"");
    }
    static int Main(string[] args) {
        if(args.Length!=3)throw new ArgumentException("verified-chest.dll helper.dll output-directory");
        using(var game=AssemblyDefinition.ReadAssembly(args[0]))
        using(var helper=AssemblyDefinition.ReadAssembly(args[1])) {
            if(Path.GetFullPath(Path.Combine(args[2],"Assembly-CSharp.dll"))==Path.GetFullPath(args[0]))throw new Exception("Output cannot overwrite input");
            var originals=game.MainModule.Types.SelectMany(Types).SelectMany(t=>t.Methods).ToDictionary(m=>m.FullName,Body);
            var cat=game.MainModule.Types.Single(t=>t.FullName=="BongoCat.Cat");
            if(cat.Methods.Any(m=>m.Name=="Update"))throw new Exception("Unexpected Cat.Update");
            var tick=helper.MainModule.Types.Single(t=>t.FullName=="BongoLocal.LocalAutoTap").Methods.Single(m=>m.Name=="Tick");
            var update=new MethodDefinition("Update",MethodAttributes.Private|MethodAttributes.HideBySig,game.MainModule.TypeSystem.Void);
            cat.Methods.Add(update);
            var il=update.Body.GetILProcessor();il.Emit(OpCodes.Ldarg_0);il.Emit(OpCodes.Call,game.MainModule.ImportReference(tick));il.Emit(OpCodes.Ret);
            Directory.CreateDirectory(args[2]);
            string dest=Path.Combine(args[2],"Assembly-CSharp.dll");game.Write(dest);
            using(var verify=AssemblyDefinition.ReadAssembly(dest)) {
                var methods=verify.MainModule.Types.SelectMany(Types).SelectMany(t=>t.Methods).ToDictionary(m=>m.FullName,Body);
                foreach(var entry in originals)if(!methods.ContainsKey(entry.Key)||methods[entry.Key]!=entry.Value)throw new Exception("Original changed: "+entry.Key);
                if(methods.Count!=originals.Count+1)throw new Exception("Unexpected method count");
            }
            Console.WriteLine("Auto chest retained; Cat.Update added. Candidate: "+dest);
        }
        return 0;
    }
}
