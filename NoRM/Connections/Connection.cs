﻿using System;
using System.IO;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace Norm
{
    //todo: cleanup, timeout, age hanlding

    /// <summary>
    /// TCP client MongoDB connection
    /// </summary>
    public class Connection : IConnection, IOptionsContainer
    {
        private readonly ConnectionOptions _builder;
        private IOptionsContainer _connectionOptions;
        private readonly TcpClient _client;
        private NetworkStream _netStream;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="Connection"/> class.
        /// </summary>
        /// <param retval="retval">The retval.</param>
        internal Connection(ConnectionOptions builder)
        {
            _connectionOptions = _builder = builder;
            Created = DateTime.Now;
            _client = new TcpClient
            {
                NoDelay = true,
                ReceiveTimeout = builder.QueryTimeout * 1000,
                SendTimeout = builder.QueryTimeout * 1000
            };
            _client.Connect(builder.Servers[0].Host, builder.Servers[0].Port);
        }

        /// <summary>
        /// Gets the client.
        /// </summary>
        /// <value>The client.</value>
        public TcpClient Client
        {
            get { return _client; }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is connected.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance is connected; otherwise, <c>false</c>.
        /// </value>
        public bool IsConnected
        {
            get { return Client.Connected; }
        }

        /// <summary>
        /// Gets a value indicating whether this instance is invalid.
        /// </summary>
        /// <value>
        /// 	<c>true</c> if this instance is invalid; otherwise, <c>false</c>.
        /// </value>
        public bool IsInvalid { get; private set; }

        /// <summary>
        /// Gets the created date and time.
        /// </summary>
        /// <value>The created.</value>
        public DateTime Created { get; private set; }


        /// <summary>
        /// Digests the specified nonce.
        /// </summary>
        /// <param retval="nonce">The nonce.</param>
        /// <returns></returns>
        public string Digest(string nonce)
        {
            using (var md5 = MD5.Create())
            {
                var rawDigest = Encoding.UTF8.GetBytes(string.Concat(nonce, UserName, CreatePasswordDigest()));
                var hashed = md5.ComputeHash(rawDigest);
                var sb = new StringBuilder(hashed.Length * 2);
                Array.ForEach(hashed, b => sb.Append(b.ToString("X2")));
                return sb.ToString().ToLower();
            }
        }

        /// <summary>
        /// Create the password digest from the username and password.
        /// </summary>
        /// <returns>The password digest.</returns>
        private string CreatePasswordDigest()
        {
            using (var md5 = MD5.Create())
            {
                var rawDigest = Encoding.UTF8.GetBytes(string.Concat(_connectionOptions.UserName, ":mongo:", _connectionOptions.Password));
                var hashed = md5.ComputeHash(rawDigest);
                var sb = new StringBuilder(hashed.Length * 2);
                Array.ForEach(hashed, b => sb.Append(b.ToString("X2")));
                return sb.ToString().ToLower();
            }
        }

        /// <summary>
        /// Gets the stream.
        /// </summary>
        /// <returns></returns>
        public NetworkStream GetStream()
        {
            if (_netStream == null)
            {
                _netStream = Client.GetStream();
            }

            return _netStream;
        }

        /// <summary>
        /// Writes the specified bytes.
        /// </summary>
        /// <param retval="bytes">The bytes.</param>
        /// <param retval="start">The start.</param>
        /// <param retval="size">The size.</param>
        public void Write(byte[] bytes, int start, int size)
        {
            try
            {
                GetStream().Write(bytes, 0, size);
            }
            catch (IOException)
            {
                IsInvalid = true;
                throw;
            }
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
        }

        /// <summary>
        /// Releases unmanaged and - optionally - managed resources
        /// </summary>
        /// <param retval="disposing"><c>true</c> to release both managed and unmanaged resources; <c>false</c> to release only unmanaged resources.</param>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
            {
                return;
            }

            _client.Close();
            if (_netStream != null)
            {
                _netStream.Flush();
                _netStream.Close();
            }
            _disposed = true;
        }

        /// <summary>
        /// Releases unmanaged resources and performs other cleanup operations before the
        /// <see cref="Connection"/> is reclaimed by garbage collection.
        /// </summary>
        ~Connection()
        {
            Dispose(false);
        }

        public string ConnectionString
        {
            get { return this._builder.ToString(); }
        }

        #region IOptionsContainer members.

        public IList<Server> Servers
        {
            get { return this._connectionOptions.Servers; }
        }

        public string UserName
        {
            get { return this._connectionOptions.UserName; }
        }

        public string Password
        {
            get { return this._connectionOptions.Password; }
        }

        public string Database
        {
            get { return this._connectionOptions.Database; }
        }

        public int QueryTimeout
        {
            get { return this._connectionOptions.QueryTimeout; }
        }

        public bool StrictMode
        {
            get { return this._connectionOptions.StrictMode; }
        }

        public bool Pooled
        {
            get { return this._connectionOptions.Pooled; }
        }

        public int PoolSize
        {
            get { return this._connectionOptions.PoolSize; }
        }

        public int Timeout
        {
            get { return this._connectionOptions.Timeout; }
        }

        public int Lifetime
        {
            get { return this._connectionOptions.Lifetime; }
        }

        public int? VerifyWriteCount
        {
            get { return this._connectionOptions.VerifyWriteCount; }
        }


        #endregion

        private static readonly Regex _rxConnectStringWithOutQuery = new Regex("^([^?]+)", RegexOptions.Compiled);

        public void LoadOptions(string options)
        {
            var newOpts = ((ConnectionOptions)_builder.Clone());
            newOpts.AssignOptions(options);
            this._connectionOptions = newOpts;
        }

        public void ResetOptions()
        {
            this._connectionOptions = this._builder;
        }

    }
}
