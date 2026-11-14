namespace GameServer;

public interface IHeightSource
{
    float Sample(float u, float v);
}
