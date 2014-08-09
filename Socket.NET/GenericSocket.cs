#region MIT License - Copyright (c) 2014 qwerty01 (qw3rty01@gmail.com, http://github.com/qwerty01)
/* 
 * The MIT License (MIT)
 * 
 * Copyright (c) 2014 qwerty01 (qw3rty01@gmail.com, http://github.com/qwerty01)
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
#endregion
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace Socket.NET
{
    class GenericSocket : IDisposable
    {
        public delegate void SendStringDelegate(string data, SocketError error);

        private Stack<SocketAsyncEventArgs> _eventPool;

        protected System.Net.Sockets.Socket socket;
        protected int defaultBuffLen;

        public bool Connected { get { return socket.Connected; } }

        protected internal GenericSocket(bool tcp = true, int defaultBuffLen = 256)
        {
            socket = (tcp) ?
                new System.Net.Sockets.Socket(System.Net.Sockets.SocketType.Dgram, System.Net.Sockets.ProtocolType.Udp) :
                new System.Net.Sockets.Socket(System.Net.Sockets.SocketType.Stream, System.Net.Sockets.ProtocolType.Tcp);
            _eventPool = new Stack<SocketAsyncEventArgs>();
            this.defaultBuffLen = defaultBuffLen;
        }

        public int Send(string data)
        {
            // Returns sent data length, -1 on error
            if (!Connected)
                return -1;
            return socket.Send(UnicodeEncoding.UTF8.GetBytes(data));
        }
        public int Send(string data, SendStringDelegate callback)
        {
            // Returns error code, 0 on success
            if (!Connected)
                return (int)SocketError.NotConnected;

            SocketAsyncEventArgs e = getArg(UTF8Encoding.UTF8.GetBytes(data), callback, SendStringCallback);
            if (socket.SendAsync(e))
                return (int)e.SocketError; // Send was done asynchronously
            else
            {
                //Send is done synchronously
                int ret = (int)e.SocketError;
                callback(data, e.SocketError); // Call the callback
                recycleArg(e, SendStringCallback);
                return ret;
            }
        }

        public byte[] Recieve()
        {
            // Figure out how to do buffer sizes for receive (while loop checking length of Receive()?)
        }

        protected void SendStringCallback(object sender, SocketAsyncEventArgs e)
        {
            SendStringDelegate callback = (SendStringDelegate)e.UserToken;
            callback(UTF8Encoding.UTF8.GetString(e.Buffer), e.SocketError);
            recycleArg(e, SendStringCallback);
        }

        protected SocketAsyncEventArgs getArg(byte[] buffer, SendStringDelegate callback, EventHandler<SocketAsyncEventArgs> handler)
        {
            return getArg(buffer, 0, buffer.Length, callback, handler);
        }
        protected SocketAsyncEventArgs getArg(byte[] buffer, int count, SendStringDelegate callback, EventHandler<SocketAsyncEventArgs> handler)
        {
            return getArg(buffer, 0, count, callback, handler);
        }
        protected SocketAsyncEventArgs getArg(byte[] buffer, int offset, int count, SendStringDelegate callback, EventHandler<SocketAsyncEventArgs> handler)
        {
            SocketAsyncEventArgs e = (_eventPool.Count == 0) ? new SocketAsyncEventArgs() : _eventPool.Pop();
            e.SetBuffer(buffer, offset, count);
            e.UserToken = callback;
            e.Completed += handler;
            e.SocketError = SocketError.Success;
            return e;
        }

        protected void recycleArg(SocketAsyncEventArgs e, EventHandler<SocketAsyncEventArgs> handler)
        {
            e.UserToken = null;
            e.SetBuffer(null, 0, 0);
            e.Completed -= handler;
            _eventPool.Push(e);
        }

        public void Dispose()
        {
            while (_eventPool.Count > 0)
                _eventPool.Pop().Dispose();
            if (Connected)
                socket.Disconnect(false);
            socket.Close();
        }
    }
}
