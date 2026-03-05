namespace App.Headless.Services;

internal interface IUiRealtimePublisher
{
    void Publish(object message);
}
