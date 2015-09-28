﻿using Medidata.CrossApplicationTracer;
using Medidata.ZipkinTracer.Core.Collector;
using log4net;
using System;

namespace Medidata.ZipkinTracer.Core
{
    public class ZipkinClient : ITracerClient
    {
        internal bool isTraceOn;
        internal SpanCollector spanCollector;
        internal SpanTracer spanTracer;
        internal Span clientSpan;
        internal Span serverSpan;

        private string requestName;
        private ITraceProvider traceProvider;
        private ILog logger;

        public ZipkinClient(ITraceProvider tracerProvider, string requestName, ILog logger) : this(tracerProvider, requestName, logger, new ZipkinConfig(), new SpanCollectorBuilder()) { }

        public ZipkinClient(ITraceProvider traceProvider, string requestName, ILog logger, IZipkinConfig zipkinConfig, ISpanCollectorBuilder spanCollectorBuilder)
        {
            this.logger = logger;
            isTraceOn = true;

            if ( logger == null || IsConfigValuesNull(zipkinConfig) || !IsConfigValuesValid(zipkinConfig) || !IsTraceProviderValidAndSamplingOn(traceProvider))
            {
                isTraceOn = false;
            }

            if (isTraceOn)
            {
                try
                {
                    spanCollector = spanCollectorBuilder.Build(zipkinConfig.ZipkinServerName, int.Parse(zipkinConfig.ZipkinServerPort), int.Parse(zipkinConfig.SpanProcessorBatchSize));
                    spanCollector.Start();

                    spanTracer = new SpanTracer(spanCollector, zipkinConfig.ServiceName, new ServiceEndpoint());

                    this.requestName = requestName;
                    this.traceProvider = traceProvider;
                }
                catch (Exception ex)
                {
                    logger.Error("Error Building Zipkin Client Provider", ex);
                    isTraceOn = false;
                }
            }
        }

        public void StartClientTrace(string clientServiceName)
        {
            if (isTraceOn)
            {
                if (string.IsNullOrWhiteSpace(clientServiceName))
                {
                    logger.Error("clientServiceName is null or whitespace");
                    return;
                }

                try
                {
                    clientSpan = spanTracer.SendClientSpan(
                        requestName,
                        traceProvider.TraceId,
                        traceProvider.ParentSpanId,
                        traceProvider.SpanId,
                        clientServiceName);
                }
                catch (Exception ex)
                {
                    logger.Error("Error Starting Client Trace", ex);
                }
            }
        }

        public void EndClientTrace(int duration, string clientServiceName)
        {
            if (isTraceOn)
            {
                if (string.IsNullOrWhiteSpace(clientServiceName))
                {
                    logger.Error("clientServiceName is null or whitespace");
                    return;
                }

                try
                {
                    spanTracer.ReceiveClientSpan(
                        clientSpan,
                        duration,
                        clientServiceName);
                }
                catch (Exception ex)
                {
                    logger.Error("Error Ending Client Trace", ex);
                }
            }
        }

        public void StartServerTrace()
        {
            if (isTraceOn)
            {
                try
                {
                    serverSpan = spanTracer.ReceiveServerSpan(
                        requestName,
                        traceProvider.TraceId,
                        traceProvider.ParentSpanId,
                        traceProvider.SpanId);
                }
                catch (Exception ex)
                {
                    logger.Error("Error Starting Server Trace", ex);
                }
            }
        }

        public void EndServerTrace(int duration)
        {
            if (isTraceOn)
            {
                try
                {
                    spanTracer.SendServerSpan(
                        serverSpan,
                        duration);
                }
                catch (Exception ex)
                {
                    logger.Error("Error Ending Server Trace", ex);
                }
            }
        }

        public void ShutDown()
        {
            if (spanCollector != null)
            {
                spanCollector.Stop();
            }
        }

        private bool IsConfigValuesNull(IZipkinConfig zipkinConfig)
        {
            if (String.IsNullOrEmpty(zipkinConfig.ZipkinServerName))
            {
                logger.Error("zipkinConfig.ZipkinServerName is null");
                return true;
            }

            if (String.IsNullOrEmpty(zipkinConfig.ZipkinServerPort))
            {
                logger.Error("zipkinConfig.ZipkinServerPort is null");
                return true;
            }

            if (String.IsNullOrEmpty(zipkinConfig.ServiceName))
            {
                logger.Error("zipkinConfig.ServiceName value is null");
                return true;
            }

            if (String.IsNullOrEmpty(zipkinConfig.SpanProcessorBatchSize))
            {
                logger.Error("zipkinConfig.SpanProcessorBatchSize value is null");
                return true;
            }

            if (String.IsNullOrEmpty(zipkinConfig.ZipkinSampleRate))
            {
                logger.Error("zipkinConfig.ZipkinSampleRate value is null");
                return true;
            }
            return false;
        }

        private bool IsConfigValuesValid(IZipkinConfig zipkinConfig)
        {
            int port;
            int spanProcessorBatchSize;
            if (!int.TryParse(zipkinConfig.ZipkinServerPort, out port))
            {
                logger.Error("zipkinConfig port is not an int");
                return false;
            }

            if (!int.TryParse(zipkinConfig.SpanProcessorBatchSize, out spanProcessorBatchSize))
            {
                logger.Error("zipkinConfig spanProcessorBatchSize is not an int");
                return false;
            }
            return true;
        }

        private bool IsTraceProviderValidAndSamplingOn(ITraceProvider traceProvider)
        {
            if (traceProvider == null)
            {
                logger.Error("traceProvider value is null");
                return false;
            }
            else if (string.IsNullOrEmpty(traceProvider.TraceId) || !traceProvider.IsSampled)
            {
                return false;
            }
            return true;
        }

    }
}
