﻿// Copyright (c) .NET Foundation. All rights reserved.
// Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.

using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Framework.MemoryPool;

namespace Microsoft.AspNet.WebUtilities
{
    /// <summary>
    /// Writes to the <see cref="Stream"/> using the supplied <see cref="Encoding"/>.
    /// It does not write the BOM and also does not close the stream.
    /// </summary>
    public class HttpResponseStreamWriter : TextWriter
    {
        private const int DefaultBufferSize = 1024;
        private readonly Stream _stream;
        private Encoder _encoder;
        private LeasedArraySegment<byte> _leasedByteBuffer;
        private LeasedArraySegment<char> _leasedCharBuffer;
        private ArraySegment<byte> _byteBuffer;
        private ArraySegment<char> _charBuffer;
        private int _charBufferSize;
        private int _charBufferCount;

        public HttpResponseStreamWriter(Stream stream, Encoding encoding)
            : this(stream, encoding, DefaultBufferSize)
        {
        }

        public HttpResponseStreamWriter(Stream stream, Encoding encoding, int bufferSize)
        {
            _stream = stream;
            Encoding = encoding;
            _encoder = encoding.GetEncoder();
            _charBufferSize = bufferSize;
            _charBuffer = new ArraySegment<char>(new char[bufferSize]);
            _byteBuffer = new ArraySegment<byte>(new byte[encoding.GetMaxByteCount(bufferSize)]);
        }

        public HttpResponseStreamWriter(Stream stream, Encoding encoding, LeasedArraySegment<char> leasedCharBuffer, LeasedArraySegment<byte> leasedByteBuffer)
        {
            _stream = stream;
            Encoding = encoding;
            _encoder = encoding.GetEncoder();
            _charBufferSize = leasedCharBuffer.Data.Count;
            _charBuffer = leasedCharBuffer.Data;
            _byteBuffer = leasedByteBuffer.Data;
            _leasedCharBuffer = leasedCharBuffer;
            _leasedByteBuffer = leasedByteBuffer;
        }

        public override Encoding Encoding { get; }

        public override void Write(char value)
        {
            if (_charBufferCount == _charBufferSize)
            {
                FlushInternal();
            }

            _charBuffer.Array[_charBuffer.Offset + _charBufferCount] = value;
            _charBufferCount++;
        }

        public override void Write(char[] values, int index, int count)
        {
            if (values == null)
            {
                return;
            }

            while (count > 0)
            {
                if (_charBufferCount == _charBufferSize)
                {
                    FlushInternal();
                }

                CopyToCharBuffer(values, ref index, ref count);
            }
        }

        public override void Write(string value)
        {
            if (value == null)
            {
                return;
            }

            var count = value.Length;
            var index = 0;
            while (count > 0)
            {
                if (_charBufferCount == _charBufferSize)
                {
                    FlushInternal();
                }

                CopyToCharBuffer(value, ref index, ref count);
            }
        }

        public override async Task WriteAsync(char value)
        {
            if (_charBufferCount == _charBufferSize)
            {
                await FlushInternalAsync();
            }

            _charBuffer.Array[_charBuffer.Offset + _charBufferCount] = value;
            _charBufferCount++;
        }

        public override async Task WriteAsync(char[] values, int index, int count)
        {
            if (values == null)
            {
                return;
            }

            while (count > 0)
            {
                if (_charBufferCount == _charBufferSize)
                {
                    await FlushInternalAsync();
                }

                CopyToCharBuffer(values, ref index, ref count);
            }
        }

        public override async Task WriteAsync(string value)
        {
            if (value == null)
            {
                return;
            }

            var count = value.Length;
            var index = 0;
            while (count > 0)
            {
                if (_charBufferCount == _charBufferSize)
                {
                    await FlushInternalAsync();
                }

                CopyToCharBuffer(value, ref index, ref count);
            }
        }

        // We want to flush the stream when Flush/FlushAsync is explicitly
        // called by the user (example: from a Razor view).

        public override void Flush()
        {
            FlushInternal(true, true);
        }

        public override Task FlushAsync()
        {
            return FlushInternalAsync(flushStream: true, flushEncoder: true);
        }

        // Do not flush the stream on Dispose, as this will cause response to be
        // sent in chunked encoding in case of Helios.
        protected override void Dispose(bool disposing)
        {
            FlushInternal(flushStream: false, flushEncoder: true);

            if (_leasedByteBuffer != null)
            {
                _leasedByteBuffer.Owner.Return(_leasedByteBuffer);
            }

            if (_leasedCharBuffer != null)
            {
                _leasedCharBuffer.Owner.Return(_leasedCharBuffer);
            }
        }

        private void FlushInternal(bool flushStream = false, bool flushEncoder = false)
        {
            if (_charBufferCount == 0)
            {
                return;
            }

            var count = _encoder.GetBytes(
                _charBuffer.Array,
                _charBuffer.Offset,
                _charBufferCount,
                _byteBuffer.Array,
                _byteBuffer.Offset,
                flushEncoder);

            if (count > 0)
            {
                _stream.Write(_byteBuffer.Array, _byteBuffer.Offset, count);
            }

            _charBufferCount = 0;

            if (flushStream)
            {
                _stream.Flush();
            }
        }

        private async Task FlushInternalAsync(bool flushStream = false, bool flushEncoder = false)
        {
            if (_charBufferCount == 0)
            {
                return;
            }

            var count = _encoder.GetBytes(
                _charBuffer.Array,
                _charBuffer.Offset,
                _charBufferCount,
                _byteBuffer.Array,
                _byteBuffer.Offset,
                flushEncoder);

            if (count > 0)
            {
                await _stream.WriteAsync(_byteBuffer.Array, _byteBuffer.Offset, count);
            }

            _charBufferCount = 0;

            if (flushStream)
            {
                await _stream.FlushAsync();
            }
        }

        private void CopyToCharBuffer(string value, ref int index, ref int count)
        {
            var remaining = Math.Min(_charBufferSize - _charBufferCount, count);

            value.CopyTo(
                sourceIndex: index,
                destination: _charBuffer.Array,
                destinationIndex: _charBuffer.Offset + _charBufferCount,
                count: remaining);

            _charBufferCount += remaining;
            index += remaining;
            count -= remaining;
        }

        private void CopyToCharBuffer(char[] values, ref int index, ref int count)
        {
            var remaining = Math.Min(_charBufferSize - _charBufferCount, count);

            Buffer.BlockCopy(
                src: values,
                srcOffset: index * sizeof(char),
                dst: _charBuffer.Array,
                dstOffset: (_charBuffer.Offset + _charBufferCount) * sizeof(char),
                count: remaining * sizeof(char));

            _charBufferCount += remaining;
            index += remaining;
            count -= remaining;
        }
    }
}