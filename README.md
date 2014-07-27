MediaBrowser.ApiClient
======================

This portable class library makes it very easy to harness the power of the Media Browser API.

This is available as a Nuget package:

[MediaBrowser.ApiClient](https://www.nuget.org/packages/MediaBrowser.ApiClient/)

Usage is very simple:

``` c#

            var client = new ApiClient("http://localhost:8096", "My client name", "My device", "My device id");

			var authResult = await AuthenticateUserAsync("username", passwordHash);

            // Get the ten most recently added items for the current user
            var items = await client.GetItemsAsync(new ItemQuery
            {
                UserId = client.UserId,

                SortBy = new[] { ItemSortBy.DateCreated },
                SortOrder = SortOrder.Descending,

                // Get media only, don't return folder items
                Filters = new[] { ItemFilter.IsNotFolder },

                Limit = 10,

                // Search recursively through the user's library
                Recursive = true
            });

			await client.Logout();
```

# Service Apps #

If your app is some kind of service or utility (e.g. Sickbeard), you should construct ApiClient with your api key.

``` c#

            var client = new ApiClient("http://localhost:8096", "0123456789");

			// Get the ten most recently added items for the current user
            var items = await client.GetItemsAsync(new ItemQuery
            {
                SortBy = new[] { ItemSortBy.DateCreated },
                SortOrder = SortOrder.Descending,

                // Get media only, don't return folder items
                Filters = new[] { ItemFilter.IsNotFolder },

                Limit = 10,

                // Search recursively through the user's library
                Recursive = true
            });
```

# Web Socket #

In addition to http requests, you can also connect to the server's web socket to receive notifications of events from the server.

``` c#

            var webSocket = ApiWebSocket.Create(apiClient, ClientWebSocketFactory.CreateWebSocket, CancellationToken.None);
```

Once instantiated, simply call EnsureConnectionAsync. Even once connected, this method can be called at anytime to verify connection status and reconnect if needed.

``` c#

            await webSocket.EnsureConnectionAsync(CancellationToken.None);
```

There is a Closed event that will fire anytime the connection is lost. From there you can attempt to reconnect. ApiWebSocket also supports the use of a timer to periodically call EnsureConnectionAsync:

``` c#

            webSocket.StartEnsureConnectionTimer(int intervalMs);
            
            webSocket.StopEnsureConnectionTimer();
```

ApiWebSocket has various events that can be used to receive notifications from the server:


``` c#

            webSocket.UserUpdated += webSocket_UserUpdated;
```

# Logging and Interfaces #

ApiClient and ApiWebSocket both have additional constructors available allowing you to pass in your own implementation of ILogger. The default implementation is NullLogger, which provides no logging. In addition you can also pass in your own implementation of IJsonSerializer, or use our NewtonsoftJsonSerializer. ClientWebSocketFactory also has an additional overload allowing you to pass in your own ILogger
