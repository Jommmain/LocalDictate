using System.IO;
using NAudio.Wave;

namespace LocalDictate.Services;

public sealed class AudioRecorder : IDisposable
{
    private readonly AppLogger _logger;
    private WaveInEvent? _waveIn;
    private MemoryStream? _buffer;
    private WaveFileWriter? _writer;
    private readonly object _gate = new();

    public bool IsRecording { get; private set; }

    public AudioRecorder(AppLogger logger)
    {
        _logger = logger;
    }

    public void Start()
    {
        lock (_gate)
        {
            if (IsRecording) return;

            _buffer = new MemoryStream();
            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(16000, 16, 1),
                BufferMilliseconds = 50,
            };
            _writer = new WaveFileWriter(_buffer, _waveIn.WaveFormat);
            _waveIn.DataAvailable += (_, args) =>
            {
                _writer?.Write(args.Buffer, 0, args.BytesRecorded);
            };
            _waveIn.StartRecording();
            IsRecording = true;
            _logger.Info("recording started");
        }
    }

    public byte[] Stop()
    {
        lock (_gate)
        {
            if (!IsRecording || _waveIn is null || _writer is null || _buffer is null)
            {
                return Array.Empty<byte>();
            }

            _waveIn.StopRecording();
            _writer.Flush();
            var wav = _buffer.ToArray();

            _waveIn.Dispose();
            _writer.Dispose();
            _buffer.Dispose();
            _waveIn = null;
            _writer = null;
            _buffer = null;
            IsRecording = false;

            _logger.Info($"recording stopped ({wav.Length} bytes wav)");
            return wav;
        }
    }

    public void Dispose()
    {
        if (IsRecording)
        {
            Stop();
        }
    }
}
