using System.Net;
using System.Net.Sockets;
using System.Buffers.Binary;
using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;

namespace TS6_SpeakerOverlay.Helpers;

internal readonly record struct TcpListenerOwner(IPAddress Address, int Port, int ProcessId);

internal static class WindowsTcpListenerTable
{
    internal static IReadOnlyList<TcpListenerOwner> Read()
    {
        var rows = ReadFamily(2, 24, 4, 4, 8, 20);
        if (Socket.OSSupportsIPv6) rows.AddRange(ReadFamily(23, 56, 0, 16, 20, 52));
        return rows;
    }

    private static List<TcpListenerOwner> ReadFamily(uint family, int rowSize, int addressOffset, int addressLength, int portOffset, int pidOffset)
    {
        uint size = 0;
        uint error = GetExtendedTcpTable(IntPtr.Zero, ref size, false, family, 3, 0); // OWNER_PID_LISTENER
        if (error != 0 && error != 122) throw new Win32Exception((int)error);
        for (int attempt = 0; attempt < 3; attempt++)
        {
            IntPtr buffer = Marshal.AllocHGlobal(checked((int)size));
            try
            {
                error = GetExtendedTcpTable(buffer, ref size, false, family, 3, 0);
                if (error == 122) continue;
                if (error != 0) throw new Win32Exception((int)error);
                var data = new byte[checked((int)size)];
                Marshal.Copy(buffer, data, 0, data.Length);
                if (data.Length < 4) throw new InvalidDataException("TCP listener table has no header.");
                uint count = BinaryPrimitives.ReadUInt32LittleEndian(data);
                if (count > (data.Length - 4) / rowSize) throw new InvalidDataException("TCP listener table is incomplete.");
                var rows = new List<TcpListenerOwner>();
                for (int index = 0; index < count; index++)
                {
                    var row = data.AsSpan(4 + index * rowSize, rowSize);
                    var address = new IPAddress(row.Slice(addressOffset, addressLength));
                    int port = BinaryPrimitives.ReadUInt16BigEndian(row.Slice(portOffset, 2));
                    int pid = checked((int)BinaryPrimitives.ReadUInt32LittleEndian(row.Slice(pidOffset, 4)));
                    rows.Add(new(address, port, pid));
                }
                return rows;
            }
            finally { Marshal.FreeHGlobal(buffer); }
        }
        throw new Win32Exception((int)error);
    }

    [DllImport("iphlpapi.dll")]
    private static extern uint GetExtendedTcpTable(IntPtr table, ref uint size, [MarshalAs(UnmanagedType.Bool)] bool order, uint family, int tableClass, uint reserved);
}
