using Microsoft.AspNetCore.SignalR.Client;

namespace TagzApp.Blazor.Client;

public interface IHasSignalRConnection
{

	HubConnection Connection { get; }

}
