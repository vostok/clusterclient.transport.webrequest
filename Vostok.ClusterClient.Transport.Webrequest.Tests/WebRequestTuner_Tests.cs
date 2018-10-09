﻿using System.Net;
using NUnit.Framework;
using Vostok.Commons.Time;

namespace Vostok.ClusterClient.Transport.Webrequest.Tests
{
    [TestFixture]
    internal class WebRequestTuner_Tests
    {
        [Test]
        public void Should_successfully_tune_http_web_request()
        {
            var request = WebRequest.CreateHttp("http://kontur.ru/");

            WebRequestTuner.Tune(request, 1.Seconds(), new WebRequestTransportSettings());
        }
    }
}