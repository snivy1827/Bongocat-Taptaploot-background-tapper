using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Mono.Cecil;
using Mono.Cecil.Cil;
class PatchTapLoot {
    static IEnumerable<TypeDefinition> Types(TypeDefinition t){yield return t;foreach(var n in t.NestedTypes)foreach(var c in Types(n))yield return c;}
    static string Body(MethodDefinition m,int skip){
        return m.FullName+(m.HasBody?string.Join("\n",m.Body.Instructions.Skip(skip).Select(i=>i.OpCode.Name+" "+
            (i.Operand is Instruction?"branch:"+(m.Body.Instructions.IndexOf((Instruction)i.Operand)-skip):
             i.Operand is Instruction[]?string.Join(",",((Instruction[])i.Operand).Select(j=>(m.Body.Instructions.IndexOf(j)-skip).ToString()).ToArray()):
             i.Operand==null?"":i.Operand.ToString())).ToArray()):"");
    }
    static int Main(string[] args){
        if(args.Length!=3)throw new ArgumentException("original.dll helper.dll output-directory");
        if(Path.GetFullPath(Path.Combine(args[2],"Assembly-CSharp.dll"))==Path.GetFullPath(args[0]))throw new Exception("Output cannot overwrite input");
        using(var a=AssemblyDefinition.ReadAssembly(args[0]))
        using(var helper=AssemblyDefinition.ReadAssembly(args[1])){
            var originals=a.MainModule.Types.SelectMany(Types).SelectMany(t=>t.Methods).ToDictionary(m=>m.FullName,m=>Body(m,0));
            var hook=a.MainModule.Types.Single(t=>t.FullName=="TapTapLoot.TurnSystem.GlobalKeyHook");
            var update=hook.Methods.Single(m=>m.Name=="Update");
            var tick=helper.MainModule.Types.Single(t=>t.FullName=="TapLootLocal.AutoTap").Methods.Single(m=>m.Name=="Tick");
            var il=update.Body.GetILProcessor();var first=update.Body.Instructions[0];
            il.InsertBefore(first,il.Create(OpCodes.Ldarg_0));
            il.InsertBefore(first,il.Create(OpCodes.Call,a.MainModule.ImportReference(tick)));
            Directory.CreateDirectory(args[2]);var dest=Path.Combine(args[2],"Assembly-CSharp.dll");a.Write(dest);
            using(var check=AssemblyDefinition.ReadAssembly(dest)){
                var methods=check.MainModule.Types.SelectMany(Types).SelectMany(t=>t.Methods).ToDictionary(m=>m.FullName,m=>Body(m,m.FullName==update.FullName?2:0));
                if(methods.Count!=originals.Count)throw new Exception("Method count changed");
                foreach(var pair in originals)if(methods[pair.Key]!=pair.Value)throw new Exception("Original changed: "+pair.Key);
            }
            Console.WriteLine("All "+originals.Count+" original method bodies retained; only the Update prefix was added.");
        }
        return 0;
    }
}
