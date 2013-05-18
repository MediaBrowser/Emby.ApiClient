MediaBrowser.ApiClient
======================

This portable class library makes it very easy to harness the power of the Media Browser API.

This is available as a Nuget package:

[MediaBrowser.ApiClient](https://www.nuget.org/packages/MediaBrowser.ApiClient/)

Usage is very simple:

``` c#

            var client = new ApiClient("localhost", 8096, "My client name", "My device", "My device id");

            // Get all users
            var users = await client.GetAllUsersAsync();

            var currentUser = users.First();

            // Get the ten most recently added items for the current user
            var items = await client.GetItemsAsync(new ItemQuery
            {
                UserId = currentUser.Id,

                SortBy = new[] { ItemSortBy.DateCreated },
                SortOrder = SortOrder.Descending,

                // Get media only, don't return folder items
                Filters = new[] { ItemFilter.IsNotFolder },

                Limit = 10,

                // Search recursively through the user's library
                Recursive = true
            });
```

After authentication you'll need to set the CurrentUserId property, and you'll need to update that value anytime the user changes (or logs out).


# Web Socket #

In addition to http requests, you can also connect to the server's web socket to receive notifications of events from the server.

The first thing you will need to do is get the SystemInfo resource to discover what port the web socket is running on.

``` c#

            var systemInfo = await client.GetSystemInfoAsync();

			var webSocketPort = systemInfo.WebSocketPortNumber;
```


Then you can simply instantiate ApiWebSocket and open a connection.

``` c#

            var webSocket = new ApiWebSocket(ClientWebSocketFactory.CreateWebSocket());

			await webSocket.ConnectAsync("localhost", webSocketPort, CancellationToken.None);
```

Please note that ClientWebSocketFactory is not available in the portable class library. In this case you'll need to supply your own implementation of IClientWebSocket.

Also note that if this is being used from a client application that requires the user to authenticate, you should use the ConnectAsync overload that allows you to specify the deviceId and clientName;

From here you can subscribe to the various events available on the web socket:


``` c#

            webSocket.UserUpdated += webSocket_UserUpdated;
```

Note that if this is being used from a client application, some of these events will actually be remote control commands. An example of a remote control event is PlayCommand, meaning the server is instructing the client to begin playing something.


# Logging and Interfaces #

ApiClient and ApiWebSocket both have additional constructors available allowing you to pass in your own implementation of ILogger. The default implementation is NullLogger, which provides no logging. In addition you can also pass in your own implementation of IJsonSerializer, or use our NewtonsoftJsonSerializer. ClientWebSocketFactory also has an additional overload allowing you to pass in your own ILogger