namespace GameServer;

public class CroppedHeightSource : IHeightSource
{
    private readonly ILogger<CroppedHeightSource> _logger;
    private readonly IHeightSource _source;
    private readonly (float, float) _leftTopCornerCoords;
    private readonly (float, float) _rightBottomCornerCoords;

    public CroppedHeightSource(
        ILogger<CroppedHeightSource> logger,
        IHeightSource source, 
        (float, float) leftTopCornerCord,
        (float, float) rightBottomCornerCord)
    {
        _logger = logger;
        _source = source;
        _leftTopCornerCoords = leftTopCornerCord;
        _rightBottomCornerCoords = rightBottomCornerCord;
        if (_leftTopCornerCoords.Item1 > _rightBottomCornerCoords.Item1)
        {
            _logger.LogWarning("left TopCornerCoords must be smaller than rightBottomCornerCoords");
            throw new ArgumentOutOfRangeException(nameof(leftTopCornerCord),
                "left TopCornerCoords must be smaller than rightBottomCornerCoords");
        }

        if (_leftTopCornerCoords.Item2 > _rightBottomCornerCoords.Item2)
        {
            _logger.LogWarning("right TopCornerCoords must be smaller than rightBottomCornerCoords");
            throw new ArgumentOutOfRangeException(nameof(leftTopCornerCord), 
                "right TopCornerCoords must be smaller than rightBottomCornerCoords");
        }

        if (_leftTopCornerCoords.Item1 < 0 || _leftTopCornerCoords.Item1 > 1)
        {
            _logger.LogWarning("left TopCornerCoords must be between 0 and 1");
            throw new ArgumentOutOfRangeException(nameof(leftTopCornerCord), 
                "left TopCornerCoords must be between 0 and 1");
        }
        if (_leftTopCornerCoords.Item2 < 0 || _leftTopCornerCoords.Item2 > 1)
        {
            _logger.LogWarning("left TopCornerCoords must be between 0 and 1");
            throw new ArgumentOutOfRangeException(nameof(leftTopCornerCord), 
                "left TopCornerCoords must be between 0 and 1");
        }

        if (_rightBottomCornerCoords.Item1 < 0 || _rightBottomCornerCoords.Item1 > 1)
        {
            _logger.LogWarning("right BottomCornerCoords must be between 0 and 1");
            throw new ArgumentOutOfRangeException(nameof(rightBottomCornerCord),
                "right BottomCornerCoords must be between 0 and 1");
        }
        if (_rightBottomCornerCoords.Item2 < 0 || _rightBottomCornerCoords.Item2 > 1)
        {
            _logger.LogWarning("right BottomCornerCoords must be between 0 and 1");
            throw new ArgumentOutOfRangeException(nameof(rightBottomCornerCord),
                "right BottomCornerCoords must be between 0 and 1");
        }
    }
    
    public float Sample(float u, float v)
    {
        float width = _rightBottomCornerCoords.Item1 - _leftTopCornerCoords.Item1;
        float height = _rightBottomCornerCoords.Item2 - _leftTopCornerCoords.Item2;
        return _source.Sample(
            _leftTopCornerCoords.Item1 + u * width,
            _leftTopCornerCoords.Item2 + v * height);
    }
}
