MediaBrowser.ApiClient
======================

This portable class library makes it very easy to harness the power of the Media Browser API. This is available as a Nuget package:

[MediaBrowser.ApiClient](https://www.nuget.org/packages/MediaBrowser.ApiClient/)

# Single Server Example #

This is an example of connecting to a single server using a fixed, predictable address, from an app that has user-specific features.

``` c#

            // Developers are encouraged to create their own ILogger implementation
			var logger = new NullLogger();

			// This describes the device capabilities
			var capabilities = new ClientCapabilities();

			// If using the portable class library you'll need to supply your own IDevice implementation.
			var device = new Device
            {
                DeviceName = "My Device Name",
                DeviceId = "My Device Id"
            };
			
			var client = new ApiClient(logger, "http://localhost:8096", "My client name", device, capabilities);

			var authResult = await AuthenticateUserAsync("username", passwordHash);

			// RemoteLoggedOut indicates the user was logged out remotely by the server
			ApiClient.RemoteLoggedOut += ApiClient_RemoteLoggedOut;

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

            // Developers are encouraged to create their own ILogger implementation
			var logger = new NullLogger();

			var client = new ApiClient(logger, "http://localhost:8096", "0123456789");

			// RemoteLoggedOut indicates the access token was revoked remotely by the server
			ApiClient.RemoteLoggedOut += ApiClient_RemoteLoggedOut;

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

Once you have an ApiClient instance, you can easily connect to the server's web socket using:

``` c#

            ApiClient.OpenWebSocket();
```

This will open a connection in a background thread, and periodically check to ensure it's still connected. The web socket provides various events that can be used to receive notifications from the server:


``` c#

            ApiClient.UserUpdated += webSocket_UserUpdated;
```

# Multi-Server Usage #


The above examples are designed for cases when your app always connects to a single server, and you always know the address. An example is an app that will always run within a local network and only connect to one server at a time. If your app is designed to support multiple networks and/or multiple servers, then **IConnectionManager** should be used in place of the above example.


``` c#

            // Developers are encouraged to create their own ILogger implementation
			var logger = new NullLogger();

			// This describes the device capabilities
			var capabilities = new ClientCapabilities();

			// If using the portable class library you'll need to supply your own IDevice implementation.
			var device = new Device
            {
                DeviceName = "My Device Name",
                DeviceId = "My Device Id"
            };
			
			// Developers will have to implement ICredentialProvider to provide storage for saving server information
			var credentialProvider = new CredentialProvider();

			// If using the portable class library you'll need to supply your own INetworkConnection implementation.
			var networkConnection = new NetworkConnection(logger);

            // If using the portable class library you'll need to supply your own IServerLocator implementation.
			var serverLocator = new ServerLocator(logger);

            var connectionManager = new ConnectionManager(logger,
                credentialProvider,
                networkConnection,
                serverLocator,
                "My App Name",
				// Application version
                "1.0.0.0",
                device,
                capabilities,
                ClientWebSocketFactory.CreateWebSocket);

			// RemoteLoggedOut indicates the user was logged out remotely by the server
			// Will be explained below
			connectionManager.RemoteLoggedOut += connectionManager_RemoteLoggedOut;          
```

# Multi-Server Startup Workflow #

After you've created your instance of IConnectionManager, simply call the Connect method. It will return a result object with three properties:

- State
- Servers
- ApiClient

ServerInfo and ApiClient will be null if State == Unavailable. If State==SignedIn or State==ServerSignIn, the Servers list will always have one single entry. Let's look at an example.


``` c#

            var result = await connectionManager.Connect(cancellationToken);

			switch (result.State)
			{
				case ConnectionState.Unavailable:
					// No servers found. User must manually enter connection info.

				case ConnectionState.ServerSignIn:
					// A server was found and the user needs to login.
					// Display a login screen and authenticate with the server using result.ApiClient

				case ConnectionState.ServerSelection:
					// Multiple servers available
					// Display a selection screen

				case ConnectionState.SignedIn:
					// A server was found and the user has been signed in using previously saved credentials.
					// Ready to browse using result.ApiClient
			}

```

If the user wishes to connect to a new server, simply use the Connect overload that accepts an address.


``` c#

			var address = "http://localhost:8096";

            var result = await connectionManager.Connect(address, cancellationToken);

			// Proceed with same switch statement as above example

```

Similarly, if the user selects a server from the selection screen, use the overload that accepts a ServerInfo instance. When the user wishes to logout of the individual server, simply call apiClient.Logout as normal.

If at anytime the RemoteLoggedOut event is fired, simply start the workflow all over again by calling connectionManager.Connect(cancellationToken).

ConnectionManager will handle opening and closing web socket connections at the appropiate times. All your app needs to do is use an ApiClient instance to subscribe to individual events.


``` c#

            ApiClient.UserUpdated += webSocket_UserUpdated;
```

With multi-server connectivity it is not recommended to keep a global ApiClient instance, or pass an ApiClient around the application. Instead keep a factory that will resolve the appropiate ApiClient instance depending on context. In order to help with this, ConnectionManager has a GetApiClient method that accepts a BaseItemDto and returns an ApiClient from the server it belongs to.