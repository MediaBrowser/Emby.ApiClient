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

Once you have an ApiClient instance, you can easily connect to the server's web socket using:

``` c#

            ApiClient.OpenWebSocket();
```

This will open the connection in a background thread, and periodically check to ensure it's still connected. This will provide various events that can be used to receive notifications from the server:


``` c#

            ApiClient.UserUpdated += webSocket_UserUpdated;
```

# Logging and Interfaces #

ApiClient has additional constructors available allowing you to pass in your own implementation of ILogger. The default implementation is NullLogger, which provides no logging. In addition you can also pass in your own implementation of IJsonSerializer, or use our NewtonsoftJsonSerializer. ClientWebSocketFactory also has an additional overload allowing you to pass in your own ILogger
