using DMicroservices.Utils.Logger;
using MessagePack;
using RedLockNet.SERedis;
using RedLockNet.SERedis.Configuration;
using StackExchange.Redis;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;

namespace DMicroservices.DataAccess.Redis
{
    public class RedLockManager
    {
        private static readonly string _redisUrl = Environment.GetEnvironmentVariable("REDIS_URL");
        private static readonly string _containerPodName = Environment.GetEnvironmentVariable("POD_NAME") ??
                                                             Environment.MachineName ??
                                                             Guid.NewGuid().ToString("N")[..8];

        private static volatile IConnectionMultiplexer _lockConnection;

        private static RedLockFactory _redLockFactory;
        private static readonly object _factoryLock = new object();

        #region Singleton section
        private static readonly Lazy<RedLockManager> _instance =
            new Lazy<RedLockManager>(() => new RedLockManager());

        private RedLockManager()
        {
            LogInfo($"Container {_containerPodName}: RedLockManager Oluşturuldu.");
            AppDomain.CurrentDomain.ProcessExit += OnContainerShutdown;
            Console.CancelKeyPress += (s, e) => OnContainerShutdown(s, null);
        }

        public static RedLockManager Instance => _instance.Value;
        #endregion

        #region Lock Operations

        public bool TryGetLockFactory(out RedLockFactory factory)
        {
            if (_redLockFactory != null && _lockConnection.IsConnected)
            {
                factory = _redLockFactory;
                return true;
            }

            lock (_factoryLock)
            {
                if (_redLockFactory != null && _lockConnection.IsConnected)
                {
                    factory = _redLockFactory;
                    return true;
                }

                LogInfo($"Container {_containerPodName}: RedisConnection sağlanmadı veya factory henüz oluşturulamamış. Tekrar oluşturulacak");

                _redLockFactory?.Dispose();
                _lockConnection?.Close();
                _lockConnection?.Dispose();

                if (string.IsNullOrEmpty(_redisUrl))
                {
                    LogError("Redis URL bulunamadı! Redlock factory oluşturulamadı!");
                    factory = null;
                    return false;
                }

                try
                {
                    var options = ConfigurationOptions.Parse(_redisUrl);
                    options.AbortOnConnectFail = false;
                    options.ConnectRetry = 3;
                    options.ConnectTimeout = 3000;  
                    options.SyncTimeout = 3000;     
                    options.AsyncTimeout = 3000;
                    options.KeepAlive = 30;         
                    options.DefaultDatabase = 15;

                    _lockConnection = ConnectionMultiplexer.Connect(options);
                    _redLockFactory = RedLockFactory.Create(new List<RedLockMultiplexer> { _lockConnection as ConnectionMultiplexer });

                    factory = _redLockFactory;
                    return true;
                }
                catch (Exception ex)
                {
                    LogError($"Container {_containerPodName}: RedLock factory oluşturulurken hata aldı: {ex.Message}", ex);
                    factory = null;
                    _redLockFactory = null;
                    _lockConnection = null;
                    return false;
                }
            }
        }

        #endregion

        #region Container Lifecycle Management

        /// <summary>
        /// pod ölürken redis bağlantılarını kapatır.
        /// </summary>
        private void OnContainerShutdown(object sender, EventArgs e)
        {
            try
            {
                LogInfo($"Pod {_containerPodName}: Kapatılıyor! redis aşamalı kapatma işlemi başladı!");

                _lockConnection?.Close();
                _lockConnection?.Dispose();
                _redLockFactory?.Dispose();

                LogInfo($"Pod {_containerPodName}: Tüm Redis bağlantıları temizlendi");
            }
            catch (Exception ex)
            {
                LogError($"Pod shutdown hatası: {ex.Message}", ex);
            }
        }

        #endregion

        #region eager başlatma
        public void PrepareConnection()
        {
            LogInfo("Redlock connectionu hazırlanıyor!");
            try
            {
                SetPrepareConnection();
            }
            catch (Exception ex)
            {
                LogError("Redlock erken başlatma başarısız! connection alınamadı!", ex);
            }
        }
        #endregion

        private void SetPrepareConnection()
        {
            TryGetLockFactory(out _);

            if (_redLockFactory != null)
            {
                LogInfo("Redlock Connection nesnesi hazırlandı! ön başlatma hazır!");
            }
            else
            {
                LogError("Redlock erken başlatma başarısız! connection nesnesi hazırlanamadı!");
            }
        }

        #region Logging

        private void LogInfo(string message)
        {
            try
            {
                Console.WriteLine($"[INFO] {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - RedLockManager: {message}");
            }
            catch
            {
            }
        }

        private void LogError(string message, Exception ex = null)
        {
            try
            {
                Console.WriteLine($"[ERROR] {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} - RedLockManager: {message}");
            }
            catch
            {
            }
        }

        #endregion
    }
}