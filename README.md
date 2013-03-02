MediaBrowser.ApiClient
======================

This portable class library makes it very easy to harness the power of the Media Browser API.

Usage is very simple:

            var client = new ApiClient
            {
                ServerHostName = "localhost",
                ServerApiPort = 8096
            };

            var users = await client.GetAllUsersAsync();

To add logging support, simply implement the ILogger interface and pass that into the constructor. The client also allows configuration of http compression and caching policies.