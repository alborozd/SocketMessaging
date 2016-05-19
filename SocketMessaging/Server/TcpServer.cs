﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;

namespace SocketMessaging.Server
{
    public class TcpServer
    {
		public TcpServer()
		{
			_connections = new List<Connection>();
		}

		public void Start(int port)
		{
			if (_listener != null)
				throw new InvalidOperationException("Already started.");

			var address = new IPAddress(0);
			_listener = new TcpListenerEx(address, port);
			_listener.Start();

			startPollingThread();
		}

		public bool IsStarted { get { return _listener != null && _listener.Active; } }

		public void Stop()
		{
			if (_listener == null)
				throw new InvalidOperationException("Not started");

			stopPollingThread();
			_listener.Stop();
			_listener = null;
		}

		public IPEndPoint LocalEndpoint { get { return _listener.LocalEndpoint as IPEndPoint; } }

		public IEnumerable<Connection> Connections { get { return _connections.AsEnumerable(); } }

		#region Public events

		public event EventHandler<ConnectionEventArgs> Connected;
		protected virtual void OnConnected(ConnectionEventArgs e)
		{
			Connected?.Invoke(this, e);
		}

		public event EventHandler<ConnectionEventArgs> Disconnected;
		protected virtual void OnDisconnected(ConnectionEventArgs e)
		{
			Disconnected?.Invoke(this, e);
		}

		#endregion

		#region Private methods

		private void startPollingThread()
		{
			if (_pollThread != null)
				throw new InvalidOperationException("Polling thread already exists.");

			_pollThread = new Thread(new ThreadStart(pollThread_run))
			{
				Name = "PollThread",
				IsBackground = true
			};

			_pollThread.Start();
		}

		private void stopPollingThread()
		{
			_pollThread.Abort();
			_pollThread = null;
		}

		private void pollThread_run()
		{
			while (true)
			{
				acceptPendingConnections();

				for (var index = _connections.Count - 1; index >= 0; index--)
				{
					//DebugInfo("Polling connection {0}...", index);
					var connection = _connections[index];
					if (connection.Available > 0)
					{
						DebugInfo("Connection {0} sent {1} bytes", connection.Id, connection.Available);
						var buffer = new byte[connection.Available];
						connection.Receive(buffer);
					}
					else if (!isConnected(connection))
					{
						DebugInfo("Connection {0} disconnected", connection.Id);
						_connections.Remove(connection);
						OnDisconnected(new ConnectionEventArgs(connection));
					}
				}

				Thread.Sleep(POLLTHREAD_SLEEP);
			}
		}

		private bool isConnected(Connection connection)
		{
			return connection.IsConnected;
		}

		private void acceptPendingConnections()
		{
			while (_listener.Pending())
			{
				var socket = _listener.AcceptSocket();
				var connection = new Connection(0, socket);
				_connections.Add(connection);
				OnConnected(new ConnectionEventArgs(connection));
				DebugInfo("Connection {0} connected.", connection.Id);
			}
		}

		#endregion Private methods

		#region Debug logging

		[System.Diagnostics.Conditional("DEBUG")]
		void DebugInfo(string format, params object[] args)
		{
			if (_debugInfoTime == null)
			{
				_debugInfoTime = new System.Diagnostics.Stopwatch();
				_debugInfoTime.Start();
			}
			System.Diagnostics.Debug.WriteLine(_debugInfoTime.ElapsedMilliseconds + ": " + format, args);
		}
		System.Diagnostics.Stopwatch _debugInfoTime;

		#endregion Debug logging


		TcpListenerEx _listener = null;
		internal Thread _pollThread = null;
		readonly List<Connection> _connections;

		const int POLLTHREAD_SLEEP = 20;
	}
}
