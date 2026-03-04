using System;
using System.Threading;
using System.Threading.Tasks;

namespace CRD.Utils;

public class PeriodicWorkRunner(Func<CancellationToken, Task> work) : IDisposable{
    private CancellationTokenSource? cts;
    private Task? loopTask;

    private TimeSpan currentInterval;
    
    public DateTime LastRunTime = DateTime.MinValue;

    public void StartOrRestart(TimeSpan interval, bool runImmediately = false, bool force = false){
        if (interval <= TimeSpan.Zero){
            Stop();
            currentInterval = Timeout.InfiniteTimeSpan;
            return;
        }
        
        if (!force && interval == currentInterval){
            return;
        }

        currentInterval = interval;

        Stop();

        cts = new CancellationTokenSource();
        loopTask = RunLoopAsync(interval, runImmediately, cts.Token);
    }

    public void StartOrRestartMinutes(int minutes, bool runImmediately = false, bool force = false)
        => StartOrRestart(TimeSpan.FromMinutes(minutes), runImmediately);

    public void Stop(){
        if (cts is null) return;

        try{
            cts.Cancel();
        } finally{
            cts.Dispose();
            cts = null;
        }
    }

    private async Task RunLoopAsync(TimeSpan interval, bool runImmediately, CancellationToken token){
        if (runImmediately){
            await SafeRunWork(token).ConfigureAwait(false);
        }

        using var timer = new PeriodicTimer(interval);

        try{
            while (await timer.WaitForNextTickAsync(token).ConfigureAwait(false)){
                await SafeRunWork(token).ConfigureAwait(false);
            }
        } catch (OperationCanceledException){
        }
    }

    private int running = 0;

    private async Task SafeRunWork(CancellationToken token){
        if (Interlocked.Exchange(ref running, 1) == 1){
            Console.Error.WriteLine("Task is already running!");
            return;
        }

        try{
            await work(token).ConfigureAwait(false);
            LastRunTime =  DateTime.Now;
        } catch (OperationCanceledException) when (token.IsCancellationRequested){
        } catch (Exception ex){
            Console.Error.WriteLine(ex);
        } finally{
            Interlocked.Exchange(ref running, 0);
        }
    }
    
    public void Dispose() => Stop();
}