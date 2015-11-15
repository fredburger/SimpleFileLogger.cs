using System;
using System.IO;
using System.Text;
using System.Threading;


/// <summary>
/// 
/// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED.
/// IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY.
/// 
/// NOTE: LockFile() retry loop to avoid DllImport (see LockFileEx() Win32 API).
/// 
/// </summary>
public class SimpleFileLogger : IDisposable
{
    private const uint ErrorLockViolation = 0x80070021;

    private readonly object _threadLock = new object();

    private readonly Random _prng = new Random();

    private readonly FileStream _fileStream;


    public SimpleFileLogger(string path)
    {
        _fileStream = new FileStream(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
    }


    public bool TryWriteLine(string fmt, params object[] args)
    {
        var str = string.Format(fmt, args);
        return TryWriteLine(str);
    }

    public bool TryWriteLine(string str)
    {
        try
        {
            var data = Encoding.UTF8.GetBytes(str + "\r\n");

            lock (_threadLock)
            {
                if (!TryLockFile())
                    return false;

                try
                {
                    _fileStream.Seek(0, SeekOrigin.End);
                    _fileStream.Write(data, 0, data.Length);
                    _fileStream.Flush();

                    return true;
                }
                finally
                {
                    UnlockFile();
                }
            }
        }
        catch
        {
            return false;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!disposing)
            return;

        _fileStream.Dispose();
    }

    private bool TryLockFile()
    {
        for (int i = 0; i < 24; i++)
        {
            try
            {
                _fileStream.Lock(0, Int64.MaxValue);
                return true;
            }
            catch (IOException ie)
            {
                if ((uint)ie.HResult == ErrorLockViolation)
                {
                    Thread.Sleep((i < 3) ? i : _prng.Next(64));
                    continue;
                }
                
                return false;
            }
        }

        return false;
    }

    private void UnlockFile()
    {
        _fileStream.Unlock(0, Int64.MaxValue);
    }
}
