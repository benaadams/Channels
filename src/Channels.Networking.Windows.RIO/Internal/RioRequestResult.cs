// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;

namespace Channels.Networking.Windows.RIO.Internal
{
    [StructLayout(LayoutKind.Sequential)]
    public struct RioRequestResult
    {
        public int Status;
        public uint BytesTransferred;
        public long ConnectionCorrelation;
        public long RequestCorrelation;
    }
}
