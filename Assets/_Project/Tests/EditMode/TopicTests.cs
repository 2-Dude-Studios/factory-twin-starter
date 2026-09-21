using System;
using NUnit.Framework;
using TwoDude.FactoryTwin.Twin;

namespace TwoDude.FactoryTwin.Tests
{
    public sealed class TopicTests
    {
        [TestCase("cell/infeed/drive_speed", "infeed", "drive_speed")]
        [TestCase("cell/gate1/part_present", "gate1", "part_present")]
        [TestCase("cell/arm/j3_deg", "arm", "j3_deg")]
        [TestCase("cell/press/tool_wear_min", "press", "tool_wear_min")]
        public void Parse_AcceptsSchemeExamples(string topic, string station, string signal)
        {
            TopicPath path = TopicPath.Parse(topic);

            Assert.AreEqual(station, path.Station);
            Assert.AreEqual(signal, path.Signal);
            Assert.AreEqual(topic, path.ToString());
        }

        [TestCase(null)]
        [TestCase("")]
        [TestCase("cell")]
        [TestCase("cell/infeed")]
        [TestCase("cell/infeed/drive/speed")]
        [TestCase("plant/infeed/drive_speed")]
        [TestCase("cells/infeed/drive_speed")]
        [TestCase("cell//drive_speed")]
        [TestCase("cell/infeed/")]
        [TestCase("cell/Infeed/drive_speed")]
        [TestCase("cell/infeed/drive speed")]
        [TestCase("cell/+/drive_speed")]
        [TestCase("cell/infeed/#")]
        public void TryParse_RejectsAnythingOffScheme(string topic)
        {
            Assert.IsFalse(TopicPath.TryParse(topic, out _));
        }

        [Test]
        public void Parse_ThrowsFormatExceptionOnBadTopic()
        {
            Assert.Throws<FormatException>(() => TopicPath.Parse("not/a/valid/topic"));
        }

        [Test]
        public void Constructor_RejectsInvalidSegments()
        {
            Assert.Throws<ArgumentException>(() => new TopicPath("in/feed", "speed"));
            Assert.Throws<ArgumentException>(() => new TopicPath("infeed", ""));
        }

        [Test]
        public void Equality_IsByTopicString()
        {
            var a = new TopicPath("arm", "j1_deg");
            TopicPath b = TopicPath.Parse("cell/arm/j1_deg");

            Assert.AreEqual(a, b);
            Assert.IsTrue(a == b);
            Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
            Assert.AreNotEqual(a, new TopicPath("arm", "j2_deg"));
        }

        [TestCase("cell/#", "cell/press/health", true)]
        [TestCase("cell/press/#", "cell/press/health", true)]
        [TestCase("cell/press/#", "cell/press", true)]
        [TestCase("cell/press/#", "cell/infeed/state", false)]
        [TestCase("cell/+/state", "cell/infeed/state", true)]
        [TestCase("cell/+/state", "cell/infeed/drive_speed", false)]
        [TestCase("cell/+", "cell/infeed/state", false)]
        [TestCase("cell/infeed/state", "cell/infeed/state", true)]
        [TestCase("cell/infeed/state", "cell/infeed/state2", false)]
        [TestCase("cell/infeed/state/extra", "cell/infeed/state", false)]
        [TestCase("#", "cell/infeed/state", true)]
        public void Filter_Matches(string filter, string topic, bool expected)
        {
            Assert.AreEqual(expected, TopicFilter.Matches(filter, topic));
        }

        [TestCase("cell/#", true)]
        [TestCase("cell/+/state", true)]
        [TestCase("cell/infeed/state", true)]
        [TestCase("", false)]
        [TestCase("cell/#/state", false)]
        [TestCase("cell/pre#", false)]
        [TestCase("cell/in+/state", false)]
        [TestCase("cell//state", false)]
        [TestCase("cell/infeed/", false)]
        public void Filter_IsValid(string filter, bool expected)
        {
            Assert.AreEqual(expected, TopicFilter.IsValid(filter));
        }
    }
}
