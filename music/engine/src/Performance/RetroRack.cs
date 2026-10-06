using System;
using System.Buffers.Binary;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace Junimo.Engine.Performance
{
    /// <summary>
    /// The retro sources: MU2000s in S-MU2000, run by music/engine/retro's smu_stream --serve (a local build; the
    /// emulator and the ROMs never ship). One helper process holds all of them, each on its own thread. They boot as
    /// the rack opens; the performance's sample 0 is the first sample after the boot, as in smu_stream's other modes,
    /// so what the rack renders is what smu_stream renders from a stream file.
    /// </summary>
    public sealed class RetroRack : ISourceRack
    {
        private readonly Process helper;
        private readonly Stream input, output;
        private readonly byte[] message = new byte[1 + 1 + 8 + 2 + 256];
        private byte[] pcm = Array.Empty<byte>();

        public RetroRack(string helperPath, string romDir, int count)
        {
            var start = new ProcessStartInfo(helperPath)
            {
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                UseShellExecute = false,
            };
            start.ArgumentList.Add("--serve");
            start.ArgumentList.Add(romDir);
            start.ArgumentList.Add(count.ToString());
            helper = Process.Start(start) ?? throw new InvalidOperationException($"can't start {helperPath}");
            input = helper.StandardInput.BaseStream;
            output = helper.StandardOutput.BaseStream;
            string ready = ReadLine();
            if (!ready.StartsWith("ready "))
                throw new InvalidOperationException($"smu_stream --serve: {ready}");
            Boot = long.Parse(ready.Substring(6));
            Count = count;
        }

        public int Count { get; }
        public int Grid => 1;

        /// <summary>Samples the MU2000s took to power on.</summary>
        public long Boot { get; }

        public void Send(int unit, TimedMessage m)
        {
            if (m.Bytes.Length > message.Length - 12)
                throw new ArgumentException("message too long for one send");
            message[0] = (byte)'M';
            message[1] = (byte)unit;
            BinaryPrimitives.WriteInt64LittleEndian(message.AsSpan(2), m.Sample);
            BinaryPrimitives.WriteUInt16LittleEndian(message.AsSpan(10), (ushort)m.Bytes.Length);
            m.Bytes.CopyTo(message, 12);
            input.Write(message, 0, 12 + m.Bytes.Length);
        }

        public void Render(int frames, double[][] left, double[][] right)
        {
            Span<byte> cmd = stackalloc byte[5];
            cmd[0] = (byte)'R';
            BinaryPrimitives.WriteUInt32LittleEndian(cmd.Slice(1), (uint)frames);
            input.Write(cmd);
            input.Flush();
            int bytes = frames * 4;
            if (pcm.Length < bytes)
                pcm = new byte[bytes];
            for (int k = 0; k < Count; k++)
            {
                Fill(pcm, bytes);
                for (int f = 0; f < frames; f++)
                {
                    left[k][f] = BinaryPrimitives.ReadInt16LittleEndian(pcm.AsSpan(4 * f)) / 32768.0;
                    right[k][f] = BinaryPrimitives.ReadInt16LittleEndian(pcm.AsSpan(4 * f + 2)) / 32768.0;
                }
            }
        }

        public void Dispose()
        {
            try
            {
                input.WriteByte((byte)'Q');
                input.Flush();
                helper.WaitForExit(5000);
            }
            catch (IOException)
            {
            }
            if (!helper.HasExited)
                helper.Kill();
            helper.Dispose();
        }

        private void Fill(byte[] buffer, int count)
        {
            for (int got = 0; got < count;)
            {
                int n = output.Read(buffer, got, count - got);
                if (n <= 0)
                    throw new InvalidOperationException("smu_stream --serve stopped");
                got += n;
            }
        }

        private string ReadLine()
        {
            var line = new StringBuilder();
            for (int b; (b = output.ReadByte()) != '\n';)
            {
                if (b < 0)
                    throw new InvalidOperationException("smu_stream --serve exited before it was ready");
                line.Append((char)b);
            }
            return line.ToString();
        }
    }
}
