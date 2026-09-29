using ESD.Core;

namespace ESD.Service;

public sealed class EventManager : IEventManager
{
    public event EventHandler<EsdEvent>? EventReceived;
    public void Publish(EsdEvent evt) => EventReceived?.Invoke(this, evt);
}
