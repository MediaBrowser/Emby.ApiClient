MediaBrowser.ApiClient
======================

This portable class library makes it very easy to harness the power of the Media Browser API.

This is available as a Nuget package:

[https://www.nuget.org/packages/MediaBrowser.ApiClient/](https://www.nuget.org/packages/MediaBrowser.ApiClient/)

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

To add logging support, simply implement the ILogger interface and set the Logger property. The client also allows configuration of http compression and caching policies.

After authentication you'll need to set the CurrentUserId property, and you'll need to update that value anytime the user changes (or logs out).