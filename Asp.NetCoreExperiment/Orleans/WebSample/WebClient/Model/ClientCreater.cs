﻿using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Orleans;
using Orleans.Configuration;
using Orleans.Hosting;
using Orleans.Runtime;
using System;
using System.Threading.Tasks;

namespace WebClient.Model
{
    public class ClientCreater : IClientCreater
    {
        const string invariant = "System.Data.SqlClient";
        readonly ILogger<ClientCreater> _logger;
        readonly int initializeAttemptsBeforeFailing = 5;
        readonly string _connectionString;
        private int attempt = 0;
        public ClientCreater(ILogger<ClientCreater> logger, IConfiguration configuration)
        {
            _logger = logger;            
            _connectionString = configuration.GetConnectionString("DefaultConnectionString");        
        }
        public async Task<IClusterClient> CreateClient(string clusterId, string serviceId)
        {
            attempt = 0;
            var client = new ClientBuilder()
                 .UseLocalhostClustering()
                 .Configure<ClusterOptions>(options =>
                 {
                     options.ClusterId = clusterId;
                     options.ServiceId = serviceId;
                 })
                 .UseAdoNetClustering(options =>
                 {
                     options.Invariant = invariant;
                     options.ConnectionString = _connectionString;
                 })
                 .ConfigureLogging(logging => logging.AddConsole())
                 .Build();

            await client.Connect(RetryFilter);

            return client;
        }

        /// <summary>
        /// 重连
        /// </summary>
        /// <param name="exception"></param>
        /// <returns></returns>
        private async Task<bool> RetryFilter(Exception exception)
        {
            if (exception.GetType() != typeof(SiloUnavailableException))
            {
                _logger.LogInformation($"集群客户端失败连接，返回unexpected error.  Exception: {exception}");
                return false;
            }
            attempt++;
            _logger.LogInformation($"集群客户端试图 {attempt}/{initializeAttemptsBeforeFailing} 失败连接.  Exception: {exception}");
            if (attempt > initializeAttemptsBeforeFailing)
            {
                return false;
            }
            await Task.Delay(TimeSpan.FromSeconds(4));
            return true;
        }
    }
}
