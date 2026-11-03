using System;
using System.Runtime.InteropServices;
using System.Threading;
namespace GameLocal {
    // One OS hotkey owner; all supported processes read the same session-local bit.
    public static class SharedF7 {
        [DllImport("user32.dll",SetLastError=true)] static extern bool RegisterHotKey(IntPtr h,int id,uint mod,uint key);
        [DllImport("user32.dll")] static extern bool UnregisterHotKey(IntPtr h,int id);
        [DllImport("user32.dll",EntryPoint="GetMessageW")] static extern int GetMessage(IntPtr msg,IntPtr h,uint min,uint max);
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode)] static extern ushort GlobalAddAtom(string name);
        [DllImport("kernel32.dll")] static extern ushort GlobalDeleteAtom(ushort atom);
        [DllImport("kernel32.dll")] static extern uint GetCurrentProcessId();
        [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern IntPtr CreateFileMapping(IntPtr file,IntPtr attributes,uint protection,uint high,uint low,string name);
        [DllImport("kernel32.dll",SetLastError=true)] static extern IntPtr MapViewOfFile(IntPtr map,uint access,uint high,uint low,IntPtr size);
        [DllImport("kernel32.dll",EntryPoint="RtlMoveMemory")] static extern void CopyWord(IntPtr dest,ref int value,IntPtr size);
        static bool started;
        static IntPtr handle,view;
        public static volatile string Status;
        public static bool Active { get { return view!=IntPtr.Zero && Marshal.ReadInt32(view)==1; } }
        static void Set(bool active){int value=active?1:0;CopyWord(view,ref value,new IntPtr(4));}
        public static void EnsureStarted() {
            if(started)return;started=true;
            if(IntPtr.Size!=8)throw new InvalidOperationException("Only Windows x64 game processes are supported");
            handle=CreateFileMapping(new IntPtr(-1),IntPtr.Zero,4,0,4,"Local\\GameInternalAutoTap.F7.v1");
            if(handle==IntPtr.Zero)throw new InvalidOperationException("Shared F7 mapping failed");
            view=MapViewOfFile(handle,6,0,0,new IntPtr(4));
            if(view==IntPtr.Zero)throw new InvalidOperationException("Shared F7 view failed");
            var worker=new Thread(Loop);worker.IsBackground=true;worker.Name="Shared game F7";worker.Start();
        }
        static void Loop() {
            ushort atom=GlobalAddAtom("GameInternalAutoTap.F7."+GetCurrentProcessId().ToString());
            IntPtr buffer=IntPtr.Zero;
            bool owner=false;
            try {
                if(atom==0)throw new InvalidOperationException("Hotkey atom unavailable");
                while(!(owner=RegisterHotKey(IntPtr.Zero,atom,0x4000,0x76))) {
                    Status="F7_FOLLOWER shared state; retry owner in 1 second";
                    Thread.Sleep(1000);
                }
                Status="F7_OWNER shared state";
                buffer=Marshal.AllocHGlobal(48);
                while(GetMessage(buffer,IntPtr.Zero,0,0)>0)
                    if(Marshal.ReadInt32(buffer,8)==0x0312 && Marshal.ReadInt32(buffer,16)==atom)
                        Set(!Active);
            } catch(Exception ex) { if(view!=IntPtr.Zero)Set(false);Status="F7_ERROR "+ex.ToString(); }
            finally {if(buffer!=IntPtr.Zero)Marshal.FreeHGlobal(buffer);if(owner)UnregisterHotKey(IntPtr.Zero,atom);if(atom!=0)GlobalDeleteAtom(atom);}
        }
    }
}
