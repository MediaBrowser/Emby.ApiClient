MediaBrowser.ApiClient
======================

This portable class library makes it very easy to harness the power of the Media Browser API.

Usage is very simple:

            var client = new ApiClient
            {
                ServerHostName = "localhost",
                ServerApiPort = 8096
            };

            // Get all users
            var users = await client.GetAllUsersAsync();

            var currentUser = users.First();

            // Get the ten most recently added items for
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

To add logging support, simply implement the ILogger interface and pass that into the constructor. The client also allows configuration of http compression and caching policies.