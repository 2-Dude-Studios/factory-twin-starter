using System;
using System.Collections.Generic;
using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class SignalBusTests
    {
        private static readonly TopicPath InfeedSpeed = new TopicPath("infeed", "drive_speed");
        private static readonly TopicPath PressHealth = new TopicPath("press", "health");

        [Test]
        public void Publish_DeliversOnlyToMatchingSubscribers()
        {
            var bus = new SignalBus();
            var press = new List<SignalMessage>();
            var all = new List<SignalMessage>();
            using (bus.Subscribe("cell/press/#", press.Add))
            using (bus.Subscribe(TopicFilter.All, all.Add))
            {
                bus.Publish(InfeedSpeed, SignalValue.FromNumber(0.35), 1L);
                bus.Publish(PressHealth, SignalValue.FromNumber(0.9), 2L);
            }

            Assert.AreEqual(1, press.Count);
            Assert.AreEqual(PressHealth, press[0].Topic);
            Assert.AreEqual(0.9, press[0].Value.Number);
            Assert.AreEqual(2L, press[0].UnixMs);
            Assert.AreEqual(2, all.Count);
        }

        [Test]
        public void Dispose_StopsDelivery()
        {
            var bus = new SignalBus();
            int calls = 0;
            IDisposable subscription = bus.Subscribe(TopicFilter.All, _ => calls++);

            bus.Publish(InfeedSpeed, SignalValue.FromNumber(1), 1L);
            subscription.Dispose();
            subscription.Dispose();
            bus.Publish(InfeedSpeed, SignalValue.FromNumber(2), 2L);

            Assert.AreEqual(1, calls);
        }

        [Test]
        public void TryGetLast_ReturnsMostRecentValuePerTopic()
        {
            var bus = new SignalBus();
            Assert.IsFalse(bus.TryGetLast(InfeedSpeed, out _));

            bus.Publish(InfeedSpeed, SignalValue.FromNumber(0.2), 1L);
            bus.Publish(InfeedSpeed, SignalValue.FromNumber(0.4), 2L);
            bus.Publish(PressHealth, SignalValue.FromNumber(1), 3L);

            Assert.IsTrue(bus.TryGetLast(InfeedSpeed, out SignalMessage last));
            Assert.AreEqual(0.4, last.Value.Number);
            Assert.AreEqual(2L, last.UnixMs);
            Assert.AreEqual(2, bus.TopicCount);
        }

        [Test]
        public void MessagePublished_FiresForEveryMessage()
        {
            var bus = new SignalBus();
            int calls = 0;
            void Handler(SignalMessage _) => calls++;

            bus.MessagePublished += Handler;
            bus.Publish(InfeedSpeed, SignalValue.FromNumber(1), 1L);
            bus.Publish(PressHealth, SignalValue.FromBool(true), 2L);
            bus.MessagePublished -= Handler;
            bus.Publish(PressHealth, SignalValue.FromBool(false), 3L);

            Assert.AreEqual(2, calls);
        }

        [Test]
        public void Handler_MayUnsubscribeItselfAndOthersDuringDispatch()
        {
            var bus = new SignalBus();
            var log = new List<string>();
            IDisposable first = null;
            IDisposable second = null;
            IDisposable third = null;

            first = bus.Subscribe(TopicFilter.All, _ => log.Add("first"));
            second = bus.Subscribe(TopicFilter.All, _ =>
            {
                log.Add("second");
                first.Dispose();
                second.Dispose();
                third.Dispose();
            });
            third = bus.Subscribe(TopicFilter.All, _ => log.Add("third"));

            bus.Publish(InfeedSpeed, SignalValue.FromNumber(1), 1L);
            bus.Publish(InfeedSpeed, SignalValue.FromNumber(2), 2L);

            // 'third' was disposed before its turn, so it must not run even within the same dispatch.
            CollectionAssert.AreEqual(new[] { "first", "second" }, log);
        }

        [Test]
        public void Handler_SubscribingDuringDispatchStartsWithNextMessage()
        {
            var bus = new SignalBus();
            int lateCalls = 0;
            bool added = false;
            bus.Subscribe(TopicFilter.All, _ =>
            {
                if (added)
                    return;
                added = true;
                bus.Subscribe(TopicFilter.All, __ => lateCalls++);
            });

            bus.Publish(InfeedSpeed, SignalValue.FromNumber(1), 1L);
            Assert.AreEqual(0, lateCalls);

            bus.Publish(InfeedSpeed, SignalValue.FromNumber(2), 2L);
            Assert.AreEqual(1, lateCalls);
        }

        [Test]
        public void Handler_MayPublishReentrantly()
        {
            var bus = new SignalBus();
            var seen = new List<TopicPath>();
            bus.Subscribe("cell/infeed/#", _ => bus.Publish(PressHealth, SignalValue.FromNumber(0.5), 2L));
            bus.Subscribe(TopicFilter.All, m => seen.Add(m.Topic));

            bus.Publish(InfeedSpeed, SignalValue.FromNumber(1), 1L);

            CollectionAssert.AreEquivalent(new[] { InfeedSpeed, PressHealth }, seen);
        }

        [Test]
        public void Subscribe_ValidatesArguments()
        {
            var bus = new SignalBus();

            Assert.Throws<ArgumentException>(() => bus.Subscribe("cell/#/state", _ => { }));
            Assert.Throws<ArgumentNullException>(() => bus.Subscribe(TopicFilter.All, null));
        }
    }
}
