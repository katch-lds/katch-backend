// Copyright (c) Grupo G11. Katch, LDS 2026/2027.

using Katch.Backend.BackgroundJobs;
using Katch.Backend.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Katch.Backend.UnitTests.BackgroundJobs;

/// <summary>
/// Testes de <see cref="QuotaRestoreWorker"/>: uma execução por intervalo, num âmbito próprio, que nunca pára a
/// aplicação (RF021; modelo de classes do backend v02, secção 5.2).
/// </summary>
public sealed class QuotaRestoreWorkerTests
{
    private static readonly TimeSpan Timeout = TimeSpan.FromSeconds(30);

    /// <summary>
    /// Cada execução abre um âmbito novo e chama a reposição uma vez.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task EachRunUsesANewScopeAndCallsTheServiceOnce()
    {
        var probe = new Probe();
        await using ServiceProvider provider = CreateProvider(probe);
        var worker = new QuotaRestoreWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new BackgroundJobsOptions()),
            new ListLogger());

        await worker.RestoreAsync(TestContext.Current.CancellationToken);
        await worker.RestoreAsync(TestContext.Current.CancellationToken);

        Assert.Equal(2, probe.Calls);
        Assert.Equal(2, probe.ScopesCreated);
        Assert.Equal(2, probe.ScopesDisposed);
    }

    /// <summary>
    /// Regista o número de quotas repostas, só quando houve alguma, e nada mais.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task LogsTheNumberOfQuotasRestoredOnlyWhenThereWasAny()
    {
        var probe = new Probe();
        var logger = new ListLogger();
        await using ServiceProvider provider = CreateProvider(probe);
        var worker = new QuotaRestoreWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new BackgroundJobsOptions()),
            logger);

        probe.Restored = 0;
        await worker.RestoreAsync(TestContext.Current.CancellationToken);
        Assert.Empty(logger.Entries);

        probe.Restored = 3;
        await worker.RestoreAsync(TestContext.Current.CancellationToken);

        (LogLevel level, string message, Exception? exception) = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Information, level);
        Assert.Equal("Quotas de interesses repostas: 3.", message);
        Assert.Null(exception);
    }

    /// <summary>
    /// Uma falha é registada com a exceção e engolida, para que a tarefa continue.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task FailureIsLoggedAndSwallowed()
    {
        var probe = new Probe { FailWith = new InvalidOperationException("base de dados indisponível") };
        var logger = new ListLogger();
        await using ServiceProvider provider = CreateProvider(probe);
        var worker = new QuotaRestoreWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new BackgroundJobsOptions()),
            logger);

        await worker.RestoreAsync(TestContext.Current.CancellationToken);

        (LogLevel level, _, Exception? exception) = Assert.Single(logger.Entries);
        Assert.Equal(LogLevel.Error, level);
        Assert.Same(probe.FailWith, exception);
        Assert.Equal(1, probe.ScopesDisposed);
    }

    /// <summary>
    /// Um cancelamento que não vem da paragem da aplicação (por exemplo um limite de tempo na base de dados) é uma falha
    /// como as outras.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task CancellationNotCausedByShutdownIsLoggedAsAFailure()
    {
        var probe = new Probe { FailWith = new OperationCanceledException() };
        var logger = new ListLogger();
        await using ServiceProvider provider = CreateProvider(probe);
        var worker = new QuotaRestoreWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new BackgroundJobsOptions()),
            logger);

        await worker.RestoreAsync(TestContext.Current.CancellationToken);

        Assert.Equal(LogLevel.Error, Assert.Single(logger.Entries).Level);
    }

    /// <summary>
    /// A paragem da aplicação cancela a execução em curso sem a registar como falha.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task ShutdownCancelsTheRunWithoutLoggingAFailure()
    {
        var probe = new Probe { FailWith = new OperationCanceledException() };
        var logger = new ListLogger();
        await using ServiceProvider provider = CreateProvider(probe);
        var worker = new QuotaRestoreWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new BackgroundJobsOptions()),
            logger);
        using var shutdown = new CancellationTokenSource();
        await shutdown.CancelAsync();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => worker.RestoreAsync(shutdown.Token));

        Assert.Empty(logger.Entries);
    }

    /// <summary>
    /// A tarefa corre a cada intervalo e uma falha numa execução não impede as seguintes.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task RunsEveryIntervalAndKeepsRunningAfterAFailure()
    {
        var probe = new Probe { FailOnFirstCall = true };
        var logger = new ListLogger();
        await using ServiceProvider provider = CreateProvider(probe);
        var worker = new QuotaRestoreWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new BackgroundJobsOptions { IntervalSeconds = 1 }),
            logger);

        await worker.StartAsync(TestContext.Current.CancellationToken);
        bool ranTwice = await probe.WaitForCallsAsync(2, Timeout, TestContext.Current.CancellationToken);
        await worker.StopAsync(TestContext.Current.CancellationToken);

        Assert.True(ranTwice);
        Assert.Contains(logger.Entries, entry => entry.Level == LogLevel.Error);
        Assert.Equal(probe.ScopesCreated, probe.ScopesDisposed);
    }

    /// <summary>
    /// A tarefa não corre ao arrancar, só ao fim do primeiro intervalo, e pára de imediato quando a aplicação pára.
    /// </summary>
    /// <returns>Tarefa do teste.</returns>
    [Fact]
    public async Task DoesNotRunAtStartupAndStopsImmediately()
    {
        var probe = new Probe();
        await using ServiceProvider provider = CreateProvider(probe);
        var worker = new QuotaRestoreWorker(
            provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new BackgroundJobsOptions { IntervalSeconds = 3600 }),
            new ListLogger());

        await worker.StartAsync(TestContext.Current.CancellationToken);
        await worker.StopAsync(TestContext.Current.CancellationToken);

        Assert.Equal(0, probe.Calls);
    }

    private static ServiceProvider CreateProvider(Probe probe)
    {
        var services = new ServiceCollection();
        services.AddSingleton(probe);
        services.AddScoped<IQuotaService>(provider => new ProbeQuotaService(provider.GetRequiredService<Probe>()));
        return services.BuildServiceProvider();
    }

    private sealed class Probe
    {
        private readonly object gate = new();
        private readonly List<(int Expected, TaskCompletionSource Signal)> waiters = [];
        private int calls;
        private int scopesCreated;
        private int scopesDisposed;

        public int Calls => Volatile.Read(ref this.calls);

        public int ScopesCreated => Volatile.Read(ref this.scopesCreated);

        public int ScopesDisposed => Volatile.Read(ref this.scopesDisposed);

        public int Restored { get; set; }

        public Exception? FailWith { get; set; }

        public bool FailOnFirstCall { get; set; }

        public void ScopeCreated()
        {
            Interlocked.Increment(ref this.scopesCreated);
        }

        public void ScopeDisposed()
        {
            Interlocked.Increment(ref this.scopesDisposed);
        }

        public int Call()
        {
            int number = Interlocked.Increment(ref this.calls);

            lock (this.gate)
            {
                foreach ((int expected, TaskCompletionSource signal) in this.waiters)
                {
                    if (number >= expected)
                    {
                        signal.TrySetResult();
                    }
                }
            }

            if (this.FailOnFirstCall && number == 1)
            {
                throw new InvalidOperationException("falha na primeira execução");
            }

            if (this.FailWith is not null)
            {
                throw this.FailWith;
            }

            return this.Restored;
        }

        public async Task<bool> WaitForCallsAsync(int expected, TimeSpan timeout, CancellationToken cancellationToken)
        {
            var signal = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (this.gate)
            {
                this.waiters.Add((expected, signal));
                if (this.Calls >= expected)
                {
                    signal.TrySetResult();
                }
            }

            Task finished = await Task.WhenAny(signal.Task, Task.Delay(timeout, cancellationToken));
            return ReferenceEquals(finished, signal.Task);
        }
    }

    private sealed class ProbeQuotaService : IQuotaService, IDisposable
    {
        private readonly Probe probe;

        public ProbeQuotaService(Probe probe)
        {
            this.probe = probe;
            probe.ScopeCreated();
        }

        public Task<int> RestoreDueQuotasAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(this.probe.Call());
        }

        public void Dispose()
        {
            this.probe.ScopeDisposed();
        }
    }

    private sealed class ListLogger : ILogger<QuotaRestoreWorker>
    {
        public List<(LogLevel Level, string Message, Exception? Exception)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            lock (this.Entries)
            {
                this.Entries.Add((logLevel, formatter(state, exception), exception));
            }
        }
    }
}
