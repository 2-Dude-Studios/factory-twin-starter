using System;
using System.IO;
using System.Net.Sockets;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>
    /// Soft PLC link: a blocking Modbus TCP client, one request at a time. Meant for a background thread; every call
    /// either returns the reply or throws, and the caller reconnects after a throw.
    /// </summary>
    public sealed class ModbusClient : IDisposable
    {
        private readonly string _host;
        private readonly int _port;
        private readonly byte _unitId;
        private readonly int _timeoutMs;
        private readonly byte[] _header = new byte[ModbusTcp.HeaderLength];

        private TcpClient _tcp;
        private NetworkStream _stream;
        private ushort _transactionId;

        public ModbusClient(string host, int port, byte unitId, int timeoutMs)
        {
            _host = host ?? throw new ArgumentNullException(nameof(host));
            _port = port;
            _unitId = unitId;
            _timeoutMs = timeoutMs;
        }

        public bool IsConnected => _tcp != null && _tcp.Connected;

        public void Connect()
        {
            Close();
            var tcp = new TcpClient { NoDelay = true, ReceiveTimeout = _timeoutMs, SendTimeout = _timeoutMs };
            IAsyncResult pending = tcp.BeginConnect(_host, _port, null, null);
            if (!pending.AsyncWaitHandle.WaitOne(_timeoutMs) || !tcp.Connected)
            {
                tcp.Close();
                throw new IOException($"No Modbus TCP server at {_host}:{_port}.");
            }
            tcp.EndConnect(pending);
            _tcp = tcp;
            _stream = tcp.GetStream();
        }

        public bool[] ReadCoils(ushort start, ushort count)
        {
            byte[] request = ModbusTcp.BuildReadCoils(NextId(), _unitId, start, count);
            return ModbusTcp.ParseReadCoils(request, Exchange(request));
        }

        public ushort[] ReadHoldingRegisters(ushort start, ushort count)
        {
            byte[] request = ModbusTcp.BuildReadHoldingRegisters(NextId(), _unitId, start, count);
            return ModbusTcp.ParseReadHoldingRegisters(request, Exchange(request));
        }

        public void WriteHoldingRegisters(ushort start, ushort[] values)
        {
            byte[] request = ModbusTcp.BuildWriteMultipleRegisters(NextId(), _unitId, start, values);
            ModbusTcp.ParseWriteMultipleRegisters(request, Exchange(request));
        }

        public void Close()
        {
            _stream?.Dispose();
            _tcp?.Close();
            _stream = null;
            _tcp = null;
        }

        public void Dispose() => Close();

        private ushort NextId() => unchecked(++_transactionId);

        private byte[] Exchange(byte[] request)
        {
            if (_stream == null)
                throw new IOException("Not connected.");

            _stream.Write(request, 0, request.Length);
            ReadExactly(_header, 0, _header.Length);
            int length = ModbusTcp.FrameLength(_header);
            if (length < ModbusTcp.HeaderLength + 1 || length > 260)
                throw new ModbusException("Reply header is not Modbus TCP.");

            var reply = new byte[length];
            Array.Copy(_header, reply, _header.Length);
            ReadExactly(reply, _header.Length, length - _header.Length);
            return reply;
        }

        private void ReadExactly(byte[] buffer, int offset, int count)
        {
            while (count > 0)
            {
                int read = _stream.Read(buffer, offset, count);
                if (read <= 0)
                    throw new IOException("The PLC closed the connection.");
                offset += read;
                count -= read;
            }
        }
    }
}
