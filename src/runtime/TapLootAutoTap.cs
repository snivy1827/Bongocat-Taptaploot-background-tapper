using System;
using System.Diagnostics;
using System.Reflection;
using GameLocal;
namespace TapLootLocal {
    public static class AutoTap {
        const BindingFlags Flags=BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Instance|BindingFlags.Static|BindingFlags.FlattenHierarchy;
        static object hook,counter;
        static FieldInfo queued;
        static MethodInfo clicks;
        static MethodInfo log;
        static bool initialized,previous,fault;
        static long generated;
        static double nextTap,nextReport;
        static string lastStatus;
        static double Now {get{return (double)Stopwatch.GetTimestamp()/Stopwatch.Frequency;}}
        static string Score(){return Convert.ToString(clicks.Invoke(counter,null),System.Globalization.CultureInfo.InvariantCulture);}
        static void Log(string s){log.Invoke(null,new object[]{"[LocalTTLAutoTap] "+s});}
        public static void Tick(object obj) {
            if(fault)return;
            try {
                if(!initialized) {
                    log=Type.GetType("UnityEngine.Debug, UnityEngine.CoreModule",true).GetMethod("Log",new[]{typeof(object)});
                    var type=obj.GetType();
                    var ct=type.Assembly.GetType("TapTapLoot.ClickCounter");
                    // ClickCounter inherits Singleton<ClickCounter>; read its actual instance.
                    var singleton=ct.BaseType.GetMethod("get_Instance",Flags);
                    counter=singleton.Invoke(null,null);if(counter==null)return;
                    queued=type.GetField("_keysDown",Flags);
                    clicks=ct.GetMethod("get_Clicks",Flags);
                    if(queued==null || clicks==null)throw new MissingMemberException("TTL keys/clicks");
                    hook=obj;SharedF7.EnsureStarted();initialized=true;
                    Log("INITIALIZED score="+Score()+" generated=0");
                }
                if(!Object.ReferenceEquals(hook,obj))return;
                var status=SharedF7.Status;
                if(status!=null && status!=lastStatus){lastStatus=status;Log(status);}
                double now=Now;bool active=SharedF7.Active;
                if(active!=previous){previous=active;nextTap=now;nextReport=now+5;Log((active?"ON":"OFF")+" score="+Score()+" generated="+generated.ToString());}
                if(active && now>=nextTap) {
                    nextTap+=1.0/30.0;
                    if(nextTap<now-.1)nextTap=now+1.0/30.0;
                    // The unchanged GlobalKeyHook.Update consumes this queued click,
                    // emits the normal event, and updates statistics/achievements.
                    queued.SetValue(obj,(int)queued.GetValue(obj)+1);generated++;
                }
                if(now>=nextReport){nextReport=now+5;Log("PROGRESS score="+Score()+" generated="+generated.ToString()+" mode="+(active?"ON":"OFF"));}
            } catch(Exception ex){fault=true;if(log!=null)Log("STOPPED_ERROR "+ex.ToString());}
        }
    }
}
