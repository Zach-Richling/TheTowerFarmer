using OpenCvSharp;
using Serilog;
using Serilog.Core;

namespace TheTowerFarmer;

internal class GameAutomation
{
    private readonly string _deviceSerial;
    private readonly Android _client;
    private readonly GameStateDetector _detector;
    private readonly UpgradeManager _upgradeManager;

    private readonly BroadcastChannel<Mat> _frameProducer = new();
    private readonly Vision _vision = new();

    private GameStates _currentState = GameStates.Unknown;
    private CancellationTokenSource _stateSource = new();
    private Task? _stateTask;

    private ILogger _logger;

    public GameAutomation(string deviceSerial)
    {
        _deviceSerial = deviceSerial;

        _logger = new LoggerConfiguration()
        .Enrich.WithProperty("DeviceSerial", deviceSerial)
        .WriteTo.Console(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] [{DeviceSerial}] {SourceContext} {Message:lj}{NewLine}{Exception}")
        .CreateLogger();

        _client = new Android(deviceSerial);
        _detector = new GameStateDetector(_vision);
        _upgradeManager = new UpgradeManager(_client, _vision, _frameProducer, _logger);

    }

    public async Task RunAsync(CancellationToken token)
    {
        _logger.Information("Starting automation controller");

        while (true)
        {
            try
            {
                var frame = await _client.CaptureScreenAsync(token);
                var detectedState = _detector.DetectGameState(frame);

                if (detectedState != _currentState)
                {
                    _logger.Information("State changed: {CurrentState} -> {DetectedState}", _currentState, detectedState);

                    _stateSource.Cancel();
                    if (_stateTask != null)
                    {
                        try { await _stateTask; }
                        catch (OperationCanceledException) { }
                    }

                    _currentState = detectedState;
                    _stateSource = new CancellationTokenSource();
                    _stateTask = HandleStateAsync(_currentState, _stateSource.Token);
                }

                _frameProducer.Publish(frame);
            }
            catch (Exception ex)
            {
                _logger.Error("Error in GameAutomation", ex);
            }

            await Task.Delay(200, token);
        }
    }

    private async Task HandleStateAsync(GameStates state, CancellationToken token)
    {
        switch (state)
        {
            case GameStates.MainMenu:
                await HandleMainMenu(token);
                break;

            case GameStates.InBattle:
                await HandleInBattle(token);
                break;

            case GameStates.Defeat:
                await HandleDefeat(token);
                break;
        }
    }

    private async Task HandleMainMenu(CancellationToken token)
    {
        while(!token.IsCancellationRequested)
        {
            var frame = await _frameProducer.WaitAsync(token);
            await FindAndClick(frame, Templates.MainMenu.ClaimGems, token);
            await FindAndClick(frame, Templates.MainMenu.BattleStart, token);
        }
    }

    private async Task HandleDefeat(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var frame = await _frameProducer.WaitAsync(token);
            await FindAndClick(frame, Templates.Defeat.Retry, token);
        }
    }

    private async Task HandleInBattle(CancellationToken token)
    {
        var subRoutines = new List<Task>
        {
            GatherGems(token),
            GatherMovingGems(token)
            //_upgradeManager.RunAsync(token)
        };

        await Task.WhenAll(subRoutines);
    }

    private async Task GatherGems(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var frame = await _frameProducer.WaitAsync(token);
                await FindAndClick(frame, Templates.Battle.ClaimGems, token, "Claimed ad gem");
            }
        }
        catch (TaskCanceledException) { }
        catch (Exception ex) 
        {
            _logger.Error("Error in GatherGems", ex);
        }
    }

    private async Task GatherMovingGems(CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var frame = await _frameProducer.WaitAsync(token);

                var tower = _vision.FindTemplate(frame, Templates.Battle.Tower);
                if (tower != null)
                {
                    var point = _vision.DetectGemByColor(frame, (Point)tower, 270);
                    if (point != null)
                    {
                        _logger.Information("Claimed moving gem");
                        await _client.TapAsync(point.Value.X, point.Value.Y, token);
                    }
                }
            }
        }
        catch (TaskCanceledException) { }
        catch (Exception ex) { _logger.Error("Error in GatherMovingGems", ex); }
    }

    private async Task<bool> FindAndClick(Mat frame, string template, CancellationToken token, string? successMessage = null, string? failureMessage = null, double threshhold = 0.8)
    {
        var found = _vision.FindTemplate(frame, template, threshhold);
        if (found != null)
        {
            var point = found.Value;
            await _client.TapAsync(point.X, point.Y, token);

            if (!string.IsNullOrEmpty(successMessage))
                _logger.Information(successMessage);

            return true;
        }

        if (!string.IsNullOrEmpty(failureMessage))
            _logger.Information(failureMessage);

        return false;
    }
}
