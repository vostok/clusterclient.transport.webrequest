using System;
using System.Linq;
using System.Net;
using FluentAssertions;
using NUnit.Framework;
using Vostok.Clusterclient.Transport.Webrequest.ArpCache;

namespace Vostok.Clusterclient.Transport.Webrequest.Tests
{
    [TestFixture]
    internal class IPv4AddressExtensions_Tests
    {
        [Test]
        public void ToUInt32_should_work_correctly()
        {
            var address = IPAddress.Parse("46.17.203.102");
            
            address.ToUInt32().Should().Be(BitConverter.ToUInt32(address.GetAddressBytes().Reverse().ToArray(), 0));
        }

        [Test]
        public void ToUInt32BigEndian_should_work_correctly()
        {
            var address = IPAddress.Parse("46.17.203.102");

            address.ToUInt32BigEndian().Should().Be(BitConverter.ToUInt32(address.GetAddressBytes(), 0));
        }

        [Test]
        public void ToUInt32_should_throw_for_non_IPv4_addresses()
        {
            var address = IPAddress.IPv6Loopback;

            new Action(() => address.ToUInt32()).Should().Throw<ArgumentException>();
        }

        [Test]
        public void ToUInt32BigEndian_should_throw_for_non_IPv4_addresses()
        {
            var address = IPAddress.IPv6Loopback;

            new Action(() => address.ToUInt32BigEndian()).Should().Throw<ArgumentException>();
        }
    }
}