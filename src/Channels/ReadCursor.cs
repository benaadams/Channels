﻿using System;
using System.Buffers;
using System.Runtime.CompilerServices;
using System.Text;

namespace Channels
{
    public struct ReadCursor : IEquatable<ReadCursor>
    {
        private BufferSegment _segment;
        private int _index;

        internal ReadCursor(BufferSegment segment)
        {
            _segment = segment;
            _index = segment?.Start ?? 0;
        }

        internal ReadCursor(BufferSegment segment, int index)
        {
            _segment = segment;
            _index = index;
        }

        internal BufferSegment Segment => _segment;

        internal int Index => _index;

        internal bool IsDefault => _segment == null;

        internal bool IsEnd
        {
            [MethodImpl(MethodImplOptions.AggressiveInlining)]
            get
            {
                bool isEnd;
                var segment = _segment;

                if (segment == null)
                {
                    isEnd = true;
                }
                else if (_index < segment.End)
                {
                    isEnd = false;
                }
                else if (segment.Next == null)
                {
                    isEnd = true;
                }
                else
                {
                    isEnd = IsEndMultiSegment();
                }

                return isEnd;
            }
        }

        private bool IsEndMultiSegment()
        {
            var segment = _segment.Next;
            while (segment != null)
            {
                if (segment.Start < segment.End)
                {
                    return false; // subsequent block has data - IsEnd is false
                }
                segment = segment.Next;
            }
            return true;
        }

        internal int GetLength(ReadCursor end)
        {
            if (IsDefault)
            {
                return 0;
            }

            var segment = _segment;
            var index = _index;
            var length = 0;
            checked
            {
                do
                {
                    if (segment == end._segment)
                    {
                        length += end._index - index;
                        break;
                    }
                    else if (segment.Next == null)
                    {
                        break;
                    }
                    else
                    {
                        length += segment.End - index;
                        segment = segment.Next;
                        index = segment.Start;
                    }
                }
                while (true);

                return length;
            }
        }

        internal ReadCursor Seek(int bytes)
        {
            int count;
            return Seek(bytes, out count);
        }

        internal ReadCursor Seek(int bytes, out int bytesSeeked)
        {
            if (IsEnd)
            {
                bytesSeeked = 0;
                return this;
            }

            var wasLastSegment = _segment.Next == null;
            var following = _segment.End - _index;

            int count;
            var segment = _segment;
            var index = _index;

            do
            {
                if (following >= bytes)
                {
                    bytesSeeked = bytes;
                    count = index + bytes;
                    break;
                }
                else if (wasLastSegment)
                {
                    bytesSeeked = following;
                    count = index + following;
                    break;
                }
                else
                {
                    bytes -= following;
                    segment = segment.Next;
                    index = segment.Start;
                }

                wasLastSegment = segment.Next == null;
                following = segment.End - index;

            } while (true);

            return new ReadCursor(segment, count);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        internal bool TryGetBuffer(ReadCursor end, out Memory<byte> span, out ReadCursor cursor)
        {
            if (IsDefault)
            {
                span = default(Memory<byte>);
                cursor = this;
                return false;
            }

            var segment = _segment;
            var index = _index;

            if (end.Segment == segment)
            {
                var following = end.Index - index;

                if (following > 0)
                {
                    span = segment.Buffer.Data.Slice(index, following);
                    cursor = new ReadCursor(segment, index + following);
                    return true;
                }

                span = default(Memory<byte>);
                cursor = this;
                return false;
            }
            else
            {
                return TryGetBufferMultiBlock(end, out span, out cursor);
            }
        }

        private bool TryGetBufferMultiBlock(ReadCursor end, out Memory<byte> span, out ReadCursor cursor)
        {
            var segment = _segment;
            var index = _index;

            // Determine if we might attempt to copy data from segment.Next before
            // calculating "following" so we don't risk skipping data that could
            // be added after segment.End when we decide to copy from segment.Next.
            // segment.End will always be advanced before segment.Next is set.

            int following = 0;

            while (true)
            {
                var wasLastSegment = segment.Next == null || end.Segment == segment;

                if (end.Segment == segment)
                {
                    following = end.Index - index;
                }
                else
                {
                    following = segment.End - index;
                }

                if (following > 0)
                {
                    break;
                }

                if (wasLastSegment)
                {
                    span = default(Memory<byte>);
                    cursor = this;
                    return false;
                }
                else
                {
                    segment = segment.Next;
                    index = segment.Start;
                }
            }

            span = segment.Buffer.Data.Slice(index, following);
            cursor = new ReadCursor(segment, index + following);
            return true;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            Span<byte> span = Segment.Buffer.Data.Slice(Index, Segment.End - Index);
            for (int i = 0; i < span.Length; i++)
            {
                sb.Append((char)span[i]);
            }
            return sb.ToString();
        }

        public static bool operator ==(ReadCursor c1, ReadCursor c2)
        {
            return c1.Equals(c2);
        }

        public static bool operator !=(ReadCursor c1, ReadCursor c2)
        {
            return !c1.Equals(c2);
        }

        public bool Equals(ReadCursor other)
        {
            return other._segment == _segment && other._index == _index;
        }

        public override bool Equals(object obj)
        {
            return Equals((ReadCursor)obj);
        }

        public override int GetHashCode()
        {
            var h1 = _segment?.GetHashCode() ?? 0;
            var h2 = _index.GetHashCode();

            var shift5 = ((uint)h1 << 5) | ((uint)h1 >> 27);
            return ((int)shift5 + h1) ^ h2;
        }
    }
}
