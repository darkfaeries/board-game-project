using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace GameClient;

public class DevAuthWorker : BackgroundService
{
    private readonly GameClient _gameClient;
    private readonly bool _autoLoginDev;

    public DevAuthWorker(GameClient gameClient, IConfiguration configuration)
    {
        _gameClient = gameClient;
        _autoLoginDev = configuration.GetValue<bool>("AutoLoginDev");
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (_autoLoginDev)
            await _gameClient.AuthenticateForDevelopmentAsync();

        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(1000, stoppingToken);
        }
    }
}