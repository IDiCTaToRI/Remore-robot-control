#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN

using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Threading;
using System.Linq;
using UnityEngine;

namespace FfmpegUnity
{
    public class FfmpegBytesInputsWinLib : FfmpegBytesInputs
    {
        [DllImport("avformat-59")]
        static extern IntPtr unitybuf_get_handle_dll(string uri);
        [DllImport("avformat-59")]
        static extern int unitybuf_write_dll(IntPtr handle, byte[] buf, int size);
        [DllImport("avformat-59")]
        static extern int unitybuf_count_dll(IntPtr handle);
        [DllImport("avformat-59")]
        static extern int unitybuf_clear_dll(IntPtr handle);

        public FfmpegBytesInputsWinLib(string[] inputOptions, FfmpegCommand command) : base(inputOptions, command)
        {
        }

        protected override string GenerateFileName()
        {
            return "unitybuf:0/0/" + Guid.NewGuid().ToString();
        }

        protected override void Write(string pipeFileName, int streamId)
        {
            IntPtr handle;
            do
            {
                handle = unitybuf_get_handle_dll(pipeFileName);
                if (handle == IntPtr.Zero)
                {
                    Thread.Sleep(1);
                }
            } while (handle == IntPtr.Zero && !IsStop);

            while (!IsStop)
            {
                if (InputBytes[streamId].Count <= 0 /*|| unitybuf_count_dll(handle) > 1*/)
                {
                    Thread.Sleep(1);
                    continue;
                }

                byte[] buffer;
                //lock (inputBytes_[streamId])
                {
                    buffer = InputBytes[streamId][0];

                    if (buffer != null && buffer.Length > 0)
                    {
                        InputBytes[streamId].RemoveAt(0);
                    }
                }

                if (buffer != null && buffer.Length > 0)
                {
                    unitybuf_write_dll(handle, buffer, buffer.Length);
                }
            }

            unitybuf_clear_dll(handle);
        }
    }
}

#endif