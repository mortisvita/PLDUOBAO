using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;

namespace PLDUOBAO.Util
{
    public static class NetworkUtil
    {
        public static int GetAvailablePort(int low = 25565, int high = 35565)
        {
            if (low > high)
            {
                return 0;
            }
            HashSet<int> usedPorts = GetUsedPorts();
            for (int i = low; i <= high; i++)
            {
                if (!usedPorts.Contains(i))
                {
                    return i;
                }
            }
            return 0;
        }

        private static HashSet<int> GetUsedPorts()
        {
            IPGlobalProperties iPGlobalProperties = IPGlobalProperties.GetIPGlobalProperties();
            IEnumerable<int> first = from e in iPGlobalProperties.GetActiveTcpListeners()
                                     select e.Port;
            IEnumerable<int> second = from e in iPGlobalProperties.GetActiveUdpListeners()
                                      select e.Port;
            IEnumerable<int> second2 = from c in iPGlobalProperties.GetActiveTcpConnections()
                                       select c.LocalEndPoint.Port;
            HashSet<int> hashSet = new HashSet<int>();
            foreach (int item in first.Concat(second).Concat(second2))
            {
                hashSet.Add(item);
            }
            return hashSet;
        }
    }
}
