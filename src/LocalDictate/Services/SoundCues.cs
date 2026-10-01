using System.IO;
using System.Media;
using System.Text;

namespace LocalDictate.Services;

public static class SoundCues
{
    private const int SampleRate = 22050;
    private static SoundPlayer? _player;
    private static MemoryStream? _stream;

    public static void Play(bool enabled, bool starting)
    {
        if (!enabled)
        {
            return;
        }

        try
        {
            var wav = BuildTone(starting ? 740d : 520d, milliseconds: 64, amplitude: 1800);
            _stream = new MemoryStream(wav);
            _player = new SoundPlayer(_stream)
            {
                Stream = _stream,
            };
            _player.Play();
        }
        catch
        {
            // A missing audio device must not interrupt dictation.
        }
    }

    internal static byte[] BuildTone(double frequency, int milliseconds, short amplitude)
    {
        var count = SampleRate * milliseconds / 1000;
        var pcm = new byte[count * 2];
        var attackSamples = SampleRate * 0.008;
        var releaseSamples = SampleRate * 0.018;
        for (var i = 0; i < count; i++)
        {
            var attack = Math.Min(1d, i / attackSamples);
            var release = Math.Min(1d, (count - 1 - i) / releaseSamples);
            var envelope = Math.Min(attack, release);
            var sample = (short)(Math.Sin(2 * Math.PI * frequency * i / SampleRate) * amplitude * envelope);
            pcm[i * 2] = (byte)(sample & 0xFF);
            pcm[i * 2 + 1] = (byte)((sample >> 8) & 0xFF);
        }

        using var output = new MemoryStream();
        using (var writer = new BinaryWriter(output, Encoding.ASCII, leaveOpen: true))
        {
            writer.Write(Encoding.ASCII.GetBytes("RIFF"));
            writer.Write(36 + pcm.Length);
            writer.Write(Encoding.ASCII.GetBytes("WAVEfmt "));
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(SampleRate);
            writer.Write(SampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(Encoding.ASCII.GetBytes("data"));
            writer.Write(pcm.Length);
            writer.Write(pcm);
        }

        return output.ToArray();
    }
}
