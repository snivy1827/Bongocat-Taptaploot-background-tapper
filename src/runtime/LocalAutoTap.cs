using System;
using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Threading;

namespace BongoLocal {
    public static class LocalAutoTap {
        static bool initialized, previousEnabled;
        static bool fault;
        static string lastStatus;
        static object localCat, pets;
        static MethodInfo tap, log;
        static PropertyInfo score;
        static FieldInfo started;
        static long generated;
        static double nextTap, nextReport;
        const BindingFlags Flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        static double Now { get { return (double)Stopwatch.GetTimestamp()/Stopwatch.Frequency; } }
        static string Score() {
            return Convert.ToString(score.GetValue(score.GetGetMethod(true).IsStatic ? null : pets,null),System.Globalization.CultureInfo.InvariantCulture);
        }
        static void Log(string text) { log.Invoke(null,new object[]{"[LocalAutoTap] "+text}); }
        public static void Tick(object cat) {
            if(fault)return;
            try {
                if (!initialized) {
                    Type type=cat.GetType();
                    var instance=type.GetField("Instance",Flags);
                    if(instance==null || !Object.ReferenceEquals(instance.GetValue(null),cat)) return;
                    var petType=type.Assembly.GetType("BongoCat.Pets");
                    pets=petType.GetField("Instance",Flags).GetValue(null);
                    if(pets==null) return;
                    tap=type.GetMethod("Tap",Flags,null,new[]{typeof(int)},null);
                    started=type.GetField("_started",Flags);
                    score=petType.GetProperty("Current",Flags);
                    log=Type.GetType("UnityEngine.Debug, UnityEngine.CoreModule",true).GetMethod("Log",new[]{typeof(object)});
                    if(tap==null || started==null || score==null) throw new MissingMemberException("Tap/Current/started");
                    localCat=cat; initialized=true;
                    GameLocal.SharedF7.EnsureStarted();
                    Log("INITIALIZED score="+Score()+" generated=0 mode=OFF");
                }
                if(!Object.ReferenceEquals(localCat,cat))return;
                string notification=GameLocal.SharedF7.Status;
                if(notification!=null && notification!=lastStatus){lastStatus=notification;Log(notification);}
                bool active=GameLocal.SharedF7.Active;
                double now=Now;
                if(active!=previousEnabled){
                    previousEnabled=active;nextTap=now;nextReport=now+5;
                    Log((active?"ON":"OFF")+" score="+Score()+" generated="+generated.ToString());
                }
                if(active && (bool)started.GetValue(cat) && now>=nextTap){
                    nextTap+=1.0/30.0; // target 30 normal taps/sec, at most one per frame
                    if(nextTap<now-.1)nextTap=now+1.0/30.0; // discard long-stall backlog
                    tap.Invoke(cat,new object[]{1});generated++;
                }
                if(now>=nextReport){nextReport=now+5;Log("PROGRESS score="+Score()+" generated="+generated.ToString()+" mode="+(active?"ON":"OFF"));}
            } catch(Exception ex) {
                fault=true;
                if(log!=null)Log("STOPPED_ERROR "+ex.GetType().Name);
            }
        }
    }
}
