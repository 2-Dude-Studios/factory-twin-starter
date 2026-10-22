using System;

namespace TwoDude.FactoryTwin.Twin
{
    /// <summary>Soft PLC link: a Modbus exception response, or a reply that does not match its request.</summary>
    public sealed class ModbusException : Exception
    {
        public ModbusException(string message) : base(message) { }

        /// <summary>The Modbus exception code (1 illegal function, 2 illegal address, …); 0 for a malformed reply.</summary>
        public int Code { get; private set; }

        public static ModbusException FromCode(byte function, byte code)
        {
            return new ModbusException($"Modbus exception {code} for function 0x{function:X2}") { Code = code };
        }
    }

    /// <summary>
    /// Soft PLC link: Modbus TCP frames for the four function codes the twin needs. Pure, no sockets. Requests are
    /// MBAP header (transaction id, protocol 0, length, unit id) followed by the PDU; replies are checked against
    /// the request they answer.
    /// </summary>
    public static class ModbusTcp
    {
        public const byte ReadCoils = 0x01;
        public const byte ReadHoldingRegisters = 0x03;
        public const byte WriteMultipleRegisters = 0x10;

        public const int HeaderLength = 7;
        public const int MaxReadBits = 2000;
        public const int MaxReadRegisters = 125;
        public const int MaxWriteRegisters = 123;

        public static byte[] BuildReadCoils(ushort transactionId, byte unitId, ushort start, ushort count)
        {
            CheckCount(count, MaxReadBits);
            return Frame(transactionId, unitId, ReadCoils, start, count, null);
        }

        public static byte[] BuildReadHoldingRegisters(ushort transactionId, byte unitId, ushort start, ushort count)
        {
            CheckCount(count, MaxReadRegisters);
            return Frame(transactionId, unitId, ReadHoldingRegisters, start, count, null);
        }

        public static byte[] BuildWriteMultipleRegisters(ushort transactionId, byte unitId, ushort start, ushort[] values)
        {
            if (values == null)
                throw new ArgumentNullException(nameof(values));
            CheckCount(values.Length, MaxWriteRegisters);

            var data = new byte[1 + values.Length * 2];
            data[0] = (byte)(values.Length * 2);
            for (int i = 0; i < values.Length; i++)
            {
                data[1 + i * 2] = (byte)(values[i] >> 8);
                data[2 + i * 2] = (byte)values[i];
            }
            return Frame(transactionId, unitId, WriteMultipleRegisters, start, (ushort)values.Length, data);
        }

        /// <summary>Total frame length announced by an MBAP header, or -1 if the header is not Modbus TCP.</summary>
        public static int FrameLength(byte[] header)
        {
            if (header == null || header.Length < HeaderLength)
                throw new ArgumentException("Need the 7-byte MBAP header.", nameof(header));
            if (header[2] != 0 || header[3] != 0)
                return -1;
            int length = (header[4] << 8) | header[5];
            return length < 2 ? -1 : 6 + length;
        }

        /// <summary>Coil states from a read-coils reply to <paramref name="request"/>.</summary>
        public static bool[] ParseReadCoils(byte[] request, byte[] reply)
        {
            byte[] pdu = CheckReply(request, reply, ReadCoils);
            int count = ReadU16(request, 10);
            int byteCount = pdu[1];
            if (byteCount != (count + 7) / 8 || pdu.Length != 2 + byteCount)
                throw new ModbusException("Read-coils reply has the wrong length.");

            var bits = new bool[count];
            for (int i = 0; i < count; i++)
                bits[i] = (pdu[2 + i / 8] & (1 << (i % 8))) != 0;
            return bits;
        }

        /// <summary>Register values from a read-holding-registers reply to <paramref name="request"/>.</summary>
        public static ushort[] ParseReadHoldingRegisters(byte[] request, byte[] reply)
        {
            byte[] pdu = CheckReply(request, reply, ReadHoldingRegisters);
            int count = ReadU16(request, 10);
            if (pdu[1] != count * 2 || pdu.Length != 2 + count * 2)
                throw new ModbusException("Read-registers reply has the wrong length.");

            var values = new ushort[count];
            for (int i = 0; i < count; i++)
                values[i] = (ushort)ReadU16(pdu, 2 + i * 2);
            return values;
        }

        /// <summary>Checks a write-multiple-registers reply echoes the request's start and count.</summary>
        public static void ParseWriteMultipleRegisters(byte[] request, byte[] reply)
        {
            byte[] pdu = CheckReply(request, reply, WriteMultipleRegisters);
            if (pdu.Length != 5 || ReadU16(pdu, 1) != ReadU16(request, 8) || ReadU16(pdu, 3) != ReadU16(request, 10))
                throw new ModbusException("Write-registers reply does not echo the request.");
        }

        private static byte[] Frame(ushort transactionId, byte unitId, byte function, ushort start, ushort count, byte[] data)
        {
            int pduLength = 5 + (data?.Length ?? 0);
            var frame = new byte[HeaderLength + pduLength];
            WriteU16(frame, 0, transactionId);
            WriteU16(frame, 2, 0);
            WriteU16(frame, 4, (ushort)(pduLength + 1));
            frame[6] = unitId;
            frame[7] = function;
            WriteU16(frame, 8, start);
            WriteU16(frame, 10, count);
            data?.CopyTo(frame, 12);
            return frame;
        }

        private static byte[] CheckReply(byte[] request, byte[] reply, byte function)
        {
            if (request == null)
                throw new ArgumentNullException(nameof(request));
            if (reply == null || reply.Length < HeaderLength + 2)
                throw new ModbusException("Reply is too short.");
            if (FrameLength(reply) != reply.Length)
                throw new ModbusException("Reply length does not match its header.");
            if (reply[0] != request[0] || reply[1] != request[1])
                throw new ModbusException("Reply answers a different transaction.");
            if (reply[6] != request[6])
                throw new ModbusException("Reply comes from a different unit.");
            if (reply[7] == (function | 0x80))
                throw ModbusException.FromCode(function, reply[8]);
            if (reply[7] != function)
                throw new ModbusException($"Reply has function 0x{reply[7]:X2}, expected 0x{function:X2}.");

            var pdu = new byte[reply.Length - HeaderLength];
            Array.Copy(reply, HeaderLength, pdu, 0, pdu.Length);
            return pdu;
        }

        private static void CheckCount(int count, int max)
        {
            if (count < 1 || count > max)
                throw new ArgumentOutOfRangeException(nameof(count), $"Must be 1–{max}.");
        }

        private static int ReadU16(byte[] buffer, int offset) => (buffer[offset] << 8) | buffer[offset + 1];

        private static void WriteU16(byte[] buffer, int offset, ushort value)
        {
            buffer[offset] = (byte)(value >> 8);
            buffer[offset + 1] = (byte)value;
        }
    }
}
