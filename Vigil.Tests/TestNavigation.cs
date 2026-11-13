using Microsoft.Extensions.DependencyInjection;
using Uno.Extensions;
using Uno.Extensions.Navigation;

namespace Vigil.Tests;

/// <summary>
/// Records the navigation a model asks for, without a window. Route and back requests succeed. A request that waits
/// for a result (a sheet, a drawer, the menu) gets the next queued answer, or none, as when the user closes it. A
/// message dialog (a confirmation) presses its first button when the queued answer is <c>true</c>.
/// </summary>
internal sealed class FakeNavigator : INavigator, IInstance<IServiceProvider>
{
    private readonly Queue<object?> _answers = new();
    private readonly List<NavigationRequest> _requests = [];

    public Route? Route => null;

    /// <summary>The navigation extensions read the route resolver from here; a named route never needs it.</summary>
    public IServiceProvider? Instance { get; } = new ServiceCollection().AddSingleton<IRouteResolver, NoRoutes>().BuildServiceProvider();

    /// <summary>Qualifier and route of each request in order: "!Monitor", "-", "./-/Today".</summary>
    public IReadOnlyList<string> Paths
    {
        get
        {
            lock (_requests)
            {
                return _requests.Select(r => r.Route.Qualifier + r.Route.Base).ToList();
            }
        }
    }

    /// <summary>The data each request carried (a CaseRef, a MenuChoice), or null.</summary>
    public IReadOnlyList<object?> Data
    {
        get
        {
            lock (_requests)
            {
                return _requests.Select(r => r.Route.NavigationData()).ToList();
            }
        }
    }

    /// <summary>Queues the answer the next sheet, drawer or confirmation gives back.</summary>
    public FakeNavigator Answer(object? answer)
    {
        _answers.Enqueue(answer);
        return this;
    }

    public Task<bool> CanNavigate(Route route) => Task.FromResult(true);

    public Task<NavigationResponse?> NavigateAsync(NavigationRequest request)
    {
        lock (_requests)
        {
            _requests.Add(request);
        }
        if (request.Result is null)
        {
            return Task.FromResult<NavigationResponse?>(new NavigationResponse(request.Route, true, this));
        }
        var answer = _answers.Count > 0 ? _answers.Dequeue() : null;
        if (answer is true && request.Route.Data?.TryGetValue("xxxxmessagedialogxxxxcommands", out var buttons) == true && buttons is DialogAction[] { Length: > 0 } actions)
        {
            actions[0].Action?.Invoke();
        }
        IOption option = answer is null ? Option.None<object>() : Option.Some(answer);
        return Task.FromResult<NavigationResponse?>(new NavigationResultResponse(request.Route, Task.FromResult(option), true, this));
    }

    private sealed class NoRoutes : IRouteResolver
    {
        public RouteInfo? FindByPath(string? path) => null;
        public RouteInfo? FindByViewModel(Type? viewModelType, INavigator? navigator) => null;
        public RouteInfo? FindByView(Type? viewType, INavigator? navigator) => null;
        public RouteInfo? FindByData(Type? dataType, INavigator? navigator) => null;
        public RouteInfo? FindByResultData(Type? resultDataType, INavigator? navigator) => null;
        public void InsertRoute(RouteInfo routeInfo) { }
    }
}

/// <summary>Raises the route changes the shell listens to.</summary>
internal sealed class FakeRouteNotifier : IRouteNotifier
{
    public event EventHandler<RouteChangedEventArgs>? RouteChanged;

    public void Show(string path)
    {
        var shown = new ShownRoute(new Route("", path));
        RouteChanged?.Invoke(this, new RouteChangedEventArgs(null!, shown, shown.Route));
    }

    private sealed class ShownRoute(Route route) : INavigator
    {
        public Route? Route => route;
        public Task<bool> CanNavigate(Route route) => Task.FromResult(false);
        public Task<NavigationResponse?> NavigateAsync(NavigationRequest request) => Task.FromResult<NavigationResponse?>(null);
    }
}
