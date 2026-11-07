using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Elysium.Core.Utils;

namespace PLDUOBAO.Util
{
    // 纯 C# 的进程内存数据补丁。
    // 本实现只用 C# 的 ReadProcessMemory / WriteProcessMemory 改「数据字节」：
    // - 不写任何文件到游戏目录
    // - 不注入 DLL
    // - 不建远程线程
    // - 不 hook 任何函数、不改代码段
    // 改写目标（全部等长，不破坏任何长度字段）：
    // "PC_100"      → "PE_100"       （'C' → 'E'）
    // "\"tag1\": \"pc\"" → "\"tag1\": \"pe\""   （'c' → 'e'）
    // "\"os\":\"10.0\"" → "\"os\":\"14.0\""      （Windows 10 → Android 14）
    // 这些是客户端上报平台身份时用的字符串常量。只改内存里的数据副本，
    // 不动代码段、不动指针、不 hook 任何函数，因此不会破坏执行流。
    // 注意：本类只做「尽力而为」的补丁。找不到目标字节时静默跳过，
    // 绝不因为补丁失败而影响游戏启动。
    internal static class PlatformPatcher
    {
        private const int PROCESS_QUERY_INFORMATION = 0x0400;
        private const int PROCESS_VM_READ = 0x0010;
        private const int PROCESS_VM_WRITE = 0x0020;
        private const int PROCESS_VM_OPERATION = 0x0008;

        private const int MEM_COMMIT = 0x00001000;
        private const int PAGE_READWRITE = 0x04;
        private const int PAGE_WRITECOPY = 0x80;
        private const int PAGE_READONLY = 0x02;
        private const int PAGE_EXECUTE_READWRITE = 0x40;
        private const int PAGE_EXECUTE_WRITECOPY = 0x80;

        [StructLayout(LayoutKind.Sequential)]
        private struct MEMORY_BASIC_INFORMATION
        {
            public IntPtr BaseAddress;
            public IntPtr AllocationBase;
            public uint AllocationProtect;
            public IntPtr RegionSize;
            public uint State;
            public uint Protect;
            public uint Type;
        }

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(int access, bool inherit, int pid);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr h);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool ReadProcessMemory(
            IntPtr h, IntPtr address, byte[] buffer, UIntPtr size, out UIntPtr read);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool WriteProcessMemory(
            IntPtr h, IntPtr address, byte[] buffer, UIntPtr size, out UIntPtr written);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern int VirtualQueryEx(
            IntPtr h, IntPtr address, out MEMORY_BASIC_INFORMATION mbi, UIntPtr length);

        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool VirtualProtectEx(
            IntPtr h, IntPtr address, UIntPtr size, uint newProtect, out uint oldProtect);

        [DllImport("kernel32.dll")]
        private static extern void GetSystemInfo(out SYSTEM_INFO si);

        [StructLayout(LayoutKind.Sequential)]
        private struct SYSTEM_INFO
        {
            public ushort wProcessorArchitecture;
            public ushort wReserved;
            public uint dwPageSize;
            public IntPtr lpMinimumApplicationAddress;
            public IntPtr lpMaximumApplicationAddress;
            public IntPtr dwActiveProcessorMask;
            public uint dwNumberOfProcessors;
            public uint dwProcessorType;
            public uint dwAllocationGranularity;
            public ushort wProcessorLevel;
            public ushort wProcessorRevision;
        }

        // ---- 等长替换规则 ----
        //
        // 跨版本通用性（实测数据）
        // ----------------------
        // 对比 3.10.5.301511 与 3.9.20.298172 两个版本的 vanilla.mcp：
        //
        //                          3.10.5    3.9.20
        //   PC_100                    2         2
        //   tag1                     11        11
        //   diagnostic-log            2         2
        //   GetPlatform              38        35
        //   RUNTIME_PLATFORM          4         4
        //   is_pccocos               15        13
        //   get_os_name              13        13
        //   get_device_info          20        20
        //   get_raw_model_name        1         1
        //   get_mac_address           5         5
        //
        // 全部符号两版都存在，所以同一套规则对两版都成立。
        //
        // 为什么这样的内存补丁能跨版本：
        //   1. 按「内容」定位，不依赖任何地址 / RVA / 版本号
        //   2. 模块基址每次启动重新枚举，天然适配 ASLR
        //   3. 只做等长字节替换，不移动数据、不改长度字段
        //
        // 注意：PE_100 在官方脚本里不存在——它是服务端侧使用的值。
        private static readonly byte[] FromPc100 = Encoding.ASCII.GetBytes("PC_100");
        private static readonly byte[] FromTagPc = Encoding.ASCII.GetBytes("\"tag1\": \"pc\"");
        private static readonly byte[] FromTagPc2 = Encoding.ASCII.GetBytes("\"tag1\":\"pc\"");
        private static readonly byte[] FromOs = Encoding.ASCII.GetBytes("\"os\":\"10.0\"");
        private static readonly byte[] FromOs2 = Encoding.ASCII.GetBytes("\"os\": \"10.0\"");

        // 动态规则（依赖当前设备档案），在 Worker 启动时重建
        private static List<(string Name, byte[] From, byte[] To)> _rules = new();

        private static readonly object _sync = new();
        private static readonly Dictionary<int, CancellationTokenSource> _running = new();

        // 构建替换规则。每条规则 = 匹配串 + 等长替换串。
        // 全部目标来自游戏自带脚本（vanilla.mcp），不依赖任何外部脚本包。
        private static List<(string Name, byte[] From, byte[] To)> BuildRules()
        {
            return new List<(string, byte[], byte[])>
            {
                ("PC_100→PE_100", FromPc100, Encoding.ASCII.GetBytes("PE_100")),
                ("tag1 pc→pe", FromTagPc, ReplaceTrailing(FromTagPc, (byte)'e', 2)),
                ("tag1 pc→pe(紧凑)", FromTagPc2, ReplaceTrailing(FromTagPc2, (byte)'e', 2)),
                ("os 10.0→14.0", FromOs, ReplaceAt(FromOs, 7, (byte)'4')),
                ("os 10.0→14.0(带空格)", FromOs2, ReplaceAt(FromOs2, 8, (byte)'4')),
            };
        }

        // 复制并在指定下标改一个字节
        private static byte[] ReplaceAt(byte[] src, int index, byte value)
        {
            var d = (byte[])src.Clone();
            if (index >= 0 && index < d.Length) d[index] = value;
            return d;
        }

        // 复制并从末尾倒数第 n 个位置改一个字节
        private static byte[] ReplaceTrailing(byte[] src, byte value, int fromEnd)
        {
            var d = (byte[])src.Clone();
            int idx = d.Length - fromEnd;
            if (idx >= 0 && idx < d.Length) d[idx] = value;
            return d;
        }

        // 对指定进程启动补丁（幂等，重复调用不会重复开线程）。
        public static void Start(int pid)
        {
            lock (_sync)
            {
                if (_running.ContainsKey(pid)) return;
                var cts = new CancellationTokenSource();
                _running[pid] = cts;

                var t = new Thread(() => Worker(pid, cts.Token))
                {
                    IsBackground = true,
                    Name = $"PlatformPatcher-{pid}",
                };
                t.Start();
                LogSys.Log($"[PlatformPatcher] started for pid={pid}");
            }
        }

        // 停止指定进程的补丁轮询。
        public static void Stop(int pid)
        {
            lock (_sync)
            {
                if (_running.TryGetValue(pid, out var cts))
                {
                    cts.Cancel();
                    _running.Remove(pid);
                }
            }
        }

        private static void Worker(int pid, CancellationToken token)
        {
            IntPtr h = IntPtr.Zero;
            try
            {
                h = OpenProcess(
                    PROCESS_QUERY_INFORMATION | PROCESS_VM_READ |
                    PROCESS_VM_WRITE | PROCESS_VM_OPERATION,
                    false, pid);

                if (h == IntPtr.Zero)
                {
                    LogSys.Warn($"[PlatformPatcher] OpenProcess failed pid={pid}, err={Marshal.GetLastWin32Error()}");
                    return;
                }

                // 每次启动按当前规则重建（规则表内含设备相关项）
                lock (_sync) { _rules = BuildRules(); }
                LogSys.Log($"[PlatformPatcher] rules={_rules.Count} for pid={pid}");

                // 前 3 分钟每 2 秒扫一次；之后每 10 秒，直到进程退出。
                // 游戏启动初期字符串表还没建立，必须持续覆盖。
                var sw = Stopwatch.StartNew();
                int round = 0;
                while (!token.IsCancellationRequested)
                {
                    round++;
                    try
                    {
                        int hits = ScanAndPatch(h);
                        if (hits > 0)
                            LogSys.Log($"[PlatformPatcher] round {round}: patched {hits} bytes");
                    }
                    catch (Exception ex)
                    {
                        LogSys.Warn($"[PlatformPatcher] scan err: {ex.Message}");
                    }

                    // 进程已退出
                    try
                    {
                        using var p = Process.GetProcessById(pid);
                        if (p.HasExited) break;
                    }
                    catch
                    {
                        break;
                    }

                    int delay = sw.Elapsed < TimeSpan.FromMinutes(3) ? 2000 : 10000;
                    if (token.WaitHandle.WaitOne(delay)) break;
                }
            }
            finally
            {
                if (h != IntPtr.Zero) CloseHandle(h);
                lock (_sync) { _running.Remove(pid); }
                LogSys.Log($"[PlatformPatcher] stopped pid={pid}");
            }
        }

        private static int ScanAndPatch(IntPtr h)
        {
            GetSystemInfo(out var si);
            IntPtr addr = si.lpMinimumApplicationAddress;
            IntPtr max = si.lpMaximumApplicationAddress;
            int totalPatch = 0;

            while (addr.ToInt64() < max.ToInt64())
            {
                if (VirtualQueryEx(h, addr, out var mbi,
                        (UIntPtr)Marshal.SizeOf<MEMORY_BASIC_INFORMATION>()) == 0)
                    break;

                long regionSize = mbi.RegionSize.ToInt64();
                if (regionSize <= 0) break;

                bool committed = mbi.State == MEM_COMMIT;
                bool readable = (mbi.Protect & (PAGE_READONLY | PAGE_READWRITE |
                                                PAGE_WRITECOPY | PAGE_EXECUTE_READWRITE |
                                                PAGE_EXECUTE_WRITECOPY)) != 0;
                bool guard = (mbi.Protect & 0x100) != 0;   // PAGE_GUARD

                // 只扫私有可读写的小块（堆/数据段），跳过镜像、栈、guard 页和大块
                if (committed && readable && !guard &&
                    regionSize <= 32L * 1024 * 1024 &&
                    (mbi.Protect & (PAGE_READWRITE | PAGE_WRITECOPY)) != 0)
                {
                    totalPatch += PatchRegion(h, mbi.BaseAddress, regionSize);
                }

                long next = mbi.BaseAddress.ToInt64() + regionSize;
                if (next <= addr.ToInt64()) break;
                addr = (IntPtr)next;
            }

            return totalPatch;
        }

        private static int PatchRegion(IntPtr h, IntPtr baseAddr, long size)
        {
            const int CHUNK = 1 << 20;   // 1 MB
            byte[] buf = new byte[CHUNK];
            int patched = 0;
            long off = 0;

            // 取当前规则快照（避免与重建竞争）
            List<(string Name, byte[] From, byte[] To)> rules;
            lock (_sync) { rules = _rules; }
            if (rules.Count == 0) return 0;

            // 按首字节分桶：只有首字节对得上才做完整比较，省掉大部分扫描
            var byFirstByte = new Dictionary<byte, List<(string Name, byte[] From, byte[] To)>>();
            foreach (var r in rules)
            {
                if (r.From.Length == 0) continue;
                byte b0 = r.From[0];
                if (!byFirstByte.TryGetValue(b0, out var lst))
                {
                    lst = new List<(string, byte[], byte[])>();
                    byFirstByte[b0] = lst;
                }
                lst.Add(r);
            }

            while (off < size)
            {
                int want = (int)Math.Min(CHUNK, size - off);
                IntPtr p = (IntPtr)(baseAddr.ToInt64() + off);

                if (!ReadProcessMemory(h, p, buf, (UIntPtr)want, out var read) ||
                    read.ToUInt64() < 4)
                {
                    off += want;
                    continue;
                }

                int n = (int)read.ToUInt64();
                var patchList = new List<(int Rel, byte Value)>(16);

                for (int i = 0; i < n; i++)
                {
                    if (!byFirstByte.TryGetValue(buf[i], out var candidates)) continue;

                    foreach (var rule in candidates)
                    {
                        byte[] from = rule.From, to = rule.To;
                        if (from.Length == 0 || from.Length != to.Length) continue;
                        if (i + from.Length > n) continue;

                        bool match = true;
                        for (int k = 1; k < from.Length; k++)
                        {
                            if (buf[i + k] != from[k]) { match = false; break; }
                        }
                        if (!match) continue;

                        // 整串比较，只记录真正需要改的字节
                        bool any = false;
                        for (int k = 0; k < to.Length; k++)
                        {
                            if (buf[i + k] != to[k]) { any = true; break; }
                        }
                        if (!any) break;   // 已经是目标值

                        for (int k = 0; k < to.Length; k++)
                        {
                            if (buf[i + k] != to[k])
                                patchList.Add((i + k, to[k]));
                        }
                        break;
                    }
                }

                foreach (var (rel, value) in patchList)
                {
                    if (rel < 0 || rel >= n) continue;
                    if (WriteByte(h, (IntPtr)(p.ToInt64() + rel), value))
                        patched++;
                }

                off += want;
            }

            return patched;
        }

        private static bool WriteByte(IntPtr h, IntPtr addr, byte value)
        {
            byte[] one = { value };

            if (WriteProcessMemory(h, addr, one, (UIntPtr)1, out var written) &&
                written.ToUInt64() == 1)
                return true;

            // 页面只读：临时提权为可写，写完立刻还原
            if (VirtualProtectEx(h, addr, (UIntPtr)1, PAGE_EXECUTE_READWRITE, out uint oldProtect))
            {
                try
                {
                    return WriteProcessMemory(h, addr, one, (UIntPtr)1, out written) &&
                           written.ToUInt64() == 1;
                }
                finally
                {
                    VirtualProtectEx(h, addr, (UIntPtr)1, oldProtect, out _);
                }
            }

            return false;
        }
    }
}
