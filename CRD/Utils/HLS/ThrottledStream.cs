using CRD.Downloader;

namespace CRD.Utils.HLS;

using System;
using System.IO;
using System.Threading;

public class GlobalThrottler{
    private static GlobalThrottler _instance;
    private static readonly object _lock = new object();
    private long _totalBytesRead;
    private DateTime _lastReadTime;

    private GlobalThrottler(){
        _totalBytesRead = 0;
        _lastReadTime = DateTime.Now;
    }

    public static GlobalThrottler Instance(){
        if (_instance == null){
            lock (_lock){
                if (_instance == null){
                    _instance = new GlobalThrottler();
                }
            }
        }

        return _instance;
    }

    public void Throttle(int bytesRead){
        
        if (Crunchyroll.Instance.CrunOptions.DownloadSpeedLimit == 0) return;
        
        lock (_lock){
            _totalBytesRead += bytesRead;
            if (_totalBytesRead >= ((Crunchyroll.Instance.CrunOptions.DownloadSpeedLimit * 1024) / 10)){
                var timeElapsed = DateTime.Now - _lastReadTime;
                if (timeElapsed.TotalMilliseconds < 100){
                    Thread.Sleep(100 - (int)timeElapsed.TotalMilliseconds);
                }

                _totalBytesRead = 0;
                _lastReadTime = DateTime.Now;
            }
        }
    }
}

public class ThrottledStream : Stream{
    private readonly Stream _baseStream;
    private readonly GlobalThrottler _throttler;

    public ThrottledStream(Stream baseStream){
        _baseStream = baseStream ?? throw new ArgumentNullException(nameof(baseStream));
        _throttler = GlobalThrottler.Instance();
    }

    public override bool CanRead => _baseStream.CanRead;
    public override bool CanSeek => _baseStream.CanSeek;
    public override bool CanWrite => _baseStream.CanWrite;
    public override long Length => _baseStream.Length;

    public override long Position{
        get => _baseStream.Position;
        set => _baseStream.Position = value;
    }

    public override void Flush() => _baseStream.Flush();

    public override long Seek(long offset, SeekOrigin origin) => _baseStream.Seek(offset, origin);

    public override void SetLength(long value) => _baseStream.SetLength(value);

    public override void Write(byte[] buffer, int offset, int count) => _baseStream.Write(buffer, offset, count);

    public override int Read(byte[] buffer, int offset, int count){
        int bytesRead = 0;
        if (Crunchyroll.Instance.CrunOptions.DownloadSpeedLimit != 0){
            int bytesToRead = Math.Min(count, (Crunchyroll.Instance.CrunOptions.DownloadSpeedLimit * 1024) / 10);
            bytesRead = _baseStream.Read(buffer, offset, bytesToRead);
            _throttler.Throttle(bytesRead);
        } else{
            bytesRead = _baseStream.Read(buffer, offset, count);
        }
        return bytesRead;
    }

    protected override void Dispose(bool disposing){
        if (disposing){
            _baseStream.Dispose();
        }

        base.Dispose(disposing);
    }
}