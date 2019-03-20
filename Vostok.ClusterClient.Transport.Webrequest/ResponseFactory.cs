﻿using System.IO;
using Vostok.Clusterclient.Core.Model;

namespace Vostok.Clusterclient.Transport.Webrequest
{
    internal class ResponseFactory
    {
        private readonly WebRequestTransportSettings settings;

        public ResponseFactory(WebRequestTransportSettings settings)
        {
            this.settings = settings;
        }

        public Response BuildSuccessResponse(WebRequestState state)
        {
            return BuildResponse((ResponseCode)(int)state.Response.StatusCode, state);
        }

        public Response BuildFailureResponse(HttpActionStatus status, WebRequestState state)
        {
            switch (status)
            {
                case HttpActionStatus.ConnectionFailure:
                    return BuildResponse(ResponseCode.ConnectFailure, state);

                case HttpActionStatus.SendFailure:
                    return BuildResponse(ResponseCode.SendFailure, state);

                case HttpActionStatus.ReceiveFailure:
                    return BuildResponse(ResponseCode.ReceiveFailure, state);

                case HttpActionStatus.Timeout:
                    return BuildResponse(ResponseCode.RequestTimeout, state);

                case HttpActionStatus.RequestCanceled:
                    return BuildResponse(ResponseCode.Canceled, state);

                case HttpActionStatus.InsufficientStorage:
                    return BuildResponse(ResponseCode.InsufficientStorage, state);

                case HttpActionStatus.UserStreamFailure:
                    return BuildResponse(ResponseCode.StreamInputFailure, state);

                default:
                    return BuildResponse(ResponseCode.UnknownFailure, state);
            }
        }

        public Response BuildResponse(ResponseCode code, WebRequestState state)
        {
            return new Response(
                code,
                CreateResponseContent(state),
                CreateResponseHeaders(state),
                CreateResponseStream(state)
            );
        }

        private Content CreateResponseContent(WebRequestState state)
        {
            if (state.ReturnStreamDirectly)
                return null;

            if (state.BodyBuffer != null)
                return new Content(state.BodyBuffer, 0, state.BodyBufferLength);

            if (state.BodyStream != null)
                return new Content(state.BodyStream.GetBuffer(), 0, (int)state.BodyStream.Position);

            return null;
        }

        private Headers CreateResponseHeaders(WebRequestState state)
        {
            var headers = Headers.Empty;

            if (state.Response == null)
                return headers;

            foreach (var key in state.Response.Headers.AllKeys)
            {
                var headerValue = state.Response.Headers[key];

                if (settings.FixNonAsciiHeaders)
                    headerValue = NonAsciiHeadersFixer.FixResponseHeaderValue(headerValue);

                headers = headers.Set(key, headerValue);
            }

            return headers;
        }

        private Stream CreateResponseStream(WebRequestState state)
        {
            return state.ReturnStreamDirectly ? new ResponseBodyStream(state) : null;
        }
    }
}