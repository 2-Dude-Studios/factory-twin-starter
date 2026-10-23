using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class ModbusTcpTests
    {
        [Test]
        public void ReadCoilsRequest_HasMbapHeaderAndPdu()
        {
            byte[] frame = ModbusTcp.BuildReadCoils(0x1234, 1, 0, 4);

            CollectionAssert.AreEqual(new byte[] { 0x12, 0x34, 0, 0, 0, 6, 1, 0x01, 0, 0, 0, 4 }, frame);
        }

        [Test]
        public void WriteRegistersRequest_EncodesValuesBigEndian()
        {
            byte[] frame = ModbusTcp.BuildWriteMultipleRegisters(7, 1, 1024, new ushort[] { 1, 0x0203 });

            CollectionAssert.AreEqual(
                new byte[] { 0, 7, 0, 0, 0, 11, 1, 0x10, 0x04, 0x00, 0, 2, 4, 0, 1, 0x02, 0x03 }, frame);
        }

        [Test]
        public void ReadCoilsReply_UnpacksBitsLeastSignificantFirst()
        {
            byte[] request = ModbusTcp.BuildReadCoils(1, 1, 0, 4);
            byte[] reply = { 0, 1, 0, 0, 0, 4, 1, 0x01, 1, 0b0000_0101 };

            CollectionAssert.AreEqual(new[] { true, false, true, false }, ModbusTcp.ParseReadCoils(request, reply));
        }

        [Test]
        public void ReadRegistersReply_ReturnsValues()
        {
            byte[] request = ModbusTcp.BuildReadHoldingRegisters(9, 1, 0, 2);
            byte[] reply = { 0, 9, 0, 0, 0, 7, 1, 0x03, 4, 0, 3, 0x01, 0x00 };

            CollectionAssert.AreEqual(new ushort[] { 3, 256 }, ModbusTcp.ParseReadHoldingRegisters(request, reply));
        }

        [Test]
        public void ExceptionReply_ThrowsWithCode()
        {
            byte[] request = ModbusTcp.BuildReadHoldingRegisters(2, 1, 5000, 1);
            byte[] reply = { 0, 2, 0, 0, 0, 3, 1, 0x83, 2 };

            var e = Assert.Throws<ModbusException>(() => ModbusTcp.ParseReadHoldingRegisters(request, reply));
            Assert.AreEqual(2, e.Code);
        }

        [Test]
        public void ReplyToAnotherTransaction_Throws()
        {
            byte[] request = ModbusTcp.BuildReadCoils(5, 1, 0, 1);
            byte[] reply = { 0, 6, 0, 0, 0, 4, 1, 0x01, 1, 1 };

            Assert.Throws<ModbusException>(() => ModbusTcp.ParseReadCoils(request, reply));
        }

        [Test]
        public void WriteReply_MustEchoStartAndCount()
        {
            byte[] request = ModbusTcp.BuildWriteMultipleRegisters(3, 1, 1024, new ushort[] { 1, 2 });
            byte[] good = { 0, 3, 0, 0, 0, 6, 1, 0x10, 0x04, 0x00, 0, 2 };
            byte[] bad = { 0, 3, 0, 0, 0, 6, 1, 0x10, 0x04, 0x00, 0, 1 };

            Assert.DoesNotThrow(() => ModbusTcp.ParseWriteMultipleRegisters(request, good));
            Assert.Throws<ModbusException>(() => ModbusTcp.ParseWriteMultipleRegisters(request, bad));
        }

        [Test]
        public void CountOutsideModbusLimits_Throws()
        {
            Assert.Throws<ArgumentOutOfRangeException>(() => ModbusTcp.BuildReadHoldingRegisters(1, 1, 0, 0));
            Assert.Throws<ArgumentOutOfRangeException>(() => ModbusTcp.BuildReadHoldingRegisters(1, 1, 0, 126));
        }
    }

    public sealed class PlcImageTests
    {
        private static PlcImage DefaultImage() => new PlcImage(PlcIoMap.DefaultPoints().ToList(), 1028);

        [Test]
        public void DefaultMap_FormsThreeBlocks()
        {
            PlcImage image = DefaultImage();

            Assert.AreEqual(((ushort)1024, (ushort)5), image.WriteBlock);
            Assert.AreEqual(((ushort)0, (ushort)4), image.CoilBlock);
            Assert.AreEqual(((ushort)0, (ushort)4), image.RegisterBlock);
        }

        [Test]
        public void WriteBlock_CarriesSensorsAndTwinHeartbeat()
        {
            PlcImage image = DefaultImage();
            var bus = new Dictionary<string, double>
            {
                ["cell/gate1/part_present"] = 1d,
                ["cell/arm/busy"] = 0d,
                ["cell/outfeed/part_exit"] = 1d,
                ["cell/press/health"] = 2d,
            };
            var block = new ushort[image.WriteBlock.Count];

            image.FillWriteBlock(block, t => bus.TryGetValue(t.ToString(), out double v) ? v : 0d, 42);

            CollectionAssert.AreEqual(new ushort[] { 1, 0, 1, 2, 42 }, block);
        }

        [Test]
        public void Decode_PublishesCoilsAsBoolsAndScalesRegisters()
        {
            PlcImage image = DefaultImage();
            var decoded = new List<(TopicPath Topic, SignalValue Value)>();

            image.Decode(new[] { true, true, false, false }, new ushort[] { 1, 500, 17, 458 }, decoded);
            var byTopic = decoded.ToDictionary(d => d.Topic.ToString(), d => d.Value);

            Assert.AreEqual(SignalValue.FromBool(true), byTopic["cell/infeed/drive_fwd"]);
            Assert.AreEqual(SignalValue.FromBool(false), byTopic["cell/arm/pick_cmd"]);
            Assert.AreEqual(SignalValue.FromNumber(1), byTopic["cell/line/state"]);
            Assert.AreEqual(SignalValue.FromNumber(500), byTopic["cell/plc/heartbeat"]);
            Assert.AreEqual(17d, byTopic["cell/line/parts_out"].AsNumber);
            Assert.AreEqual(4.58d, byTopic["cell/line/cycle_time_s"].AsNumber, 1e-9);
        }

        [Test]
        public void DuplicateAddress_IsRejected()
        {
            var points = new[]
            {
                new PlcPoint("cell/infeed/drive_fwd", PlcArea.Coil, 0, PlcDirection.FromPlc, true, "a"),
                new PlcPoint("cell/outfeed/drive_fwd", PlcArea.Coil, 0, PlcDirection.FromPlc, true, "b"),
            };

            Assert.Throws<ArgumentException>(() => new PlcImage(points, 1028));
        }

        [Test]
        public void TwinWritingACoil_IsRejected()
        {
            var points = new[] { new PlcPoint("cell/gate1/part_present", PlcArea.Coil, 8, PlcDirection.ToPlc, true, "a") };

            Assert.Throws<ArgumentException>(() => new PlcImage(points, 1028));
        }

        [Test]
        public void ToRegister_RoundsAndClamps()
        {
            var number = new PlcPoint("cell/line/cycle_time_s", PlcArea.HoldingRegister, 3, PlcDirection.ToPlc, false, "c", "", 0.01d);

            Assert.AreEqual(458, PlcImage.ToRegister(4.578d, number));
            Assert.AreEqual(0, PlcImage.ToRegister(-3d, number));
            Assert.AreEqual(ushort.MaxValue, PlcImage.ToRegister(1e9d, number));
        }

        [Test]
        public void IecAddresses_FollowOpenPlcLayout()
        {
            Assert.AreEqual("%QX0.2", new PlcPoint("cell/arm/pick_cmd", PlcArea.Coil, 2, PlcDirection.FromPlc, true, "p").IecAddress);
            Assert.AreEqual("%QX1.1", new PlcPoint("cell/arm/pick_cmd", PlcArea.Coil, 9, PlcDirection.FromPlc, true, "p").IecAddress);
            Assert.AreEqual("%QW3", new PlcPoint("cell/line/state", PlcArea.HoldingRegister, 3, PlcDirection.FromPlc, false, "s").IecAddress);
            Assert.AreEqual("%MW1", new PlcPoint("cell/arm/busy", PlcArea.HoldingRegister, 1025, PlcDirection.ToPlc, true, "b").IecAddress);
        }
    }

    public sealed class PlcWatchdogTests
    {
        [Test]
        public void NoReplyYet_IsNotAlive()
        {
            Assert.IsFalse(new PlcWatchdog(0.5d).IsAlive(10d));
        }

        [Test]
        public void MovingHeartbeat_StaysAlive_FrozenHeartbeatTimesOut()
        {
            var watchdog = new PlcWatchdog(0.5d);
            watchdog.Observe(1, 0d);
            watchdog.Observe(2, 0.4d);
            Assert.IsTrue(watchdog.IsAlive(0.8d));

            watchdog.Observe(2, 0.8d);
            watchdog.Observe(2, 0.95d);
            Assert.IsFalse(watchdog.IsAlive(0.95d));
        }

        [Test]
        public void Reset_ForgetsThePlc()
        {
            var watchdog = new PlcWatchdog(0.5d);
            watchdog.Observe(1, 0d);
            watchdog.Reset();

            Assert.IsFalse(watchdog.IsAlive(0.1d));
        }
    }

    public sealed class ModbusClientTests
    {
        [Test]
        public void Client_RoundTripsAgainstALoopbackServer()
        {
            var listener = new TcpListener(IPAddress.Loopback, 0);
            listener.Start();
            int port = ((IPEndPoint)listener.LocalEndpoint).Port;
            ushort[] stored = new ushort[8];

            var server = new Thread(() =>
            {
                using (TcpClient peer = listener.AcceptTcpClient())
                using (NetworkStream stream = peer.GetStream())
                {
                    for (int i = 0; i < 2; i++)
                    {
                        var header = new byte[7];
                        ReadFully(stream, header);
                        var pdu = new byte[ModbusTcp.FrameLength(header) - 7];
                        ReadFully(stream, pdu);
                        byte[] reply = Answer(header, pdu, stored);
                        stream.Write(reply, 0, reply.Length);
                    }
                }
            }) { IsBackground = true };
            server.Start();

            try
            {
                using (var client = new ModbusClient("127.0.0.1", port, 1, 2000))
                {
                    client.Connect();
                    client.WriteHoldingRegisters(2, new ushort[] { 11, 22 });
                    CollectionAssert.AreEqual(new ushort[] { 0, 11, 22 }, client.ReadHoldingRegisters(1, 3));
                }
                Assert.IsTrue(server.Join(2000));
            }
            finally
            {
                listener.Stop();
            }
        }

        private static void ReadFully(NetworkStream stream, byte[] buffer)
        {
            int offset = 0;
            while (offset < buffer.Length)
            {
                int read = stream.Read(buffer, offset, buffer.Length - offset);
                if (read <= 0)
                    throw new InvalidOperationException("Closed early.");
                offset += read;
            }
        }

        // A tiny holding-register server: function 0x03 and 0x10 only.
        private static byte[] Answer(byte[] header, byte[] pdu, ushort[] stored)
        {
            int start = (pdu[1] << 8) | pdu[2];
            int count = (pdu[3] << 8) | pdu[4];
            var body = new List<byte> { pdu[0] };
            if (pdu[0] == ModbusTcp.WriteMultipleRegisters)
            {
                for (int i = 0; i < count; i++)
                    stored[start + i] = (ushort)((pdu[6 + i * 2] << 8) | pdu[7 + i * 2]);
                body.AddRange(new[] { pdu[1], pdu[2], pdu[3], pdu[4] });
            }
            else
            {
                body.Add((byte)(count * 2));
                for (int i = 0; i < count; i++)
                    body.AddRange(new[] { (byte)(stored[start + i] >> 8), (byte)stored[start + i] });
            }

            var reply = new List<byte> { header[0], header[1], 0, 0, 0, (byte)(body.Count + 1), header[6] };
            reply.AddRange(body);
            return reply.ToArray();
        }
    }
}
