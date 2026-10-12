using System;
using System.Collections.Generic;
using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class SignalPublisherTests
    {
        private static readonly TopicPath Speed = new TopicPath("infeed", "speed_mm_s");
        private static readonly TopicPath Gate = new TopicPath("gate1", "part_present");

        private SignalBus _bus;
        private List<SignalMessage> _sent;

        [SetUp]
        public void SetUp()
        {
            _bus = new SignalBus();
            _sent = new List<SignalMessage>();
        }

        private SignalPublisher Publisher(double rateHz = 10d, double deadband = 0.001d) =>
            new SignalPublisher(_bus, _sent.Add, rateHz, deadband);

        [Test]
        public void FirstValue_IsAlwaysSent()
        {
            using (SignalPublisher publisher = Publisher())
            {
                _bus.Publish(Speed, SignalValue.FromNumber(200), 1L);
                publisher.Flush(0d);
            }

            Assert.AreEqual(1, _sent.Count);
            Assert.AreEqual(Speed, _sent[0].Topic);
            Assert.AreEqual(200d, _sent[0].Value.Number);
        }

        [Test]
        public void NothingIsSentUntilFlush()
        {
            using (SignalPublisher publisher = Publisher())
            {
                _bus.Publish(Speed, SignalValue.FromNumber(200), 1L);
                Assert.AreEqual(0, _sent.Count);
                publisher.Flush(0d);
                Assert.AreEqual(1, _sent.Count);
            }
        }

        [Test]
        public void MovesInsideTheDeadband_AreSuppressed()
        {
            using (SignalPublisher publisher = Publisher(deadband: 0.5d))
            {
                _bus.Publish(Speed, SignalValue.FromNumber(200), 1L);
                publisher.Flush(0d);

                _bus.Publish(Speed, SignalValue.FromNumber(200.2), 2L);   // inside
                publisher.Flush(10d);
                Assert.AreEqual(1, _sent.Count);

                _bus.Publish(Speed, SignalValue.FromNumber(201), 3L);     // outside
                publisher.Flush(20d);
                Assert.AreEqual(2, _sent.Count);
                Assert.AreEqual(201d, _sent[1].Value.Number);
                Assert.AreEqual(1, publisher.Suppressed);
            }
        }

        [Test]
        public void Booleans_IgnoreTheDeadbandAndSendEveryChange()
        {
            using (SignalPublisher publisher = Publisher(rateHz: 1000d, deadband: 10d))
            {
                _bus.Publish(Gate, SignalValue.FromBool(false), 1L);
                publisher.Flush(0d);
                _bus.Publish(Gate, SignalValue.FromBool(true), 2L);
                publisher.Flush(1d);
                _bus.Publish(Gate, SignalValue.FromBool(false), 3L);
                publisher.Flush(2d);
            }

            Assert.AreEqual(3, _sent.Count);
        }

        [Test]
        public void RateLimit_HoldsAndCoalescesToTheLatestValue()
        {
            using (SignalPublisher publisher = Publisher(rateHz: 10d))   // 100 ms
            {
                _bus.Publish(Speed, SignalValue.FromNumber(1), 1L);
                publisher.Flush(0d);
                Assert.AreEqual(1, _sent.Count);

                for (int i = 2; i <= 9; i++)
                {
                    _bus.Publish(Speed, SignalValue.FromNumber(i), i);
                    publisher.Flush(0.01d * i);          // all inside the 100 ms window
                }
                Assert.AreEqual(1, _sent.Count, "the window must hold everything back");

                publisher.Flush(0.1d);
                Assert.AreEqual(2, _sent.Count);
                Assert.AreEqual(9d, _sent[1].Value.Number, "only the newest value is worth sending");
            }
        }

        [Test]
        public void SteadyValue_IsNotResentWhenTheWindowExpires()
        {
            using (SignalPublisher publisher = Publisher(rateHz: 10d))
            {
                _bus.Publish(Speed, SignalValue.FromNumber(200), 1L);
                publisher.Flush(0d);
                for (int i = 1; i < 50; i++)
                    publisher.Flush(i * 0.1d);
            }

            Assert.AreEqual(1, _sent.Count);
        }

        [Test]
        public void TopicsAreRateLimitedIndependently()
        {
            using (SignalPublisher publisher = Publisher(rateHz: 10d))
            {
                _bus.Publish(Speed, SignalValue.FromNumber(1), 1L);
                _bus.Publish(Gate, SignalValue.FromBool(true), 1L);
                publisher.Flush(0d);
                Assert.AreEqual(2, _sent.Count);

                _bus.Publish(Speed, SignalValue.FromNumber(2), 2L);
                publisher.Flush(0.2d);
                Assert.AreEqual(3, _sent.Count, "a quiet topic must not delay a busy one");
            }
        }

        [Test]
        public void ResendAll_RepeatsSteadyValuesOnce()
        {
            using (SignalPublisher publisher = Publisher(rateHz: 10d))
            {
                _bus.Publish(Speed, SignalValue.FromNumber(200), 1L);
                _bus.Publish(Gate, SignalValue.FromBool(true), 1L);
                publisher.Flush(0d);
                Assert.AreEqual(2, _sent.Count);

                publisher.ResendAll();
                publisher.Flush(1d);
                Assert.AreEqual(4, _sent.Count, "a link that just came up needs every topic again");
                CollectionAssert.AreEquivalent(new[] { Speed, Gate }, new[] { _sent[2].Topic, _sent[3].Topic });

                publisher.Flush(2d);
                Assert.AreEqual(4, _sent.Count, "and then goes quiet again");
            }
        }

        [Test]
        public void ChangesWhileNotFlushing_AreKeptAndCoalesced()
        {
            using (SignalPublisher publisher = Publisher(rateHz: 10d))
            {
                // What the bridge does during an outage: the bus keeps moving, nothing is flushed.
                _bus.Publish(Speed, SignalValue.FromNumber(1), 1L);
                _bus.Publish(Speed, SignalValue.FromNumber(2), 2L);
                _bus.Publish(Speed, SignalValue.FromNumber(3), 3L);
                Assert.AreEqual(0, _sent.Count);

                publisher.Flush(60d);
                Assert.AreEqual(1, _sent.Count);
                Assert.AreEqual(3d, _sent[0].Value.Number);
            }
        }

        [Test]
        public void InboundMessage_IsNotSentBack()
        {
            using (SignalPublisher publisher = Publisher())
            {
                var fromBroker = new SignalMessage(Speed, SignalValue.FromNumber(150), 7L);
                publisher.ExpectInbound(fromBroker);
                _bus.Publish(fromBroker);
                publisher.Flush(0d);
                Assert.AreEqual(0, _sent.Count, "the broker already has what it just delivered");

                publisher.ResendAll();
                publisher.Flush(1d);
                Assert.AreEqual(0, _sent.Count, "a topic owned by the far side is theirs to repeat");

                _bus.Publish(Speed, SignalValue.FromNumber(180), 8L);   // now changed locally
                publisher.Flush(2d);
                Assert.AreEqual(1, _sent.Count);
                Assert.AreEqual(180d, _sent[0].Value.Number);
            }
        }

        [Test]
        public void InboundMessage_DoesNotSwallowWhatItTriggers()
        {
            using (SignalPublisher publisher = Publisher())
            {
                // A local station reacting to the inbound command publishes its own state inside the same dispatch.
                using (_bus.Subscribe(Speed.ToString(), _ => _bus.Publish(Gate, SignalValue.FromBool(true), 9L)))
                {
                    var fromBroker = new SignalMessage(Speed, SignalValue.FromNumber(150), 7L);
                    publisher.ExpectInbound(fromBroker);
                    _bus.Publish(fromBroker);
                }
                publisher.Flush(0d);
            }

            Assert.AreEqual(1, _sent.Count);
            Assert.AreEqual(Gate, _sent[0].Topic);
        }

        [Test]
        public void Dispose_StopsWatchingTheBus()
        {
            SignalPublisher publisher = Publisher();
            publisher.Dispose();

            _bus.Publish(Speed, SignalValue.FromNumber(200), 1L);
            publisher.Flush(1d);

            Assert.AreEqual(0, _sent.Count);
        }

        [Test]
        public void Constructor_RejectsBadArguments()
        {
            Assert.Throws<ArgumentNullException>(() => new SignalPublisher(null, _sent.Add, 10d, 0d));
            Assert.Throws<ArgumentNullException>(() => new SignalPublisher(_bus, null, 10d, 0d));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SignalPublisher(_bus, _sent.Add, 0d, 0d));
            Assert.Throws<ArgumentOutOfRangeException>(() => new SignalPublisher(_bus, _sent.Add, 10d, -1d));
            Assert.Throws<ArgumentException>(() => new SignalPublisher(_bus, _sent.Add, 10d, 0d, "cell/#/bad"));
        }
    }
}
