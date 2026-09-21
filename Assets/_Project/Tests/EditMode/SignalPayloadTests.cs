using System;
using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class SignalPayloadTests
    {
        [Test]
        public void Serialize_WritesNumberAndTimestamp()
        {
            string json = SignalPayload.Serialize(SignalValue.FromNumber(0.35), 1790033400000L);

            Assert.AreEqual("{\"v\":0.35,\"t\":1790033400000}", json);
        }

        [Test]
        public void Serialize_WritesJsonBooleans()
        {
            Assert.AreEqual("{\"v\":true,\"t\":1}", SignalPayload.Serialize(SignalValue.FromBool(true), 1L));
            Assert.AreEqual("{\"v\":false,\"t\":1}", SignalPayload.Serialize(SignalValue.FromBool(false), 1L));
        }

        [Test]
        public void Serialize_RejectsNonFiniteNumbers()
        {
            Assert.Throws<ArgumentException>(() => SignalPayload.Serialize(SignalValue.FromNumber(double.NaN), 1L));
            Assert.Throws<ArgumentException>(() => SignalPayload.Serialize(SignalValue.FromNumber(double.PositiveInfinity), 1L));
        }

        [TestCase(0d)]
        [TestCase(-12.5)]
        [TestCase(308.6)]
        [TestCase(1e-9)]
        [TestCase(1.7976931348623157E+308)]
        public void RoundTrip_PreservesNumbersExactly(double number)
        {
            string json = SignalPayload.Serialize(SignalValue.FromNumber(number), 42L);

            Assert.IsTrue(SignalPayload.TryParse(json, out SignalValue value, out long unixMs));
            Assert.AreEqual(SignalKind.Number, value.Kind);
            Assert.AreEqual(number, value.Number);
            Assert.AreEqual(42L, unixMs);
        }

        [Test]
        public void TryParse_AcceptsPythonReplayerFormatting()
        {
            // json.dumps puts a space after ':' and ','.
            Assert.IsTrue(SignalPayload.TryParse("{\"v\": 308.5, \"t\": 1790033392854}", out SignalValue value, out long unixMs));
            Assert.AreEqual(308.5, value.Number);
            Assert.AreEqual(1790033392854L, unixMs);

            Assert.IsTrue(SignalPayload.TryParse("{\"v\": true, \"t\": 7}", out value, out _));
            Assert.AreEqual(SignalKind.Bool, value.Kind);
            Assert.IsTrue(value.Bool);
        }

        [Test]
        public void TryParse_AcceptsEitherKeyOrderAndOuterWhitespace()
        {
            Assert.IsTrue(SignalPayload.TryParse(" \r\n{ \"t\" : 9 ,\t\"v\" : 3 } ", out SignalValue value, out long unixMs));
            Assert.AreEqual(3d, value.Number);
            Assert.AreEqual(9L, unixMs);
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("{}")]
        [TestCase("{\"v\":1}")]
        [TestCase("{\"t\":1}")]
        [TestCase("{\"v\":1,\"t\":2")]
        [TestCase("{\"v\":1,\"t\":2}x")]
        [TestCase("{\"v\":1,\"t\":2,}")]
        [TestCase("{\"v\":1,\"v\":2,\"t\":3}")]
        [TestCase("{\"v\":1,\"t\":2,\"q\":3}")]
        [TestCase("{\"v\":\"on\",\"t\":2}")]
        [TestCase("{\"v\":NaN,\"t\":2}")]
        [TestCase("{\"v\":Infinity,\"t\":2}")]
        [TestCase("{\"v\":1,\"t\":2.5}")]
        [TestCase("{\"value\":1,\"t\":2}")]
        [TestCase("[1,2]")]
        public void TryParse_RejectsMalformedPayloads(string json)
        {
            Assert.IsFalse(SignalPayload.TryParse(json, out _, out _));
        }

        [Test]
        public void AsNumber_ReadsBooleansAsZeroOrOne()
        {
            Assert.AreEqual(1d, SignalValue.FromBool(true).AsNumber);
            Assert.AreEqual(0d, SignalValue.FromBool(false).AsNumber);
            Assert.AreEqual(2.5, SignalValue.FromNumber(2.5).AsNumber);
        }
    }
}
